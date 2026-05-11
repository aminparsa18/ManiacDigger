using ManicDigger.Maui.Services;
using ManicDigger.Worker;
using OpenTK.Graphics.ES30;
using OpenTK.Mathematics;
using SkiaSharp.Views.Maui;
using System.Runtime.InteropServices;

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
        _singlePlayerService = singlePlayerService;
        _workerHost = workerHost;
        _dummyNetwork = dummyNetwork; 
        _serverSystemBootstraper = serverSystemBootstraper;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        _gameLoopTimer = Dispatcher.CreateTimer();
        _gameLoopTimer.Interval = TimeSpan.FromMilliseconds(16); // ~60 fps
        _gameLoopTimer.Tick += (_, _) => GlView.InvalidateSurface();
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
#if WINDOWS
        ((MauiGameWindowService)_gameWindowService).CaptureMouse(GlView.Handler?.MauiContext);
#endif
        _gameWindowService.AddOnNewFrame(Draw);
       // _gameWindowService.AddOnMouseEvent(HandleMouseDown, HandleMouseUp, HandleMouseMove, HandleMouseWheel);

        _gameWindowService.Start();

        _game.IsSinglePlayer = true;

        Connect();
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

    private float _lastPointerX;
    private float _lastPointerY;

    private void PointerGestureRecognizer_PointerMoved(object sender, PointerEventArgs e)
    {
        if (!_gameWindowService.Focused())
            return;

        Point? pos = e.GetPosition(GlView);
        if (pos == null) return;

        float density = (float)DeviceDisplay.MainDisplayInfo.Density;
        float x = (float)pos.Value.X * density;
        float y = (float)pos.Value.Y * density;
        float dx = x - _lastPointerX;
        float dy = y - _lastPointerY;
        _lastPointerX = x;
        _lastPointerY = y;

        // Pass 1 — real event sets absolute cursor position
        var real = new MouseEventArgs();
        real.SetX((int)x);
        real.SetY((int)y);
        real.SetEmulated(false);
        _game.MouseMove(real);

        // Pass 2 — emulated event accumulates delta for camera rotation
        var emulated = new MouseEventArgs();
        emulated.SetX((int)x);
        emulated.SetY((int)y);
        emulated.SetMovementX((int)dx);
        emulated.SetMovementY((int)dy);
        emulated.SetEmulated(true);
        _game.MouseMove(emulated);
    }
}