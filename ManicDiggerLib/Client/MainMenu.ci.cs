using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

public class MainMenu
{
    public MainMenu()
    {
        one = 1;
        textures = [];
        textTextures = new TextTexture[256];
        textTexturesCount = 0;
        screen = new MainScreen
        {
            menu = this
        };
        loginClient = new LoginClientCi();
        assets = new();
    }

    internal IGamePlatform p;
    internal Language lang;

    internal float one;

    internal List<Asset> assets;
    internal float assetsLoadProgress;
    internal TextColorRenderer textColorRenderer;

    public void Start(IGamePlatform p_)
    {
        this.p = p_;

        //Initialize translations
        lang = new();
        lang.LoadTranslations();
        p.SetTitle(lang.GameName());

        textColorRenderer = new TextColorRenderer
        {
            platform = p_
        };
        assets = p_.LoadAssetsAsyc(out assetsLoadProgress);

        overlap = 200;
        minspeed = 20;
        rnd = new Random();

        xRot = 0;
        xInv = false;
        xSpeed = minspeed + rnd.Next(5);

        yRot = 0;
        yInv = false;
        ySpeed = minspeed + rnd.Next(5);

        filter = 0;

        mvMatrix = Matrix4.Identity;
        pMatrix = Matrix4.Identity;

        currentlyPressedKeys = new bool[360];
        p.AddOnNewFrame(dt => OnNewFrame(dt));
        p.AddOnKeyEvent(HandleKeyDown, HandleKeyUp, HandleKeyPress);
        p.AddOnMouseEvent(HandleMouseDown, HandleMouseUp, HandleMouseMove, HandleMouseWheel);
        p.AddOnTouchEvent(HandleTouchStart, HandleTouchMove, HandleTouchEnd);
    }

    private int viewportWidth;
    private int viewportHeight;

    private Matrix4 mvMatrix;
    private Matrix4 pMatrix;

    private bool[] currentlyPressedKeys;

    public void HandleKeyDown(KeyEventArgs e)
    {
        currentlyPressedKeys[e.KeyChar] = true;
        screen.OnKeyDown(e);
    }

    public void HandleKeyUp(KeyEventArgs e)
    {
        currentlyPressedKeys[e.KeyChar] = false;
        screen.OnKeyUp(e);
    }

    public void HandleKeyPress(KeyPressEventArgs e)
    {
        if (e.KeyChar == 70 || e.KeyChar == 102) // 'F', 'f'
        {
            filter += 1;
            if (filter == 3)
            {
                filter = 0;
            }
        }
        if (e.KeyChar == 96) // '`'
        {
            screen.OnBackPressed();
        }
        screen.OnKeyPress(e);
    }

    private void DrawScene(float dt)
    {
        p.GlViewport(0, 0, viewportWidth, viewportHeight);
        p.GlClearColorBufferAndDepthBuffer();
        p.GlDisableDepthTest();
        p.GlDisableCullFace();

        Matrix4.CreateOrthographicOffCenter(0, p.GetCanvasWidth(), p.GetCanvasHeight(), 0, 0, 10, out pMatrix);

        screen.Render(dt);
    }

    private ScreenBase screen;

    internal void DrawButton(string text, float fontSize, float dx, float dy, float dw, float dh, bool pressed)
    {
        Draw2dQuad(pressed ? GetTexture("button_sel.png") : GetTexture("button.png"), dx, dy, dw, dh);
        
        if ((text != null) && (text != ""))
        {
            DrawText(text, fontSize, dx + dw / 2, dy + dh / 2, TextAlign.Center, TextBaseline.Middle);
        }
    }

    internal void DrawText(string text, float fontSize, float x, float y, TextAlign align, TextBaseline baseline)
    {
        TextTexture t = GetTextTexture(text, fontSize);
        int dx = 0;
        int dy = 0;
        if (align == TextAlign.Center)
        {
            dx -= t.textwidth / 2;
        }
        if (align == TextAlign.Right)
        {
            dx -= t.textwidth;
        }
        if (baseline == TextBaseline.Middle)
        {
            dy -= t.textheight / 2;
        }
        if (baseline == TextBaseline.Bottom)
        {
            dy -= t.textheight;
        }
        Draw2dQuad(t.texture, x + dx, y + dy, t.texturewidth, t.textureheight);
    }

