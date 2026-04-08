public class ScreenConnectToIp : Screen
{
    public ScreenConnectToIp()
    {
        buttonConnect = new MenuWidget
        {
            text = "Connect",
            type = WidgetType.Button,
            nextWidget = 3
        };
        textboxIp = new MenuWidget
        {
            type = WidgetType.Textbox,
            text = "",
            description = "Ip",
            nextWidget = 2
        };
        textboxPort = new MenuWidget
        {
            type = WidgetType.Textbox,
            text = "",
            description = "Port",
            nextWidget = 0
        };

        back = new MenuWidget
        {
            text = "Back",
            type = WidgetType.Button,
            nextWidget = 1
        };

        title = "Connect to IP";

        widgets[0] = buttonConnect;
        widgets[1] = textboxIp;
        widgets[2] = textboxPort;
        widgets[3] = back;

        textboxIp.GetFocus();
    }

    private readonly MenuWidget buttonConnect;
    private readonly MenuWidget textboxIp;
    private readonly MenuWidget textboxPort;

    private readonly MenuWidget back;

    private bool loaded;
    private string title;

    public override void LoadTranslations()
    {
        buttonConnect.text = menu.lang.Get("MainMenu_ConnectToIpConnect");
        textboxIp.description = menu.lang.Get("MainMenu_ConnectToIpIp");
        textboxPort.description = menu.lang.Get("MainMenu_ConnectToIpPort");
        title = menu.lang.Get("MainMenu_MultiplayerConnectIP");
    }

    private string preferences_ip;
    private string preferences_port;
    public override void Render(float dt)
    {
        if (!loaded)
        {
            preferences_ip = menu.p.GetPreferences().GetString("ConnectToIpIp", "127.0.0.1");
            preferences_port = menu.p.GetPreferences().GetString("ConnectToIpPort", "25565");
            textboxIp.text = preferences_ip;
            textboxPort.text = preferences_port;
            loaded = true;
        }

        if (textboxIp.text != preferences_ip
            || textboxPort.text != preferences_port)
        {
            preferences_ip = textboxIp.text;
            preferences_port = textboxPort.text;
            Preferences preferences = menu.p.GetPreferences();
            preferences.SetString("ConnectToIpIp", preferences_ip);
            preferences.SetString("ConnectToIpPort", preferences_port);
            menu.p.SetPreferences(preferences);
        }

        GamePlatform p = menu.p;
        float scale = menu.GetScale();
        menu.DrawBackground();


        float leftx = p.GetCanvasWidth() / 2 - 400 * scale;
        float y = p.GetCanvasHeight() / 2 - 250 * scale;

        string loginResultText = null;
        if (errorText != null)
        {
            menu.DrawText(loginResultText, 14 * scale, leftx, y - 50 * scale, TextAlign.Left, TextBaseline.Top);
        }

        menu.DrawText(title, 14 * scale, leftx, y + 50 * scale, TextAlign.Left, TextBaseline.Top);

        textboxIp.x = leftx;
        textboxIp.y = y + 100 * scale;
        textboxIp.sizex = 256 * scale;
        textboxIp.sizey = 64 * scale;
        textboxIp.fontSize = 14 * scale;

        textboxPort.x = leftx;
        textboxPort.y = y + 200 * scale;
        textboxPort.sizex = 256 * scale;
        textboxPort.sizey = 64 * scale;
        textboxPort.fontSize = 14 * scale;

        buttonConnect.x = leftx;
        buttonConnect.y = y + 400 * scale;
        buttonConnect.sizex = 256 * scale;
        buttonConnect.sizey = 64 * scale;
        buttonConnect.fontSize = 14 * scale;

        back.x = 40 * scale;
        back.y = p.GetCanvasHeight() - 104 * scale;
        back.sizex = 256 * scale;
        back.sizey = 64 * scale;
        back.fontSize = 14 * scale;

        DrawWidgets();
    }

    private readonly string errorText;

    public override void OnBackPressed()
    {
        menu.StartMultiplayer();
    }

    public override void OnButton(MenuWidget w)
    {
        if (w == buttonConnect)
        {
            if (!string.Equals(textboxIp.text, "")
                && float.TryParse(textboxPort.text, out _))
            {
                menu.StartLogin(null, textboxIp.text, int.Parse(textboxPort.text));
            }
        }
        if (w == back)
        {
            OnBackPressed();
        }
    }
}
