using ManicDigger.Mods;

public partial class Game
{
    // ── Startup constants ─────────────────────────────────────────────────────

    /// <summary>Default view distance in blocks on fast (desktop) hardware.</summary>
    private const int ViewDistanceFast = 128;

    /// <summary>Default view distance in blocks on slow (mobile/web) hardware.</summary>
    private const int ViewDistanceSlow = 32;

    /// <summary>Default map dimensions before the server sends its own size.</summary>
    private const int DefaultMapSizeX = 256;
    private const int DefaultMapSizeY = 256;
    private const int DefaultMapSizeZ = 128;

    // ── Entry point ───────────────────────────────────────────────────────────

    public void Start()
    {
        InitSubsystems();
        InitRenderer();
    }

    // ── Start helpers ─────────────────────────────────────────────────────────

    private void InitSubsystems()
    {
        // ── Text / language ───────────────────────────────────────────────────
        language.LoadTranslations();

        // ── Core data / config ────────────────────────────────────────────────
        BlockTypeRegistry gamedata = new();
        gamedata.Start();
        BlockRegistry = gamedata;

        Config3d config3d = new()
        {
            ViewDistance = Platform.IsFastSystem() ? ViewDistanceFast : ViewDistanceSlow
        };
        d_Config3d = config3d;

        // ── Rendering subsystems ──────────────────────────────────────────────

        FrustumCulling = new() { CameraMatrix = CameraMatrix };

        d_TerrainChunkTesselator = new TerrainChunkTesselator(this, Platform);

        d_Batcher = new MeshBatcher(Platform, this);

        d_SunMoonRenderer = new();

        // ── World / map ───────────────────────────────────────────────────────
        VoxelMap.Reset(DefaultMapSizeX, DefaultMapSizeY, DefaultMapSizeZ);
        d_Heightmap = new ChunkedMap2d<int>(VoxelMap.MapSizeX, VoxelMap.MapSizeY);

        // ── Inventory ─────────────────────────────────────────────────────────
        Packet_Inventory inventory = new() { RightHand = new Packet_Item[10] };
        InventoryUtils dataItems = new(this);
        d_Inventory = inventory;
        d_InventoryUtil = new InventoryUtilClient(inventory, dataItems);

        // ── Misc ──────────────────────────────────────────────────────────────
        rnd = new Random();
        Platform.AddOnCrash(OnCrashHandlerLeave.Create(this));

        // ── Mods ──────────────────────────────────────────────────────────────
        InitMods();

        s = new();

        // Prevent the loading screen from immediately showing the lag symbol.
        LastReceivedMilliseconds = Platform.TimeMillisecondsFromStart;
        ENABLE_DRAW_TEST_CHARACTER = Platform.IsDebuggerAttached();

        int detectedSize = Platform.GlGetMaxTextureSize();
        maxTextureSize = Math.Max(detectedSize, 1024);

        taskScheduler.Initialise(this);
        MapLoadingStart();
    }

    private void InitMods()
    {
        clientmods = [];

        // ── Core loop ─────────────────────────────────────────────────────────
        AddMod(new ModDrawMain());
        AddMod(new ModUpdateMain());
        AddMod(new ModNetworkProcess());
        AddMod(new ModNetworkEntity());
        AddMod(new ModUnloadRendererChunks());

        // ── Camera ────────────────────────────────────────────────────────────
        AddMod(new ModAutoCamera());
        AddMod(new ModCameraKeys());
        AddMod(new ModCamera());

        // ── Player logic ──────────────────────────────────────────────────────
        AddMod(new ModFallDamageToPlayer());
        AddMod(new ModBlockDamageToPlayer());
        AddMod(new ModLoadPlayerTextures());
        AddMod(new ModSendPosition());
        AddMod(new ModInterpolatePositions());
        AddMod(new ModPush());

        // ── Gameplay mechanics ────────────────────────────────────────────────
        AddMod(new ModRail());
        AddMod(new ModCompass());
        AddMod(new ModGrenade());
        AddMod(new ModBullet());
        AddMod(new ModExpire());
        AddMod(new ModPicking());

        // ── Inventory / ammo ──────────────────────────────────────────────────
        AddMod(new ModReloadAmmo());
        AddMod(new ModSendActiveMaterial());
        AddMod(new ModGuiCrafting());
        AddMod(new ModGuiInventory());

        // ── Audio ─────────────────────────────────────────────────────────────
        AddMod(new ModWalkSound());
        AddMod(new ModAudio());

        // ── Sky / environment (must precede terrain) ──────────────────────────
        if (Platform.IsFastSystem())
            AddMod(new ModSkySphereAnimated());
        else
            AddMod(new ModSkySphereStatic());
        AddMod(d_SunMoonRenderer);

        // ── World rendering ───────────────────────────────────────────────────
        AddMod(new ModDrawTerrain());
        AddMod(new ModDrawArea());
        AddMod(new ModDrawSprites());
        AddMod(new ModDrawMinecarts());
        AddMod(new ModDrawLinesAroundSelectedBlock());
        AddMod(new ModDebugChunk());

        // ── Particle effects ──────────────────────────────────────────────────
        // Create the instance, store the field reference, then register as a mod
        // so that both the field and the entry in clientmods point to the same
        // object. Previously InitSubsystems created one instance and InitMods
        // added a second independent instance — the field reference was dead.
        particleEffectBlockBreak = new ModDrawParticleEffectBlockBreak();
        AddMod(particleEffectBlockBreak);

        // ── Entity / player rendering ─────────────────────────────────────────
        AddMod(new ModDrawPlayers());
        AddMod(new ModDrawPlayerNames());
        AddMod(new ModDrawTestModel());
        AddMod(new ModClearInactivePlayersDrawInfo());

        // ── HUD / 2D overlay ──────────────────────────────────────────────────
        AddMod(new ModDrawHand2d());
        AddMod(new ModDrawHand3d());
        AddMod(new ModDrawText());
        AddMod(new ModDraw2dMisc());
        AddMod(new ModFpsHistoryGraph());

        // ── GUI (topmost — rendered last) ─────────────────────────────────────
        AddMod(new ModDialog());
        AddMod(new ModGuiTouchButtons());
        AddMod(new ModGuiEscapeMenu());
        AddMod(new ModGuiMapLoading());
        AddMod(new ModGuiPlayerStats());
        AddMod(new ModGuiChat());
        AddMod(new ModScreenshot());
    }

