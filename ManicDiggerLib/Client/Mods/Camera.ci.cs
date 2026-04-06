using OpenTK.Mathematics;
public class ModCamera : ClientMod
{
    public ModCamera()
    {
        OverheadCamera_cameraEye = new Vector3();
        upVec3 = new Vector3(0, 1, 0);
    }

    public override void OnBeforeNewFrameDraw3d(Game game, float deltaTime)
    {
        if (game.overheadcamera)
        {
            game.camera = OverheadCamera(game);
        }
        else
        {
            game.camera = FppCamera(game);
        }
    }

    internal Vector3 OverheadCamera_cameraEye;
    internal Matrix4 OverheadCamera(Game game)
    {
        game.overheadcameraK.GetPosition(game.platform, ref OverheadCamera_cameraEye);
        Vector3 cameraEye = OverheadCamera_cameraEye;
        Vector3 cameraTarget = new(
            game.overheadcameraK.Center.X,
            game.overheadcameraK.Center.Y + game.GetCharacterEyesHeight(),
            game.overheadcameraK.Center.Z);
        FloatRef currentOverheadcameradistance = FloatRef.Create(game.overheadcameradistance);
        LimitThirdPersonCameraToWalls(game, ref cameraEye, ref cameraTarget, currentOverheadcameradistance);
        Matrix4 ret = Matrix4.LookAt(cameraEye, cameraTarget, upVec3);
        game.CameraEyeX = cameraEye.X;
        game.CameraEyeY = cameraEye.Y;
        game.CameraEyeZ = cameraEye.Z;
        return ret;
    }
    private readonly Vector3 upVec3;

    internal Matrix4 FppCamera(Game game)
    {
        Vector3 forward = new();
        VectorTool.ToVectorInFixedSystem(0, 0, 1, game.player.position.rotx, game.player.position.roty, ref forward);
        Vector3 cameraEye = new();
        Vector3 cameraTarget = new();
        float playerEyeX = game.player.position.x;
        float playerEyeY = game.player.position.y + game.GetCharacterEyesHeight();
        float playerEyeZ = game.player.position.z;
        if (!game.ENABLE_TPP_VIEW)
        {
            cameraEye.X = playerEyeX;
            cameraEye.Y = playerEyeY;
            cameraEye.Z = playerEyeZ;
            cameraTarget.X = playerEyeX + forward.X;
            cameraTarget.Y = playerEyeY + forward.Y;
            cameraTarget.Z = playerEyeZ + forward.Z;
        }
        else
        {
            cameraEye.X = playerEyeX + forward.X * -game.tppcameradistance;
            cameraEye.Y = playerEyeY + forward.Y * -game.tppcameradistance;
            cameraEye.Z = playerEyeZ + forward.Z * -game.tppcameradistance;
            cameraTarget.X = playerEyeX;
            cameraTarget.Y = playerEyeY;
            cameraTarget.Z = playerEyeZ;
            FloatRef currentTppcameradistance = FloatRef.Create(game.tppcameradistance);
            LimitThirdPersonCameraToWalls(game, ref cameraEye, ref cameraTarget, currentTppcameradistance);
        }
        Matrix4 ret = Matrix4.LookAt(cameraEye, cameraTarget, upVec3);
        game.CameraEyeX = cameraEye.X;
        game.CameraEyeY = cameraEye.Y;
        game.CameraEyeZ = cameraEye.Z;
        return ret;
    }

    internal static void LimitThirdPersonCameraToWalls(Game game, ref Vector3 eye, ref Vector3 target, FloatRef curtppcameradistance)
    {
        float one = 1;
        Vector3 ray_start_point = target;
        Vector3 raytarget = eye;

        Line3D pick = new();
        float raydirX = (raytarget.X - ray_start_point.X);
        float raydirY = (raytarget.Y - ray_start_point.Y);
        float raydirZ = (raytarget.Z - ray_start_point.Z);

        float raydirLength1 = game.Length(raydirX, raydirY, raydirZ);
        raydirX /= raydirLength1;
        raydirY /= raydirLength1;
        raydirZ /= raydirLength1;
        raydirX = raydirX * (game.tppcameradistance + 1);
        raydirY = raydirY * (game.tppcameradistance + 1);
        raydirZ = raydirZ * (game.tppcameradistance + 1);
        pick.Start = new Vector3(ray_start_point.X, ray_start_point.Y, ray_start_point.Z);
        pick.End = new Vector3(ray_start_point.X + raydirX, ray_start_point.Y + raydirY, ray_start_point.Z + raydirZ);

        // pick terrain
        IntRef pick2Count = new();
        BlockPosSide[] pick2 = game.Pick(game.s, pick, pick2Count);

        if (pick2Count.value > 0)
        {
            BlockPosSide pick2nearest = game.Nearest(pick2, pick2Count.value, ray_start_point.X, ray_start_point.Y, ray_start_point.Z);

            float pickX = pick2nearest.blockPos[0] - target.X;
            float pickY = pick2nearest.blockPos[1] - target.Y;
            float pickZ = pick2nearest.blockPos[2] - target.Z;
            float pickdistance = game.Length(pickX, pickY, pickZ);
            curtppcameradistance.value = Math.Min(pickdistance - 1, curtppcameradistance.value);
            if (curtppcameradistance.value < one * 3 / 10) { curtppcameradistance.value = one * 3 / 10; }
        }

        float raydirLength = game.Length(raydirX, raydirY, raydirZ);
        raydirX /= raydirLength;
        raydirY /= raydirLength;
        raydirZ /= raydirLength;
        eye.X = target.X + raydirX * curtppcameradistance.value;
        eye.Y = target.Y + raydirY * curtppcameradistance.value;
        eye.Z = target.Z + raydirZ * curtppcameradistance.value;
    }
}
