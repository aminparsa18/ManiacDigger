using System.Buffers;
using System.Numerics;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

/// <summary>
/// Compares the original ChunkedMap2d (new T[] per chunk) against the rewritten version
/// (ArrayPool + inlined index math) across two scenarios:
///
///   Alloc  — GetOrAllocChunk on a cold map (measures pool vs heap allocation cost)
///   Hot    — GetBlock / SetBlock on a fully populated map (measures index math hot path)
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class ChunkedMap2dBenchmarks
{
    private class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(10));
        }
    }

    private const int MapSize = 256; // 256x256 blocks = 16x16 chunk grid
    private const int ChunkSize = 16;

    // Pre-populated maps for hot-path benchmarks
    private ChunkedMap2dOriginal<int> _originalWarm = null!;
    private ChunkedMap2dRewritten<int> _rewrittenWarm = null!;

    // Cold maps reset before each alloc benchmark iteration
    private ChunkedMap2dOriginal<int> _originalCold = null!;
    private ChunkedMap2dRewritten<int> _rewrittenCold = null!;

    [GlobalSetup]
    public void Setup()
    {
        _originalWarm = new ChunkedMap2dOriginal<int>(MapSize, MapSize);
        _rewrittenWarm = new ChunkedMap2dRewritten<int>(MapSize, MapSize);

        // Populate every chunk so hot-path benchmarks never hit allocation
        for (int y = 0; y < MapSize; y += ChunkSize)
            for (int x = 0; x < MapSize; x += ChunkSize)
            {
                _originalWarm.GetChunk(x, y);
                _rewrittenWarm.GetOrAllocChunk(x, y);
            }
    }

    // ── Alloc benchmarks — cold map, measures pool vs new ────────────────────
    // IterationSetup resets the map before each measured iteration so every
    // GetOrAllocChunk call hits a null slot and must allocate.

    [IterationSetup(Targets = new[] { nameof(Original_AllocAllChunks), nameof(Rewritten_AllocAllChunks) })]
    public void ResetColdMaps()
    {
        _originalCold = new ChunkedMap2dOriginal<int>(MapSize, MapSize);
        _rewrittenCold.Restart(MapSize, MapSize); // return to pool, then reallocate
    }

    [Benchmark(Baseline = true, Description = "Original  — alloc all chunks (new T[])")]
    [BenchmarkCategory("Alloc")]
    public void Original_AllocAllChunks()
    {
        for (int y = 0; y < MapSize; y += ChunkSize)
            for (int x = 0; x < MapSize; x += ChunkSize)
                _originalCold.GetChunk(x, y);
    }

    [Benchmark(Description = "Rewritten — alloc all chunks (ArrayPool)")]
    [BenchmarkCategory("Alloc")]
    public void Rewritten_AllocAllChunks()
    {
        for (int y = 0; y < MapSize; y += ChunkSize)
            for (int x = 0; x < MapSize; x += ChunkSize)
                _rewrittenCold.GetOrAllocChunk(x, y);
    }

    // ── Hot path benchmarks — warm map, measures index math ──────────────────

    [Benchmark(Description = "Original  — GetBlock hot path")]
    [BenchmarkCategory("Hot")]
    public int Original_GetBlock()
    {
        int sum = 0;
        for (int y = 0; y < MapSize; y++)
            for (int x = 0; x < MapSize; x++)
                sum += _originalWarm.GetBlock(x, y);
        return sum;
    }

    [Benchmark(Description = "Rewritten — GetBlock hot path")]
    [BenchmarkCategory("Hot")]
    public int Rewritten_GetBlock()
    {
        int sum = 0;
        for (int y = 0; y < MapSize; y++)
            for (int x = 0; x < MapSize; x++)
                sum += _rewrittenWarm.GetBlock(x, y);
        return sum;
    }

    [Benchmark(Description = "Original  — SetBlock hot path")]
    [BenchmarkCategory("Hot")]
    public void Original_SetBlock()
    {
        for (int y = 0; y < MapSize; y++)
            for (int x = 0; x < MapSize; x++)
                _originalWarm.SetBlock(x, y, x + y);
    }

    [Benchmark(Description = "Rewritten — SetBlock hot path")]
    [BenchmarkCategory("Hot")]
    public void Rewritten_SetBlock()
    {
        for (int y = 0; y < MapSize; y++)
            for (int x = 0; x < MapSize; x++)
                _rewrittenWarm.SetBlock(x, y, x + y);
    }
}

