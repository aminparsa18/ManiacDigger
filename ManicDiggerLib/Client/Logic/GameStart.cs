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
        textColorRenderer = new TextColorRenderer { platform = platform };
        language.LoadTranslations();

        // ── Core data / config ────────────────────────────────────────────────
        BlockTypeRegistry gamedata = new();
        gamedata.Start();
        d_Data = gamedata;
        d_DataMonsters = new GameDataMonsters();

        Config3d config3d = new();
        config3d.viewdistance = platform.IsFastSystem() ? ViewDistanceFast : ViewDistanceSlow;
        d_Config3d = config3d;

        // ── Rendering subsystems ──────────────────────────────────────────────
        ITerrainTextures terrainTextures = new() { game = this };
        d_TerrainTextures = terrainTextures;

        FrustumCulling frustumculling = new() { d_GetCameraMatrix = CameraMatrix };
        d_FrustumCulling = frustumculling;

        TerrainChunkTesselatorCi terrainchunktesselator = new();
        terrainchunktesselator.game = this;
        d_TerrainChunkTesselator = terrainchunktesselator;

        d_Batcher = new MeshBatcher
        {
            d_FrustumCulling = frustumculling,
            game = this,
        };

        SunMoonRenderer sunmoonrenderer = new();
        d_SunMoonRenderer = sunmoonrenderer;

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
        platform.AddOnCrash(OnCrashHandlerLeave.Create(this));

        // ── Mods ──────────────────────────────────────────────────────────────
        InitMods();

        s = new();

        // Prevent the loading screen from immediately showing the lag symbol.
        LastReceivedMilliseconds = platform.TimeMillisecondsFromStart;
        ENABLE_DRAW_TEST_CHARACTER = platform.IsDebuggerAttached();

        int detectedSize = platform.GlGetMaxTextureSize();
        maxTextureSize = Math.Max(detectedSize, 1024);

        taskScheduler.Initialise(this);
        MapLoadingStart();
    }

    private void InitMods()
    {
        clientmods = [];
        modmanager.game = this;

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
        if (platform.IsFastSystem())
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
        platform.GlClearColorRgbaf(0, 0, 0, 1);

        if (d_Config3d.ENABLE_BACKFACECULLING)
        {
            platform.GlDepthMask(true);
            platform.GlEnableDepthTest();
            platform.GlCullFaceBack();
            platform.GlEnableCullFace();
        }

        platform.GlEnableLighting();
        platform.GlEnableColorMaterial();
        platform.GlColorMaterialFrontAndBackAmbientAndDiffuse();
        platform.GlShadeModelSmooth();
    }

    public void AddMod(ModBase mod)
    {
        clientmods.Add(mod);
        mod.Start(modmanager);
    }
}