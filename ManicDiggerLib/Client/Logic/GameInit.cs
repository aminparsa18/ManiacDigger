using ManicDigger;
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
    // Platform & core subsystems
    // -------------------------------------------------------------------------

    public IGamePlatform Platform { get; set; }
    public Language Language { get; set; }
    public Config3d Config3d { get; set; }
    public GameOption options { get; set; }
    public ServerInformation ServerInfo { get; set; }
    private Dictionary<string, string> performanceinfo;
    private TaskScheduler taskScheduler;
    public ConcurrentQueue<Action> commitActions { get; set; }
    private GameExit d_Exit;

    // -------------------------------------------------------------------------
    // Rendering / textures
    // -------------------------------------------------------------------------

    public List<Asset> Assets { get; set; }
    public float AssetsLoadProgress { get; set; }
    internal TextRenderer textRenderer;

    /// <summary>Texture IDs indexed by [blockId][TileSide].</summary>
    public Dictionary<int, int[]> TextureId { get; set; }
    public Dictionary<int, int> TextureIdForInventory { get; set; }

    public int TerrainTexture { get; set; }
    public int[] TerrainTextures1d { get; set; }

    /// <summary>Maximum texture size detected at runtime.</summary>
    internal int maxTextureSize;
    internal int Atlas1dheight() => maxTextureSize;

    internal static int TexturesPacked => GlobalVar.MAX_BLOCKTYPES_SQRT;
    internal static int Atlas2DTiles => GlobalVar.MAX_BLOCKTYPES_SQRT;

    public int handTexture { get; set; }
    public bool HandRedraw { get; set; }

    private int whitetexture;
    public Dictionary<TextStyle, CachedTexture> CachedTextTextures { get; set; } = [];
    internal Dictionary<string, int> textures;
    internal List<string> AllowedFonts;

    public int ActiveMaterial { get; set; }
    public int Font { get; set; }

    public bool ENABLE_DRAW2D { get; set; }
    public bool EnableDrawTestCharacter { get; set; }
    public bool EnableDrawPosition { get; set; }
    public int EnableLog { get; set; }

    // -------------------------------------------------------------------------
    // World / map
    // -------------------------------------------------------------------------

    public VoxelMap VoxelMap { get; set; }
    public ChunkedMap2d<int> Heightmap { get; set; }

    public int LastplacedblockX { get; set; }
    public int LastplacedblockY { get; set; }
    public int LastplacedblockZ { get; set; }

    public int FillAreaLimit { get; set; }
    public int ReceivedMapLength { get; set; }
    public bool ShouldRedrawAllBlocks { get; set; }
    public MapLoadingProgressEventArgs maploadingprogress { get; set; }
    public Font FontMapLoading { get; set; }

    // -------------------------------------------------------------------------
    // Player — identity & position
    // -------------------------------------------------------------------------

    public Entity Player { get; set; }
    public int LocalPlayerId { get; set; }

    public float LocalPositionX { get => Player.position.x; set => Player.position.x = value; }
    public float LocalPositionY { get => Player.position.y; set => Player.position.y = value; }
    public float LocalPositionZ { get => Player.position.z; set => Player.position.z = value; }

    public float LocalOrientationX { get => Player.position.rotx; set => Player.position.rotx = value; }
    public float LocalOrientationY { get => Player.position.roty; set => Player.position.roty = value; }
    public float LocalOrientationZ { get => Player.position.rotz; set => Player.position.rotz = value; }

    public float PlayerPositionSpawnX { get; set; }
    public float PlayerPositionSpawnY { get; set; }
    public float PlayerPositionSpawnZ { get; set; }

    public Vector3 PlayerDestination { get; set; }
    public Vector3 playervelocity { get; set; }

    private float lastplayerpositionX;
    private float lastplayerpositionY;
    private float lastplayerpositionZ;

    // -------------------------------------------------------------------------
    // Player — state & stats
    // -------------------------------------------------------------------------

    public bool Spawned { get; set; }
    public bool IsPlayerOnGround { get; set; }
    public bool IsShiftPressed { get; set; }
    public byte LocalStance { get; set; }
    public GuiState GuiState { get; set; }
    public AnimationHint LocalPlayerAnimationHint { get; set; }

    public Packet_ServerPlayerStats PlayerStats { get; set; }
    public Dictionary<(int x, int y, int z), float> blockHealth { get; set; } = new();

    public int CurrentlyAttackedEntity { get; set; }
    public Vector3i? CurrentAttackedBlock { get; set; }
    public int SelectedEntityId { get; set; }
    public int SelectedBlockPositionX { get; set; }
    public int SelectedBlockPositionY { get; set; }
    public int SelectedBlockPositionZ { get; set; }

    public int[] TotalAmmo { get; set; }
    public int[] LoadedAmmo { get; set; }
    public bool AmmoStarted { get; set; }
    public bool AudioEnabled { get; set; }
    public bool AutoJumpEnabled { get; set; }
    public bool IronSights { get; set; }
    public bool DrawBlockInfo { get; set; }

    public VisibleDialog[] Dialogs { get; set; }
    public List<string> TypingLog { get; set; }
    public int TypingLogPos { get; set; }
    public TypingState GuiTyping { get; set; }

    private int playertexturedefault;
    public const string playertexturedefaultfilename = "mineplayer.png";

    private int lastOxygenTickMilliseconds;
    public int LastReceivedMilliseconds { get; set; }
    public int ReloadBlock { get; set; }
    public int ReloadStartMilliseconds { get; set; }
    public int CurrentTimeMilliseconds { get; set; }
    public int TotalTimeMilliseconds { get; set; }
    public int LastPositionSentMilliseconds { get; set; }

    // -------------------------------------------------------------------------
    // Player — movement & physics
    // -------------------------------------------------------------------------

    public bool EnableMove { get; set; }
    public bool stopPlayerMove { get; set; }
    public float MoveSpeed { get; set; }
    public float Basemovespeed { get; set; }
    public float PICK_DISTANCE { get; set; }
    public float MovedZ { get; set; }
    public bool ReachedWall { get; set; }
    public bool ReachedWall1BlockHigh { get; set; }
    public bool ReachedHalfBlock { get; set; }
    public bool AllowFreeMove { get; set; }

    public float PushX { get; set; }
    public float PushY { get; set; }
    public float PushZ { get; set; }

    public float WallDistance { get; set; }
    internal float constRotationSpeed;
    private float RadiusWhenMoving;
    private float rotationspeed;
    private int PlayerPushDistance;

    public int grenadetime { get; set; }
    public int grenadecookingstartMilliseconds { get; set; }
    public int pistolcycle { get; set; }
    public bool leftpressedpicking { get; set; }
    public int lastironsightschangeMilliseconds { get; set; }
    public bool handSetAttackBuild { get; set; }    // already declared above — kept for region clarity
    public bool handSetAttackDestroy { get; set; }  // already declared above — kept for region clarity

    // -------------------------------------------------------------------------
    // Camera
    // -------------------------------------------------------------------------

    public Matrix4 Camera { get; set; }
    public Vector3 CameraEye { get; set; }
    public Stack<Matrix4> mvMatrix { get; set; }
    public Stack<Matrix4> pMatrix { get; set; }

    private CameraMatrixProvider CameraMatrix;
    private float fov;
    public CameraType CameraType { get; set; }
    public bool EnableTppView { get; set; }
    public float TppCameraDistance { get; set; }
    public float OverHeadCameraDistance { get; set; }
    public Camera OverheadCameraK { get; set; }
    public bool OverheadCamera { get; set; }
    private bool enableCameraControl;
    private float znear;
    private bool ENABLE_ZFAR;
    private float TPP_CAMERA_DISTANCE_MIN;
    private float TPP_CAMERA_DISTANCE_MAX;
    private bool currentMatrixModeProjection;
    private Matrix4 identityMatrix;
    private Matrix4 Set3dProjectionTempMat4;
    private int selectedmodelid;
    private int maxdrawdistance;

    // -------------------------------------------------------------------------
    // Lighting
    // -------------------------------------------------------------------------

    public float[] LightLevels { get; set; }
    public int Sunlight { get; set; }
    public int[] NightLevels { get; set; }
    public Vector3 sunPosition { get; set; }
    public Vector3 moonPosition { get; set; }
    public bool isNight { get; set; }
    public bool fancySkysphere { get; set; }
    public bool SkySphereNight { get; set; }
    private ModSkySphereStatic skysphere;
    public bool shadowssimple { get; set; }

    // -------------------------------------------------------------------------
    // Input
    // -------------------------------------------------------------------------

    public Controls Controls { get; set; }
    internal bool mouseSmoothing;

    public bool MouseLeftClick { get; set; }
    public bool mouseleftdeclick { get; set; }
    private bool wasmouseleft;
    public bool mouserightclick { get; set; }
    private bool mouserightdeclick;
    private bool wasmouseright;

    public bool[] KeyboardState { get; set; }
    public bool[] KeyboardStateRaw { get; set; }

    public bool mouseLeft { get; set; }
    public bool mouseMiddle { get; set; }
    public bool mouseRight { get; set; }

    public int MouseCurrentX { get; set; }
    public int MouseCurrentY { get; set; }
    private float mouseDeltaX;
    private float mouseDeltaY;
    private float mouseSmoothingVelX;
    private float mouseSmoothingVelY;
    private float mouseSmoothingAccum;
    private bool mousePointerLockShouldBe;

    public float TouchMoveDx { get; set; }
    public float TouchMoveDy { get; set; }
    public float TouchOrientationDx { get; set; }
    public float TouchOrientationDy { get; set; }

    // -------------------------------------------------------------------------
    // Audio
    // -------------------------------------------------------------------------

    public AudioControl Audio { get; set; }
    public bool soundnow { get; set; }

    // -------------------------------------------------------------------------
    // Networking / server
    // -------------------------------------------------------------------------

    public ClientPacketHandler[] PacketHandlers { get; set; }
    public NetClient NetClient { get; set; }
    public string ServerGameVersion { get; set; }
    public bool IsSinglePlayer { get; set; }
    public string Follow { get; set; }
    internal ConnectionData connectdata;
    internal bool reconnect;
    internal bool exitToMainMenu;
    private bool startedconnecting;

    public Dictionary<int, BlockType> BlockTypes { get; set; }
    public Dictionary<int, BlockType> NewBlockTypes { get; set; }

    public string BlobDownloadName { get; set; }
    public string BlobDownloadMd5 { get; set; }
    public MemoryStream BlobDownload { get; set; }

    public string InvalidVersionDrawMessage { get; set; }
    public Packet_Server InvalidVersionPacketIdentification { get; set; }

    // -------------------------------------------------------------------------
    // Speculative block placement
    // -------------------------------------------------------------------------

    private Speculative?[] speculative;
    private int speculativeCount;

    // -------------------------------------------------------------------------
    // Subsystems
    // -------------------------------------------------------------------------

    public List<Entity> Entities { get; set; }
    public List<ModBase> ClientMods { get; set; }
    public FrustumCulling FrustumCulling { get; set; }
    public TerrainChunkTesselator TerrainChunkTesselator { get; set; }
    public MeshBatcher Batcher { get; set; }
    public SunMoonRenderer SunMoonRenderer { get; set; }
    public InventoryUtilClient InventoryUtil { get; set; }
    public BlockTypeRegistry BlockRegistry { get; set; }
    public Packet_Inventory Inventory { get; set; }
    public BlockOctreeSearcher BlockOctreeSearcher { get; set; }
    private ModDrawParticleEffectBlockBreak particleEffectBlockBreak;

    // -------------------------------------------------------------------------
    // UI / menus
    // -------------------------------------------------------------------------

    public MenuState MenuState { get; set; }
    public bool EscapeMenuRestart { get; set; }

    private int ChatLinesMax;
    public List<Chatline> ChatLines { get; set; }

    private float accumulator;
    private int lastWidth;
    private int lastHeight;
    private Random rnd;
    private string[] getAsset;

    // -------------------------------------------------------------------------
    // IGameClient — computed / forwarded members
    // -------------------------------------------------------------------------

    public bool EnableCameraControl { set => enableCameraControl = value; }
    public Dictionary<string, string> PerformanceInfo => performanceinfo;

    public float LocalEyeHeight =>
        Entities[LocalPlayerId].drawModel.eyeHeight;

    public int FreemoveLevel
    {
        get
        {
            if (!Controls.FreeMove) return FreemoveLevelEnum.None;
            return Controls.NoClip ? FreemoveLevelEnum.Noclip : FreemoveLevelEnum.Freemove;
        }
        set
        {
            Controls.FreeMove = value != FreemoveLevelEnum.None;
            Controls.NoClip = value == FreemoveLevelEnum.Noclip;
        }
    }

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
    // Initialisation helpers
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
        taskScheduler = new TaskScheduler(this, Platform);
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
        TextureId = [];
        TextureIdForInventory = [];
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
        WallDistance = 0.3f;
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
        Camera = Matrix4.Identity;
        mvMatrix = new();
        pMatrix = new();
        mvMatrix.Push(Matrix4.Identity);
        pMatrix.Push(Matrix4.Identity);

        CameraEye = Vector3.Zero;
        CameraMatrix = new CameraMatrixProvider();
        fov = MathF.PI / 3;
        CameraType = CameraType.Fpp;
        EnableTppView = false;
        znear = 1f / 10;
        ENABLE_ZFAR = true;
        OverHeadCameraDistance = 10;
        OverheadCameraK = new Camera();
        TppCameraDistance = 3;
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
        KeyboardStateRaw = new bool[KeysMax];
    }

    private void InitOptions()
    {
        ENABLE_DRAW2D = true;
        EnableLog = 0;
        AllowFreeMove = true;
        MenuState = new MenuState();
    }

    private void InitNetworking()
    {
        PacketHandlers = new ClientPacketHandler[256];
        NewBlockTypes = [];
        speculativeCount = 0;
        speculative = new Speculative?[speculativeMax];
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