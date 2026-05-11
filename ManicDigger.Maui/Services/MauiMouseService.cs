#if WINDOWS
using Microsoft.Maui.Platform;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input; 
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.UI.Core;
using WinRT;
using Application = Microsoft.Maui.Controls.Application;
using Window = Microsoft.Maui.Controls.Window;

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

    private const uint OCR_NORMAL = 32512; // arrow cursor id
    private const uint SPI_SETCURSORS = 0x0057; // restore all cursors to default

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    public void CaptureMouse()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Create 1x1 invisible cursor
            IntPtr invisible = CreateCursor(
                IntPtr.Zero, 0, 0, 1, 1,
                new byte[] { 0xFF }, // AND mask — fully transparent
                new byte[] { 0x00 });// XOR mask — no pixels

            // Replace the system arrow cursor globally
            SetSystemCursor(invisible, OCR_NORMAL);

            ClipToWindow();

            System.Diagnostics.Debug.WriteLine("[CaptureMouse] system cursor replaced");
        });
    }

    public void ClipToWindow()
    {
        IntPtr hwnd = GetMauiHwnd();
        if (hwnd == IntPtr.Zero) return;

        GetWindowRect(hwnd, out RECT rect);
        bool ok = ClipCursor(ref rect);
        System.Diagnostics.Debug.WriteLine(
            $"[ClipToWindow] hwnd={hwnd} rect={rect.Left},{rect.Top},{rect.Right},{rect.Bottom} ok={ok}");
    }

    public void RecenterCursor()
    {
        IntPtr hwnd = GetMauiHwnd();
        if (hwnd == IntPtr.Zero) return;

        GetWindowRect(hwnd, out RECT rect);
        int cx = (rect.Left + rect.Right) / 2;
        int cy = (rect.Top + rect.Bottom) / 2;
        SetCursorPos(cx, cy);
    }

    public void ReleaseMouse()
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            // Restore ALL system cursors to Windows defaults in one call
            SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
            ClipCursor(IntPtr.Zero);

            System.Diagnostics.Debug.WriteLine("[ReleaseMouse] system cursors restored");
        });
    }

    private IntPtr GetMauiHwnd()
    {
        var window = Application.Current?.Windows[0].Handler?.PlatformView
            as Microsoft.UI.Xaml.Window;
        if (window == null) return IntPtr.Zero;
        return WinRT.Interop.WindowNative.GetWindowHandle(window);
    }
}

#endif