    private void InitRenderer()
    {
        Platform.GlClearColorRgbaf(0, 0, 0, 1);

        if (d_Config3d.EnableBackfaceCulling)
        {
            Platform.GlDepthMask(true);
            Platform.GlEnableDepthTest();
            Platform.GlCullFaceBack();
            Platform.GlEnableCullFace();
        }

        Platform.GlEnableLighting();
        Platform.GlEnableColorMaterial();
        Platform.GlColorMaterialFrontAndBackAmbientAndDiffuse();
        Platform.GlShadeModelSmooth();
    }

    public void AddMod(ModBase mod)
    {
        clientmods.Add(mod);
        mod.Start(this, Platform);
    }
}

/// <summary>
/// Provides mods with controlled access to game state, rendering, chat,
/// and player controls. Implemented by <see cref="Game"/>.
/// </summary>
public interface IGameClient
{
    // -------------------------------------------------------------------------
    // Platform
    // -------------------------------------------------------------------------

    /// <summary>The host platform abstraction (screenshot, canvas size, etc.).</summary>
    IGamePlatform Platform { get; set; }

    // -------------------------------------------------------------------------
    // Player
    // -------------------------------------------------------------------------

    /// <summary>Local player's world-space X position.</summary>
    float LocalPositionX { get; set; }

    /// <summary>Local player's world-space Y position.</summary>
    float LocalPositionY { get; set; }

    /// <summary>Local player's world-space Z position.</summary>
    float LocalPositionZ { get; set; }

    /// <summary>Local player's X orientation (pitch/yaw/roll component).</summary>
    float LocalOrientationX { get; set; }

    /// <summary>Local player's Y orientation component.</summary>
    float LocalOrientationY { get; set; }

    /// <summary>Local player's Z orientation component.</summary>
    float LocalOrientationZ { get; set; }

    // -------------------------------------------------------------------------
    // Movement
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets or sets the current freemove level.
    /// See <see cref="FreemoveLevelEnum"/> for valid values.
    /// </summary>
    int FreemoveLevel { get; set; }

    /// <summary>Whether freemove is permitted by the current server.</summary>
    bool IsFreemoveAllowed { get; }

    // -------------------------------------------------------------------------
    // Camera / GUI
    // -------------------------------------------------------------------------

    /// <summary>When <c>false</c>, the 2-D HUD is hidden.</summary>
    bool EnableDraw2d { get; set; }

    /// <summary>Enables or disables player camera control.</summary>
    bool EnableCameraControl { set; }

    // -------------------------------------------------------------------------
    // Chat
    // -------------------------------------------------------------------------

    /// <summary>Appends a message to the local chat log.</summary>
    void AddChatLine(string message);

    /// <summary>Sends a chat message to the server.</summary>
    void SendChat(string message);

    // -------------------------------------------------------------------------
    // Rendering
    // -------------------------------------------------------------------------

    /// <summary>Returns the OpenGL ID of the engine's built-in white texture.</summary>
    int WhiteTexture();

    /// <inheritdoc cref="Game.Draw2dTexture"/>
    void Draw2dTexture(int textureid, int x, int y, int width, int height,
                       int inAtlasId, int atlasIndex, int color, bool blend);

    /// <inheritdoc cref="Game.Draw2dTextures"/>
    void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureId);

    /// <inheritdoc cref="Game.Draw2dText"/>
    void Draw2dText(string text, Font font, float x, float y, object extra, bool shadow);

    /// <summary>Switches the GL projection to orthographic for 2-D rendering.</summary>
    void OrthoMode(int width, int height);

    /// <summary>Restores the GL projection to perspective for 3-D rendering.</summary>
    void PerspectiveMode();

    // -------------------------------------------------------------------------
    // Diagnostics
    // -------------------------------------------------------------------------

    /// <summary>Live performance counters exposed to mods for overlay display.</summary>
    Dictionary<string, string> PerformanceInfo { get; }
}