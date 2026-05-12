using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

/// <summary>
/// Compares the original VoxelMap against the rewritten version across three scenarios:
///
///   GetBlock     — hot path: recomputed chunk dims + VectorIndexUtil vs cached dims + inlined math
///   GetMapPortion — bulk read: per-voxel index recomputation vs hoisted row bases
///   DirtyMarking  — SetMapPortion dirty: repeated SetChunkDirty calls vs HashSet deduplication
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class VoxelMapBenchmarks
{
    private class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(10)
                .WithToolchain(InProcessEmitToolchain.Instance));
        }
    }

    // Map: 128³ blocks = 8³ chunks at ChunkSize=16
    private const int MapSize = 128;
    private const int ChunkSize = 16;
    private const int CsBits = 4; // log2(16)

    // Portion size used for GetMapPortion — one chunk worth of blocks with padding
    private const int PortionSize = 18; // ChunkSize + 2 (typical buffered chunk)

    private VoxelMapOriginal _original = null!;
    private VoxelMapRewritten _rewritten = null!;

    private int[] _portionBuffer = null!;
    private int[] _sourceBuffer = null!;

    [GlobalSetup]
    public void Setup()
    {
        _original = new VoxelMapOriginal(MapSize, MapSize, MapSize);
        _rewritten = new VoxelMapRewritten(MapSize, MapSize, MapSize);

        // Populate all chunks so GetBlock never hits null
        for (int z = 0; z < MapSize; z += ChunkSize)
            for (int y = 0; y < MapSize; y += ChunkSize)
                for (int x = 0; x < MapSize; x += ChunkSize)
                {
                    _original.GetChunkAt(x >> CsBits, y >> CsBits, z >> CsBits);
                    _rewritten.GetChunkAt(x >> CsBits, y >> CsBits, z >> CsBits);
                }

        // Write some non-zero values
        var rng = new Random(42);
        for (int i = 0; i < 10_000; i++)
            _original.SetBlock(
                rng.Next(MapSize), rng.Next(MapSize), rng.Next(MapSize),
                rng.Next(1, 10));

        // Sync rewritten with same data
        rng = new Random(42);
        for (int i = 0; i < 10_000; i++)
            _rewritten.SetBlock(
                rng.Next(MapSize), rng.Next(MapSize), rng.Next(MapSize),
                rng.Next(1, 10));

        _portionBuffer = new int[PortionSize * PortionSize * PortionSize];
        _sourceBuffer = new int[ChunkSize * ChunkSize * ChunkSize];
    }

    // ── GetBlock hot path ─────────────────────────────────────────────────────

    [Benchmark(Baseline = true, Description = "Original  — GetBlock (recomputed dims, VectorIndexUtil)")]
    [BenchmarkCategory("GetBlock")]
    public int Original_GetBlock()
    {
        int sum = 0;
        for (int z = 0; z < MapSize; z++)
            for (int y = 0; y < MapSize; y++)
                for (int x = 0; x < MapSize; x++)
                    sum += _original.GetBlock(x, y, z);
        return sum;
    }

    [Benchmark(Description = "Rewritten — GetBlock (cached dims, inlined math)")]
    [BenchmarkCategory("GetBlock")]
    public int Rewritten_GetBlock()
    {
        int sum = 0;
        for (int z = 0; z < MapSize; z++)
            for (int y = 0; y < MapSize; y++)
                for (int x = 0; x < MapSize; x++)
                    sum += _rewritten.GetBlock(x, y, z);
        return sum;
    }

    // ── GetMapPortion ─────────────────────────────────────────────────────────

    [Benchmark(Description = "Original  — GetMapPortion (per-voxel index recomputation)")]
    [BenchmarkCategory("GetMapPortion")]
    public void Original_GetMapPortion()
        => _original.GetMapPortion(_portionBuffer, 7, 7, 7, PortionSize, PortionSize, PortionSize);

    [Benchmark(Description = "Rewritten — GetMapPortion (hoisted row bases)")]
    [BenchmarkCategory("GetMapPortion")]
    public void Rewritten_GetMapPortion()
        => _rewritten.GetMapPortion(_portionBuffer, 7, 7, 7, PortionSize, PortionSize, PortionSize);

    // ── Dirty marking — SetMapPortion ─────────────────────────────────────────

    // Real workload: server sends 2×2×2 chunk packets
    private const int SourceSizeX = ChunkSize * 2; // 32
    private const int SourceSizeY = ChunkSize * 2; // 32
    private const int SourceSizeZ = ChunkSize * 2; // 32

    [Benchmark(Baseline = true, Description = "Original  — SetMapPortion dirty (repeated per-block marking)")]
    [BenchmarkCategory("DirtyMarking")]
    public void Original_SetMapPortion()
    {
        _original = new VoxelMapOriginal(MapSize, MapSize, MapSize);
        _original.SetMapPortion(0, 0, 0, _sourceBuffer, SourceSizeX, SourceSizeY, SourceSizeZ);
    }

    [Benchmark(Description = "Rewritten — SetMapPortion dirty (HashSet deduplication)")]
    [BenchmarkCategory("DirtyMarking")]
    public void Rewritten_SetMapPortion()
    {
        _rewritten = new VoxelMapRewritten(MapSize, MapSize, MapSize);
        _rewritten.SetMapPortion(0, 0, 0, _sourceBuffer, SourceSizeX, SourceSizeY, SourceSizeZ);
    }
}

