/// <summary>
/// Screen providing world-modification tools. Currently a stub — only the
/// Back button is implemented.
/// </summary>
public class ModifyWorldScreen : ScreenBase
{
    private readonly MenuWidget buttonBack;

    private string title = "Modify World";

    public ModifyWorldScreen()
    {
        buttonBack = new MenuWidget
        {
            text = "Back",
            type = WidgetType.Button
        };

        widgets.Add(buttonBack);
    }

    /// <inheritdoc/>
    public override void LoadTranslations()
    {
        buttonBack.text = menu.lang.Get("MainMenu_ButtonBack");
        title = menu.lang.Get("MainMenu_ModifyWorld");
    }

    /// <inheritdoc/>
    public override void Render(float dt)
    {
        IGamePlatform p = menu.p;
        float scale = menu.GetScale();

        menu.DrawBackground();
        menu.DrawText(title, 14 * scale, p.GetCanvasWidth() / 2, 0, TextAlign.Center, TextBaseline.Top);

        buttonBack.x = 40 * scale;
        buttonBack.y = p.GetCanvasHeight() - 104 * scale;
        buttonBack.sizex = 256 * scale;
        buttonBack.sizey = 64 * scale;
        buttonBack.fontSize = 14 * scale;

        DrawWidgets();
    }

    /// <inheritdoc/>
    public override void OnBackPressed() => menu.StartSingleplayer();

    /// <inheritdoc/>
    public override void OnButton(MenuWidget w)
    {
        if (w == buttonBack) { OnBackPressed(); }
    }
}