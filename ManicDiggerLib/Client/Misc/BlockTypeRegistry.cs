using ManicDigger;

// When the game server sends the client a list of all block types (dirt, lava, ladder, rail, etc.), this class reads that list and stores every block's properties in arrays, indexed by the block's ID number. 
// So if dirt is block #5, then WalkSpeed[5], Strength[5], DamageToPlayer[5] etc. all tell you everything about dirt.
//The game then consults this registry constantly to answer questions like:

//Physics — is this block slippery? How fast can you walk on it? Can you walk through it at all?
//Combat — does standing in this block hurt you (like lava)?
//Mining — how long does this block take to break?
//Rendering — does this block glow? Does it draw like a flower/plant?
//Sound — what sound plays when you walk on, break, or place this block?
//Rails — is this block part of a rail track?

//It also keeps a shortlist of special block IDs (lava, ladder, trampoline, compass, etc.) that the game needs to reference by name for specific logic — like "is the player standing in lava?" checks DamageToPlayer[BlockIdLava].
//So essentially: the server defines the rules, this class stores them, and the rest of the game reads from it constantly.When the game server sends the client a list of all block types (dirt, lava, ladder, rail, etc.), this class reads that list and stores every block's properties in arrays, indexed by the block's ID number. So if dirt is block #5, then WalkSpeed[5], Strength[5], DamageToPlayer[5] etc. all tell you everything about dirt.
//The game then consults this registry constantly to answer questions like:

//Physics — is this block slippery? How fast can you walk on it? Can you walk through it at all?
//Combat — does standing in this block hurt you (like lava)?
//Mining — how long does this block take to break?
//Rendering — does this block glow? Does it draw like a flower/plant?
//Sound — what sound plays when you walk on, break, or place this block?
//Rails — is this block part of a rail track?

//It also keeps a shortlist of special block IDs (lava, ladder, trampoline, compass, etc.) that the game needs to reference by name for specific logic — like "is the player standing in lava?" checks DamageToPlayer[BlockIdLava].
//So essentially: the server defines the rules, this class stores them, and the rest of the game reads from it constantly.

/// <summary>
/// Registry of per-block-type gameplay properties derived from server
/// <see cref="Packet_BlockType"/> descriptors.
/// </summary>
/// <remarks>
/// <para>
/// Each public array property is indexed by block-type ID in the range
/// [0, <see cref="GlobalVar.MAX_BLOCKTYPES"/>). All arrays are allocated in
/// <see cref="Start"/> and populated via <see cref="UseBlockTypes"/>.
/// </para>
/// <para>
/// <b>Rename note:</b> This class was previously named <c>GameData</c>.
/// <c>BlockTypeRegistry</c> better describes its responsibility — it registers
/// block-type metadata (physics, sounds, special IDs) sent by the server.
/// </para>
/// </remarks>
public class BlockTypeRegistry
{
    // ── Block-type property arrays (indexed by block ID) ──────────────────────

    /// <summary>Maps a placed block ID to the block ID it converts into on placement.</summary>
    public int[] WhenPlayerPlacesGetsConvertedTo { get; private set; }

    /// <summary>Whether each block renders as a flower/plant (cross-face geometry).</summary>
    public bool[] IsFlower { get; private set; }

    /// <summary>Rail sub-type for each block ID (0 = not a rail).</summary>
    public int[] Rail { get; private set; }

    /// <summary>Walk speed multiplier for each block type (default 1.0).</summary>
    public float[] WalkSpeed { get; private set; }

    /// <summary>Whether each block type causes slippery walking physics.</summary>
    public bool[] IsSlipperyWalk { get; private set; }

    /// <summary>Per-block walk sound filenames. Indexed as [blockId][soundVariant].</summary>
    public string[][] WalkSound { get; private set; }

    /// <summary>Per-block break sound filenames. Indexed as [blockId][soundVariant].</summary>
    public string[][] BreakSound { get; private set; }

    /// <summary>Per-block build/place sound filenames. Indexed as [blockId][soundVariant].</summary>
    public string[][] BuildSound { get; private set; }

    /// <summary>Per-block clone sound filenames. Indexed as [blockId][soundVariant].</summary>
    public string[][] CloneSound { get; private set; }

    /// <summary>Emitted light radius for each block type (0 = no light).</summary>
    public int[] LightRadius { get; private set; }

    /// <summary>Default starting inventory amount for each block type.</summary>
    public int[] StartInventoryAmount { get; private set; }

    /// <summary>Mining strength (time-to-break) for each block type.</summary>
    public float[] Strength { get; private set; }

    /// <summary>Contact damage dealt to the player per tick for each block type.</summary>
    public int[] DamageToPlayer { get; private set; }

    /// <summary>Walkability classification for each block type.</summary>
    public WalkableType[] WalkableType { get; private set; }

