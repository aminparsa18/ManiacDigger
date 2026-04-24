using OpenTK.Mathematics;
using System.Collections.Concurrent;

/// <summary>
/// Partial class containing field declarations and constructor initialization
/// for the core Game state. Fields owned by other subsystem partials are
/// initialized via their own partial-class initializer methods called from here.
/// </summary>
public partial class Game : IGameClient
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

    public List<Asset> Assets { get; set; }
    public float AssetsLoadProgress { get; set; }
    internal TextRenderer textRenderer;

    /// <summary>Texture IDs indexed by [blockId][TileSide].</summary>
    public int[][] TextureId { get; set; }
    public int[] TextureIdForInventory { get; set; }

    public int TerrainTexture { get; set; }
    public int[] TerrainTextures1d { get; set; }

    /// <summary>Maximum texture size detected at runtime.</summary>
    internal int maxTextureSize;
    internal int Atlas1dheight() => maxTextureSize;

    internal static int TexturesPacked => GlobalVar.MAX_BLOCKTYPES_SQRT; // 16x16
    internal static int Atlas2DTiles => GlobalVar.MAX_BLOCKTYPES_SQRT;   // 16x16

    internal int handTexture;
    public bool HandRedraw { get; set; }
    internal bool handSetAttackBuild;
    internal bool handSetAttackDestroy;

    internal int whitetexture;
    public Dictionary<TextStyle, CachedTexture> CachedTextTextures { get; set; } = [];
    internal Dictionary<string, int> textures;
    internal List<string> AllowedFonts;

    public int ActiveMaterial { get; set; }

    // -------------------------------------------------------------------------
    // Fields — world / map
    // -------------------------------------------------------------------------

    public VoxelMap VoxelMap { get; set; }
    public ChunkedMap2d<int> Heightmap { get; set; }
    public Config3d Config3d { get; set; }

    public int LastplacedblockX { get; set; }
    public int LastplacedblockY { get; set; }
    public int LastplacedblockZ { get; set; }

    // -------------------------------------------------------------------------
    // Fields — player
    // -------------------------------------------------------------------------

    public Entity Player { get; set; }

    public float PlayerPositionSpawnX { get; set; }
    public float PlayerPositionSpawnY { get; set; }
    public float PlayerPositionSpawnZ { get; set; }

    public bool IsPlayerOnGround { get; set; }

    public float PushX { get; set; }
    public float PushY { get; set; }
    public float PushZ { get; set; }

    private float lastplayerpositionX;
    private float lastplayerpositionY;
    private float lastplayerpositionZ;

    public bool ReachedWall { get; set; }
    public bool ReachedWall1BlockHigh { get; set; }
    public bool ReachedHalfBlock { get; set; }
    public float MovedZ {  get; set; }

    internal float constWallDistance;
    internal float constRotationSpeed;
    public AnimationHint LocalPlayerAnimationHint { get; set; }
    public bool EnableMove { get; set; }

    internal bool stopPlayerMove;
    public GuiState GuiState { get; set; }

    public byte LocalStance { get; set; }
    public bool Spawned {  get; set; }
    public bool IsShiftPressed { get; set; }
    internal int playertexturedefault;
    public const string playertexturedefaultfilename = "mineplayer.png";

    public int ReloadBlock {  get; set; }
    public int ReloadStartMilliseconds {  get; set; }
    internal int lastOxygenTickMilliseconds;
    public int LastReceivedMilliseconds { get; set; }

    public int LocalPlayerId { get; set; }
    public int CurrentlyAttackedEntity {  get; set; }
    internal int selectedmodelid;
    internal Vector3 playervelocity;
    internal float RadiusWhenMoving;
    public float Basemovespeed { get; set; }
    public float MoveSpeed { get; set; }
    internal float rotationspeed;
    internal float PICK_DISTANCE;
    internal int grenadetime;
    internal int PlayerPushDistance;
    public bool AudioEnabled { get; set; }
    public bool AutoJumpEnabled { get; set; }
    public int[] TotalAmmo { get; set; }
    public int[] LoadedAmmo { get; set; }
    internal Dictionary<(int x, int y, int z), float> blockHealth = new();
    public VisibleDialog[] Dialogs { get; set; }
    public List<string> TypingLog { get; set;}

    internal bool IronSights;
    internal Random rnd;
    public Vector3i? CurrentAttackedBlock {  get; set; }
    public int CurrentTimeMilliseconds { get; set; }
    public int TotalTimeMilliseconds { get; set; }
    public int ReceivedMapLength { get; set; }
    internal int maxdrawdistance;

    internal bool leftpressedpicking;
    internal int pistolcycle;
    internal int lastironsightschangeMilliseconds;
    internal int grenadecookingstartMilliseconds;
    public int LastPositionSentMilliseconds {  get; set; }

    internal bool shadowssimple;
    public bool ShouldRedrawAllBlocks { get; set; }
    public bool EscapeMenuRestart {  get; set; }

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
    public CameraType CameraType { get; set; }
    internal bool ENABLE_TPP_VIEW;
    internal float znear;
    internal bool ENABLE_ZFAR;
    public float OverHeadCameraDistance { get; set; }
    public Camera OverheadCameraK {  get; set; }
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
    public float[] LightLevels { get; set; }
    public int Sunlight { get; set; }
    public int[] NightLevels { get; set; }

    internal float sunPositionX;
    internal float sunPositionY;
    internal float sunPositionZ;
    internal float moonPositionX;
    internal float moonPositionY;
    internal float moonPositionZ;
    internal bool isNight;
    internal bool fancySkysphere;
    public bool SkySphereNight { get; set; }
    internal ModSkySphereStatic skysphere;

    // -------------------------------------------------------------------------
    // Fields — input
    // -------------------------------------------------------------------------

    public Controls Controls { get; set; }
    internal bool mouseSmoothing;
    public bool MouseLeftClick { get; set; }
    internal bool mouseleftdeclick;
    internal bool wasmouseleft;
    internal bool mouserightclick;
    internal bool mouserightdeclick;
    internal bool wasmouseright;
    public bool[] KeyboardState { get; set; }
    public bool[] KeyboardStateRaw { get; set; }

    internal bool mouseLeft;
    internal bool mouseMiddle;
    internal bool mouseRight;

    public int MouseCurrentX { get; set; }
    public int MouseCurrentY { get; set; }
    internal float mouseDeltaX;
    internal float mouseDeltaY;
    private float mouseSmoothingVelX;
    private float mouseSmoothingVelY;
    private float mouseSmoothingAccum;
    private bool mousePointerLockShouldBe;
    public bool OverheadCamera { get; set; }

    public float TouchMoveDx { get; set; }
    public float TouchMoveDy { get; set; }
    public float TouchOrientationDx { get; set; }
    public float TouchOrientationDy {  get; set; }

    public bool DrawBlockInfo {  get; set; }

    // -------------------------------------------------------------------------
    // Fields — audio
    // -------------------------------------------------------------------------

    public AudioControl Audio { get; set; }
    public bool soundnow {  get; set; }

    // -------------------------------------------------------------------------
    // Fields — networking / server
    // -------------------------------------------------------------------------

    public ClientPacketHandler[] PacketHandlers { get; set; }
    public string ServerGameVersion { get; set; }

    public bool AmmoStarted { get; set; }
    public Packet_BlockType[] NewBlockTypes { get; set; }
    public Packet_BlockType[] BlockTypes { get; set; }

    public string BlobDownloadName { get; set; }
    public string BlobDownloadMd5 { get; set; }
    public MemoryStream BlobDownload { get; set; }

    internal ConnectionData connectdata;
    public bool IsSinglePlayer {  get; set; }
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

    internal List<ModBase> clientmods;

    public IGamePlatform Platform { get; set; }
    public Language Language { get; set; }
    internal FrustumCulling FrustumCulling;
    public TerrainChunkTesselator TerrainChunkTesselator { get; set; }
    public MeshBatcher Batcher { get; set; }
    public SunMoonRenderer SunMoonRenderer { get; set; }
    public InventoryUtilClient InventoryUtil { get; set; }
    internal ModDrawParticleEffectBlockBreak particleEffectBlockBreak;
    public BlockTypeRegistry BlockRegistry { get; set; }
    public Packet_Inventory Inventory { get; set; }

    internal int[] materialSlots;
    public int Font { get; set; }
    internal GameExit d_Exit;

    public int TypingLogPos { get; set; }
    public TypingState GuiTyping { get; set; }

    public bool EnableDrawTestCharacter { get; set; }
    public bool EnableDrawPosition { get; set; }
    internal bool ENABLE_DRAW2D;
    public int EnableLog {  get; set; }
    public bool AllowFreeMove { get; set; }
    internal MenuState menustate;
    public ServerInformation ServerInfo { get; set; }
    public GameOption options { get; set; }
    internal Dictionary<string, string> performanceinfo;
    public Packet_ServerPlayerStats PlayerStats { get; set; }
    internal string[] getAsset;
    public int FillAreaLimit { get; set; }

    public List<Entity> Entities { get; set; }

    internal int ChatLinesMax;
    public List<Chatline> ChatLines { get; set; }

    public MapLoadingProgressEventArgs maploadingprogress {  get; set; }
    public Font FontMapLoading { get; set; }
    public string InvalidVersionDrawMessage { get; set; }
    public Packet_Server InvalidVersionPacketIdentification { get; set;}

    public Vector3 PlayerDestination { get; set; }
    public string Follow { get; set; }
    private bool startedconnecting;

    private int lastWidth;
    private int lastHeight;

    private float accumulator;
    private TaskScheduler taskScheduler;

    internal ConcurrentQueue<Action> commitActions;

    public int SelectedBlockPositionX { get; set; }
    public int SelectedBlockPositionY { get; set; }
    public int SelectedBlockPositionZ { get; set; }
    internal int SelectedEntityId;

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public Game(IGamePlatform platform)
    {
        Platform = platform;
        InitCore();
        InitMap();
        InitTextures();
        InitPlayer();
        InitCamera();
        InitLighting();
        InitInput();
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
        performanceinfo = [];
        Language = new Language();
        particleEffectBlockBreak = new ModDrawParticleEffectBlockBreak();
        ServerInfo = new ServerInformation();
        options = new GameOption();
        getAsset = new string[1024 * 2];
        identityMatrix = Matrix4.Identity;
        Set3dProjectionTempMat4 = Matrix4.Identity;
        PlayerStats = new Packet_ServerPlayerStats();
        taskScheduler = new TaskScheduler();
        commitActions = new();
        Entities = [];
    }

    private void InitMap()
    {
        VoxelMap = new VoxelMap();
        LastplacedblockX = -1;
        LastplacedblockY = -1;
        LastplacedblockZ = -1;
        FillAreaLimit = 200;
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
        CachedTextTextures = [];

        AllowedFonts = ["Verdana"];
    }

    private void InitPlayer()
    {
        Player = new Entity { position = new EntityPosition_() };
        LocalPlayerId = -1;
        CurrentlyAttackedEntity = -1;
        selectedmodelid = -1;
        playertexturedefault = -1;

        PlayerPositionSpawnX = 15.5f;
        PlayerPositionSpawnY = 64;
        PlayerPositionSpawnZ = 15.5f;

        playervelocity = new Vector3();
        MovedZ = 0;
        constWallDistance = 0.3f;
        constRotationSpeed = 180 / 20;
        RadiusWhenMoving = 3f / 10;
        Basemovespeed = 5;
        MoveSpeed = 5;
        rotationspeed = 15f / 100;
        PICK_DISTANCE = 4.1f;
        grenadetime = 3;
        PlayerPushDistance = 2;

        LocalPlayerAnimationHint = new AnimationHint();
        EnableMove = true;
        AudioEnabled = true;
        AutoJumpEnabled = false;

        TotalAmmo = new int[GlobalVar.MAX_BLOCKTYPES];
        LoadedAmmo = new int[GlobalVar.MAX_BLOCKTYPES];
        blockHealth = [];
        Dialogs = new VisibleDialog[512];
        TypingLog = [];
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
        CameraType = CameraType.Fpp;
        ENABLE_TPP_VIEW = false;
        znear = 1f / 10;
        ENABLE_ZFAR = true;
        OverHeadCameraDistance = 10;
        OverheadCameraK = new Camera();
        tppcameradistance = 3;
        TPP_CAMERA_DISTANCE_MIN = 1;
        TPP_CAMERA_DISTANCE_MAX = 10;
        enableCameraControl = true;
    }

    private void InitLighting()
    {
        Sunlight = 15;
        LightLevels = new float[16];
        for (int i = 0; i < 16; i++)
            LightLevels[i] = 0.15f;
    }

    private void InitInput()
    {
        Controls = new Controls();
        mouseSmoothing = true;
        MouseLeftClick = false;
        mouseleftdeclick = false;
        wasmouseleft = false;
        mouserightclick = false;
        mouserightdeclick = false;
        wasmouseright = false;

        const int KeysMax = 360;
        KeyboardState = new bool[KeysMax];
        for (int i = 0; i < KeysMax; i++)
            KeyboardState[i] = false;

        KeyboardStateRaw = new bool[KeysMax];
        for (int i = 0; i < KeysMax; i++)
            KeyboardStateRaw[i] = false;
    }

    private void InitOptions()
    {
        ENABLE_DRAW2D = true;
        EnableLog = 0;
        AllowFreeMove = true;
        menustate = new MenuState();
    }

    private void InitNetworking()
    {
        PacketHandlers = new ClientPacketHandler[256];
        NewBlockTypes = new Packet_BlockType[GlobalVar.MAX_BLOCKTYPES];
        speculativeCount = 0;
        speculative = new Speculative[speculativeMax];
    }

    private void InitChat()
    {
        ChatLinesMax = 1;
        ChatLines = [];
    }

    private void InitAudio()
    {
        Audio = new AudioControl();
    }
}