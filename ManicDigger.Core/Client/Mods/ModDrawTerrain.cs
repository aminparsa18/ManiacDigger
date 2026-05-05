using ManicDigger;
using ManicDigger.Worker;
using OpenTK.Mathematics;
using System.Buffers;

/// <summary>
/// Client-side mod responsible for drawing the voxel terrain.
/// Finding dirty chunks and enqueuing tessellation work to <see cref="IChunkWorkQueue"/>.
/// All heavy tessellation work runs inside <see cref="ChunkTessellationDispatcher"/>.
/// </summary>
public class ModDrawTerrain : ModBase
{
    public const int MaxLight = 15;

    private const int NoChunk = -1;

    private readonly IGameService _platform;
    private readonly IVoxelMap _voxelMap;
    private readonly IMeshBatcher _meshBatcher;
    private readonly IChunkWorkQueue _chunkWorkQueue;
    private readonly ChunkTessellationDispatcher _dispatcher;

    private readonly IBlockRegistry _blockRegistry;
    private readonly LightBase _lightBase;
    private readonly LightBetweenChunks _lightBetweenChunks;
    private readonly int[] _shadowLightRadius = new int[GameConstants.MAX_BLOCKTYPES];
    private readonly bool[] _shadowIsTransparent = new bool[GameConstants.MAX_BLOCKTYPES];
    private bool _blockTypeCacheDirty = true;

    private bool _terrainStarted;
    private int _chunkUpdates;
    private int _lastPerfUpdateMs;
    private int _lastChunkUpdatesSnapshot;

    private readonly Vector3i[] _blocksAround7Buffer = new Vector3i[7];

