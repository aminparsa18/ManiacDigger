/// <summary>
/// Screen that shows available singleplayer save files and lets the player
/// launch, create, or modify a world.
/// </summary>
/// <remarks>
/// When the platform does not support singleplayer (i.e. web/mobile), all world
/// widgets are hidden and an explanatory message is displayed instead.
/// </remarks>
public class SingleplayerScreen : ScreenBase
{
    private readonly MenuWidget play;
    private readonly MenuWidget newWorld;
    private readonly MenuWidget modify;
    private readonly MenuWidget back;
    private readonly MenuWidget open;

    /// <summary>Dynamically populated buttons, one per discovered save file (up to 10).</summary>
    private readonly MenuWidget[] worldButtons;

    private string[] savegames;
    private int savegamesCount;
    private string title;

    public SingleplayerScreen()
    {
        play = new MenuWidget
        {
            text = "Play"
        };
        newWorld = new MenuWidget
        {
            text = "New World"
        };
        modify = new MenuWidget
        {
            text = "Modify"
        };
        back = new MenuWidget
        {
            text = "Back",
            type = WidgetType.Button
        };
        open = new MenuWidget
        {
            text = "Create or open...",
            type = WidgetType.Button
        };

        title = "Singleplayer";

        widgets.Add(play);
        widgets.Add(newWorld);
        widgets.Add(modify);
        widgets.Add(back);
        widgets.Add(open);

        worldButtons = new MenuWidget[10];
        for (int i = 0; i < 10; i++)
        {
            worldButtons[i] = new MenuWidget { visible = false };
            widgets.Add(worldButtons[i]);
        }
    }

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        back.text = menu.lang.Get("MainMenu_ButtonBack");
        open.text = menu.lang.Get("MainMenu_SingleplayerButtonCreate");
        title = menu.lang.Get("MainMenu_Singleplayer");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        IGamePlatform p = menu.p;
        float scale = menu.GetScale();

        menu.DrawBackground();
        menu.DrawText(title, 20 * scale, p.GetCanvasWidth() / 2, 10, TextAlign.Center, TextBaseline.Top);

        float leftx = p.GetCanvasWidth() / 2 - 128 * scale;
        float y = p.GetCanvasHeight() / 2 + 0 * scale;

        play.x = leftx;
        play.y = y + 100 * scale;
        play.sizex = 256 * scale;
        play.sizey = 64 * scale;
        play.fontSize = 14 * scale;

        newWorld.x = leftx;
        newWorld.y = y + 170 * scale;
        newWorld.sizex = 256 * scale;
        newWorld.sizey = 64 * scale;
        newWorld.fontSize = 14 * scale;

        modify.x = leftx;
        modify.y = y + 240 * scale;
        modify.sizex = 256 * scale;
        modify.sizey = 64 * scale;
        modify.fontSize = 14 * scale;

        back.x = 40 * scale;
        back.y = p.GetCanvasHeight() - 104 * scale;
        back.sizex = 256 * scale;
        back.sizey = 64 * scale;
        back.fontSize = 14 * scale;

        open.x = leftx;
        open.y = y + 0 * scale;
        open.sizex = 256 * scale;
        open.sizey = 64 * scale;
        open.fontSize = 14 * scale;

        if (savegames == null)
        {
            savegames = menu.GetSavegames(out int savegamesCount_);
            savegamesCount = savegamesCount_;
        }

        for (int i = 0; i < 10; i++)
        {
            worldButtons[i].visible = false;
        }
        for (int i = 0; i < savegamesCount; i++)
        {
            worldButtons[i].visible = true;
            worldButtons[i].text = menu.p.FileName(savegames[i]);
            worldButtons[i].x = leftx;
            worldButtons[i].y = 100 + 100 * scale * i;
            worldButtons[i].sizex = 256 * scale;
            worldButtons[i].sizey = 64 * scale;
            worldButtons[i].fontSize = 14 * scale;
        }

        // Only the Open button is active on supporting platforms.
        // Play, NewWorld, Modify, and worldButtons are reserved for a future
        // save-file browser and are hidden until that UI is implemented.
        open.visible = menu.p.SinglePlayerServerAvailable();
        play.visible = false;
        newWorld.visible = false;
        modify.visible = false;
        for (int i = 0; i < savegamesCount; i++)
        {
            worldButtons[i].visible = false;
        }

        DrawWidgets();

        if (!menu.p.SinglePlayerServerAvailable())
        {
            menu.DrawText(
                "Singleplayer is only available on desktop (Windows, Linux, Mac) version of game.",
                16 * scale, menu.p.GetCanvasWidth() / 2, menu.p.GetCanvasHeight() / 2,
                TextAlign.Center, TextBaseline.Middle);
        }
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => menu.StartMainMenu();

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        for (int i = 0; i < 10; i++)
        {
            worldButtons[i].selected = false;
        }
        for (int i = 0; i < 10; i++)
        {
            if (worldButtons[i] == w)
            {
                worldButtons[i].selected = true;
            }
        }

        if (w == newWorld)
        {
            MainMenu.StartNewWorld();
        }

        if (w == play)
        {
            // Reserved — will launch the selected world once the save-file browser is implemented.
        }

        if (w == modify)
        {
            MainMenu.StartModifyWorld();
        }

        if (w == back)
        {
            OnBackPressed();
        }

        if (w == open)
        {
            string extension;
            if (menu.p.SinglePlayerServerAvailable())
            {
                extension = "mddbs";
            }
            else
            {
                extension = "mdss";
            }
            string result = menu.p.FileOpenDialog(extension, "Manic Digger Savegame", menu.p.PathSavegames());
            if (result != null)
            {
                menu.ConnectToSingleplayer(result);
            }
        }
    }
}