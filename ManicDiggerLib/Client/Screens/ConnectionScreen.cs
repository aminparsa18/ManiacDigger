/// <summary>
/// Screen that lets the player connect to a server by entering an IP address
/// and port number directly. Persists the last-used values to preferences and
/// restores them on the next visit.
/// </summary>
/// <remarks>
/// Tab order (by list index): [0] Connect → [3] Back → [1] IP → [2] Port → [0] Connect.
/// </remarks>
public class ConnectionScreen : ScreenBase
{
    private readonly MenuWidget buttonConnect;
    private readonly MenuWidget buttonBack;
    private readonly MenuWidget textboxIp;
    private readonly MenuWidget textboxPort;

    /// <summary>
    /// Error text displayed above the form. Currently unused — set this field
    /// to surface connection or validation errors to the player.
    /// </summary>
    private string errorText;

    private string savedIp;
    private string savedPort;
    private string title;
    private bool loaded;

    public ConnectionScreen()
    {
        buttonConnect = new MenuWidget { text = "Connect", type = WidgetType.Button, nextWidget = 3 };
        textboxIp = new MenuWidget { text = "", type = WidgetType.Textbox, description = "IP", nextWidget = 2 };
        textboxPort = new MenuWidget { text = "", type = WidgetType.Textbox, description = "Port", nextWidget = 0 };
        buttonBack = new MenuWidget { text = "Back", type = WidgetType.Button, nextWidget = 1 };

        title = "Connect to IP";

        widgets.Add(buttonConnect); // 0
        widgets.Add(textboxIp);     // 1
        widgets.Add(textboxPort);   // 2
        widgets.Add(buttonBack);    // 3

        textboxIp.GetFocus();
    }

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        buttonConnect.text = menu.lang.Get("MainMenu_ConnectToIpConnect");
        textboxIp.description = menu.lang.Get("MainMenu_ConnectToIpIp");
        textboxPort.description = menu.lang.Get("MainMenu_ConnectToIpPort");
        title = menu.lang.Get("MainMenu_MultiplayerConnectIP");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        if (!loaded)
        {
            savedIp = menu.p.GetPreferences().GetString("ConnectToIpIp", "127.0.0.1");
            savedPort = menu.p.GetPreferences().GetString("ConnectToIpPort", "25565");
            textboxIp.text = savedIp;
            textboxPort.text = savedPort;
            loaded = true;
        }

        // Persist changes to preferences whenever the player edits either field.
        if (textboxIp.text != savedIp || textboxPort.text != savedPort)
        {
            savedIp = textboxIp.text;
            savedPort = textboxPort.text;

            Preferences prefs = menu.p.GetPreferences();
            prefs.SetString("ConnectToIpIp", savedIp);
            prefs.SetString("ConnectToIpPort", savedPort);
            menu.p.SetPreferences(prefs);
        }

        IGamePlatform p = menu.p;
        float scale = menu.GetScale();

        menu.DrawBackground();

        float leftx = p.GetCanvasWidth() / 2 - 400 * scale;
        float y = p.GetCanvasHeight() / 2 - 250 * scale;

        if (errorText != null)
        {
            menu.DrawText(errorText, 14 * scale, leftx, y - 50 * scale, TextAlign.Left, TextBaseline.Top);
        }

        menu.DrawText(title, 14 * scale, leftx, y + 50 * scale, TextAlign.Left, TextBaseline.Top);

        LayoutWidget(textboxIp, leftx, y + 100 * scale, 256, 64, scale);
        LayoutWidget(textboxPort, leftx, y + 200 * scale, 256, 64, scale);
        LayoutWidget(buttonConnect, leftx, y + 400 * scale, 256, 64, scale);

        buttonBack.x = 40 * scale;
        buttonBack.y = p.GetCanvasHeight() - 104 * scale;
        buttonBack.sizex = 256 * scale;
        buttonBack.sizey = 64 * scale;
        buttonBack.fontSize = 14 * scale;

        DrawWidgets();
    }

    /// <summary>
    /// Assigns position, size, and font size to a widget using pre-scaled base dimensions.
    /// </summary>
    private static void LayoutWidget(MenuWidget w, float x, float y, float w_, float h, float scale)
    {
        w.x = x;
        w.y = y;
        w.sizex = w_ * scale;
        w.sizey = h * scale;
        w.fontSize = 14 * scale;
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => menu.StartMultiplayer();

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        if (w == buttonBack)
        {
            OnBackPressed();
            return;
        }

        if (w == buttonConnect)
        {
            if (!string.IsNullOrEmpty(textboxIp.text)
                && int.TryParse(textboxPort.text, out int port))
            {
                menu.StartLogin(null, textboxIp.text, port);
            }
        }
    }
}