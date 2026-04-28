using ManicDigger;

public class ModEventHandlers
{
    public List<ModDelegates.WorldGenerator> getchunk { get; set; } = [];
    public List<ModDelegates.BlockUse> onuse = [];
    public List<ModDelegates.BlockBuild> onbuild = [];
    public List<ModDelegates.BlockDelete> ondelete = [];
    public List<ModDelegates.BlockUseWithTool> onusewithtool = [];
    public List<ModDelegates.ChangedActiveMaterialSlot> changedactivematerialslot = [];
    public List<ModDelegates.BlockUpdate> blockticks = [];
    public List<ModDelegates.PopulateChunk> populatechunk { get; set; } = [];
    public List<ModDelegates.Command> oncommand = [];
    public List<ModDelegates.WeaponShot> onweaponshot = [];
    public List<ModDelegates.WeaponHit> onweaponhit = [];
    public List<ModDelegates.SpecialKey1> onspecialkey = [];
    public List<ModDelegates.PlayerJoin> onplayerjoin = [];
    public List<ModDelegates.PlayerLeave> onplayerleave = [];
    public List<ModDelegates.PlayerDisconnect> onplayerdisconnect = [];
    public List<ModDelegates.PlayerChat> onplayerchat = [];
    public List<ModDelegates.PlayerDeath> onplayerdeath = [];
    public List<ModDelegates.DialogClick> ondialogclick = [];
    public List<ModDelegates.DialogClick2> ondialogclick2 = [];
    public List<ModDelegates.LoadWorld> onloadworld = [];
    public List<ModDelegates.UpdateEntity> onupdateentity = [];
    public List<ModDelegates.UseEntity> onuseentity = [];
    public List<ModDelegates.HitEntity> onhitentity = [];
    public List<ModDelegates.Permission> onpermission = [];
    public List<ModDelegates.CheckBlockUse> checkonuse = [];
    public List<ModDelegates.CheckBlockBuild> checkonbuild = [];
    public List<ModDelegates.CheckBlockDelete> checkondelete = [];
}
