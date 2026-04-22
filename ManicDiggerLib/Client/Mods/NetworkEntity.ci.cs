/// <summary>
/// Client mod that routes entity lifecycle and position packets to their handlers
/// and drives networked-entity interpolation each frame.
/// </summary>
public class ModNetworkEntity : ModBase
{
    private readonly ClientPacketHandlerEntitySpawn _spawn;
    private readonly ClientPacketHandlerEntityPosition _position;
    private readonly ClientPacketHandlerEntityDespawn _despawn;

    /// <summary>True once the three packet handlers have been registered.</summary>
    private bool _handlersRegistered;

    public ModNetworkEntity()
    {
        _spawn = new ClientPacketHandlerEntitySpawn();
        _position = new ClientPacketHandlerEntityPosition();
        _despawn = new ClientPacketHandlerEntityDespawn();
    }

    public override void OnNewFrame(Game game, float args)
    {
        // Register once — previously wrote to the handler dictionary every frame.
        if (!_handlersRegistered)
        {
            game.packetHandlers[Packet_ServerIdEnum.EntitySpawn] = _spawn;
            game.packetHandlers[Packet_ServerIdEnum.EntityPosition] = _position;
            game.packetHandlers[Packet_ServerIdEnum.EntityDespawn] = _despawn;
            _handlersRegistered = true;
        }
    }
}