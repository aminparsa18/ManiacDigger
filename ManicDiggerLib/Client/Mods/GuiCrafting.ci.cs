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

    // ── Per-frame scratch (pre-allocated) ─────────────────────────────────────
    /// <summary>
    /// Indices into <see cref="craftingRecipes2"/> of recipes the player currently
    /// has materials for. Populated every frame by <see cref="DrawCraftingRecipes"/>.
    /// Pre-allocated to avoid a <c>new int[1024]</c> allocation on every draw call.
    /// </summary>
    private readonly int[] currentRecipes = new int[1024];
    private int currentRecipesCount;

    // ── Crafting session state ────────────────────────────────────────────────
    private int craftingTablePosX, craftingTablePosY, craftingTablePosZ;
    private Packet_CraftingRecipe[] craftingRecipes2;
    private int craftingRecipes2Count;
    private int craftingSelectedRecipe;

    /// <summary>
    /// Copy of the on-table block list for the current session.
    /// This is a private owned array — NOT a reference to CraftingTableTool's
    /// internal buffer — so that re-opening the crafting table cannot corrupt
    /// an active session's ingredient counts.
    /// </summary>
    private readonly int[] _craftingBlocksCopy = new int[2048];

    /// <summary>
    /// Per-block-type count of what is currently on the table.
    /// Built once in <see cref="CraftingRecipesStart"/> and read in
    /// <see cref="DrawCraftingRecipes"/>. Replaces the O(n) <c>CountBlock</c>
    /// scan that ran for every ingredient of every recipe, every frame.
    /// Indexed by block type ID; size must be at least <see cref="GlobalVar.MAX_BLOCKTYPES"/>.
    /// </summary>
    private readonly int[] _blockTypeCounts = new int[GlobalVar.MAX_BLOCKTYPES];

    // ── Injected dependencies ─────────────────────────────────────────────────
    internal Packet_CraftingRecipe[] d_CraftingRecipes;
    internal int d_CraftingRecipesCount;
    internal CraftingTableTool d_CraftingTableTool;

    // ── Once-flags ────────────────────────────────────────────────────────────
    /// <summary>
    /// True after the packet handler has been registered.
    /// Prevents re-registering the same handler reference on every frame.
    /// </summary>
    private bool _handlerRegistered;

    private IGameClient game;

    public ModGuiCrafting(IGameClient game)
    {
        this.game = game;
        handler = new PacketHandlerCraftingRecipes { mod = this };
    }

    // ── ModBase overrides ─────────────────────────────────────────────────────

    public override void OnNewFrameDraw2d(float deltaTime)
    {
        // Lazy-initialise the tool once.
        d_CraftingTableTool ??= new CraftingTableTool
        {
            d_Map = new MapStorage(game.VoxelMap, game.SetBlock),
            d_Data = game.BlockRegistry
        };

        // Register the packet handler once, not every frame.
        if (!_handlerRegistered)
        {
            game.PacketHandlers[(int)Packet_ServerIdEnum.CraftingRecipes] = handler;
            _handlerRegistered = true;
        }

        if (game.GuiState == GuiState.CraftingRecipes)
            DrawCraftingRecipes();
    }

    public override void OnNewFrameFixed(float args)
    {
        if (game.GuiState == GuiState.CraftingRecipes)
            CraftingMouse();
    }

    public override void OnKeyDown(Game game, KeyEventArgs args)
    {
        if (args.KeyChar != game.GetKey(Keys.E) || game.GuiTyping != TypingState.None) return;
        if (game.SelectedBlockPositionX == -1
         && game.SelectedBlockPositionY == -1
         && game.SelectedBlockPositionZ == -1) return;

        int posX = game.SelectedBlockPositionX;
        int posY = game.SelectedBlockPositionZ;
        int posZ = game.SelectedBlockPositionY;

        if (game.VoxelMap.GetBlock(posX, posY, posZ) != game.BlockRegistry.BlockIdCraftingTable) return;

        // GetTable / GetOnTable return references to CraftingTableTool's internal
        // reusable buffers. CraftingRecipesStart copies the on-table data into
        // _craftingBlocksCopy before storing it, so a subsequent table-open
        // cannot corrupt the active session.
        Vector3i[] table = d_CraftingTableTool.GetTable(posX, posY, posZ, out int tableCount);
        int[] onTable = d_CraftingTableTool.GetOnTable(table, tableCount, out int onTableCount);

        CraftingRecipesStart(game,
            d_CraftingRecipes, d_CraftingRecipesCount,
            onTable, onTableCount,
            posX, posY, posZ);

        args.Handled = true;
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    internal void DrawCraftingRecipes()
    {
        // ── Filter recipes for which the player has all materials ─────────────
        // Uses _blockTypeCounts (built once in CraftingRecipesStart) for O(1)
        // ingredient lookup. The old CountBlock was an O(n) linear scan called
        // for every ingredient of every recipe, every frame.
        currentRecipesCount = 0;
        for (int i = 0; i < craftingRecipes2Count; i++)
        {
            Packet_CraftingRecipe r = craftingRecipes2[i];
            if (r == null) continue;

            bool canCraft = true;
            for (int k = 0; k < r.Ingredients.Length; k++)
            {
                Packet_Ingredient ing = r.Ingredients[k];
                if (ing == null) continue;
                if (_blockTypeCounts[ing.Type] < ing.Amount)
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
            game.Draw2dText1(game.Language.NoMaterialsForCrafting(),
                game.Xcenter(200), game.Ycenter(20), FontSize, null, false);
            return;
        }

        int menuX = game.Xcenter(MenuWidth);
        int menuY = game.Ycenter(currentRecipesCount * RecipeRowHeight);

        for (int i = 0; i < currentRecipesCount; i++)
        {
            Packet_CraftingRecipe r = craftingRecipes2[currentRecipes[i]];
            int rowY = menuY + i * RecipeRowHeight;
            int color = i == craftingSelectedRecipe
                ? ColorUtils.ColorFromArgb(255, 255, 0, 0)
                : ColorUtils.ColorFromArgb(255, 255, 255, 255);
            int white = ColorUtils.ColorFromArgb(255, 255, 255, 255);

            for (int ii = 0; ii < r.Ingredients.Length; ii++)
            {
                Packet_Ingredient ing = r.Ingredients[ii];
                int colX = menuX + 20 + ii * 130;
                game.Draw2dTexture(game.TerrainTexture,
                    colX, rowY, 32, 32,
                    game.TextureIdForInventory[ing.Type], Game.TexturesPacked, white, false);
                game.Draw2dText1($"{ing.Amount} {game.BlockTypes[ing.Type].Name}",
                    colX + 50, rowY, FontSize, color, false);
            }

            int outX = menuX + 20 + OutputColumnOffset;
            game.Draw2dTexture(game.TerrainTexture,
                outX, rowY, 32, 32,
                game.TextureIdForInventory[r.Output.Type], Game.TexturesPacked, white, false);
            game.Draw2dText1($"{r.Output.Amount} {game.BlockTypes[r.Output.Type].Name}",
                outX + 50, rowY, FontSize, color, false);
        }
    }

    // ── Input ─────────────────────────────────────────────────────────────────

    internal void CraftingMouse()
    {
        if (currentRecipesCount == 0) return;

        int menuY = game.Ycenter(currentRecipesCount * RecipeRowHeight);

        if (game.MouseCurrentY >= menuY
         && game.MouseCurrentY < menuY + currentRecipesCount * RecipeRowHeight)
        {
            craftingSelectedRecipe = (game.MouseCurrentY - menuY) / RecipeRowHeight;
        }

        if (!game.MouseLeftClick) return;

        game.SendPacketClient(ClientPackets.Craft(
            craftingTablePosX, craftingTablePosY, craftingTablePosZ,
            currentRecipes[craftingSelectedRecipe]));

        game.MouseLeftClick = false;
        game.GuiStateBackToGame();
    }

    // ── Session management ────────────────────────────────────────────────────

    internal void CraftingRecipesStart(Game game,
        Packet_CraftingRecipe[] recipes, int recipesCount,
        int[] blocks, int blocksCount,
        int posX, int posY, int posZ)
    {
        craftingRecipes2 = recipes;
        craftingRecipes2Count = recipesCount;
        craftingTablePosX = posX;
        craftingTablePosY = posY;
        craftingTablePosZ = posZ;

        // ── Copy the on-table block list into our own buffer ──────────────────
        // 'blocks' is a reference to CraftingTableTool._onTableBuffer — a shared
        // reusable array. Storing the reference directly (the old code) meant
        // the next GetOnTable call would silently overwrite craftingBlocks while
        // DrawCraftingRecipes was still reading it.
        Array.Copy(blocks, _craftingBlocksCopy, blocksCount);

        // ── Build O(1) lookup table from the copied block list ────────────────
        // Clear only the range that was previously populated to avoid a full
        // MAX_BLOCKTYPES clear on every open (most block IDs will be zero anyway).
        Array.Clear(_blockTypeCounts, 0, GlobalVar.MAX_BLOCKTYPES);
        for (int i = 0; i < blocksCount; i++)
        {
            int blockId = _craftingBlocksCopy[i];
            if ((uint)blockId < (uint)GlobalVar.MAX_BLOCKTYPES)
                _blockTypeCounts[blockId]++;
        }

        game.GuiState = GuiState.CraftingRecipes;
        game.menustate = new MenuState();
        game.SetFreeMouse(true);
    }
}

/// <summary>
/// Discovers the connected region of crafting-table blocks starting from a given
/// position (flood-fill), then reads which items are placed on top of them.
/// </summary>
public class CraftingTableTool
{
    internal IMapStorage d_Map;
    internal BlockTypeRegistry d_Data;

    // ── Pre-allocated buffers ─────────────────────────────────────────────────
    // GetTable and GetOnTable are called together once per player interaction,
    // never concurrently. Keeping the work buffers as instance fields eliminates
    // three ~24 KB temporary array allocations per crafting-table open.

    private const int MaxCraftingTableSize = 2000;
    private const int BufferCapacity = 2048; // power-of-two headroom above max

    /// <summary>Output buffer for discovered crafting-table positions (reused each call).</summary>
    private readonly Vector3i[] _tableBuffer = new Vector3i[BufferCapacity];

    /// <summary>DFS frontier (reused each call).</summary>
    private readonly Vector3i[] _todoBuffer = new Vector3i[BufferCapacity];

    /// <summary>Output buffer for on-table block types (reused each call).</summary>
    private readonly int[] _onTableBuffer = new int[BufferCapacity];

    /// <summary>
    /// O(1) visited-position set.
    /// Replaces <c>Vector3IntRefArrayContains</c> which was O(n) per check,
    /// producing O(n²) total work for a fully-connected table of n blocks.
    /// <see cref="Vector3i"/> implements <see cref="IEquatable{T}"/> in OpenTK,
    /// so it is safe to use directly as a <see cref="HashSet{T}"/> key.
    /// </summary>
    private readonly HashSet<Vector3i> _visited = new();

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>
    /// Reads the block type placed directly above each position in
    /// <paramref name="table"/> (i.e. at Z + 1).
    /// </summary>
    /// <param name="table">Crafting-table positions returned by <see cref="GetTable"/>.</param>
    /// <param name="tableCount">Number of valid entries in <paramref name="table"/>.</param>
    /// <param name="retCount">Returns the number of valid entries in the returned array.</param>
    /// <returns>
    /// A pre-allocated int buffer whose first <paramref name="retCount"/> elements
    /// contain block types. Valid until the next call to <see cref="GetOnTable"/>.
    /// </returns>
    public int[] GetOnTable(Vector3i[] table, int tableCount, out int retCount)
    {
        int count = 0;
        for (int i = 0; i < tableCount; i++)
        {
            Vector3i v = table[i];
            _onTableBuffer[count++] = d_Map.GetBlock(v.X, v.Y, v.Z + 1);
        }
        retCount = count;
        return _onTableBuffer;
    }

    /// <summary>
    /// Flood-fills the connected region of crafting-table blocks reachable from
    /// (<paramref name="posx"/>, <paramref name="posy"/>, <paramref name="posz"/>)
    /// in the XY plane and returns the discovered positions.
    /// </summary>
    /// <remarks>
    /// Uses a DFS frontier (<c>_todoBuffer</c>) and an O(1) <see cref="HashSet{T}"/>
    /// for visited tracking. The previous implementation used a linear-scan array
    /// which was O(n²) in the size of the discovered region.
    /// </remarks>
    /// <param name="posx">Starting block X coordinate.</param>
    /// <param name="posy">Starting block Y coordinate.</param>
    /// <param name="posz">Starting block Z coordinate.</param>
    /// <param name="retCount">Returns the number of valid entries in the returned array.</param>
    /// <returns>
    /// A pre-allocated <see cref="Vector3i"/> buffer whose first <paramref name="retCount"/>
    /// elements contain the table positions. Valid until the next call to <see cref="GetTable"/>.
    /// </returns>
    public Vector3i[] GetTable(int posx, int posy, int posz, out int retCount)
    {
        int lCount = 0;
        int todoCount = 0;

        // ── Reset reusable state ──────────────────────────────────────────────
        _visited.Clear();
        _todoBuffer[todoCount++] = new Vector3i(posx, posy, posz);

        // ── Cache block ID — it's constant for the entire flood-fill ─────────
        // Previously called 4× per loop iteration; now fetched once.
        int craftingTableId = d_Data.BlockIdCraftingTable;

        while (todoCount > 0 && lCount < MaxCraftingTableSize)
        {
            // Pop from the top of the DFS stack.
            Vector3i p = _todoBuffer[--todoCount];

            // ── O(1) visited check via HashSet ────────────────────────────────
            // Previously Vector3IntRefArrayContains scanned _tableBuffer linearly
            // — O(n) per check, O(n²) total for a table of n blocks.
            if (!_visited.Add(p))
                continue;

            _tableBuffer[lCount++] = p;

            // Expand in all four horizontal directions.
            TryEnqueue(p.X + 1, p.Y, p.Z, craftingTableId, _todoBuffer, ref todoCount);
            TryEnqueue(p.X - 1, p.Y, p.Z, craftingTableId, _todoBuffer, ref todoCount);
            TryEnqueue(p.X, p.Y + 1, p.Z, craftingTableId, _todoBuffer, ref todoCount);
            TryEnqueue(p.X, p.Y - 1, p.Z, craftingTableId, _todoBuffer, ref todoCount);
        }

        retCount = lCount;
        return _tableBuffer;
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Pushes (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) onto
    /// <paramref name="frontier"/> if the block there is a crafting table and the
    /// frontier has space.
    /// </summary>
    private void TryEnqueue(int x, int y, int z, int craftingTableId,
                             Vector3i[] frontier, ref int count)
    {
        if (count < BufferCapacity
            && d_Map.GetBlock(x, y, z) == craftingTableId)
        {
            frontier[count++] = new Vector3i(x, y, z);
        }
    }
}