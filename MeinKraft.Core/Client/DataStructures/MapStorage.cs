public interface IMapStorage
{
    int MapSizeX { get; }
    int MapSizeY { get; }
    int MapSizeZ { get; }
    int GetBlock(int x, int y, int z);
}

public class MapStorage(IVoxelMap voxelMap) : IMapStorage
{
    private readonly IVoxelMap _voxelMap = voxelMap;

    public int MapSizeX => _voxelMap.MapSizeX;
    public int MapSizeY => _voxelMap.MapSizeY;
    public int MapSizeZ => _voxelMap.MapSizeZ;

    public int GetBlock(int x, int y, int z) => _voxelMap.GetBlock(x, y, z);
}
