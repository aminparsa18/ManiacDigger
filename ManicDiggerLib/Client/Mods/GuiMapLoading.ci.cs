/// <summary>
/// Renders the map loading screen including background, server info, connection status, and progress bar.
/// </summary>
public class ModGuiMapLoading : ModBase
{
    private const int BackgroundTileSize = 512;
    private const int ProgressBarWidth = 400;
    private const int ProgressBarHeight = 40;
    private const int FontSize = 14;

    private static readonly int[] ProgressBarColors =
    [
        ColorUtils.ColorFromArgb(255, 255, 0,   0),  // red
        ColorUtils.ColorFromArgb(255, 255, 255, 0),  // yellow
        ColorUtils.ColorFromArgb(255, 0,   255, 0),  // green
    ];

    private readonly IGameClient game;
    private readonly IGamePlatform platform;

    public ModGuiMapLoading(IGameClient game, IGamePlatform platform)
    {
        this.game = game;
        this.platform = platform;
    }

    public override void OnNewFrameDraw2d(float deltaTime)
    {
        if (game.GuiState != GuiState.MapLoading) return;

        int width = platform.GetCanvasWidth();
        int height = platform.GetCanvasHeight();
        int centerY = height / 2;

        DrawBackground(width, height);

        if (game.InvalidVersionDrawMessage != null)
        {
            DrawCentered(game.InvalidVersionDrawMessage, centerY - 50);
            DrawCentered("Click to connect", centerY + 50);
            return;
        }

        string status = GetConnectionStatus(platform);

        DrawCentered(game.ServerInfo.ServerName, centerY - 150);

        if (game.ServerInfo.ServerMotd != null)
            DrawCentered(game.ServerInfo.ServerMotd, centerY - 100);

        DrawCentered(status, centerY - 50);

        if (game.maploadingprogress.ProgressPercent > 0)
            DrawProgress(centerY);
    }

    private string GetConnectionStatus(IGamePlatform platform)
    {
        if (game.maploadingprogress.ProgressStatus != null)
            return game.maploadingprogress.ProgressStatus;
        if (game.IsSinglePlayer && !platform.SinglePlayerServerLoaded())
            return "Starting game...";
        return game.Language.Connecting();
    }

    private void DrawProgress(int centerY)
    {
        string progress = string.Format(game.Language.ConnectingProgressPercent(), game.maploadingprogress.ProgressPercent.ToString());
        string progress1 = string.Format(game.Language.ConnectingProgressKilobytes(), (game.maploadingprogress.ProgressBytes / 1024).ToString());

        DrawCentered(progress, centerY - 20);
        DrawCentered(progress1, centerY + 10);

        float ratio = game.maploadingprogress.ProgressPercent / 100f;
        int barX = game.Xcenter(ProgressBarWidth);
        int barY = centerY + 70;
        int color = ColorUtils.InterpolateColor(ratio, ProgressBarColors, ProgressBarColors.Length);

        game.Draw2dTexture(game.GetOrCreateWhiteTexture(), barX, barY, ProgressBarWidth, ProgressBarHeight, null, 0, ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
        game.Draw2dTexture(game.GetOrCreateWhiteTexture(), barX, barY, ratio * ProgressBarWidth, ProgressBarHeight, null, 0, color, false);
    }

    private void DrawCentered(string text, int y)
    {
        TextRenderer.TextSize(text, FontSize, out int textWidth, out _);
        game.Draw2dText(text, game.FontMapLoading, game.Xcenter(textWidth), y, null, false);
    }

    private void DrawBackground(int width, int height)
    {
        int countX = width / BackgroundTileSize + 1;
        int countY = height / BackgroundTileSize + 1;
        int tex = game.GetTexture("background.png");
        int white = ColorUtils.ColorFromArgb(255, 255, 255, 255);

        for (int x = 0; x < countX; x++)
            for (int y = 0; y < countY; y++)
                game.Draw2dTexture(tex, x * BackgroundTileSize, y * BackgroundTileSize, BackgroundTileSize, BackgroundTileSize, null, 0, white, false);
    }
}