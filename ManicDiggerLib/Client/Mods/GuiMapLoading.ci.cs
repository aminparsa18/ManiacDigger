public class ModGuiMapLoading : ClientMod
{
    private int Width;
    private int Height;
    private int backgroundW;
    private int backgroundH;

    public override void OnNewFrameDraw2d(Game game, float deltaTime)
    {
        if (game.guistate != GuiState.MapLoading)
        {
            return;
        }

        GamePlatform platform = game.platform;
        float one = 1;

        Width = platform.GetCanvasWidth();
        Height = platform.GetCanvasHeight();
        DrawBackground(game);

        string connecting = game.language.Connecting();
        if (game.issingleplayer && (!platform.SinglePlayerServerLoaded()))
        {
            connecting = "Starting game...";
        }
        if (game.maploadingprogress.ProgressStatus != null)
        {
            connecting = game.maploadingprogress.ProgressStatus;
        }

        if (game.invalidVersionDrawMessage != null)
        {
            game.Draw2dText(game.invalidVersionDrawMessage, game.fontMapLoading, game.xcenter(game.TextSizeWidth(game.invalidVersionDrawMessage, 14)), Height / 2 - 50, null, false);
            string connect = "Click to connect";
            game.Draw2dText(connect, game.fontMapLoading, game.xcenter(game.TextSizeWidth(connect, 14)), Height / 2 + 50, null, false);
            return;
        }

        platform.TextSize(game.ServerInfo.ServerName, 14, out int serverNameWidth, out int serverNameHeight);
        game.Draw2dText(game.ServerInfo.ServerName, game.fontMapLoading, game.xcenter(serverNameWidth), Height / 2 - 150, null, false);

        if (game.ServerInfo.ServerMotd != null)
        {
            platform.TextSize(game.ServerInfo.ServerMotd, 14, out int serverMotdWidth, out int serverMotdHeight);
            game.Draw2dText(game.ServerInfo.ServerMotd, game.fontMapLoading, game.xcenter(serverMotdWidth), Height / 2 - 100, null, false);
        }

        platform.TextSize(connecting, 14, out int connectingWidth, out int connectingHeight);
        game.Draw2dText(connecting, game.fontMapLoading, game.xcenter(connectingWidth), Height / 2 - 50, null, false);

        string progress = string.Format(game.language.ConnectingProgressPercent(), game.maploadingprogress.ProgressPercent.ToString());
        string progress1 = string.Format(game.language.ConnectingProgressKilobytes(), (game.maploadingprogress.ProgressBytes / 1024).ToString());

        if (game.maploadingprogress.ProgressPercent > 0)
        {
            platform.TextSize(progress, 14, out int progressWidth, out int progressHeight);
            game.Draw2dText(progress, game.fontMapLoading, game.xcenter(progressWidth), Height / 2 - 20, null, false);

            platform.TextSize(progress1, 14, out int progress1Width, out int progress1Height);
            game.Draw2dText(progress1, game.fontMapLoading, game.xcenter(progress1Width), Height / 2 + 10, null, false);

            float progressratio = one * game.maploadingprogress.ProgressPercent / 100;
            int sizex = 400;
            int sizey = 40;
            game.Draw2dTexture(game.WhiteTexture(), game.xcenter(sizex), Height / 2 + 70, sizex, sizey, null, 0, Game.ColorFromArgb(255, 0, 0, 0), false);
            int red = Game.ColorFromArgb(255, 255, 0, 0);
            int yellow = Game.ColorFromArgb(255, 255, 255, 0);
            int green = Game.ColorFromArgb(255, 0, 255, 0);
            int[] colors = new int[3];
            colors[0] = red;
            colors[1] = yellow;
            colors[2] = green;
            int c = InterpolationCi.InterpolateColor(platform, progressratio, colors, 3);
            game.Draw2dTexture(game.WhiteTexture(), game.xcenter(sizex), Height / 2 + 70, progressratio * sizex, sizey, null, 0, c, false);
        }
    }

    private void DrawBackground(Game game)
    {
        backgroundW = 512;
        backgroundH = 512;
        //Background tiling
        int countX = (int)(Width / backgroundW) + 1;
        int countY = (int)(Height / backgroundH) + 1;
        for (int x = 0; x < countX; x++)
        {
            for (int y = 0; y < countY; y++)
            {
                game.Draw2dTexture(game.GetTexture("background.png"), x * backgroundW, y * backgroundH, backgroundW, backgroundH, null, 0, Game.ColorFromArgb(255, 255, 255, 255), false);
            }
        }
    }
}
