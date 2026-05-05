using System.Buffers;

namespace ManicDigger.Worker;

/// <summary>
/// Implements <see cref="IChunkWorkDispatcher"/> for chunk tessellation.
///
/// Each worker thread gets its own <see cref="ChunkTessellationContext"/> via
/// <see cref="ThreadLocal{T}"/> — no locking, no shared mutable state.
/// </summary>
public sealed class ChunkTessellationDispatcher : IChunkWorkDispatcher
{
    private const int BufferedChunkEdge = 18;
    private const int BufferedChunkVolume = BufferedChunkEdge * BufferedChunkEdge * BufferedChunkEdge;

    private readonly IGame _game;
    private readonly IVoxelMap _voxelMap;
    private readonly IBlockRegistry _blockRegistry;
    private readonly IMeshBatcher _meshBatcher;

    private readonly ITerrainChunkTesselator _tesselator;
    private readonly ThreadLocal<ChunkTessellationContext> _context;

    private int _chunkSize;
    private int _bufferedChunkSize;
    private bool _started;

    public ChunkTessellationDispatcher(
        IGame game,
        IVoxelMap voxelMap,
        ITerrainChunkTesselator tesselator,
        IBlockRegistry blockRegistry,
        IMeshBatcher meshBatcher)
    {
        _game = game;
        _voxelMap = voxelMap;
        _tesselator = tesselator;
        _blockRegistry = blockRegistry;
        _meshBatcher = meshBatcher;

        _context = new ThreadLocal<ChunkTessellationContext>(
            () => _tesselator.CreateContext(),
            trackAllValues: false);
    }

    public void Start()
    {
        _chunkSize = GameConstants.CHUNK_SIZE;
        _bufferedChunkSize = _chunkSize + 2;
        _tesselator.Start();
        _started = true;
    }

    // ── IChunkWorkDispatcher ──────────────────────────────────────────────────

    public Task DispatchAsync(ChunkWorkItem item, CancellationToken ct)
    {
        if (!_started) return Task.CompletedTask;

        switch (item.Type)
        {
            case ChunkWorkType.Tessellate:
                TessellateChunk(item.ChunkX, item.ChunkY, item.ChunkZ);
                break;
        }

        return Task.CompletedTask;
    }

    // ── Tessellation (runs on worker thread) ──────────────────────────────────

    private void TessellateChunk(int x, int y, int z)
    {
        int mxc = _voxelMap.Mapsizexchunks;
        int myc = _voxelMap.Mapsizeychunks;

        Chunk c = _voxelMap.Chunks[VectorIndexUtil.Index3d(x, y, z, mxc, myc)];
        if (c == null) return;

        c.Rendered ??= new RenderedChunk();

        ChunkTessellationContext ctx = _context.Value;

        GetExtendedChunk(ctx, x, y, z);

        int meshCount = 0;
        VerticesIndicesToLoad[] meshData = null;
        bool dataRented = false;

        if (!IsUniformChunk(ctx.CurrentChunk, BufferedChunkVolume))
        {
            CalculateShadows(ctx, x, y, z);

            VerticesIndicesToLoad[] meshes = _tesselator.MakeChunk(
                x, y, z,
                _game.LightLevels,
                ctx,
                out meshCount);

            if (meshCount > 0)
            {
                meshData = ArrayPool<VerticesIndicesToLoad>.Shared.Rent(meshCount);
                dataRented = true;
                for (int i = 0; i < meshCount; i++)
                    meshData[i] = CloneVerticesIndicesToLoad(meshes[i]);
            }
        }

        meshData ??= [];

        // Worker thread: just stage it, no OpenGL, no queue injection needed
        _meshBatcher.StageChunk(c, meshData, meshCount, dataRented);
    }

    // ── Shadow / lighting (worker thread) ─────────────────────────────────────