    public ModDrawTerrain(
        IGameService platform,
        IVoxelMap voxelMap,
        IMeshBatcher meshBatcher,
        IBlockRegistry blockRegistry,
        ILightManager lightManager,
        IChunkWorkQueue chunkWorkQueue,
        ChunkTessellationDispatcher dispatcher,
        IGame game) : base(game)
    {
        _platform = platform;
        _voxelMap = voxelMap;
        _meshBatcher = meshBatcher;
        _blockRegistry = blockRegistry;
        _chunkWorkQueue = chunkWorkQueue;
        _dispatcher = dispatcher;
        _lightBase = new LightBase(voxelMap, lightManager);
        _lightBetweenChunks = new LightBetweenChunks(voxelMap);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public int TrianglesCount() => _meshBatcher.TotalTriangleCount();
    internal static int InvertChunk(int num) => (int)(num * (1.0f / GameConstants.CHUNK_SIZE));

    // ── ModBase overrides ─────────────────────────────────────────────────────

    public override void OnRender3d(float _)
    {
        if (Game.ShouldRedrawAllBlocks)
        {
            Game.ShouldRedrawAllBlocks = false;
            RedrawAllBlocks();
        }

        _meshBatcher.FlushPendingUploads();
        _meshBatcher.Draw(Game.LocalPositionX, Game.LocalPositionY, Game.LocalPositionZ);
        UpdatePerformanceInfo();
    }

    public override void OnFrame(float dt)
    {
        if (!_terrainStarted) return;

        // Mark chunks around the last placed block dirty — fast, no tessellation.
        RedrawChunksAroundLastPlacedBlock();

        // Refresh per-block-type light caches once when block types change.
        // Safe here because this runs single-threaded on the main thread.
        if (_blockTypeCacheDirty)
        {
            foreach ((int id, BlockType blockType) in _blockRegistry.BlockTypes)
            {
                _shadowLightRadius[id] = blockType.LightRadius;
                _shadowIsTransparent[id] = IsTransparentForLight(id);
            }
            _blockTypeCacheDirty = false;
        }

        // Enqueue one dirty chunk per worker so all workers stay busy.
        // The channel's DropOldest policy handles overflow gracefully.
        int slots = ChunkWorkerPool.DefaultWorkerCount;
        for (int i = 0; i < slots; i++)
        {
            (int x, int y, int z)? nearest = NearestDirty();
            if (!nearest.HasValue) break;

            (int cx, int cy, int cz) = nearest.Value;

            int mxc = _voxelMap.Mapsizexchunks;
            int myc = _voxelMap.Mapsizeychunks;
            Chunk c = _voxelMap.Chunks[VectorIndexUtil.Index3d(cx, cy, cz, mxc, myc)];
            if (c == null) continue;

            c.Rendered ??= new RenderedChunk();     // ensure Rendered exists before handoff

            // All lighting state is touched here, on the main thread, before the
            // work item is visible to any worker.
            byte[] snapshot = ComputeAndSnapshotShadows(cx, cy, cz, c);

            _chunkUpdates++;
            _ = _chunkWorkQueue.EnqueueAsync(new TessellationChunkWorkItem(
                ChunkX: cx,
                ChunkY: cy,
                ChunkZ: cz,
                Chunk: c,
                ShadowBuffer: snapshot,
                ShadowBufferRented: true));
        }
    }

    // ── Initialisation ────────────────────────────────────────────────────────

    public void StartTerrain()
    {
        _dispatcher.Start();
        _terrainStarted = true;
    }

    public void RedrawAllBlocks()
    {
        if (!_terrainStarted) StartTerrain();

        int chunksLength = InvertChunk(Game.MapSizeX)
                         * InvertChunk(Game.MapSizeY)
                         * InvertChunk(Game.MapSizeZ);

        for (int i = 0; i < chunksLength; i++)
        {
            Chunk c = _voxelMap.Chunks[i];
            if (c == null) continue;
            c.Rendered ??= new RenderedChunk();
            c.Rendered.Dirty = true;
            c.BaseLightDirty = true;
        }
    }

    // ── Dirty chunk detection (main thread) ───────────────────────────────────

    private void RedrawChunksAroundLastPlacedBlock()
    {
        if (Game.LastplacedblockX == NoChunk
         && Game.LastplacedblockY == NoChunk
         && Game.LastplacedblockZ == NoChunk)
            return;

        int mapSizeX = InvertChunk(_voxelMap.MapSizeX);
        int mapSizeY = InvertChunk(_voxelMap.MapSizeY);
        int mapSizeZ = InvertChunk(_voxelMap.MapSizeZ);

        BlocksAround7Inplace(
            new(Game.LastplacedblockX, Game.LastplacedblockY, Game.LastplacedblockZ),
            _blocksAround7Buffer);

        for (int i = 0; i < 7; i++)
        {
            Vector3i a = _blocksAround7Buffer[i];
            int cx = InvertChunk(a.X), cy = InvertChunk(a.Y), cz = InvertChunk(a.Z);

            if (cx < 0 || cy < 0 || cz < 0
             || cx >= mapSizeX || cy >= mapSizeY || cz >= mapSizeZ)
                continue;

            Chunk c = _voxelMap.Chunks[VectorIndexUtil.Index3d(cx, cy, cz, mapSizeX, mapSizeY)];
            if (c?.Rendered != null)
                c.Rendered.Dirty = true;
        }

        Game.LastplacedblockX = NoChunk;
        Game.LastplacedblockY = NoChunk;
        Game.LastplacedblockZ = NoChunk;
    }

    private (int x, int y, int z)? NearestDirty()
    {
        if (_voxelMap?.Chunks == null) return null;

        int px = InvertChunk((int)Game.LocalPositionX);
        int py = InvertChunk((int)Game.LocalPositionZ);
        int pz = InvertChunk((int)Game.LocalPositionY);
        int half = InvertChunk((int)Game.Config3d.ViewDistance);

        int mxc = _voxelMap.Mapsizexchunks;
        int myc = _voxelMap.Mapsizeychunks;
        int mzc = _voxelMap.Mapsizezchunks;

        int startX = Math.Max(px - half, 0);
        int startY = Math.Max(py - half, 0);
        int startZ = Math.Max(pz - half, 0);
        int endX = Math.Min(px + half, mxc - 1);
        int endY = Math.Min(py + half, myc - 1);
        int endZ = Math.Min(pz + half, mzc - 1);

        int bestIdx = -1;
        int bestDist = int.MaxValue;

        for (int ix = startX; ix <= endX; ix++)
            for (int iy = startY; iy <= endY; iy++)
                for (int iz = startZ; iz <= endZ; iz++)
                {
                    int i = VectorIndexUtil.Index3d(ix, iy, iz, mxc, myc);
                    Chunk c = _voxelMap.Chunks[i];
                    if (c?.Rendered == null || !c.Rendered.Dirty) continue;

                    int dx = px - ix, dy = py - iy, dz = pz - iz;
                    int dist = (dx * dx) + (dy * dy) + (dz * dz);
                    if (dist < bestDist) { bestDist = dist; bestIdx = i; }
                }

        if (bestIdx == -1) return null;

        // Mark as no longer dirty immediately so a second OnFrame call this
        // frame doesn't enqueue the same chunk twice.
        _voxelMap.Chunks[bestIdx].Rendered.Dirty = false;

        int biz = bestIdx / (mxc * myc);
        int biy = (bestIdx % (mxc * myc)) / mxc;
        int bix = bestIdx % mxc;
        return (bix, biy, biz);
    }

    private static void BlocksAround7Inplace(Vector3i pos, Vector3i[] buffer)
    {
        buffer[0] = pos;
        buffer[1] = new(pos.X + 1, pos.Y, pos.Z);
        buffer[2] = new(pos.X - 1, pos.Y, pos.Z);
        buffer[3] = new(pos.X, pos.Y + 1, pos.Z);
        buffer[4] = new(pos.X, pos.Y - 1, pos.Z);
        buffer[5] = new(pos.X, pos.Y, pos.Z + 1);
        buffer[6] = new(pos.X, pos.Y, pos.Z - 1);
    }

    // ── Shadow computation (main thread only) ─────────────────────────────────────

    private const int BufferedChunkEdge = 18;
    private const int BufferedChunkVolume = BufferedChunkEdge * BufferedChunkEdge * BufferedChunkEdge;

    /// <summary>
    /// Runs the two-stage lighting pipeline for the chunk at (cx, cy, cz),
    /// then snapshots the result into a freshly rented pool buffer.
    /// The caller (OnFrame) gives ownership of that buffer to ChunkWorkItem;
    /// the worker returns it after MakeChunk.
    /// </summary>
    private byte[] ComputeAndSnapshotShadows(int cx, int cy, int cz, Chunk target)
    {
        bool anyBaseLightChanged = false;

        for (int xx = 0; xx < 3; xx++)
            for (int yy = 0; yy < 3; yy++)
                for (int zz = 0; zz < 3; zz++)
                {
                    int cx1 = cx + xx - 1, cy1 = cy + yy - 1, cz1 = cz + zz - 1;
                    if (!_voxelMap.IsValidChunkPos(cx1, cy1, cz1)) continue;

                    Chunk neighbour = _voxelMap.Chunks[
                        VectorIndexUtil.Index3d(cx1, cy1, cz1,
                            _voxelMap.Mapsizexchunks,
                            _voxelMap.Mapsizeychunks)];
                    if (neighbour == null) continue;

                    if (neighbour.BaseLightDirty)
                    {
                        _lightBase.CalculateChunkBaseLight(
                            cx1, cy1, cz1,
                            _shadowLightRadius, _shadowIsTransparent);
                        neighbour.BaseLightDirty = false;
                        anyBaseLightChanged = true;
                    }
                }

        RenderedChunk rendered = target.Rendered;

        if (rendered.Light == null)
        {
            rendered.Light = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
            rendered.LightRented = true;
            rendered.Light.AsSpan(0, BufferedChunkVolume).Fill(15);
            anyBaseLightChanged = true;     // first time — must propagate regardless
        }

        if (anyBaseLightChanged)
        {
            _lightBetweenChunks.CalculateLightBetweenChunks(
                cx, cy, cz, _shadowLightRadius, _shadowIsTransparent);
        }

        // Snapshot rendered.Light into a new rented buffer for the worker.
        // The worker gets its own copy so rendered.Light remains valid for future
        // rebuilds even if the worker is still mid-flight on this snapshot.
        byte[] snapshot = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
        rendered.Light.AsSpan(0, BufferedChunkVolume)
                      .CopyTo(snapshot.AsSpan(0, BufferedChunkVolume));
        return snapshot;
    }

    public bool IsTransparentForLight(int blockId)
    {
        BlockType b = _blockRegistry.BlockTypes[blockId];
        return b.DrawType is not DrawType.Solid and not DrawType.ClosedDoor;
    }

    // ── Performance info ──────────────────────────────────────────────────────

    private void UpdatePerformanceInfo()
    {
        const float MsToSeconds = 1f / 1000f;
        float elapsed = (_platform.TimeMillisecondsFromStart - _lastPerfUpdateMs) * MsToSeconds;
        if (elapsed < 1f) return;

        _lastPerfUpdateMs = _platform.TimeMillisecondsFromStart;
        int updatesThisPeriod = _chunkUpdates - _lastChunkUpdatesSnapshot;
        _lastChunkUpdatesSnapshot = _chunkUpdates;

        Game.PerformanceInfo["chunk updates"] = string.Format(
            Game.Language.ChunkUpdates(), updatesThisPeriod.ToString());
        Game.PerformanceInfo["triangles"] = string.Format(
            Game.Language.Triangles(), TrianglesCount().ToString());
    }

    internal void Clear() => _meshBatcher.Clear();
}