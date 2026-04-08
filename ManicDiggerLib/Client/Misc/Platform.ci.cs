using OpenTK.Mathematics;
using OpenTK.Windowing.Common;

public abstract class GamePlatform
{
   
    // Misc
    public abstract void TextSize(string text, float fontSize, out int outWidth, out int outHeight);
    public abstract void Exit();
    public abstract bool ExitAvailable();
    public abstract string PathSavegames();
    public abstract void WebClientDownloadDataAsync(string url, HttpResponseCi response);
    public abstract void ThumbnailDownloadAsync(string ip, int port, ThumbnailResponseCi response);
    public abstract string FileName(string fullpath);
    public abstract void AddOnNewFrame(NewFrameHandler handler);
    public abstract void AddOnKeyEvent(KeyEventHandler handler);
    public abstract void AddOnMouseEvent(MouseEventHandler handler);
    public abstract void AddOnTouchEvent(TouchEventHandler handler);
    public abstract int GetCanvasWidth();
    public abstract int GetCanvasHeight();
    public abstract int TimeMillisecondsFromStart();
    public abstract void ThrowException(string message);
    public abstract void BitmapSetPixelsArgb(Bitmap bmp, int[] pixels);
    public abstract Bitmap CreateTextTexture(Text_ t);
    public abstract void SetTextRendererFont(int fontID);
    public abstract float BitmapGetWidth(Bitmap bmp);
    public abstract float BitmapGetHeight(Bitmap bmp);
    public abstract void BitmapDelete(Bitmap bmp);
    public abstract void ConsoleWriteLine(string p);
    public abstract MonitorObject MonitorCreate();
    public abstract void MonitorEnter(MonitorObject monitorObject);
    public abstract void MonitorExit(MonitorObject monitorObject);
    public abstract void SaveScreenshot();
    public abstract Bitmap GrabScreenshot();
    public abstract AviWriterCi AviWriterCreate();
    public abstract string PathStorage();
    public abstract void SetVSync(bool enabled);
    public abstract string GetGameVersion();
    public abstract void GzipDecompress(byte[] compressed, int compressedLength, byte[] ret);
    public abstract bool ChatLog(string servername, string p);
    public abstract bool IsValidTypingChar(int c);
    public abstract void WindowExit();
    public abstract void MessageBoxShowError(string text, string caption);
    public abstract Bitmap BitmapCreateFromPng(byte[] data, int dataLength);
    public abstract void BitmapGetPixelsArgb(Bitmap bitmap, int[] bmpPixels);
    public abstract void SetTitle(string applicationname);
    public abstract bool Focused();
    public abstract void AddOnCrash(OnCrashHandler handler);
    public abstract string KeyName(int key);
    public abstract DisplayResolutionCi[] GetDisplayResolutions(out int resolutionsCount);
    public abstract WindowState GetWindowState();
    public abstract void SetWindowState(WindowState value);
    public abstract void ChangeResolution(int width, int height, int bitsPerPixel, float refreshRate);
    public abstract DisplayResolutionCi GetDisplayResolutionDefault();
    public abstract void WebClientUploadDataAsync(string url, byte[] data, int dataLength, HttpResponseCi response);
    public abstract string FileOpenDialog(string extension, string extensionName, string initialDirectory);
    public abstract void MouseCursorSetVisible(bool value);
    public abstract bool MouseCursorIsVisible();
    public abstract void ApplicationDoEvents();
    public abstract void ThreadSpinWait(int iterations);
    public abstract void ShowKeyboard(bool show);
    public abstract bool IsFastSystem();
    public abstract Preferences GetPreferences();
    public abstract void SetPreferences(Preferences preferences);
    public abstract bool IsMousePointerLocked();
    public abstract void RequestMousePointerLock();
    public abstract void ExitMousePointerLock();
    public abstract bool MultithreadingAvailable();
    public abstract void QueueUserWorkItem(Action action);
    public abstract void LoadAssetsAsyc(AssetList list, out float progress);
    public abstract byte[] GzipCompress(byte[] data, int dataLength, out int retLength);
    public abstract bool IsDebuggerAttached();
    public abstract bool IsSmallScreen();
    public abstract void OpenLinkInBrowser(string url);
    public abstract void SaveAssetToCache(Asset tosave);
    public abstract Asset LoadAssetFromCache(string md5);
    public abstract bool IsCached(string md5);
    public abstract bool IsChecksum(string checksum);
    public abstract string DecodeHTMLEntities(string htmlencodedstring);
    public abstract string QueryStringValue(string key);
    public abstract void SetWindowCursor(int hotx, int hoty, int sizex, int sizey, byte[] imgdata, int imgdataLength);
    public abstract void RestoreWindowCursor();

