using ManicDigger.Mods;
using System.Diagnostics;
using System.Text;

namespace ManicDigger.Maui;

public partial class App : Application
{
    public App()
    {
        InitializeComponent();
        MainPage = new ContentPage(); // empty, never seen
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        Window window = base.CreateWindow(activationState);

        // Hide the MAUI window — OpenTK will create its own
        window.Width = 0;
        window.Height = 0;
        window.X = -9999;
        window.Y = -9999;

        // Start the game once the window is created
        window.Created += OnWindowCreated;

        return window;
    }

    private void OnWindowCreated(object? sender, EventArgs e)
    {
        // Run on a background thread so we don't block the MAUI UI thread
        // OpenTK's Run() is blocking so it must not run on the main thread
        MainThread.BeginInvokeOnMainThread(static () =>
        {
            GameRunner.Start([.. Environment.GetCommandLineArgs().Skip(1)]);
        });
    }
}

public class GameRunner
{
    /// <summary>The application-wide DI container, available after <see cref="Main"/> returns.</summary>
    public static IServiceProvider ServiceProvider { get; private set; }

    // ── Service registration ──────────────────────────────────────────────────

    private static void ConfigureServices(ServiceCollection services)
    {
        // ── Infrastructure ────────────────────────────────────────────────────
        services.AddSingleton<GameWindowNative>();
        services.AddSingleton<IVoxelMap, VoxelMap>();
        services.AddSingleton<IGameExitService, GameExitService>();
        services.AddSingleton<IGameService, GameService>();
        services.AddSingleton<ICameraService, CameraService>();
        services.AddSingleton<IAudioService, AudioService>();
        services.AddSingleton<IPreferences, Preferences>();
        services.AddSingleton<IOpenGlService, OpenGlService>();
        services.AddSingleton<IFrustumCulling, FrustumCulling>();
        services.AddSingleton<IMeshBatcher, MeshBatcher>();
        services.AddSingleton<IMeshDrawer, MeshDrawer>();
        services.AddSingleton<ISinglePlayerService, SinglePlayerService>();
        services.AddSingleton<IDummyNetwork, DummyNetwork>();
        services.AddSingleton<IScreenManager, ScreenManager>();
        services.AddSingleton<IModRegistry, ModRegistry>();
        services.AddSingleton<ITaskScheduler, TaskScheduler>();
        services.AddSingleton<IBlockRegistry, BlockRegistry>();
        services.AddSingleton<IGame, Game>();

        // ── Player logic ──────────────────────────────────────────────────────
        services.AddScoped<IModBase, ModDrawMain>();
        services.AddScoped<IModBase, ModUpdateMain>();
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
        services.AddScoped<IModBase, ModSunMoon>();
        services.AddScoped<IModBase, ModDrawTerrain>();
        services.AddScoped<IModBase, ModDrawArea>();
        services.AddScoped<IModBase, ModDrawSprites>();
        services.AddScoped<IModBase, ModDrawMinecarts>();
        services.AddScoped<IModBase, ModDrawLinesAroundSelectedBlock>();
        services.AddScoped<IModBase, ModDebugChunk>();
        services.AddScoped<IModBase, ModDrawParticleEffectBlockBreak>();

        // ── Entity / player rendering ─────────────────────────────────────────
        services.AddScoped<IModBase, ModDrawPlayers>();
        services.AddScoped<IModBase, ModDrawPlayerNames>();
        services.AddScoped<IModBase, ModDrawTestModel>();
        services.AddScoped<IModBase, ModClearInactivePlayersDrawInfo>();

        // ── HUD / 2D overlay ──────────────────────────────────────────────────
        services.AddScoped<IModBase, ModDrawHand2d>();
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
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    public static void Start(string[] args)
    {
        if (!Debugger.IsAttached)
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(AppContext.BaseDirectory)!;
        }

        ServiceCollection services = new();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

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