public interface IMapStorage
{
    int MapSizeX { get; }
    int MapSizeY { get; }
    int MapSizeZ { get; }
    int GetBlock(int x, int y, int z);
    void SetBlock(int x, int y, int z, int tileType);
}

public class MapStorage : IMapStorage
{
    private readonly VoxelMap _voxelMap;
    private readonly Action<int, int, int, int> _setBlock;

    public MapStorage(VoxelMap voxelMap, Action<int, int, int, int> setBlock)
    {
        _voxelMap = voxelMap;
        _setBlock = setBlock;
    }

    public int MapSizeX => _voxelMap.MapSizeX;
    public int MapSizeY => _voxelMap.MapSizeY;
    public int MapSizeZ => _voxelMap.MapSizeZ;

    public int GetBlock(int x, int y, int z) => _voxelMap.GetBlock(x, y, z);
    public void SetBlock(int x, int y, int z, int tileType) => _setBlock(x, y, z, tileType);
}