    internal void DrawServerButton(string name, string motd, string gamemode, string playercount, float x, float y, float width, float height, string image)
    {
        //Server buttons default to: (screen width - 200) x 64
        Draw2dQuad(GetTexture("serverlist_entry_background.png"), x, y, width, height);
        Draw2dQuad(GetTexture(image), x, y, height, height);

        //       value          size    x position              y position              text alignment      text baseline
        DrawText(name,          14,     x + 70,                 y + 5,                  TextAlign.Left,     TextBaseline.Top);
        DrawText(gamemode,      12,     x + width - 10,         y + height - 5,         TextAlign.Right,    TextBaseline.Bottom);
        DrawText(playercount,   12,     x + width - 10,         y + 5,                  TextAlign.Right,    TextBaseline.Top);
        DrawText(motd,          12,     x + 70,                 y + height - 5,         TextAlign.Left,     TextBaseline.Bottom);
    }

    private TextTexture GetTextTexture(string text, float fontSize)
    {
        for (int i = 0; i < textTexturesCount; i++)
        {
            TextTexture t = textTextures[i];
            if (t == null)
            {
                continue;
            }
            if (t.text == text && t.size == fontSize)
            {
                return t;
            }
        }
        TextTexture textTexture = new();

        TextStyle text_ = new()
        {
            Text = text,
            FontSize = fontSize,
            FontFamily = "Arial",
            Color = ColorUtils.ColorFromArgb(255, 255, 255, 255)
        };
        Bitmap textBitmap = textColorRenderer.CreateTextTexture(text_);

        int texture = p.LoadTextureFromBitmap(textBitmap);
        
        p.TextSize(text, fontSize, out int textWidth, out int textHeight);

        textTexture.texture = texture;
        textTexture.texturewidth = textBitmap.Width;
        textTexture.textureheight = textBitmap.Height;
        textTexture.text = text;
        textTexture.size = fontSize;
        textTexture.textwidth = textWidth;
        textTexture.textheight = textHeight;

        textBitmap.Dispose();
        
        textTextures[textTexturesCount++] = textTexture;
        return textTexture;
    }

    internal Dictionary<string,int> textures;
    internal int GetTexture(string name)
    {
        if (!textures.TryGetValue(name, out int value))
        {
            Bitmap bmp = PixelBuffer.BitmapFromPng(GetFile(name), GetFileLength(name));
            value = p.LoadTextureFromBitmap(bmp);
            textures[name] = value;
            bmp.Dispose();
        }
        return value;
    }

