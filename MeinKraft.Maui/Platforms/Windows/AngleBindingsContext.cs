// MeinKraft.Maui/Platforms/Windows/AngleBindingsContext.cs
//
// Resolves OpenGL ES 3.0 function pointers from ANGLE (libEGL.dll + libGLESv2.dll).
// Pass an instance to GL.LoadBindings() on the first frame tick, once the
// SKGLView EGL context is guaranteed current.
//
// Resolution order:
//   1. eglGetProcAddress  — extension functions + most ES3 core functions
//   2. GetProcAddress(libGLESv2.dll) — core ES functions that are DLL exports
//      (some drivers return null from eglGetProcAddress for core entry points)

using OpenTK;
using System.Runtime.InteropServices;

namespace MeinKraft.Maui.Platforms.Windows;

public sealed class AngleBindingsContext : IBindingsContext
{
    private static readonly IntPtr _glesModule;

    static AngleBindingsContext()
    {
        _glesModule = LoadLibrary("libGLESv2.dll");
    }

    /// <inheritdoc/>
    public IntPtr GetProcAddress(string procName)
    {
        // Try eglGetProcAddress first — works for extensions and
        // most core ES3 functions on ANGLE.
        IntPtr ptr = EglGetProcAddress(procName);

        // eglGetProcAddress can return non-null sentinel values (1, 2, 3)
        // for unrecognised names on some ANGLE builds — treat those as failure.
        if (ptr == IntPtr.Zero || (nint)ptr is 1 or 2 or 3)
        {
            // Fall back to the libGLESv2 export table for guaranteed
            // core entry points (glCreateShader, glBindBuffer, etc.)
            ptr = _glesModule != IntPtr.Zero
                ? GetProcAddressFromModule(_glesModule, procName)
                : IntPtr.Zero;
        }

        return ptr;
    }

    // ── P/Invoke ──────────────────────────────────────────────────────────────

    [DllImport("libEGL.dll", EntryPoint = "eglGetProcAddress", CharSet = CharSet.Ansi)]
    private static extern IntPtr EglGetProcAddress(string procName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern IntPtr LoadLibrary(string lpFileName);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddressFromModule(IntPtr hModule, string lpProcName);
}