using ManicDigger.Worker;
using OpenTK.Windowing.Common;

/// <summary>
/// Manages the screens lifecycle: asset loading, screen routing, input handling,
/// background animation, and launching the game or server connection flows.
/// </summary>
public class ScreenManager : IScreenManager
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    /// <summary>The key char code for 'F' (uppercase), used to cycle texture filters.</summary>
    private const char KeyF_Upper = 'F';

    /// <summary>The key char code for 'f' (lowercase), used to cycle texture filters.</summary>
    private const char KeyF_Lower = 'f';

    /// <summary>The key char code for backtick '`', used to trigger back navigation.</summary>
    private const int KeyBacktick = 96;

    /// <summary>Number of texture filter modes available (0, 1, 2).</summary>
    private const int FilterModeCount = 3;

    // -------------------------------------------------------------------------
    // Public state
    // -------------------------------------------------------------------------

    /// <summary>Renderer responsible for rasterising coloured/styled text into bitmaps.</summary>
    public TextColorRenderer TextColorRenderer { get; set; }

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    /// <summary>The active platform abstraction (windowing, GL, input, etc.).</summary>
    private readonly IScreenFactory _screenFactory;
    private readonly IGameService _gameService;
    private readonly IOpenGlService _platformOpenGl;
    private readonly IAssetManager _assetManager;
    private readonly ILanguageService _lang;

    private bool[] currentlyPressedKeys;

    private ScreenBase screen;

    // Background animation

    private int filter;
    private bool initialized;

    // Touch tracking
    private int touchId;
    private int previousTouchX;
    private int previousTouchY;


    private readonly LoginService loginClient;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public ScreenManager(IScreenFactory screenFactory, IGameService platform, IOpenGlService platformOpenGl,
        IAssetManager assetManager, ILanguageService languageService)
    {
        _screenFactory = screenFactory;
        _gameService = platform;
        _platformOpenGl = platformOpenGl;
        _lang = languageService;
        loginClient = new LoginService();
        _assetManager = assetManager;
    }

    // -------------------------------------------------------------------------
    // Initialisation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Bootstraps the main menu: loads translations, triggers async asset loading,
    /// initialises background animation state, and registers all platform callbacks.
    /// Must be called once before the platform's main loop starts.
    /// </summary>
    /// <param name="platform">The platform implementation to bind to.</param>
    public void Start(string[] args)
    {
        screen = (MainScreen)_screenFactory.CreateMainScreen();

        _lang.LoadTranslations();
        _gameService.SetTitle(_lang.GameName());

        TextColorRenderer = new TextColorRenderer();
        _assetManager.LoadAssets();

        filter = 0;

        currentlyPressedKeys = new bool[360];

        _gameService.AddOnNewFrame(OnNewFrame);
        _gameService.AddOnKeyEvent(HandleKeyDown, HandleKeyUp, HandleKeyPress);
        _gameService.AddOnMouseEvent(HandleMouseDown, HandleMouseUp, HandleMouseMove, HandleMouseWheel);
        _gameService.AddOnTouchEvent(HandleTouchStart, HandleTouchMove, HandleTouchEnd);

        if (args.Length > 0)
        {
            StartGame(false, ConnectionData.FromUri(new Uri(args[0])));
        }

        _gameService.Start();
    }

    // -------------------------------------------------------------------------
    // Per-frame update
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called once per rendered frame. Lazily initialises GL state on the first
    /// invocation, then updates viewport dimensions, draws the scene, advances
    /// background animation, and pumps the login client.
    /// </summary>
    /// <param name="dt">Elapsed time since the last frame, in seconds.</param>
    private void OnNewFrame(float dt)
    {
        if (!initialized)
        {
            initialized = true;
            _platformOpenGl.InitShaders();
            _platformOpenGl.GlClearColorRgbaf(0, 0, 0, 1);
            _platformOpenGl.GlEnableDepthTest();
        }

        screen.DrawScene(dt);

        screen.Animate(dt);
    }

    // -------------------------------------------------------------------------
    // Screen navigation
    // -------------------------------------------------------------------------

    /// <summary>Navigates to the main (home) screen and releases any mouse pointer lock.</summary>
    public void StartMainMenu()
    {
        screen = (MainScreen)_screenFactory.CreateMainScreen();
        _gameService.ExitMousePointerLock();
    }

    /// <summary>Navigates to the single-player world selection screen.</summary>
    public void StartSingleplayer()
    {
        screen = (SingleplayerScreen)_screenFactory.CreateSingleplayerScreen();
        screen.LoadTranslations();
    }

    /// <summary>Navigates to the multiplayer server-browser screen.</summary>
    public void StartMultiplayer()
    {
        screen = (MultiplayerScreen)_screenFactory.CreateMultiplayerScreen();
        screen.LoadTranslations();
    }

    /// <summary>
    /// Navigates to the login screen pre-populated with the target server details.
    /// </summary>
    /// <param name="serverHash">Server authentication hash.</param>
    /// <param name="ip">Server IP address.</param>
    /// <param name="port">Server port number.</param>
    public void StartLogin(string serverHash, string ip, int port)
    {
        screen = (LoginScreen)_screenFactory.CreateLoginScreen(serverHash, ip, port);
        screen.LoadTranslations();
    }

    /// <summary>Navigates to the direct-connect / manual IP entry screen.</summary>
    public void StartConnectToIp()
    {
        screen = (ConnectionScreen)_screenFactory.CreateConnectionScreen();
        screen.LoadTranslations();
    }

    /// <summary>
    /// Creates a <see cref="ScreenGame"/>, starts it in single- or multi-player mode,
    /// and makes it the active screen.
    /// </summary>
    /// <param name="singleplayer"><c>true</c> to start a local server; <c>false</c> for a remote connection.</param>
    /// <param name="singleplayerSavePath">Path to the save file; ignored when <paramref name="singleplayer"/> is <c>false</c>.</param>
    /// <param name="connectData">Remote connection parameters; ignored when <paramref name="singleplayer"/> is <c>true</c>.</param>
    public void StartGame(bool singleplayer, ConnectionData connectData)
    {
        ScreenGame screenGame = (ScreenGame)_screenFactory.CreateScreenGame();
        screenGame.Start(singleplayer, connectData);
        screen = screenGame;
    }

    /// <summary>
    /// Builds a <see cref="ConnectionData"/> from a successful login response and
    /// immediately transitions to the game screen.
    /// </summary>
    public void ConnectToGame(LoginData loginResultData, string username)
    {
        ConnectionData connectData = new()
        {
            Ip = loginResultData.ServerAddress,
            Port = loginResultData.Port,
            Auth = loginResultData.AuthCode,
            Username = username
        };
        StartGame(false, connectData);
    }

    /// <summary>Starts a single-player session using an existing save file.</summary>
    /// <param name="filename">Absolute path to the <c>.mddbs</c> save file.</param>
    public void ConnectToSingleplayer()
        => StartGame(true, null);

    // -------------------------------------------------------------------------
    // Login / account helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Initiates a login request. Returns early (setting <paramref name="loginResult"/>
    /// to <see cref="LoginResult.Failed"/>) when credentials are incomplete.
    /// </summary>
    /// <param name="user">Username.</param>
    /// <param name="password">Password; may be empty when <paramref name="token"/> is provided.</param>
    /// <param name="serverHash">Server authentication hash.</param>
    /// <param name="token">Session token; may be empty when <paramref name="password"/> is provided.</param>
    /// <param name="loginResult">Receives the immediate outcome (may change asynchronously via <paramref name="loginResultData"/>).</param>
    /// <param name="loginResultData">Populated with server address and auth code on success.</param>
    public void Login(
        string user, string password,
        string serverHash, string token,
        LoginResult loginResult, LoginData loginResultData)
    {
        if (string.IsNullOrEmpty(user) || (string.IsNullOrEmpty(password) && string.IsNullOrEmpty(token)))
        {
            loginResult = LoginResult.Failed;
            return;
        }

        loginClient.Login(_gameService, user, password, serverHash, token, loginResult, loginResultData);
    }

    /// <summary>
    /// Validates account creation inputs. Currently only performs local validation;
    /// actual account creation is not yet implemented.
    /// </summary>
    /// <returns><see cref="LoginResult.Failed"/> on invalid input; <see cref="LoginResult.Ok"/> otherwise.</returns>
    public static LoginResult CreateAccount(string user, string password)
        => string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password) ? LoginResult.Failed : LoginResult.Ok;

    // -------------------------------------------------------------------------
    // Input handlers
    // -------------------------------------------------------------------------

    /// <summary>Records the key as pressed and forwards the event to the active screen.</summary>
    public void HandleKeyDown(KeyEventArgs e)
    {
        currentlyPressedKeys[e.KeyChar] = true;
        screen.OnKeyDown(e);
    }

    /// <summary>Records the key as released and forwards the event to the active screen.</summary>
    public void HandleKeyUp(KeyEventArgs e)
    {
        currentlyPressedKeys[e.KeyChar] = false;
        screen.OnKeyUp(e);
    }

    /// <summary>
    /// Handles character-level key events: 'F'/'f' cycles the texture filter,
    /// backtick triggers back-navigation, then the event is forwarded to the active screen.
    /// </summary>
    public void HandleKeyPress(KeyPressEventArgs e)
    {
        if (e.KeyChar is KeyF_Upper or KeyF_Lower)
        {
            filter = (filter + 1) % FilterModeCount;
        }

        if (e.KeyChar == KeyBacktick)
        {
            screen.OnBackPressed();
        }

        screen.OnKeyPress(e);
    }

    /// <summary>Records a mouse-button press, caches the initial position, and forwards to the active screen.</summary>
    public void HandleMouseDown(MouseEventArgs e) => screen.OnMouseDown(e);

    /// <summary>Records a mouse-button release and forwards to the active screen.</summary>
    public void HandleMouseUp(MouseEventArgs e) => screen.OnMouseUp(e);

    /// <summary>
    /// Tracks the cursor position and forwards to the active screen.
    /// </summary>
    public void HandleMouseMove(MouseEventArgs e) => screen.OnMouseMove(e);

    /// <summary>Forwards mouse-wheel events to the active screen.</summary>
    public void HandleMouseWheel(MouseWheelEventArgs e)
        => screen.OnMouseWheel(e);

    /// <summary>Records the initiating touch contact and forwards to the active screen.</summary>
    public void HandleTouchStart(TouchEventArgs e)
    {
        touchId = e.GetId();
        previousTouchX = e.GetX();
        previousTouchY = e.GetY();
        screen.OnTouchStart(e);
    }

    /// <summary>
    /// Forwards touch-move to the active screen, then — for the tracked contact —
    /// applies the delta to the background scroll speed.
    /// </summary>
    public void HandleTouchMove(TouchEventArgs e)
    {
        screen.OnTouchMove(e);

        if (e.GetId() != touchId)
        {
            return;
        }

        float dx = e.GetX() - previousTouchX;
        float dy = e.GetY() - previousTouchY;

        previousTouchX = e.GetX();
        previousTouchY = e.GetY();

        screen.ySpeed += dx / 10;
        screen.xSpeed += dy / 10;
    }

    /// <summary>Forwards touch-end events to the active screen.</summary>
    public void HandleTouchEnd(TouchEventArgs e)
        => screen.OnTouchEnd(e);
}

