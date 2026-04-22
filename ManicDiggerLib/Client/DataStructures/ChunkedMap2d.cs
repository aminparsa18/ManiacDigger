/// <summary>
/// A flat (2D) block map stored as a sparse array of fixed-size chunks.
/// Typically used for heightmap data that mirrors a <see cref="VoxelMap"/>.
/// </summary>
/// <remarks>
/// Chunks are allocated lazily on first write via the <c>??=</c> pattern in
/// <see cref="GetChunk"/>. Unwritten chunks read as <c>default(T)</c> (zero for
/// numeric types). Call <see cref="ClearChunk"/> to release a chunk's memory
/// without reinitialising the whole map.
/// <para>
/// <b>Note:</b> <see cref="ChunkSize"/> is hardcoded to 16 and must match
/// <c>ServerMapStorage.ChunkSize</c> if used alongside it.
/// </para>
/// </remarks>
/// <typeparam name="T">Element type. Use a value type (e.g. <see langword="int"/>,
/// <see langword="ushort"/>) to avoid per-element heap allocations.</typeparam>
public class ChunkedMap2d<T>
{
    /// <summary>
    /// Edge length of one chunk in blocks.
    /// Must be a power of two; the implementation uses bit-shifts for all
    /// division and modulo operations on this constant.
    /// </summary>
    public const int ChunkSize = 16;

    /// <summary>log₂(<see cref="ChunkSize"/>) — used for bit-shift arithmetic.</summary>
    private const int ChunkSizeBits = 4; // 2^4 = 16

    /// <summary>Bit mask equivalent to <c>% ChunkSize</c> for non-negative integers.</summary>
    private const int ChunkSizeMask = ChunkSize - 1; // 0b00001111

    /// <summary>Map width measured in chunks.</summary>
    private int _chunkGridWidth;

    /// <summary>
    /// Sparse array of chunk data buffers. A null slot means the chunk has
    /// never been written and reads as <c>default(T)</c>.
    /// </summary>
    private T[][] _chunks;

    // ── Construction ──────────────────────────────────────────────────────────

    /// <param name="mapSizeX">Map width in blocks. Must be a multiple of <see cref="ChunkSize"/>.</param>
    /// <param name="mapSizeY">Map height in blocks. Must be a multiple of <see cref="ChunkSize"/>.</param>
    public ChunkedMap2d(int mapSizeX, int mapSizeY)
    {
        Restart(mapSizeX, mapSizeY);
    }

    // ── Coordinate helpers ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the flat index of the chunk that contains block coordinate
    /// (<paramref name="x"/>, <paramref name="y"/>).
    /// Uses bit-shifting instead of division because <see cref="ChunkSize"/> is
    /// a power of two — avoids the integer divide on the hot path.
    /// </summary>
    private int ChunkIndex(int x, int y)
        => VectorIndexUtil.Index2d(x >> ChunkSizeBits, y >> ChunkSizeBits, _chunkGridWidth);

    /// <summary>
    /// Returns the flat index within a chunk for block coordinate
    /// (<paramref name="x"/>, <paramref name="y"/>).
    /// Uses a bitmask instead of modulo — equivalent for non-negative inputs.
    /// </summary>
    private static int BlockIndex(int x, int y)
        => VectorIndexUtil.Index2d(x & ChunkSizeMask, y & ChunkSizeMask, ChunkSize);

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Returns the block value at (<paramref name="x"/>, <paramref name="y"/>),
    /// or <c>default(T)</c> if the containing chunk has never been written.</summary>
    public T GetBlock(int x, int y)
        => GetChunk(x, y)[BlockIndex(x, y)];

    /// <summary>Writes <paramref name="blocktype"/> at (<paramref name="x"/>, <paramref name="y"/>),
    /// allocating the containing chunk on first write.</summary>
    public void SetBlock(int x, int y, T blocktype)
        => GetChunk(x, y)[BlockIndex(x, y)] = blocktype;

    /// <summary>
    /// Returns the data buffer for the chunk containing block (<paramref name="x"/>, <paramref name="y"/>),
    /// allocating a zeroed buffer on first access.
    /// </summary>
    public T[] GetChunk(int x, int y)
    {
        int index = ChunkIndex(x, y);
        return _chunks[index] ??= new T[ChunkSize * ChunkSize];
    }

    /// <summary>
    /// Releases the data buffer for the chunk containing block
    /// (<paramref name="x"/>, <paramref name="y"/>), returning it to the GC.
    /// Subsequent reads from this chunk return <c>default(T)</c> until the
    /// chunk is written again.
    /// </summary>
    public void ClearChunk(int x, int y)
        => _chunks[ChunkIndex(x, y)] = null;

    /// <summary>
    /// Reinitialises the map for new dimensions, releasing all existing chunk buffers.
    /// The old chunks are eligible for GC collection after this call.
    /// </summary>
    /// <param name="mapSizeX">New map width in blocks. Must be a multiple of <see cref="ChunkSize"/>.</param>
    /// <param name="mapSizeY">New map height in blocks. Must be a multiple of <see cref="ChunkSize"/>.</param>
    public void Restart(int mapSizeX, int mapSizeY)
    {
        _chunkGridWidth = mapSizeX >> ChunkSizeBits;
        int n = _chunkGridWidth * (mapSizeY >> ChunkSizeBits);
        _chunks = new T[n][];
    }
}