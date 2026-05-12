using MeinKraft.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace MeinKraft.Extensions;

public static class WorkerInfrastructureExtensions
{
    /// <summary>
    /// Registers the simulation loop, chunk worker pools (lighting + tessellation),
    /// periodic task scheduler, and WorkerHost as singletons.
    ///
    /// Lighting pool:      workerCount = max(1, ProcessorCount / 4)
    ///                     Safe with multiple workers now that Option B (BaseLight
    ///                     snapshot) is implemented in ChunkLightingDispatcher.
    /// Tessellation pool:  workerCount = N  — parallel geometry factory
    ///
    /// Usage:
    /// <code>
    /// services.AddWorkerInfrastructure()
    ///         .AddScheduledTask&lt;SaveGameTask&gt;()
    ///         .AddScheduledTask&lt;SeasonBroadcastTask&gt;();
    /// </code>
    /// </summary>
    public static IServiceCollection AddWorkerInfrastructure(
        this IServiceCollection services,
        int workerCount = 0,
        int chunkChannelCapacity = 512,
        TimeSpan simulationTickInterval = default)
    {
        // Registers IPublisher<T> / ISubscriber<T> for all event types.
        // Must come before any service that injects IPublisher or ISubscriber.
        services.AddMessagePipe();

        // ── Tessellation pool ─────────────────────────────────────────────────

        services.AddSingleton<ChunkTessellationDispatcher>();
        services.AddSingleton<IChunkWorkDispatcher>(sp =>
            sp.GetRequiredService<ChunkTessellationDispatcher>());

        services.AddSingleton(sp => new ChunkWorkerPool(
            sp.GetRequiredService<IChunkWorkDispatcher>(),
            sp.GetRequiredService<IGameLogger>(),
            workerCount,
            chunkChannelCapacity));

        services.AddSingleton<IChunkWorkQueue>(sp =>
            sp.GetRequiredService<ChunkWorkerPool>());

        // ── Lighting pool ─────────────────────────────────────────────────────
        // Option B (BaseLight snapshot) is implemented — the read/write race
        // between LightBetweenChunks.Input and LightBase is eliminated.
        // Scale to ProcessorCount / 4: lighting is heavier per-chunk than
        // tessellation so it needs fewer workers to saturate the tessellation queue.

        int lightingWorkerCount = 4;// Math.Max(1, Environment.ProcessorCount / 4);

        services.AddSingleton(sp => new ChunkLightingDispatcher(
            sp.GetRequiredService<IChunkWorkQueue>(),
            sp.GetRequiredService<IVoxelMap>(),
            sp.GetRequiredService<IBlockRegistry>()));

        services.AddSingleton(sp => new ChunkLightingPool(
            sp.GetRequiredService<ChunkLightingDispatcher>(),
            sp.GetRequiredService<IGameLogger>(),
            lightingWorkerCount,
            channelCapacity: lightingWorkerCount * 2));

        services.AddSingleton<ILightingWorkQueue>(sp =>
            sp.GetRequiredService<ChunkLightingPool>());

        // ── Simulation loop ───────────────────────────────────────────────────

        services.AddSingleton(sp => new SimulationLoop(
            sp.GetRequiredService<ISimulationStep>(),
            sp.GetRequiredService<ILogger<SimulationLoop>>(),
            simulationTickInterval));

        // ── Periodic scheduler ────────────────────────────────────────────────

        services.AddSingleton<PeriodicTaskScheduler>();

        // ── WorkerHost — started manually from Connect() ──────────────────────

        services.AddSingleton<WorkerHost>();

        services.AddSingleton<ServerLifetime>();
        services.AddSingleton<ISimulationStep, ServerSimulationStep>();
        services.AddScheduledTask<ServerAutoRestartTask>();
        services.AddScheduledTask<SaveGameTask>();
        services.AddScheduledTask<SeasonBroadcastTask>();

        return services;
    }

    public static IServiceCollection AddScheduledTask<T>(this IServiceCollection services)
        where T : class, IScheduledTask
    {
        services.AddSingleton<IScheduledTask, T>();
        return services;
    }
}

/// <summary>Enqueue a LightingChunkWorkItem here from ModDrawTerrain.</summary>
public interface ILightingWorkQueue : IChunkWorkQueue { }

/// <summary>
/// Single-worker ChunkWorkerPool for the lighting stage.
/// Implements ILightingWorkQueue so the DI container resolves it unambiguously
/// alongside the tessellation ChunkWorkerPool that implements IChunkWorkQueue.
/// </summary>
public sealed class ChunkLightingPool(
    IChunkWorkDispatcher dispatcher,
    IGameLogger logger,
    int workerCount,
    int channelCapacity)
    : ChunkWorkerPool(dispatcher, logger, workerCount, channelCapacity), ILightingWorkQueue;