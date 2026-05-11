#if WINDOWS
using System.Runtime.InteropServices;
using Application = Microsoft.Maui.Controls.Application;

namespace ManicDigger.Maui.Services;

public partial class MauiGameWindowService : IGameWindowService
{
    [DllImport("user32.dll")] private static extern IntPtr CreateCursor(IntPtr hInst, int xHotSpot, int yHotSpot, int nWidth, int nHeight, byte[] andPlane, byte[] xorPlane);
    [DllImport("user32.dll")] private static extern bool SetSystemCursor(IntPtr hCursor, uint id);
    [DllImport("user32.dll")] private static extern bool SystemParametersInfo(uint uiAction, uint uiParam, IntPtr pvParam, uint fWinIni);
    [DllImport("user32.dll")] private static extern bool ClipCursor(ref RECT lpRect);
    [DllImport("user32.dll")] private static extern bool ClipCursor(IntPtr lpRect);
    [DllImport("user32.dll")] private static extern bool SetCursorPos(int x, int y);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern bool RegisterRawInputDevices(RAWINPUTDEVICE[] devices, uint count, uint size);
    [DllImport("user32.dll")] private static extern uint GetRawInputData(IntPtr hRawInput, uint command, IntPtr data, ref uint size, uint headerSize);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, WndProcDelegate proc);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr prev, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const uint OCR_NORMAL = 32512; // arrow cursor id
    private const uint SPI_SETCURSORS = 0x0057; // restore all cursors to default
    private const int GWL_WNDPROC = -4;
    private const uint WM_INPUT = 0x00FF;
    private const uint RID_INPUT = 0x10000003;
    private const uint RIM_TYPEMOUSE = 0;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }
   
    public void RecenterCursor()
    {
        if (!_mousePointerLocked)
            return;
        IntPtr hwnd = GetMauiHwnd();
        if (hwnd == IntPtr.Zero)
        {
            return;
        }

        GetWindowRect(hwnd, out RECT rect);
        int cx = (rect.Left + rect.Right) / 2;
        int cy = (rect.Top + rect.Bottom) / 2;
        SetCursorPos(cx, cy);
    }

    private static IntPtr GetMauiHwnd()
    {
        Microsoft.UI.Xaml.Window? window = Application.Current?.Windows[0].Handler?.PlatformView
            as Microsoft.UI.Xaml.Window;
        return window == null ? IntPtr.Zero : WinRT.Interop.WindowNative.GetWindowHandle(window);
    }

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
        public uint Buttons;
        public uint RawButtons;
        public int LastX;   // ← hardware delta X
        public int LastY;   // ← hardware delta Y
        public uint ExtraInformation;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct RAWINPUT
    {
        public RAWINPUTHEADER Header;
        public RAWMOUSE Mouse;
    }

    private WndProcDelegate _wndProc;
    private IntPtr _oldWndProc = IntPtr.Zero;

    // Called from GameView after GL is initialized
    public void StartRawInput(IntPtr hwnd)
    {
        // Register for raw mouse input
        var rid = new RAWINPUTDEVICE[]
        {
        new RAWINPUTDEVICE
        {
            UsagePage = 0x01, // generic desktop
            Usage     = 0x02, // mouse
            Flags     = 0,
            Target    = hwnd
        }
        };
        RegisterRawInputDevices(rid, 1, (uint)Marshal.SizeOf<RAWINPUTDEVICE>());

        // Subclass window to intercept WM_INPUT
        _wndProc = WndProc;
        _oldWndProc = SetWindowLongPtr(hwnd, GWL_WNDPROC, _wndProc);

        System.Diagnostics.Debug.WriteLine($"[RawInput] registered hwnd={hwnd} old={_oldWndProc}");
    }

    public void StopRawInput(IntPtr hwnd)
    {
        if (_oldWndProc == IntPtr.Zero)
        {
            return;
        }

        SetWindowLongPtr(hwnd, GWL_WNDPROC,
            Marshal.GetDelegateForFunctionPointer<WndProcDelegate>(_oldWndProc));
        _oldWndProc = IntPtr.Zero;
    }

    // Fired on raw mouse movement — post to MAUI thread
    public event Action<int, int>? RawMouseDelta;

    private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WM_INPUT)
        {
            uint size = 0;
            GetRawInputData(lParam, RID_INPUT, IntPtr.Zero, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());

            IntPtr buf = Marshal.AllocHGlobal((int)size);
            try
            {
                GetRawInputData(lParam, RID_INPUT, buf, ref size, (uint)Marshal.SizeOf<RAWINPUTHEADER>());
                var raw = Marshal.PtrToStructure<RAWINPUT>(buf);

                if (raw.Header.Type == RIM_TYPEMOUSE && _isFocused)
                {
                    int dx = raw.Mouse.LastX;
                    int dy = raw.Mouse.LastY;
                    if (dx != 0 || dy != 0)
                    {
                        RawMouseDelta?.Invoke(dx, dy);
                    }
                }
            }
            finally
            {
                Marshal.FreeHGlobal(buf);
            }
        }
        return CallWindowProc(_oldWndProc, hWnd, msg, wParam, lParam);
    }
}

#endif