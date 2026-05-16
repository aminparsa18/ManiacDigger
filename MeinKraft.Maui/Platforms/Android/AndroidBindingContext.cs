using System;
using System.Collections.Generic;
using System.Text;

public class AndroidBindingsContext : IBindingsContext
{
    private readonly IntPtr _libHandle;

    public AndroidBindingsContext()
    {
        _libHandle = NativeLibrary.Load("libGLESv2.so");
    }

    public IntPtr GetProcAddress(string procName)
    {
        if (NativeLibrary.TryGetExport(_libHandle, procName, out IntPtr ptr))
            return ptr;

        return IntPtr.Zero;
    }
}