namespace ManicDigger.Mods;

public class Sign : IMod
{
    private IModManager? m;

    public void PreStart(IModManager m)
    {
        m.RequireMod("CoreBlocks");
    }

    public void Start(IModManager manager)
    {
        m = manager;
        m.SetBlockType(154, "Sign", new BlockType()
        {
            AllTextures = "Sign",
            DrawType = DrawType.Solid,
            WalkableType = WalkableType.Solid,
            IsUsable = true,
            IsTool = true,
        });
        m.AddToCreativeInventory("Sign");
        m.SetBlockType(155, "PermissionSign", new BlockType()
        {
            AllTextures = "PermissionSign",
            DrawType = DrawType.Solid,
            WalkableType = WalkableType.Solid,
            IsUsable = true,
            IsTool = true,
        });
        m.AddToCreativeInventory("PermissionSign");
    }

}