    /// <summary>Default hotbar material slot assignments.</summary>
    public int[] DefaultMaterialSlots { get; private set; }

    /// <summary>Maximum number of sound variants stored per block per sound category.</summary>
    public const int SoundCount = 8;

    // ── Special block IDs ─────────────────────────────────────────────────────
    // These are resolved by name from server block-type data in SetSpecialBlock.
    // Until resolved, sentinel value -1 means "not yet assigned".

    /// <summary>Block ID for air/empty (always 0).</summary>
    public int BlockIdEmpty { get; private set; } = 0;

    /// <summary>Block ID for dirt, or -1 if not defined by this server.</summary>
    public int BlockIdDirt { get; private set; } = -1;

    /// <summary>Block ID for sponge, or -1 if not defined by this server.</summary>
    public int BlockIdSponge { get; private set; } = -1;

    /// <summary>Block ID for trampoline, or -1 if not defined by this server.</summary>
    public int BlockIdTrampoline { get; private set; } = -1;

    /// <summary>Block ID for adminium (indestructible block), or -1 if not defined.</summary>
    public int BlockIdAdminium { get; private set; } = -1;

    /// <summary>Block ID for the compass item, or -1 if not defined.</summary>
    public int BlockIdCompass { get; private set; } = -1;

    /// <summary>Block ID for ladder, or -1 if not defined.</summary>
    public int BlockIdLadder { get; private set; } = -1;

    /// <summary>Block ID used to represent an empty hand slot, or -1 if not defined.</summary>
    public int BlockIdEmptyHand { get; private set; } = -1;

    /// <summary>Block ID for the crafting table, or -1 if not defined.</summary>
    public int BlockIdCraftingTable { get; private set; } = -1;

    /// <summary>Block ID for flowing lava, or -1 if not defined.</summary>
    public int BlockIdLava { get; private set; } = -1;

    /// <summary>Block ID for stationary lava, or -1 if not defined.</summary>
    public int BlockIdStationaryLava { get; private set; } = -1;

    /// <summary>Block ID for the fill-start marker, or -1 if not defined.</summary>
    public int BlockIdFillStart { get; private set; } = -1;

    /// <summary>Block ID for cuboid tool, or -1 if not defined.</summary>
    public int BlockIdCuboid { get; private set; } = -1;

    /// <summary>Block ID for fill-area tool, or -1 if not defined.</summary>
    public int BlockIdFillArea { get; private set; } = -1;

    /// <summary>Block ID for minecart, or -1 if not defined.</summary>
    public int BlockIdMinecart { get; private set; } = -1;

    /// <summary>
    /// First block ID in the 64-tile rail sequence.
    /// Rail tile IDs occupy [BlockIdRailStart, BlockIdRailStart + 64).
    /// Defaults to -128 as a sentinel; overridden when the server sends "Rail0".
    /// </summary>
    public int BlockIdRailStart { get; private set; } = -128;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    /// <summary>Allocates all per-block-type arrays sized for <see cref="GlobalVar.MAX_BLOCKTYPES"/> entries.</summary>
    public void Start() => AllocateArrays(GlobalVar.MAX_BLOCKTYPES);

    /// <summary>
    /// Allocates all block-type property arrays to <paramref name="blockTypeCount"/> entries.
    /// Walk speed defaults to 1.0; all sound variants default to null.
    /// </summary>
    private void AllocateArrays(int blockTypeCount)
    {
        WhenPlayerPlacesGetsConvertedTo = new int[blockTypeCount];
        IsFlower = new bool[blockTypeCount];
        Rail = new int[blockTypeCount];
        IsSlipperyWalk = new bool[blockTypeCount];
        LightRadius = new int[blockTypeCount];
        StartInventoryAmount = new int[blockTypeCount];
        Strength = new float[blockTypeCount];
        DamageToPlayer = new int[blockTypeCount];
        WalkableType = new WalkableType[blockTypeCount];
        DefaultMaterialSlots = new int[10];

        WalkSpeed = new float[blockTypeCount];
        WalkSpeed.AsSpan().Fill(1f);

        // Pre-allocate all sound variant arrays so UseBlockType can clear-and-fill
        // rather than allocate on every block-type load.
        WalkSound = new string[blockTypeCount][];
        BreakSound = new string[blockTypeCount][];
        BuildSound = new string[blockTypeCount][];
        CloneSound = new string[blockTypeCount][];
        for (int i = 0; i < blockTypeCount; i++)
        {
            WalkSound[i] = new string[SoundCount];
            BreakSound[i] = new string[SoundCount];
            BuildSound[i] = new string[SoundCount];
            CloneSound[i] = new string[SoundCount];
        }
    }

    // ── Block type loading ────────────────────────────────────────────────────

