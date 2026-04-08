/// <summary>Represents a minecart entity moving along rails.</summary>
public class Minecart
{
    internal bool enabled;
    internal float positionX, positionY, positionZ;
    internal VehicleDirection12 direction;
    internal VehicleDirection12 lastdirection;
    internal float progress;
}

/// <summary>
/// Renders minecart entities in the 3D world with interpolated rotation.
/// </summary>
public class ModDrawMinecarts : ModBase
{
    private const float VerticalOffset = -0.7f;
    private const float HalfSize = -0.5f;
    private const float HeightOffset = -0.3f;

    private int minecartTexture = -1;

    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        for (int i = 0; i < game.entities.Count; i++)
        {
            Minecart m = game.entities[i]?.minecart;
            if (m == null || !m.enabled) continue;
            Draw(game, m);
        }
    }

    private void Draw(Game game, Minecart m)
    {
        minecartTexture = minecartTexture == -1 ? game.GetTexture("minecart.png") : minecartTexture;

        float rot = AngleInterpolation.InterpolateAngle360(
            VehicleRotation(m.lastdirection),
            VehicleRotation(m.direction),
            m.progress);

        RectangleF[] cc = CuboidRenderer.CuboidNet(8, 8, 8, 0, 0);
        CuboidRenderer.CuboidNetNormalize(cc, 32, 16);

        game.GLPushMatrix();
        game.GLTranslate(m.positionX, m.positionY + VerticalOffset, m.positionZ);
        game.GLRotate(-rot - 90, 0, 1, 0);
        game.platform.BindTexture2d(minecartTexture);
        CuboidRenderer.DrawCuboidWorld(game, HalfSize, HeightOffset, HalfSize, 1, 1, 1, cc, 1);
        game.GLPopMatrix();
    }

    private static float VehicleRotation(VehicleDirection12 dir) => dir switch
    {
        VehicleDirection12.VerticalUp => 0,
        VehicleDirection12.DownRightRight or VehicleDirection12.UpLeftUp => 45,
        VehicleDirection12.HorizontalRight => 90,
        VehicleDirection12.UpRightRight or VehicleDirection12.DownLeftDown => 135,
        VehicleDirection12.VerticalDown => 180,
        VehicleDirection12.UpLeftLeft or VehicleDirection12.DownRightDown => 225,
        VehicleDirection12.HorizontalLeft => 270,
        VehicleDirection12.UpRightUp or VehicleDirection12.DownLeftLeft => 315,
        _ => 0,
    };
}