/// <summary>
/// Screen that handles player authentication. Supports two paths:
/// <list type="bullet">
///   <item><description>
///     <b>Server hash login</b> — authenticates against the main game servers
///     using a username, password, and the server's public hash.
///   </description></item>
///   <item><description>
///     <b>Direct IP login</b> — skips authentication and connects directly to
///     <see cref="serverIp"/>:<see cref="serverPort"/> using the entered username.
///   </description></item>
/// </list>
/// On first render, any saved credentials are tried automatically.
/// </summary>
public class LoginScreen : ScreenBase
{
    private readonly MenuWidget buttonLogin;
    private readonly MenuWidget textboxUsername;
    private readonly MenuWidget textboxPassword;
    private readonly MenuWidget buttonRememberMe;
    private readonly MenuWidget buttonBack;

    /// <summary>
    /// Create-account widgets are laid out on the right column but are hidden.
    /// Reserved for a future account-registration flow.
    /// </summary>
    private readonly MenuWidget buttonCreateAccount;
    private readonly MenuWidget textboxCreateUsername;
    private readonly MenuWidget textboxCreatePassword;
    private readonly MenuWidget buttonCreateRememberMe;

    /// <summary>
    /// Public hash of the target server, used for authenticated login.
    /// <see langword="null"/> when connecting directly by IP.
    /// </summary>
    internal string serverHash;

    /// <summary>Target server IP for direct-IP connections (no authentication).</summary>
    internal string serverIp;

    /// <summary>Target server port for direct-IP connections.</summary>
    internal int serverPort;

    private LoginResult loginResult;
    private LoginData loginResultData;

    /// <summary>Whether to persist credentials to preferences after a successful login.</summary>
    private bool rememberMe = true;

    private bool triedSavedLogin;
    private string title;

    public LoginScreen()
    {
        // Tab chain (by list index):
        // [1] Username → [2] Password → [3] RememberMe → [0] Login → [8] Back → [1] Username
        buttonLogin = new MenuWidget { text = "Login", type = WidgetType.Button, nextWidget = 8 };
        textboxUsername = new MenuWidget { text = "", type = WidgetType.Textbox, description = "Username", nextWidget = 2 };
        textboxPassword = new MenuWidget { text = "", type = WidgetType.Textbox, description = "Password", password = true, nextWidget = 3 };
        buttonRememberMe = new MenuWidget { text = "Yes", type = WidgetType.Button, description = "Remember me", nextWidget = 0 };
        buttonCreateAccount = new MenuWidget { text = "Create account", type = WidgetType.Button };
        textboxCreateUsername = new MenuWidget { text = "", type = WidgetType.Textbox, description = "Username" };
        textboxCreatePassword = new MenuWidget { text = "", type = WidgetType.Textbox, description = "Password", password = true };
        buttonCreateRememberMe = new MenuWidget { text = "Yes", type = WidgetType.Button, description = "Remember me" };
        buttonBack = new MenuWidget { text = "Back", type = WidgetType.Button, nextWidget = 1 };

        title = "Login";

        widgets.Add(buttonLogin);           // 0
        widgets.Add(textboxUsername);       // 1
        widgets.Add(textboxPassword);       // 2
        widgets.Add(buttonRememberMe);      // 3
        widgets.Add(buttonCreateAccount);   // 4
        widgets.Add(textboxCreateUsername); // 5
        widgets.Add(textboxCreatePassword); // 6
        widgets.Add(buttonCreateRememberMe);// 7
        widgets.Add(buttonBack);            // 8

        textboxUsername.GetFocus();
        loginResult = new LoginResult();
    }

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        buttonLogin.text = menu.lang.Get("MainMenu_Login");
        textboxUsername.description = menu.lang.Get("MainMenu_LoginUsername");
        textboxPassword.description = menu.lang.Get("MainMenu_LoginPassword");
        buttonRememberMe.description = menu.lang.Get("MainMenu_LoginRemember");
        buttonBack.text = menu.lang.Get("MainMenu_ButtonBack");
        title = menu.lang.Get("MainMenu_Login");

        // Keep the toggle label in sync with the current state.
        UpdateRememberMeLabel();
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        if (!triedSavedLogin)
        {
            TrySavedLogin();
            triedSavedLogin = true;
        }

        // Auto-advance when a pending login attempt succeeds.
        if (loginResultData != null
            && loginResultData.ServerCorrect
            && loginResultData.PasswordCorrect)
        {
            if (rememberMe)
            {
                SaveCredentials(loginResultData.Token);
            }
            menu.ConnectToGame(loginResultData, textboxUsername.text);
        }

        IGamePlatform p = menu.p;
        float scale = menu.GetScale();

        menu.DrawBackground();

        float leftx = p.GetCanvasWidth() / 2 - 400 * scale;
        float rightx = p.GetCanvasWidth() / 2 + 150 * scale;
        float y = p.GetCanvasHeight() / 2 - 250 * scale;

