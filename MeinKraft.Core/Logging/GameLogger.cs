using Serilog;

/// <summary>
/// Provides two pre-configured Serilog loggers — one for the client process
/// and one for the server process — that route to their respective hourly
/// rolling log files while still flowing through the shared Serilog pipeline.
///
/// Inject this where you need explicit channel control. For everything else,
/// inject the standard <c>Microsoft.Extensions.Logging.ILogger&lt;T&gt;</c>
/// which also flows through Serilog automatically.
///
/// Usage:
/// <code>
///   public class ModDrawTerrain(IGameLogger log)
///   {
///       void SomeMethod() => log.Client.Information("Chunk rebuilt at {X} {Y} {Z}", x, y, z);
///   }
/// </code>
/// </summary>
public interface IGameLogger
{
    /// <summary>Logger that writes to the client channel (client-*.log).</summary>
    ILogger Client { get; }

    /// <summary>Logger that writes to the server channel (server-*.log).</summary>
    ILogger Server { get; }
}

/// <inheritdoc cref="IGameLogger"/>
public sealed class GameLogger : IGameLogger
{
    // Both loggers are thin ForContext wrappers around the shared root logger.
    // They enrich every event with the GameChannel property that LoggingSetup's
    // sub-logger filters use to route entries to the correct file.
    public ILogger Client { get; } =
        Log.Logger.ForContext(LoggingSetup.ChannelProperty, LoggingSetup.ClientChannel);

    public ILogger Server { get; } =
        Log.Logger.ForContext(LoggingSetup.ChannelProperty, LoggingSetup.ServerChannel);
}