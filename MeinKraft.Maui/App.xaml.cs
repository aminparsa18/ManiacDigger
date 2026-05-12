using System.Diagnostics;

namespace MeinKraft.Maui;

public partial class App : Application
{
    private readonly IModRegistry _modRegistry;
    private readonly IEnumerable<IModBase> _mods;

    public App(IModRegistry modRegistry, IEnumerable<IModBase> mods)
    {
        InitializeComponent();
        _modRegistry = modRegistry;
        _mods = mods;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        // Initialise mods before any screen is shown.
        // This is safe here — CreateWindow runs on the main thread before
        // the first frame, so all singletons are fully resolved by DI.
        if (!Debugger.IsAttached)
            Environment.CurrentDirectory = Path.GetDirectoryName(AppContext.BaseDirectory)!;

        _modRegistry.Initialise(_mods);

        return new Window(new AppShell()
        {
            Title = "Mein Kraft"
        });
    }
}