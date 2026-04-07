public class SunMoonRenderer : ClientMod
{
    public SunMoonRenderer()
    {
        one = 1;
        hour = 6;
        t = 0;
        suntexture = -1;
        moontexture = -1;
        ImageSize = 96;
        day_length_in_seconds = 30;
    }
    private int hour;
    private readonly float one;
    public int GetHour()
    {
        return hour;
    }
    public void SetHour(int value)
    {
        hour = value;
        t = (hour - 6) / (one * 24) * 2 * Game.GetPi();
    }
    private float t;
    private int suntexture;
    private int moontexture;
    internal int ImageSize;
    internal float day_length_in_seconds;
    public override void OnNewFrameDraw3d(Game game, float dt)
    {
        GamePlatform platform = game.platform;
        game.GLMatrixModeModelView();
        if (suntexture == -1)
        {
            suntexture = game.GetTexture("sun.png");
            moontexture = game.GetTexture("moon.png");
        }
        UpdateSunMoonPosition(game, dt);

        float posX;
        float posY;
        float posZ;
        if (!game.isNight)
        {
            posX = game.sunPositionX;
            posY = game.sunPositionY;
            posZ = game.sunPositionZ;
        }
        else
        {
            posX = game.moonPositionX;
            posY = game.moonPositionY;
            posZ = game.moonPositionZ;
        }
        posX += game.player.position.x;
        posY += game.player.position.y;
        posZ += game.player.position.z;
        
        game.GLPushMatrix();
        game.GLTranslate(posX, posY, posZ);
        ModDrawSprites.Billboard(game);
        game.GLScale(one * 2 / 100, one * 2 / 100, one * 2 / 100);
        //GL.Translate(-ImageSize / 2, -ImageSize / 2, 0);
        game.Draw2dTexture(game.isNight ? moontexture : suntexture, 0, 0, ImageSize, ImageSize, null, 0, Game.ColorFromArgb(255, 255, 255, 255), false);
        game.GLPopMatrix();
    }

    private void UpdateSunMoonPosition(Game game, float dt)
    {
        t += dt * 2 * Game.GetPi() / day_length_in_seconds;
        game.isNight = (t + 2 * Game.GetPi()) % (2 * Game.GetPi()) > Game.GetPi();
        game.sunPositionX = MathF.Cos(t) * 20;
        game.sunPositionY = MathF.Sin(t) * 20;
        game.sunPositionZ = MathF.Sin(t) * 20;
        game.moonPositionX = MathF.Cos(-t) * 20;
        game.moonPositionY = MathF.Sin(-t) * 20;
        game.moonPositionZ = MathF.Sin(t) * 20;
    }
}
