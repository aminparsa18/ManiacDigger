using System.Numerics;

/// <summary>
/// Renders floating 3D text labels attached to entities, facing the player when nearby.
/// </summary>
public class ModDrawText : ModBase
{
    private const float TextScale = 0.005f;
    private const float TextDrawDistance = 20f;

    private static readonly FontCi Font = new() { family = "Arial", size = 14 };

    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        for (int i = 0; i < game.entitiesCount; i++)
        {
            Entity e = game.entities[i];
            if (e?.drawText == null) continue;
            if (e.networkPosition != null && !e.networkPosition.PositionLoaded) continue;

            EntityDrawText p = e.drawText;
            float posX = -MathF.Sin(e.position.roty) * p.dx + e.position.x;
            float posY = p.dy + e.position.y;
            float posZ = MathF.Cos(e.position.roty) * p.dz + e.position.z;

            bool nearEnough = Vector3.Distance(new Vector3(game.player.position.x, game.player.position.y, game.player.position.z), new Vector3(posX, posY, posZ)) < TextDrawDistance;
            bool altHeld = game.keyboardState[Game.KeyAltLeft] || game.keyboardState[Game.KeyAltRight];
            if (!nearEnough && !altHeld) continue;

            DrawText(game, e, p, posX, posY, posZ);
        }
    }

    private static void DrawText(Game game, Entity e, EntityDrawText p, float posX, float posY, float posZ)
    {
        game.GLPushMatrix();
        game.GLTranslate(posX, posY, posZ);
        game.GLRotate(180, 1, 0, 0);
        game.GLRotate(float.RadiansToDegrees(e.position.roty), 0, 1, 0);
        game.GLScale(TextScale, TextScale, TextScale);
        game.Draw2dText(p.text, Font, -game.TextSizeWidth(p.text, 14) / 2, 0, Game.ColorFromArgb(255, 255, 255, 255), true);
        game.GLPopMatrix();
    }
}