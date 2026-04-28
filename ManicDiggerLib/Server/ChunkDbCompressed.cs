using OpenTK.Mathematics;

/// <summary>
/// Decorator over <see cref="IChunkDb"/> that transparently compresses data
/// before writing and decompresses after reading.
/// </summary>
public class ChunkDbCompressed : IChunkDb
{
    /// <summary>The underlying storage backend.</summary>
    public IChunkDb InnerChunkDb { get; set; }

    /// <summary>The compression algorithm to apply.</summary>
    public ICompression Compression { get; set; }

    /// <inheritdoc/>
    public void Open(string filename) => InnerChunkDb.Open(filename);

    /// <inheritdoc/>
    public void Backup(string backupFilename) => InnerChunkDb.Backup(backupFilename);

    /// <inheritdoc/>
    public bool ReadOnly
    {
        get => InnerChunkDb.ReadOnly;
        set => InnerChunkDb.ReadOnly = value;
    }

    /// <inheritdoc/>
    public IEnumerable<byte[]> GetChunks(IEnumerable<Vector3i> chunkpositions)
    {
        foreach (byte[] b in InnerChunkDb.GetChunks(chunkpositions))
            yield return Decompress(b);
    }

    /// <inheritdoc/>
    public Dictionary<Vector3i, byte[]> GetChunksFromFile(IEnumerable<Vector3i> chunkpositions, string filename)
    {
        Dictionary<Vector3i, byte[]> result = [];
        foreach (var (key, value) in InnerChunkDb.GetChunksFromFile(chunkpositions, filename))
            result.Add(key, Decompress(value));
        return result;
    }

    /// <inheritdoc/>
    public void SetChunks(IEnumerable<DbChunk> chunks)
        => InnerChunkDb.SetChunks(CompressChunks(chunks));

    /// <inheritdoc/>
    public void SetChunksToFile(IEnumerable<DbChunk> chunks, string filename)
        => InnerChunkDb.SetChunksToFile(CompressChunks(chunks), filename);

    /// <inheritdoc/>
    public byte[] GetGlobalData() => Decompress(InnerChunkDb.GetGlobalData());

    /// <inheritdoc/>
    public void DeleteChunks(IEnumerable<Vector3i> chunkpositions) => InnerChunkDb.DeleteChunks(chunkpositions);

    /// <inheritdoc/>
    public void SetGlobalData(byte[] data)
    {
        if (data == null)
        {
            InnerChunkDb.SetGlobalData(null);
        }
        else
        {
            InnerChunkDb.SetGlobalData(Compression.Compress(data));
        }
    }

    private byte[] Compress(byte[] data) => data == null ? null : Compression.Compress(data);
    private byte[] Decompress(byte[] data) => data == null ? null : Compression.Decompress(data);

    private IEnumerable<DbChunk> CompressChunks(IEnumerable<DbChunk> chunks)
    {
        foreach (DbChunk c in chunks)
            yield return new DbChunk { Position = c.Position, Chunk = Compress(c.Chunk) };
    }
}