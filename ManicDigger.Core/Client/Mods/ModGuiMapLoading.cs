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

    private readonly IGameService platform;
    private readonly ISinglePlayerService singlePlayerService;

    public ModGuiMapLoading(IGameService platform, ISinglePlayerService singlePlayerService, IGame game) : base(game)
    {
        this.platform = platform;
        this.singlePlayerService = singlePlayerService;
    }

    public override void OnRender2d(float deltaTime)
    {
        if (Game.GuiState != GameState.MapLoading)
        {
            return;
        }

        int width = platform.CanvasWidth;
        int height = platform.CanvasHeight;
        int centerY = height / 2;

        DrawBackground(width, height);

        if (Game.InvalidVersionDrawMessage != null)
        {
            DrawCentered(Game.InvalidVersionDrawMessage, centerY - 50);
            DrawCentered("Click to connect", centerY + 50);
            return;
        }

        string status = GetConnectionStatus();

        DrawCentered(Game.ServerInfo.ServerName, centerY - 150);

        if (Game.ServerInfo.ServerMotd != null)
        {
            DrawCentered(Game.ServerInfo.ServerMotd, centerY - 100);
        }

        DrawCentered(status, centerY - 50);

        if (Game.maploadingprogress.ProgressPercent > 0)
        {
            DrawProgress(centerY);
        }
    }

    private string GetConnectionStatus()
    {
        if (Game.maploadingprogress.ProgressStatus != null)
        {
            return Game.maploadingprogress.ProgressStatus;
        }

        if (Game.IsSinglePlayer && !singlePlayerService.SinglePlayerServerLoaded)
        {
            return "Starting game...";
        }

        return Game.Language.Connecting();
    }

    private void DrawProgress(int centerY)
    {
        string progress = string.Format(Game.Language.ConnectingProgressPercent(), Game.maploadingprogress.ProgressPercent.ToString());
        string progress1 = string.Format(Game.Language.ConnectingProgressKilobytes(), (Game.maploadingprogress.ProgressBytes / 1024).ToString());

        DrawCentered(progress, centerY - 20);
        DrawCentered(progress1, centerY + 10);
        float ratio = Game.maploadingprogress.ProgressPercent / 100f;
        int barX = Game.Xcenter(ProgressBarWidth);
        int barY = centerY + 70;
        int color = ColorUtils.InterpolateColor(ratio, ProgressBarColors, ProgressBarColors.Length);

        Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(), barX, barY, ProgressBarWidth, ProgressBarHeight, null, 0, ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
        Game.Draw2dTexture(Game.GetOrCreateWhiteTexture(), barX, barY, ratio * ProgressBarWidth, ProgressBarHeight, null, 0, color, false);
    }

    private void DrawCentered(string text, int y)
    {
        TextRenderer.TextSize(text, FontSize, out int textWidth, out _);
        Game.Draw2dText(text, Game.FontMapLoading, Game.Xcenter(textWidth), y, null, false);
    }

    private void DrawBackground(int width, int height)
    {
        int countX = (width / BackgroundTileSize) + 1;
        int countY = (height / BackgroundTileSize) + 1;
        int tex = Game.GetTexture("background.png");
        int white = ColorUtils.ColorFromArgb(255, 255, 255, 255);

        for (int x = 0; x < countX; x++)
        {
            for (int y = 0; y < countY; y++)
            {
                Game.Draw2dTexture(tex, x * BackgroundTileSize, y * BackgroundTileSize, BackgroundTileSize, BackgroundTileSize, null, 0, white, false);
            }
        }
    }
}