using OpenTK.Windowing.Common;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public class ModGuiEscapeMenu : ModBase
{
    private readonly IGameService platform;
    private readonly IPreferences preferences;
    private readonly IOpenGlService openGlService;
    private readonly ITerrainChunkTesselator _terrainChunkTesselator;

    public ModGuiEscapeMenu(IGameService platform, IPreferences preferences, IOpenGlService openGlService, IGame game, ITerrainChunkTesselator terrainChunkTesselator) : base(game)
    {
        this.platform = platform;
        this.preferences = preferences;
        this.openGlService = openGlService;
        _terrainChunkTesselator = terrainChunkTesselator;
        fonts = new string[4];
        fonts[0] = "Nice";
        fonts[1] = "Simple";
        fonts[2] = "BlackBackground";
        fonts[3] = "Default";
        fontsLength = 4;
        fontValues = new int[4];
        fontValues[0] = 0;
        fontValues[1] = 1;
        fontValues[2] = 2;
        fontValues[3] = 3;
        widgets = new Button[1024];
        keyselectid = -1;
    }

    private Button buttonMainReturnToGame;
    private Button buttonMainOptions;
    private Button buttonMainExit;

    private int widgetsCount;
    private void MainSet(IGame game)
    {
        LanguageService language = game.Language;
        buttonMainReturnToGame = new Button
        {
            Text = language.ReturnToGame()
        };
        buttonMainOptions = new Button
        {
            Text = language.Options()
        };
        buttonMainExit = new Button
        {
            Text = language.Exit()
        };

        WidgetsClear();
        AddWidget(buttonMainReturnToGame);
        AddWidget(buttonMainOptions);
        AddWidget(buttonMainExit);
    }

    private void MainHandleClick(Button b)
    {
        if (b == buttonMainReturnToGame)
        {
            Game.GuiStateBackToGame();
        }

        if (b == buttonMainOptions)
        {
            SetEscapeMenuState(EscapeMenuState.Options);
        }

        if (b == buttonMainExit)
        {
            Game.SendLeave(PacketLeaveReason.Leave);
            Game.ExitToMainMenu();
        }
    }

    private Button optionsGraphics;
    private Button optionsKeys;
    private Button optionsOther;
    private Button optionsReturnToMainMenu;
    private void OptionsSet()
    {
        LanguageService language = Game.Language;
        optionsGraphics = new Button
        {
            Text = language.Graphics()
        };
        optionsKeys = new Button
        {
            Text = language.Keys()
        };
        optionsOther = new Button
        {
            Text = language.Other()
        };
        optionsReturnToMainMenu = new Button
        {
            Text = language.ReturnToMainMenu()
        };

        WidgetsClear();
        AddWidget(optionsGraphics);
        AddWidget(optionsKeys);
        AddWidget(optionsOther);
        AddWidget(optionsReturnToMainMenu);
    }

    private void OptionsHandleClick(Button b)
    {
        if (b == optionsGraphics)
        {
            SetEscapeMenuState(EscapeMenuState.Graphics);
        }

        if (b == optionsKeys)
        {
            SetEscapeMenuState(EscapeMenuState.Keys);
        }

        if (b == optionsOther)
        {
            SetEscapeMenuState(EscapeMenuState.Other);
        }

        if (b == optionsReturnToMainMenu)
        {
            SaveOptions();
            SetEscapeMenuState(EscapeMenuState.Main);
        }
    }

    private Button graphicsOptionSmoothShadows;
    private Button graphicsOptionDarkenSides;
    private Button graphicsViewDistanceOption;
    private Button graphicsOptionFramerate;
    private Button graphicsOptionResolution;
    private Button graphicsOptionFullscreen;
    private Button graphicsUseServerTexturesOption;
    private Button graphicsFontOption;
    private Button graphicsReturnToOptionsMenu;
    private void GraphicsSet(IGame game)
    {
        GameOption options = game.options;
        LanguageService language = game.Language;
        graphicsOptionSmoothShadows = new Button
        {
            Text = string.Format(language.OptionSmoothShadows(), options.Smoothshadows ? language.On() : language.Off())
        };
        graphicsOptionDarkenSides = new Button
        {
            Text = string.Format(language.Get("OptionDarkenSides"), options.EnableBlockShadow ? language.On() : language.Off())
        };
        graphicsViewDistanceOption = new Button
        {
            Text = string.Format(language.ViewDistanceOption(), ((int)game.Config3d.ViewDistance).ToString())
        };
        graphicsOptionFramerate = new Button
        {
            Text = string.Format(language.OptionFramerate(), VsyncString())
        };
        graphicsOptionResolution = new Button
        {
            Text = string.Format(language.OptionResolution(), ResolutionString())
        };
        graphicsOptionFullscreen = new Button
        {
            Text = string.Format(language.OptionFullscreen(), options.Fullscreen ? language.On() : language.Off())
        };
        graphicsUseServerTexturesOption = new Button
        {
            Text = string.Format(language.UseServerTexturesOption(), options.UseServerTextures ? language.On() : language.Off())
        };
        graphicsFontOption = new Button
        {
            Text = string.Format(language.FontOption(), FontString(game))
        };
        graphicsReturnToOptionsMenu = new Button
        {
            Text = language.ReturnToOptionsMenu()
        };

        WidgetsClear();
        AddWidget(graphicsOptionSmoothShadows);
        AddWidget(graphicsOptionDarkenSides);
        AddWidget(graphicsViewDistanceOption);
        AddWidget(graphicsOptionFramerate);
        AddWidget(graphicsOptionResolution);
        AddWidget(graphicsOptionFullscreen);
        AddWidget(graphicsUseServerTexturesOption);
        AddWidget(graphicsFontOption);
        AddWidget(graphicsReturnToOptionsMenu);
    }
    private void GraphicsHandleClick(Button b)
    {
        GameOption options = Game.options;
        if (b == graphicsOptionSmoothShadows)
        {
            options.Smoothshadows = !options.Smoothshadows;
            _terrainChunkTesselator.EnableSmoothLight = options.Smoothshadows;
            if (options.Smoothshadows)
            {
                options.BlockShadowSave = 0.7f;
                _terrainChunkTesselator.BlockShadow = options.BlockShadowSave;
            }
            else
            {
                options.BlockShadowSave = 0.6f;
                _terrainChunkTesselator.BlockShadow = options.BlockShadowSave;
            }

            Game.RedrawAllBlocks();
        }

        if (b == graphicsOptionDarkenSides)
        {
            options.EnableBlockShadow = !options.EnableBlockShadow;
            _terrainChunkTesselator.DarkenBlockSidesOption = options.EnableBlockShadow;
            Game.RedrawAllBlocks();
        }

        if (b == graphicsViewDistanceOption)
        {
            Game.ToggleFog();
        }

        if (b == graphicsOptionFramerate)
        {
            Game.ToggleVsync();
        }

        if (b == graphicsOptionResolution)
        {
            ToggleResolution();
        }

        if (b == graphicsOptionFullscreen)
        {
            options.Fullscreen = !options.Fullscreen;
        }

        if (b == graphicsUseServerTexturesOption)
        {
            options.UseServerTextures = !options.UseServerTextures;
        }

        if (b == graphicsFontOption)
        {
            ToggleFont();
        }

        if (b == graphicsReturnToOptionsMenu)
        {
            UseFullscreen();
            UseResolution();
            SetEscapeMenuState(EscapeMenuState.Options);
        }
    }

    private Button otherSoundOption;
    private Button otherReturnToOptionsMenu;
    private Button otherAutoJumpOption;
    private Button otherLanguageSetting;
    private void OtherSet()
    {
        LanguageService language = Game.Language;

        otherSoundOption = new Button
        {
            Text = string.Format(language.SoundOption(), Game.AudioEnabled ? language.On() : language.Off())
        };
        otherAutoJumpOption = new Button
        {
            Text = string.Format(language.AutoJumpOption(), Game.AutoJumpEnabled ? language.On() : language.Off())
        };
        otherLanguageSetting = new Button
        {
            Text = string.Format(language.ClientLanguageOption(), language.GetUsedLanguage())
        };
        otherReturnToOptionsMenu = new Button
        {
            Text = language.ReturnToOptionsMenu()
        };

        WidgetsClear();
        AddWidget(otherSoundOption);
        AddWidget(otherAutoJumpOption);
        AddWidget(otherLanguageSetting);
        AddWidget(otherReturnToOptionsMenu);
    }

    private void OtherHandleClick(Button b)
    {
        if (b == otherSoundOption)
        {
            Game.AudioEnabled = !Game.AudioEnabled;
        }

        if (b == otherAutoJumpOption)
        {
            Game.AutoJumpEnabled = !Game.AutoJumpEnabled;
        }

        if (b == otherLanguageSetting)
        {
            //Switch language based on available languages
            Game.Language.NextLanguage();
        }

        if (b == otherReturnToOptionsMenu)
        {
            SetEscapeMenuState(EscapeMenuState.Options);
        }
    }


    private Button[] keyButtons;
    private Button keysDefaultKeys;
    private Button keysReturnToOptionsMenu;

    private const int keyButtonsCount = 1024;
    private void KeysSet(IGame game)
    {
        LanguageService language = game.Language;

        keyButtons = new Button[keyButtonsCount];
        for (int i = 0; i < keyButtonsCount; i++)
        {
            keyButtons[i] = null;
        }

        KeyHelp[] helps = keyhelps();
        for (int i = 0; i < keyButtonsCount; i++)
        {
            if (helps[i] == null)
            {
                break;
            }

            int defaultkey = helps[i].DefaultKey;
            int key = defaultkey;
            if (game.options.Keys[defaultkey] != 0)
            {
                key = game.options.Keys[defaultkey];
            }

            keyButtons[i] = new Button
            {
                Text = string.Format(language.KeyChange(), helps[i].Text, KeyName(key))
            };
            AddWidget(keyButtons[i]);

        }

        keysDefaultKeys = new Button
        {
            Text = language.DefaultKeys()
        };
        keysReturnToOptionsMenu = new Button
        {
            Text = language.ReturnToOptionsMenu()
        };
        AddWidget(keysDefaultKeys);
        AddWidget(keysReturnToOptionsMenu);
    }

    private void KeysHandleClick(Button b)
    {
        if (keyButtons != null)
        {
            for (int i = 0; i < keyButtonsCount; i++)
            {
                if (keyButtons[i] == b)
                {
                    keyselectid = i;
                }
            }
        }

        if (b == keysDefaultKeys)
        {
            Game.options.Keys = new int[256];
        }

        if (b == keysReturnToOptionsMenu)
        {
            SetEscapeMenuState(EscapeMenuState.Options);
        }
    }

    private void HandleButtonClick(Button w)
    {
        MainHandleClick(w);
        OptionsHandleClick(w);
        GraphicsHandleClick(w);
        OtherHandleClick(w);
        KeysHandleClick(w);
    }

    private void AddWidget(Button b) => widgets[widgetsCount++] = b;

    private void WidgetsClear() => widgetsCount = 0;

    private EscapeMenuState escapemenustate;
    private void EscapeMenuMouse1()
    {
        for (int i = 0; i < widgetsCount; i++)
        {
            Button w = widgets[i];
            w.selected = RectContains(w.x, w.y, w.width, w.height, Game.MouseCurrentX, Game.MouseCurrentY);
            if (w.selected && Game.MouseLeftClick)
            {
                HandleButtonClick(w);
                break;
            }
        }
    }

    private static bool RectContains(int x, int y, int w, int h, int px, int py)
    {
        return px >= x
            && py >= y
            && px < x + w
            && py < y + h;
    }

    private void SetEscapeMenuState(EscapeMenuState state)
    {
        LanguageService language = Game.Language;
        escapemenustate = state;
        WidgetsClear();
        if (state == EscapeMenuState.Main)
        {
            MainSet(Game);
            MakeSimpleOptions(20, 50);
        }
        else if (state == EscapeMenuState.Options)
        {
            OptionsSet();
            MakeSimpleOptions(20, 50);
        }
        else if (state == EscapeMenuState.Graphics)
        {
            GraphicsSet(Game);
            MakeSimpleOptions(20, 50);
        }
        else if (state == EscapeMenuState.Other)
        {
            OtherSet();
            MakeSimpleOptions(20, 50);
        }
        else if (state == EscapeMenuState.Keys)
        {
            KeysSet(Game);
            int fontsize = 12;
            int textheight = 20;
            MakeSimpleOptions(fontsize, textheight);
        }
    }

    private void UseFullscreen()
    {
        if (Game.options.Fullscreen)
        {
            if (!changedResolution)
            {
                originalResolutionWidth = platform.GetDisplayResolutionDefault().Width;
                originalResolutionHeight = platform.GetDisplayResolutionDefault().Height;
                changedResolution = true;
            }

            platform.SetWindowState(WindowState.Fullscreen);
            UseResolution();
        }
        else
        {
            platform.SetWindowState(WindowState.Normal);
            RestoreResolution();
        }
    }

    private string VsyncString()
    {
        if (Game.EnableLog == 0)
        {
            return "Vsync";
        }
        else if (Game.EnableLog == 1)
        {
            return "Unlimited";
        }
        else if (Game.EnableLog == 2)
        {
            return "Lag";
        }
        else
        {
            return null; //throw new Exception();
        }
    }

    private string ResolutionString()
    {
        DisplayResolutionCi res = platform.GetDisplayResolutions()[Game.options.Resolution];
        return string.Format("{0}x{1}, {2}, {3} Hz",
            res.Width.ToString(),
            res.Height.ToString(),
            res.BitsPerPixel.ToString(),
            ((int)res.RefreshRate).ToString());
    }

    private void ToggleResolution()
    {
        GameOption options  = Game.options;
        options.Resolution++;

        platform.GetDisplayResolutions();

        if (options.Resolution >= platform.GetDisplayResolutions().Count)
        {
            options.Resolution = 0;
        }
    }

    private int originalResolutionWidth;
    private int originalResolutionHeight;
    private bool changedResolution;
    public void RestoreResolution()
    {
        if (changedResolution)
        {
            platform.ChangeResolution(originalResolutionWidth, originalResolutionHeight, 32, -1);
            changedResolution = false;
        }
    }
    public void UseResolution()
    {
        GameOption options = Game.options;
        List<DisplayResolutionCi> resolutions = platform.GetDisplayResolutions();

        if (resolutions == null)
        {
            return;
        }

        if (options.Resolution >= resolutions.Count)
        {
            options.Resolution = 0;
        }

        DisplayResolutionCi res = resolutions[options.Resolution];
        if (platform.GetWindowState() == WindowState.Fullscreen)
        {
            platform.ChangeResolution(res.Width, res.Height, res.BitsPerPixel, res.RefreshRate);
            platform.SetWindowState(WindowState.Normal);
            platform.SetWindowState(WindowState.Fullscreen);
        }
        else
        {
            //d_GlWindow.Width = res.Width;
            //d_GlWindow.Height = res.Height;
        }
    }

    private readonly string[] fonts;
    private readonly int fontsLength;
    private readonly int[] fontValues;

    private string FontString(IGame game) => fonts[game.options.Font];

    private void ToggleFont()
    {
        GameOption options = Game.options;
        options.Font++;
        if (options.Font >= fontsLength)
        {
            options.Font = 0;
        }

        Game.Font = fontValues[options.Font];
        TextRenderer.SetFont(Game.Font);

        // Release all cached text textures — they were rendered with the old font
        // and are now invalid. Previously set list entries to null (leaking GPU
        // handles). Now explicitly delete each texture before clearing the dictionary.
        foreach (CachedTexture ct in Game.CachedTextTextures.Values)
        {
            openGlService.GLDeleteTexture(ct.TextureId);
        }

        Game.CachedTextTextures.Clear();
    }

    private string KeyName(int key) => platform.KeyName(key);

    private void MakeSimpleOptions(int fontsize, int textheight)
    {
        int starty = Game.Ycenter(widgetsCount * textheight);
        for (int i = 0; i < widgetsCount; i++)
        {
            string s = widgets[i].Text;
            float sizeWidth = Game.TextSizeWidth(s, fontsize);
            float sizeHeight = Game.TextSizeHeight(s, fontsize);
            int Width = (int)sizeWidth + 10;
            int Height = (int)sizeHeight;
            int X = Game.Xcenter(sizeWidth);
            int Y = starty + (textheight * i);
            widgets[i].x = X;
            widgets[i].y = Y;
            widgets[i].width = Width;
            widgets[i].height = Height;
            widgets[i].fontsize = fontsize;
            if (i == keyselectid)
            {
                widgets[i].fontcolor = ColorUtils.ColorFromArgb(255, 0, 255, 0);
                widgets[i].fontcolorselected = ColorUtils.ColorFromArgb(255, 0, 255, 0);
            }
        }
    }
    private bool loaded;
    public override void OnRender2d(float deltaTime)
    {
        if (!loaded)
        {
            loaded = true;
            LoadOptions();
        }

        if (Game.EscapeMenuRestart)
        {
            Game.EscapeMenuRestart = false;
            SetEscapeMenuState(EscapeMenuState.Main);
        }

        if (Game.GuiState != GameState.EscapeMenu)
        {
            return;
        }

        SetEscapeMenuState(escapemenustate);
        EscapeMenuMouse1();
        for (int i = 0; i < widgetsCount; i++)
        {
            Button w = widgets[i];
            Game.Draw2dText1(w.Text, w.x, w.y, w.fontsize, w.selected ? w.fontcolorselected : w.fontcolor, false);
        }
    }
    private readonly Button[] widgets;
    private KeyHelp[] keyhelps()
    {
        int n = 1024;
        KeyHelp[] helps = new KeyHelp[n];
        for (int i = 0; i < n; i++)
        {
            helps[i] = null;
        }

        LanguageService language = Game.Language;
        int count = 0;
        helps[count++] = KeyHelpCreate(language.KeyMoveFoward(), Keys.W);
        helps[count++] = KeyHelpCreate(language.KeyMoveBack(), Keys.S);
        helps[count++] = KeyHelpCreate(language.KeyMoveLeft(), Keys.A);
        helps[count++] = KeyHelpCreate(language.KeyMoveRight(), Keys.D);
        helps[count++] = KeyHelpCreate(language.KeyJump(), Keys.Space);
        helps[count++] = KeyHelpCreate(language.KeyShowMaterialSelector(), Keys.B);
        helps[count++] = KeyHelpCreate(language.KeySetSpawnPosition(), Keys.P);
        helps[count++] = KeyHelpCreate(language.KeyRespawn(), Keys.O);
        helps[count++] = KeyHelpCreate(language.KeyReloadWeapon(), Keys.R);
        helps[count++] = KeyHelpCreate(language.KeyToggleFogDistance(), Keys.F);
        helps[count++] = KeyHelpCreate(string.Format(language.KeyMoveSpeed(), "1"), Keys.F1);
        helps[count++] = KeyHelpCreate(string.Format(language.KeyMoveSpeed(), "10"), Keys.F2);
        helps[count++] = KeyHelpCreate(language.KeyFreeMove(), Keys.F3);
        helps[count++] = KeyHelpCreate(language.KeyThirdPersonCamera(), Keys.F5);
        helps[count++] = KeyHelpCreate(language.KeyTextEditor(), Keys.F9);
        helps[count++] = KeyHelpCreate(language.KeyFullscreen(), Keys.F11);
        helps[count++] = KeyHelpCreate(language.KeyScreenshot(), Keys.F12);
        helps[count++] = KeyHelpCreate(language.KeyPlayersList(), Keys.Tab);
        helps[count++] = KeyHelpCreate(language.KeyChat(), Keys.T);
        helps[count++] = KeyHelpCreate(language.KeyTeamChat(), Keys.Y);
        helps[count++] = KeyHelpCreate(language.KeyCraft(), Keys.C);
        helps[count++] = KeyHelpCreate(language.KeyBlockInfo(), Keys.I);
        helps[count++] = KeyHelpCreate(language.KeyUse(), Keys.E);
        helps[count++] = KeyHelpCreate(language.KeyReverseMinecart(), Keys.Q);
        return helps;
    }

    private static KeyHelp KeyHelpCreate(string text, Keys defaultKey)
    {
        KeyHelp h = new()
        {
            Text = text,
            DefaultKey = (int)defaultKey
        };
        return h;
    }


    private int keyselectid;
    public override void OnKeyDown(KeyEventArgs args)
    {
        int eKey = args.KeyChar;
        if (eKey == Game.GetKey(Keys.Escape))
        {
            if (escapemenustate is EscapeMenuState.Graphics
                or EscapeMenuState.Keys
                or EscapeMenuState.Other)
            {
                SetEscapeMenuState(EscapeMenuState.Options);
            }
            else if (escapemenustate == EscapeMenuState.Options)
            {
                SaveOptions();
                SetEscapeMenuState(EscapeMenuState.Main);
            }
            else
            {
                SetEscapeMenuState(EscapeMenuState.Main);
                Game.GuiStateBackToGame();
            }

            args.Handled = true;
        }

        if (escapemenustate == EscapeMenuState.Keys)
        {
            if (keyselectid != -1)
            {
                Game.options.Keys[keyhelps()[keyselectid].DefaultKey] = eKey;
                keyselectid = -1;
                args.Handled = true;
            }
        }

        if (eKey == Game.GetKey(Keys.F11))
        {
            if (platform.GetWindowState() == WindowState.Fullscreen)
            {
                platform.SetWindowState(WindowState.Normal);
                RestoreResolution();
                SaveOptions();
            }
            else
            {
                platform.SetWindowState(WindowState.Fullscreen);
                UseResolution();
                SaveOptions();
            }

            args.Handled = true;
        }
    }
    public void LoadOptions()
    {
        GameOption o = LoadOptions_();
        if (o == null)
        {
            return;
        }

        Game.options = o;
        GameOption options = o;

        Game.Font = fontValues[options.Font];
        TextRenderer.SetFont(Game.Font);
        //Game.d_CurrentShadows.ShadowsFull = options.Shadows;
        Game.Config3d.ViewDistance = options.DrawDistance;
        Game.AudioEnabled = options.EnableSound;
        Game.AutoJumpEnabled = options.EnableAutoJump;
        if (options.ClientLanguage != "")
        {
            Game.Language.OverrideLanguage = options.ClientLanguage;
        }

        _terrainChunkTesselator.EnableSmoothLight = options.Smoothshadows;
        _terrainChunkTesselator.BlockShadow = options.BlockShadowSave;
        _terrainChunkTesselator.DarkenBlockSidesOption = options.EnableBlockShadow;
        Game.EnableLog = options.Framerate;
        UseFullscreen();
        Game.UseVsync();
        UseResolution();
    }

    private GameOption LoadOptions_()
    {
        GameOption options = new()
        {
            Shadows = preferences.GetBool("Shadows", true),
            Font = preferences.GetInt("Font", 0),
            DrawDistance = preferences.GetInt("DrawDistance", platform.IsFastSystem() ? 128 : 32),
            UseServerTextures = preferences.GetBool("UseServerTextures", true),
            EnableSound = preferences.GetBool("EnableSound", true),
            EnableAutoJump = preferences.GetBool("EnableAutoJump", false),
            ClientLanguage = preferences.GetString("ClientLanguage", ""),
            Framerate = preferences.GetInt("Framerate", 0),
            Resolution = preferences.GetInt("Resolution", 0),
            Fullscreen = preferences.GetBool("Fullscreen", false),
            Smoothshadows = preferences.GetBool("Smoothshadows", true),
            BlockShadowSave = 1f * preferences.GetInt("BlockShadowSave", 70) / 100,
            EnableBlockShadow = preferences.GetBool("EnableBlockShadow", true)
        };

        for (int i = 0; i < 256; i++)
        {
            string preferencesKey = string.Concat("Key", i.ToString());
            int value = preferences.GetInt(preferencesKey, 0);
            if (value != 0)
            {
                options.Keys[i] = value;
            }
        }

        return options;
    }

    public void SaveOptions()
    {
        GameOption options = Game.options;

        options.Font = Game.Font;
        options.Shadows = true; // Game.d_CurrentShadows.ShadowsFull;
        options.DrawDistance = (int)Game.Config3d.ViewDistance;
        options.EnableSound = Game.AudioEnabled;
        options.EnableAutoJump = Game.AutoJumpEnabled;
        if (Game.Language.OverrideLanguage != null)
        {
            options.ClientLanguage = Game.Language.OverrideLanguage;
        }

        options.Framerate = Game.EnableLog;
        options.Fullscreen = platform.GetWindowState() == WindowState.Fullscreen;
        options.Smoothshadows = _terrainChunkTesselator.EnableSmoothLight;
        options.EnableBlockShadow = _terrainChunkTesselator.DarkenBlockSidesOption;

        SaveOptions_(options);
    }

    private void SaveOptions_(GameOption options)
    {
        preferences.SetBool("Shadows", options.Shadows);
        preferences.SetInt("Font", options.Font);
        preferences.SetInt("DrawDistance", options.DrawDistance);
        preferences.SetBool("UseServerTextures", options.UseServerTextures);
        preferences.SetBool("EnableSound", options.EnableSound);
        preferences.SetBool("EnableAutoJump", options.EnableAutoJump);
        if (options.ClientLanguage != "")
        {
            preferences.SetString("ClientLanguage", options.ClientLanguage);
        }

        preferences.SetInt("Framerate", options.Framerate);
        preferences.SetInt("Resolution", options.Resolution);
        preferences.SetBool("Fullscreen", options.Fullscreen);
        preferences.SetBool("Smoothshadows", options.Smoothshadows);
        preferences.SetInt("BlockShadowSave", (int)(options.BlockShadowSave * 100));
        preferences.SetBool("EnableBlockShadow", options.EnableBlockShadow);

        for (int i = 0; i < 256; i++)
        {
            int value = options.Keys[i];
            string preferencesKey = string.Concat(platform, "Key", i.ToString());
            if (value != 0)
            {
                preferences.SetInt(preferencesKey, value);
            }
            else
            {
                preferences.Remove(preferencesKey);
            }
        }

        preferences.SetValues();
    }
}

public class Button
{
    public Button()
    {
        fontcolor = ColorUtils.ColorFromArgb(255, 255, 255, 255);
        fontcolorselected = ColorUtils.ColorFromArgb(255, 255, 0, 0);
        fontsize = 20;
    }
    internal int x;
    internal int y;
    internal int width;
    internal int height;
    internal string Text;
    internal bool selected;
    internal int fontsize;
    internal int fontcolor;
    internal int fontcolorselected;
}

public class KeyHelp
{
    internal string Text;
    internal int DefaultKey;
}

public class DisplayResolutionCi
{
    internal int Width;
    internal int Height;
    internal int BitsPerPixel;
    internal float RefreshRate;
    public int GetWidth() => Width;
    public void SetWidth(int value) => Width = value;
    public int GetHeight() => Height;
    public void SetHeight(int value) => Height = value;
    public int GetBitsPerPixel() => BitsPerPixel;
    public void SetBitsPerPixel(int value) => BitsPerPixel = value;
    public float GetRefreshRate() => RefreshRate;
    public void SetRefreshRate(float value) => RefreshRate = value;
}

public enum EscapeMenuState
{
    Main,
    Options,
    Graphics,
    Keys,
    Other
}