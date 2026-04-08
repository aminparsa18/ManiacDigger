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

    public const int minlight = 0;
    public const int maxlight = 15;

    public const int entityMonsterIdStart = 128;
    public const int entityMonsterIdCount = 128;
    public const int entityLocalIdStart = 256;

    public const int KeyAltLeft = 5;
    public const int KeyAltRight = 6;

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

    internal int whitetexture;
    internal int cachedTextTexturesMax;
    internal CachedTextTexture[] cachedTextTextures;
    internal Dictionary<string, int> textures;
    internal int AllowedFontsCount;
    internal string[] AllowedFonts;

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

    internal bool stopPlayerMove;
    internal GuiState guistate;

    internal byte localstance;
    internal bool spawned;
    internal bool IsShiftPressed;
    internal int playertexturedefault;
    public const string playertexturedefaultfilename = "mineplayer.png";

    internal int reloadblock;
    internal int reloadstartMilliseconds;
    internal int lastOxygenTickMilliseconds;
    internal int LastReceivedMilliseconds;

    internal int LocalPlayerId;
    internal int currentlyAttackedEntity;
    internal int selectedmodelid;
    internal Vector3 playervelocity;
    internal float RadiusWhenMoving;
    internal float basemovespeed;
    internal float movespeed;
    internal float rotationspeed;
    internal float PICK_DISTANCE;
    internal int grenadetime;
    internal int PlayerPushDistance;
    internal bool AudioEnabled;
    internal bool AutoJumpEnabled;
    internal int[] TotalAmmo;
    internal int[] LoadedAmmo;
    internal Dictionary<(int x, int y, int z), float> blockHealth = new();
    internal VisibleDialog[] dialogs;
    internal string[] typinglog;
    internal int typinglogCount;

    internal bool IronSights;
    internal Random rnd;
    internal Vector3i? currentAttackedBlock;
    internal int currentTimeMilliseconds;
    internal int totaltimeMilliseconds;
    internal int ReceivedMapLength;
    internal int maxdrawdistance;

    internal bool leftpressedpicking;
    internal int pistolcycle;
    internal int lastironsightschangeMilliseconds;
    internal int grenadecookingstartMilliseconds;
    internal int lastpositionsentMilliseconds;

    internal bool shadowssimple;
    internal bool shouldRedrawAllBlocks;
    internal bool escapeMenuRestart;

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

    internal CameraMatrixProvider CameraMatrix;
    internal float fov;
    internal CameraType cameratype;
    internal bool ENABLE_TPP_VIEW;
    internal float znear;
    internal bool ENABLE_ZFAR;
    internal float overheadcameradistance;
    internal Kamera overheadcameraK;
    internal float tppcameradistance;
    internal float TPP_CAMERA_DISTANCE_MIN;
    internal float TPP_CAMERA_DISTANCE_MAX;
    internal bool enableCameraControl;
    internal Matrix4 identityMatrix;
    internal Matrix4 Set3dProjectionTempMat4;

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
    internal bool mouseSmoothing;
    internal bool mouseleftclick;
    internal bool mouseleftdeclick;
    internal bool wasmouseleft;
    internal bool mouserightclick;
    internal bool mouserightdeclick;
    internal bool wasmouseright;
    internal bool[] keyboardState;
    internal bool[] keyboardStateRaw;

    internal bool mouseLeft;
    internal bool mouseMiddle;
    internal bool mouseRight;

    internal int mouseCurrentX;
    internal int mouseCurrentY;
    internal float mouseDeltaX;
    internal float mouseDeltaY;
    private float mouseSmoothingVelX;
    private float mouseSmoothingVelY;
    private float mouseSmoothingAccum;
    private bool mousePointerLockShouldBe;
    internal bool overheadcamera;

    internal float touchMoveDx;
    internal float touchMoveDy;
    internal float touchOrientationDx;
    internal float touchOrientationDy;

    internal bool drawblockinfo;

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

    internal ModBase[] clientmods;
    internal int clientmodsCount;

    internal GamePlatform platform;
    internal Language language;
    internal ClientModManager1 modmanager;
    internal FrustumCulling d_FrustumCulling;
    internal TerrainChunkTesselatorCi d_TerrainChunkTesselator;
    internal MeshBatcher d_Batcher;
    internal SunMoonRenderer d_SunMoonRenderer;
    internal InventoryUtilClient d_InventoryUtil;
    internal ModDrawParticleEffectBlockBreak particleEffectBlockBreak;
    internal GameData d_Data;
    internal GameDataMonsters d_DataMonsters;
    internal Packet_Inventory d_Inventory;

    internal int[] materialSlots;
    internal int Font;
    internal GameExit d_Exit;

    internal int typinglogpos;
    internal TypingState GuiTyping;

    internal bool ENABLE_DRAW_TEST_CHARACTER;
    internal bool ENABLE_DRAWPOSITION;
    internal bool ENABLE_DRAW2D;
    internal int ENABLE_LAG;
    internal bool AllowFreemove;
    internal MenuState menustate;
    internal ServerInformation ServerInfo;
    internal OptionsCi options;
    internal Dictionary<string, string> performanceinfo;
    internal Packet_ServerPlayerStats PlayerStats;
    internal string[] getAsset;
    internal int fillAreaLimit;

    internal Entity[] entities;
    internal const int entitiesMax = 4096;
    internal int entitiesCount;

    internal int ChatLinesMax;
    internal Chatline[] ChatLines;
    internal int ChatLineLength;

    internal MapLoadingProgressEventArgs maploadingprogress;
    internal FontCi fontMapLoading;
    internal string invalidVersionDrawMessage;
    internal Packet_Server invalidVersionPacketIdentification;

    internal Vector3 playerdestination;
    internal string Follow;
    private ModelData circleModelData;
    private bool startedconnecting;

    private int lastWidth;
    private int lastHeight;

    private float accumulator;
    private TaskScheduler taskScheduler;

    internal List<Action> commitActions;

    internal int SelectedBlockPositionX;
    internal int SelectedBlockPositionY;
    internal int SelectedBlockPositionZ;
    internal int SelectedEntityId;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Game()
    {
        one = 1;

        InitCore();
        InitMap();
        InitTextures();
        InitPlayer();
        InitCamera();
        InitLighting();
        InitInput();
        InitEntities();
        InitOptions();
        InitNetworking();
        InitChat();
        InitAudio();
    }

    // -------------------------------------------------------------------------
    // Initialization helpers
    // -------------------------------------------------------------------------

    private void InitCore()
    {
        performanceinfo = new();
        language = new Language();
        modmanager = new ClientModManager1();
        particleEffectBlockBreak = new ModDrawParticleEffectBlockBreak();
        ServerInfo = new ServerInformation();
        options = new OptionsCi();
        getAsset = new string[1024 * 2];
        identityMatrix = Matrix4.Identity;
        Set3dProjectionTempMat4 = Matrix4.Identity;
        PlayerStats = new Packet_ServerPlayerStats();
        taskScheduler = new TaskScheduler();
        commitActions = new List<Action>();
    }

    private void InitMap()
    {
        map = new Map();
        lastplacedblockX = -1;
        lastplacedblockY = -1;
        lastplacedblockZ = -1;
        fillAreaLimit = 200;
    }

    private void InitTextures()
    {
        TextureId = new int[MaxBlockTypes][];
        for (int i = 0; i < MaxBlockTypes; i++)
            TextureId[i] = new int[6];

        TextureIdForInventory = new int[MaxBlockTypes];
        handTexture = -1;
        whitetexture = -1;
        textures = [];

        cachedTextTexturesMax = 1024;
        cachedTextTextures = new CachedTextTexture[cachedTextTexturesMax];
        for (int i = 0; i < cachedTextTexturesMax; i++)
            cachedTextTextures[i] = null;

        AllowedFontsCount = 1;
        AllowedFonts = new string[AllowedFontsCount];
        AllowedFonts[0] = "Verdana";
    }

    private void InitPlayer()
    {
        player = new Entity { position = new EntityPosition_() };
        LocalPlayerId = -1;
        currentlyAttackedEntity = -1;
        selectedmodelid = -1;
        playertexturedefault = -1;

        playerPositionSpawnX = 15 + one / 2;
        playerPositionSpawnY = 64;
        playerPositionSpawnZ = 15 + one / 2;

        playervelocity = new Vector3();
        movedz = 0;
        constWallDistance = 0.3f;
        constRotationSpeed = one * 180 / 20;
        RadiusWhenMoving = one * 3 / 10;
        basemovespeed = 5;
        movespeed = 5;
        rotationspeed = one * 15 / 100;
        PICK_DISTANCE = 4.1f;
        grenadetime = 3;
        PlayerPushDistance = 2;

        localplayeranimationhint = new AnimationHint();
        enable_move = true;
        AudioEnabled = true;
        AutoJumpEnabled = false;

        TotalAmmo = new int[GlobalVar.MAX_BLOCKTYPES];
        LoadedAmmo = new int[GlobalVar.MAX_BLOCKTYPES];
        blockHealth = new();
        dialogs = new VisibleDialog[512];
        typinglog = new string[1024 * 16];
        typinglogCount = 0;
    }

    private void InitCamera()
    {
        camera = Matrix4.Identity;
        mvMatrix = new();
        pMatrix = new();
        mvMatrix.Push(Matrix4.Identity);
        pMatrix.Push(Matrix4.Identity);

        CameraEyeX = -1;
        CameraEyeY = -1;
        CameraEyeZ = -1;

        CameraMatrix = new CameraMatrixProvider();
        fov = MathF.PI / 3;
        cameratype = CameraType.Fpp;
        ENABLE_TPP_VIEW = false;
        znear = one / 10;
        ENABLE_ZFAR = true;
        overheadcameradistance = 10;
        overheadcameraK = new Kamera();
        tppcameradistance = 3;
        TPP_CAMERA_DISTANCE_MIN = 1;
        TPP_CAMERA_DISTANCE_MAX = 10;
        enableCameraControl = true;
    }

    private void InitLighting()
    {
        sunlight_ = 15;
        mLightLevels = new float[16];
        for (int i = 0; i < 16; i++)
            mLightLevels[i] = one * i / 15;
    }

    private void InitInput()
    {
        controls = new Controls();
        mouseSmoothing = true;
        mouseleftclick = false;
        mouseleftdeclick = false;
        wasmouseleft = false;
        mouserightclick = false;
        mouserightdeclick = false;
        wasmouseright = false;

        const int KeysMax = 360;
        keyboardState = new bool[KeysMax];
        for (int i = 0; i < KeysMax; i++)
            keyboardState[i] = false;

        keyboardStateRaw = new bool[KeysMax];
        for (int i = 0; i < KeysMax; i++)
            keyboardStateRaw[i] = false;
    }

    private void InitEntities()
    {
        entities = new Entity[entitiesMax];
        for (int i = 0; i < entitiesMax; i++)
            entities[i] = null;

        entitiesCount = 512;
    }

    private void InitOptions()
    {
        ENABLE_DRAW2D = true;
        ENABLE_LAG = 0;
        AllowFreemove = true;
        menustate = new MenuState();
    }

    private void InitNetworking()
    {
        packetHandlers = new ClientPacketHandler[256];
        NewBlockTypes = new Packet_BlockType[GlobalVar.MAX_BLOCKTYPES];
        speculativeCount = 0;
        speculative = new Speculative[speculativeMax];
    }

    private void InitChat()
    {
        ChatLinesMax = 1;
        ChatLines = new Chatline[ChatLinesMax];
        ChatLineLength = 64;
    }

    private void InitAudio()
    {
        audio = new AudioControl();
    }
}