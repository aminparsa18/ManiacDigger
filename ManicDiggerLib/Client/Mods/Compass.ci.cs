/// <summary>
/// Renders a compass HUD element when the player has a compass in their active material slots.
/// </summary>
public class ModCompass : ModBase
{
    private const float CompassSize = 175f;
    private const float CompassPosX = 100f;
    private const float CompassPosY = 100f;
    private const float NeedleDamping = 0.9f;
    private const float NeedleStiffness = 50f;

    private int compassId = -1;
    private int needleId = -1;
    private float compassAngle;
    private float compassVelocity;

    public override void OnNewFrameDraw2d(Game game, float dt)
    {
        if (game.guistate == GuiState.MapLoading) return;
        DrawCompass(game);
    }

    private static bool CompassInActiveMaterials(Game game)
    {
        for (int i = 0; i < 10; i++)
        {
            if (game.MaterialSlots_(i) == game.d_Data.BlockIdCompass)
                return true;
        }
        return false;
    }

    public void DrawCompass(Game game)
    {
        if (!CompassInActiveMaterials(game)) return;

        if (compassId == -1)
        {
            compassId = game.GetTexture("compass.png");
            needleId = game.GetTexture("compassneedle.png");
        }

        float posX = game.Width() - CompassPosX;
        float posY = CompassPosY;
        float playerOrientation = -(game.player.position.roty / (2 * MathF.PI)) * 360f;

        // Spring-damper smoothing toward player orientation
        compassVelocity += (playerOrientation - compassAngle) / NeedleStiffness;
        compassVelocity *= NeedleDamping;
        compassAngle += compassVelocity;

        int white = ColorUtils.ColorFromArgb(255, 255, 255, 255);

        // Compass rose
        game.Draw2dTexture(compassId, posX - CompassSize / 2, posY - CompassSize / 2, CompassSize, CompassSize, null, 0, white, false);

        // Compass needle (rotated to match orientation)
        game.GLPushMatrix();
        game.GLTranslate(posX, posY, 0);
        game.GLRotate(compassAngle, 0, 0, 90);
        game.GLTranslate(-CompassSize / 2, -CompassSize / 2, 0);
        game.Draw2dTexture(needleId, 0, 0, CompassSize, CompassSize, null, 0, white, false);
        game.GLPopMatrix();
    }
}