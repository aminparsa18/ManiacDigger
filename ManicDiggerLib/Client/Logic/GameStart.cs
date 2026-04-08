using ManicDigger;
using ManicDigger.Mods;
using Microsoft.CodeAnalysis.Operations;

public partial class Game
{
    public void Start()
    {
        InitSubsystems();
        InitRenderer();
    }

    // -------------------------------------------------------------------------
    // Start helpers
    // -------------------------------------------------------------------------

    private void InitSubsystems()
    {
        textColorRenderer = new TextColorRenderer { platform = platform };
        language.LoadTranslations();

        GameData gamedata = new();
        gamedata.Start();

        Config3d config3d = new();
        config3d.viewdistance = platform.IsFastSystem() ? 128 : 32;

        ITerrainTextures terrainTextures = new() { game = this };
        d_TextureAtlasConverter = new TextureAtlasConverter();
        d_TerrainTextures = terrainTextures;

        FrustumCulling frustumculling = new() { d_GetCameraMatrix = CameraMatrix };
        d_FrustumCulling = frustumculling;

        TerrainChunkTesselatorCi terrainchunktesselator = new();
        d_TerrainChunkTesselator = terrainchunktesselator;
        d_Batcher = new MeshBatcher
        {
            d_FrustumCulling = frustumculling,
            game = this
        };
        d_FrustumCulling = frustumculling;
        d_Data = gamedata;
        d_DataMonsters = new GameDataMonsters();
        d_Config3d = config3d;

        particleEffectBlockBreak = new ModDrawParticleEffectBlockBreak();
        d_Data = gamedata;
        d_TerrainTextures = terrainTextures;

        map.Reset(256, 256, 128);

        SunMoonRenderer sunmoonrenderer = new();
        d_SunMoonRenderer = sunmoonrenderer;

        d_Heightmap = new InfiniteMapChunked2d { d_Map = this };
        d_Heightmap.Restart();
        d_TerrainChunkTesselator = terrainchunktesselator;
        terrainchunktesselator.game = this;

        Packet_Inventory inventory = new() { RightHand = new Packet_Item[10] };
        InventoryUtils dataItems = new(this);
        d_Inventory = inventory;
        d_InventoryUtil = new InventoryUtilClient(inventory, dataItems);

        platform.AddOnCrash(OnCrashHandlerLeave.Create(this));

        rnd = new Random();

        InitMods();

        s = new();

        // Prevent loading screen from immediately displaying lag symbol.
        LastReceivedMilliseconds = platform.TimeMillisecondsFromStart();
        ENABLE_DRAW_TEST_CHARACTER = platform.IsDebuggerAttached();

        int detectedSize = platform.GlGetMaxTextureSize();
        maxTextureSize = Math.Max(detectedSize, 1024);

        taskScheduler.Initialise(this);
        MapLoadingStart();
    }

    private void InitMods()
    {
        clientmods = new ModBase[128];
        clientmodsCount = 0;
        modmanager.game = this;

        // Core loop
        AddMod(new ModDrawMain());
        AddMod(new ModUpdateMain());
        AddMod(new ModNetworkProcess());
        AddMod(new ModNetworkEntity());
        AddMod(new ModUnloadRendererChunks());

        // Camera
        AddMod(new ModAutoCamera());
        AddMod(new ModCameraKeys());
        AddMod(new ModCamera());

        // Player logic
        AddMod(new ModFallDamageToPlayer());
        AddMod(new ModBlockDamageToPlayer());
        AddMod(new ModLoadPlayerTextures());
        AddMod(new ModSendPosition());
        AddMod(new ModInterpolatePositions());
        AddMod(new ModPush());

        // Gameplay mechanics
        AddMod(new ModRail());
        AddMod(new ModCompass());
        AddMod(new ModGrenade());
        AddMod(new ModBullet());
        AddMod(new ModExpire());
        AddMod(new ModPicking());

        // Inventory / ammo
        AddMod(new ModReloadAmmo());
        AddMod(new ModSendActiveMaterial());
        AddMod(new ModGuiCrafting());
        AddMod(new ModGuiInventory());

        // Audio
        AddMod(new ModWalkSound());
        AddMod(new ModAudio());

        // Sky / environment (before terrain)
        if (platform.IsFastSystem())
            AddMod(new ModSkySphereAnimated());
        else
            AddMod(new ModSkySphereStatic());
        AddMod(d_SunMoonRenderer);

        // World rendering
        AddMod(new ModDrawTerrain());
        AddMod(new ModDrawArea());
        AddMod(new ModDrawSprites());
        AddMod(new ModDrawMinecarts());
        AddMod(new ModDrawLinesAroundSelectedBlock());
        AddMod(new ModDebugChunk());
        AddMod(new ModDrawParticleEffectBlockBreak());

        // Entity / player rendering
        AddMod(new ModDrawPlayers());
        AddMod(new ModDrawPlayerNames());
        AddMod(new ModDrawTestModel());
        AddMod(new ModClearInactivePlayersDrawInfo());

        // HUD / 2D overlay
        AddMod(new ModDrawHand2d());
        AddMod(new ModDrawHand3d());
        AddMod(new ModDrawText());
        AddMod(new ModDraw2dMisc());
        AddMod(new ModFpsHistoryGraph());

        // GUI (topmost, rendered last)
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
        clientmods[clientmodsCount++] = mod;
        mod.Start(modmanager);
    }
}