/// <summary>
/// Renders the player's health and oxygen bars on the HUD.
/// </summary>
public class ModGuiPlayerStats : ModBase
{
    private const int BarWidth = 220;
    private const int BarHeight = 32;
    private const int CenterOffset = 20;

    private static readonly int White = ColorUtils.ColorFromArgb(255, 255, 255, 255);
    private static readonly int Red = ColorUtils.ColorFromArgb(255, 255, 0, 0);
    private static readonly int Blue = ColorUtils.ColorFromArgb(255, 0, 0, 255);

    private readonly IGameClient game;
    private readonly IGamePlatform platform;

    public ModGuiPlayerStats(IGameClient game, IGamePlatform platform)
    {
        this.game = game;
        this.platform = platform;
    }

    public override void OnNewFrameDraw2d(float deltaTime)
    {
        if (game.GuiState == GuiState.MapLoading || game.PlayerStats == null) return;

        int barY = platform.GetCanvasHeight() - 122;
        int healthX = platform.GetCanvasWidth() / 2 - BarWidth - CenterOffset;
        int oxygenX = platform.GetCanvasWidth() / 2 + CenterOffset;

        DrawBar(healthX, barY, (float)game.PlayerStats.CurrentHealth / game.PlayerStats.MaxHealth, Red);

        if (game.PlayerStats.CurrentOxygen < game.PlayerStats.MaxOxygen)
            DrawBar(oxygenX, barY, (float)game.PlayerStats.CurrentOxygen / game.PlayerStats.MaxOxygen, Blue);
    }

    /// <summary>Draws a background + filled progress bar at the given position.</summary>
    private void DrawBar(int x, int y, float progress, int color)
    {
        int bgTex = game.GetTexture("ui_bar_background.png");
        int barTex = game.GetTexture("ui_bar_inner.png");

        game.Draw2dTexture(bgTex, x, y, BarWidth, BarHeight, null, 0, White, false);
        game.Draw2dTexturePart(barTex, progress, 1, x, y, progress * BarWidth, BarHeight, color, false);
    }
}