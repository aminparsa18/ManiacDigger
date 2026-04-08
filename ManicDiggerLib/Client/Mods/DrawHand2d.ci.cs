/// <summary>
/// Renders the player's held item as a 2D hand image overlay in first-person view.
/// </summary>
public class ModDrawHand2d : ModBase
{
    private string lastHandImage;

    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        if (!ShouldDrawHand(game)) return;

        string img = HandImage2d(game);
        if (img == null) return;

        game.OrthoMode(game.Width(), game.Height());

        if (lastHandImage != img)
        {
            lastHandImage = img;
            byte[] file = game.GetAssetFile(img);
            Bitmap bmp = game.platform.BitmapCreateFromPng(file, file.Length);
            if (bmp != null)
            {
                game.handTexture = game.platform.LoadTextureFromBitmap(bmp);
                game.platform.BitmapDelete(bmp);
            }
        }

        game.Draw2dTexture(game.handTexture, game.Width() / 2, game.Height() - 512, 512, 512, null, 0, Game.ColorFromArgb(255, 255, 255, 255), false);
        game.PerspectiveMode();
    }

    /// <summary>Returns true if the hand should be drawn (first-person view with 2D enabled).</summary>
    public static bool ShouldDrawHand(Game game) => !game.ENABLE_TPP_VIEW && game.ENABLE_DRAW2D;

    /// <summary>Returns the appropriate hand image path for the currently held item, or null if none.</summary>
    public static string HandImage2d(Game game)
    {
        Packet_Item item = game.d_Inventory.RightHand[game.ActiveMaterial];
        if (item == null) return null;

        return game.IronSights
            ? game.blocktypes[item.BlockId].IronSightsImage
            : game.blocktypes[item.BlockId].Handimage;
    }
}