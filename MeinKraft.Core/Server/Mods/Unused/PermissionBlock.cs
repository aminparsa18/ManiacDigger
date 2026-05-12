namespace MeinKraft.Mods;

public class PermissionBlock : IMod
{
    private IServerModManager? m;

    public int PermissionLevelsCount = 4;
    public int AreaSize = 64;

    public void PreStart(IServerModManager m) => m.RequireMod("CoreBlocks");

    public void Start(IServerModManager manager, IModEvents modEvents)
    {
        m = manager;

        for (int i = 0; i < PermissionLevelsCount; i++)
        {
            m.SetBlockType(241 + i, "BuildPermission" + i, new BlockType()
            {
                AllTextures = "BuildPermission" + i,
                DrawType = DrawType.Solid,
                WalkableType = WalkableType.Solid,
                IsBuildable = true,
            });
            m.AddToCreativeInventory("BuildPermission" + i);
        }

        modEvents.BlockBuild += OnBuild;
        modEvents.BlockDelete += OnDelete;
    }

    private void OnBuild(BlockBuildArgs args)
    {
        int permissionblock = m.GetBlockId("BuildPermission0");

        //can't build any block in column
        for (int zz = 0; zz < m.GetMapSizeZ(); zz++)
        {
            if (zz == args.Z)
                continue;

            for (int i = 0; i < PermissionLevelsCount; i++)
            {
                if (m.GetBlock(args.X, args.Y, zz) == permissionblock + i)
                {
                    m.SetBlock(args.X, args.Y, args.Z, 0);
                    m.SendMessage(args.Player, "You can't build in a column that contains permission block.");
                    return;
                }
            }
        }

        //add area
        for (int i = 0; i < PermissionLevelsCount; i++)
        {
            if (m.GetBlock(args.X, args.Y, args.Z) == permissionblock + i)
            {
                if (m.GetPlayerPermissionLevel(args.Player) <= i)
                {
                    m.SendMessage(args.Player, "No permission");
                    m.SetBlock(args.X, args.Y, args.Z, 0);
                    return;
                }

                m.AddPermissionArea(args.X - AreaSize, args.Y - AreaSize, 0, args.X + AreaSize, args.Y + AreaSize, m.GetMapSizeZ(), i);
            }
        }
    }

    private void OnDelete(BlockDeleteArgs args)
    {
        int permissionblock = m.GetBlockId("BuildPermission0");

        //remove area
        for (int i = 0; i < PermissionLevelsCount; i++)
        {
            if (args.OldBlock == permissionblock + i)
            {
                if (m.GetPlayerPermissionLevel(args.Player) <= i)
                {
                    m.SendMessage(args.Player, "No permission");
                    m.SetBlock(args.X, args.Y, args.Z, args.OldBlock);
                    return;
                }

                m.RemovePermissionArea(args.X - AreaSize, args.Y - AreaSize, 0, args.X + AreaSize, args.Y + AreaSize, m.GetMapSizeZ());
            }
        }
    }
}