// =============================================================================
// Supporting types
// =============================================================================

/// <summary>
/// Holds a rasterised text string as an uploaded GPU texture together with the
/// layout metrics needed to position and align it during rendering.
/// </summary>
public class TextTexture
{
    /// <summary>Font size (in points) used when this texture was rasterised.</summary>
    public float Size { get; set; }

    /// <summary>The string that was rasterised into this texture.</summary>
    public string Text { get; set; }

    /// <summary>OpenGL texture ID.</summary>
    public int Texture { get; set; }

    /// <summary>Width of the uploaded bitmap in texels (may include padding).</summary>
    public int TextureWidth { get; set; }

    /// <summary>Height of the uploaded bitmap in texels (may include padding).</summary>
    public int TextureHeight { get; set; }

    /// <summary>Typographic width of the text (no padding), used for alignment calculations.</summary>
    public int TextWidth { get; set; }

    /// <summary>Typographic height of the text (no padding), used for alignment calculations.</summary>
    public int TextHeight { get; set; }
}

/// <summary>Represents the outcome of a login or account-creation attempt.</summary>
public enum LoginResult
{
    /// <summary>No attempt has been made yet.</summary>
    None,

    /// <summary>A request is in-flight.</summary>
    Connecting,

    /// <summary>The attempt was rejected or input was invalid.</summary>
    Failed,

    /// <summary>The attempt succeeded.</summary>
    Ok
}

/// <summary>
/// Carries the raw HTTP response from a server-thumbnail request,
/// including the image bytes and any error state.
/// </summary>
public class ThumbnailResponseCi
{
    /// <summary><c>true</c> once the request has completed (successfully or not).</summary>
    public bool Done { get; set; }

    /// <summary><c>true</c> if the request failed; check <see cref="ServerMessage"/> for details.</summary>
    public bool Error { get; set; }

    /// <summary>Optional message returned by the server alongside the result.</summary>
    public string ServerMessage { get; set; }

    /// <summary>Raw PNG or JPEG bytes of the thumbnail, or <c>null</c> on error.</summary>
    public byte[] Data { get; set; }
}