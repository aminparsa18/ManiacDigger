using OpenTK.Mathematics;

public class ModDrawSprites : ClientMod
{
    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        float one = 1;
        for (int i = 0; i < game.entitiesCount; i++)
        {
            Entity entity = game.entities[i];
            if (entity == null) { continue; }
            if (entity.sprite == null) { continue; }
            Sprite b = entity.sprite;
            game.GLMatrixModeModelView();
            game.GLPushMatrix();
            game.GLTranslate(b.positionX, b.positionY, b.positionZ);
            Billboard(game);
            game.GLScale((one * 2 / 100), (one * 2 / 100), (one * 2 / 100));
            game.GLTranslate(0 - b.size / 2, 0 - b.size / 2, 0);
            //d_Draw2d.Draw2dTexture(night ? moontexture : suntexture, 0, 0, ImageSize, ImageSize, null, Color.White);
            int? n = null;
            if (b.animationcount > 0)
            {
                float progress = one - (entity.expires.timeLeft / entity.expires.totalTime);
                n = (int)(progress * (b.animationcount * b.animationcount - 1));
            }
            game.Draw2dTexture(game.GetTexture(b.image), 0, 0, b.size, b.size, n, b.animationcount, Game.ColorFromArgb(255, 255, 255, 255), true);
            game.GLPopMatrix();
        }
    }

    public static void Billboard(Game game)
    {
        Matrix4 m = game.mvMatrix.Peek();
        // http://stackoverflow.com/a/5487981
        // | d 0 0 T.x |
        // | 0 d 0 T.y |
        // | 0 0 d T.z |
        // | 0 0 0   1 |
        float d = MathF.Sqrt(m.Row0.X * m.Row0.X + m.Row0.Y * m.Row0.Y + m.Row0.Z * m.Row0.Z);

        m.Row0 = new Vector4(d, 0, 0, 0);
        m.Row1 = new Vector4(0, d, 0, 0);
        m.Row2 = new Vector4(0, 0, d, 0);
        m.Row3 = new Vector4(m.Row3.X, m.Row3.Y, m.Row3.Z, 1);

        Matrix4.CreateRotationX(Game.GetPi(), out Matrix4 rotX);
        m = rotX * m;
        game.mvMatrix.Pop();
        game.mvMatrix.Push(m);

        game.GLLoadMatrix(m);
    }
}