        string loginResultText = loginResult switch
        {
            LoginResult.Failed => menu.lang.Get("MainMenu_LoginInvalid"),
            LoginResult.Connecting => menu.lang.Get("MainMenu_LoginConnecting"),
            _ => null
        };

        if (loginResultText != null)
        {
            menu.DrawText(loginResultText, 14 * scale, leftx, y - 50 * scale, TextAlign.Left, TextBaseline.Top);
        }

        menu.DrawText(title, 14 * scale, leftx, y + 50 * scale, TextAlign.Left, TextBaseline.Top);

        LayoutWidget(textboxUsername, leftx, y + 100 * scale, scale);
        LayoutWidget(textboxPassword, leftx, y + 200 * scale, scale);
        LayoutWidget(buttonRememberMe, leftx, y + 300 * scale, scale);
        LayoutWidget(buttonLogin, leftx, y + 400 * scale, scale);

        // Create-account column — laid out but hidden pending a future registration flow.
        LayoutWidget(textboxCreateUsername, rightx, y + 100 * scale, scale);
        LayoutWidget(textboxCreatePassword, rightx, y + 200 * scale, scale);
        LayoutWidget(buttonCreateRememberMe, rightx, y + 300 * scale, scale);
        LayoutWidget(buttonCreateAccount, rightx, y + 400 * scale, scale);

        textboxCreateUsername.visible = false;
        textboxCreatePassword.visible = false;
        buttonCreateRememberMe.visible = false;
        buttonCreateAccount.visible = false;

        buttonBack.x = 40 * scale;
        buttonBack.y = p.GetCanvasHeight() - 104 * scale;
        buttonBack.sizex = 256 * scale;
        buttonBack.sizey = 64 * scale;
        buttonBack.fontSize = 14 * scale;

        DrawWidgets();
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

        if (w == buttonLogin)
        {
            loginResultData = new LoginData();

            if (serverHash != null)
            {
                // Authenticated login via the main game servers.
                menu.Login(textboxUsername.text, textboxPassword.text,
                    serverHash, "", loginResult, loginResultData);
            }
            else
            {
                // Direct-IP connection — no authentication required.
                if (rememberMe)
                {
                    SaveUsername();
                }
                menu.StartGame(false, null, new ConnectData
                {
                    Ip = serverIp,
                    Port = serverPort,
                    Username = textboxUsername.text
                });
            }
            return;
        }

        if (w == buttonCreateAccount)
        {
            loginResult = MainMenu.CreateAccount(
                textboxCreateUsername.text, textboxCreatePassword.text);
            return;
        }

        if (w == buttonRememberMe || w == buttonCreateRememberMe)
        {
            rememberMe = !rememberMe;
            UpdateRememberMeLabel();
        }
    }

    /// <summary>
    /// Attempts to log in automatically using credentials saved from a previous session.
    /// Only runs when a <see cref="serverHash"/> is present and a saved token exists.
    /// </summary>
    private void TrySavedLogin()
    {
        Preferences prefs = menu.p.GetPreferences();
        textboxUsername.text = prefs.GetString("Username", "");
        textboxPassword.text = "";

        string token = prefs.GetString("Password", "");
        loginResultData = new LoginData();

        if (serverHash != null && token != "")
        {
            menu.Login(textboxUsername.text, textboxPassword.text,
                serverHash, token, loginResult, loginResultData);
        }
    }

    /// <summary>
    /// Persists the current username and, optionally, an updated auth token to preferences.
    /// </summary>
    /// <param name="token">New token returned by the server, or <see langword="null"/> to leave the stored token unchanged.</param>
    private void SaveCredentials(string token)
    {
        Preferences prefs = menu.p.GetPreferences();
        prefs.SetString("Username", textboxUsername.text);
        if (!string.IsNullOrEmpty(token))
        {
            prefs.SetString("Password", token);
        }
        menu.p.SetPreferences(prefs);
    }

    /// <summary>Persists only the username to preferences (used for direct-IP connections).</summary>
    private void SaveUsername()
    {
        Preferences prefs = menu.p.GetPreferences();
        prefs.SetString("Username", textboxUsername.text);
        menu.p.SetPreferences(prefs);
    }

    /// <summary>
    /// Syncs the remember-me button label with the current <see cref="rememberMe"/> state.
    /// </summary>
    private void UpdateRememberMeLabel()
    {
        string label = rememberMe
            ? menu.lang.Get("MainMenu_ChoiceYes")
            : menu.lang.Get("MainMenu_ChoiceNo");

        buttonRememberMe.text = label;
        buttonCreateRememberMe.text = label;
    }

    /// <summary>
    /// Assigns position, size, and font size to a widget using standard 256×64 logical dimensions.
    /// </summary>
    private static void LayoutWidget(MenuWidget w, float x, float y, float scale)
    {
        w.x = x;
        w.y = y;
        w.sizex = 256 * scale;
        w.sizey = 64 * scale;
        w.fontSize = 14 * scale;
    }
}