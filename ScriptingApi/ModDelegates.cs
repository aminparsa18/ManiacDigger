namespace ManicDigger;

public class ModDelegates
{
    public delegate void BlockBuild(int player, int x, int y, int z);
    public delegate void BlockDelete(int player, int x, int y, int z, int oldblock);
    public delegate void BlockUse(int player, int x, int y, int z);
    public delegate void BlockUseWithTool(int player, int x, int y, int z, int tool);
    public delegate void BlockUpdate(int x, int y, int z);
    public delegate void WorldGenerator(int x, int y, int z, ushort[] chunk);
    public delegate void PopulateChunk(int x, int y, int z);
    public delegate bool Command(int player, string command, string argument);
    public delegate void PlayerJoin(int player);
    public delegate void PlayerLeave(int player);
    public delegate void PlayerDisconnect(int player);
    public delegate string PlayerChat(int player, string message, bool toteam);
    public delegate void PlayerDeath(int player, DeathReason reason, int sourceID);
    public delegate void DialogClick(int player, string widgetId);
    public delegate void WeaponHit(int sourcePlayer, int targetPlayer, int block, bool headshot);
    public delegate void WeaponShot(int sourceplayer, int block);
    public delegate void SpecialKey1(int player, SpecialKey key);
    public delegate void ChangedActiveMaterialSlot(int player);
    public delegate void LoadWorld();
    public delegate void UpdateEntity(int chunkx, int chunky, int chunkz, int id);
    public delegate void UseEntity(int player, int chunkx, int chunky, int chunkz, int id);
    public delegate void HitEntity(int player, int chunkx, int chunky, int chunkz, int id);
    public delegate bool CheckBlockUse(int player, int x, int y, int z);
    public delegate bool CheckBlockBuild(int player, int x, int y, int z);
    public delegate bool CheckBlockDelete(int player, int x, int y, int z);

    public delegate void DialogClick2(DialogClickArgs args);
    public delegate void Permission(PermissionArgs args);
}