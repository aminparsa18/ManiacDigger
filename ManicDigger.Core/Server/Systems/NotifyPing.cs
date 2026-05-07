using ManicDigger;

/// <summary>
/// Sends a ping packet to every connected client once per second and disconnects
/// any client that fails to respond within the timeout window.
/// Half-dropped connections (where the TCP socket appears open but the client is
/// unresponsive) are detected this way.
/// </summary>
public class ServerSystemNotifyPing : ServerSystem
{
    private readonly ServerTimer pingTimer = new()
    {
        Interval = TimeSpan.FromSeconds(1),
        MaxDeltaTime = TimeSpan.FromSeconds(5),
    };
    private readonly IGameService gameService;
    private readonly IGameExitService gameExit;
    private readonly IClientRegistry _serverClientService;
    private readonly IServerPacketService _serverPacketService;

    public ServerSystemNotifyPing(IGameService gameService, IGameExitService gameExit, IModEvents modEvents, IClientRegistry serverClientService, 
        IServerPacketService serverPacketService) : base(modEvents)
    {
        this.gameService = gameService;
        this.gameExit = gameExit;
        _serverClientService = serverClientService;
        _serverPacketService = serverPacketService;
    }

    /// <inheritdoc/>
    protected override void OnUpdate(Server server, float dt)
    {
        pingTimer.Update(() =>
        {
            if (gameExit.Exit)
            {
                return;
            }

            List<int> timedOut = new();

            foreach ((int clientId, ServerPlayer? client) in _serverClientService.Clients)
            {
                if (!client.Ping.Send(gameService.TimeMillisecondsFromStart))
                {
                    if (client.Ping.CheckTimeout(gameService.TimeMillisecondsFromStart))
                    {
                        Console.WriteLine($"{clientId}: ping timeout. Disconnecting...");
                        timedOut.Add(clientId);
                    }
                }
                else
                {
                    _serverPacketService.SendPacket(clientId, ServerPackets.Ping());
                }
            }

            foreach (int clientId in timedOut)
            {
                server.KillPlayer(clientId);
            }
        });
    }
}