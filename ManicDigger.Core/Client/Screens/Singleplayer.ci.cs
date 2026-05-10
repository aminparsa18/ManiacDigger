using ManicDigger;

/// <summary>
/// Screen that shows available singleplayer save files and lets the player
/// launch, create, or modify a world.
/// </summary>
/// <remarks>
/// When the platform does not support singleplayer (i.e. web/mobile), all world
/// widgets are hidden and an explanatory message is displayed instead.
/// </remarks>
public class SingleplayerScreen : ScreenBase, ISingleplayerScreen
{
    // Maximum number of save-file buttons shown in the list.
    private const int MaxWorldButtons = 10;

    private readonly MenuWidget play;
    private readonly MenuWidget newWorld;
    private readonly MenuWidget modify;
    private readonly MenuWidget back;
    private readonly MenuWidget open;

    /// <summary>Dynamically populated buttons, one per discovered save file (up to <see cref="MaxWorldButtons"/>).</summary>
    private readonly MenuWidget[] worldButtons;

    private readonly IScreenManager _menu;
    private readonly ISinglePlayerService singlePlayerService;
    private readonly ILanguageService _languageService;
    private readonly ISaveGameService saveGameService;

    /// <summary>
    /// Save files discovered on first render. <c>null</c> until the first call to
    /// <see cref="Render"/> so that the file scan is deferred until the screen is actually shown.
    /// </summary>
    private List<string> savegames;

    private string title;

    public SingleplayerScreen(IGameWindowService platform, IOpenGlService platformOpenGl, IAssetManager assetManager,
        ISinglePlayerService singlePlayerService, ILanguageService languageService, IScreenManager menu, ISaveGameService saveGameService) : base(platform, platformOpenGl, assetManager)
    {
        play = new MenuWidget
        {
            Text = "Play"
        };
        newWorld = new MenuWidget
        {
            Text = "New World"
        };
        modify = new MenuWidget
        {
            Text = "Modify"
        };
        back = new MenuWidget
        {
            Text = "Back",
            Type = UIWidgetType.Button
        };
        open = new MenuWidget
        {
            Text = "Create or open...",
            Type = UIWidgetType.Button
        };

        title = "Singleplayer";

        Widgets.Add(play);
        Widgets.Add(newWorld);
        Widgets.Add(modify);
        Widgets.Add(back);
        Widgets.Add(open);

        worldButtons = new MenuWidget[MaxWorldButtons];
        for (int i = 0; i < MaxWorldButtons; i++)
        {
            worldButtons[i] = new MenuWidget { Visible = false };
            Widgets.Add(worldButtons[i]);
        }

        _menu = menu;
        _languageService = languageService;
        this.singlePlayerService = singlePlayerService;
        this.saveGameService = saveGameService;
    }

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        back.Text = _languageService.Get("MainMenu_ButtonBack");
        open.Text = _languageService.Get("MainMenu_SingleplayerButtonCreate");
        title = _languageService.Get("MainMenu_Singleplayer");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        float scale = GetScale();

        DrawBackground();
        DrawText(title, 20 * scale, GameService.CanvasWidth / 2, 10, TextAlign.Center, TextBaseline.Top);

        float leftx = (GameService.CanvasWidth / 2) - (128 * scale);
        float y = (GameService.CanvasHeight / 2) + (0 * scale);

        play.X = leftx;
        play.Y = y + (100 * scale);
        play.Sizex = 256 * scale;
        play.Sizey = 64 * scale;
        play.FontSize = 14 * scale;

        newWorld.X = leftx;
        newWorld.Y = y + (170 * scale);
        newWorld.Sizex = 256 * scale;
        newWorld.Sizey = 64 * scale;
        newWorld.FontSize = 14 * scale;

        modify.X = leftx;
        modify.Y = y + (240 * scale);
        modify.Sizex = 256 * scale;
        modify.Sizey = 64 * scale;
        modify.FontSize = 14 * scale;

        back.X = 40 * scale;
        back.Y = GameService.CanvasHeight - (104 * scale);
        back.Sizex = 256 * scale;
        back.Sizey = 64 * scale;
        back.FontSize = 14 * scale;

        open.X = leftx;
        open.Y = y + (0 * scale);
        open.Sizex = 256 * scale;
        open.Sizey = 64 * scale;
        open.FontSize = 14 * scale;

        // Deferred scan: only read the filesystem once, on the first render.
        savegames ??= GetSaveGames();

        // Reset all world buttons, then re-enable one per discovered save file.
        for (int i = 0; i < MaxWorldButtons; i++)
        {
            worldButtons[i].Visible = false;
        }

        for (int i = 0; i < savegames.Count; i++)
        {
            worldButtons[i].Visible = true;
            worldButtons[i].Text = Path.GetFileNameWithoutExtension(savegames[i]);
            worldButtons[i].X = leftx;
            worldButtons[i].Y = 100 + (100 * scale * i);
            worldButtons[i].Sizex = 256 * scale;
            worldButtons[i].Sizey = 64 * scale;
            worldButtons[i].FontSize = 14 * scale;
        }

        // Only the Open button is active on supporting platforms.
        // Play, NewWorld, Modify, and worldButtons are reserved for a future
        // save-file browser and are hidden until that UI is implemented.
        open.Visible = singlePlayerService.SinglePlayerServerAvailable;
        play.Visible = false;
        newWorld.Visible = false;
        modify.Visible = false;
        for (int i = 0; i < savegames.Count; i++)
        {
            worldButtons[i].Visible = false;
        }

        DrawWidgets();

        if (!singlePlayerService.SinglePlayerServerAvailable)
        {
            DrawText(
                "Singleplayer is only available on desktop (Windows, Linux, Mac) version of game.",
                16 * scale, GameService.CanvasWidth / 2, GameService.CanvasHeight / 2,
                TextAlign.Center, TextBaseline.Middle);
        }
    }

    // -------------------------------------------------------------------------
    // Save-game helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Scans <see cref="GameWindowService.GameSavePath"/> for <c>.mddbs</c> files
    /// and returns their paths. Files without the expected extension are excluded.
    /// </summary>
    /// <returns>List of fully-qualified paths to every discovered <c>.mddbs</c> save file.</returns>
    private List<string> GetSaveGames()
    {
        string[] files = FileHelper.DirectoryGetFiles(GameService.GameSavePath);
        List<string> savegames = [];

        foreach (string file in files)
        {
            if (file.EndsWith(FileConstatns.DbFileExtension, StringComparison.OrdinalIgnoreCase))
            {
                savegames.Add(file);
            }
        }

        return savegames;
    }

    private static string GetDefaultSavePath(string extension)
    {
        string folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Manic Digger Save");
        Directory.CreateDirectory(folder); // no-op if already exists
        return Path.Combine(folder, "Default." + extension);
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => _menu.StartMainMenu();

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        for (int i = 0; i < MaxWorldButtons; i++)
        {
            worldButtons[i].Selected = false;
        }

        for (int i = 0; i < MaxWorldButtons; i++)
        {
            if (worldButtons[i] == w)
            {
                worldButtons[i].Selected = true;
            }
        }

        if (w == back)
        {
            OnBackPressed();
        }

        if (w == open)
        {
            string extension = singlePlayerService.SinglePlayerServerAvailable ? "mddbs" : "mdss";
            string path = GetDefaultSavePath(extension);

            saveGameService.InitialiseSession(
                File.Exists(path)
                    ? SaveTarget.FromFile(path)
                    : SaveTarget.NewGame());

            _menu.ConnectToSingleplayer();
        }
    }
}