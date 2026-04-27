using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using static ManicDigger.AudioOpenAl;

// ─────────────────────────────────────────────────────────────────────────────
// Composite — the full platform contract used throughout the game.
// GamePlatformNative implements this; everything else depends on it.
// ─────────────────────────────────────────────────────────────────────────────

public interface IGamePlatform :
    IPlatformMisc,
    IPlatformAudio,
    IPlatformNetwork,
    IPlatformOpenGl,
    IPlatformSinglePlayer
{
}

// ─────────────────────────────────────────────────────────────────────────────
// Misc / OS / window
// ─────────────────────────────────────────────────────────────────────────────

public interface IPlatformMisc
{
    void ThumbnailDownloadAsync(string ip, int port, ThumbnailResponseCi response);
    void AddOnNewFrame(Action<float> handler);
    void AddOnKeyEvent(Action<KeyEventArgs> onKeyDown,
        Action<KeyEventArgs> onKeyUp,
        Action<KeyPressEventArgs> onKeyPress);
    void AddOnMouseEvent(
        Action<MouseEventArgs> onMouseDown,
        Action<MouseEventArgs> onMouseUp,
        Action<MouseEventArgs> onMouseMove,
        Action<MouseWheelEventArgs> onMouseWheel);
    void AddOnTouchEvent(Action<TouchEventArgs> onTouchStart,
        Action<TouchEventArgs> onTouchMove,
        Action<TouchEventArgs> onTouchEnd);
    int GetCanvasWidth();
    int GetCanvasHeight();
    int TimeMillisecondsFromStart { get; }

    void SaveScreenshot();
    Bitmap GrabScreenshot();
    IAviWriter AviWriterCreate();
    string PathStorage();
    void SetVSync(bool enabled);
    string GetGameVersion();
    void GzipDecompress(byte[] compressed, int compressedLength, byte[] ret);
    bool ChatLog(string servername, string p);
    bool IsValidTypingChar(int c);
    void WindowExit();
    void MessageBoxShowError(string text, string caption);
    void SetTitle(string applicationname);
    bool Focused();
    void AddOnCrash(OnCrashHandler handler);
    string KeyName(int key);
    List<DisplayResolutionCi> GetDisplayResolutions();
    WindowState GetWindowState();
    void SetWindowState(WindowState value);
    void ChangeResolution(int width, int height, int bitsPerPixel, float refreshRate);
    DisplayResolutionCi GetDisplayResolutionDefault();
   
    string FileOpenDialog(string extension, string extensionName, string initialDirectory);
    void MouseCursorSetVisible(bool value);
    bool MouseCursorIsVisible();
    void ApplicationDoEvents();
    void ShowKeyboard(bool show);
    bool IsFastSystem();
    Preferences GetPreferences();
    void SetPreferences(Preferences preferences);
    bool IsMousePointerLocked();
    void RequestMousePointerLock();
    void ExitMousePointerLock();
    bool MultithreadingAvailable();
    void QueueUserWorkItem(Action action);
    byte[] GzipCompress(byte[] data, int dataLength);
    bool IsDebuggerAttached();
    bool IsSmallScreen();
    void OpenLinkInBrowser(string url);
    void SaveAssetToCache(Asset tosave);
    Asset LoadAssetFromCache(string md5);
    bool IsCached(string md5);
    string QueryStringValue(string key);
    void SetWindowCursor(int hotx, int hoty, int sizex, int sizey, byte[] imgdata, int imgdataLength);
    void RestoreWindowCursor();
}

// ─────────────────────────────────────────────────────────────────────────────
// Audio
// ─────────────────────────────────────────────────────────────────────────────

public interface IPlatformAudio
{
    AudioData AudioDataCreate(byte[] data, int dataLength);
    bool AudioDataLoaded(AudioData data);
    AudioTask AudioCreate(AudioData data);
    void AudioPlay(AudioTask audio);
    void AudioPause(AudioTask audio);
    void AudioDelete(AudioTask audio);
    bool AudioFinished(AudioTask audio);
    void AudioSetPosition(AudioTask audio, float x, float y, float z);
    void AudioUpdateListener(float posX, float posY, float posZ, float orientX, float orientY, float orientZ);
}

// ─────────────────────────────────────────────────────────────────────────────
// Networking (TCP / ENet / WebSocket)
// ─────────────────────────────────────────────────────────────────────────────

public interface IPlatformNetwork
{
    bool TcpAvailable();
    // ENet
    bool EnetAvailable();
    EnetHost EnetCreateHost();
    void EnetHostInitialize(EnetHost host, IpEndpoint? address, int peerLimit, int channelLimit, int incomingBandwidth, int outgoingBandwidth);
    void EnetHostInitializeServer(EnetHost host, int port, int peerLimit);
    EnetEvent? EnetHostService(EnetHost host, int timeout);
    EnetEvent? EnetHostCheckEvents(EnetHost host);
    EnetPeer EnetHostConnect(EnetHost host, string hostName, int port, int channelCount, int data);
    void EnetPeerSend(EnetPeer peer, int channelId, ReadOnlyMemory<byte> payload, int flags);

    // WebSocket
    bool WebSocketAvailable();
    void WebSocketConnect(string ip, int port);
    void WebSocketSend(byte[] data, int dataLength);
    int WebSocketReceive(byte[] data, int dataLength);
}

// ─────────────────────────────────────────────────────────────────────────────
// OpenGL
// ─────────────────────────────────────────────────────────────────────────────

