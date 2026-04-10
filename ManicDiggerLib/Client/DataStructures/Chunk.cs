/// <summary>
/// Stores block data for a single chunk of the voxel map.
/// Block data is stored as <see cref="byte"/> until a block value of 255 or greater is set,
/// at which point the storage is transparently promoted to <see cref="int"/> to accommodate
/// the larger value.
/// </summary>
public class Chunk
{
    /// <summary>Compact byte storage used when all block values are below 255.</summary>
    internal byte[] data;

    /// <summary>
    /// Expanded int storage, allocated on demand when a block value of 255 or greater is written.
    /// When this is non-null, <see cref="data"/> is null.
    /// </summary>
    internal int[] dataInt;

    /// <summary>Per-block base light levels for this chunk.</summary>
    internal byte[] baseLight;

    /// <summary>Whether <see cref="baseLight"/> needs to be recalculated before next use.</summary>
    internal bool baseLightDirty = true;

    /// <summary>The last rendered state of this chunk, used by the renderer.</summary>
    internal RenderedChunk rendered;

    /// <summary>
    /// Returns the block value at the given flat index within this chunk.
    /// Reads from whichever backing store is currently active.
    /// </summary>
    /// <param name="pos">Flat index into the chunk's block array.</param>
    public int GetBlock(int pos) => dataInt != null ? dataInt[pos] : data[pos];

    /// <summary>
    /// Sets the block value at the given flat index within this chunk.
    /// If <paramref name="block"/> is 255 or greater and the chunk is using byte storage,
    /// the storage is promoted to int and all existing values are migrated.
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

        // Promote byte storage to int storage to accommodate the large block value.
        int n = Game.chunksize * Game.chunksize * Game.chunksize;
        dataInt = new int[n];
        for (int i = 0; i < n; i++)
            dataInt[i] = data[i];
        data = null;
        dataInt[pos] = block;
    }

    /// <summary>
    /// Returns <see langword="true"/> if this chunk has been populated with block data.
    /// A chunk with no data is considered empty/unloaded.
    /// </summary>
    public bool HasData() => data != null || dataInt != null;
}