using MeinKraft.Maui.Services;
using MeinKraft.Worker;
using OpenTK.Graphics.ES30;
using SkiaSharp.Views.Maui;
using System.Runtime.InteropServices;
using OpenTK.Windowing.Common;


#if WINDOWS
using Application = Microsoft.Maui.Controls.Application;
using Microsoft.UI.Xaml.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
#endif

namespace MeinKraft.Maui.Views;

public partial class GameView : ContentPage
{
    private bool _glInitialized = false;
    private IDispatcherTimer _gameLoopTimer;
    private DateTime _lastFrame = DateTime.UtcNow;

    private readonly IGame _game;
    private readonly ISinglePlayerService _singlePlayerService;
    private readonly IOpenGlService _openGlService;
    private readonly IGameWindowService _gameWindowService;
    private readonly IAssetManager _assetManager;
    private readonly IDummyNetwork _dummyNetwork;
    private readonly WorkerHost _workerHost;
    private readonly ServerSystemBootstraper _serverSystemBootstraper;

    [DllImport("libEGL.dll")]
    private static extern IntPtr eglGetProcAddress(string procName);

    private class AngleBindingsContext : OpenTK.IBindingsContext
    {
        public IntPtr GetProcAddress(string procName) => eglGetProcAddress(procName);
    }

    public GameView(IOpenGlService openGlService, IGameWindowService gameWindowService, IAssetManager assetManager,
        IGame game, ISinglePlayerService singlePlayerService, IDummyNetwork dummyNetwork,
        WorkerHost workerHost, ServerSystemBootstraper serverSystemBootstraper)
    {
        InitializeComponent();
        _openGlService = openGlService;
        _gameWindowService = gameWindowService;
        _assetManager = assetManager;
        _game = game;
        _singlePlayerService = singlePlayerService;
        _workerHost = workerHost;
        _dummyNetwork = dummyNetwork;
        _serverSystemBootstraper = serverSystemBootstraper;

        // Wire up overlay events. OverlayMenuView owns its internal navigation
        // (Pause ↔ Options); GameView only handles the two exit points that
        // require cursor and game-state changes.
        OverlayMenu.ReturnToGameRequested += (_, _) => HideOverlay();
        OverlayMenu.ExitToMenuRequested += OnExitToMenuRequested;
    }

#if WINDOWS
    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
        AttachWindowKeyEvents();
        ((MauiGameWindowService)_gameWindowService).CaptureCursor();
    }

    public void AttachWindowKeyEvents()
    {
        var mauiWindow = Application.Current?.Windows.FirstOrDefault();
        var nativeWindow = mauiWindow?.Handler?.PlatformView
                           as Microsoft.UI.Xaml.Window;

        if (nativeWindow?.Content is UIElement root)
        {
            root.AddHandler(
                UIElement.KeyDownEvent,
                new KeyEventHandler((s, args) =>
                {
                    var keyEvent = WinKeyMapper.ToKeyEventArgs(args);
                    if (keyEvent.KeyChar == (int)Keys.Escape && _game.GuiState == GameState.Normal)
                    {
                        ((MauiGameWindowService)_gameWindowService).ReleaseCursor();
                        ShowPauseMenu();
                        _game.GuiState = GameState.EscapeMenu;
                    }
                    if (keyEvent.KeyChar == (int)Keys.Escape && _game.GuiState == GameState.Inventory)
                    {
                        ((MauiGameWindowService)_gameWindowService).CaptureCursor();
                    }
                    else if (keyEvent.KeyChar == (int)Keys.B && _game.GuiState == GameState.Normal)
                    {
                        ((MauiGameWindowService)_gameWindowService).ReleaseCursor();
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
                    var keyEvent = WinKeyMapper.ToKeyEventArgs(args);
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
                    var glNative = GlView.Handler?.PlatformView as UIElement;
                    var pt = args.GetCurrentPoint(glNative);
                    _game.MouseDown(WinMouseMapper.ToMouseDownEventArgs(pt));
                }),
                handledEventsToo: true);

            root.AddHandler(UIElement.PointerReleasedEvent,
                new PointerEventHandler((s, args) =>
                {
                    var glNative = GlView.Handler?.PlatformView as UIElement;
                    var pt = args.GetCurrentPoint(glNative);
                    _game.MouseUp(WinMouseMapper.ToMouseUpEventArgs(pt));
                }),
                handledEventsToo: true);

            root.AddHandler(UIElement.PointerWheelChangedEvent,
                new PointerEventHandler((s, args) =>
                {
                    var glNative = GlView.Handler?.PlatformView as UIElement;
                    var pt = args.GetCurrentPoint(glNative);
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

        var svc = (MauiGameWindowService)_gameWindowService;
        svc.StartRawInput(hwnd);
        svc.RawMouseDelta += OnRawMouseDelta;
#endif
        _game.IsSinglePlayer = true;

        Connect();
    }

    private void Connect()
    {
        if (true) //single player
        {
            IDummyNetwork network = _singlePlayerService.SinglePlayerServerNetwork;

            // Wire the server socket BEFORE starting workers so the first
            // simulation tick already has a valid socket to drain.
            Server server = _serverSystemBootstraper.Server;
            server.MainSockets[0] = new DummyNetServer(_dummyNetwork);

            // Start simulation loop + chunk workers + periodic tasks.
            // WorkerHost sets SinglePlayerServerLoaded = true once everything is live.
            // Fire-and-forget is fine — startup is fast, socket is already wired above.
            _ = _workerHost.StartAsync();

            _game.NetClient = new DummyNetClient(network);
            _game.ConnectData = new ConnectionData { Username = "Local" };
        }
        //else
        //{
        //    game.ConnectData = connectData;
        //    game.NetClient = CreateNetClient()
        //        ?? throw new InvalidOperationException("No network transport available.");
        //}
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

        var svc = (MauiGameWindowService)_gameWindowService;
        // Lines 228-245 from original (StopRawInput + RawMouseDelta unsubscribe) unchanged:
        svc.StopRawInput(hwnd);
        svc.RawMouseDelta -= OnRawMouseDelta;
#endif
    }

    private void GlView_PaintSurface(object sender, SKPaintGLSurfaceEventArgs e)
    {
        if (!_glInitialized)
        {
            GL.LoadBindings(new AngleBindingsContext());
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

#if WINDOWS
    private void OnRawMouseDelta(int dx, int dy)
    {
        var emulated = new MouseEventArgs
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