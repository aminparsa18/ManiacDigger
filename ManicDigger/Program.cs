using ManicDigger.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

public class Program
{
    /// <summary>The application-wide DI container, available after <see cref="Main"/> returns.</summary>
    public static IServiceProvider? ServiceProvider { get; private set; }

    [STAThread]
    public static void Main(string[] args)
    {
        _ = new Program(args);
    }

    public Program(string[] args)
    {
        CrashReporter.EnableGlobalExceptionHandling(false);

        ServiceCollection services = new();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();
        Start(args);
    }

    // ── Service registration ──────────────────────────────────────────────────

    private static void ConfigureServices(ServiceCollection services)
    {
        services.AddSharedServices();
        services.AddClientServices();
        services.AddServerServices();

        services.AddClientMods();
        services.AddServerMods();

        services.AddScreens();

        services.AddWorkerInfrastructure();
    }

    // ── Startup ───────────────────────────────────────────────────────────────

    private static void Start(string[] args)
    {
        if (!Debugger.IsAttached)
        {
            Environment.CurrentDirectory = Path.GetDirectoryName(Application.ExecutablePath)!;
        }

        if(ServiceProvider == null)
        {
            throw new InvalidOperationException("ServiceProvider is not initialized.");
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