// ── Shared block storage (used by both implementations) ───────────────────────

public class BlockChunk
{
    public int[] Data;
    public bool Dirty;
    public bool BaseLightDirty;

    public BlockChunk(int size)
    {
        Data = new int[size];
    }

    public int GetBlock(int pos) => Data[pos];
    public void SetBlock(int pos, int val) => Data[pos] = val;
}

// ── Original VoxelMap ─────────────────────────────────────────────────────────

public class VoxelMapOriginal
{
    private const int Cs = 16;
    private const int CsBits = 4;
    private const int CsMask = Cs - 1;

    public int MapSizeX { get; }
    public int MapSizeY { get; }
    public int MapSizeZ { get; }

    private readonly BlockChunk[] _chunks;

    public VoxelMapOriginal(int sx, int sy, int sz)
    {
        MapSizeX = sx; MapSizeY = sy; MapSizeZ = sz;
        _chunks = new BlockChunk[sx / Cs * (sy / Cs) * (sz / Cs)];
    }

    // Recomputes map chunk dimensions on every call — original pattern
    private int ChunkIndex(int cx, int cy, int cz)
        => Index3d(cx, cy, cz, MapSizeX >> CsBits, MapSizeY >> CsBits);

    private static int Index3d(int x, int y, int z, int sx, int sy)
        => z * sx * sy + y * sx + x;

    private static int BlockIndex(int lx, int ly, int lz)
        => Index3d(lx, ly, lz, Cs, Cs);

    public BlockChunk GetChunkAt(int cx, int cy, int cz)
    {
        int ci = ChunkIndex(cx, cy, cz);
        return _chunks[ci] ??= new BlockChunk(Cs * Cs * Cs);
    }

    public int GetBlock(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0 || x >= MapSizeX || y >= MapSizeY || z >= MapSizeZ)
            return 0;

