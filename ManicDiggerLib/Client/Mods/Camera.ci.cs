using OpenTK.Mathematics;

/// <summary>
/// Handles first-person, third-person, and overhead camera positioning each frame.
/// </summary>
public class ModCamera : ModBase
{
    private static readonly Vector3 Up = Vector3.UnitY;
    private Vector3 overheadCameraEye;

    public override void OnBeforeNewFrameDraw3d(Game game, float deltaTime)
    {
        game.camera = game.overheadcamera ? OverheadCamera(game) : FppCamera(game);
    }

    internal Matrix4 OverheadCamera(Game game)
    {
        game.overheadcameraK.GetPosition(game.platform, ref overheadCameraEye);
        Vector3 eye = overheadCameraEye;
        Vector3 target = new(
            game.overheadcameraK.Center.X,
            game.overheadcameraK.Center.Y + game.GetCharacterEyesHeight(),
            game.overheadcameraK.Center.Z);

        game.overheadcameradistance = LimitThirdPersonCameraToWalls(game, ref eye, ref target, game.overheadcameradistance);
        SetCameraEye(game, eye);
        return Matrix4.LookAt(eye, target, Up);
    }

    internal static Matrix4 FppCamera(Game game)
    {
        Vector3 forward = new();
        VectorUtils.ToVectorInFixedSystem(0, 0, 1, game.player.position.rotx, game.player.position.roty, ref forward);

        float eyeX = game.player.position.x;
        float eyeY = game.player.position.y + game.GetCharacterEyesHeight();
        float eyeZ = game.player.position.z;

        Vector3 eye, target;

        if (!game.ENABLE_TPP_VIEW)
        {
            eye = new Vector3(eyeX, eyeY, eyeZ);
            target = new Vector3(eyeX + forward.X, eyeY + forward.Y, eyeZ + forward.Z);
        }
        else
        {
            eye = new Vector3(eyeX + forward.X * -game.tppcameradistance,
                                 eyeY + forward.Y * -game.tppcameradistance,
                                 eyeZ + forward.Z * -game.tppcameradistance);
            target = new Vector3(eyeX, eyeY, eyeZ);
            game.tppcameradistance = LimitThirdPersonCameraToWalls(game, ref eye, ref target, game.tppcameradistance);
        }

        SetCameraEye(game, eye);
        return Matrix4.LookAt(eye, target, Up);
    }

    /// <summary>
    /// Casts a ray from the camera target toward the eye and pulls the camera
    /// in if terrain blocks the view, with a minimum distance of 0.3 units.
    /// </summary>
    internal static float LimitThirdPersonCameraToWalls(Game game, ref Vector3 eye, ref Vector3 target, float distance)
    {
        const float MinDistance = 0.3f;

        Vector3 dir = eye - target;
        float dirLength = dir.Length;
        dir /= dirLength;

        Vector3 rayEnd = target + dir * (game.tppcameradistance + 1);
        Line3D pick = new()
        {
            Start = target,
            End = rayEnd
        };

        ArraySegment<BlockPosSide> hits = game.Pick(game.s, pick, out int hitCount);
        if (hitCount > 0)
        {
            BlockPosSide nearest = game.Nearest(hits, hitCount, target);
            float pickDistance = new Vector3(nearest.blockPos[0] - target.X, nearest.blockPos[1] - target.Y, nearest.blockPos[2] - target.Z).Length;
            distance = Math.Max(MinDistance, Math.Min(pickDistance - 1, distance));
        }

        eye = target + dir * distance;
        return distance;
    }

    /// <summary>Writes the eye position back to the game for other systems to use.</summary>
    private static void SetCameraEye(Game game, Vector3 eye)
    {
        game.CameraEyeX = eye.X;
        game.CameraEyeY = eye.Y;
        game.CameraEyeZ = eye.Z;
    }
}