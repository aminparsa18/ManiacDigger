using OpenTK.Windowing.Common;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public class ModGuiEscapeMenu : ModBase
{
    public ModGuiEscapeMenu()
    {
        one = 1;
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
    private readonly float one;
    private Button buttonMainReturnToGame;
    private Button buttonMainOptions;
    private Button buttonMainExit;

    private int widgetsCount;
    private void MainSet()
    {
        Language language = game.language;
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
            game.GuiStateBackToGame();
        }
        if (b == buttonMainOptions)
        {
            SetEscapeMenuState(EscapeMenuState.Options);
        }
        if (b == buttonMainExit)
        {
            game.SendLeave(Packet_LeaveReasonEnum.Leave);
            game.ExitToMainMenu_();
        }
    }

    private Button optionsGraphics;
    private Button optionsKeys;
    private Button optionsOther;
    private Button optionsReturnToMainMenu;
    private void OptionsSet()
    {
        Language language = game.language;
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
            SaveOptions(); SetEscapeMenuState(EscapeMenuState.Main);
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
    private void GraphicsSet()
    {
        OptionsCi options = game.options;
        Language language = game.language;
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
            Text = string.Format(language.ViewDistanceOption(), ((int)(game.d_Config3d.viewdistance)).ToString())
        };
        graphicsOptionFramerate = new Button
        {
            Text = string.Format(language.OptionFramerate(), (VsyncString()))
        };
        graphicsOptionResolution = new Button
        {
            Text = string.Format(language.OptionResolution(), (ResolutionString()))
        };
        graphicsOptionFullscreen = new Button
        {
            Text = string.Format(language.OptionFullscreen(), options.Fullscreen ? language.On() : language.Off())
        };
        graphicsUseServerTexturesOption = new Button
        {
            Text = string.Format(language.UseServerTexturesOption(), (options.UseServerTextures ? language.On() : language.Off()))
        };
        graphicsFontOption = new Button
        {
            Text = string.Format(language.FontOption(), (FontString()))
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
        OptionsCi options = game.options;
        if (b == graphicsOptionSmoothShadows)
        {
            options.Smoothshadows = !options.Smoothshadows;
            game.d_TerrainChunkTesselator.EnableSmoothLight = options.Smoothshadows;
            if (options.Smoothshadows)
            {
                options.BlockShadowSave = one * 7 / 10;
                game.d_TerrainChunkTesselator.BlockShadow = options.BlockShadowSave;
            }
            else
            {
                options.BlockShadowSave = one * 6 / 10;
                game.d_TerrainChunkTesselator.BlockShadow = options.BlockShadowSave;
            }
            game.RedrawAllBlocks();
        }
        if (b == graphicsOptionDarkenSides)
        {
            options.EnableBlockShadow = !options.EnableBlockShadow;
            game.d_TerrainChunkTesselator.option_DarkenBlockSides = options.EnableBlockShadow;
            game.RedrawAllBlocks();
        }
        if (b == graphicsViewDistanceOption)
        {
            game.ToggleFog();
        }
        if (b == graphicsOptionFramerate)
        {
            game.ToggleVsync();
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
        Language language = game.language;

        otherSoundOption = new Button
        {
            Text = string.Format(language.SoundOption(), (game.AudioEnabled ? language.On() : language.Off()))
        };
        otherAutoJumpOption = new Button
        {
            Text = string.Format(language.AutoJumpOption(), (game.AutoJumpEnabled ? language.On() : language.Off()))
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
            game.AudioEnabled = !game.AudioEnabled;
        }
        if (b == otherAutoJumpOption)
        {
            game.AutoJumpEnabled = !game.AutoJumpEnabled;
        }
        if (b == otherLanguageSetting)
        {
            //Switch language based on available languages
            game.language.NextLanguage();
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
    private void KeysSet()
    {
        Language language = game.language;

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
            game.options.Keys = new int[256];
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

    private void AddWidget(Button b)
    {
        widgets[widgetsCount++] = b;
    }

    private void WidgetsClear()
    {
        widgetsCount = 0;
    }

    internal Game game;
    private EscapeMenuState escapemenustate;
    private void EscapeMenuMouse1()
    {
        for (int i = 0; i < widgetsCount; i++)
        {
            Button w = widgets[i];
            w.selected = RectContains(w.x, w.y, w.width, w.height, game.mouseCurrentX, game.mouseCurrentY);
            if (w.selected && game.mouseleftclick)
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
        Language language = game.language;
        escapemenustate = state;
        WidgetsClear();
        if (state == EscapeMenuState.Main)
        {
            MainSet();
            MakeSimpleOptions(20, 50);
        }
        else if (state == EscapeMenuState.Options)
        {
            OptionsSet();
            MakeSimpleOptions(20, 50);
        }
        else if (state == EscapeMenuState.Graphics)
        {
            GraphicsSet();
            MakeSimpleOptions(20, 50);
        }
        else if (state == EscapeMenuState.Other)
        {
            OtherSet();
            MakeSimpleOptions(20, 50);
        }
        else if (state == EscapeMenuState.Keys)
        {
            KeysSet();
            int fontsize = 12;
            int textheight = 20;
            MakeSimpleOptions(fontsize, textheight);
        }
    }

    private void UseFullscreen()
    {
        if (game.options.Fullscreen)
        {
            if (!changedResolution)
            {
                originalResolutionWidth = game.platform.GetDisplayResolutionDefault().Width;
                originalResolutionHeight = game.platform.GetDisplayResolutionDefault().Height;
                changedResolution = true;
            }
            game.platform.SetWindowState(WindowState.Fullscreen);
            UseResolution();
        }
        else
        {
            game.platform.SetWindowState(WindowState.Normal);
            RestoreResolution();
        }
    }

    private string VsyncString()
    {
        if (game.ENABLE_LAG == 0) { return "Vsync"; }
        else if (game.ENABLE_LAG == 1) { return "Unlimited"; }
        else if (game.ENABLE_LAG == 2) { return "Lag"; }
        else return null; //throw new Exception();
    }

    private string ResolutionString()
    {
        DisplayResolutionCi res = game.platform.GetDisplayResolutions(out int resolutionsCount)[game.options.Resolution];
        return string.Format("{0}x{1}, {2}, {3} Hz",
            res.Width.ToString(),
            res.Height.ToString(),
            res.BitsPerPixel.ToString(),
            ((int)(res.RefreshRate)).ToString());
    }

    private void ToggleResolution()
    {
        OptionsCi options = game.options;
        options.Resolution++;

        game.platform.GetDisplayResolutions(out int resolutionsCount);

        if (options.Resolution >= resolutionsCount)
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
            game.platform.ChangeResolution(originalResolutionWidth, originalResolutionHeight, 32, -1);
            changedResolution = false;
        }
    }
    public void UseResolution()
    {
        OptionsCi options = game.options;
        DisplayResolutionCi[] resolutions = game.platform.GetDisplayResolutions(out int resolutionsCount);

        if (resolutions == null)
        {
            return;
        }

        if (options.Resolution >= resolutionsCount)
        {
            options.Resolution = 0;
        }
        DisplayResolutionCi res = resolutions[options.Resolution];
        if (game.platform.GetWindowState() == WindowState.Fullscreen)
        {
            game.platform.ChangeResolution(res.Width, res.Height, res.BitsPerPixel, res.RefreshRate);
            game.platform.SetWindowState(WindowState.Normal);
            game.platform.SetWindowState(WindowState.Fullscreen);
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

    private string FontString()
    {
        return fonts[game.options.Font];
    }
    private void ToggleFont()
    {
        OptionsCi options = game.options;
        options.Font++;
        if (options.Font >= fontsLength)
        {
            options.Font = 0;
        }
        game.Font = fontValues[options.Font];
        game.UpdateTextRendererFont();
        for (int i = 0; i < game.cachedTextTexturesMax; i++)
        {
            game.cachedTextTextures[i] = null;
        }
    }

    private string KeyName(int key)
    {
        return game.platform.KeyName(key);
    }

    private void MakeSimpleOptions(int fontsize, int textheight)
    {
        int starty = game.Ycenter(widgetsCount * textheight);
        for (int i = 0; i < widgetsCount; i++)
        {
            string s = widgets[i].Text;
            float sizeWidth = game.TextSizeWidth(s, fontsize);
            float sizeHeight = game.TextSizeHeight(s, fontsize);
            int Width = (int)(sizeWidth) + 10;
            int Height = (int)(sizeHeight);
            int X = game.Xcenter(sizeWidth);
            int Y = starty + textheight * i;
            widgets[i].x = X;
            widgets[i].y = Y;
            widgets[i].width = Width;
            widgets[i].height = Height;
            widgets[i].fontsize = fontsize;
            if (i == keyselectid)
            {
                widgets[i].fontcolor = Game.ColorFromArgb(255, 0, 255, 0);
                widgets[i].fontcolorselected = Game.ColorFromArgb(255, 0, 255, 0);
            }
        }
    }
    private bool loaded;
    public override void OnNewFrameDraw2d(Game game_, float deltaTime)
    {
        game = game_;
        if (!loaded)
        {
            loaded = true;
            LoadOptions();
        }
        if (game.escapeMenuRestart)
        {
            game.escapeMenuRestart = false;
            SetEscapeMenuState(EscapeMenuState.Main);
        }
        if (game.guistate != GuiState.EscapeMenu)
        {
            return;
        }
        SetEscapeMenuState(escapemenustate);
        EscapeMenuMouse1();
        for (int i = 0; i < widgetsCount; i++)
        {
            Button w = widgets[i];
            game.Draw2dText1(w.Text, w.x, w.y, w.fontsize, w.selected ? w.fontcolorselected : w.fontcolor, false);
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
        Language language = game.language;
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
    public override void OnKeyDown(Game game_, KeyEventArgs args)
    {
        int eKey = args.GetKeyCode();
        if (eKey == game.GetKey(Keys.Escape))
        {
            if (escapemenustate == EscapeMenuState.Graphics
                || escapemenustate == EscapeMenuState.Keys
                || escapemenustate == EscapeMenuState.Other)
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
                game.GuiStateBackToGame();
            }
            args.SetHandled(true);
        }
        if (escapemenustate == EscapeMenuState.Keys)
        {
            if (keyselectid != -1)
            {
                game.options.Keys[keyhelps()[keyselectid].DefaultKey] = eKey;
                keyselectid = -1;
                args.SetHandled(true);
            }
        }
        if (eKey == game.GetKey(Keys.F11))
        {
            if (game.platform.GetWindowState() == WindowState.Fullscreen)
            {
                game.platform.SetWindowState(WindowState.Normal);
                RestoreResolution();
                SaveOptions();
            }
            else
            {
                game.platform.SetWindowState(WindowState.Fullscreen);
                UseResolution();
                SaveOptions();
            }
            args.SetHandled(true);
        }
    }
    public void LoadOptions()
    {
        OptionsCi o = LoadOptions_();
        if (o == null)
        {
            return;
        }
        game.options = o;
        OptionsCi options = o;

        game.Font = fontValues[options.Font];
        game.UpdateTextRendererFont();
        //game.d_CurrentShadows.ShadowsFull = options.Shadows;
        game.d_Config3d.viewdistance = options.DrawDistance;
        game.AudioEnabled = options.EnableSound;
        game.AutoJumpEnabled = options.EnableAutoJump;
        if (options.ClientLanguage != "")
        {
            game.language.OverrideLanguage = options.ClientLanguage;
        }
        game.d_TerrainChunkTesselator.EnableSmoothLight = options.Smoothshadows;
        game.d_TerrainChunkTesselator.BlockShadow = options.BlockShadowSave;
        game.d_TerrainChunkTesselator.option_DarkenBlockSides = options.EnableBlockShadow;
        game.ENABLE_LAG = options.Framerate;
        UseFullscreen();
        game.UseVsync();
        UseResolution();
    }

    private OptionsCi LoadOptions_()
    {
        OptionsCi options = new();
        Preferences preferences = game.platform.GetPreferences();
                
        options.Shadows = preferences.GetBool("Shadows", true);
        options.Font = preferences.GetInt("Font", 0);
        options.DrawDistance = preferences.GetInt("DrawDistance", game.platform.IsFastSystem() ? 128 : 32);
        options.UseServerTextures = preferences.GetBool("UseServerTextures", true);
        options.EnableSound = preferences.GetBool("EnableSound", true);
        options.EnableAutoJump = preferences.GetBool("EnableAutoJump", false);
        options.ClientLanguage = preferences.GetString("ClientLanguage", "");
        options.Framerate = preferences.GetInt("Framerate", 0);
        options.Resolution = preferences.GetInt("Resolution", 0);
        options.Fullscreen = preferences.GetBool("Fullscreen", false);
        options.Smoothshadows = preferences.GetBool("Smoothshadows", true);
        options.BlockShadowSave = one * preferences.GetInt("BlockShadowSave", 70) / 100;
        options.EnableBlockShadow = preferences.GetBool("EnableBlockShadow", true);

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
        OptionsCi options = game.options;

        options.Font = game.Font;
        options.Shadows = true; // game.d_CurrentShadows.ShadowsFull;
        options.DrawDistance = (int)(game.d_Config3d.viewdistance);
        options.EnableSound = game.AudioEnabled;
        options.EnableAutoJump = game.AutoJumpEnabled;
        if (game.language.OverrideLanguage != null)
        {
            options.ClientLanguage = game.language.OverrideLanguage;
        }
        options.Framerate = game.ENABLE_LAG;
        options.Fullscreen = game.platform.GetWindowState() == WindowState.Fullscreen;
        options.Smoothshadows = game.d_TerrainChunkTesselator.EnableSmoothLight;
        options.EnableBlockShadow = game.d_TerrainChunkTesselator.option_DarkenBlockSides;

        SaveOptions_(options);
    }

    private void SaveOptions_(OptionsCi options)
    {
        Preferences preferences = game.platform.GetPreferences();

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
            int value = options.Keys[i];string preferencesKey = string.Concat(game.platform, "Key", i.ToString());
            if (value != 0)
            {
                preferences.SetInt(preferencesKey, value);
            }
            else
            {
                preferences.Remove(preferencesKey);
            }
        }

        game.platform.SetPreferences(preferences);
    }
}

public class Button
{
    public Button()
    {
        fontcolor = Game.ColorFromArgb(255, 255, 255, 255);
        fontcolorselected = Game.ColorFromArgb(255, 255, 0, 0);
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
    public int GetWidth() { return Width; } public void SetWidth(int value) { Width = value; }
    public int GetHeight() { return Height; } public void SetHeight(int value) { Height = value; }
    public int GetBitsPerPixel() { return BitsPerPixel; } public void SetBitsPerPixel(int value) { BitsPerPixel = value; }
    public float GetRefreshRate() { return RefreshRate; } public void SetRefreshRate(float value) { RefreshRate = value; }
}
