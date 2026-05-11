#if WINDOWS
using Microsoft.Maui.Platform;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Input; 
using System.Reflection;
using System.Runtime.InteropServices;
using Windows.UI.Core;

namespace ManicDigger.Maui.Services;

public partial class MauiGameWindowService : IGameWindowService
{
    [DllImport("user32.dll")] private static extern bool ClipCursor(ref RECT lpRect);
    [DllImport("user32.dll")] private static extern bool ClipCursor(IntPtr lpRect);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    public void CaptureMouse(IMauiContext? mauiContext)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (mauiContext == null) return;
            _view?.HideCursor(mauiContext);

            // Still clip so cursor can't escape the window
            IntPtr hwnd = GetForegroundWindow();
            GetWindowRect(hwnd, out RECT rect);
            ClipCursor(ref rect);
        });
    }

}

public static class CursorExtensions
{
    public static void HideCursor(this VisualElement visualElement, IMauiContext? mauiContext)
    {
        ArgumentNullException.ThrowIfNull(mauiContext);
        UIElement view = visualElement.ToPlatform(mauiContext);
        view.ChangeCursor(null); // null = hidden
    }

    public static void ShowCursor(this VisualElement visualElement, IMauiContext? mauiContext)
    {
        ArgumentNullException.ThrowIfNull(mauiContext);
        UIElement view = visualElement.ToPlatform(mauiContext);
        view.ChangeCursor(InputCursor.CreateFromCoreCursor(
            new CoreCursor(CoreCursorType.Arrow, 1)));
    }

    private static void ChangeCursor(this UIElement uiElement, InputCursor? cursor)
    {
        typeof(UIElement).InvokeMember(
            "ProtectedCursor",
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.SetProperty | BindingFlags.Instance,
            null, uiElement, [cursor]);
    }
}
#endif