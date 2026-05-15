using MeinKraft.Maui.Services;
using MeinKraft.Worker;
using OpenTK.Graphics.ES30;
using SkiaSharp.Views.Maui;
using System.Runtime.InteropServices;
using OpenTK;

#if WINDOWS
using Application = Microsoft.Maui.Controls.Application;
using Microsoft.UI.Xaml.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Windowing;
using Microsoft.UI;
#endif

namespace MeinKraft.Maui.Views;

public partial class GameView : ContentPage
{
    private bool _glInitialized = false;
    private IDispatcherTimer _gameLoopTimer;
    private DateTime _lastFrame = DateTime.UtcNow;

    private readonly IGame _game;
    private readonly IOpenGlService _openGlService;
    private readonly IGameWindowService _gameWindowService;
    private readonly IAssetManager _assetManager;
    private readonly ClientWorkerHost _workerHost;

#if WINDOWS
    [DllImport("libEGL.dll")]
    private static extern IntPtr eglGetProcAddress(string procName);

    private class AngleBindingsContext : OpenTK.IBindingsContext
    {
        public IntPtr GetProcAddress(string procName) => eglGetProcAddress(procName);
    }

#elif ANDROID
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
#endif

    public GameView(IOpenGlService openGlService, IGameWindowService gameWindowService, IAssetManager assetManager,
        IGame game, ITerrainChunkTesselator terrainChunkTesselator, ClientWorkerHost workerHost)
    {
        InitializeComponent();
        _openGlService = openGlService;
        _gameWindowService = gameWindowService;
        _assetManager = assetManager;
        _game = game;
        _workerHost = workerHost;

        // Inject game services into the overlay so it can apply options directly.
        // Must happen after InitializeComponent() so OverlayMenu is already created.
        OverlayMenu.Initialize(_game, terrainChunkTesselator);

        // Wire up overlay exit events. OverlayMenuView owns its internal navigation
        // (Pause ↔ Options); GameView only handles the two exit points that
        // require cursor and game-state changes, plus the platform fullscreen call.
        OverlayMenu.ReturnToGameRequested += (_, _) => HideOverlay();
        OverlayMenu.ExitToMenuRequested += OnExitToMenuRequested;
        OverlayMenu.FullscreenChanged += OnFullscreenChanged;
    }

#if WINDOWS
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        AttachWindowKeyEvents();
    }

    public void AttachWindowKeyEvents()
    {
        Microsoft.Maui.Controls.Window? mauiWindow = Application.Current?.Windows.FirstOrDefault();
        Microsoft.UI.Xaml.Window? nativeWindow = mauiWindow?.Handler?.PlatformView
                           as Microsoft.UI.Xaml.Window;

        if (nativeWindow?.Content is UIElement root)
        {
            root.AddHandler(
                UIElement.KeyDownEvent,
                new KeyEventHandler((s, args) =>
                {
                    KeyEventArgs keyEvent = WinKeyMapper.ToKeyEventArgs(args);
                    if (keyEvent.KeyChar == (int)Keys.Escape && _game.GuiState == GameState.Normal && !OverlayMenu.IsVisible)
                    {
                        ((MauiGameWindowService)_gameWindowService).ReleaseCursor();
                        ShowPauseMenu();
                    }
                    else if (keyEvent.KeyChar == (int)Keys.Escape && _game.GuiState == GameState.Inventory)
                    {
                        ((MauiGameWindowService)_gameWindowService).CaptureCursor();
                    }
                    else if (keyEvent.KeyChar == (int)Keys.B && _game.GuiState == GameState.Normal)
                    {
                        ((MauiGameWindowService)_gameWindowService).ReleaseCursor();
                    }
                    else if (keyEvent.KeyChar == (int)Keys.Escape && OverlayMenu.IsVisible)
                    {
                        HideOverlay();
                        ((MauiGameWindowService)_gameWindowService).CaptureCursor();
                    }
                    _game.KeyDown(keyEvent);
                    _game.KeyPress(keyEvent);
                    args.Handled = keyEvent.Handled;
                }),
                handledEventsToo: true
            );

            root.AddHandler(
                UIElement.KeyUpEvent,
                new KeyEventHandler((s, args) =>
                {
                    KeyEventArgs keyEvent = WinKeyMapper.ToKeyEventArgs(args);
                    _game.KeyUp(keyEvent);
                    args.Handled = keyEvent.Handled;
                }),
                handledEventsToo: true
            );

            // Must set these BEFORE trying to focus
            if (root is Microsoft.UI.Xaml.Controls.Control control)
            {
                control.IsTabStop = true;
                control.AllowFocusOnInteraction = true;
            }

            root.Tapped += (s, _) => root.Focus(FocusState.Pointer);  // focus on tap
            root.Focus(FocusState.Programmatic);                       // focus immediately

            root.AddHandler(UIElement.PointerPressedEvent,
                new PointerEventHandler((s, args) =>
                {
                    UIElement? glNative = GlView.Handler?.PlatformView as UIElement;
                    PointerPoint pt = args.GetCurrentPoint(glNative);
                    _game.MouseDown(WinMouseMapper.ToMouseDownEventArgs(pt));
                }),
                handledEventsToo: true);

            root.AddHandler(UIElement.PointerReleasedEvent,
                new PointerEventHandler((s, args) =>
                {
                    UIElement? glNative = GlView.Handler?.PlatformView as UIElement;
                    PointerPoint pt = args.GetCurrentPoint(glNative);
                    _game.MouseUp(WinMouseMapper.ToMouseUpEventArgs(pt));
                }),
                handledEventsToo: true);

            root.AddHandler(UIElement.PointerWheelChangedEvent,
                new PointerEventHandler((s, args) =>
                {
                    UIElement? glNative = GlView.Handler?.PlatformView as UIElement;
                    PointerPoint pt = args.GetCurrentPoint(glNative);
                    _game.MouseWheelChanged(WinMouseMapper.ToMouseWheelEventArgs(pt));
                }),
                handledEventsToo: true);
        }
    }
