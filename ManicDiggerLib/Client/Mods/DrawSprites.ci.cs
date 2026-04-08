using OpenTK.Mathematics;

/// <summary>
/// Renders billboard sprites for entities (e.g. particles, projectile effects) in the 3D world.
/// </summary>
public class ModDrawSprites : ModBase
{
    private const float SpriteScale = 0.02f;

    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        for (int i = 0; i < game.entities.Count; i++)
        {
            Entity entity = game.entities[i];
            if (entity?.sprite == null) continue;

            Sprite b = entity.sprite;
            int? frame = null;
            if (b.animationcount > 0)
            {
                float progress = 1f - (entity.expires.timeLeft / entity.expires.totalTime);
                frame = (int)(progress * (b.animationcount * b.animationcount - 1));
            }

            game.GLMatrixModeModelView();
            game.GLPushMatrix();
            game.GLTranslate(b.positionX, b.positionY, b.positionZ);
            Billboard(game);
            game.GLScale(SpriteScale, SpriteScale, SpriteScale);
            game.GLTranslate(-b.size / 2, -b.size / 2, 0);
            game.Draw2dTexture(game.GetTexture(b.image), 0, 0, b.size, b.size, frame, b.animationcount, Game.ColorFromArgb(255, 255, 255, 255), true);
            game.GLPopMatrix();
        }
    }

    /// <summary>
    /// Replaces the rotation component of the current model-view matrix with an identity rotation,
    /// making the object always face the camera (cylindrical billboard).
    /// See: http://stackoverflow.com/a/5487981
    /// </summary>
    public static void Billboard(Game game)
    {
        Matrix4 m = game.mvMatrix.Peek();

        float d = MathF.Sqrt(m.Row0.X * m.Row0.X + m.Row0.Y * m.Row0.Y + m.Row0.Z * m.Row0.Z);

        m.Row0 = new Vector4(d, 0, 0, 0);
        m.Row1 = new Vector4(0, d, 0, 0);
        m.Row2 = new Vector4(0, 0, d, 0);
        m.Row3 = new Vector4(m.Row3.X, m.Row3.Y, m.Row3.Z, 1);

        Matrix4.CreateRotationX(MathF.PI, out Matrix4 rotX);
        m = rotX * m;

        game.mvMatrix.Pop();
        game.mvMatrix.Push(m);
        game.GLLoadMatrix(m);
    }
}