    /// <summary>
    /// Applies all non-null entries in <paramref name="blocktypes"/> to the
    /// registry by calling <see cref="RegisterBlockType"/> for each.
    /// </summary>
    public void UseBlockTypes(Dictionary<int, BlockType> blocktypes)
    {
        for (int i = 0; i < blocktypes.Count; i++)
        {
            if (blocktypes[i] != null)
                RegisterBlockType(i, blocktypes[i]);
        }
    }

    /// <summary>
    /// Populates all per-block-type arrays for block ID <paramref name="id"/>
    /// from the server descriptor <paramref name="b"/> and resolves any special
    /// block ID this block may represent.
    /// </summary>
    public void RegisterBlockType(int id, BlockType b)
    {
        if (b.Name == null) return;

        WhenPlayerPlacesGetsConvertedTo[id] = b.WhenPlayerPlacesGetsConvertedTo != 0
            ? b.WhenPlayerPlacesGetsConvertedTo
            : id;

        IsFlower[id] = b.DrawType == DrawType.Plant;
        Rail[id] = b.Rail;
        WalkSpeed[id] = b.WalkSpeed;
        IsSlipperyWalk[id] = b.IsSlipperyWalk;

        // ── Sound variants ────────────────────────────────────────────────────
        // Clear the pre-allocated sound arrays rather than reallocating them.
        // The original code did `WalkSound()[id] = new string[SoundCount]` which
        // discarded the arrays allocated in AllocateArrays on every call.
        Array.Clear(WalkSound[id], 0, SoundCount);
        Array.Clear(BreakSound[id], 0, SoundCount);
        Array.Clear(BuildSound[id], 0, SoundCount);
        Array.Clear(CloneSound[id], 0, SoundCount);

        if (b.Sounds != null)
        {
            // ── Bug fix: the original code passed `platform` as the first argument
            // to string.Concat, prepending its ToString() to every sound filename.
            // Sound paths should just be the name + ".wav".
            for (int i = 0; i < b.Sounds.Walk.Length; i++) WalkSound[id][i] = b.Sounds.Walk[i] + ".wav";
            for (int i = 0; i < b.Sounds.Break.Length; i++) BreakSound[id][i] = b.Sounds.Break[i] + ".wav";
            for (int i = 0; i < b.Sounds.Build.Length; i++) BuildSound[id][i] = b.Sounds.Build[i] + ".wav";
            for (int i = 0; i < b.Sounds.Clone.Length; i++) CloneSound[id][i] = b.Sounds.Clone[i] + ".wav";
        }

        LightRadius[id] = b.LightRadius;
        Strength[id] = b.Strength;
        DamageToPlayer[id] = b.DamageToPlayer;
        WalkableType[id] = b.WalkableType;

        SetSpecialBlockId(b, id);
    }

    // ── Special block ID registration ─────────────────────────────────────────

    /// <summary>
    /// Checks whether <paramref name="b"/> matches a known special block name
    /// and, if so, stores <paramref name="id"/> in the corresponding property.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if <paramref name="b"/> was a special block;
    /// <see langword="false"/> otherwise.
    /// </returns>
    /// <remarks>
    /// TODO: replace name-based matching with a dedicated block property so that
    /// special IDs do not depend on server-side naming conventions.
    /// </remarks>
    private bool SetSpecialBlockId(BlockType b, int id)
    {
        switch (b.Name)
        {
            case "Empty": BlockIdEmpty = id; return true;
            case "Dirt": BlockIdDirt = id; return true;
            case "Sponge": BlockIdSponge = id; return true;
            case "Trampoline": BlockIdTrampoline = id; return true;
            case "Adminium": BlockIdAdminium = id; return true;
            case "Compass": BlockIdCompass = id; return true;
            case "Ladder": BlockIdLadder = id; return true;
            case "EmptyHand": BlockIdEmptyHand = id; return true;
            case "CraftingTable": BlockIdCraftingTable = id; return true;
            case "Lava": BlockIdLava = id; return true;
            case "StationaryLava": BlockIdStationaryLava = id; return true;
            case "FillStart": BlockIdFillStart = id; return true;
            case "Cuboid": BlockIdCuboid = id; return true;
            case "FillArea": BlockIdFillArea = id; return true;
            case "Minecart": BlockIdMinecart = id; return true;
            case "Rail0": BlockIdRailStart = id; return true;
            default: return false;
        }
    }

    // ── Queries ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="id"/> falls within the
    /// 64-tile rail sequence starting at <see cref="BlockIdRailStart"/>.
    /// </summary>
    public bool IsRailTile(int id)
        => id >= BlockIdRailStart && id < BlockIdRailStart + 64;

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Decodes a fixed-point integer (Q5 format, i.e. value / 32) to a float.
    /// </summary>
    private static float DeserializeFloat(int p) => p / 32f;
}