// ManicDigger.Maui/Platforms/Windows/MauiGameWindowService.cs
//
// MAUI-specific implementation of IGameWindowService.
// SkiaSharp's SKGLView owns the EGL/ANGLE context — we never touch EGL directly.
//
// Attach(GameSKGLView) is called from GameView.OnAppearing.
// Detach() is called from GameView.OnDisappearing.

using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using System.Diagnostics;
using System.Drawing;
using System.Text.RegularExpressions;
using SkiaSharp.Views.Maui.Controls;


#if WINDOWS
using ManicDigger.Maui.Platforms.Windows;
using OpenTK.Graphics.ES30;
using Windows.UI.Core;
#endif

namespace ManicDigger.Maui.Services;

public sealed partial class MauiGameWindowService : IGameWindowService
{
    // ── Dependencies ──────────────────────────────────────────────────────────

    private readonly IGameExitService _gameExit;
    private readonly CrashReporter _crashReporter;
    private readonly IDisplayService _displayService;
    private readonly IOpenGlService _openGlService;

    // ── Late-bound view ───────────────────────────────────────────────────────

    private SKGLView? _view;

    // ── Timing ────────────────────────────────────────────────────────────────

    private readonly Stopwatch _start = Stopwatch.StartNew();
    public int TimeMillisecondsFromStart => (int)_start.ElapsedMilliseconds;

    // ── Canvas ────────────────────────────────────────────────────────────────

    public int CanvasWidth => (int)(_view?.Width ?? 0);
    public int CanvasHeight => (int)(_view?.Height ?? 0);

    // ── Constructor ───────────────────────────────────────────────────────────

    public MauiGameWindowService(
        IGameExitService gameExit,
        IDisplayService displayService,
        IOpenGlService openGlService,
        CrashReporter crashReporter)
    {
        _gameExit = gameExit;
        _displayService = displayService;
        _crashReporter = crashReporter;
        _openGlService = openGlService;

        _crashReporter.SetCursorVisible = MouseCursorSetVisible;
        _crashReporter.ShowErrorDialog = MessageBoxShowError;

        ThreadPool.SetMinThreads(32, 32);
        ThreadPool.SetMaxThreads(128, 128);
    }

    // ── View lifecycle ────────────────────────────────────────────────────────

    private bool _isFocused = false;
    /// <summary>
    /// Called from GameView.OnAppearing once the SKGLView is in the visual tree.
    /// SkiaSharp has already created the EGL/ANGLE context at this point.
    /// </summary>
    public void Attach(SKGLView view)
    {
        _view = view;
        view.EnableTouchEvents = true;
        view.Touch += OnSkiaTouch;
        _isFocused = true;
        _initialised = false;
    }

    /// <summary>
    /// Called from GameView.OnDisappearing.
    /// </summary>
    public void Detach()
    {
        if (_view is null) return;
        _view.Touch -= OnSkiaTouch;
        _view = null;
    }

    // ── Frame dispatch (called by GameSKGLView) ───────────────────────────────

    private bool _initialised;

    // ── IGameWindowService — render loop ──────────────────────────────────────

    /// <summary>No-op — render loop is driven by SKGLView.InvalidateSurface().</summary>
    public void Start()
    {
    }

    private readonly List<Action<float>> _newFrameHandlers = [];
    public void AddOnNewFrame(Action<float> handler) => _newFrameHandlers.Add(handler);
    public void RemoveOnNewFrame(Action<float> handler) => _newFrameHandlers.Remove(handler);

    // ── IGameWindowService — input ────────────────────────────────────────────

    public List<Action<KeyEventArgs>> KeyDownHandlers { get; set; } = [];
    public List<Action<KeyEventArgs>> KeyUpHandlers { get; set; } = [];
    public List<Action<KeyPressEventArgs>> KeyPressHandlers { get; set; } = [];

    public event Action<MouseEventArgs>? OnMouseDown;
    public event Action<MouseEventArgs>? OnMouseUp;
    public event Action<MouseEventArgs>? OnMouseMove;
    public event Action<float>? OnMouseWheel;
    public event Action<TouchEventArgs>? OnTouchStart;
    public event Action<TouchEventArgs>? OnTouchMove;
    public event Action<TouchEventArgs>? OnTouchEnd;

    public delegate void GameKeyEventHandler(KeyEventArgs e);
    public List<GameKeyEventHandler> keyEventHandlers { get; set; } = [];
    public void AddOnKeyEvent(GameKeyEventHandler handler) => keyEventHandlers.Add(handler);

