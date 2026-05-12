namespace MeinKraft.Worker;

public sealed class ServerSimulationStep : ISimulationStep
{
    private readonly Server _server;
    private readonly IGameExitService _gameExit;                  
    private readonly ISinglePlayerService _singlePlayerService;
    private readonly ServerLifetime _lifetime;

    public ServerSimulationStep(
        Server server,
        IGameExitService gameExit,
        ISinglePlayerService singlePlayerService,
        ServerLifetime lifetime)
    {
        _server = server;
        _gameExit = gameExit;
        _singlePlayerService = singlePlayerService;
        _lifetime = lifetime;
    }

    public void Tick(float dt)
    {
        // Exit — same logic as before, now cleanly isolated in one place.
        if (_gameExit.Exit)
        {
            _server.Stop();
            _gameExit.Exit = false;
            _lifetime.SignalStop();  // cancels the CancellationToken → SimulationLoop stops
            return;
        }

        // SinglePlayerServerExit — same as before.
        if (_singlePlayerService.SinglePlayerServerExit)
        {
            _server.Exit();
            _singlePlayerService.SinglePlayerServerExit = false;
            return;
        }

        _server.Process(dt);
    }
}