    // Audio
    public abstract AudioData AudioDataCreate(byte[] data, int dataLength);
    public abstract bool AudioDataLoaded(AudioData data);
    public abstract AudioCi AudioCreate(AudioData data);
    public abstract void AudioPlay(AudioCi audio);
    public abstract void AudioPause(AudioCi audio);
    public abstract void AudioDelete(AudioCi audioCi);
    public abstract bool AudioFinished(AudioCi audio);
    public abstract void AudioSetPosition(AudioCi audio, float x, float y, float z);
    public abstract void AudioUpdateListener(float posX, float posY, float posZ, float orientX, float orientY, float orientZ);
    
    // Tcp
    public abstract bool TcpAvailable();
    public abstract void TcpConnect(string ip, int port, bool connected);
    public abstract void TcpSend(byte[] data, int length);
    public abstract int TcpReceive(byte[] data, int dataLength);

    // Enet
    public abstract bool EnetAvailable();
    public abstract EnetHost EnetCreateHost();
    public abstract void EnetHostInitializeServer(EnetHost host, int port, int peerLimit);
    public abstract bool EnetHostService(EnetHost host, int timeout, EnetEventRef enetEvent);
    public abstract bool EnetHostCheckEvents(EnetHost host, EnetEventRef event_);
    public abstract EnetPeer EnetHostConnect(EnetHost host, string hostName, int port, int data, int channelLimit);
    public abstract void EnetPeerSend(EnetPeer peer, byte channelID, byte[] data, int dataLength, int flags);
    public abstract void EnetHostInitialize(EnetHost host, IPEndPointCi address, int peerLimit, int channelLimit, int incomingBandwidth, int outgoingBandwidth);

    // WebSocket
    public abstract bool WebSocketAvailable();
    public abstract void WebSocketConnect(string ip, int port);
    public abstract void WebSocketSend(byte[] data, int dataLength);
    public abstract int WebSocketReceive(byte[] data, int dataLength);
    
    // OpenGl
    public abstract void GlViewport(int x, int y, int width, int height);
    public abstract void GlClearColorBufferAndDepthBuffer();
    public abstract void GlDisableDepthTest();
    public abstract void GlClearColorRgbaf(float r, float g, float b, float a);
    public abstract void GlEnableDepthTest();
    public abstract void GlDisableCullFace();
    public abstract void GlEnableCullFace();
    public abstract void GlEnableTexture2d();
    public abstract void GLLineWidth(int width);
    public abstract void GLDisableAlphaTest();
    public abstract void GLEnableAlphaTest();
    public abstract void GLDeleteTexture(int id);
    public abstract void GlClearDepthBuffer();
    public abstract void GlLightModelAmbient(int r, int g, int b);
    public abstract void GlEnableFog();
    public abstract void GlHintFogHintNicest();
    public abstract void GlFogFogModeExp2();
    public abstract void GlFogFogColor(int r, int g, int b, int a);
    public abstract void GlFogFogDensity(float density);
    public abstract int GlGetMaxTextureSize();
    public abstract void GlDepthMask(bool flag);
    public abstract void GlCullFaceBack();
    public abstract void GlEnableLighting();
    public abstract void GlEnableColorMaterial();
    public abstract void GlColorMaterialFrontAndBackAmbientAndDiffuse();
    public abstract void GlShadeModelSmooth();
    public abstract void GlDisableFog();
    public abstract void BindTexture2d(int texture);
    public abstract Model CreateModel(ModelData modelData);
    public abstract void DrawModel(Model model);
    public abstract void InitShaders();
    public abstract void SetMatrixUniformProjection(ref Matrix4 pMatrix);
    public abstract void SetMatrixUniformModelView(ref Matrix4 mvMatrix);
    public abstract void DrawModels(List<Model> model, int count);
    public abstract void DrawModelData(ModelData data);
    public abstract void DeleteModel(Model model);
    public abstract int LoadTextureFromBitmap(Bitmap bmp);
    
    // Game
    public abstract bool SinglePlayerServerAvailable();
    public abstract void SinglePlayerServerStart(string saveFilename);
    public abstract void SinglePlayerServerExit();
    public abstract bool SinglePlayerServerLoaded();
    public abstract void SinglePlayerServerDisable();
    public abstract DummyNetwork SinglePlayerServerGetNetwork();
    public abstract PlayerInterpolationState CastToPlayerInterpolationState(InterpolatedObject a);
    public abstract EnetNetConnection CastToEnetNetConnection(NetConnection connection);
}

public class Asset
{
    internal string name;
    internal string md5;
    internal byte[] data;
    internal int dataLength;

    public string GetName() { return name; } public void SetName(string value) { name = value; }
    public string GetMd5() { return md5; }public void SetMd5(string value) { md5 = value; }
    public byte[] GetData() { return data; } public void SetData(byte[] value) { data = value; }
    public int GetDataLength() { return dataLength; } public void SetDataLength(int value) { dataLength = value; }
}

public class AssetList
{
    internal Asset[] items;
    internal int count;

