/// <summary>
/// Client mod that routes entity lifecycle and position packets to their handlers
/// and drives networked-entity interpolation each frame.
/// </summary>
public class ModNetworkEntity : ModBase
{
    private readonly ClientPacketHandlerEntitySpawn _spawn;
    private readonly ClientPacketHandlerEntityPosition _position;
    private readonly ClientPacketHandlerEntityDespawn _despawn;

    public ModNetworkEntity(
        IGameWindowService gameService,
        IVoxelMap voxelMap,
        IGame game,
        IBlockRegistry blockTypeRegistry) : base(game)
    {
        _spawn = new ClientPacketHandlerEntitySpawn(gameService, voxelMap, blockTypeRegistry, game);
        _position = new ClientPacketHandlerEntityPosition(gameService, game);
        _despawn = new ClientPacketHandlerEntityDespawn(gameService, game);

        // Register immediately — EntitySpawn arrives before the first OnFrame tick.
        Game.PacketHandlers[(int)Packet_ServerIdEnum.EntitySpawn] = _spawn;
        Game.PacketHandlers[(int)Packet_ServerIdEnum.EntityPosition] = _position;
        Game.PacketHandlers[(int)Packet_ServerIdEnum.EntityDespawn] = _despawn;
    }

    public override void OnFrame(float dt) { }
}