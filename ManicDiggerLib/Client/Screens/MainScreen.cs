using OpenTK.Windowing.Common;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// The top-level main menu screen. Displays the game logo and the three primary
/// navigation buttons: Singleplayer, Multiplayer, and Quit.
/// </summary>
/// <remarks>
/// On first render (after assets finish loading) the screen checks for an
/// <c>?ip=&amp;port=</c> query string and auto-connects if one is present.
/// </remarks>
public class MainScreen : ScreenBase
{
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

    public MainScreen()
    {
        buttonSingleplayer = new MenuWidget { text = "Singleplayer" };
        buttonMultiplayer = new MenuWidget { text = "Multiplayer" };
        buttonExit = new MenuWidget { text = "Quit" };

        widgets.Add(buttonSingleplayer);
        widgets.Add(buttonMultiplayer);
        widgets.Add(buttonExit);
    }

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        buttonSingleplayer.text = menu.lang.Get("MainMenu_Singleplayer");
        buttonMultiplayer.text = menu.lang.Get("MainMenu_Multiplayer");
        buttonExit.text = menu.lang.Get("MainMenu_Quit");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        windowX = menu.p.GetCanvasWidth();
        windowY = menu.p.GetCanvasHeight();

        float scale = menu.GetScale();

        if (menu.assetsLoadProgress != 1)
        {
            string s = string.Format(menu.lang.Get("MainMenu_AssetsLoadProgress"),
                ((int)(menu.assetsLoadProgress * 100)).ToString());
            menu.DrawText(s, 20 * scale, windowX / 2, windowY / 2, TextAlign.Center, TextBaseline.Middle);
            return;
        }

        if (!cursorLoaded)
        {
            menu.p.SetWindowCursor(0, 0, 32, 32,
                menu.GetFile("mousecursor.png"),
                menu.GetFileLength("mousecursor.png"));
            cursorLoaded = true;
        }

        UseQueryStringIpAndPort();

        menu.DrawBackground();
        menu.Draw2dQuad(menu.GetTexture("logo.png"),
            windowX / 2 - 1024 * scale / 2, 0, 1024 * scale, 512 * scale);

        float centerX = windowX / 2 - ButtonWidth / 2 * scale;

        buttonSingleplayer.x = centerX;
        buttonSingleplayer.y = ButtonY(3, scale);
        buttonSingleplayer.sizex = ButtonWidth * scale;
        buttonSingleplayer.sizey = ButtonHeight * scale;

        buttonMultiplayer.x = centerX;
        buttonMultiplayer.y = ButtonY(2, scale);
        buttonMultiplayer.sizex = ButtonWidth * scale;
        buttonMultiplayer.sizey = ButtonHeight * scale;

        buttonExit.visible = menu.p.ExitAvailable();
        buttonExit.x = centerX;
        buttonExit.y = ButtonY(1, scale);
        buttonExit.sizex = ButtonWidth * scale;
        buttonExit.sizey = ButtonHeight * scale;

        DrawWidgets();
    }

    /// <summary>
    /// Returns the Y position for a button at the given slot (1 = bottom, 2 = above that, etc.)
    /// measured from the bottom of the canvas.
    /// </summary>
    private float ButtonY(int slot, float scale)
        => windowY - slot * (ButtonHeight * scale + SpaceBetween) - OffsetFromBorder * scale;

    /// <summary>
    /// On the first call, checks the page query string for <c>ip</c> and <c>port</c>
    /// parameters and auto-starts a login if an IP is found. Subsequent calls are no-ops.
    /// </summary>
    private void UseQueryStringIpAndPort()
    {
        if (queryStringChecked) { return; }
        queryStringChecked = true;

        string ip = menu.p.QueryStringValue("ip");
        string port = menu.p.QueryStringValue("port");

        int portInt = int.TryParse(port, out int parsedPort) ? parsedPort : 25565;

        if (ip != null)
        {
            menu.StartLogin(null, ip, portInt);
        }
    }

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        if (w == buttonSingleplayer) { menu.StartSingleplayer(); return; }
        if (w == buttonMultiplayer) { menu.StartMultiplayer(); return; }
        if (w == buttonExit) { menu.Exit(); return; }
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => menu.Exit();

    /// <inheritdoc/>
    public override void OnKeyDown(KeyEventArgs e)
    {
#if DEBUG
        // F5 — launch default singleplayer save (legacy .mdss format).
        if (e.KeyChar == (int)Keys.F5)
        {
            menu.p.SinglePlayerServerDisable();
            menu.StartGame(true, Path.Combine(menu.p.PathSavegames(), "Default.mdss"), null);
        }
        // F6 — launch default singleplayer save (database .mddbs format).
        if (e.KeyChar == (int)Keys.F6)
        {
            menu.StartGame(true, Path.Combine(menu.p.PathSavegames(), "Default.mddbs"), null);
        }
#endif
    }
}