    public Asset[] GetItems() { return items; } public void SetItems(Asset[] value) { items = value; }
    public int GetCount() { return count; } public void SetCount(int value) { count = value; }
}

public class OnCrashHandler
{
    public virtual void OnCrash() { }
}

public class Preferences
{
    public Preferences()
    {
        items = [];
    }
    internal GamePlatform platform;
    internal Dictionary<string, string> items;

    public string GetKey(int i)
    {
        return items.Keys.ElementAtOrDefault(i);
    }

    public int GetKeysCount()
    {
        return items.Count;
    }

    public string GetString(string key, string default_)
    {
        return items.TryGetValue(key, out string value) ? value : default_;
    }

    public void SetString(string key, string value)
    {
        items[key] = value;
    }

    public bool GetBool(string key, bool default_)
    {
        string value = GetString(key, null);
        if (value == null)
        {
            return default_;
        }
        if (value == "0")
        {
            return false;
        }
        if (value == "1")
        {
            return true;
        }
        return default_;
    }

    public int GetInt(string key, int default_)
    {
        if (GetString(key, null) == null)
        {
            return default_;
        }
        if (float.TryParse(GetString(key, null), out float ret))
        {
            return (int)(ret);
        }
        return default_;
    }

    public void SetBool(string key, bool value)
    {
        SetString(key, value ? "1" : "0");
    }

    public void SetInt(string key, int value)
    {
        SetString(key, value.ToString());
    }

    internal void Remove(string key)
    {
        items.Remove(key);
    }
}

public class EnetHost
{
}

public abstract class EnetEvent
{
    public abstract EnetEventType Type();
    public abstract EnetPeer Peer();
    public abstract EnetPacket Packet();
}

public class EnetEventRef
{
    internal EnetEvent e;
}

public enum EnetEventType
{
    None,
    Connect,
    Disconnect,
    Receive
}

public class EnetPacketFlags
{
    public const int None = 0;
    public const int Reliable = 1;
    public const int Unsequenced = 2;
    public const int NoAllocate = 4;
    public const int UnreliableFragment = 8;
}

public abstract class EnetPeer
{
    public abstract int UserData();
    public abstract void SetUserData(int value);
    public abstract IPEndPointCi GetRemoteAddress();
}

public abstract class EnetPacket
{
    public abstract int GetBytesCount();
    public abstract byte[] GetBytes();
    public abstract void Dispose();
}

public class MonitorObject
{
}

public class KeyEventArgs
{
    private int keyCode;
    public int GetKeyCode() { return keyCode; }
    public void SetKeyCode(int value) { keyCode = value; }
    private bool handled;
    public bool GetHandled() { return handled; }
    public void SetHandled(bool value) { handled = value; }
    private bool modifierCtrl;
    public bool GetCtrlPressed() { return modifierCtrl; }
    public void SetCtrlPressed(bool value) { modifierCtrl = value; }
    private bool modifierShift;
    public bool GetShiftPressed() { return modifierShift; }
    public void SetShiftPressed(bool value) { modifierShift = value; }
    private bool modifierAlt;
    public bool GetAltPressed() { return modifierAlt; }
    public void SetAltPressed(bool value) { modifierAlt = value; }
}

public class KeyPressEventArgs
{
    private int keyChar;
    public int GetKeyChar() { return keyChar; }
    public void SetKeyChar(int value) { keyChar = value; }
    private bool handled;
    public bool GetHandled() { return handled; }
    public void SetHandled(bool value) { handled = value; }
}

public abstract class NewFrameHandler
{
    public abstract void OnNewFrame(NewFrameEventArgs args);
}

public abstract class ImageOnLoadHandler
{
    public abstract void OnLoad();
}

public abstract class KeyEventHandler
{
    public abstract void OnKeyDown(KeyEventArgs e);
    public abstract void OnKeyPress(KeyPressEventArgs e);
    public abstract void OnKeyUp(KeyEventArgs e);
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



public class MouseButtonEnum
{
    public const int Left = 0;
    public const int Middle = 1;
    public const int Right = 2;
}

public abstract class MouseEventHandler
{
    public abstract void OnMouseDown(MouseEventArgs e);
    public abstract void OnMouseUp(MouseEventArgs e);
    public abstract void OnMouseMove(MouseEventArgs e);
    public abstract void OnMouseWheel(MouseWheelEventArgs e);
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

public abstract class TouchEventHandler
{
    public abstract void OnTouchStart(TouchEventArgs e);
    public abstract void OnTouchMove(TouchEventArgs e);
    public abstract void OnTouchEnd(TouchEventArgs e);
}

public class NewFrameEventArgs
{
    private float dt;
    public float GetDt()
    {
        return dt;
    }
    public void SetDt(float p)
    {
        this.dt = p;
    }
}

public abstract class Texture
{
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

public abstract class AudioData
{
}

public abstract class AudioCi
{
}
