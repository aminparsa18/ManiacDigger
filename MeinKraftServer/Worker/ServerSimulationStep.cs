namespace MeinKraft.Worker;

public sealed class ServerSimulationStep : ISimulationStep
{
    private readonly IServer _server;
    private readonly ServerLifetime _lifetime;
    private bool _isConfigLoaded;

    public ServerSimulationStep(
        IServer server,
        ServerLifetime lifetime,
        ServerSystemBootstraper serverSystemBootstraper)
    {
        _server = server;
        _lifetime = lifetime;
        _server.Systems = serverSystemBootstraper.Systems;
    }

    public void Tick(float dt)
    {
        // Exit — same logic as before, now cleanly isolated in one place.
        //if (_gameExit.Exit)
        //{
        //    _server.Stop();
        //    _gameExit.Exit = false;
        //    _lifetime.SignalStop();  // cancels the CancellationToken → SimulationLoop stops
        //    return;
        //}

        //// SinglePlayerServerExit — same as before.
        //if (_singlePlayerService.SinglePlayerServerExit)
        //{
        //    _server.Exit();
        //    _singlePlayerService.SinglePlayerServerExit = false;
        //    return;
        //}

        if (!_isConfigLoaded)
        {
            _isConfigLoaded = true;
            _server.OnConfigLoaded();
        }

        _server.Process(dt);
    }
}