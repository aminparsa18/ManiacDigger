using ManicDigger;
using ManicDigger.Extensions;
using ManicDigger.Mods;
using ManicDigger.Mods.Fortress;
using ManicDigger.Worker;
using MessagePipe;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

public class Program
{
    /// <summary>The application-wide DI container, available after <see cref="Main"/> returns.</summary>
    public static IServiceProvider ServiceProvider { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        _ = new Program(args);
    }

    public Program(string[] args)
    {
        CrashReporter.EnableGlobalExceptionHandling(false);

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
        Start(args);
    }

    // ── Service registration ──────────────────────────────────────────────────

    private static void ConfigureServices(ServiceCollection services)
    {
        services.AddGameLogging(
                    minimumLevel: Serilog.Events.LogEventLevel.Debug,
                    enableConsole: false);

        // Step 3 ─ register CrashReporter itself so its ILogger<T> is injected
        services.AddSingleton<CrashReporter>();

        // ── Infrastructure ────────────────────────────────────────────────────
        services.AddSingleton<GameWindowNative>();
        services.AddSingleton<IVoxelMap, VoxelMap>();
        services.AddSingleton<IGameExit, GameExit>();
        services.AddSingleton<IGameService, GameService>();
        services.AddSingleton<ICameraService, CameraService>();
        services.AddSingleton<IAudioService, AudioService>();
        services.AddSingleton<IPreferences, Preferences>();
        services.AddSingleton<IOpenGlService, OpenGlService>();
        services.AddSingleton<IFrustumCulling, FrustumCulling>();
        services.AddSingleton<ILightManager>(sp => new LightManager(
        sp.GetRequiredService<IVoxelMap>(),
        sp.GetRequiredService<IBlockRegistry>(),
        new Lazy<ILightingWorkQueue>(() => sp.GetRequiredService<ILightingWorkQueue>()),
        sp.GetRequiredService<ISubscriber<BlockChangedEvent>>()));
        services.AddSingleton<IMeshBatcher, MeshBatcher>();
        services.AddSingleton<IMeshDrawer, MeshDrawer>();
        services.AddSingleton<ITerrainChunkTesselator, TerrainChunkTesselator>();
        services.AddSingleton<ISinglePlayerService, SinglePlayerService>();
        services.AddSingleton<IDummyNetwork, DummyNetwork>();
        services.AddSingleton<IScreenFactory, ScreenFactory>();
        services.AddSingleton<IModRegistry, ModRegistry>();
        services.AddSingleton<IModEvents, ModEvents>();
        services.AddSingleton<IAssetManager, AssetManager>();
        services.AddSingleton<ILanguageService, LanguageService>();
        services.AddSingleton<IServerModManager, ServerModManager>();
        services.AddSingleton<IBlockRegistry, BlockRegistry>();
        services.AddSingleton<IBlockChangeNotifier, BlockChangeNotifier>();
        services.AddSingleton<ICompression, CompressionGzip>();
        services.AddSingleton<IChunkDbCompressed, ChunkDbCompressed>();
        services.AddSingleton<IChunkDbRegion, ChunkDbRegion>();
        services.AddSingleton<IServerMapStorage, ServerMapStorage>();
        services.AddSingleton<IServerConfig, ServerConfig>();
        services.AddSingleton<ISaveGameService, SaveGameService>();
        services.AddSingleton<PlayerStatusService, PlayerStatusService>();
        services.AddSingleton<IClientRegistry, ClientRegistry>();
        services.AddSingleton<IServerPacketService, ServerPacketService>();

        // ScreenManager satisfies both contracts from the same singleton instance.
        services.AddSingleton<ScreenManager>();
        services.AddSingleton<IScreenManager>(sp => sp.GetRequiredService<ScreenManager>());
        services.AddSingleton<INavigator>(sp => sp.GetRequiredService<ScreenManager>());

        services.AddSingleton<IGame, Game>();

        // ── Player logic ──────────────────────────────────────────────────────
        services.AddScoped<IModBase, ModNetworkProcess>();
        services.AddScoped<IModBase, ModNetworkEntity>();
        services.AddScoped<IModBase, ModFallDamageToPlayer>();
        services.AddScoped<IModBase, ModBlockDamageToPlayer>();
        services.AddScoped<IModBase, ModLoadPlayerTextures>();
        services.AddScoped<IModBase, ModSendPosition>();
        services.AddScoped<IModBase, ModInterpolatePositions>();
        services.AddScoped<IModBase, ModPush>();
        services.AddScoped<IModBase, ModFly>();

        // ── Camera ────────────────────────────────────────────────────────────
        services.AddScoped<IModBase, ModAutoCamera>();
        services.AddScoped<IModBase, ModCameraKeys>();
        services.AddScoped<IModBase, ModCamera>();

        // ── Gameplay mechanics ────────────────────────────────────────────────
        services.AddScoped<IModBase, ModRail>();
        services.AddScoped<IModBase, ModCompass>();
        services.AddScoped<IModBase, ModGrenade>();
        services.AddScoped<IModBase, ModBullet>();
        services.AddScoped<IModBase, ModExpire>();
        services.AddScoped<IModBase, ModPicking>();

        // ── Inventory / ammo ──────────────────────────────────────────────────
        services.AddScoped<IModBase, ModReloadAmmo>();
        services.AddScoped<IModBase, ModSendActiveMaterial>();
        services.AddScoped<IModBase, ModGuiCrafting>();
        services.AddScoped<IModBase, ModGuiInventory>();

        // ── Audio ─────────────────────────────────────────────────────────────
        services.AddScoped<IModBase, ModWalkSound>();
        services.AddScoped<IModBase, ModAudio>();

        // ── World rendering ───────────────────────────────────────────────────
        services.AddScoped<IModBase, ModSkySphereAnimated>();
        services.AddScoped<IModBase, SunMoonRenderer>();
        services.AddScoped<IModBase, ModDrawTerrain>();
        services.AddScoped<IModBase, ModDrawSprites>();
        services.AddScoped<IModBase, ModDrawMinecarts>();
        services.AddScoped<IModBase, ModDrawLinesAroundSelectedBlock>();

        // ── Entity / player rendering ─────────────────────────────────────────
        services.AddScoped<IModBase, ModDrawPlayers>();
        services.AddScoped<IModBase, ModDrawPlayerNames>();
        services.AddScoped<IModBase, ModDrawTestModel>();
        services.AddScoped<IModBase, ModClearInactivePlayersDrawInfo>();

        // ── HUD / 2D overlay ──────────────────────────────────────────────────
        services.AddScoped<IModBase, ModDrawHand3d>();
        services.AddScoped<IModBase, ModDrawText>();
        services.AddScoped<IModBase, ModDraw2dMisc>();
        services.AddScoped<IModBase, ModFpsHistoryGraph>();

        // ── GUI (topmost — rendered last) ─────────────────────────────────────
        services.AddScoped<IModBase, ModDialog>();
        services.AddScoped<IModBase, ModGuiTouchButtons>();
        services.AddScoped<IModBase, ModGuiEscapeMenu>();
        services.AddScoped<IModBase, ModGuiMapLoading>();
        services.AddScoped<IModBase, ModGuiPlayerStats>();
        services.AddScoped<IModBase, ModGuiChat>();
        services.AddScoped<IModBase, ModScreenshot>();

        services.AddScoped<IMod, Core>();
        services.AddScoped<IMod, CoreBlocks>();
        services.AddScoped<IMod, AdvanceWorldGenerator>();
        services.AddScoped<IMod, BuildLog>();
        services.AddScoped<IMod, CoreCrafting>();
        services.AddScoped<IMod, CoreEvents>();
        services.AddScoped<IMod, Doors>();
        services.AddScoped<IMod, Food>();
        services.AddScoped<IMod, Ghost>();
        services.AddScoped<IMod, PlayerList>();
        services.AddScoped<IMod, RememberPosition>();
        services.AddScoped<IMod, Revert>();
        services.AddScoped<IMod, SandPhysics>();
        services.AddScoped<IMod, Tnt>();
        services.AddScoped<IMod, TreeGenerator>();
        services.AddScoped<IMod, VandalFinder>();
        services.AddScoped<IMod, VegetationGrowth>();

       // services.AddSingleton<ModBootstrapper>();

        services.AddScoped<IMainScreen, MainScreen>();
        services.AddScoped<IScreenGame, ScreenGame>();
        services.AddScoped<ISingleplayerScreen, SingleplayerScreen>();
        services.AddScoped<IScreenMultiplayer, MultiplayerScreen>();

        services.AddSingleton<Server>();
        services.AddSingleton<GameTimer>();
        services.AddSingleton<ServerSystemLoadFirst>();
        services.AddSingleton<ServerSystemLoadConfig>();
        services.AddSingleton<ServerSystemHeartbeat>();
        services.AddSingleton<ServerSystemHttpServer>();
        services.AddSingleton<ServerSystemUnloadUnusedChunks>();
        services.AddSingleton<ServerSystemNotifyMap>();
        services.AddSingleton<ServerSystemNotifyPing>();
        services.AddSingleton<ServerSystemChunksSimulation>();
        services.AddSingleton<ServerSystemBanList>();
        services.AddSingleton<ServerSystemModLoader>();
        services.AddSingleton<ServerSystemLoadServerClient>();
        services.AddSingleton<ServerSystemNotifyEntities>();
        services.AddSingleton<ServerSystemLoadLast>();

        services.AddSingleton<ServerSystemBootstraper>();

        services.AddWorkerInfrastructure();

    }

    // ── Startup ───────────────────────────────────────────────────────────────

    private static void Start(string[] args)
    {
        if (!Debugger.IsAttached)
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(Application.ExecutablePath)!;
        }

        // 1. Mods constructed — each gets IGame injected (Game already exists)
        IEnumerable<IModBase> mods = ServiceProvider.GetServices<IModBase>();

        // 2. Registry populated — Game.ClientMods and any other IModRegistry 
        //    consumer now see the full list
        IModRegistry registry = ServiceProvider.GetRequiredService<IModRegistry>();
        registry.Initialise(mods);

        // 3. Loop starts — ClientMods is fully populated
        ServiceProvider.GetRequiredService<IScreenManager>().Start(args);
    }
}