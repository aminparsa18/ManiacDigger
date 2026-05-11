using ManicDigger.Maui.Services;
using ManicDigger.Worker;
using OpenTK.Graphics.ES30;
using OpenTK.Mathematics;
using SkiaSharp.Views.Maui;
using System.Runtime.InteropServices;
using Application = Microsoft.Maui.Controls.Application;
using Microsoft.UI.Xaml.Input;
using OpenTK.Windowing.GraphicsLibraryFramework;






#if WINDOWS
using Windows.UI.Core;
using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
#endif

namespace ManicDigger.Maui.Views;

public partial class GameView : ContentPage
{
    private bool _glInitialized = false;
    private IDispatcherTimer _gameLoopTimer;
    private DateTime _lastFrame = DateTime.UtcNow;

    private readonly IGame _game;
    private readonly ISinglePlayerService _singlePlayerService;
    private readonly ISaveGameService _saveGameService;
    private readonly IOpenGlService _openGlService;
    private readonly IGameLogger _gameLogger;
    private readonly IGameWindowService _gameWindowService;
    private readonly IAssetManager _assetManager;
    private readonly IDummyNetwork _dummyNetwork;
    private readonly WorkerHost _workerHost;
    private readonly ServerSystemBootstraper _serverSystemBootstraper;

    private Matrix4 pMatrix = Matrix4.Identity;

    [DllImport("libEGL.dll")]
    private static extern IntPtr eglGetProcAddress(string procName);

    private class AngleBindingsContext : OpenTK.IBindingsContext
    {
        public IntPtr GetProcAddress(string procName) => eglGetProcAddress(procName);
    }

    public GameView(IOpenGlService openGlService, IGameWindowService gameWindowService, IAssetManager assetManager,
        IGameLogger gameLogger, IGame game, ISinglePlayerService singlePlayerService, IDummyNetwork dummyNetwork,
        ISaveGameService saveGameService, WorkerHost workerHost, ServerSystemBootstraper serverSystemBootstraper)
    {
        InitializeComponent();
        _openGlService = openGlService;
        _gameWindowService = gameWindowService;
        _saveGameService = saveGameService;
        _assetManager = assetManager;
        _game = game;
        _gameLogger = gameLogger;
        _singlePlayerService = singlePlayerService;
        _workerHost = workerHost;
        _dummyNetwork = dummyNetwork;
        _serverSystemBootstraper = serverSystemBootstraper;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();
#if WINDOWS
        AttachWindowKeyEvents();
#endif
    }

#if WINDOWS
    void AttachWindowKeyEvents()
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
                    _game.KeyDown(keyEvent);
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
            if (_gameWindowService.Focused())
                ((MauiGameWindowService)_gameWindowService).RecenterCursor();
#endif
        };
        _gameLoopTimer.Start();

        string extension = _singlePlayerService.SinglePlayerServerAvailable ? "mddbs" : "mdss";
        string path = GetDefaultSavePath(extension);

        _saveGameService.InitialiseSession(
            File.Exists(path)
                ? SaveTarget.FromFile(path)
                : SaveTarget.NewGame());

        _assetManager.LoadAssets();

        GlView.Focus();
        ((MauiGameWindowService)_gameWindowService).Attach(GlView);

        _gameWindowService.AddOnNewFrame(Draw);
        // _gameWindowService.AddOnMouseEvent(HandleMouseDown, HandleMouseUp, HandleMouseMove, HandleMouseWheel);

        _gameWindowService.Start();

