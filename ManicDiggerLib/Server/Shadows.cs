public class InfiniteMapChunked2dServer
{
    public IMapStorage? d_Map;
    private ChunkedMap2d<ushort> _map;

    public void Restart()
        => _map = new ChunkedMap2d<ushort>(d_Map.MapSizeX, d_Map.MapSizeY);

    public int GetBlock(int x, int y)
        => _map.GetBlock(x, y);

    public void SetBlock(int x, int y, int blocktype)
        => _map.SetBlock(x, y, (ushort)blocktype);

    public ushort[] GetChunk(int x, int y)
        => _map.GetChunk(x, y);

    public void ClearChunk(int x, int y)
        => _map.ClearChunk(x, y);
}