    internal byte[] GetFile(string name)
    {
        string pLowercase = name.ToLowerInvariant();
        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i].name == pLowercase)
            {
                return assets[i].data;
            }
        }
        return null;
    }

    internal int GetFileLength(string name)
    {
        string pLowercase = name.ToLowerInvariant();
        for (int i = 0; i < assets.Count; i++)
        {
            if (assets[i].name == pLowercase)
            {
                return assets[i].dataLength;
            }
        }
        return 0;
    }

    private GeometryModel cubeModel;
    public void Draw2dQuad(int textureid, float dx, float dy, float dw, float dh)
    {
        mvMatrix = Matrix4.Identity;
        Matrix4.CreateTranslation(dx, dy, 0, out Matrix4 t1);
        mvMatrix = t1 * mvMatrix;
        Matrix4.CreateScale(dw, dh, 0, out Matrix4 s1);
        mvMatrix = s1 * mvMatrix;
        Matrix4.CreateScale(0.5f, 0.5f, 0, out Matrix4 s2);
        mvMatrix = s2 * mvMatrix;
        Matrix4.CreateTranslation(1, 1, 0, out Matrix4 t2);
        mvMatrix = t2 * mvMatrix;
        SetMatrixUniforms();
        cubeModel ??= p.CreateModel(Quad.Create());
        p.BindTexture2d(textureid);
        p.DrawModel(cubeModel);
    }

    private void SetMatrixUniforms()
    {
        p.SetMatrixUniformProjection(ref pMatrix);
        p.SetMatrixUniformModelView(ref mvMatrix);
    }

    private float xRot;
    private bool xInv;
    private float xSpeed;

    private float yRot;
    private bool yInv;
    private float ySpeed;

    private int overlap;
    private int minspeed;
    private Random rnd;

    private int filter;

    private bool initialized;

    private void Animate(float dt)
    {
        float maxDt = 1;
        if (dt > maxDt)
        {
            dt = maxDt;
        }
        if (xInv)
        {
            if (xRot <= -overlap)
            {
                xInv = false;
                xSpeed = minspeed + rnd.Next(5);
            }
            xRot -= xSpeed * dt;
        }
        else
        {
            if (xRot >= overlap)
            {
                xInv = true;
                xSpeed = minspeed + rnd.Next(5);
            }
            xRot += xSpeed * dt;
        }
        if (yInv)
        {
            if (yRot <= -overlap)
            {
                yInv = false;
                ySpeed = minspeed + rnd.Next(5);
            }
            yRot -= ySpeed * dt;
        }
        else
        {
            if (yRot >= overlap)
            {
                yInv = true;
                ySpeed = minspeed + rnd.Next(5);
            }
            yRot += ySpeed * dt;
        }
    }

    public void OnNewFrame(float args)
    {
        if (!initialized)
        {
            initialized = true;
            p.InitShaders();

            p.GlClearColorRgbaf(0, 0, 0, 1);
            p.GlEnableDepthTest();
        }
        viewportWidth = p.GetCanvasWidth();
        viewportHeight = p.GetCanvasHeight();
        DrawScene(args);
        Animate(args);
        loginClient.Update(p);
    }

    public void HandleMouseDown(MouseEventArgs e)
    {
        mousePressed = true;
        previousMouseX = e.GetX();
        previousMouseY = e.GetY();
        screen.OnMouseDown(e);
    }

    public void HandleMouseUp(MouseEventArgs e)
    {
        mousePressed = false;
        screen.OnMouseUp(e);
    }

    private bool mousePressed;

    private int previousMouseX;
    private int previousMouseY;

    public void HandleMouseMove(MouseEventArgs e)
    {
        previousMouseX = e.GetX();
        previousMouseY = e.GetY();
        if (mousePressed)
        {
            //            ySpeed += dx / 10;
            //            xSpeed += dy / 10;
        }
        screen.OnMouseMove(e);
    }

    public void HandleMouseWheel(MouseWheelEventArgs e)
    {
        screen.OnMouseWheel(e);
    }

    public void HandleTouchStart(TouchEventArgs e)
    {
        touchId = e.GetId();
        previousTouchX = e.GetX();
        previousTouchY = e.GetY();
        screen.OnTouchStart(e);
    }

    private int touchId;
    private int previousTouchX;
    private int previousTouchY;

    public void HandleTouchMove(TouchEventArgs e)
    {
        screen.OnTouchMove(e);
        if (e.GetId() != touchId)
        {
            return;
        }
        float dx = e.GetX() - previousTouchX;
        float dy = e.GetY() - previousTouchY;
        previousTouchX = e.GetX();
        previousTouchY = e.GetY();

        ySpeed += dx / 10;
        xSpeed += dy / 10;
    }

    public void HandleTouchEnd(TouchEventArgs e)
    {
        screen.OnTouchEnd(e);
    }

    private readonly TextTexture[] textTextures;
    private int textTexturesCount;

    internal void StartSingleplayer()
    {
        screen = new SingleplayerScreen
        {
            menu = this
        };
        screen.LoadTranslations();
    }

    internal void StartLogin(string serverHash, string ip, int port)
    {
        LoginScreen screenLogin = new()
        {
            serverHash = serverHash,
            serverIp = ip,
            serverPort = port
        };
        screen = screenLogin;
        screen.menu = this;
        screen.LoadTranslations();
    }

    internal void StartConnectToIp()
    {
        ConnectionScreen screenConnectToIp = new();
        screen = screenConnectToIp;
        screen.menu = this;
        screen.LoadTranslations();
    }

    internal void Exit()
    {
        Environment.Exit(0);
    }

    internal void StartMainMenu()
    {
        screen = new MainScreen
        {
            menu = this
        };
        p.ExitMousePointerLock();
    }

    internal int backgroundW;
    internal int backgroundH;
    internal float windowX;
    internal float windowY;
    internal void DrawBackground()
    {
        backgroundW = 512;
        backgroundH = 512;
        windowX = p.GetCanvasWidth();
        windowY = p.GetCanvasHeight();
        //Background tiling
        int countX = (int)((windowX + (2 * overlap)) / backgroundW) + 1;
        int countY = (int)((windowY + (2 * overlap)) / backgroundH) + 1;
        for (int x = 0; x < countX; x++)
        {
            for (int y = 0; y < countY; y++)
            {
                Draw2dQuad(GetTexture("background.png"), x * backgroundW + xRot - overlap, y * backgroundH + yRot - overlap, backgroundW, backgroundH);
            }
        }
    }

    internal void StartMultiplayer()
    {
        screen = new MultiplayerScreen
        {
            menu = this
        };
        screen.LoadTranslations();
    }

    internal void Login(string user, string password, string serverHash, string token, LoginResult loginResult, LoginData loginResultData)
    {
        if (user == "" || (password == "" && token == ""))
        {
            loginResult = LoginResult.Failed;
            return;
        }
        loginClient.Login(p, user, password, serverHash, token, loginResult, loginResultData);
    }

    private readonly LoginClientCi loginClient;

    internal static LoginResult CreateAccount(string user, string password)
    {
        if (user == "" || password == "")
            return LoginResult.Failed;

        return LoginResult.Ok;
    }

    internal static string[] GetSaveGames(out int length)
    {
        string[] files = FileHelper.DirectoryGetFiles(GamePlatformNative.PathSavegames);
        length = files.Length;
        string[] savegames = new string[length];
        int count = 0;
        for (int i = 0; i < length; i++)
        {
            if(files[i].EndsWith(".mddbs"))
            {
                savegames[count++] = files[i];
            }
        }
        length = count;
        return savegames;
    }

    internal static void StartNewWorld()
    {
    }

    internal static void StartModifyWorld()
    {
    }

    public void StartGame(bool singleplayer, string singleplayerSavePath, ConnectionData connectData)
    {
        ScreenGame screenGame = new()
        {
            menu = this
        };
        screenGame.Start(p, singleplayer, singleplayerSavePath, connectData);
        screen = screenGame;
    }

    internal void ConnectToGame(LoginData loginResultData, string username)
    {
        ConnectionData connectData = new()
        {
            Ip = loginResultData.ServerAddress,
            Port = loginResultData.Port,
            Auth = loginResultData.AuthCode,
            Username = username
        };

        StartGame(false, null, connectData);
    }

    public void ConnectToSingleplayer(string filename)
    {
        StartGame(true, filename, null);
    }

    public float GetScale()
    {
        float scale;
        if (p.IsSmallScreen())
        {
            scale = one * p.GetCanvasWidth() / 1280;
        }
        else
        {
            scale = one;
        }
        return scale;
    }
}

public class TextTexture
{
    internal float size;
    internal string text;
    internal int texture;
    internal int texturewidth;
    internal int textureheight;
    internal int textwidth;
    internal int textheight;
}

public enum LoginResult
{
    None,
    Connecting,
    Failed,
    Ok
}

public class ThumbnailResponseCi
{
    internal bool done;
    internal bool error;
    internal string serverMessage;
    internal byte[] data;
}