#endif

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _gameLoopTimer = Dispatcher.CreateTimer();
        _gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 fps
        _gameLoopTimer.Tick += (_, _) =>
        {
            GlView.InvalidateSurface();
#if WINDOWS
            if (_gameWindowService.Focused() && _game.GuiState == GameState.Normal)
                ((MauiGameWindowService)_gameWindowService).TrapCursorInCenter();
#endif
        };
        _gameLoopTimer.Start();

        _assetManager.LoadAssets();

        GlView.Focus();
        ((MauiGameWindowService)_gameWindowService).Attach(GlView);

        _gameWindowService.AddOnNewFrame(Draw);

#if WINDOWS
        _gameWindowService.RequestMousePointerLock();

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
            Application.Current.Windows[0].Handler.PlatformView
            as Microsoft.UI.Xaml.Window);

        MauiGameWindowService svc = (MauiGameWindowService)_gameWindowService;
        svc.StartRawInput(hwnd);
        svc.RawMouseDelta += OnRawMouseDelta;
#endif
        _game.IsSinglePlayer = true;

        _ = Connect().ContinueWith(t =>
        {
            if (t.IsFaulted)
                MainThread.BeginInvokeOnMainThread(() =>
                    throw t.Exception!.InnerException!);
        });
    }

    private async Task Connect()
    {
        // Start simulation loop + chunk workers + periodic tasks.
        // WorkerHost sets SinglePlayerServerLoaded = true once everything is live.
        // Fire-and-forget is fine — startup is fast, socket is already wired above.
        _ = _workerHost.StartAsync();

        int port = Microsoft.Maui.Storage.Preferences.Get("session_port", 0);
        string username = Microsoft.Maui.Storage.Preferences.Get("username", "Player");
        string apiKey = Microsoft.Maui.Storage.Preferences.Get("api_key", string.Empty);

        _game.NetClient = new EnetNetClient(new NetworkService());
        _game.NetClient.Start();
        _game.NetClient.Connect("127.0.0.1", port);
        _game.ConnectData = new ConnectionData
        {
            Ip = "127.0.0.1",
            Port = port,
            Username = username,
            Auth = apiKey,
            ServerPassword = string.Empty,
            IsServerPasswordProtected = false,
        };
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _gameLoopTimer?.Stop();
        _gameLoopTimer = null;

#if WINDOWS
        _gameWindowService.ExitMousePointerLock();

        IntPtr hwnd = WinRT.Interop.WindowNative.GetWindowHandle(
            Application.Current.Windows[0].Handler.PlatformView
            as Microsoft.UI.Xaml.Window);

        MauiGameWindowService svc = (MauiGameWindowService)_gameWindowService;
        // Lines 228-245 from original (StopRawInput + RawMouseDelta unsubscribe) unchanged:
        svc.StopRawInput(hwnd);
        svc.RawMouseDelta -= OnRawMouseDelta;
#endif
    }

    private void GlView_PaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
    {
        try { 
        if (!_glInitialized)
        {
#if WINDOWS
            GL.LoadBindings(new AngleBindingsContext());
#elif ANDROID
            GL.LoadBindings(new AndroidBindingsContext());
#endif
            InitGL();
            _glInitialized = true;
            _game.Start();
        }

        // Compute delta time
        DateTime now = DateTime.UtcNow;
        float dt = (float)(now - _lastFrame).TotalSeconds;
        _lastFrame = now;

        Draw(dt);
        }
        catch (Exception ex)
        {
            // Replace with your actual logger
            System.Diagnostics.Debug.WriteLine($"[FATAL] PaintSurface crashed: {ex}");
            File.AppendAllText(
                Path.Combine(FileSystem.CacheDirectory, "crash.txt"),
                $"{DateTime.UtcNow}: {ex}\n");
            throw; // re-throw so you still see it's fatal
        }
    }

    private void InitGL()
    {
        _openGlService.InitShaders();
        _openGlService.GlClearColorRgbaf(0, 0, 0, 1);
        _openGlService.GlEnableDepthTest();
    }

    private void Draw(float dt)
    {
        _openGlService.GlViewport(0, 0, (int)GlView.CanvasSize.Width, (int)GlView.CanvasSize.Height);
        _openGlService.GlClearColorBufferAndDepthBuffer();
        _openGlService.GlDisableDepthTest();
        _openGlService.GlDisableCullFace();

        Render(dt);
    }

    public void Render(float dt)
    {
        if (_game.IsReconnecting)
        {
            _game.Dispose();
            // restart game
            return;
        }

        if (_game.IsExitingToMainMenu)
        {
            _game.Dispose();
            // need to handle exit
            return;
        }

        _game.OnRenderFrame(dt);
    }

    // =========================================================================
    // Overlay — public API
    // =========================================================================

    /// <summary>
    /// Shows the pause menu overlay and releases the mouse lock so the cursor
    /// is usable in the overlay. Call when the player presses ESC.
    /// </summary>
    public void ShowPauseMenu()
    {
        OverlayMenu.ShowPauseMenu();   // reset to Pause panel (not Options)
        OverlayMenu.IsVisible = true;
        _gameWindowService.ExitMousePointerLock();
    }

    /// <summary>
    /// Hides the overlay entirely and restores input to the game.
    /// Called by the ReturnToGameRequested event handler.
    /// </summary>
    private void HideOverlay()
    {
        OverlayMenu.IsVisible = false;
#if WINDOWS
        ((MauiGameWindowService)_gameWindowService).CaptureCursor();
#endif
        _game.GuiState = GameState.Normal;
    }

    /// <summary>
    /// Handler for OverlayMenuView.ExitToMenuRequested.
    /// Hides the overlay, releases the cursor, and navigates to the main menu.
    /// </summary>
    private async void OnExitToMenuRequested(object? sender, EventArgs e)
    {
        HideOverlay();
#if WINDOWS
        ((MauiGameWindowService)_gameWindowService).ReleaseCursor();
#endif
        await Shell.Current.GoToAsync("//MainMenuView");
    }

    /// <summary>
    /// Uses the AppWindow / OverlappedPresenter API — the only reliable way to
    /// toggle borderless fullscreen in a MAUI WinUI3 app.
    /// </summary>
    private void OnFullscreenChanged(object? sender, bool fullscreen)
    {
#if WINDOWS
        MauiWinUIWindow? window = GetParentWindow().Handler.PlatformView as MauiWinUIWindow;
        AppWindow appWindow = GetAppWindow(window);

        if (fullscreen)
        {
            appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.FullScreen);
        }
        else
        {
            appWindow.SetPresenter(Microsoft.UI.Windowing.AppWindowPresenterKind.Default);
        }
