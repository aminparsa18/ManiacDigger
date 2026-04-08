using OpenTK.Mathematics;

public partial class Game
{
    // -------------------------------------------------------------------------
    // Camera mode
    // -------------------------------------------------------------------------

    internal void SetCamera(CameraType type)
    {
        switch (type)
        {
            case CameraType.Fpp:
                cameratype = CameraType.Fpp;
                SetFreeMouse(false);
                ENABLE_TPP_VIEW = false;
                overheadcamera = false;
                break;

            case CameraType.Tpp:
                cameratype = CameraType.Tpp;
                ENABLE_TPP_VIEW = true;
                break;

            default: // Overhead
                cameratype = CameraType.Overhead;
                overheadcamera = true;
                SetFreeMouse(true);
                ENABLE_TPP_VIEW = true;
                playerdestination = new Vector3(player.position.x, player.position.y, player.position.z);
                break;
        }
    }

    public void CameraChange()
    {
        // Prevent switching camera mode when following an entity.
        if (Follow != null)
            return;

        switch (cameratype)
        {
            case CameraType.Fpp:
                cameratype = CameraType.Tpp;
                ENABLE_TPP_VIEW = true;
                break;

            case CameraType.Tpp:
                cameratype = CameraType.Overhead;
                overheadcamera = true;
                SetFreeMouse(true);
                ENABLE_TPP_VIEW = true;
                playerdestination = new Vector3(player.position.x, player.position.y, player.position.z);
                break;

            case CameraType.Overhead:
                cameratype = CameraType.Fpp;
                SetFreeMouse(false);
                ENABLE_TPP_VIEW = false;
                overheadcamera = false;
                break;

            default:
                platform.ThrowException("");
                break;
        }
    }

    // -------------------------------------------------------------------------
    // Camera block queries
    // -------------------------------------------------------------------------

    internal bool WaterSwimmingCamera() => GetCameraBlock() == -1 || IsWater(GetCameraBlock());

    internal bool LavaSwimmingCamera() => IsLava(GetCameraBlock());

    private int GetCameraBlock()
    {
        int bx = (int)MathF.Floor(CameraEyeX);
        int by = (int)MathF.Floor(CameraEyeZ);
        int bz = (int)MathF.Floor(CameraEyeY);

        return map.IsValidPos(bx, by, bz) ? map.GetBlockValid(bx, by, bz) : 0;
    }
}