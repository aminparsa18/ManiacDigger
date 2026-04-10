/// <summary>
/// A flat (2D) block map stored as a sparse array of fixed-size chunks.
/// Typically used for heightmap data that mirrors a <see cref="VoxelMap"/>.
/// <para>
/// <b>Note:</b> <see cref="ChunkSize"/> is hardcoded to 16 and must match
/// <see cref="ServerMapStorage.ChunkSize"/> if used alongside it.
/// </para>
/// </summary>
public class ChunkedMap2d
{
    public const int ChunkSize = 16;

    private int _chunkGridWidth;   // map width in chunks
    private int[][] _chunks;

    /// <param name="mapSizeX">Map width in blocks. Must be a multiple of <see cref="ChunkSize"/>.</param>
    /// <param name="mapSizeY">Map height in blocks. Must be a multiple of <see cref="ChunkSize"/>.</param>
    public ChunkedMap2d(int mapSizeX, int mapSizeY)
    {
        Restart(mapSizeX, mapSizeY);
    }

    // ── chunk-key helpers ──────────────────────────────────────────────────

    private int ChunkIndex(int x, int y)
        => VectorIndexUtil.Index2d(x / ChunkSize, y / ChunkSize, _chunkGridWidth);

    private static int BlockIndex(int x, int y)
        => VectorIndexUtil.Index2d(x % ChunkSize, y % ChunkSize, ChunkSize);

    // ── public API ────────────────────────────────────────────────────────

    public int GetBlock(int x, int y)
        => GetChunk(x, y)[BlockIndex(x, y)];

    public void SetBlock(int x, int y, int blocktype)
        => GetChunk(x, y)[BlockIndex(x, y)] = blocktype;

    public int[] GetChunk(int x, int y)
    {
        int index = ChunkIndex(x, y);
        return _chunks[index] ??= new int[ChunkSize * ChunkSize];
    }

    public void ClearChunk(int x, int y)
        => _chunks[ChunkIndex(x, y)] = null;

    /// <summary>
    /// Reinitialises the map for new dimensions, discarding all block data.
    /// </summary>
    public void Restart(int mapSizeX, int mapSizeY)
    {
        _chunkGridWidth = mapSizeX / ChunkSize;
        int n = _chunkGridWidth * (mapSizeY / ChunkSize);
        _chunks = new int[n][];
    }
}