// ── Original implementation ───────────────────────────────────────────────────

public class ChunkedMap2dOriginal<T>
{
    public const int ChunkSize = 16;
    private const int ChunkSizeBits = 4;
    private const int ChunkSizeMask = ChunkSize - 1;

    private int _chunkGridWidth;
    private T[][] _chunks;

    public ChunkedMap2dOriginal(int mapSizeX, int mapSizeY)
    {
        _chunkGridWidth = mapSizeX >> ChunkSizeBits;
        int n = _chunkGridWidth * (mapSizeY >> ChunkSizeBits);
        _chunks = new T[n][];
    }

    private int ChunkIndex(int x, int y)
        => Index2d(x >> ChunkSizeBits, y >> ChunkSizeBits, _chunkGridWidth);

    private static int BlockIndex(int x, int y)
        => Index2d(x & ChunkSizeMask, y & ChunkSizeMask, ChunkSize);

    private static int Index2d(int x, int y, int width) => y * width + x;

    public T? GetBlock(int x, int y)
    {
        T[] chunk = _chunks[ChunkIndex(x, y)];
        return chunk == null ? default : chunk[BlockIndex(x, y)];
    }

    public void SetBlock(int x, int y, T value)
        => GetChunk(x, y)[BlockIndex(x, y)] = value;

    public T[] GetChunk(int x, int y)
    {
        int index = ChunkIndex(x, y);
        return _chunks[index] ??= new T[ChunkSize * ChunkSize]; // ← heap allocation
    }
}

// ── Rewritten implementation ──────────────────────────────────────────────────

public class ChunkedMap2dRewritten<T>
{
    private readonly int _chunkSize;
    private readonly int _chunkSizeBits;
    private readonly int _chunkSizeMask;
    private readonly int _chunkArea;
    private int _chunkGridWidth;
    private T[]?[] _chunks;

    public ChunkedMap2dRewritten(int mapSizeX, int mapSizeY, int chunkSize = 16)
    {
        _chunkSize = chunkSize;
        _chunkSizeBits = BitOperations.TrailingZeroCount((uint)chunkSize);
        _chunkSizeMask = chunkSize - 1;
        _chunkArea = chunkSize * chunkSize;
        _chunks = [];
        Restart(mapSizeX, mapSizeY);
    }

    private int ChunkIndex(int x, int y)
        => (y >> _chunkSizeBits) * _chunkGridWidth + (x >> _chunkSizeBits);

    private int BlockIndex(int x, int y)
        => (y & _chunkSizeMask) * _chunkSize + (x & _chunkSizeMask);

    public T? GetBlock(int x, int y)
    {
        T[]? chunk = _chunks[ChunkIndex(x, y)];
        return chunk is null ? default : chunk[BlockIndex(x, y)];
    }

    public void SetBlock(int x, int y, T value)
        => GetOrAllocChunk(x, y)[BlockIndex(x, y)] = value;

    public T[] GetOrAllocChunk(int x, int y)
    {
        int ci = ChunkIndex(x, y);
        if (_chunks[ci] is not null) return _chunks[ci]!;

        T[] rented = ArrayPool<T>.Shared.Rent(_chunkArea); // ← pooled allocation
        Array.Clear(rented, 0, _chunkArea);
        _chunks[ci] = rented;
        return rented;
    }

    public void Restart(int mapSizeX, int mapSizeY)
    {
        if (_chunks is not null)
            foreach (T[]? chunk in _chunks)
                if (chunk is not null) ArrayPool<T>.Shared.Return(chunk);

        _chunkGridWidth = mapSizeX >> _chunkSizeBits;
        int n = _chunkGridWidth * (mapSizeY >> _chunkSizeBits);
        _chunks = new T[]?[n];
    }
}