    private void CalculateShadows(ChunkTessellationContext ctx, int cx, int cy, int cz)
    {
        if (ctx.BlockTypeCacheDirty)
        {
            foreach ((int id, BlockType? blockType) in _blockRegistry.BlockTypes)
            {
                ctx.ShadowLightRadius[id] = blockType.LightRadius;
                ctx.ShadowIsTransparent[id] = IsTransparentForLight(id);
            }
            ctx.BlockTypeCacheDirty = false;
        }

        bool anyBaseLightChanged = false;

        for (int xx = 0; xx < 3; xx++)
            for (int yy = 0; yy < 3; yy++)
                for (int zz = 0; zz < 3; zz++)
                {
                    int cx1 = cx + xx - 1;
                    int cy1 = cy + yy - 1;
                    int cz1 = cz + zz - 1;
                    if (!_voxelMap.IsValidChunkPos(cx1, cy1, cz1)) continue;

                    int nIdx = VectorIndexUtil.Index3d(
                        cx1, cy1, cz1,
                        _voxelMap.Mapsizexchunks,
                        _voxelMap.Mapsizeychunks);

                    Chunk neighbour = _voxelMap.Chunks[nIdx];
                    if (neighbour == null) continue;

                    if (neighbour.BaseLightDirty)
                    {
                        ctx.LightBase.CalculateChunkBaseLight(
                            _game, cx1, cy1, cz1,
                            ctx.ShadowLightRadius, ctx.ShadowIsTransparent);
                        neighbour.BaseLightDirty = false;
                        anyBaseLightChanged = true;
                    }
                }

        RenderedChunk rendered = _voxelMap
            .GetChunk(cx * _chunkSize, cy * _chunkSize, cz * _chunkSize).Rendered;

        if (rendered.Light == null)
        {
            rendered.Light = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
            rendered.LightRented = true;
            rendered.Light.AsSpan(0, BufferedChunkVolume).Fill(15);
            anyBaseLightChanged = true;
        }

        if (anyBaseLightChanged)
        {
            ctx.LightBetweenChunks.CalculateLightBetweenChunks(
                _game, cx, cy, cz,
                ctx.ShadowLightRadius, ctx.ShadowIsTransparent);
        }

        rendered.Light.AsSpan(0, BufferedChunkVolume)
                      .CopyTo(ctx.CurrentChunkShadows.AsSpan(0, BufferedChunkVolume));
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void GetExtendedChunk(ChunkTessellationContext ctx, int x, int y, int z)
    {
        _voxelMap.GetMapPortion(
            ctx.CurrentChunk,
            (x * _chunkSize) - 1, (y * _chunkSize) - 1, (z * _chunkSize) - 1,
            _bufferedChunkSize, _bufferedChunkSize, _bufferedChunkSize);
    }

    private static bool IsUniformChunk(int[] chunk, int length)
    {
        int first = chunk[0];
        for (int i = 1; i < length; i++)
            if (chunk[i] != first) return false;
        return true;
    }

    public bool IsTransparentForLight(int blockId)
    {
        BlockType b = _blockRegistry.BlockTypes[blockId];
        return b.DrawType is not DrawType.Solid and not DrawType.ClosedDoor;
    }

    private static VerticesIndicesToLoad CloneVerticesIndicesToLoad(VerticesIndicesToLoad source)
        => new()
        {
            ModelData = CloneModelData(source.ModelData),
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            PositionZ = source.PositionZ,
            Texture = source.Texture,
            Transparent = source.Transparent,
        };

    private static GeometryModel CloneModelData(GeometryModel source)
    {
        GeometryModel dest = new()
        {
            Xyz = ArrayPool<float>.Shared.Rent(source.XyzCount),
            Uv = ArrayPool<float>.Shared.Rent(source.UvCount),
            Rgba = ArrayPool<byte>.Shared.Rent(source.RgbaCount),
            Indices = ArrayPool<int>.Shared.Rent(source.IndicesCount)
        };

        source.Xyz.AsSpan(0, source.XyzCount).CopyTo(dest.Xyz);
        source.Uv.AsSpan(0, source.UvCount).CopyTo(dest.Uv);
        source.Rgba.AsSpan(0, source.RgbaCount).CopyTo(dest.Rgba);
        source.Indices.AsSpan(0, source.IndicesCount).CopyTo(dest.Indices);

        dest.VerticesCount = source.VerticesCount;
        dest.IndicesCount = source.IndicesCount;
        return dest;
    }
}

/// <summary>
/// Carries tessellated geometry for one chunk from a worker thread
/// </summary>
public readonly record struct TerrainRendererRedraw(
    Chunk Chunk,
    VerticesIndicesToLoad[] Data,
    int DataCount,
    bool DataRented);