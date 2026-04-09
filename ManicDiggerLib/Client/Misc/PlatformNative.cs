using ENet;
using ManicDigger;
using ManicDigger.ClientNative;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Net;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using Monitor = System.Threading.Monitor;
using PixelFormat = System.Drawing.Imaging.PixelFormat;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

public class GamePlatformNative : IGamePlatform
{
    #region Misc
    public GamePlatformNative()
    {
        ThreadPool.SetMinThreads(32, 32);
        ThreadPool.SetMaxThreads(128, 128);
        datapaths = [Path.Combine(Path.Combine(Path.Combine("..", ".."), ".."), "data"), "data"];
        start.Start();
    }

    public bool TouchTest = false;
    private readonly string[] datapaths;

    private readonly ManicDigger.Renderers.TextRenderer r = new();
    private readonly Dictionary<TextAndSize, SizeF> textsizes = new();
    public SizeF TextSize(string text, float fontsize)
    {
        if (textsizes.TryGetValue(new TextAndSize() { text = text, size = fontsize }, out SizeF size))
        {
            return size;
        }
        size = textrenderer.MeasureTextSize(text, fontsize);
        textsizes[new TextAndSize() { text = text, size = fontsize }] = size;
        return size;
    }

    public void TextSize(string text, float fontSize, out int outWidth, out int outHeight)
    {
        SizeF size = TextSize(text, fontSize);
        outWidth = (int)size.Width;
        outHeight = (int)size.Height;
    }

    public  void Exit()
    {
        Environment.Exit(0);
    }

    public  bool ExitAvailable()
    {
        return true;
    }

    public  string PathSavegames()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    public  void WebClientDownloadDataAsync(string url, HttpResponseCi response)
    {
        DownloadDataArgs args = new()
        {
            url = url,
            response = response
        };
        ThreadPool.QueueUserWorkItem(DownloadData, args);
    }

    private class DownloadDataArgs
    {
        public string url;
        public HttpResponseCi response;
    }

    private void DownloadData(object o)
    {
        DownloadDataArgs args = (DownloadDataArgs)o;
        WebClient c = new();
        try
        {
            byte[] data = c.DownloadData(args.url);
            args.response.value = data;
            args.response.valueLength = data.Length;
            args.response.done = true;
        }
        catch
        {
            args.response.error = true;
        }
    }

    public void ThumbnailDownloadAsync(string ip, int port, ThumbnailResponseCi response)
    {
        ThumbnailDownloadArgs args = new() { ip = ip, port = port, response = response };
        _ = Task.Run(() => DownloadServerThumbnailAsync(args));
    }

    private async Task DownloadServerThumbnailAsync(ThumbnailDownloadArgs args)
    {
        var (result, message) = await new QueryClient(this).QueryAsync(args.ip, args.port);

        if (result != null)
        {
            args.response.data = result.ServerThumbnail;
            args.response.dataLength = result.ServerThumbnail.Length;
            args.response.serverMessage = message;
            args.response.done = true;
        }
        else
        {
            args.response.serverMessage = message;
            args.response.error = true;
        }
    }

    private class ThumbnailDownloadArgs
    {
        public string ip;
        public int port;
        public ThumbnailResponseCi response;
    }

    public  string FileName(string fullpath)
    {
        FileInfo info = new(fullpath);
        return info.Name.Replace(info.Extension, "");
    }

    private readonly Stopwatch start = new();

    public  int TimeMillisecondsFromStart()
    {
        return (int)start.ElapsedMilliseconds;
    }

    public  void ThrowException(string message)
    {
        throw new Exception(message);
    }

    public  void BitmapSetPixelsArgb(Bitmap bmp, int[] pixels)
    {
        if (IsMono)
        {
            SetPixelsSafe(bmp, pixels);
        }
        else
        {
            SetPixelsFast(bmp, pixels);
        }
    }

