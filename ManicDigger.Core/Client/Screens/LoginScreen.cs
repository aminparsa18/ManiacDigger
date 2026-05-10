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
public class LoginScreen : ScreenBase, ILoginScreen
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

    private readonly IScreenManager _menu;
    private readonly IPreferences preferences;
    private readonly ILanguageService _languageService;

    public LoginScreen(IGameWindowService platform, IPreferences preferences, IOpenGlService openGlService, IAssetManager assetManager, ILanguageService languageService, IScreenManager menu)
        : base(platform, openGlService, assetManager)
    {
        this.preferences = preferences;
        _languageService = languageService;
        _menu = menu;

        // Tab chain (by list index):
        // [1] Username → [2] Password → [3] RememberMe → [0] Login → [8] Back → [1] Username
        buttonLogin = new MenuWidget { Text = "Login", Type = UIWidgetType.Button, NextWidget = 8 };
        textboxUsername = new MenuWidget { Text = "", Type = UIWidgetType.Textbox, Description = "Username", NextWidget = 2 };
        textboxPassword = new MenuWidget { Text = "", Type = UIWidgetType.Textbox, Description = "Password", Password = true, NextWidget = 3 };
        buttonRememberMe = new MenuWidget { Text = "Yes", Type = UIWidgetType.Button, Description = "Remember me", NextWidget = 0 };
        buttonCreateAccount = new MenuWidget { Text = "Create account", Type = UIWidgetType.Button };
        textboxCreateUsername = new MenuWidget { Text = "", Type = UIWidgetType.Textbox, Description = "Username" };
        textboxCreatePassword = new MenuWidget { Text = "", Type = UIWidgetType.Textbox, Description = "Password", Password = true };
        buttonCreateRememberMe = new MenuWidget { Text = "Yes", Type = UIWidgetType.Button, Description = "Remember me" };
        buttonBack = new MenuWidget { Text = "Back", Type = UIWidgetType.Button, NextWidget = 1 };

        title = "Login";

        Widgets.Add(buttonLogin);           // 0
        Widgets.Add(textboxUsername);       // 1
        Widgets.Add(textboxPassword);       // 2
        Widgets.Add(buttonRememberMe);      // 3
        Widgets.Add(buttonCreateAccount);   // 4
        Widgets.Add(textboxCreateUsername); // 5
        Widgets.Add(textboxCreatePassword); // 6
        Widgets.Add(buttonCreateRememberMe);// 7
        Widgets.Add(buttonBack);            // 8

        textboxUsername.GetFocus();
        loginResult = new LoginResult();
    }

    public void Configure(string serverHash, string ip, int port)
    {
        this.serverHash = serverHash;
        serverIp = ip;
        serverPort = port;
    }

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        buttonLogin.Text = _languageService.Get("MainMenu_Login");
        textboxUsername.Description = _languageService.Get("MainMenu_LoginUsername");
        textboxPassword.Description = _languageService.Get("MainMenu_LoginPassword");
        buttonRememberMe.Description = _languageService.Get("MainMenu_LoginRemember");
        buttonBack.Text = _languageService.Get("MainMenu_ButtonBack");
        title = _languageService.Get("MainMenu_Login");

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

            _menu.ConnectToGame(loginResultData, textboxUsername.Text);
        }

        float scale = GetScale();

        DrawBackground();

        float leftx = (GameService.CanvasWidth / 2) - (400 * scale);
        float rightx = (GameService.CanvasWidth / 2) + (150 * scale);
        float y = (GameService.CanvasHeight / 2) - (250 * scale);

        string loginResultText = loginResult switch
        {
            LoginResult.Failed => _languageService.Get("MainMenu_LoginInvalid"),
            LoginResult.Connecting => _languageService.Get("MainMenu_LoginConnecting"),
            _ => null
        };

        if (loginResultText != null)
        {
            DrawText(loginResultText, 14 * scale, leftx, y - (50 * scale), TextAlign.Left, TextBaseline.Top);
        }

        DrawText(title, 14 * scale, leftx, y + (50 * scale), TextAlign.Left, TextBaseline.Top);

        LayoutWidget(textboxUsername, leftx, y + (100 * scale), scale);
        LayoutWidget(textboxPassword, leftx, y + (200 * scale), scale);
        LayoutWidget(buttonRememberMe, leftx, y + (300 * scale), scale);
        LayoutWidget(buttonLogin, leftx, y + (400 * scale), scale);

        // Create-account column — laid out but hidden pending a future registration flow.
        LayoutWidget(textboxCreateUsername, rightx, y + (100 * scale), scale);
        LayoutWidget(textboxCreatePassword, rightx, y + (200 * scale), scale);
        LayoutWidget(buttonCreateRememberMe, rightx, y + (300 * scale), scale);
        LayoutWidget(buttonCreateAccount, rightx, y + (400 * scale), scale);

        textboxCreateUsername.Visible = false;
        textboxCreatePassword.Visible = false;
        buttonCreateRememberMe.Visible = false;
        buttonCreateAccount.Visible = false;

        buttonBack.X = 40 * scale;
        buttonBack.Y = GameService.CanvasHeight - (104 * scale);
        buttonBack.Sizex = 256 * scale;
        buttonBack.Sizey = 64 * scale;
        buttonBack.FontSize = 14 * scale;

        DrawWidgets();
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => _menu.StartMultiplayer();

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
                _menu.Login(textboxUsername.Text, textboxPassword.Text,
                    serverHash, "", loginResult, loginResultData);
            }
            else
            {
                // Direct-IP connection — no authentication required.
                if (rememberMe)
                {
                    SaveUsername();
                }

                _menu.StartGame(false, new ConnectionData
                {
                    Ip = serverIp,
                    Port = serverPort,
                    Username = textboxUsername.Text
                });
            }

            return;
        }

        if (w == buttonCreateAccount)
        {
            loginResult = ScreenManager.CreateAccount(
                textboxCreateUsername.Text, textboxCreatePassword.Text);
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
        textboxUsername.Text = preferences.GetString("Username", "");
        textboxPassword.Text = "";

        string token = preferences.GetString("Password", "");
        loginResultData = new LoginData();

        if (serverHash != null && token != "")
        {
            _menu.Login(textboxUsername.Text, textboxPassword.Text,
                serverHash, token, loginResult, loginResultData);
        }
    }

    /// <summary>
    /// Persists the current username and, optionally, an updated auth token to preferences.
    /// </summary>
    /// <param name="token">New token returned by the server, or <see langword="null"/> to leave the stored token unchanged.</param>
    private void SaveCredentials(string token)
    {
        preferences.SetString("Username", textboxUsername.Text);
        if (!string.IsNullOrEmpty(token))
        {
            preferences.SetString("Password", token);
        }

        preferences.SetValues();
    }

    /// <summary>Persists only the username to preferences (used for direct-IP connections).</summary>
    private void SaveUsername()
    {
        preferences.SetString("Username", textboxUsername.Text);
        preferences.SetValues();
    }

    /// <summary>
    /// Syncs the remember-me button label with the current <see cref="rememberMe"/> state.
    /// </summary>
    private void UpdateRememberMeLabel()
    {
        string label = rememberMe
            ? _languageService.Get("MainMenu_ChoiceYes")
            : _languageService.Get("MainMenu_ChoiceNo");

        buttonRememberMe.Text = label;
        buttonCreateRememberMe.Text = label;
    }

    /// <summary>
    /// Assigns position, size, and font size to a widget using standard 256×64 logical dimensions.
    /// </summary>
    private static void LayoutWidget(MenuWidget w, float x, float y, float scale)
    {
        w.X = x;
        w.Y = y;
        w.Sizex = 256 * scale;
        w.Sizey = 64 * scale;
        w.FontSize = 14 * scale;
    }
}