    public void AddOnKeyEvent(
        Action<KeyEventArgs> onKeyDown,
        Action<KeyEventArgs> onKeyUp,
        Action<KeyPressEventArgs> onKeyPress)
    {
        KeyDownHandlers.Add(onKeyDown);
        KeyUpHandlers.Add(onKeyUp);
        KeyPressHandlers.Add(onKeyPress);
    }

    public void AddOnMouseEvent(
        Action<MouseEventArgs> onMouseDown,
        Action<MouseEventArgs> onMouseUp,
        Action<MouseEventArgs> onMouseMove,
        Action<float> onMouseWheel)
    {
        OnMouseDown += onMouseDown;
        OnMouseUp += onMouseUp;
        OnMouseMove += onMouseMove;
        OnMouseWheel += onMouseWheel;
    }

    public void AddOnTouchEvent(
        Action<TouchEventArgs> onTouchStart,
        Action<TouchEventArgs> onTouchMove,
        Action<TouchEventArgs> onTouchEnd)
    {
        OnTouchStart += onTouchStart;
        OnTouchMove += onTouchMove;
        OnTouchEnd += onTouchEnd;
    }

    public void AddOnCrash(OnCrashHandler handler) => _crashReporter.OnCrash += handler.OnCrash;

    // ── Touch/mouse from SKGLView ─────────────────────────────────────────────

    public bool TouchTest = false;

    private void OnSkiaTouch(object? sender, SkiaSharp.Views.Maui.SKTouchEventArgs e)
    {
        e.Handled = true;

        switch (e.ActionType)
        {
            case SkiaSharp.Views.Maui.SKTouchAction.Pressed:
                if (TouchTest)
                {
                    var a = new TouchEventArgs();
                    a.SetX((int)e.Location.X); a.SetY((int)e.Location.Y); a.SetId((int)e.Id);
                    OnTouchStart?.Invoke(a);
                }
                else
                {
                    var a = new MouseEventArgs();
                    a.X = ((int)e.Location.X); a.Y = (int)e.Location.Y;
                    a.                    Button = e.MouseButton == SkiaSharp.Views.Maui.SKMouseButton.Right ? 1 : 0;
                    OnMouseDown?.Invoke(a);
                }
                break;

            case SkiaSharp.Views.Maui.SKTouchAction.Released:
                if (TouchTest)
                {
                    var a = new TouchEventArgs();
                    a.SetX((int)e.Location.X); a.SetY((int)e.Location.Y); a.SetId((int)e.Id);
                    OnTouchEnd?.Invoke(a);
                }
                else
                {
                    var a = new MouseEventArgs();
                    a.X = ((int)e.Location.X); a.Y = (int)e.Location.Y;
                    OnMouseUp?.Invoke(a);
                }
                break;

            case SkiaSharp.Views.Maui.SKTouchAction.Moved:
                if (TouchTest)
                {
                    var a = new TouchEventArgs();
                    a.SetX((int)e.Location.X); a.SetY((int)e.Location.Y); a.SetId((int)e.Id);
                    OnTouchMove?.Invoke(a);
                }
                else
                {
                    var a = new MouseEventArgs();
                    a.X = ((int)e.Location.X); a.Y = (int)e.Location.Y;
                    OnMouseMove?.Invoke(a);
                }
                break;

            case SkiaSharp.Views.Maui.SKTouchAction.WheelChanged:
                OnMouseWheel?.Invoke(e.WheelDelta / 120f);
                break;
        }
    }

    // ── Keyboard — raised from GameView code-behind ───────────────────────────

    public void RaiseKeyDown(KeyEventArgs args)
    {
        foreach (var h in KeyDownHandlers) { h(args); if (args.Handled) break; }
    }

    public void RaiseKeyUp(KeyEventArgs args)
    {
        foreach (var h in KeyUpHandlers) { h(args); if (args.Handled) break; }
    }

    public void RaiseKeyPress(KeyPressEventArgs args)
    {
        foreach (var h in KeyPressHandlers) { h(args); if (args.Handled) break; }
    }

    // ── Cursor / focus ────────────────────────────────────────────────────────

    private bool _mousePointerLocked;
    private bool _mouseCursorVisible = true;

    public bool IsMousePointerLocked() => _mousePointerLocked;
    public bool MouseCursorIsVisible() => _mouseCursorVisible;
    public bool Focused() => _isFocused;

