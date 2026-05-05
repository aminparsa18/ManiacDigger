using ManicDigger.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ManicDigger.Extensions;

public static class WorkerInfrastructureExtensions
{
    /// <summary>
    /// Registers the simulation loop, chunk worker pool, periodic task scheduler,
    /// and <see cref="WorkerHost"/> as singletons. Nothing starts at registration time —
    /// <see cref="WorkerHost.StartAsync"/> is called manually from <c>Connect()</c>
    /// when the player enters the game screen.
    ///
    /// Usage:
    /// <code>
    /// services.AddWorkerInfrastructure()
    ///         .AddScheduledTask&lt;SaveGameTask&gt;()
    ///         .AddScheduledTask&lt;SeasonBroadcastTask&gt;()
    ///         .AddScheduledTask&lt;StatsResetTask&gt;();
    /// </code>
    /// </summary>
    public static IServiceCollection AddWorkerInfrastructure(
        this IServiceCollection services,
        int workerCount = 0,
        int chunkChannelCapacity = 512,
        TimeSpan simulationTickInterval = default)
    {
        // Chunk worker pool — registered as both its concrete type (so WorkerHost
        // can call StartAsync/StopAsync on it) and as IChunkWorkQueue (so anything
        // that enqueues work never takes a dependency on the concrete class).

        services.AddSingleton<ChunkTessellationDispatcher>()
        .AddSingleton<IChunkWorkDispatcher>(sp =>
            sp.GetRequiredService<ChunkTessellationDispatcher>())
        .AddSingleton(sp => new ChunkWorkerPool(
            sp.GetRequiredService<IChunkWorkDispatcher>(),
            sp.GetRequiredService<ILogger<ChunkWorkerPool>>(),
            workerCount,
            chunkChannelCapacity));
        services.AddSingleton<IChunkWorkQueue>(sp => sp.GetRequiredService<ChunkWorkerPool>());

        // Simulation loop.
        services.AddSingleton(sp => new SimulationLoop(
            sp.GetRequiredService<ISimulationStep>(),
            sp.GetRequiredService<ILogger<SimulationLoop>>(),
            simulationTickInterval));

        // Periodic task scheduler — collects all IScheduledTask registrations.
        services.AddSingleton<PeriodicTaskScheduler>();

        // WorkerHost owns the lifetime of all three above.
        // Resolved and started manually in Connect().
        services.AddSingleton<WorkerHost>();

        // ServerLifetime is the shared token — register first, everything else reads it.
        services.AddSingleton<ServerLifetime>();
        services.AddSingleton<ISimulationStep, ServerSimulationStep>();
        services.AddScheduledTask<ServerAutoRestartTask>();
        services.AddScheduledTask<SaveGameTask>();
        services.AddScheduledTask<SeasonBroadcastTask>();
        return services;
    }

    /// <summary>Registers a <see cref="IScheduledTask"/> implementation.</summary>
    public static IServiceCollection AddScheduledTask<T>(this IServiceCollection services)
        where T : class, IScheduledTask
    {
        services.AddSingleton<IScheduledTask, T>();
        return services;
    }
}

/// <summary>
/// Placeholder until chunk jobs are implemented.
/// Does nothing — chunk work is still handled by the existing background thread path.
/// </summary>
public sealed class NullChunkWorkDispatcher : IChunkWorkDispatcher
{
    public Task DispatchAsync(ChunkWorkItem item, CancellationToken ct)
        => Task.CompletedTask;
}