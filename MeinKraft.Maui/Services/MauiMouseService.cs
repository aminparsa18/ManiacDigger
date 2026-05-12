#if WINDOWS
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Runtime.InteropServices;
using Application = Microsoft.Maui.Controls.Application;

namespace MeinKraft.Maui.Services;

public partial class MauiGameWindowService : IGameWindowService
{
    // -------------------------------------------------------------------------
    // P/Invoke
    // -------------------------------------------------------------------------

    [DllImport("user32.dll")] private static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] andPlane, byte[] xorPlane);
    [DllImport("user32.dll")] private static extern bool SetSystemCursor(IntPtr hCursor, uint id);
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
    [DllImport("user32.dll")] private static extern bool ClipCursor(IntPtr lpRect);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate proc);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr proc);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr prev, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const uint OCR_NORMAL = 32512;
    private const uint SPI_SETCURSORS = 0x0057;
    private const int GWL_WNDPROC = -4;

    // WM_ messages
    private const uint WM_INPUT = 0x00FF;

    // Raw input
    private const uint RID_INPUT = 0x10000003;
    private const uint RIM_TYPEMOUSE = 0;

    // -------------------------------------------------------------------------
    // Structs
    // -------------------------------------------------------------------------

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTDEVICE
    {
        public ushort UsagePage;
        public ushort Usage;
        public uint Flags;
        public IntPtr Target;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUTHEADER
    {
        public uint Type;
        public uint Size;
        public IntPtr Device;
        public IntPtr wParam;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWMOUSE
    {
        public ushort Flags;
        public ushort ButtonFlags;  // was: uint Buttons (low word is flags)
        public ushort ButtonData;   // high word — wheel delta for scroll
        public uint RawButtons;
        public int LastX;
        public int LastY;
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER Header;
        public RAWMOUSE Mouse;
    }

    // -------------------------------------------------------------------------
    // State
    // -------------------------------------------------------------------------

    private WndProcDelegate? _wndProc;
    private IntPtr _oldWndProc = IntPtr.Zero;

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>Raw mouse movement delta (dx, dy) from WM_INPUT.</summary>
    public event Action<int, int>? RawMouseDelta;

    /// <summary>Mouse button pressed. Args: (button, x, y) in client coords.</summary>
    public event Action<MouseButton, int, int>? RawMouseDown;

    /// <summary>Mouse button released. Args: (button, x, y) in client coords.</summary>
    public event Action<MouseButton, int, int>? RawMouseUp;

    public void StartRawInput(IntPtr hwnd)
    {
        RAWINPUTDEVICE[] rid =
        [
            new RAWINPUTDEVICE
            {
                UsagePage = 0x01, // Generic Desktop
                Usage     = 0x02, // Mouse
                Flags     = 0,
                Target    = hwnd
            }
        ];

        RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());

        _wndProc = WndProc;
        _oldWndProc = SetWindowLongPtr(hwnd, GWL_WNDPROC, _wndProc);

        System.Diagnostics.Debug.WriteLine($"[RawInput] registered hwnd={hwnd} oldWndProc={_oldWndProc}");
    }

    public void StopRawInput(IntPtr hwnd)
    {
        if (_oldWndProc == IntPtr.Zero)
        {
            return;
        }

        SetWindowLongPtr(hwnd, GWL_WNDPROC, _oldWndProc);
        _oldWndProc = IntPtr.Zero;
    }

    public void TrapCursorInCenter()
    {
        if (!_mousePointerLocked)
        {
            return;
        }

        IntPtr hwnd = GetMauiHwnd();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        GetWindowRect(hwnd, out RECT rect);
        SetCursorPos((rect.Left + rect.Right) / 2, (rect.Top + rect.Bottom) / 2);
    }

    public void CaptureCursor()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Create 1x1 invisible cursor
            IntPtr invisible = CreateCursor(
                IntPtr.Zero, 0, 0, 1, 1,
                [0xFF], // AND mask — fully transparent
                [0x00]);// XOR mask — no pixels

            // Replace the system arrow cursor globally
            SetSystemCursor(invisible, OCR_NORMAL);
        });
    }

    public void ReleaseCursor()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Restore ALL system cursors to Windows defaults in one call
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            ClipCursor(IntPtr.Zero);
        });
    }

    // -------------------------------------------------------------------------
    // WndProc
    // -------------------------------------------------------------------------

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            case WM_INPUT:
                HandleRawInput(lParam);
                break;
        }

        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    // Button flag constants

    private void HandleRawInput(IntPtr lParam)
    {
        uint size = 0;
        GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());

        IntPtr buf = Marshal.AllocHGlobal((int)size);
        try
        {
            GetRawInputData(lParam, RID_INPUT, buf, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
            RAWINPUT raw = Marshal.PtrToStructure<RAWINPUT>(buf);

            if (raw.Header.Type != RIM_TYPEMOUSE)
            {
                return;
            }

            // Mouse movement
            if (_isFocused && (raw.Mouse.LastX != 0 || raw.Mouse.LastY != 0))
            {
                RawMouseDelta?.Invoke(raw.Mouse.LastX, raw.Mouse.LastY);
            }

            // Mouse buttons — always fire regardless of focus
            ushort flags = raw.Mouse.ButtonFlags;
        }
        finally
        {
            Marshal.FreeHGlobal(buf);
        }
    }

    private static IntPtr GetMauiHwnd() 
        => Application.Current?.Windows[0].Handler?.PlatformView is not Microsoft.UI.Xaml.Window window ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(window);
}

#endif