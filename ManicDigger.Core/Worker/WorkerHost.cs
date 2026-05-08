using ManicDigger.Extensions;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ManicDigger.Worker;

/// <summary>
/// Manages the lifetime of all background workers for a single game session.
/// Call StartAsync from Connect(), StopAsync on exit.
/// </summary>
public sealed class WorkerHost : IAsyncDisposable
{
    private readonly SimulationLoop _simulationLoop;
    private readonly ChunkWorkerPool _tessellationPool;
    private readonly ChunkLightingPool _lightingPool;
    private readonly PeriodicTaskScheduler _periodicScheduler;
    private readonly ISinglePlayerService _singlePlayerService;
    private readonly ServerLifetime _lifetime;
    private readonly IGameLogger _logger;

    private Task? _allWorkers;
    private bool _started;

    public WorkerHost(
        SimulationLoop simulationLoop,
        ChunkWorkerPool tessellationPool,
        ChunkLightingPool lightingPool,
        PeriodicTaskScheduler periodicScheduler,
        ISinglePlayerService singlePlayerService,
        ServerLifetime lifetime,
        IGameLogger logger)
    {
        _simulationLoop = simulationLoop;
        _tessellationPool = tessellationPool;
        _lightingPool = lightingPool;
        _periodicScheduler = periodicScheduler;
        _singlePlayerService = singlePlayerService;
        _lifetime = lifetime;
        _logger = logger;
    }

    public async Task StartAsync()
    {
        if (_started)
        {
            _logger.Client.Warning("WorkerHost.StartAsync called twice — ignoring");
            return;
        }

        _started = true;
        CancellationToken ct = _lifetime.Token;

        _logger.Client.Information("WorkerHost: starting workers");
 
        await _simulationLoop.StartAsync(ct);
        await _tessellationPool.StartAsync(ct);
        await _lightingPool.StartAsync(ct);       // single-worker lighting stage
        await _periodicScheduler.StartAsync(ct);

        _allWorkers = Task.WhenAll(
            _simulationLoop.ExecuteTask ?? Task.CompletedTask,
            _tessellationPool.ExecuteTask ?? Task.CompletedTask,
            _lightingPool.ExecuteTask ?? Task.CompletedTask,
            _periodicScheduler.ExecuteTask ?? Task.CompletedTask);

        _singlePlayerService.SinglePlayerServerLoaded = true;

        _logger.Client.Information("WorkerHost: all workers running");
    }

    public async Task StopAsync()
    {
        if (!_started) return;

        _logger.Client.Information("WorkerHost: stopping workers");

        _lifetime.SignalStop();

        // Stop lighting first — it feeds the tessellation queue.
        // Stopping in reverse pipeline order ensures no new tessellation
        // items are enqueued after the tessellation pool has shut down.
        await _lightingPool.StopAsync(CancellationToken.None);
        await _tessellationPool.StopAsync(CancellationToken.None);
        await _simulationLoop.StopAsync(CancellationToken.None);
        await _periodicScheduler.StopAsync(CancellationToken.None);

        if (_allWorkers is not null)
        {
            try { await _allWorkers; }
            catch (OperationCanceledException) { }
        }

        _started = false;
        _singlePlayerService.SinglePlayerServerLoaded = false;

        _logger.Client.Information("WorkerHost: all workers stopped");
    }

    public async ValueTask DisposeAsync() => await StopAsync();
}