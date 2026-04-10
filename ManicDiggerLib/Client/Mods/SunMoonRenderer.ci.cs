/// <summary>
/// Renders the sun and moon as billboarded sprites and updates their positions based on time of day.
/// </summary>
public class SunMoonRenderer : ModBase
{
    private const float TwoPi = 2 * MathF.PI;
    private const float OrbitRadius = 20f;
    private const float SpriteScale = 0.02f;

    internal int ImageSize = 96;
    internal float day_length_in_seconds = 30f;

    private int hour = 6;
    private float t;
    private int sunTexture = -1;
    private int moonTexture = -1;

    public int GetHour() => hour;

    public void SetHour(int value)
    {
        hour = value;
        t = (hour - 6) / 24f * TwoPi;
    }

    public override void OnNewFrameDraw3d(Game game, float dt)
    {
        if (sunTexture == -1)
        {
            sunTexture = game.GetTexture("sun.png");
            moonTexture = game.GetTexture("moon.png");
        }

        UpdateSunMoonPosition(dt, game);

        float bodyX = (game.isNight ? game.moonPositionX : game.sunPositionX) + game.player.position.x;
        float bodyY = (game.isNight ? game.moonPositionY : game.sunPositionY) + game.player.position.y;
        float bodyZ = (game.isNight ? game.moonPositionZ : game.sunPositionZ) + game.player.position.z;

        game.GLMatrixModeModelView();
        game.GLPushMatrix();
        game.GLTranslate(bodyX, bodyY, bodyZ);
        ModDrawSprites.Billboard(game);
        game.GLScale(SpriteScale, SpriteScale, SpriteScale);
        game.Draw2dTexture(game.isNight ? moonTexture : sunTexture, 0, 0, ImageSize, ImageSize, null, 0, ColorUtils.ColorFromArgb(255, 255, 255, 255), false);
        game.GLPopMatrix();
    }

    private void UpdateSunMoonPosition(float dt, Game game)
    {
        t += dt * TwoPi / day_length_in_seconds;

        game.isNight = (t + TwoPi) % TwoPi > MathF.PI;

        game.sunPositionX = MathF.Cos(t) * OrbitRadius;
        game.sunPositionY = MathF.Sin(t) * OrbitRadius;
        game.sunPositionZ = MathF.Sin(t) * OrbitRadius;
        game.moonPositionX = MathF.Cos(-t) * OrbitRadius;
        game.moonPositionY = MathF.Sin(-t) * OrbitRadius;
        game.moonPositionZ = MathF.Sin(t) * OrbitRadius;
    }
}