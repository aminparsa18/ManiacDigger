/// <summary>
/// Handles screenshot capture on F12 and displays a brief flash overlay after taking one.
/// </summary>
public class ModScreenshot : ModBase
{
    private const int FlashFrames = 5;
    private const int FlashFontSize = 50;
    private const string ScreenshotText = "&0Screenshot";

    private static readonly FontCi FlashFont = new() { family = "Arial", size = FlashFontSize };
    private static readonly int White = Game.ColorFromArgb(255, 255, 255, 255);

    private bool takeScreenshot;
    private int screenshotFlashFramesLeft;

    public override void OnNewFrameDraw2d(Game game, float deltaTime)
    {
        if (takeScreenshot)
        {
            takeScreenshot = false;
            game.platform.SaveScreenshot(); // Must be done after rendering, before SwapBuffers
            screenshotFlashFramesLeft = FlashFrames;
        }

        if (screenshotFlashFramesLeft > 0)
        {
            DrawScreenshotFlash(game);
            screenshotFlashFramesLeft--;
        }
    }

    public override void OnKeyDown(Game game, KeyEventArgs args)
    {
        if (args.GetKeyCode() != game.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.F12)) return;
        takeScreenshot = true;
        args.SetHandled(true);
    }

    internal static void DrawScreenshotFlash(Game game)
    {
        game.Draw2dTexture(game.WhiteTexture(), 0, 0, game.platform.GetCanvasWidth(), game.platform.GetCanvasHeight(), null, 0, White, false);
        game.platform.TextSize(ScreenshotText, FlashFontSize, out int textWidth, out int textHeight);
        game.Draw2dText(ScreenshotText, FlashFont, game.Xcenter(textWidth), game.Ycenter(textHeight), null, false);
    }
}