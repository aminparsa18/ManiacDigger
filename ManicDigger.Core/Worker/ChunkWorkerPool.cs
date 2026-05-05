using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ManicDigger.Worker;

/// <summary>
/// Reads from <see cref="IChunkWorkQueue"/> using a configurable number of parallel
/// workers — one <see cref="Task"/> per worker, each running on the thread pool.
/// Replacing <c>Thread.Sleep(1)</c> poll loops with proper channel back-pressure.
/// </summary>
public sealed class ChunkWorkerPool : BackgroundService, IChunkWorkQueue
{
    // How many parallel workers process chunk jobs.
    // Default: leave one logical core free for the main/render thread.
    public static int DefaultWorkerCount =>
        Math.Max(1, Environment.ProcessorCount - 1);

    private readonly ConcurrentQueue<TerrainRendererRedraw> _results = new();
    private readonly Channel<ChunkWorkItem> _channel;
    private readonly IChunkWorkDispatcher _dispatcher;
    private readonly ILogger<ChunkWorkerPool> _logger;
    private readonly int _workerCount;

    // Exposed so callers can monitor queue depth (HUD, diagnostics).
    public int PendingCount => _channel.Reader.Count;

    public ChunkWorkerPool(
        IChunkWorkDispatcher dispatcher,
        ILogger<ChunkWorkerPool> logger,
        int workerCount = 0,            // 0 = use DefaultWorkerCount
        int channelCapacity = 512)
    {
        _dispatcher = dispatcher;
        _logger = logger;
        _workerCount = workerCount > 0 ? workerCount : DefaultWorkerCount;

        _channel = Channel.CreateBounded<ChunkWorkItem>(new BoundedChannelOptions(channelCapacity)
        {
            SingleReader = false,
            SingleWriter = false,
            // Drop the oldest pending item when the queue overflows rather than blocking
            // the game loop. Distant dirty chunks will simply be re-marked and retried.
            FullMode = BoundedChannelFullMode.DropOldest,
        });

        _logger.LogInformation(
            "ChunkWorkerPool: {Workers} workers, channel capacity {Capacity}",
            _workerCount, channelCapacity);
    }

    // IChunkWorkQueue ──────────────────────────────────────────────────────────

    public ValueTask EnqueueAsync(ChunkWorkItem item, CancellationToken ct = default)
        => _channel.Writer.WriteAsync(item, ct);

    public void EnqueueResult(TerrainRendererRedraw redraw)
    => _results.Enqueue(redraw);

    public bool TryDequeueResult(out TerrainRendererRedraw redraw)
        => _results.TryDequeue(out redraw);

    // BackgroundService ────────────────────────────────────────────────────────

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Spin up N independent worker tasks. Each races to read from the shared
        // channel — no manual locking needed because Channel is already thread-safe.
        Task[] workers = new Task[_workerCount];
        for (int i = 0; i < _workerCount; i++)
        {
            int workerId = i;
            workers[i] = Task.Run(() => WorkerLoopAsync(workerId, stoppingToken), stoppingToken);
        }

        // Complete the channel writer when the service is stopping so all workers
        // drain gracefully instead of being hard-cancelled mid-item.
        stoppingToken.Register(() => _channel.Writer.TryComplete());

        return Task.WhenAll(workers);
    }

    private async Task WorkerLoopAsync(int workerId, CancellationToken ct)
    {
        _logger.LogDebug("Chunk worker {Id} started", workerId);

        await foreach (ChunkWorkItem item in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await _dispatcher.DispatchAsync(item, ct);
                item.Completion?.TrySetResult();
            }
            catch (OperationCanceledException)
            {
                item.Completion?.TrySetCanceled(ct);
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Chunk worker {Id} failed on {Type} ({X},{Y},{Z})",
                    workerId, item.Type, item.ChunkX, item.ChunkY, item.ChunkZ);

                item.Completion?.TrySetException(ex);
                // Worker continues — one bad chunk should not kill the pool.
            }
        }

        _logger.LogDebug("Chunk worker {Id} stopped", workerId);
    }
}