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
        Language.LoadTranslations();

        // ── Core data / config ────────────────────────────────────────────────
        BlockTypeRegistry gamedata = new();
        gamedata.Start();
        BlockRegistry = gamedata;

        Config3d config3d = new()
        {
            ViewDistance = Platform.IsFastSystem() ? ViewDistanceFast : ViewDistanceSlow
        };
        Config3d = config3d;

        // ── Rendering subsystems ──────────────────────────────────────────────

        FrustumCulling = new() { CameraMatrix = CameraMatrix };

        TerrainChunkTesselator = new TerrainChunkTesselator(this, Platform);

        Batcher = new MeshBatcher(Platform, this);

        SunMoonRenderer = new(this);

        // ── World / map ───────────────────────────────────────────────────────
        VoxelMap.Reset(DefaultMapSizeX, DefaultMapSizeY, DefaultMapSizeZ);
        Heightmap = new ChunkedMap2d<int>(VoxelMap.MapSizeX, VoxelMap.MapSizeY);

        // ── Inventory ─────────────────────────────────────────────────────────
        Packet_Inventory inventory = new() { RightHand = new Packet_Item[10] };
        InventoryUtils dataItems = new(this);
        Inventory = inventory;
        InventoryUtil = new InventoryUtilClient(inventory, dataItems);

        // ── Misc ──────────────────────────────────────────────────────────────
        rnd = new Random();
        Platform.AddOnCrash(OnCrashHandlerLeave.Create(this));

        // ── Mods ──────────────────────────────────────────────────────────────
        InitMods();

        BlockOctreeSearcher = new();

        // Prevent the loading screen from immediately showing the lag symbol.
        LastReceivedMilliseconds = Platform.TimeMillisecondsFromStart;
        EnableDrawTestCharacter = Platform.IsDebuggerAttached();

        int detectedSize = Platform.GlGetMaxTextureSize();
        maxTextureSize = Math.Max(detectedSize, 1024);

        taskScheduler.Initialise();
        MapLoadingStart();
    }

    private void InitMods()
    {
        ClientMods = [];

        // ── Core loop ─────────────────────────────────────────────────────────
        AddMod(new ModDrawMain(this));
        AddMod(new ModUpdateMain(this));
        AddMod(new ModNetworkProcess(this, Platform));
        AddMod(new ModNetworkEntity(this));
        AddMod(new ModUnloadRendererChunks(this, Platform));
        AddMod(new ModDiagLog(this));

        // ── Camera ────────────────────────────────────────────────────────────
        AddMod(new ModAutoCamera(this, Platform));
        AddMod(new ModCameraKeys(this, Platform));
        AddMod(new ModCamera(this));

        // ── Player logic ──────────────────────────────────────────────────────
        AddMod(new ModFallDamageToPlayer(this, Platform));
        AddMod(new ModBlockDamageToPlayer(this, Platform));
        AddMod(new ModLoadPlayerTextures(this, Platform));
        AddMod(new ModSendPosition(this, Platform));
        AddMod(new ModInterpolatePositions(this));
        AddMod(new ModPush(this));
        AddMod(new ModFly(this));

        // ── Gameplay mechanics ────────────────────────────────────────────────
        AddMod(new ModRail(this, Platform));
        AddMod(new ModCompass(this, Platform));
        AddMod(new ModGrenade(this));
        AddMod(new ModBullet(this));
        AddMod(new ModExpire(this));
        AddMod(new ModPicking(this, Platform));

        // ── Inventory / ammo ──────────────────────────────────────────────────
        AddMod(new ModReloadAmmo(this, Platform));
        AddMod(new ModSendActiveMaterial(this));
        AddMod(new ModGuiCrafting(this));
        AddMod(new ModGuiInventory(this, Platform));

        // ── Audio ─────────────────────────────────────────────────────────────
        AddMod(new ModWalkSound(this));
        AddMod(new ModAudio(this, Platform));

        // ── Sky / environment (must precede terrain) ──────────────────────────
        if (Platform.IsFastSystem())
            AddMod(new ModSkySphereAnimated(this, Platform));
        else
            AddMod(new ModSkySphereStatic(this, Platform));
        AddMod(SunMoonRenderer);

        // ── World rendering ───────────────────────────────────────────────────
        AddMod(new ModDrawTerrain(this, Platform));
        AddMod(new ModDrawArea(this, Platform));
        AddMod(new ModDrawSprites(this));
        AddMod(new ModDrawMinecarts(this, Platform));
        AddMod(new ModDrawLinesAroundSelectedBlock(this, Platform));
        AddMod(new ModDebugChunk(this, Platform));

        // ── Particle effects ──────────────────────────────────────────────────
        // Create the instance, store the field reference, then register as a mod
        // so that both the field and the entry in clientmods point to the same
        // object. Previously InitSubsystems created one instance and InitMods
        // added a second independent instance — the field reference was dead.
        particleEffectBlockBreak = new ModDrawParticleEffectBlockBreak();
        AddMod(particleEffectBlockBreak);

        // ── Entity / player rendering ─────────────────────────────────────────
        AddMod(new ModDrawPlayers(this, Platform));
        AddMod(new ModDrawPlayerNames(this));
        AddMod(new ModDrawTestModel(this, Platform));
        AddMod(new ModClearInactivePlayersDrawInfo(this, Platform));

        // ── HUD / 2D overlay ──────────────────────────────────────────────────
        AddMod(new ModDrawHand2d(this, Platform));
        AddMod(new ModDrawHand3d(this, Platform));
        AddMod(new ModDrawText(this));
        AddMod(new ModDraw2dMisc(this, Platform));
        AddMod(new ModFpsHistoryGraph(this, Platform));

        // ── GUI (topmost — rendered last) ─────────────────────────────────────
        AddMod(new ModDialog(this, Platform));
        AddMod(new ModGuiTouchButtons(this, Platform));
        AddMod(new ModGuiEscapeMenu(this, Platform));
        AddMod(new ModGuiMapLoading(this, Platform));
        AddMod(new ModGuiPlayerStats(this, Platform));
        AddMod(new ModGuiChat(this, Platform));
        AddMod(new ModScreenshot(this, Platform));
    }

    private void InitRenderer()
    {
        Platform.GlClearColorRgbaf(0, 0, 0, 1);

        if (Config3d.EnableBackfaceCulling)
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
        ClientMods.Add(mod);
    }
}