    private static void SetPixelsSafe(Bitmap bmp, int[] pixels)
    {
        int width = bmp.Width;
        int height = bmp.Height;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bmp.SetPixel(x, y, Color.FromArgb(pixels[y * width + x]));
            }
    }

    private static void SetPixelsFast(Bitmap bmp, int[] pixels)
    {
        BitmapData bmd = bmp.LockBits(
            new Rectangle(0, 0, bmp.Width, bmp.Height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(pixels, 0, bmd.Scan0, bmp.Width * bmp.Height);
        }
        finally
        {
            bmp.UnlockBits(bmd);
        }
    }

    public  Bitmap BitmapCreateFromPng(byte[] data, int dataLength)
    {
        Bitmap bmp;
        try
        {
            bmp = new Bitmap(new MemoryStream(data, 0, dataLength));
        }
        catch
        {
            bmp = new Bitmap(1, 1);
            bmp.SetPixel(0, 0, Color.Orange);
        }
        return bmp;
    }

    public bool IsMono = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public  void BitmapGetPixelsArgb(Bitmap bitmap, int[] bmpPixels)
    {
        if (IsMono)
        {
            GetPixelsSafe(bitmap, bmpPixels);
        }
        else
        {
            GetPixelsFast(bitmap, bmpPixels);
        }
    }

    /// <summary>
    /// Slow but portable pixel read using <see cref="Bitmap.GetPixel"/>.
    /// Used on platforms where pointer access into locked bitmap memory is unsafe.
    /// </summary>
    private static void GetPixelsSafe(Bitmap bmp, int[] bmpPixels)
    {
        int width = bmp.Width;
        int height = bmp.Height;
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                bmpPixels[y * width + x] = bmp.GetPixel(x, y).ToArgb();
            }
    }

    /// <summary>
    /// Fast pixel read using <see cref="BitmapData"/> and <see cref="Marshal.Copy"/>.
    /// Converts to <see cref="PixelFormat.Format32bppArgb"/> first if needed.
    /// </summary>
    private static void GetPixelsFast(Bitmap bmp, int[] bmpPixels)
    {
        // Ensure the bitmap is in the format we expect before locking.
        Bitmap source = bmp.PixelFormat == PixelFormat.Format32bppArgb
            ? bmp
            : new Bitmap(bmp.Width, bmp.Height, PixelFormat.Format32bppArgb);

        if (!ReferenceEquals(source, bmp))
        {
            using Graphics g = Graphics.FromImage(source);
            g.DrawImage(bmp, 0, 0);
        }

        BitmapData bmd = source.LockBits(
            new Rectangle(0, 0, source.Width, source.Height),
            ImageLockMode.ReadOnly,
            PixelFormat.Format32bppArgb);
        try
        {
            Marshal.Copy(bmd.Scan0, bmpPixels, 0, source.Width * source.Height);
        }
        finally
        {
            source.UnlockBits(bmd);
            if (!ReferenceEquals(source, bmp)) { source.Dispose(); }
        }
    }

    public  int LoadTextureFromBitmap(Bitmap bmp)
    {
        return LoadTexture(bmp, false);
    }

    private readonly ManicDigger.Renderers.TextRenderer textrenderer = new();

    public  Bitmap CreateTextTexture(Text_ t)
    {
        Bitmap bmp = textrenderer.MakeTextTexture(t);
        return bmp;
    }

    public  void SetTextRendererFont(int fontID)
    {
        textrenderer.SetFont(fontID);
    }

    public  float BitmapGetWidth(Bitmap bmp)
    {
        return bmp.Width;
    }

    public  float BitmapGetHeight(Bitmap bmp)
    {
        return bmp.Height;
    }

    public  void BitmapDelete(Bitmap bmp)
    {
        bmp.Dispose();
    }

    public  void ConsoleWriteLine(string s)
    {
        Console.WriteLine(s);
    }

    public  MonitorObject MonitorCreate()
    {
        return new MonitorObject();
    }

    public  void MonitorEnter(MonitorObject monitorObject)
    {
        Monitor.Enter(monitorObject);
    }

    public  void MonitorExit(MonitorObject monitorObject)
    {
        Monitor.Exit(monitorObject);
    }

    public  AviWriterCi AviWriterCreate()
    {
        AviWriterCiCs avi = new();
        return avi;
    }

    public  string PathStorage()
    {
        return GameStorePath.GetStorePath();
    }

    public  string GetGameVersion()
    {
        return GameVersion.Version;
    }

    private readonly ICompression compression = new CompressionGzip();
    public  void GzipDecompress(byte[] compressed, int compressedLength, byte[] ret)
    {
        byte[] data = new byte[compressedLength];
        for (int i = 0; i < compressedLength; i++)
        {
            data[i] = compressed[i];
        }
        byte[] decompressed = compression.Decompress(data);
        for (int i = 0; i < decompressed.Length; i++)
        {
            ret[i] = decompressed[i];
        }
    }
    public  byte[] GzipCompress(byte[] data, int dataLength, out int retLength)
    {
        byte[] data_ = new byte[dataLength];
        for (int i = 0; i < dataLength; i++)
        {
            data_[i] = data[i];
        }
        byte[] compressed = compression.Compress(data_);
        retLength = compressed.Length;
        return compressed;
    }
    public bool ENABLE_CHATLOG = true;
    public string gamepathlogs() { return Path.Combine(PathStorage(), "Logs"); }
    private static string MakeValidFileName(string name)
    {
        string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        string invalidReStr = string.Format(@"[{0}]", invalidChars);
        return Regex.Replace(name, invalidReStr, "_");
    }
    public  bool ChatLog(string servername, string p)
    {
        if (!ENABLE_CHATLOG)
        {
            return true;
        }
        if (!Directory.Exists(gamepathlogs()))
        {
            Directory.CreateDirectory(gamepathlogs());
        }
        string filename = Path.Combine(gamepathlogs(), MakeValidFileName(servername) + ".txt");
        try
        {
            File.AppendAllText(filename, string.Format("{0} {1}\n", DateTime.Now, p));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public  bool IsValidTypingChar(int c_)
    {
        char c = (char)c_;
        return (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)
                    || char.IsPunctuation(c) || char.IsSeparator(c) || char.IsSymbol(c))
                    && c != '\r' && c != '\t';
    }

    public  void MessageBoxShowError(string text, string caption)
    {
        MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
    }

    public void SetExit(GameExit exit)
    {
        gameexit = exit;
    }

    private class UploadData
    {
        public string url;
        public byte[] data;
        public int dataLength;
        public HttpResponseCi response;
    }

    public  void WebClientUploadDataAsync(string url, byte[] data, int dataLength, HttpResponseCi response)
    {
        UploadData d = new()
        {
            url = url,
            data = data,
            dataLength = dataLength,
            response = response
        };
        ThreadPool.QueueUserWorkItem(DoUploadData, d);
    }

    private void DoUploadData(object o)
    {
        UploadData d = (UploadData)o;
        try
        {
            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(d.url);
            request.Method = "POST";
            request.Timeout = 15000; // 15s timeout
            request.ContentType = "application/x-www-form-urlencoded";
            request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.NoCacheNoStore);

            request.ContentLength = d.dataLength;

            ServicePointManager.Expect100Continue = false; // fixes lighthttpd 417 error

            using (Stream requestStream = request.GetRequestStream())
            {
                requestStream.Write(d.data, 0, d.dataLength);
                requestStream.Flush();
            }
            WebResponse response_ = request.GetResponse();

            MemoryStream m = new();
            using (Stream s = response_.GetResponseStream())
            {
                CopyTo(s, m);
            }
            d.response.value = m.ToArray();
            d.response.valueLength = d.response.value.Length;
            d.response.done = true;

            request.Abort();

        }
        catch
        {
            d.response.error = true;
        }
    }

    public static void CopyTo(Stream source, Stream destination)
    {
        // TODO: Argument validation
        byte[] buffer = new byte[16384]; // For example...
        int bytesRead;
        while ((bytesRead = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            destination.Write(buffer, 0, bytesRead);
        }
    }

    public  string FileOpenDialog(string extension, string extensionName, string initialDirectory)
    {
        OpenFileDialog d = new()
        {
            InitialDirectory = initialDirectory,
            FileName = "Default." + extension,
            Filter = string.Format("{1}|*.{0}|All files|*.*", extension, extensionName),
            CheckFileExists = false,
            CheckPathExists = true
        };
        string dir = Environment.CurrentDirectory;
        DialogResult result = d.ShowDialog();
        Environment.CurrentDirectory = dir;
        if (result == DialogResult.OK)
        {
            return d.FileName;
        }
        return null;
    }

    public  void ApplicationDoEvents()
    {
        if (IsMono)
        {
            Application.DoEvents();
            Thread.Sleep(0);
        }
    }

    public  void ThreadSpinWait(int iterations)
    {
        Thread.SpinWait(iterations);
    }

    public  void ShowKeyboard(bool show)
    {
    }

    public  bool IsFastSystem()
    {
        return true;
    }

    private static string GetPreferencesFilePath()
    {
        string path = GameStorePath.GetStorePath();
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
        return Path.Combine(path, "Preferences.txt");
    }

    public  Preferences GetPreferences()
    {
        if (File.Exists(GetPreferencesFilePath()))
        {
            try
            {
                Preferences p = new()
                {
                };
                string[] lines = File.ReadAllLines(GetPreferencesFilePath());
                foreach (string l in lines)
                {
                    int a = l.IndexOf("=", StringComparison.InvariantCultureIgnoreCase);
                    string name = l[..a];
                    string value = l[(a + 1)..];
                    p.SetString(name, value);
                }
                return p;
            }
            catch
            {
                File.Delete(GetPreferencesFilePath());
                return new Preferences();
            }
        }
        else
        {
            Preferences p = new()
            {
            };
            return p;
        }
    }

    public void SetPreferences(Preferences preferences)
    {
        try
        {
            File.WriteAllLines(GetPreferencesFilePath(), preferences.ToLines());
        }
        catch
        {
            // TODO: log write failure
        }
    }

    public bool IsMac = Environment.OSVersion.Platform == PlatformID.MacOSX;

    public  bool MultithreadingAvailable()
    {
        return true;
    }

    public  void QueueUserWorkItem(Action action)
    {
        ThreadPool.QueueUserWorkItem((a) => { action(); });
    }

    private AssetLoader assetloader;
    public List<Asset> LoadAssetsAsyc(out float progress)
    {
        assetloader ??= new AssetLoader(datapaths);
        return assetloader.LoadAssetsAsync(out progress);
    }

    public  bool IsSmallScreen()
    {
        return TouchTest;
    }

    public  void OpenLinkInBrowser(string url)
    {
        if (!(url.StartsWith("http://") || url.StartsWith("https://")))
        {
            //Check if string is an URL - if not, abort
            return;
        }
        Process.Start(url);
    }

    public string Cachepath() { return Path.Combine(PathStorage(), "Cache"); }
    public void Checkcachedir()
    {
        if (!Directory.Exists(Cachepath()))
        {
            Directory.CreateDirectory(Cachepath());
        }
    }

    public  void SaveAssetToCache(Asset tosave)
    {
        //Check if cache directory exists
        Checkcachedir();
        BinaryWriter bw = new(File.Create(Path.Combine(Cachepath(), tosave.md5)));
        bw.Write(tosave.name);
        bw.Write(tosave.dataLength);
        bw.Write(tosave.data);
        bw.Close();
    }

    public  Asset LoadAssetFromCache(string md5)
    {
        //Check if cache directory exists
        Checkcachedir();
        BinaryReader br = new(File.OpenRead(Path.Combine(Cachepath(), md5)));
        string contentName = br.ReadString();
        int contentLength = br.ReadInt32();
        byte[] content = br.ReadBytes(contentLength);
        br.Close();
        Asset a = new()
        {
            data = content,
            dataLength = contentLength,
            md5 = md5,
            name = contentName
        };
        return a;
    }

    public  bool IsCached(string md5)
    {
        if (!Directory.Exists(Cachepath()))
            return false;
        return File.Exists(Path.Combine(Cachepath(), md5));
    }

    public  bool IsChecksum(string checksum)
    {
        //Check if checksum string has correct length
        if (checksum.Length != 32)
        {
            return false;
        }
        //Convert checksum string to lowercase letters
        checksum = checksum.ToLower();
        char[] chars = checksum.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if ((chars[i] < '0' || chars[i] > '9') && (chars[i] < 'a' || chars[i] > 'f'))
            {
                //Return false if any character inside the checksum is not hexadecimal
                return false;
            }
        }
        //Return true if all checks have been passed
        return true;
    }

    public  string DecodeHTMLEntities(string htmlencodedstring)
    {
        return System.Web.HttpUtility.HtmlDecode(htmlencodedstring);
    }

    public  bool IsDebuggerAttached()
    {
        return Debugger.IsAttached;
    }

    public  string QueryStringValue(string key)
    {
        return null;
    }

    #endregion

    #region Audio

    private AudioOpenAl audio;
    public GameExit gameexit;
    private void StartAudio()
    {
        audio ??= new AudioOpenAl
        {
            d_GameExit = gameexit
        };
    }

    public  AudioData AudioDataCreate(byte[] data, int dataLength)
    {
        StartAudio();
        return AudioOpenAl.GetSampleFromArray(data);
    }

    public  bool AudioDataLoaded(AudioData data)
    {
        return true;
    }

    public  AudioCi AudioCreate(AudioData data)
    {
        return audio.CreateAudio((AudioDataCs)data);
    }

    public  void AudioPlay(AudioCi audio_)
    {
        StartAudio();
        ((AudioOpenAl.AudioTask)audio_).Play();
    }

    public  void AudioPause(AudioCi audio_)
    {
        ((AudioOpenAl.AudioTask)audio_).Pause();
    }

    public  void AudioDelete(AudioCi audio_)
    {
        ((AudioOpenAl.AudioTask)audio_).Stop();
    }

    public  bool AudioFinished(AudioCi audio_)
    {
        return ((AudioOpenAl.AudioTask)audio_).Finished;
    }

    public  void AudioSetPosition(AudioCi audio_, float x, float y, float z)
    {
        ((AudioOpenAl.AudioTask)audio_).position = new Vector3(x, y, z);
    }

    public  void AudioUpdateListener(float posX, float posY, float posZ, float orientX, float orientY, float orientZ)
    {
        StartAudio();
        AudioOpenAl.UpdateListener(new Vector3(posX, posY, posZ), new Vector3(orientX, orientY, orientZ));
    }

    #endregion

    #region ENet
    public  bool TcpAvailable()
    {
        return true;
    }

    public bool EnetAvailable() => true;

    public EnetHost EnetCreateHost() => new EnetHostWrapper(new Host());

    public void EnetHostInitialize(EnetHost host, IPEndPointCi? address, int peerLimit,
        int channelLimit, int incomingBandwidth, int outgoingBandwidth)
    {
        // Client hosts always pass null address.
        if (address != null)
            throw new ArgumentException("Client ENet host must have a null address.");

        ((EnetHostWrapper)host).Host.Create(peerLimit, channelLimit,
            (uint)incomingBandwidth, (uint)outgoingBandwidth);
    }

    public void EnetHostInitializeServer(EnetHost host, int port, int peerLimit)
    {
        ((EnetHostWrapper)host).Host.Create(port, peerLimit);
    }

    public EnetEvent? EnetHostService(EnetHost host, int timeout)
    {
        int ret = ((EnetHostWrapper)host).Host.Service(timeout, out Event e);
        return ret > 0 ? new EnetEventWrapper(e) : null;
    }

    public EnetEvent? EnetHostCheckEvents(EnetHost host)
    {
        int ret = ((EnetHostWrapper)host).Host.CheckEvents(out Event e);
        return ret > 0 ? new EnetEventWrapper(e) : null;
    }

    public EnetPeer EnetHostConnect(EnetHost host, string hostName, int port, int channelCount, int data)
    {
        Address address = new() { Port = (ushort)port };
        address.SetHost(hostName);
        Peer peer = ((EnetHostWrapper)host).Host.Connect(address, channelCount, (uint)data);
        return new EnetPeerWrapper(peer);
    }

    public void EnetPeerSend(EnetPeer peer, int channelId, ReadOnlyMemory<byte> payload, int flags)
    {
        try
        {
            Packet packet = default;
            packet.Create(payload.ToArray(), payload.Length, (PacketFlags)flags);
            ((EnetPeerWrapper)peer).Peer.Send((byte)channelId, ref packet);
        }
        catch (Exception ex)
        {
        }
    }

    // ---------------------------------------------------------------------------
    // Native wrappers — thin shells that satisfy our abstract types.
    // All live in the platform assembly, not in game logic.
    // ---------------------------------------------------------------------------

    /// <summary>Wraps ENet-CSharp's Host struct.</summary>
    internal sealed class EnetHostWrapper : EnetHost
    {
        internal readonly Host Host;
        internal EnetHostWrapper(Host host) => Host = host;
    }

    /// <summary>Wraps ENet-CSharp's Peer struct.</summary>
    internal sealed class EnetPeerWrapper : EnetPeer
    {
        internal Peer Peer; // Not readonly — Peer is a struct, SetUserData must mutate it in place
        internal EnetPeerWrapper(Peer peer) => Peer = peer;

        public override int UserData() => (int)Peer.Data;
        public override void SetUserData(int value) => Peer.Data = value;
        public override IPEndPointCi GetRemoteAddress() =>
            IPEndPointCiDefault.Create(Peer.IP);
    }

    /// <summary>
    /// Wraps ENet-CSharp's Event struct.
    /// Only allocated when an event actually occurred (ret > 0 from Service/CheckEvents).
    /// </summary>
    internal sealed class EnetEventWrapper : EnetEvent
    {
        private readonly Event _e;
        internal EnetEventWrapper(Event e) => _e = e;

        public override EnetEventType Type() => _e.Type switch
        {
            EventType.Connect => EnetEventType.Connect,
            EventType.Disconnect => EnetEventType.Disconnect,
            EventType.Receive => EnetEventType.Receive,
            EventType.Timeout => EnetEventType.Disconnect, // treat timeout as disconnect
            _ => EnetEventType.None,
        };

        public override EnetPeer Peer() => new EnetPeerWrapper(_e.Peer);

        public override EnetPacket Packet() => new EnetPacketWrapper(_e.Packet);
    }

    /// <summary>Wraps ENet-CSharp's Packet struct.</summary>
    internal sealed class EnetPacketWrapper : EnetPacket
    {
        private readonly Packet _p;
        internal EnetPacketWrapper(Packet p) => _p = p;

        public override int GetBytesCount() => _p.Length;
        public override byte[] GetBytes()
        {
            byte[] buffer = new byte[_p.Length];
            _p.CopyTo(buffer);
            return buffer;
        }
        public override void Dispose() => _p.Dispose();
    }
    #endregion

    #region WebSocket

    public bool WebSocketAvailable()
    {
        return false;
    }

    public  void WebSocketConnect(string ip, int port)
    {
    }

    public  void WebSocketSend(byte[] data, int dataLength)
    {
    }

    public  int WebSocketReceive(byte[] data, int dataLength)
    {
        return -1;
    }

    #endregion

    #region OpenGlImpl

    public GameWindow window;

    public  int GetCanvasWidth()
    {
        return window.ClientSize.X;
    }

    public  int GetCanvasHeight()
    {
        return window.ClientSize.Y;
    }

    public void Start()
    {
        window.KeyDown += GameKeyDown;
        window.KeyUp += GameKeyUp;
        window.TextInput += GameTextInput;
        window.MouseDown += Mouse_ButtonDown;
        window.MouseUp += Mouse_ButtonUp;
        window.MouseMove += Mouse_Move;
        window.MouseWheel += Mouse_WheelChanged;
        window.RenderFrame += WindowRenderFrame;
        window.Closing += WindowClosed;
        window.Title = "Manic Digger";
        //GL.Enable(EnableCap.DebugOutput);
       // GL.Enable(EnableCap.DebugOutputSynchronous);
        GL.DebugMessageCallback((source, type, id, severity, length, message, param) =>
        {
            if (severity == DebugSeverity.DebugSeverityNotification)
                return; // ignore info messages like this one
            string msg = Marshal.PtrToStringAnsi(message, length);
            Console.WriteLine($"[OpenGL] [{severity}] [{type}] {msg}");

        }, IntPtr.Zero);
    }

    private void WindowClosed(CancelEventArgs e)
    {
        gameexit.exit = e.Cancel;
    }

    public  void SetVSync(bool enabled)
    {
        window.VSync = enabled ? VSyncMode.On : VSyncMode.Off;
    }

    private readonly Screenshot screenshot = new();

    public  void SaveScreenshot()
    {
        screenshot.d_GameWindow = window;
        screenshot.SaveScreenshot();
    }

    public  Bitmap GrabScreenshot()
    {
        screenshot.d_GameWindow = window;
        Bitmap bmp = screenshot.GrabScreenshot();
        return bmp;
    }

    public  void WindowExit()
    {
        gameexit?.exit = true;
        window.Close();
    }

    public  void SetTitle(string applicationname)
    {
        window.Title = applicationname;
    }

    public  string KeyName(int key)
    {
        if (Enum.IsDefined(typeof(Keys), key))
        {
            return Enum.GetName(typeof(Keys), key)!;
        }
        return key.ToString();
    }

    private DisplayResolutionCi[] resolutions;
    private int resolutionsCount;
    public  DisplayResolutionCi[] GetDisplayResolutions(out int retResolutionsCount)
    {
        if (resolutions == null)
        {
            resolutions = new DisplayResolutionCi[1024];
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                var r2 = new DisplayResolutionCi
                {
                    Width = screen.Bounds.Width,
                    Height = screen.Bounds.Height,
                    BitsPerPixel = screen.BitsPerPixel,
                    RefreshRate = 60 // Screen doesn't expose refresh rate
                };
                if (r2.Width < 800 || r2.Height < 600 || r2.BitsPerPixel < 16)
                    continue;
                resolutions[resolutionsCount++] = r2;
            }
        }
        retResolutionsCount = resolutionsCount;
        return resolutions;
    }

    public  WindowState GetWindowState()
    {
        return window.WindowState;
    }

    public  void SetWindowState(WindowState value)
    {
        window.WindowState = value;
    }

    public  void ChangeResolution(int width, int height, int bitsPerPixel, float refreshRate)
    {
        window.Size = new Vector2i(width, height);
    }

    public  DisplayResolutionCi GetDisplayResolutionDefault()
    {
        var screen = System.Windows.Forms.Screen.PrimaryScreen!;
        var r = new DisplayResolutionCi
        {
            Width = screen.Bounds.Width,
            Height = screen.Bounds.Height,
            BitsPerPixel = screen.BitsPerPixel,
            RefreshRate = 60
        };
        return r;
    }

    #endregion

    #region OpenGl
    public  void GlViewport(int x, int y, int width, int height)
    {
        GL.Viewport(x, y, width, height);
    }

    public  void GlClearColorBufferAndDepthBuffer()
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public  void GlDisableDepthTest()
    {
        GL.Disable(EnableCap.DepthTest);
    }

    public  void BindTexture2d(int texture)
    {
        GL.BindTexture(TextureTarget.Texture2D, texture);
        if (_shaderProgram != -1)
            GL.Uniform1(_uUseTexture, texture != 0 ? 1 : 0);
    }

    private int _uUseTexture;
    private int _uProjection;
    private int _uModelView;
    private int _uAmbientLight;
    private int _uFogEnabled;
    private int _uFogColor;
    private int _uFogDensity;

    public Model CreateModel(ModelData data)
    {
        int vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        // positions → attribute 0
        int vertexVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.xyz.Length * sizeof(float), data.xyz, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
        GL.EnableVertexAttribArray(0);

        // colors → attribute 1
        int colorVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, colorVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.rgba.Length * sizeof(byte), data.rgba, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.UnsignedByte, true, 0, 0);
        GL.EnableVertexAttribArray(1);

        // UVs → attribute 2
        int uvVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, uvVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.uv.Length * sizeof(float), data.uv, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 0, 0);
        GL.EnableVertexAttribArray(2);

        // indices
        int indexVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexVbo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, data.indices.Length * sizeof(int), data.indices, BufferUsageHint.StaticDraw);

        GL.BindVertexArray(0);

        data.vaoId = vao;
        data.vertexVboId = vertexVbo;
        data.colorVboId = colorVbo;
        data.uvVboId = uvVbo;
        data.indexVboId = indexVbo;
        return data;
    }

    public void UpdateModel(ModelData data)
    {
        if (data.vaoId == 0)
        {
            CreateModel(data);
            return;
        }

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.vertexVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.xyz.Length * sizeof(float), data.xyz);

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.colorVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.rgba.Length * sizeof(byte), data.rgba);

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.uvVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.uv.Length * sizeof(float), data.uv);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, data.indexVboId);
        GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero,
            data.indices.Length * sizeof(int), data.indices);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public void UpdateModelColors(ModelData data)
    {
        if (data.vaoId == 0)
        {
            // first time - full upload
            CreateModel(data);
            return;
        }

        // re-upload only the color buffer
        GL.BindBuffer(BufferTarget.ArrayBuffer, data.colorVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.rgba.Length * sizeof(byte), data.rgba);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public  void DrawModelData(ModelData data)
    {
        GL.UseProgram(_shaderProgram);
        GL.UniformMatrix4(_uProjection, false, ref _projectionMatrix);
        GL.UniformMatrix4(_uModelView, false, ref _modelViewMatrix);
        GL.BindVertexArray(data.vaoId);
        PrimitiveType primitiveType = data.mode == DrawModeEnum.Triangles
            ? PrimitiveType.Triangles
            : PrimitiveType.Lines;
        GL.DrawElements(primitiveType, data.indicesCount, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    public void DrawModel(Model model)
    {
        DrawModelData((ModelData)model);
    }

    public  void DrawModels(List<Model> models, int count)
    {
        for (int i = 0; i < count; i++)
        {
            DrawModelData((ModelData)models[i]);
        }
    }

    public  void InitShaders()
    {
        string vertexSource = @"
        #version 330 core

        layout(location = 0) in vec3 aPosition;
        layout(location = 1) in vec4 aColor;
        layout(location = 2) in vec2 aUv;

        uniform mat4 uProjection;
        uniform mat4 uModelView;

        out vec4 vColor;
        out vec2 vUv;
        out float vFogDepth;

        void main()
        {
            vec4 viewPos = uModelView * vec4(aPosition, 1.0);
            gl_Position = uProjection * viewPos;
            vColor = aColor;
            vUv = aUv;
            vFogDepth = abs(viewPos.z);
        }
    ";

        string fragmentSource = @"
        #version 330 core

        in vec4 vColor;
        in vec2 vUv;
        in float vFogDepth;

        uniform sampler2D uTexture;
        uniform vec3 uAmbientLight;
        uniform vec4 uFogColor;
        uniform float uFogDensity;
        uniform bool uFogEnabled;
        uniform bool uUseTexture;
        
        out vec4 fragColor;

        void main()
        {
            if (uUseTexture)
                fragColor = texture(uTexture, vUv) * vColor;
            else
                fragColor = vColor; // sky sphere, hand tint, etc.
            
            // only discard when texturing — alpha test doesn't apply to vertex-colored geometry
            if (uUseTexture && fragColor.a < 0.5)
                discard;

            fragColor.rgb *= uAmbientLight;

            if (uFogEnabled)
            {
                float fogFactor = exp(-uFogDensity * uFogDensity * vFogDepth * vFogDepth);
                fogFactor = clamp(fogFactor, 0.0, 1.0);
                fragColor.rgb = mix(uFogColor.rgb, fragColor.rgb, fogFactor);
            }
        }
    ";

        // compile vertex shader
        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexSource);
        GL.CompileShader(vertexShader);
        GL.GetShader(vertexShader, ShaderParameter.CompileStatus, out int vertStatus);
        if (vertStatus == 0)
            throw new Exception($"Vertex shader error: {GL.GetShaderInfoLog(vertexShader)}");

        // compile fragment shader
        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentSource);
        GL.CompileShader(fragmentShader);
        GL.GetShader(fragmentShader, ShaderParameter.CompileStatus, out int fragStatus);
        if (fragStatus == 0)
            throw new Exception($"Fragment shader error: {GL.GetShaderInfoLog(fragmentShader)}");

        // link program
        _shaderProgram = GL.CreateProgram();
        GL.AttachShader(_shaderProgram, vertexShader);
        GL.AttachShader(_shaderProgram, fragmentShader);
        GL.LinkProgram(_shaderProgram);
        GL.GetProgram(_shaderProgram, GetProgramParameterName.LinkStatus, out int linkStatus);
        if (linkStatus == 0)
            throw new Exception($"Shader link error: {GL.GetProgramInfoLog(_shaderProgram)}");
        
        // cleanup - shaders are linked into program, no longer needed
        GL.DetachShader(_shaderProgram, vertexShader);
        GL.DetachShader(_shaderProgram, fragmentShader);
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);

        // set initial uniform values
        GL.UseProgram(_shaderProgram);
        GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uTexture"), 0);
        GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "uAmbientLight"), 1f, 1f, 1f);
        GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uFogEnabled"), 0);
        GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uFogDensity"), _fogDensity);
        GL.Uniform4(GL.GetUniformLocation(_shaderProgram, "uFogColor"), _fogColor);
        _uUseTexture = GL.GetUniformLocation(_shaderProgram, "uUseTexture");
        _uProjection = GL.GetUniformLocation(_shaderProgram, "uProjection");
        _uModelView = GL.GetUniformLocation(_shaderProgram, "uModelView");
        _uAmbientLight = GL.GetUniformLocation(_shaderProgram, "uAmbientLight");
        _uFogEnabled = GL.GetUniformLocation(_shaderProgram, "uFogEnabled");
        _uFogColor = GL.GetUniformLocation(_shaderProgram, "uFogColor");
        _uFogDensity = GL.GetUniformLocation(_shaderProgram, "uFogDensity");
        GL.UseProgram(_shaderProgram);
    }

    private Matrix4 _projectionMatrix;
    private Matrix4 _modelViewMatrix;
    private int _shaderProgram = -1; // will be set when we create shaders

    public void SetMatrixUniformProjection(ref Matrix4 pMatrix)
    {
        _projectionMatrix = pMatrix;
        if (_shaderProgram != -1)
            GL.UniformMatrix4(_uProjection, false, ref _projectionMatrix);
    }

    public void SetMatrixUniformModelView(ref Matrix4 mvMatrix)
    {
        _modelViewMatrix = mvMatrix;
        if (_shaderProgram != -1)
            GL.UniformMatrix4(_uModelView, false, ref _modelViewMatrix);
    }

    public  void GlClearColorRgbaf(float r, float g, float b, float a)
    {
        GL.ClearColor(r, g, b, a);
    }

    public  void GlEnableDepthTest()
    {
        GL.Enable(EnableCap.DepthTest);
    }

    public bool ALLOW_NON_POWER_OF_TWO = false;
    public bool ENABLE_MIPMAPS = true;
    public bool ENABLE_TRANSPARENCY = true;

    //http://www.opentk.com/doc/graphics/textures/loading
    public int LoadTexture(Bitmap bmpArg, bool linearMag)
    {
        Bitmap bmp = bmpArg;
        bool convertedbitmap = false;
        if (!ALLOW_NON_POWER_OF_TWO &&
            !(BitOperations.IsPow2((uint)bmp.Width) && BitOperations.IsPow2((uint)bmp.Height)))
        {
            Bitmap bmp2 = new(
                (int)BitOperations.RoundUpToPowerOf2((uint)bmp.Width),
                (int)BitOperations.RoundUpToPowerOf2((uint)bmp.Height)
            );
            using (Graphics g = Graphics.FromImage(bmp2))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(bmp, 0, 0, bmp2.Width, bmp2.Height);
            }
            convertedbitmap = true;
            bmp = bmp2;
        }
       // GL.Enable(EnableCap.Texture2D);
        int id = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2D, id);
        if (!ENABLE_MIPMAPS)
        {
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
        }
        else
        {
            //GL.GenerateMipmap(GenerateMipmapTarget.Texture2D); //DOES NOT WORK ON ATI GRAPHIC CARDS
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.GenerateMipmap, 1); //DOES NOT WORK ON ???
            int[] MipMapCount = new int[1];
            GL.GetTexParameter(TextureTarget.Texture2D, GetTextureParameter.TextureMaxLevel, out MipMapCount[0]);
            if (MipMapCount[0] == 0)
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            }
            else
            {
                GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.NearestMipmapLinear);
            }
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, linearMag ? (int)TextureMagFilter.Linear : (int)TextureMagFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMaxLevel, 4);
        }
        BitmapData bmp_data = bmp.LockBits(new Rectangle(0, 0, bmp.Width, bmp.Height), ImageLockMode.ReadOnly, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, bmp_data.Width, bmp_data.Height, 0,
            OpenTK.Graphics.OpenGL4.PixelFormat.Bgra, PixelType.UnsignedByte, bmp_data.Scan0);

        bmp.UnlockBits(bmp_data);

        GL.Enable(EnableCap.DepthTest);

        if (ENABLE_TRANSPARENCY)
        {
            // TODO: alpha test moved to fragment shader
            // GL.Enable(EnableCap.AlphaTest);
            // GL.AlphaFunc(AlphaFunction.Greater, 0.5f);
        }


        if (ENABLE_TRANSPARENCY)
        {
            GL.Enable(EnableCap.Blend);
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
            //GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvMode, (int)TextureEnvMode.Blend);
            //GL.TexEnv(TextureEnvTarget.TextureEnv, TextureEnvParameter.TextureEnvColor, new Color4(0, 0, 0, byte.MaxValue));
        }

        if (convertedbitmap)
        {
            bmp.Dispose();
        }
        return id;
    }

    public  void GlDisableCullFace()
    {
        GL.Disable(EnableCap.CullFace);
    }

    public  void GlEnableCullFace()
    {
        GL.Enable(EnableCap.CullFace);
    }

    public  void DeleteModel(Model model)
    {
        ModelData m = (ModelData)model;
        GL.DeleteVertexArray(m.vaoId);
        GL.DeleteBuffer(m.vertexVboId);
        GL.DeleteBuffer(m.colorVboId);
        GL.DeleteBuffer(m.uvVboId);
        GL.DeleteBuffer(m.indexVboId);
    }

    public  void GlEnableTexture2d()
    {
       // GL.Enable(EnableCap.Texture2D);
    }

    public  void GLLineWidth(int width)
    {
        GL.LineWidth(width);
    }

    public void GLDisableAlphaTest()
    {
        // TODO: alpha test moved to fragment shader (discard if alpha < 0.5)
    }

    public void GLEnableAlphaTest()
    {
        // TODO: alpha test moved to fragment shader (discard if alpha < 0.5)
    }

    public  void GLDeleteTexture(int id)
    {
        GL.DeleteTexture(id);
    }

    public  void GlClearDepthBuffer()
    {
        GL.Clear(ClearBufferMask.DepthBufferBit);
    }

    private Vector3 _ambientLight = Vector3.One;
    private Vector4 _fogColor = Vector4.One;
    private float _fogDensity = 0.003f;
    private bool _fogEnabled = false;

    public void GlLightModelAmbient(int r, int g, int b)
    {
        _ambientLight = new Vector3(r / 255f, g / 255f, b / 255f);
        if (_shaderProgram != -1)
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "uAmbientLight"),
                _ambientLight.X, _ambientLight.Y, _ambientLight.Z);
    }

    public void GlEnableFog()
    {
        _fogEnabled = true;
        if (_shaderProgram != -1)
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uFogEnabled"), 1);
    }

    public void GlDisableFog()
    {
        _fogEnabled = false;
        if (_shaderProgram != -1)
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uFogEnabled"), 0);
    }

    public void GlFogFogColor(int r, int g, int b, int a)
    {
        _fogColor = new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        if (_shaderProgram != -1)
            GL.Uniform4(GL.GetUniformLocation(_shaderProgram, "uFogColor"), _fogColor);
    }

    public void GlFogFogDensity(float density)
    {
        _fogDensity = density;
        if (_shaderProgram != -1)
            GL.Uniform1(GL.GetUniformLocation(_shaderProgram, "uFogDensity"), _fogDensity);
    }

    public  int GlGetMaxTextureSize()
    {
        int size = 1024;
        try
        {
            GL.GetInteger(GetPName.MaxTextureSize, out size);
        }
        catch
        {
        }
        return size;
    }

    public  void GlDepthMask(bool flag)
    {
        GL.DepthMask(flag);
    }

    public void GlCullFaceBack()
    {
        GL.CullFace(TriangleFace.Back);
    }

    public void GlEnableLighting()
    {
        // TODO: lighting moved to shader, use _ambientLight uniform
    }

    public void GlEnableColorMaterial()
    {
        // no equivalent needed - shader reads vertex color attribute directly
    }

    public void GlColorMaterialFrontAndBackAmbientAndDiffuse()
    {
        // no equivalent needed - shader handles material properties
    }

    public void GlShadeModelSmooth()
    {
        // no equivalent needed - interpolation across fragments is default in modern GL
    }

    #endregion

    #region Game

    private bool singlePlayerServerAvailable = true;
    public  bool SinglePlayerServerAvailable()
    {
        return singlePlayerServerAvailable;
    }

    public  void SinglePlayerServerStart(string saveFilename)
    {
        singlepLayerServerExit = false;
        StartSinglePlayerServer(saveFilename);
    }

    public bool singlepLayerServerExit;
    public  void SinglePlayerServerExit()
    {
        singlepLayerServerExit = true;
    }

    public System.Action<string> StartSinglePlayerServer;
    public bool singlePlayerServerLoaded;

    public  bool SinglePlayerServerLoaded()
    {
        return singlePlayerServerLoaded;
    }
    public DummyNetwork singlePlayerServerDummyNetwork;
    public  DummyNetwork SinglePlayerServerGetNetwork()
    {
        return singlePlayerServerDummyNetwork;
    }

    public  void SinglePlayerServerDisable()
    {
        singlePlayerServerAvailable = false;
    }

    public  EnetNetConnection CastToEnetNetConnection(NetConnection connection)
    {
        return (EnetNetConnection)connection;
    }

    public  PlayerInterpolationState CastToPlayerInterpolationState(InterpolatedObject a)
    {
        return (PlayerInterpolationState)a;
    }

    #endregion

    #region Event handlers

    public List<Action<float>> newFrameHandlers = new();
    public void AddOnNewFrame(Action<float> handler)
    {
        newFrameHandlers.Add(handler);
    }

    public List<Action<KeyEventArgs>> keyDownHandlers = new();
    public List<Action<KeyEventArgs>> keyUpHandlers = new();
    public List<Action<KeyPressEventArgs>> keyPressHandlers = new();

    public void AddOnKeyEvent(
        Action<KeyEventArgs> onKeyDown,
        Action<KeyEventArgs> onKeyUp,
        Action<KeyPressEventArgs> onKeyPress)
    {
        keyDownHandlers.Add(onKeyDown);
        keyUpHandlers.Add(onKeyUp);
        keyPressHandlers.Add(onKeyPress);
    }

    public List<KeyEventHandler> keyEventHandlers = new();
    public  void AddOnKeyEvent(KeyEventHandler handler)
    {
        keyEventHandlers.Add(handler);
    }

    public List<MouseEventHandler> mouseEventHandlers = new();
    public  void AddOnMouseEvent(MouseEventHandler handler)
    {
        mouseEventHandlers.Add(handler);
    }

    public List<TouchEventHandler> touchEventHandlers = new();
    public  void AddOnTouchEvent(TouchEventHandler handler)
    {
        touchEventHandlers.Add(handler);
    }

    public CrashReporter crashreporter;
    public  void AddOnCrash(OnCrashHandler handler)
    {
        crashreporter.OnCrash += handler.OnCrash;
    }

    #endregion

    #region Input

    private bool mousePointerLocked;
    private bool mouseCursorVisible = true;
    //private MouseState current, previous;
    private float lastX, lastY;

    public  bool IsMousePointerLocked()
    {
        return mousePointerLocked;
    }

    public  bool MouseCursorIsVisible()
    {
        return mouseCursorVisible;
    }

    public  void SetWindowCursor(int hotx, int hoty, int sizex, int sizey, byte[] imgdata, int imgdataLength)
    {
        try
        {
            Bitmap bmp = new(new MemoryStream(imgdata, 0, imgdataLength)); //new Bitmap("data/local/gui/mousecursor.png");
            if (bmp.Width > 32 || bmp.Height > 32)
            {
                // Limit cursor size to 32x32
                return;
            }
            // Convert to required 0xBBGGRRAA format - see https://github.com/opentk/opentk/pull/107#issuecomment-41771702
            int i = 0;
            byte[] data = new byte[4 * bmp.Width * bmp.Height];
            for (int y = 0; y < bmp.Width; y++)
            {
                for (int x = 0; x < bmp.Height; x++)
                {
                    Color color = bmp.GetPixel(x, y);
                    data[i] = color.B;
                    data[i + 1] = color.G;
                    data[i + 2] = color.R;
                    data[i + 3] = color.A;
                    i += 4;
                }
            }
            bmp.Dispose();
            window.Cursor = new MouseCursor(hotx, hoty, sizex, sizey, data);
        }
        catch
        {
            RestoreWindowCursor();
        }
    }

    public  void RestoreWindowCursor()
    {
        window.Cursor = MouseCursor.Default;
    }

    public static int ToGlKey(Keys key)
    {
        return (int)key;
    }

    public  void MouseCursorSetVisible(bool value)
    {
        if (!value)
        {
            if (TouchTest)
            {
                return;
            }
            if (!mouseCursorVisible)
            {
                //Cursor already hidden. Do nothing.
                return;
            }
            window.CursorState = CursorState.Grabbed;
            mouseCursorVisible = false;
        }
        else
        {
            if (mouseCursorVisible)
            {
                //Cursor already visible. Do nothing.
                return;
            }
            window.CursorState = CursorState.Normal;
            mouseCursorVisible = true;
        }
    }

    public  void RequestMousePointerLock()
    {
        MouseCursorSetVisible(false);
        mousePointerLocked = true;
    }

    public  void ExitMousePointerLock()
    {
        MouseCursorSetVisible(true);
        mousePointerLocked = false;
    }

    public  bool Focused()
    {
        return window.IsFocused;
    }

    private void WindowRenderFrame(FrameEventArgs e)
    {
        UpdateMousePosition();
        foreach (Action<float> h in newFrameHandlers)
            h((float)e.Time);
        window.SwapBuffers();
    }

    private void UpdateMousePosition()
    {
        if (!window.IsFocused)
        {
            return;
        }

        // Mouse state has changed
        var mouse = window.MouseState;
        float xdelta = mouse.Delta.X;
        float ydelta = mouse.Delta.Y;

        if (xdelta != 0 || ydelta != 0)
        {
            foreach (MouseEventHandler h in mouseEventHandlers)
            {
                MouseEventArgs args = new();
                args.SetX((int)mouse.Position.X);
                args.SetY((int)mouse.Position.Y);
                args.SetMovementX((int)xdelta);
                args.SetMovementY((int)ydelta);
                args.SetEmulated(true);
                h.OnMouseMove(args);
            }
        }
    }

    private void Mouse_WheelChanged(MouseWheelEventArgs e)
    {
        foreach (MouseEventHandler h in mouseEventHandlers)
        {
            h.OnMouseWheel(e);
        }
    }

    private void Mouse_ButtonDown(MouseButtonEventArgs e)
    {
        var pos = window.MousePosition;
        if (TouchTest)
        {
            foreach (TouchEventHandler h in touchEventHandlers)
            {
                TouchEventArgs args = new();
                args.SetX((int)pos.X);
                args.SetY((int)pos.Y);
                args.SetId(0);
                h.OnTouchStart(args);
            }
        }
        else
        {
            foreach (MouseEventHandler h in mouseEventHandlers)
            {
                MouseEventArgs args = new();
                args.SetX((int)pos.X);
                args.SetY((int)pos.Y);
                args.SetButton((int)e.Button);
                h.OnMouseDown(args);
            }
        }
    }

    private void Mouse_ButtonUp(MouseButtonEventArgs e)
    {
        var pos = window.MousePosition;
        if (TouchTest)
        {
            foreach (TouchEventHandler h in touchEventHandlers)
            {
                TouchEventArgs args = new();
                args.SetX((int)pos.X);
                args.SetY((int)pos.Y);
                args.SetId(0);
                h.OnTouchEnd(args);
            }
        }
        else
        {
            foreach (MouseEventHandler h in mouseEventHandlers)
            {
                MouseEventArgs args = new();
                args.SetX((int)pos.X);
                args.SetY((int)pos.Y);
                args.SetButton((int)e.Button);
                h.OnMouseUp(args);
            }
        }
    }

    private void Mouse_Move(MouseMoveEventArgs e)
    {
        try
        {
            lastX = e.X;
            lastY = e.Y;
            Console.WriteLine($"Mouse_Move: {e.X}, {e.Y}, delta: {e.DeltaX}, {e.DeltaY}");

            if (TouchTest)
            {
                Console.WriteLine("TouchTest path");
                foreach (TouchEventHandler h in touchEventHandlers)
                {
                    Console.WriteLine($"Touch handler: {h}");
                    TouchEventArgs args = new();
                    args.SetX((int)e.X);
                    args.SetY((int)e.Y);
                    args.SetId(0);
                    h.OnTouchMove(args);
                }
            }
            else
            {
                Console.WriteLine("Mouse path");
                foreach (MouseEventHandler h in mouseEventHandlers)
                {
                    Console.WriteLine($"Mouse handler: {h}");
                    MouseEventArgs args = new();
                    args.SetX((int)e.X);
                    args.SetY((int)e.Y);
                    args.SetMovementX((int)e.DeltaX);
                    args.SetMovementY((int)e.DeltaY);
                    args.SetEmulated(false);
                    h.OnMouseMove(args);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"CRASH in Mouse_Move: {ex}");
        }
    }

    private void GameTextInput(TextInputEventArgs e)
    {
        var args = new KeyPressEventArgs { KeyChar = e.Unicode };
        foreach (var h in keyPressHandlers)
        {
            h(args);
            if (args.Handled) break;
        }
    }

    private void GameKeyDown(KeyboardKeyEventArgs e)
    {
        KeyEventArgs args = new()
        {
            KeyChar = ToGlKey(e.Key),
            CtrlPressed = e.Modifiers == KeyModifiers.Control,
            ShiftPressed = e.Modifiers == KeyModifiers.Shift,
            AltPressed = e.Modifiers == KeyModifiers.Alt
        };
        foreach (var h in keyDownHandlers)
        {
            h(args);
            if (args.Handled) break;
        }
    }

    private void GameKeyUp(KeyboardKeyEventArgs e)
    {
        KeyEventArgs args = new() { KeyChar = ToGlKey(e.Key) };
        foreach (var h in keyUpHandlers)
        {
            h(args);
            if (args.Handled) break;
        }
    }

    #endregion
}

public class AviWriterCiCs : AviWriterCi
{
    public AviWriterCiCs()
    {
        avi = new AviWriter();
    }

    public AviWriter avi;
    public Bitmap openbmp;

    public override void Open(string filename, int framerate, int width, int height)
    {
        openbmp = avi.Open(filename, (uint)framerate, width, height);
    }

    public override void AddFrame(Bitmap bitmap)
    {
        var bmp_ = bitmap;

        using (Graphics g = Graphics.FromImage(openbmp))
        {
            g.DrawImage(bmp_, 0, 0);
        }
        openbmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

        avi.AddFrame();
    }

    public override void Close()
    {
        avi.Close();
    }
}

public class TextureNative : Texture
{
    public int value;
}

public class GameWindowNative : GameWindow
{
    public GamePlatformNative platform;
    public GameWindowNative()
        : base(
            new GameWindowSettings
            {

                UpdateFrequency = 0 // unlimited,
            },
            new NativeWindowSettings
            {
                ClientSize = new Vector2i(1280, 720),
                Title = "",
                WindowState = WindowState.Normal,
                Profile = ContextProfile.Compatability,
               // APIVersion = new Version(3, 3),
            })
    {
    }
}