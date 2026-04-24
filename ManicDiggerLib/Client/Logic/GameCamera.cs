using OpenTK.Mathematics;

public partial class Game
{
    // -------------------------------------------------------------------------
    // Camera mode
    // -------------------------------------------------------------------------

    public void SetCamera(CameraType type)
    {
        switch (type)
        {
            case CameraType.Fpp:
                CameraType = CameraType.Fpp;
                SetFreeMouse(false);
                ENABLE_TPP_VIEW = false;
                OverheadCamera = false;
                break;

            case CameraType.Tpp:
                CameraType = CameraType.Tpp;
                ENABLE_TPP_VIEW = true;
                break;

            default: // Overhead
                CameraType = CameraType.Overhead;
                OverheadCamera = true;
                SetFreeMouse(true);
                ENABLE_TPP_VIEW = true;
                PlayerDestination = new Vector3(Player.position.x, Player.position.y, Player.position.z);
                break;
        }
    }

    public void CameraChange()
    {
        // Prevent switching camera mode when following an entity.
        if (Follow != null)
            return;

        switch (CameraType)
        {
            case CameraType.Fpp:
                CameraType = CameraType.Tpp;
                ENABLE_TPP_VIEW = true;
                break;

            case CameraType.Tpp:
                CameraType = CameraType.Overhead;
                OverheadCamera = true;
                SetFreeMouse(true);
                ENABLE_TPP_VIEW = true;
                PlayerDestination = new Vector3(Player.position.x, Player.position.y, Player.position.z);
                break;

            case CameraType.Overhead:
                CameraType = CameraType.Fpp;
                SetFreeMouse(false);
                ENABLE_TPP_VIEW = false;
                OverheadCamera = false;
                break;

            default:
                throw new ArgumentException($"Camera type is unknown in: {nameof(Game)} - {nameof(CameraChange)}");
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

        return VoxelMap.IsValidPos(bx, by, bz) ? VoxelMap.GetBlockValid(bx, by, bz) : 0;
    }
}