        int ci = ChunkIndex(x >> CsBits, y >> CsBits, z >> CsBits);
        BlockChunk chunk = _chunks[ci];
        if (chunk == null) return 0;
        return chunk.GetBlock(BlockIndex(x & CsMask, y & CsMask, z & CsMask));
    }

    public void SetBlock(int x, int y, int z, int val)
    {
        int ci = ChunkIndex(x >> CsBits, y >> CsBits, z >> CsBits);
        (_chunks[ci] ??= new BlockChunk(Cs * Cs * Cs))
            .SetBlock(BlockIndex(x & CsMask, y & CsMask, z & CsMask), val);
    }

    public void GetMapPortion(int[] out_, int x, int y, int z, int sx, int sy, int sz)
    {
        int mapCX = MapSizeX >> CsBits;
        int mapCY = MapSizeY >> CsBits;
        int total = mapCX * mapCY * (MapSizeZ >> CsBits);

        Array.Clear(out_, 0, sx * sy * sz);

        int sCX = x >> CsBits, eCX = (x + sx - 1) >> CsBits;
        int sCY = y >> CsBits, eCY = (y + sy - 1) >> CsBits;
        int sCZ = z >> CsBits, eCZ = (z + sz - 1) >> CsBits;

        for (int cx = sCX; cx <= eCX; cx++)
            for (int cy = sCY; cy <= eCY; cy++)
                for (int cz = sCZ; cz <= eCZ; cz++)
                {
                    int ci = cz * mapCX * mapCY + cy * mapCX + cx;
                    if ((uint)ci >= (uint)total) continue;
                    BlockChunk chunk = _chunks[ci];
                    if (chunk == null) continue;

                    int cgx = cx << CsBits, cgy = cy << CsBits, cgz = cz << CsBits;
                    int bx0 = Math.Max(x, cgx), bx1 = Math.Min(x + sx, cgx + Cs);
                    int by0 = Math.Max(y, cgy), by1 = Math.Min(y + sy, cgy + Cs);
                    int bz0 = Math.Max(z, cgz), bz1 = Math.Min(z + sz, cgz + Cs);

                    // Original: recomputes all index components inside innermost loop
                    for (int bx = bx0; bx < bx1; bx++)
                    {
                        int lcx = bx - cgx, ox = bx - x;
                        for (int by = by0; by < by1; by++)
                        {
                            int lcy = by - cgy, oy = by - y;
                            for (int bz = bz0; bz < bz1; bz++)
                            {
                                int lcz = bz - cgz, oz = bz - z;
                                int pos = (((lcz << CsBits) + lcy) << CsBits) + lcx;
                                out_[(((oz * sy) + oy) * sx) + ox] = chunk.GetBlock(pos);
                            }
                        }
                    }
                }
    }

    public void SetMapPortion(int x, int y, int z, int[] src, int sX, int sY, int sZ)
    {
        int chunksX = sX >> CsBits;
        int chunksY = sY >> CsBits;
        int chunksZ = sZ >> CsBits;

        for (int cx = 0; cx < chunksX; cx++)
            for (int cy = 0; cy < chunksY; cy++)
                for (int cz = 0; cz < chunksZ; cz++)
                {
                    int wx = x + (cx << CsBits);
                    int wy = y + (cy << CsBits);
                    int wz = z + (cz << CsBits);
                    int ci = ChunkIndex(wx >> CsBits, wy >> CsBits, wz >> CsBits);
                    BlockChunk c = _chunks[ci] ??= new BlockChunk(Cs * Cs * Cs);

                    // Fill chunk
                    for (int lz = 0; lz < Cs; lz++)
                        for (int ly = 0; ly < Cs; ly++)
                            for (int lx = 0; lx < Cs; lx++)
                                c.SetBlock((((lz << CsBits) + ly) << CsBits) + lx,
                                    src[(((lz + (cz << CsBits)) * sY + (ly + (cy << CsBits))) * sX) + lx + (cx << CsBits)]);

                    // Original: marks dirty once per chunk + 6 neighbors without deduplication
                    MarkDirty(wx >> CsBits, wy >> CsBits, wz >> CsBits);
                }
    }

    private void MarkDirty(int cx, int cy, int cz)
    {
        SetDirty(cx, cy, cz);
        SetDirty(cx - 1, cy, cz); SetDirty(cx + 1, cy, cz);
        SetDirty(cx, cy - 1, cz); SetDirty(cx, cy + 1, cz);
        SetDirty(cx, cy, cz - 1); SetDirty(cx, cy, cz + 1);
    }

    private void SetDirty(int cx, int cy, int cz)
    {
        if (cx < 0 || cy < 0 || cz < 0) return;
        if (cx >= MapSizeX >> CsBits || cy >= MapSizeY >> CsBits || cz >= MapSizeZ >> CsBits) return;
        int ci = ChunkIndex(cx, cy, cz);
        if (_chunks[ci] != null) _chunks[ci]!.Dirty = true;
    }
}