    public void MouseCursorSetVisible(bool value)
    {
        _mouseCursorVisible = value;
        if (_mouseCursorVisible)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                // Restore ALL system cursors to Windows defaults in one call
                SystemParametersInfo(SPI_SETCURSORS, 0, IntPtr.Zero, 0);
                ClipCursor(IntPtr.Zero);
            });
        }
        else
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
    }

    public void RequestMousePointerLock() { MouseCursorSetVisible(false); _mousePointerLocked = true; }
    public void ExitMousePointerLock() { MouseCursorSetVisible(true); _mousePointerLocked = false; }

    public void SetWindowCursor(int hotx, int hoty, int sizex, int sizey,
        byte[] imgdata, int imgdataLength)
    { }
    public void RestoreWindowCursor() => MouseCursorSetVisible(true);

    // ── Window ────────────────────────────────────────────────────────────────

    public void SetTitle(string title)
    {
        var win = Microsoft.Maui.Controls.Application.Current?.Windows[0];
#if WINDOWS
        if (win?.Handler?.PlatformView is Microsoft.UI.Xaml.Window w) w.Title = title;
#endif
    }

    public void SetVSync(bool enabled) { }
    public void WindowExit() { _gameExit.Exit = true; Microsoft.Maui.Controls.Application.Current?.Quit(); }
    public WindowState GetWindowState() => WindowState.Normal;
    public void SetWindowState(WindowState value) { }
    public void ChangeResolution(int w, int h, int bpp, float refresh) { }

    public List<DisplayResolutionCi> GetDisplayResolutions()
        => _displayService.GetDisplayResolutions()
            .Where(r => r.Width >= 800 && r.Height >= 600 && r.BitsPerPixel >= 16)
            .Select(r => new DisplayResolutionCi
            {
                Width = r.Width,
                Height = r.Height,
                BitsPerPixel = r.BitsPerPixel,
                RefreshRate = r.RefreshRate,
            }).ToList();

    public DisplayResolutionCi GetDisplayResolutionDefault()
        => _displayService.GetDisplayResolutionDefault();

    public string KeyName(int key)
        => Enum.IsDefined(typeof(OpenTK.Windowing.GraphicsLibraryFramework.Keys), key)
            ? Enum.GetName(typeof(OpenTK.Windowing.GraphicsLibraryFramework.Keys), key)!
            : key.ToString();

    // ── Misc ──────────────────────────────────────────────────────────────────

    public string StoragePath => GameStorePath.GetStorePath();
    public string GameSavePath => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    public string GameLogsPath => Path.Combine(StoragePath, "Logs");

    public INetworkService NetworkService { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
    public GameWindow Window { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

    public string GetGameVersion() => GameVersion.Version;
    public bool IsFastSystem() => true;
    public bool MultithreadingAvailable() => true;
    public bool IsSmallScreen() => TouchTest;
    public bool IsDebuggerAttached() => Debugger.IsAttached;
    public string QueryStringValue(string key) => null;
    public void ApplicationDoEvents() { }
    public void ShowKeyboard(bool show) { }
    public void QueueUserWorkItem(Action action) => ThreadPool.QueueUserWorkItem(_ => action());
    public void OpenLinkInBrowser(string url)
    {
        if (url.StartsWith("http://") || url.StartsWith("https://")) Process.Start(url);
    }

    public void MessageBoxShowError(string text, string caption)
    {
        if (OperatingSystem.IsWindows())
            Application.Current.MainPage.DisplayAlert(caption, text, "OK");
    }

    public bool ChatLog(string servername, string p)
    {
        Directory.CreateDirectory(GameLogsPath);
        File.AppendAllText(
            Path.Combine(GameLogsPath, SanitiseFileName(servername) + ".txt"),
            $"{DateTime.Now} {p}\n");
        return true;
    }

    private static string SanitiseFileName(string name)
        => Regex.Replace(name, $"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]", "_");

    public void SaveScreenshot() { }
    public Bitmap GrabScreenshot() => new(1, 1);
    public Stream? OpenIconStream()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "md.ico");
        return File.Exists(path) ? File.OpenRead(path) : null;
    }

    public string Cachepath() => Path.Combine(StoragePath, "Cache");
    public void Checkcachedir() => Directory.CreateDirectory(Cachepath());

    public void SaveAssetToCache(Asset tosave)
    {
        Checkcachedir();
        using BinaryWriter bw = new(File.Create(Path.Combine(Cachepath(), tosave.md5)));
        bw.Write(tosave.name); bw.Write(tosave.dataLength); bw.Write(tosave.data);
    }

    public Asset LoadAssetFromCache(string md5)
    {
        using BinaryReader br = new(File.OpenRead(Path.Combine(Cachepath(), md5)));
        string name = br.ReadString(); int len = br.ReadInt32(); byte[] data = br.ReadBytes(len);
        return new Asset { data = data, dataLength = len, md5 = md5, name = name };
    }

    public bool IsCached(string md5)
        => Directory.Exists(Cachepath()) && File.Exists(Path.Combine(Cachepath(), md5));
}