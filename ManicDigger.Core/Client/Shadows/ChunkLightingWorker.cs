using ManicDigger.Worker;
using Microsoft.Extensions.Logging;
using System.Buffers;
using System.Threading.Channels;

namespace ManicDigger.Client.Shadows;

/// <summary>
/// Runs on a single dedicated thread. Processes lighting sequentially so
/// BaseLightDirty and rendered.Light are never touched concurrently.
/// After computing shadows it enqueues a TessellationChunkWorkItem for
/// the tessellation worker pool.
/// </summary>
public sealed class ChunkLightingWorker
{
    private const int BufferedChunkVolume = 18 * 18 * 18;

    private readonly ChannelReader<ChunkWorkItem> _input;   // fed by main thread
    private readonly IChunkWorkQueue _tessellationQueue;
    private readonly IVoxelMap _voxelMap;
    private readonly IBlockRegistry _blockRegistry;
    private readonly ILogger<ChunkLightingWorker> _logger;

    private readonly LightBase _lightBase = new();
    private readonly LightBetweenChunks _lightBetweenChunks = new();
    private readonly int[] _shadowLightRadius = new int[GlobalVar.MAX_BLOCKTYPES];
    private readonly bool[] _shadowIsTransparent = new bool[GlobalVar.MAX_BLOCKTYPES];
    private bool _blockTypeCacheDirty = true;

    public ChunkLightingWorker(
        ChannelReader<ChunkWorkItem> input,
        IChunkWorkQueue tessellationQueue,
        IVoxelMap voxelMap,
        IBlockRegistry blockRegistry,
        ILogger<ChunkLightingWorker> logger)
    {
        _input = input;
        _tessellationQueue = tessellationQueue;
        _voxelMap = voxelMap;
        _blockRegistry = blockRegistry;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogDebug("Lighting worker started");

        await foreach (ChunkWorkItem item in _input.ReadAllAsync(ct))
        {
            if (item is not LightingChunkWorkItem li) continue;

            try
            {
                byte[] snapshot = ComputeAndSnapshot(li.ChunkX, li.ChunkY, li.ChunkZ, li.Chunk);

                await _tessellationQueue.EnqueueAsync(new TessellationChunkWorkItem(
                    li.ChunkX, li.ChunkY, li.ChunkZ,
                    li.Chunk,
                    ShadowBuffer: snapshot,
                    ShadowBufferRented: true,
                    Completion: li.Completion));  // forward completion to tessellation
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lighting failed ({X},{Y},{Z})",
                    li.ChunkX, li.ChunkY, li.ChunkZ);
                li.Completion?.TrySetException(ex);
            }
        }

        _logger.LogDebug("Lighting worker stopped");
    }

    // ── shadow computation (identical logic, now safely single-threaded) ──────

    private byte[] ComputeAndSnapshot(int cx, int cy, int cz, Chunk target)
    {
        RefreshBlockTypeCache();

        bool anyBaseLightChanged = false;

        for (int xx = 0; xx < 3; xx++)
            for (int yy = 0; yy < 3; yy++)
                for (int zz = 0; zz < 3; zz++)
                {
                    int cx1 = cx + xx - 1, cy1 = cy + yy - 1, cz1 = cz + zz - 1;
                    if (!_voxelMap.IsValidChunkPos(cx1, cy1, cz1)) continue;

                    Chunk neighbour = _voxelMap.Chunks[VectorIndexUtil.Index3d(
                        cx1, cy1, cz1,
                        _voxelMap.Mapsizexchunks,
                        _voxelMap.Mapsizeychunks)];
                    if (neighbour == null) continue;

                    if (neighbour.BaseLightDirty)
                    {
                        _lightBase.CalculateChunkBaseLight(
                            cx1, cy1, cz1, _shadowLightRadius, _shadowIsTransparent);
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
            anyBaseLightChanged = true;
        }

        if (anyBaseLightChanged)
            _lightBetweenChunks.CalculateLightBetweenChunks(
                cx, cy, cz, _shadowLightRadius, _shadowIsTransparent);

        byte[] snapshot = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
        rendered.Light.AsSpan(0, BufferedChunkVolume)
                      .CopyTo(snapshot.AsSpan(0, BufferedChunkVolume));
        return snapshot;
    }

    private void RefreshBlockTypeCache()
    {
        if (!_blockTypeCacheDirty) return;
        foreach ((int id, BlockType blockType) in _blockRegistry.BlockTypes)
        {
            _shadowLightRadius[id] = blockType.LightRadius;
            _shadowIsTransparent[id] = IsTransparentForLight(id);
        }
        _blockTypeCacheDirty = false;
    }

    public void InvalidateBlockTypeCache() => _blockTypeCacheDirty = true;

    private bool IsTransparentForLight(int blockId)
    {
        BlockType b = _blockRegistry.BlockTypes[blockId];
        return b.DrawType is not DrawType.Solid and not DrawType.ClosedDoor;
    }
}

/// <summary>
/// Enqueued by the main thread when a chunk is dirty.
/// The single lighting thread converts this into a
/// <see cref="TessellationChunkWorkItem"/> after computing shadows.
/// </summary>
public record LightingChunkWorkItem(
    int ChunkX,
    int ChunkY,
    int ChunkZ,
    Chunk Chunk,
    TaskCompletionSource? Completion = null
) : ChunkWorkItem(ChunkX, ChunkY, ChunkZ, ChunkWorkType.RelightBase, Completion);