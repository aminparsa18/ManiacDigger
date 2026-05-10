using OpenTK.Windowing.GraphicsLibraryFramework;

/// <summary>
/// The top-level main menu screen. Displays the game logo and the three primary
/// navigation buttons: Singleplayer, Multiplayer, and Quit.
/// </summary>
/// <remarks>
/// On first render (after assets finish loading) the screen checks for an
/// <c>?ip=&amp;port=</c> query string and auto-connects if one is present.
/// </remarks>
public class MainScreen : ScreenBase, IMainScreen
{
    public MainScreen(IGameWindowService platform, ISinglePlayerService singlePlayerService, IOpenGlService openGlService,
        ILanguageService languageService, IAssetManager assetManager, INavigator navigator)
        : base(platform, openGlService, assetManager)
    {
        buttonSingleplayer = new MenuWidget { Text = "Singleplayer" };
        buttonMultiplayer = new MenuWidget { Text = "Multiplayer" };
        buttonExit = new MenuWidget { Text = "Quit" };

        this.singlePlayerService = singlePlayerService;
        _languageService = languageService;
        _navigator = navigator;

        Widgets.Add(buttonSingleplayer);
        Widgets.Add(buttonMultiplayer);
        Widgets.Add(buttonExit);
    }

    private readonly ISinglePlayerService singlePlayerService;
    private readonly ILanguageService _languageService;
    private readonly INavigator _navigator;

    private readonly MenuWidget buttonSingleplayer;
    private readonly MenuWidget buttonMultiplayer;
    private readonly MenuWidget buttonExit;

    /// <summary>Current canvas width, updated every frame. Exposed for external layout queries.</summary>
    internal float windowX;

    /// <summary>Current canvas height, updated every frame. Exposed for external layout queries.</summary>
    internal float windowY;

    private bool queryStringChecked;
    private bool cursorLoaded;

    private const int ButtonHeight = 64;
    private const int ButtonWidth = 256;
    private const int SpaceBetween = 5;
    private const int OffsetFromBorder = 50;

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        buttonSingleplayer.Text = _languageService.Get("MainMenu_Singleplayer");
        buttonMultiplayer.Text = _languageService.Get("MainMenu_Multiplayer");
        buttonExit.Text = _languageService.Get("MainMenu_Quit");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        windowX = GameService.CanvasWidth;
        windowY = GameService.CanvasHeight;

        float scale = GetScale();

        if (AssetManager.AssetsLoadProgress != 1)
        {
            string s = string.Format(_languageService.Get("MainMenu_AssetsLoadProgress"),
                ((int)(AssetManager.AssetsLoadProgress * 100)).ToString());
            DrawText(s, 20 * scale, windowX / 2, windowY / 2, TextAlign.Center, TextBaseline.Middle);
            return;
        }

        if (!cursorLoaded)
        {
            GameService.SetWindowCursor(0, 0, 32, 32,
                GetFile("mousecursor.png"),
                GetFileLength("mousecursor.png"));
            cursorLoaded = true;
        }

        UseQueryStringIpAndPort();

        DrawBackground();
        Draw2dQuad(GetTexture("logo.png"),
            (windowX / 2) - (1024 * scale / 2), 0, 1024 * scale, 512 * scale);

        float centerX = (windowX / 2) - (ButtonWidth / 2 * scale);

        buttonSingleplayer.X = centerX;
        buttonSingleplayer.Y = ButtonY(3, scale);
        buttonSingleplayer.Sizex = ButtonWidth * scale;
        buttonSingleplayer.Sizey = ButtonHeight * scale;

        buttonMultiplayer.X = centerX;
        buttonMultiplayer.Y = ButtonY(2, scale);
        buttonMultiplayer.Sizex = ButtonWidth * scale;
        buttonMultiplayer.Sizey = ButtonHeight * scale;

        buttonExit.Visible = true;
        buttonExit.X = centerX;
        buttonExit.Y = ButtonY(1, scale);
        buttonExit.Sizex = ButtonWidth * scale;
        buttonExit.Sizey = ButtonHeight * scale;

        DrawWidgets();
    }

    /// <summary>
    /// Returns the Y position for a button at the given slot (1 = bottom, 2 = above that, etc.)
    /// measured from the bottom of the canvas.
    /// </summary>
    private float ButtonY(int slot, float scale)
        => windowY - (slot * ((ButtonHeight * scale) + SpaceBetween)) - (OffsetFromBorder * scale);

    /// <summary>
    /// On the first call, checks the page query string for <c>ip</c> and <c>port</c>
    /// parameters and auto-starts a login if an IP is found. Subsequent calls are no-ops.
    /// </summary>
    private void UseQueryStringIpAndPort()
    {
        if (queryStringChecked)
        {
            return;
        }

        queryStringChecked = true;

        string ip = GameService.QueryStringValue("ip");
        string port = GameService.QueryStringValue("port");

        int portInt = int.TryParse(port, out int parsedPort) ? parsedPort : 25565;

        if (ip != null)
        {
            _navigator.StartLogin(null, ip, portInt);
        }
    }

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        if (w == buttonSingleplayer)
        {
            _navigator.StartSingleplayer();
            return;
        }

        if (w == buttonMultiplayer)
        {
            _navigator.StartMultiplayer();
            return;
        }

        if (w == buttonExit)
        {
            Environment.Exit(0);
            return;
        }
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => Environment.Exit(0);

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
#if DEBUG
        // F5 — launch current singleplayer save (legacy .mdss format).
        if (e.KeyChar == (int)Keys.F5)
        {
            singlePlayerService.SinglePlayerServerAvailable = false;
            _navigator.StartGame(true, null);
        }
        // F6 — launch current singleplayer save (database .mddbs format).
        if (e.KeyChar == (int)Keys.F6)
        {
            _navigator.StartGame(true, null);
        }
#endif
    }
}