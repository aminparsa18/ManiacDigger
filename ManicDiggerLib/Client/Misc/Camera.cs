using OpenTK.Mathematics;

public class Camera
{
    public Camera()
    {
        Distance = 5;
        Angle = 45;
        MinimumDistance = 2;
        T = 0;
        MaximumAngle = 89;
        MinimumAngle = 0;
        Center = new Vector3();
    }

    /// <summary>
    /// Computes the world-space position of the camera and writes it into <paramref name="ret"/>.
    /// The camera orbits <see cref="Center"/> at the current azimuth (<see cref="T"/>),
    /// elevation (<see cref="Angle"/>), and <see cref="Distance"/>.
    /// </summary>
    public void GetPosition(ref Vector3 ret)
    {
        float flatDist = GetFlatDistance();
        ret.X = MathF.Cos(T / 2) * flatDist + Center.X;
        ret.Y = Center.Y + GetCameraHeightFromCenter();
        ret.Z = MathF.Sin(T / 2) * flatDist + Center.Z;
    }

    /// <summary>Gets or sets the orbit radius, clamped to at least <see cref="MinimumDistance"/> on set.</summary>
    public float Distance
    {
        get => distance;
        set => distance = MathF.Max(value, MinimumDistance);
    }
    private float distance;

    /// <summary>Minimum allowed orbit radius.</summary>
    internal float MinimumDistance { get; set; }

    /// <summary>Elevation angle of the camera above the horizontal plane, in degrees.</summary>
    internal float Angle { get; set; }

    /// <summary>Maximum allowed elevation angle, in degrees.</summary>
    internal float MaximumAngle { get; set; }

    /// <summary>Minimum allowed elevation angle, in degrees.</summary>
    internal float MinimumAngle { get; set; }

    /// <summary>
    /// Horizontal rotation parameter (azimuth). The actual angle used in position math
    /// is <c>T / 2</c>, so a full orbit requires <c>T</c> to advance by <c>4π</c>.
    /// </summary>
    internal float T { get; set; }

    /// <summary>The point in world space the camera orbits around.</summary>
    internal Vector3 Center { get; set; }

    /// <summary>
    /// Returns the vertical offset of the camera above <see cref="Center"/>
    /// based on the current <see cref="Angle"/> and <see cref="Distance"/>.
    /// </summary>
    private float GetCameraHeightFromCenter()
    {
        return MathF.Sin(Angle * MathF.PI / 180) * Distance;
    }

    /// <summary>
    /// Returns the horizontal (XZ-plane) distance from <see cref="Center"/> to the camera
    /// based on the current <see cref="Angle"/> and <see cref="Distance"/>.
    /// </summary>
    private float GetFlatDistance()
    {
        return MathF.Cos(Angle * MathF.PI / 180) * Distance;
    }

    /// <summary>Rotates the camera left (counter-clockwise) by <paramref name="p"/> units.</summary>
    public void TurnLeft(float p) { T += p; }

    /// <summary>Rotates the camera right (clockwise) by <paramref name="p"/> units.</summary>
    public void TurnRight(float p) { T -= p; }

    /// <summary>
    /// Applies a <see cref="CameraMove"/> input to the camera for one frame.
    /// <paramref name="p"/> is scaled before use — turning scales by 4, angle changes by 40.
    /// </summary>
    public void Move(CameraMove camera_move, float p)
    {
        p *= 4;
        if (camera_move.TurnLeft) { TurnLeft(p); }
        if (camera_move.TurnRight) { TurnRight(p); }
        if (camera_move.DistanceUp) { Distance += p; }
        if (camera_move.DistanceDown) { Distance -= p; }
        if (camera_move.AngleUp) { Angle += p * 10; }
        if (camera_move.AngleDown) { Angle -= p * 10; }
        Distance = camera_move.Distance;
        SetValidAngle();
    }

    /// <summary>Clamps <see cref="Angle"/> to the range [<see cref="MinimumAngle"/>, <see cref="MaximumAngle"/>].</summary>
    private void SetValidAngle()
    {
        Angle = Math.Clamp(Angle, MinimumAngle, MaximumAngle);
    }

    /// <summary>
    /// Increases the elevation angle by <paramref name="p"/> degrees and clamps to valid range.
    /// </summary>
    public void TurnUp(float p)
    {
        Angle += p;
        SetValidAngle();
    }
}