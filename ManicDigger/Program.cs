#region Using Statements
using OpenTK.Graphics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.GraphicsLibraryFramework;
using Serilog;
using System.Diagnostics;
#endregion

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
        dummyNetwork.Start(new MonitorObject(), new MonitorObject());

        Start(args);
    }

    // ── Fields ────────────────────────────────────────────────────────────────

    private readonly DummyNetwork dummyNetwork;
    private string savefilename;
    public GameExit exit = new();
    private GamePlatformNative platform;

    // ── Start ─────────────────────────────────────────────────────────────────

    private void Start(string[] args)
    {
        string appPath = Path.GetDirectoryName(Application.ExecutablePath)!;
        if (!Debugger.IsAttached)
            Environment.CurrentDirectory = appPath;

        Log.Debug("Initialising GamePlatformNative");

        GamePlatformNative platform = new()
        {
            crashreporter = new CrashReporter(),
            singlePlayerServerDummyNetwork = dummyNetwork
        };
        platform.SetExit(exit);

        this.platform = platform;
        platform.StartSinglePlayerServer = (filename) =>
        {
            savefilename = filename;
            new Thread(ServerThreadStart) { IsBackground = true }.Start();
        };

        Log.Debug("Creating GameWindowNative");
        using GameWindowNative game = new();
        platform.window = game;
        game.platform = platform;

        MainMenu mainmenu = new();
        mainmenu.Start(platform);
        ReadArgs(mainmenu, args);

        platform.Start();
        game.Run();
    }

    private static void ReadArgs(MainMenu mainmenu, string[] args)
    {
        if (args.Length > 0)
        {
            var connectdata = ConnectData.FromUri(new(args[0]));
            mainmenu.StartGame(false, null, connectdata);
        }
    }

    // ── Server thread ─────────────────────────────────────────────────────────

    public void ServerThreadStart()
    {
        Log.Debug("Single-player server thread started");
        try
        {
            Server server = new()
            {
                SaveFilenameOverride = savefilename,
                exit = exit,
                mainSockets = new NetServer[3]
            };

            DummyNetServer netServer = new();
            netServer.SetPlatform(new GamePlatformNative());
            netServer.SetNetwork(dummyNetwork);
            server.mainSockets[0] = netServer;

            for (; ; )
            {
                server.Process();
                Thread.Sleep(1);
                platform.singlePlayerServerLoaded = true;

                if (exit?.GetExit() == true) { server.Stop(); break; }

                if (platform.singlepLayerServerExit)
                {
                    server.Exit();
                    platform.singlepLayerServerExit = false;
                }
            }

            exit.SetExit(false);
            Log.Debug("Single-player server thread stopped cleanly");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Server thread crashed");
            MessageBox.Show(ex.ToString(), "Server Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}