// ── Rewritten VoxelMap ────────────────────────────────────────────────────────

public class VoxelMapRewritten
{
    private const int Cs = 16;
    private const int CsBits = 4;
    private const int CsMask = Cs - 1;

    public int MapSizeX { get; }
    public int MapSizeY { get; }
    public int MapSizeZ { get; }

    // Cached once in constructor — no recomputation on hot path
    private readonly int _mapChunksX;
    private readonly int _mapChunksY;
    private readonly int _mapChunksZ;

    private readonly BlockChunk[] _chunks;

    public VoxelMapRewritten(int sx, int sy, int sz)
    {
        MapSizeX = sx; MapSizeY = sy; MapSizeZ = sz;
        _mapChunksX = sx >> CsBits;
        _mapChunksY = sy >> CsBits;
        _mapChunksZ = sz >> CsBits;
        _chunks = new BlockChunk[_mapChunksX * _mapChunksY * _mapChunksZ];
    }

    // Inlined — no helper call, no recomputation of map dims
    private int ChunkIndex(int cx, int cy, int cz)
        => cz * _mapChunksX * _mapChunksY + cy * _mapChunksX + cx;

    private static int BlockIndex(int lx, int ly, int lz)
        => (lz << CsBits << CsBits) + (ly << CsBits) + lx;

    public BlockChunk GetChunkAt(int cx, int cy, int cz)
    {
        int ci = ChunkIndex(cx, cy, cz);
        return _chunks[ci] ??= new BlockChunk(Cs * Cs * Cs);
    }

    public int GetBlock(int x, int y, int z)
    {
        if ((uint)x >= (uint)MapSizeX || (uint)y >= (uint)MapSizeY || (uint)z >= (uint)MapSizeZ)
            return 0;

        int ci = ChunkIndex(x >> CsBits, y >> CsBits, z >> CsBits);
        BlockChunk chunk = _chunks[ci];
        if (chunk == null) return 0;
        return chunk.GetBlock(BlockIndex(x & CsMask, y & CsMask, z & CsMask));
    }

    public void SetBlock(int x, int y, int z, int val)
    {
        int ci = ChunkIndex(x >> CsBits, y >> CsBits, z >> CsBits);
        (_chunks[ci] ??= new BlockChunk(Cs * Cs * Cs))
            .SetBlock(BlockIndex(x & CsMask, y & CsMask, z & CsMask), val);
    }

    public void GetMapPortion(int[] out_, int x, int y, int z, int sx, int sy, int sz)
    {
        int total = _mapChunksX * _mapChunksY * _mapChunksZ;

        Array.Clear(out_, 0, sx * sy * sz);

        int sCX = x >> CsBits, eCX = (x + sx - 1) >> CsBits;
        int sCY = y >> CsBits, eCY = (y + sy - 1) >> CsBits;
        int sCZ = z >> CsBits, eCZ = (z + sz - 1) >> CsBits;

        for (int cz = sCZ; cz <= eCZ; cz++)
        {
            int cgz = cz << CsBits;
            int bz0 = Math.Max(z, cgz), bz1 = Math.Min(z + sz, cgz + Cs);

            for (int cy = sCY; cy <= eCY; cy++)
            {
                int cgy = cy << CsBits;
                int by0 = Math.Max(y, cgy), by1 = Math.Min(y + sy, cgy + Cs);

                for (int cx = sCX; cx <= eCX; cx++)
                {
                    int ci = ChunkIndex(cx, cy, cz);
                    if ((uint)ci >= (uint)total) continue;
                    BlockChunk chunk = _chunks[ci];
                    if (chunk == null) continue;

                    int cgx = cx << CsBits;
                    int bx0 = Math.Max(x, cgx), bx1 = Math.Min(x + sx, cgx + Cs);

                    // Rewritten: Z and Y row bases hoisted outside innermost loop
                    for (int bz = bz0; bz < bz1; bz++)
                    {
                        int lcz = bz - cgz;
                        int chunkZ = lcz << CsBits << CsBits;
                        int outZ = (bz - z) * sy;

                        for (int by = by0; by < by1; by++)
                        {
                            int lcy = by - cgy;
                            int chunkBase = chunkZ + (lcy << CsBits);
                            int outBase = (outZ + (by - y)) * sx;

                            for (int bx = bx0; bx < bx1; bx++)
                                out_[outBase + (bx - x)] =
                                    chunk.GetBlock(chunkBase + (bx - cgx));
                        }
                    }
                }
            }
        }
    }

