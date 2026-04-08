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
        Game.ColorFromArgb(255, 255, 0,   0),  // red
        Game.ColorFromArgb(255, 255, 255, 0),  // yellow
        Game.ColorFromArgb(255, 0,   255, 0),  // green
    ];

    public override void OnNewFrameDraw2d(Game game, float deltaTime)
    {
        if (game.guistate != GuiState.MapLoading) return;

        GamePlatform platform = game.platform;
        int width = platform.GetCanvasWidth();
        int height = platform.GetCanvasHeight();
        int centerY = height / 2;

        DrawBackground(game, width, height);

        if (game.invalidVersionDrawMessage != null)
        {
            DrawCentered(game, game.invalidVersionDrawMessage, centerY - 50);
            DrawCentered(game, "Click to connect", centerY + 50);
            return;
        }

        string status = GetConnectionStatus(game, platform);

        DrawCentered(game, game.ServerInfo.ServerName, centerY - 150);

        if (game.ServerInfo.ServerMotd != null)
            DrawCentered(game, game.ServerInfo.ServerMotd, centerY - 100);

        DrawCentered(game, status, centerY - 50);

        if (game.maploadingprogress.ProgressPercent > 0)
            DrawProgress(game, platform, width, height, centerY);
    }

    private static string GetConnectionStatus(Game game, GamePlatform platform)
    {
        if (game.maploadingprogress.ProgressStatus != null)
            return game.maploadingprogress.ProgressStatus;
        if (game.issingleplayer && !platform.SinglePlayerServerLoaded())
            return "Starting game...";
        return game.language.Connecting();
    }

    private void DrawProgress(Game game, GamePlatform platform, int width, int height, int centerY)
    {
        string progress = string.Format(game.language.ConnectingProgressPercent(), game.maploadingprogress.ProgressPercent.ToString());
        string progress1 = string.Format(game.language.ConnectingProgressKilobytes(), (game.maploadingprogress.ProgressBytes / 1024).ToString());

        DrawCentered(game, progress, centerY - 20);
        DrawCentered(game, progress1, centerY + 10);

        float ratio = game.maploadingprogress.ProgressPercent / 100f;
        int barX = game.Xcenter(ProgressBarWidth);
        int barY = centerY + 70;
        int color = InterpolationCi.InterpolateColor(platform, ratio, ProgressBarColors, ProgressBarColors.Length);

        game.Draw2dTexture(game.WhiteTexture(), barX, barY, ProgressBarWidth, ProgressBarHeight, null, 0, Game.ColorFromArgb(255, 0, 0, 0), false);
        game.Draw2dTexture(game.WhiteTexture(), barX, barY, ratio * ProgressBarWidth, ProgressBarHeight, null, 0, color, false);
    }

    private void DrawCentered(Game game, string text, int y)
    {
        game.platform.TextSize(text, FontSize, out int textWidth, out _);
        game.Draw2dText(text, game.fontMapLoading, game.Xcenter(textWidth), y, null, false);
    }

    private static void DrawBackground(Game game, int width, int height)
    {
        int countX = width / BackgroundTileSize + 1;
        int countY = height / BackgroundTileSize + 1;
        int tex = game.GetTexture("background.png");
        int white = Game.ColorFromArgb(255, 255, 255, 255);

        for (int x = 0; x < countX; x++)
            for (int y = 0; y < countY; y++)
                game.Draw2dTexture(tex, x * BackgroundTileSize, y * BackgroundTileSize, BackgroundTileSize, BackgroundTileSize, null, 0, white, false);
    }
}