public interface IPlatformOpenGl
{
    void GlViewport(int x, int y, int width, int height);
    void GlClearColorBufferAndDepthBuffer();
    void GlDisableDepthTest();
    void GlClearColorRgbaf(float r, float g, float b, float a);
    void GlEnableDepthTest();
    void GlDisableCullFace();
    void GlEnableCullFace();
    void GLLineWidth(int width);
    void GLDeleteTexture(int id);
    void GlClearDepthBuffer();
    void GlLightModelAmbient(int r, int g, int b);
    void GlEnableFog();
    void GlFogFogColor(int r, int g, int b, int a);
    void GlFogFogDensity(float density);
    int GlGetMaxTextureSize();
    void GlDepthMask(bool flag);
    void GlCullFaceBack();
    void GlEnableLighting();
    void GlEnableColorMaterial();
    void GlColorMaterialFrontAndBackAmbientAndDiffuse();
    void GlShadeModelSmooth();
    void GlDisableFog();
    void BindTexture2d(int texture);
    GeometryModel CreateModel(GeometryModel modelData);
    void UpdateModel(GeometryModel data);
    void DrawModel(GeometryModel model);
    void InitShaders();
    void SetMatrixUniformProjection(ref Matrix4 pMatrix);
    void SetMatrixUniformModelView(ref Matrix4 mvMatrix);
    void DrawModels(List<GeometryModel> model, int count);
    void DrawModelData(GeometryModel data);
    void DeleteModel(GeometryModel model);
    int LoadTextureFromBitmap(Bitmap bmp);
}

// ─────────────────────────────────────────────────────────────────────────────
// Single-player server lifecycle + casting helpers
// ─────────────────────────────────────────────────────────────────────────────

public interface IPlatformSinglePlayer
{
    bool SinglePlayerServerAvailable();
    void SinglePlayerServerStart(string saveFilename);
    void SinglePlayerServerExit();
    bool SinglePlayerServerLoaded();
    void SinglePlayerServerDisable();
    DummyNetwork SinglePlayerServerGetNetwork();
}

public class OnCrashHandler
{
    public virtual void OnCrash() { }
}

/// <summary>
/// A simple string-keyed settings store. All values are persisted as strings
/// and converted on read. Backed by a <see cref="Dictionary{TKey,TValue}"/>.
/// </summary>
public class Preferences
{
    private readonly Dictionary<string, string> items = new();

    // -------------------------------------------------------------------------
    // String
    // -------------------------------------------------------------------------

    public string GetString(string key, string default_) =>
        items.TryGetValue(key, out string value) ? value : default_;

    public void SetString(string key, string value) =>
        items[key] = value;

    // -------------------------------------------------------------------------
    // Bool (stored as "0" / "1")
    // -------------------------------------------------------------------------

    public bool GetBool(string key, bool default_)
    {
        string value = GetString(key, null);
        return value switch
        {
            "0" => false,
            "1" => true,
            _ => default_
        };
    }

    public void SetBool(string key, bool value) =>
        SetString(key, value ? "1" : "0");

    // -------------------------------------------------------------------------
    // Int (stored as string, parsed via float to handle decimals gracefully)
    // -------------------------------------------------------------------------

    public int GetInt(string key, int default_)
    {
        string raw = GetString(key, null);
        if (raw == null) return default_;
        return float.TryParse(raw, out float result) ? (int)result : default_;
    }

    public void SetInt(string key, int value) =>
        SetString(key, value.ToString());

    public IEnumerable<string> ToLines() =>
        items.Select(kvp => $"{kvp.Key}={kvp.Value}");

    // -------------------------------------------------------------------------
    // Collection
    // -------------------------------------------------------------------------

    public int GetKeysCount() => items.Count;

    public string GetKey(int i) => items.Keys.ElementAtOrDefault(i);

    internal void Remove(string key) => items.Remove(key);
}

public class KeyEventArgs : KeyPressEventArgs
{
    public bool CtrlPressed { get; init; }
    public bool ShiftPressed { get; init; }
    public bool AltPressed { get; init; }
}

public class KeyPressEventArgs
{
    public int KeyChar { get; init; }
    public bool Handled { get; set; }
}


public class MouseEventArgs
{
    private int x;
    private int y;
    private int movementX;
    private int movementY;
    private int button;
    public int GetX() { return x; } public void SetX(int value) { x = value; }
    public int GetY() { return y; } public void SetY(int value) { y = value; }
    public int GetMovementX() { return movementX; } public void SetMovementX(int value) { movementX = value; }
    public int GetMovementY() { return movementY; } public void SetMovementY(int value) { movementY = value; }
    public int GetButton() { return button; } public void SetButton(int value) { button = value; }
    private bool handled;
    public bool GetHandled() { return handled; }
    public void SetHandled(bool value) { handled = value; }
    private bool forceUsage;
    public bool GetForceUsage() { return forceUsage; }
    public void SetForceUsage(bool value) { forceUsage = value; }
    private bool emulated;
    public bool GetEmulated() { return emulated; }
    public void SetEmulated(bool value) { emulated = value; }
}

public class TouchEventArgs
{
    private int x;
    private int y;
    private int id;
    private bool handled;
    public int GetX() { return x; } public void SetX(int value) { x = value; }
    public int GetY() { return y; } public void SetY(int value) { y = value; }
    public int GetId() { return id; } public void SetId(int value) { id = value; }
    public bool GetHandled() { return handled; } public void SetHandled(bool value) { handled = value; }
}

public enum TextAlign
{
    Left,
    Center,
    Right
}

public enum TextBaseline
{
    Top,
    Middle,
    Bottom
}

