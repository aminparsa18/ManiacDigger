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

        SunMoonRenderer = new();

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

        s = new();

        // Prevent the loading screen from immediately showing the lag symbol.
        LastReceivedMilliseconds = Platform.TimeMillisecondsFromStart;
        EnableDrawTestCharacter = Platform.IsDebuggerAttached();

        int detectedSize = Platform.GlGetMaxTextureSize();
        maxTextureSize = Math.Max(detectedSize, 1024);

        taskScheduler.Initialise(this);
        MapLoadingStart();
    }

    private void InitMods()
    {
        clientmods = [];

        // ── Core loop ─────────────────────────────────────────────────────────
        AddMod(new ModDrawMain(this));
        AddMod(new ModUpdateMain(this));
        AddMod(new ModNetworkProcess(this, Platform));
        AddMod(new ModNetworkEntity(this));
        AddMod(new ModUnloadRendererChunks(this, Platform));

        // ── Camera ────────────────────────────────────────────────────────────
        AddMod(new ModAutoCamera(this, Platform));
        AddMod(new ModCameraKeys(this));
        AddMod(new ModCamera());

        // ── Player logic ──────────────────────────────────────────────────────
        AddMod(new ModFallDamageToPlayer(this));
        AddMod(new ModBlockDamageToPlayer(this, Platform));
        AddMod(new ModLoadPlayerTextures(this));
        AddMod(new ModSendPosition(this));
        AddMod(new ModInterpolatePositions(this));
        AddMod(new ModPush(this));

        // ── Gameplay mechanics ────────────────────────────────────────────────
        AddMod(new ModRail(this));
        AddMod(new ModCompass(this, Platform));
        AddMod(new ModGrenade(this));
        AddMod(new ModBullet(this));
        AddMod(new ModExpire(this));
        AddMod(new ModPicking());

        // ── Inventory / ammo ──────────────────────────────────────────────────
        AddMod(new ModReloadAmmo(this));
        AddMod(new ModSendActiveMaterial(this));
        AddMod(new ModGuiCrafting(this));
        AddMod(new ModGuiInventory(this, Platform));

        // ── Audio ─────────────────────────────────────────────────────────────
        AddMod(new ModWalkSound(this));
        AddMod(new ModAudio(this));

        // ── Sky / environment (must precede terrain) ──────────────────────────
        if (Platform.IsFastSystem())
            AddMod(new ModSkySphereAnimated());
        else
            AddMod(new ModSkySphereStatic());
        AddMod(SunMoonRenderer);

        // ── World rendering ───────────────────────────────────────────────────
        AddMod(new ModDrawTerrain(this));
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
        AddMod(new ModDrawTestModel(this));
        AddMod(new ModClearInactivePlayersDrawInfo(this, Platform));

        // ── HUD / 2D overlay ──────────────────────────────────────────────────
        AddMod(new ModDrawHand2d());
        AddMod(new ModDrawHand3d());
        AddMod(new ModDrawText());
        AddMod(new ModDraw2dMisc(this, Platform));
        AddMod(new ModFpsHistoryGraph(this, Platform));

        // ── GUI (topmost — rendered last) ─────────────────────────────────────
        AddMod(new ModDialog(this, Platform));
        AddMod(new ModGuiTouchButtons(this, Platform));
        AddMod(new ModGuiEscapeMenu(this));
        AddMod(new ModGuiMapLoading(this));
        AddMod(new ModGuiPlayerStats(this, Platform));
        AddMod(new ModGuiChat(this));
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
        clientmods.Add(mod);
    }
}