    public void SetMapPortion(int x, int y, int z, int[] src, int sX, int sY, int sZ)
    {
        int chunksX = sX >> CsBits;
        int chunksY = sY >> CsBits;
        int chunksZ = sZ >> CsBits;

        var dirtyBlocks = new HashSet<int>(chunksX * chunksY * chunksZ);
        var dirtyNeighbor = new HashSet<int>(chunksX * chunksY * chunksZ * 6);

        for (int cx = 0; cx < chunksX; cx++)
            for (int cy = 0; cy < chunksY; cy++)
                for (int cz = 0; cz < chunksZ; cz++)
                {
                    int wx = x + (cx << CsBits);
                    int wy = y + (cy << CsBits);
                    int wz = z + (cz << CsBits);
                    int ci = ChunkIndex(wx >> CsBits, wy >> CsBits, wz >> CsBits);
                    BlockChunk c = _chunks[ci] ??= new BlockChunk(Cs * Cs * Cs);

                    for (int lz = 0; lz < Cs; lz++)
                        for (int ly = 0; ly < Cs; ly++)
                            for (int lx = 0; lx < Cs; lx++)
                                c.SetBlock((((lz << CsBits) + ly) << CsBits) + lx,
                                    src[(((lz + (cz << CsBits)) * sY + (ly + (cy << CsBits))) * sX) + lx + (cx << CsBits)]);

                    int ccx = wx >> CsBits, ccy = wy >> CsBits, ccz = wz >> CsBits;
                    dirtyBlocks.Add(ChunkIndex(ccx, ccy, ccz));

                    TryAddNeighbor(ccx - 1, ccy, ccz, dirtyNeighbor);
                    TryAddNeighbor(ccx + 1, ccy, ccz, dirtyNeighbor);
                    TryAddNeighbor(ccx, ccy - 1, ccz, dirtyNeighbor);
                    TryAddNeighbor(ccx, ccy + 1, ccz, dirtyNeighbor);
                    TryAddNeighbor(ccx, ccy, ccz - 1, dirtyNeighbor);
                    TryAddNeighbor(ccx, ccy, ccz + 1, dirtyNeighbor);
                }

        foreach (int ci in dirtyBlocks)
            if (_chunks[ci] != null) _chunks[ci]!.Dirty = _chunks[ci]!.BaseLightDirty = true;

        foreach (int ci in dirtyNeighbor)
            if (!dirtyBlocks.Contains(ci) && _chunks[ci] != null)
                _chunks[ci]!.Dirty = true;
    }

    private void TryAddNeighbor(int cx, int cy, int cz, HashSet<int> set)
    {
        if ((uint)cx < (uint)_mapChunksX &&
            (uint)cy < (uint)_mapChunksY &&
            (uint)cz < (uint)_mapChunksZ)
            set.Add(ChunkIndex(cx, cy, cz));
    }

    private void SetDirty(int ci) { if (_chunks[ci] != null) _chunks[ci]!.Dirty = true; }
}