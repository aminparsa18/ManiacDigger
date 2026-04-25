using OpenTK.Mathematics;

/// <summary>
/// Renders player name tags and health bars above entity models in the 3D world.
/// </summary>
public class ModDrawPlayerNames : ModBase
{
    private const float NameTagHeightOffset = 0.7f;
    private const float NameTagScale = 0.02f;
    private const float NameTagDrawDistance = 20f;

    private readonly IGameClient game;

    public ModDrawPlayerNames(IGameClient game)
    {
        this.game = game;
    }

    public override void OnNewFrameDraw3d(float deltaTime)
    {
        for (int i = 0; i < game.Entities.Count; i++)
        {
            Entity e = game.Entities[i];
            if (e?.drawName == null) continue;
            if (i == game.LocalPlayerId) continue;
            if (e.networkPosition != null && !e.networkPosition.PositionLoaded) continue;

            DrawName p = e.drawName;
            if (p.OnlyWhenSelected) continue;

            float posX = p.TextX + e.position.x;
            float posY = p.TextY + e.position.y + e.drawModel.ModelHeight + NameTagHeightOffset;
            float posZ = p.TextZ + e.position.z;
            bool nearEnough = Vector3.Distance(new(game.Player.position.x, game.Player.position.y, game.Player.position.z), new(posX, posY, posZ)) < NameTagDrawDistance;
            bool altHeld = game.KeyboardState[Game.KeyAltLeft] || game.KeyboardState[Game.KeyAltRight];
            if (!nearEnough && !altHeld) continue;

            DrawNameTag(p, posX, posY, posZ);
        }
    }

    private void DrawNameTag(DrawName p, float posX, float posY, float posZ)
    {
        game.GLPushMatrix();
        game.GLTranslate(posX, posY, posZ);
        new ModDrawSprites(game).Billboard();
        game.GLScale(NameTagScale, NameTagScale, NameTagScale);

        if (p.DrawHealth)
        {
            game.Draw2dTexture(game.GetOrCreateWhiteTexture(), -26, -11, 52, 12, null, 0, ColorUtils.ColorFromArgb(255, 0, 0, 0), false);
            game.Draw2dTexture(game.GetOrCreateWhiteTexture(), -25, -10, 50 * p.Health, 10, null, 0, ColorUtils.ColorFromArgb(255, 255, 0, 0), false);
        }

        Font font = new("Arial", 14);
        game.Draw2dText(p.Name, font, -game.TextSizeWidth(p.Name, 14) / 2, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), true);

        game.GLPopMatrix();
    }
}