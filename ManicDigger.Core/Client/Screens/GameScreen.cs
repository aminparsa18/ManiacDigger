using ManicDigger.Worker;
using Microsoft.Extensions.DependencyInjection;
using OpenTK.Windowing.Common;
using static ManicDigger.Mods.ModNetworkProcess;

/// <summary>
/// Extends the base screen contract with the game-session initialisation
/// method that must be called before the screen becomes active.
/// </summary>
public interface IScreenGame : IScreenBase
{
    void Start(bool singleplayer, ConnectionData connectData);
}

/// <summary>
/// Menu screen that owns and drives the active <see cref="Game"/> session.
/// Bridges platform input events to the game, manages the singleplayer
/// embedded server lifecycle, and handles reconnect / exit-to-menu transitions.
/// </summary>
public class ScreenGame(IGameService platform, IOpenGlService openGlService, IAssetManager assetManager, ISaveGameService saveGameService,
    ISinglePlayerService singlePlayerService, IPreferences preferences, IGameExit gameExit, IScreenManager menu, IGameLogger gameLogger,
    IDummyNetwork dummyNetwork, IGame game, IServiceProvider serviceProvider, WorkerHost workerHost) : ScreenBase(platform, openGlService, assetManager), IScreenGame
{
    /// <summary>The game instance owned by this screen.</summary>
    private readonly IGame game = game;
    private ConnectionData connectData;
    private bool singleplayer;
    private readonly IGameExit gameExit = gameExit;
    private readonly IScreenManager _menu = menu;
    private readonly IDummyNetwork _dummyNetwork = dummyNetwork;
    private readonly WorkerHost _workerHost = workerHost;
    private readonly IGameLogger _gameLogger = gameLogger;

    /// <summary>
    /// Initialises the game with the given connection parameters and starts the
    /// network session. Must be called before the screen becomes active.
    /// </summary>
    /// <param name="singleplayer_">
    /// <see langword="true"/> to start an embedded singleplayer session;
    /// <see langword="false"/> to connect to a remote server via <paramref name="connectData_"/>.
    /// </param>
    /// <param name="singleplayerSavePath_">Path to the singleplayer save directory.</param>
    /// <param name="connectData_">Remote server address and credentials (multiplayer only).</param>
    public void Start(bool singleplayer_, ConnectionData connectData_)
    {

        singleplayer = singleplayer_;
        connectData = connectData_;

        game.IsSinglePlayer = singleplayer;

        game.Start();
        Connect();
    }

    /// <summary>
    /// Sets up the network transport and, for singleplayer, the embedded server.
    /// </summary>
    private void Connect()
    {
        if (singleplayer)
        {
            IDummyNetwork network = singlePlayerService.SinglePlayerServerNetwork;

            // Wire the server socket BEFORE starting workers so the first
            // simulation tick already has a valid socket to drain.
            ServerSystemBootstraper bootstrapper = serviceProvider.GetRequiredService<ServerSystemBootstraper>();
            Server server = bootstrapper.Server;
            server.MainSockets[0] = new DummyNetServer(_dummyNetwork);

            // Start simulation loop + chunk workers + periodic tasks.
            // WorkerHost sets SinglePlayerServerLoaded = true once everything is live.
            // Fire-and-forget is fine — startup is fast, socket is already wired above.
            _ = _workerHost.StartAsync();

            game.NetClient = new DummyNetClient(network);
            game.ConnectData = connectData = new ConnectionData { Username = "Local" };
        }
        else
        {
            game.ConnectData = connectData;
            game.NetClient = CreateNetClient()
                ?? throw new InvalidOperationException("No network transport available.");
        }
    }

    /// <summary>
    /// Returns the first available <see cref="NetClient"/> transport supported by
    /// the platform, in priority order: TCP → ENet → WebSocket.
    /// Returns <see langword="null"/> if no transport is available.
    /// </summary>
    private NetClient? CreateNetClient()
    {
        if (GameService.NetworkService.TcpAvailable())
        {
            return new TcpNetClient();
        }

        if (GameService.NetworkService.EnetAvailable())
        {
            return new EnetNetClient(GameService.NetworkService);
        }

        if (GameService.NetworkService.WebSocketAvailable())
        {
            return new WebSocketNetClient();
        }

        return null;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Checks the game's reconnect and exit-to-menu flags before delegating to
    /// <see cref="Game.OnRenderFrame"/>. Reconnects restart the game in place.
    /// Exits trigger the redirect handshake when the server requests a redirect,
    /// or navigate back to the main menu otherwise.
    /// </remarks>
    public override void Render(float dt)
    {
        if (game.IsReconnecting)
        {
            game.Dispose();
            _menu.StartGame(singleplayer, connectData);
            return;
        }

        if (game.IsExitingToMainMenu)
        {
            game.Dispose();
            HandleExit();
            return;
        }

        game.OnRenderFrame(dt);
    }

    /// <summary>
    /// Handles the exit-to-menu transition. If the server issued a redirect,
    /// queries the target server and re-authenticates before connecting.
    /// Otherwise returns directly to the main menu.
    /// </summary>
    private void HandleExit()
    {
        Packet_ServerRedirect redirect = game.Redirect;

        if (redirect == null)
        {
            _menu.StartMainMenu();
            return;
        }

        // Synchronously query the redirect target.
        // NOTE: This is a deliberate sync-over-async call on the render thread;
        // the game loop is already stopped at this point so blocking is acceptable.
        (QueryResult? qresult, string? message) = Task.Run(() =>
            new QueryClient(GameService.NetworkService).QueryAsync(redirect.IP, redirect.Port)
        ).GetAwaiter().GetResult();

        if (qresult == null)
        {
            platform.MessageBoxShowError(message, "Redirection error");
            _menu.StartMainMenu();
            return;
        }

        LoginClientCi lic = new();
        LoginData lidata = new();
        string token = qresult.PublicHash.Split('=')[1];

        lic.Login(platform, connectData.Username, "",
            token, preferences.GetString("Password", ""),
            new LoginResult(), lidata);

        if (!lidata.ServerCorrect)
        {
            platform.MessageBoxShowError("Invalid server address!", "Redirection error!");
            _menu.StartMainMenu();
        }
        else if (!lidata.PasswordCorrect)
        {
            _menu.StartLogin(token, null, 0);
        }
        else if (!string.IsNullOrEmpty(lidata.ServerAddress))
        {
            _menu.ConnectToGame(lidata, connectData.Username);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the platform window has focus.
    /// Mouse events are suppressed while focus is lost to avoid unintended actions.
    /// </summary>
    private bool IsFocused => GameService.Focused();

    public override void OnKeyDown(KeyEventArgs e) => game.KeyDown(e);
    public override void OnKeyUp(KeyEventArgs e) => game.KeyUp(e);
    public override void OnKeyPress(KeyPressEventArgs e) => game.KeyPress(e);
    public override void OnMouseWheel(MouseWheelEventArgs e) => game.MouseWheelChanged(e);
    public override void OnTouchStart(TouchEventArgs e) => game.OnTouchStart(e);
    public override void OnTouchMove(TouchEventArgs e) => game.OnTouchMove(e);
    public override void OnTouchEnd(TouchEventArgs e) => game.OnTouchEnd(e);
    public override void OnBackPressed() => Game.OnBackPressed();

    public override void OnMouseDown(MouseEventArgs e)
    {
        if (IsFocused)
        {
            game.MouseDown(e);
        }
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        if (IsFocused)
        {
            game.MouseMove(e);
        }
    }

    public override void OnMouseUp(MouseEventArgs e)
    {
        if (IsFocused)
        {
            game.MouseUp(e);
        }
    }
}