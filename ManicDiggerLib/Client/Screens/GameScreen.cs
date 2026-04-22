using OpenTK.Windowing.Common;

/// <summary>
/// Menu screen that owns and drives the active <see cref="Game"/> session.
/// Bridges platform input events to the game, manages the singleplayer
/// embedded server lifecycle, and handles reconnect / exit-to-menu transitions.
/// </summary>
public class ScreenGame : ScreenBase
{
    /// <summary>The game instance owned by this screen.</summary>
    private readonly Game game = new();

    private IGamePlatform platform;
    private ConnectionData connectData;
    private bool singleplayer;
    private string singleplayerSavePath;

    /// <summary>
    /// Initialises the game with the given connection parameters and starts the
    /// network session. Must be called before the screen becomes active.
    /// </summary>
    /// <param name="platform_">Host platform abstraction.</param>
    /// <param name="singleplayer_">
    /// <see langword="true"/> to start an embedded singleplayer session;
    /// <see langword="false"/> to connect to a remote server via <paramref name="connectData_"/>.
    /// </param>
    /// <param name="singleplayerSavePath_">Path to the singleplayer save directory.</param>
    /// <param name="connectData_">Remote server address and credentials (multiplayer only).</param>
    public void Start(IGamePlatform platform_, bool singleplayer_, string singleplayerSavePath_, ConnectionData connectData_)
    {
        platform = platform_;
        singleplayer = singleplayer_;
        singleplayerSavePath = singleplayerSavePath_;
        connectData = connectData_;

        game.platform = platform;
        game.issingleplayer = singleplayer;
        game.assets = menu.assets;
        game.assetsLoadProgress = menu.assetsLoadProgress;

        game.Start();
        Connect(platform);
    }

    /// <summary>
    /// Sets up the network transport and, for singleplayer, the embedded server.
    /// </summary>
    private void Connect(IGamePlatform platform)
    {
        if (singleplayer)
        {
            DummyNetwork network = platform.SinglePlayerServerGetNetwork();

            // Platform provides its own singleplayer server (e.g. mobile).
            platform.SinglePlayerServerStart(singleplayerSavePath);

            // Prime the server inbox so the handshake starts immediately.
            network.ServerInbox.Enqueue([]);
            game.main = new DummyNetClient(network);
            game.connectdata = connectData = new ConnectionData { Username = "Local" };
        }
        else
        {
            game.connectdata = connectData;
            game.main = CreateNetClient(platform)
                ?? throw new InvalidOperationException("No network transport available.");
        }
    }

    /// <summary>
    /// Returns the first available <see cref="NetClient"/> transport supported by
    /// the platform, in priority order: TCP → ENet → WebSocket.
    /// Returns <see langword="null"/> if no transport is available.
    /// </summary>
    private static NetClient? CreateNetClient(IGamePlatform platform)
    {
        if (platform.TcpAvailable()) return new TcpNetClient();
        if (platform.EnetAvailable()) return new EnetNetClient(platform);
        if (platform.WebSocketAvailable()) return new WebSocketNetClient();
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
        if (game.reconnect)
        {
            game.Dispose();
            menu.StartGame(singleplayer, singleplayerSavePath, connectData);
            return;
        }

        if (game.exitToMainMenu)
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
        var redirect = game.GetRedirect();

        if (redirect == null)
        {
            menu.StartMainMenu();
            return;
        }

        // Synchronously query the redirect target.
        // NOTE: This is a deliberate sync-over-async call on the render thread;
        // the game loop is already stopped at this point so blocking is acceptable.
        var (qresult, message) = Task.Run(() =>
            new QueryClient(platform).QueryAsync(redirect.GetIP(), redirect.GetPort())
        ).GetAwaiter().GetResult();

        if (qresult == null)
        {
            platform.MessageBoxShowError(message, "Redirection error");
            menu.StartMainMenu();
            return;
        }

        LoginClientCi lic = new();
        LoginData lidata = new();
        string token = qresult.PublicHash.Split('=')[1];

        lic.Login(platform, connectData.Username, "",
            token, platform.GetPreferences().GetString("Password", ""),
            new LoginResult(), lidata);

        // Poll until the login attempt completes.
        while (lic.loginResult == LoginResult.Connecting)
        {
            lic.Update(platform);
        }

        if (!lidata.ServerCorrect)
        {
            platform.MessageBoxShowError("Invalid server address!", "Redirection error!");
            menu.StartMainMenu();
        }
        else if (!lidata.PasswordCorrect)
        {
            menu.StartLogin(token, null, 0);
        }
        else if (!string.IsNullOrEmpty(lidata.ServerAddress))
        {
            menu.ConnectToGame(lidata, connectData.Username);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the platform window has focus.
    /// Mouse events are suppressed while focus is lost to avoid unintended actions.
    /// </summary>
    private bool IsFocused() => game.platform.Focused();

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
        if (IsFocused()) { game.MouseDown(e); }
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        if (IsFocused()) { game.MouseMove(e); }
    }

    public override void OnMouseUp(MouseEventArgs e)
    {
        if (IsFocused()) { game.MouseUp(e); }
    }
}