#endif
    }

#if WINDOWS
    private static Microsoft.UI.Windowing.AppWindow GetAppWindow(MauiWinUIWindow? window)
    {
        var handle = WinRT.Interop.WindowNative.GetWindowHandle(window);
        WindowId id = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(handle);
        return Microsoft.UI.Windowing.AppWindow.GetFromWindowId(id);
    }

    private void OnRawMouseDelta(int dx, int dy)
    {
        MouseEventArgs emulated = new MouseEventArgs
        {
            MovementX = dx,
            MovementY = dy,
            Emulated = true
        };
        _game.MouseMove(emulated);
    }
#endif
}

#if WINDOWS
public static class WinMouseMapper
{
    public static MouseEventArgs ToMouseDownEventArgs(PointerPoint point)
    {
        return new MouseEventArgs
        {
            X = (int)point.Position.X,
            Y = (int)point.Position.Y,
            Button = MapPressedButton(point.Properties)
        };
    }

    public static MouseEventArgs ToMouseUpEventArgs(PointerPoint point)
    {
        return new MouseEventArgs
        {
            X = (int)point.Position.X,
            Y = (int)point.Position.Y,
            Button = MapReleasedButton(point.Properties)
        };
    }

    public static float ToMouseWheelEventArgs(PointerPoint point)
    {
        return point.Properties.IsHorizontalMouseWheel
            ? 0f
            : point.Properties.MouseWheelDelta / 120f;
    }

    private static int MapPressedButton(PointerPointProperties props)
    {
        if (props.IsLeftButtonPressed) return (int)MouseButton.Left;
        if (props.IsRightButtonPressed) return (int)MouseButton.Right;
        if (props.IsMiddleButtonPressed) return (int)MouseButton.Middle;
        if (props.IsXButton1Pressed) return (int)MouseButton.Button4;
        if (props.IsXButton2Pressed) return (int)MouseButton.Button5;
        return -1;
    }

    private static int MapReleasedButton(PointerPointProperties props)
    {
        return props.PointerUpdateKind switch
        {
            PointerUpdateKind.LeftButtonReleased => (int)MouseButton.Left,
            PointerUpdateKind.RightButtonReleased => (int)MouseButton.Right,
            PointerUpdateKind.MiddleButtonReleased => (int)MouseButton.Middle,
            PointerUpdateKind.XButton1Released => (int)MouseButton.Button4,
            PointerUpdateKind.XButton2Released => (int)MouseButton.Button5,
            _ => -1
        };
    }
}
#endif