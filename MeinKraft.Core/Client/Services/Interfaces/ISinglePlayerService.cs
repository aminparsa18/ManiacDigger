/// <summary>
/// Manages the lifecycle of the embedded single-player server that runs
/// in-process alongside the game client.
/// </summary>
public interface ISinglePlayerService
{
    /// <summary>
    /// <see langword="true"/> when the single-player server binary or subsystem
    /// is present and can be started. Set to <see langword="false"/> to suppress
    /// the single-player option in the UI.
    /// </summary>
    bool SinglePlayerServerAvailable { get; set; }

    /// <summary>
    /// <see langword="true"/> while the server is running and has finished
    /// its initial world load and is ready to accept a client connection.
    /// </summary>
    bool SinglePlayerServerLoaded { get; set; }

    /// <summary>
    /// Set to <see langword="true"/> to signal the server thread to shut down.
    /// Reset to <see langword="false"/> by <see cref="SinglePlayerServerStart"/>
    /// at the beginning of each session.
    /// </summary>
    bool SinglePlayerServerExit { get; set; }

    /// <summary>
    /// The in-process network channel used by the client to communicate with
    /// the single-player server without a real socket.
    /// </summary>
    IDummyNetwork SinglePlayerServerNetwork { get; set; }

    /// <summary>
    /// Prepares and starts the single-player server for the given
    /// <paramref name="saveFilename"/>. Resets <see cref="SinglePlayerServerExit"/>
    /// to <see langword="false"/> before launch.
    /// </summary>
    /// <param name="saveFilename">
    /// The save file the server should load or create.
    /// </param>
    void SinglePlayerServerStart(string saveFilename);
}