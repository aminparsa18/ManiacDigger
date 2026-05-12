/// <inheritdoc/>
public sealed class SinglePlayerService(IDummyNetwork dummyNetwork) : ISinglePlayerService
{
    /// <inheritdoc/>
    public bool SinglePlayerServerAvailable { get; set; } = true;

    /// <inheritdoc/>
    public bool SinglePlayerServerLoaded { get; set; }

    /// <inheritdoc/>
    public bool SinglePlayerServerExit { get; set; }

    /// <inheritdoc/>
    public IDummyNetwork SinglePlayerServerNetwork { get; set; } = dummyNetwork;

    /// <inheritdoc/>
    public void SinglePlayerServerStart(string saveFilename) => SinglePlayerServerExit = false;
}