using System.Buffers;

/// <summary>
/// Stores block data for a single chunk of the voxel map.
/// Block data is stored as <see cref="byte"/> until a block value of 255 or greater is set,
/// at which point the storage is transparently promoted to <see cref="int"/> to accommodate
/// the larger value.
/// </summary>
/// <remarks>
/// All backing arrays (<see cref="data"/>, <see cref="dataInt"/>, <see cref="baseLight"/>)
/// are rented from <see cref="ArrayPool{T}.Shared"/> and must be returned by calling
/// <see cref="Release"/> before the chunk reference is discarded.
/// </remarks>
public class Chunk
{
    /// <summary>Number of blocks in one dimension of a chunk.</summary>
    private static int ChunkSide => Game.chunksize;

    /// <summary>Total number of blocks in a full chunk volume (ChunkSide³).</summary>
    private static int ChunkVolume => Game.chunksize * Game.chunksize * Game.chunksize;

    // ── Backing stores ───────────────────────────────────────────────────────
    // Exactly one of data/dataInt is active at any time; the other is null.

    /// <summary>Compact byte storage used when all block values are below 255.</summary>
    internal byte[] data;

    /// <summary>
    /// Expanded int storage, allocated on demand when a block value ≥ 255 is written.
    /// When non-null, <see cref="data"/> is null (and has been returned to the pool).
    /// </summary>
    internal int[] dataInt;

    /// <summary>Per-block base light levels for this chunk.</summary>
    internal byte[] baseLight;

    /// <summary>Whether <see cref="baseLight"/> needs to be recalculated before next use.</summary>
    internal bool baseLightDirty = true;

    /// <summary>The last rendered state of this chunk, used by the renderer.</summary>
    internal RenderedChunk rendered;

    // ── Block accessors ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns the block value at the given flat index within this chunk.
    /// Reads from whichever backing store is currently active.
    /// </summary>
    /// <param name="pos">Flat index into the chunk's block array.</param>
    public int GetBlock(int pos) => dataInt != null ? dataInt[pos] : data[pos];

    /// <summary>
    /// Sets the block value at the given flat index within this chunk.
    /// If <paramref name="block"/> is ≥ 255 and the chunk is using byte storage,
    /// the byte array is returned to the pool and storage is promoted to a rented int array.
    /// </summary>
    /// <param name="pos">Flat index into the chunk's block array.</param>
    /// <param name="block">The block type to store.</param>
    public void SetBlock(int pos, int block)
    {
        if (dataInt != null)
        {
            dataInt[pos] = block;
            return;
        }

        if (block < 255)
        {
            data[pos] = (byte)block;
            return;
        }

        // ── Promote byte storage → int storage ───────────────────────────────
        // Rent a new int array at least ChunkVolume in size.
        int n = ChunkVolume;
        int[] promoted = ArrayPool<int>.Shared.Rent(n);
        for (int i = 0; i < n; i++)
            promoted[i] = data[i];

        // Return the now-redundant byte array to the pool.
        ArrayPool<byte>.Shared.Return(data);
        data = null;

        dataInt = promoted;
        dataInt[pos] = block;
    }

    /// <summary>
    /// Returns <see langword="true"/> if this chunk has been populated with block data.
    /// A chunk with no data is considered empty/unloaded.
    /// </summary>
    public bool HasData() => data != null || dataInt != null;

    /// <summary>
    /// Returns all pooled arrays back to <see cref="ArrayPool{T}.Shared"/> and nulls
    /// every reference so the chunk slot can safely be set to null by the caller.
    /// Must be called before discarding a chunk reference to avoid leaking pool memory.
    /// </summary>
    /// <remarks>
    /// It is safe to call <see cref="Release"/> on a chunk that was never fully
    /// initialised (e.g. if data was already null); each guard is checked individually.
    /// </remarks>
    public void Release()
    {
        if (data != null)
        {
            ArrayPool<byte>.Shared.Return(data);
            data = null;
        }

        if (dataInt != null)
        {
            ArrayPool<int>.Shared.Return(dataInt);
            dataInt = null;
        }

        if (baseLight != null)
        {
            ArrayPool<byte>.Shared.Return(baseLight);
            baseLight = null;
        }
    }
}