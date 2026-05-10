/// <summary>
/// Screen that lets the player connect to a server by entering an IP address
/// and port number directly. Persists the last-used values to preferences and
/// restores them on the next visit.
/// </summary>
/// <remarks>
/// Tab order (by list index): [0] Connect → [3] Back → [1] IP → [2] Port → [0] Connect.
/// </remarks>
public class ConnectionScreen : ScreenBase, IConnectionScreen
{
    private readonly MenuWidget buttonConnect;
    private readonly MenuWidget buttonBack;
    private readonly MenuWidget textboxIp;
    private readonly MenuWidget textboxPort;
    private readonly IPreferences preferences;
    private readonly ILanguageService languageService;

    /// <summary>
    /// Error text displayed above the form. Currently unused — set this field
    /// to surface connection or validation errors to the player.
    /// </summary>
    private readonly string errorText;

    private string savedIp;
    private string savedPort;
    private string title;
    private bool loaded;

    public ConnectionScreen(ILanguageService languageService, IGameWindowService platform, IPreferences preferences, IOpenGlService openGlService, IAssetManager assetManager)
        : base(platform, openGlService, assetManager)
    {
        buttonConnect = new MenuWidget { Text = "Connect", Type = UIWidgetType.Button, NextWidget = 3 };
        textboxIp = new MenuWidget { Text = "", Type = UIWidgetType.Textbox, Description = "IP", NextWidget = 2 };
        textboxPort = new MenuWidget { Text = "", Type = UIWidgetType.Textbox, Description = "Port", NextWidget = 0 };
        buttonBack = new MenuWidget { Text = "Back", Type = UIWidgetType.Button, NextWidget = 1 };

        this.preferences = preferences;
        this.languageService = languageService;

        title = "Connect to IP";

        Widgets.Add(buttonConnect); // 0
        Widgets.Add(textboxIp);     // 1
        Widgets.Add(textboxPort);   // 2
        Widgets.Add(buttonBack);    // 3

        textboxIp.GetFocus();
    }

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        buttonConnect.Text = languageService.Get("MainMenu_ConnectToIpConnect");
        textboxIp.Description = languageService.Get("MainMenu_ConnectToIpIp");
        textboxPort.Description = languageService.Get("MainMenu_ConnectToIpPort");
        title = languageService.Get("MainMenu_MultiplayerConnectIP");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        if (!loaded)
        {
            savedIp = preferences.GetString("ConnectToIpIp", "127.0.0.1");
            savedPort = preferences.GetString("ConnectToIpPort", "25565");
            textboxIp.Text = savedIp;
            textboxPort.Text = savedPort;
            loaded = true;
        }

        // Persist changes to preferences whenever the player edits either field.
        if (textboxIp.Text != savedIp || textboxPort.Text != savedPort)
        {
            savedIp = textboxIp.Text;
            savedPort = textboxPort.Text;

            IPreferences prefs = preferences;
            prefs.SetString("ConnectToIpIp", savedIp);
            prefs.SetString("ConnectToIpPort", savedPort);
            prefs.SetValues();
        }

        float scale = GetScale();

        DrawBackground();

        float leftx = (GameService.CanvasWidth / 2) - (400 * scale);
        float y = (GameService.CanvasHeight / 2) - (250 * scale);

        if (errorText != null)
        {
            DrawText(errorText, 14 * scale, leftx, y - (50 * scale), TextAlign.Left, TextBaseline.Top);
        }

        DrawText(title, 14 * scale, leftx, y + (50 * scale), TextAlign.Left, TextBaseline.Top);

        LayoutWidget(textboxIp, leftx, y + (100 * scale), 256, 64, scale);
        LayoutWidget(textboxPort, leftx, y + (200 * scale), 256, 64, scale);
        LayoutWidget(buttonConnect, leftx, y + (400 * scale), 256, 64, scale);

        buttonBack.X = 40 * scale;
        buttonBack.Y = GameService.CanvasHeight - (104 * scale);
        buttonBack.Sizex = 256 * scale;
        buttonBack.Sizey = 64 * scale;
        buttonBack.FontSize = 14 * scale;

        DrawWidgets();
    }

    /// <summary>
    /// Assigns position, size, and font size to a widget using pre-scaled base dimensions.
    /// </summary>
    private static void LayoutWidget(MenuWidget w, float x, float y, float w_, float h, float scale)
    {
        w.X = x;
        w.Y = y;
        w.Sizex = w_ * scale;
        w.Sizey = h * scale;
        w.FontSize = 14 * scale;
    }

    //TODO commented for now until decoupling menu and screens is done
    /// <inheritdoc/>
    //public override void OnBackPressed() => Menu.StartMultiplayer();

    ///// <inheritdoc/>
    //public override void OnButton(MenuWidget w)
    //{
    //    if (w == buttonBack)
    //    {
    //        OnBackPressed();
    //        return;
    //    }

    //    if (w == buttonConnect)
    //    {
    //        if (!string.IsNullOrEmpty(textboxIp.text)
    //            && int.TryParse(textboxPort.text, out int port))
    //        {
    //            Menu.StartLogin(null, textboxIp.text, port);
    //        }
    //    }
    //}
}