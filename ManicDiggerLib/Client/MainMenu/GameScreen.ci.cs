using OpenTK.Windowing.Common;

public class ScreenGame : Screen
{
    public ScreenGame()
    {
        game = new Game();
    }
    private readonly Game game;

    public void Start(IGamePlatform platform_, bool singleplayer_, string singleplayerSavePath_, ConnectData connectData_)
    {
        platform = platform_;
        singleplayer = singleplayer_;
        singleplayerSavePath = singleplayerSavePath_;
        connectData = connectData_;

        game.platform = platform;
        game.issingleplayer = singleplayer;
        game.assets = menu.assets;
        game.assetsLoadProgress = menu.assetsLoadProgress;

        game.Start();
        Connect(platform);
    }

    private ServerSimple serverSimple;
    private ModServerSimple serverSimpleMod;

    private void Connect(IGamePlatform platform)
    {
        if (singleplayer)
        {
            DummyNetwork network = platform.SinglePlayerServerGetNetwork();

            if (platform.SinglePlayerServerAvailable())
            {
                platform.SinglePlayerServerStart(singleplayerSavePath);
            }
            else
            {
                DummyNetServer server = new(network);
                server.Start();

                serverSimple = new ServerSimple();
                serverSimple.Start(server, singleplayerSavePath, platform);

                serverSimpleMod = new ModServerSimple { server = serverSimple };
                game.AddMod(serverSimpleMod);
            }

            network.ServerInbox.Enqueue([]);
            game.main = new DummyNetClient(network);

            connectData = new ConnectData { Username = "Local" };
            game.connectdata = connectData;
        }
        else
        {
            game.connectdata = connectData;
            game.main = CreateNetClient(platform)
                ?? throw new InvalidOperationException("No network transport available.");
        }
    }

    private static NetClient? CreateNetClient(IGamePlatform platform)
    {
        if (platform.TcpAvailable())
            return new TcpNetClient();

        if (platform.EnetAvailable())
            return new EnetNetClient(platform);

        if (platform.WebSocketAvailable())
            return new WebSocketNetClient();

        return null;
    }

    private IGamePlatform platform;
    private ConnectData connectData;
    private bool singleplayer;
    private string singleplayerSavePath;

    public override void Render(float dt)
    {
        if (game.reconnect)
        {
            game.Dispose();
            menu.StartGame(singleplayer, singleplayerSavePath, connectData);
            return;
        }
        if (game.exitToMainMenu)
        {
            game.Dispose();
            if (game.GetRedirect() != null)
            {
                var (qresult, message) = Task.Run(() => new QueryClient(platform).QueryAsync(game.GetRedirect().GetIP(), game.GetRedirect().GetPort())).GetAwaiter().GetResult();

                if (qresult == null)
                {
                    platform.MessageBoxShowError(message, "Redirection error");
                    menu.StartMainMenu();
                    return;
                }

                LoginClientCi lic = new();
                LoginData lidata = new();
                string token = qresult.PublicHash.Split('=')[1];
                lic.Login(platform, connectData.Username, "", token, platform.GetPreferences().GetString("Password", ""), new LoginResultRef(), lidata);
                while (lic.loginResult.value == LoginResult.Connecting)
                {
                    lic.Update(platform);
                }
                //Check if login was successful
                if (!lidata.ServerCorrect)
                {
                    //Invalid server adress
                    platform.MessageBoxShowError("Invalid server address!", "Redirection error!");
                    menu.StartMainMenu();
                }
                else if (!lidata.PasswordCorrect)
                {
                    //Authentication failed
                    menu.StartLogin(token, null, 0);
                }
                else if (lidata.ServerAddress != null && lidata.ServerAddress != "")
                {
                    //Finally switch to the new server
                    menu.ConnectToGame(lidata, connectData.Username);
                }
            }
            else
            {
                menu.StartMainMenu();
            }
            return;
        }
        game.OnRenderFrame(dt);
    }

    public override void OnKeyDown(KeyEventArgs e)
    {
        game.KeyDown(e);
    }

    public override void OnKeyUp(KeyEventArgs e)
    {
        game.KeyUp(e);
    }

    public override void OnKeyPress(KeyPressEventArgs e)
    {
        game.KeyPress(e);
    }

    public override void OnMouseDown(MouseEventArgs e)
    {
        if (!game.platform.Focused())
        {
            return;
        }
        game.MouseDown(e);
    }

    public override void OnMouseMove(MouseEventArgs e)
    {
        if (!game.platform.Focused())
        {
            return;
        }
        game.MouseMove(e);
    }

    public override void OnMouseUp(MouseEventArgs e)
    {
        if (!game.platform.Focused())
        {
            return;
        }
        game.MouseUp(e);
    }

    public override void OnMouseWheel(MouseWheelEventArgs e)
    {
        game.MouseWheelChanged(e);
    }

    public override void OnTouchStart(TouchEventArgs e)
    {
        game.OnTouchStart(e);
    }

    public override void OnTouchMove(TouchEventArgs e)
    {
        game.OnTouchMove(e);
    }

    public override void OnTouchEnd(TouchEventArgs e)
    {
        game.OnTouchEnd(e);
    }

    public override void OnBackPressed()
    {
        Game.OnBackPressed();
    }
}
