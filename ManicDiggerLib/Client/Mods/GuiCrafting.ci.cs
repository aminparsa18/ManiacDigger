using OpenTK.Mathematics;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles the crafting table UI — drawing available recipes, mouse selection, and sending craft packets.
/// </summary>
public class ModGuiCrafting : ModBase
{
    private const int RecipeRowHeight = 80;
    private const int MenuWidth = 600;
    private const int OutputColumnOffset = 400;
    private const int FontSize = 12;

    private readonly PacketHandlerCraftingRecipes handler;

    private int[] currentRecipes;
    private int currentRecipesCount;
    private int craftingTablePosX, craftingTablePosY, craftingTablePosZ;
    private Packet_CraftingRecipe[] craftingRecipes2;
    private int craftingRecipes2Count;
    private int[] craftingBlocks;
    private int craftingBlocksCount;
    private int craftingSelectedRecipe;

    internal Packet_CraftingRecipe[] d_CraftingRecipes;
    internal int d_CraftingRecipesCount;
    internal CraftingTableTool d_CraftingTableTool;

    public ModGuiCrafting()
    {
        handler = new PacketHandlerCraftingRecipes { mod = this };
    }

    public override void OnNewFrameDraw2d(Game game, float deltaTime)
    {
        d_CraftingTableTool ??= new CraftingTableTool
        {
            d_Map = MapStorage2.Create(game),
            d_Data = game.d_Data
        };

        game.packetHandlers[Packet_ServerIdEnum.CraftingRecipes] = handler;

        if (game.guistate == GuiState.CraftingRecipes)
            DrawCraftingRecipes(game);
    }

    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        if (game.guistate == GuiState.CraftingRecipes)
            CraftingMouse(game);
    }

    public override void OnKeyDown(Game game, KeyEventArgs args)
    {
        if (args.GetKeyCode() != game.GetKey(Keys.E) || game.GuiTyping != TypingState.None) return;
        if (game.SelectedBlockPositionX == -1 && game.SelectedBlockPositionY == -1 && game.SelectedBlockPositionZ == -1) return;

        int posX = game.SelectedBlockPositionX;
        int posY = game.SelectedBlockPositionZ;
        int posZ = game.SelectedBlockPositionY;

        if (game.map.GetBlock(posX, posY, posZ) != game.d_Data.BlockIdCraftingTable()) return;

        Vector3i[] table = d_CraftingTableTool.GetTable(posX, posY, posZ, out int tableCount);
        int[] onTable = d_CraftingTableTool.GetOnTable(table, tableCount, out int onTableCount);
        CraftingRecipesStart(game, d_CraftingRecipes, d_CraftingRecipesCount, onTable, onTableCount, posX, posY, posZ);
        args.SetHandled(true);
    }

    internal void DrawCraftingRecipes(Game game)
    {
        // Filter recipes the player has materials for
        currentRecipes = new int[1024];
        currentRecipesCount = 0;

        for (int i = 0; i < craftingRecipes2Count; i++)
        {
            Packet_CraftingRecipe r = craftingRecipes2[i];
            if (r == null) continue;

            bool canCraft = true;
            for (int k = 0; k < r.IngredientsCount; k++)
            {
                Packet_Ingredient ing = r.Ingredients[k];
                if (ing == null) continue;
                if (CountBlock(craftingBlocks, craftingBlocksCount, ing.Type) < ing.Amount)
                {
                    canCraft = false;
                    break;
                }
            }
            if (canCraft)
                currentRecipes[currentRecipesCount++] = i;
        }

        if (currentRecipesCount == 0)
        {
            game.Draw2dText1(game.language.NoMaterialsForCrafting(), game.Xcenter(200), game.Ycenter(20), FontSize, null, false);
            return;
        }

        int menuX = game.Xcenter(MenuWidth);
        int menuY = game.Ycenter(currentRecipesCount * RecipeRowHeight);

        for (int i = 0; i < currentRecipesCount; i++)
        {
            Packet_CraftingRecipe r = craftingRecipes2[currentRecipes[i]];
            int rowY = menuY + i * RecipeRowHeight;
            int color = i == craftingSelectedRecipe
                ? Game.ColorFromArgb(255, 255, 0, 0)
                : Game.ColorFromArgb(255, 255, 255, 255);
            int white = Game.ColorFromArgb(255, 255, 255, 255);

            for (int ii = 0; ii < r.IngredientsCount; ii++)
            {
                Packet_Ingredient ing = r.Ingredients[ii];
                int colX = menuX + 20 + ii * 130;
                game.Draw2dTexture(game.d_TerrainTextures.TerrainTexture, colX, rowY, 32, 32, game.TextureIdForInventory[ing.Type], Game.texturesPacked(), white, false);
                game.Draw2dText1($"{ing.Amount} {game.blocktypes[ing.Type].Name}", colX + 50, rowY, FontSize, color, false);
            }

            int outX = menuX + 20 + OutputColumnOffset;
            game.Draw2dTexture(game.d_TerrainTextures.TerrainTexture, outX, rowY, 32, 32, game.TextureIdForInventory[r.Output.Type], Game.texturesPacked(), white, false);
            game.Draw2dText1($"{r.Output.Amount} {game.blocktypes[r.Output.Type].Name}", outX + 50, rowY, FontSize, color, false);
        }
    }

    internal void CraftingMouse(Game game)
    {
        if (currentRecipes == null) return;

        int menuX = game.Xcenter(MenuWidth);
        int menuY = game.Ycenter(currentRecipesCount * RecipeRowHeight);

        if (game.mouseCurrentY >= menuY && game.mouseCurrentY < menuY + currentRecipesCount * RecipeRowHeight)
            craftingSelectedRecipe = (game.mouseCurrentY - menuY) / RecipeRowHeight;

        if (!game.mouseleftclick) return;

        if (currentRecipesCount != 0)
            game.SendPacketClient(ClientPackets.Craft(craftingTablePosX, craftingTablePosY, craftingTablePosZ, currentRecipes[craftingSelectedRecipe]));

        game.mouseleftclick = false;
        game.GuiStateBackToGame();
    }

    internal void CraftingRecipesStart(Game game, Packet_CraftingRecipe[] recipes, int recipesCount, int[] blocks, int blocksCount, int posX, int posY, int posZ)
    {
        craftingRecipes2 = recipes;
        craftingRecipes2Count = recipesCount;
        craftingBlocks = blocks;
        craftingBlocksCount = blocksCount;
        craftingTablePosX = posX;
        craftingTablePosY = posY;
        craftingTablePosZ = posZ;
        game.guistate = GuiState.CraftingRecipes;
        game.menustate = new MenuState();
        game.SetFreeMouse(true);
    }

    /// <summary>Counts how many times a block type appears in the crafting block list.</summary>
    private static int CountBlock(int[] blocks, int count, int type)
    {
        int total = 0;
        for (int i = 0; i < count; i++)
            if (blocks[i] == type) total++;
        return total;
    }
}

