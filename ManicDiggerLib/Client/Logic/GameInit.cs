using ManicDigger.Mods;
using OpenTK.Mathematics;

/// <summary>
/// Partial class containing field declarations and constructor initialization
/// for the core Game state. Fields owned by other subsystem partials are
/// initialized via their own partial-class initializer methods called from here.
/// </summary>
public partial class Game
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    private const int MaxBlockTypes = 1024;

    public const int HourDetail = 4;
    public const int ChatFontSize = 11;
    public const int DISCONNECTED_ICON_AFTER_SECONDS = 10;

    internal const int chunksize = 16;
    internal const int chunksizebits = 4;
    internal const int speculativeMax = 8 * 1024;

    // -------------------------------------------------------------------------
    // Fields — rendering / textures
    // -------------------------------------------------------------------------

    internal AssetList assets;
    internal float assetsLoadProgress;
    internal TextColorRenderer textColorRenderer;

    /// <summary>Texture IDs indexed by [blockId][TileSide].</summary>
    internal int[][] TextureId;
    internal int[] TextureIdForInventory;

    internal int terrainTexturesPerAtlas;
    internal int terrainTexture;
    internal int[] terrainTextures1d;
    internal ITerrainTextures d_TerrainTextures;
    internal TextureAtlasConverter d_TextureAtlasConverter;

    /// <summary>Maximum texture size detected at runtime.</summary>
    internal int maxTextureSize;
    internal int Atlas1dheight() => maxTextureSize;

    internal static int texturesPacked() => GlobalVar.MAX_BLOCKTYPES_SQRT; // 16x16
    internal static int atlas2dtiles() => GlobalVar.MAX_BLOCKTYPES_SQRT;   // 16x16

    internal int handTexture;
    internal bool handRedraw;
    internal bool handSetAttackBuild;
    internal bool handSetAttackDestroy;

    // -------------------------------------------------------------------------
    // Fields — world / map
    // -------------------------------------------------------------------------

    internal Map map;
    internal InfiniteMapChunked2d d_Heightmap;
    internal Config3d d_Config3d;

    internal int lastplacedblockX;
    internal int lastplacedblockY;
    internal int lastplacedblockZ;

    // -------------------------------------------------------------------------
    // Fields — player
    // -------------------------------------------------------------------------

    internal Entity player;

    internal float playerPositionSpawnX;
    internal float playerPositionSpawnY;
    internal float playerPositionSpawnZ;

    internal bool isplayeronground;

    internal float pushX;
    internal float pushY;
    internal float pushZ;

    private float lastplayerpositionX;
    private float lastplayerpositionY;
    private float lastplayerpositionZ;

    internal bool reachedwall;
    internal bool reachedwall_1blockhigh;
    internal bool reachedHalfBlock;
    internal float movedz;

    internal float constWallDistance;
    internal float constRotationSpeed;
    internal AnimationHint localplayeranimationhint;
    internal bool enable_move;

    internal byte localstance;
    internal bool spawned;
    internal bool IsShiftPressed;
    internal int playertexturedefault;
    public const string playertexturedefaultfilename = "mineplayer.png";

    internal int reloadblock;
    internal int reloadstartMilliseconds;
    internal int lastOxygenTickMilliseconds;
    internal int LastReceivedMilliseconds;

    // -------------------------------------------------------------------------
    // Fields — camera
    // -------------------------------------------------------------------------

    internal Matrix4 camera;
    internal float CameraEyeX;
    internal float CameraEyeY;
    internal float CameraEyeZ;

    internal bool currentMatrixModeProjection;
    internal Stack<Matrix4> mvMatrix;
    internal Stack<Matrix4> pMatrix;

    // -------------------------------------------------------------------------
    // Fields — lighting
    // -------------------------------------------------------------------------

    /// <summary>Maps light level (0–15) to a GL color multiplier.</summary>
    internal float[] mLightLevels;
    internal int sunlight_;
    internal int[] NightLevels;

    internal float sunPositionX;
    internal float sunPositionY;
    internal float sunPositionZ;
    internal float moonPositionX;
    internal float moonPositionY;
    internal float moonPositionZ;
    internal bool isNight;
    internal bool fancySkysphere;
    internal bool SkySphereNight;
    internal ModSkySphereStatic skysphere;

    // -------------------------------------------------------------------------
    // Fields — input
    // -------------------------------------------------------------------------

    internal Controls controls;

    internal bool mouseLeft;
    internal bool mouseMiddle;
    internal bool mouseRight;

    internal float touchMoveDx;
    internal float touchMoveDy;
    internal float touchOrientationDx;
    internal float touchOrientationDy;

    // -------------------------------------------------------------------------
    // Fields — audio
    // -------------------------------------------------------------------------

    internal AudioControl audio;
    internal bool soundnow;

    // -------------------------------------------------------------------------
    // Fields — networking / server
    // -------------------------------------------------------------------------

    internal ClientPacketHandler[] packetHandlers;
    internal string serverGameVersion;

    internal bool ammostarted;
    internal Packet_BlockType[] NewBlockTypes;
    internal Packet_BlockType[] blocktypes;

    internal string blobdownloadname;
    internal string blobdownloadmd5;
    internal CitoMemoryStream blobdownload;

    internal ConnectData connectdata;
    internal bool issingleplayer;
    internal bool reconnect;
    internal bool exitToMainMenu;

    // -------------------------------------------------------------------------
    // Fields — speculative block placement
    // -------------------------------------------------------------------------

    internal Speculative[] speculative;
    internal int speculativeCount;

    // -------------------------------------------------------------------------
    // Fields — subsystems
    // -------------------------------------------------------------------------

    // TODO: clarify purpose of `one` — appears to be a typed 1f stand-in, possibly
    // a legacy Cito transpiler artifact where float literals needed to go through a variable.
    internal float one;

    internal GamePlatform platform;
    internal Language language;
    internal ClientModManager1 modmanager;
    internal FrustumCulling d_FrustumCulling;
    internal TerrainChunkTesselatorCi d_TerrainChunkTesselator;
    internal MeshBatcher d_Batcher;
    internal SunMoonRenderer d_SunMoonRenderer;
    internal InventoryUtilClient d_InventoryUtil;
    internal ModDrawParticleEffectBlockBreak particleEffectBlockBreak;

    internal int[] materialSlots;
    internal int Font;
    internal GameExit d_Exit;

    internal int typinglogpos;
    internal TypingState GuiTyping;

    internal bool ENABLE_DRAW_TEST_CHARACTER;
    internal bool ENABLE_DRAWPOSITION;

    private int lastWidth;
    private int lastHeight;

    private float accumulator;
    private readonly float[] modelViewInverted;
    private readonly TaskScheduler taskScheduler;

    internal List<Action> commitActions;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Game()
    {
        one = 1;
        modelViewInverted = new float[16];
        taskScheduler = new TaskScheduler();
        commitActions = new List<Action>();
        modmanager = new ClientModManager1();
        language = new Language();

        InitMap();
        InitTextures();
        InitPlayer();
        InitCamera();
        InitLighting();
        InitNetworking();
        InitAudio();
        InitInput();

        // Fields owned by other partial classes — each partial is responsible
        // for declaring and initializing its own state in a corresponding Init method.
        //InitUi();       // UI, menus, dialogs, chat — see GameUi.cs
        //InitEntities(); // Entity pool — see GameEntities.cs
       // InitOptions();  // Game options and settings — see GameOptions.cs
    }

    // -------------------------------------------------------------------------
    // Initialization helpers
    // -------------------------------------------------------------------------

    private void InitMap()
    {
        map = new Map();
        lastplacedblockX = -1;
        lastplacedblockY = -1;
        lastplacedblockZ = -1;
    }

    private void InitTextures()
    {
        TextureId = new int[MaxBlockTypes][];
        for (int i = 0; i < MaxBlockTypes; i++)
            TextureId[i] = new int[6];

        TextureIdForInventory = new int[MaxBlockTypes];
        handTexture = -1;
    }

    private void InitPlayer()
    {
        player = new Entity { position = new EntityPosition_() };

        playerPositionSpawnX = 15 + one / 2;
        playerPositionSpawnY = 64;
        playerPositionSpawnZ = 15 + one / 2;

        movedz = 0;
        constWallDistance = 0.3f;
        constRotationSpeed = one * 180 / 20;
        localplayeranimationhint = new AnimationHint();
        enable_move = true;
        playertexturedefault = -1;
    }

    private void InitCamera()
    {
        camera = Matrix4.Identity;
        mvMatrix = new Stack<Matrix4>();
        pMatrix = new Stack<Matrix4>();
        mvMatrix.Push(Matrix4.Identity);
        pMatrix.Push(Matrix4.Identity);

        CameraEyeX = -1;
        CameraEyeY = -1;
        CameraEyeZ = -1;
    }

    private void InitLighting()
    {
        sunlight_ = 15;
        mLightLevels = new float[16];
        for (int i = 0; i < 16; i++)
            mLightLevels[i] = one * i / 15;
    }

    private void InitNetworking()
    {
        packetHandlers = new ClientPacketHandler[256];
        NewBlockTypes = new Packet_BlockType[GlobalVar.MAX_BLOCKTYPES];
        speculativeCount = 0;
        speculative = new Speculative[speculativeMax];
    }

    private void InitAudio()
    {
        audio = new AudioControl();
    }

    private void InitInput()
    {
        controls = new Controls();
    }
}