#if WINDOWS
       GlView.HandlerChanged += AttachKeyEvents;

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

    void AttachKeyEvents(object? sender, EventArgs e)
    {
#if WINDOWS
        if (GlView.Handler?.PlatformView is UIElement el)
        {
            // Must set these BEFORE trying to focus
            if (el is Microsoft.UI.Xaml.Controls.Control control)
            {
                control.IsTabStop = true;
                control.AllowFocusOnInteraction = true;
            }

            el.Tapped += (s, _) => el.Focus(FocusState.Pointer);  // focus on tap
            el.Focus(FocusState.Programmatic);                     // focus immediately

            el.AddHandler(UIElement.KeyDownEvent,new KeyEventHandler((s, args) =>
                {
                    var keyEvent = WinKeyMapper.ToKeyEventArgs(args);
                    _game.KeyDown(keyEvent);
                    _game.KeyPress(keyEvent);
                }), handledEventsToo: true);

            el.AddHandler(UIElement.KeyUpEvent,new KeyEventHandler((s, args) =>
                {
                   var keyEvent = WinKeyMapper.ToKeyEventArgs(args);
                   _game.KeyUp(keyEvent);
                }), handledEventsToo: true);

            el.AddHandler(UIElement.PointerPressedEvent,
           new PointerEventHandler((s, args) =>
           {
               var pt = args.GetCurrentPoint(el);
               var kir = WinMouseMapper.ToMouseEventArgs(pt);
               _game.MouseDown(WinMouseMapper.ToMouseEventArgs(pt));
           }),
           handledEventsToo: true);

            el.AddHandler(UIElement.PointerReleasedEvent,
                new PointerEventHandler((s, args) =>
                {
                    var pt = args.GetCurrentPoint(el);
                    _game.MouseUp(WinMouseMapper.ToMouseEventArgs(pt));
                }),
                handledEventsToo: true);

            el.AddHandler(UIElement.PointerWheelChangedEvent,
                new PointerEventHandler((s, args) =>
                {
                    var pt = args.GetCurrentPoint(el);
                    _game.MouseWheelChanged(WinMouseMapper.ToMouseWheelEventArgs(pt));
                }),
                handledEventsToo: true);
        }
#endif
    }

    private static string GetDefaultSavePath(string extension)
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Manic Digger Save");
        Directory.CreateDirectory(folder); // no-op if already exists
        return Path.Combine(folder, "Default." + extension);
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
        svc.RawMouseDelta -= OnRawMouseDelta;
        svc.StopRawInput(hwnd);
#endif
    }

    private void OnRawMouseDelta(int dx, int dy)
    {
        var emulated = new MouseEventArgs();
        emulated.        MovementX = dx;
        emulated.        MovementY = dy;
        emulated.        Emulated = true;
        _game.MouseMove(emulated);
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

        Matrix4.CreateOrthographicOffCenter(
            0, GlView.CanvasSize.Width,
            GlView.CanvasSize.Height, 0,
            0, 10,
            out pMatrix);

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

}

#if WINDOWS
public static class WinMouseMapper
{
    public static MouseEventArgs ToMouseEventArgs(PointerPoint point)
    {
        return new MouseEventArgs
        {
            X = (int)point.Position.X,
            Y = (int)point.Position.Y,
            Button = MapButton(point.Properties)
        };
    }

    public static MouseEventArgs ToMouseMoveEventArgs(PointerPoint point)
    {
        return new MouseEventArgs
        {
            X = (int)point.Position.X,
            Y = (int)point.Position.Y,
            Button = MapButton(point.Properties)
        };
    }

    public static float ToMouseWheelEventArgs(PointerPoint point)
    {
        var delta = point.Properties.MouseWheelDelta;
        bool isHorizontal = point.Properties.IsHorizontalMouseWheel;

        return isHorizontal ? 0f : delta / 120f;
    }

    private static int MapButton(PointerPointProperties props)
    {
        if (props.IsLeftButtonPressed) return (int)MouseButton.Left;
        if (props.IsRightButtonPressed) return (int)MouseButton.Right;
        if (props.IsMiddleButtonPressed) return (int)MouseButton.Middle;
        if (props.IsXButton1Pressed) return (int)MouseButton.Button4;
        if (props.IsXButton2Pressed) return (int)MouseButton.Button5;
        return -1;
    }
}
#endif