public class PacketHandlerCraftingRecipes : ClientPacketHandler
{
    internal ModGuiCrafting mod;
    public override void Handle(Game game, Packet_Server packet)
    {
        mod.d_CraftingRecipes = packet.CraftingRecipes.CraftingRecipes;
        mod.d_CraftingRecipesCount = packet.CraftingRecipes.CraftingRecipesCount;
    }
}

public class CraftingTableTool
{
    internal IMapStorage2 d_Map;
    internal GameData d_Data;
    public int[] GetOnTable(Vector3i[] table, int tableCount, out int retCount)
    {
        int[] ontable = new int[2048];
        int ontableCount = 0;
        for (int i = 0; i < tableCount; i++)
        {
            Vector3i v = table[i];
            int t = d_Map.GetBlock(v.X, v.Y, v.Z + 1);
            ontable[ontableCount++] = t;
        }
        retCount = ontableCount;
        return ontable;
    }

    private const int maxcraftingtablesize = 2000;
    public Vector3i[] GetTable(int posx, int posy, int posz, out int retCount)
    {
        Vector3i[] l = new Vector3i[2048];
        int lCount = 0;
        Vector3i[] todo = new Vector3i[2048];
        int todoCount = 0;
        todo[todoCount++] = new Vector3i(posx, posy, posz);
        for (; ; )
        {
            if (todoCount == 0 || lCount >= maxcraftingtablesize)
            {
                break;
            }
            Vector3i p = todo[todoCount - 1];
            todoCount--;
            if (Vector3IntRefArrayContains(l, lCount, p))
            {
                continue;
            }
            l[lCount++] = p;
            Vector3i a = new(p.X + 1, p.Y, p.Z);
            if (d_Map.GetBlock(a.X, a.Y, a.Z) == d_Data.BlockIdCraftingTable())
            {
                todo[todoCount++] = a;
            }
            Vector3i b = new(p.X - 1, p.Y, p.Z);
            if (d_Map.GetBlock(b.X, b.Y, b.Z) == d_Data.BlockIdCraftingTable())
            {
                todo[todoCount++] = b;
            }
            Vector3i c = new(p.X, p.Y + 1, p.Z);
            if (d_Map.GetBlock(c.X, c.Y, c.Z) == d_Data.BlockIdCraftingTable())
            {
                todo[todoCount++] = c;
            }
            Vector3i d = new(p.X, p.Y - 1, p.Z);
            if (d_Map.GetBlock(d.X, d.Y, d.Z) == d_Data.BlockIdCraftingTable())
            {
                todo[todoCount++] = d;
            }
        }
        retCount = lCount;
        return l;
    }

    private static bool Vector3IntRefArrayContains(Vector3i[] l, int lCount, Vector3i p)
    {
        for (int i = 0; i < lCount; i++)
        {
            if (l[i].X == p.X
                && l[i].Y == p.Y
                && l[i].Z == p.Z)
            {
                return true;
            }
        }
        return false;
    }
}
