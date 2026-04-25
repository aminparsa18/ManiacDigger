using Serilog;
using System.Diagnostics;

public class Program
{

    [STAThread]
    public static void Main(string[] args)
    {
        CrashReporter.DefaultFileName = "ManicDiggerClientCrash.txt";
        CrashReporter.EnableGlobalExceptionHandling(isConsole: false);
        _ = new Program(args);
    }

    public Program(string[] args)
    {
        dummyNetwork = new DummyNetwork();
        Start(args);
    }

    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    private readonly DummyNetwork dummyNetwork;
    private string savefilename;
    public GameExit exit = new();
    private GamePlatformNative platform;

    // -------------------------------------------------------------------------
    // Startup
    // -------------------------------------------------------------------------

    private void Start(string[] args)
    {
        if (!Debugger.IsAttached)
            Environment.CurrentDirectory = Path.GetDirectoryName(Application.ExecutablePath)!;

        Log.Debug("Initialising GamePlatformNative");

        platform = new GamePlatformNative
        {
            crashreporter = new CrashReporter(),
            singlePlayerServerDummyNetwork = dummyNetwork
        };
        platform.SetExit(exit);
        platform.StartSinglePlayerServer = filename =>
        {
            savefilename = filename;
            new Thread(ServerThreadStart) { IsBackground = true }.Start();
        };

        Log.Debug("Creating GameWindowNative");
        using GameWindowNative window = new();
        platform.window = window;

        MainMenu mainmenu = new(platform);
        mainmenu.Start();
        ReadArgs(mainmenu, args);

        platform.Start();
        window.Run();
    }

    private static void ReadArgs(MainMenu mainmenu, string[] args)
    {
        if (args.Length > 0)
            mainmenu.StartGame(false, null, ConnectionData.FromUri(new Uri(args[0])));
    }

    // -------------------------------------------------------------------------
    // Single-player server thread
    // -------------------------------------------------------------------------

    public void ServerThreadStart()
    {
        Log.Debug("Single-player server thread started");
        //try
        //{
        DummyNetServer netServer = new(dummyNetwork);

        Server server = new()
        {
            SaveFilenameOverride = savefilename,
            exit = exit,
            mainSockets = new NetServer[3]
        };
        server.mainSockets[0] = netServer;

        while (true)
        {
            server.Process();
            Thread.Sleep(1);
            platform.singlePlayerServerLoaded = true;

            if (exit?.GetExit() == true)
            {
                server.Stop();
                break;
            }

            if (platform.singlepLayerServerExit)
            {
                server.Exit();
                platform.singlepLayerServerExit = false;
            }
        }

        exit.SetExit(false);
        Log.Debug("Single-player server thread stopped cleanly");
        //}
        //catch (Exception ex)
        //{
        //    Log.Error(ex, "Server thread crashed");
        //    MessageBox.Show(ex.ToString(), "Server Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //}
    }
}
