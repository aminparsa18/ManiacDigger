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
using System.IO.Compression;
using System.Net;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using static ManicDigger.AudioOpenAl;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using Vector3 = OpenTK.Mathematics.Vector3;
using Vector4 = OpenTK.Mathematics.Vector4;

public class GamePlatformNative : IGamePlatform
{
    #region Misc
    public GamePlatformNative()
    {
        ThreadPool.SetMinThreads(32, 32);
        ThreadPool.SetMaxThreads(128, 128);
        start.Start();
    }

    public bool TouchTest = false;

    public static string PathSavegames => Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

   
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
            args.response.Data = result.ServerThumbnail;
            args.response.ServerMessage = message;
            args.response.Done = true;
        }
        else
        {
            args.response.ServerMessage = message;
            args.response.Error = true;
        }
    }

    private class ThumbnailDownloadArgs
    {
        public string ip;
        public int port;
        public ThumbnailResponseCi response;
    }

    private readonly Stopwatch start = new();

    public int TimeMillisecondsFromStart => (int)start.ElapsedMilliseconds;

    public bool IsMono = RuntimeInformation.IsOSPlatform(OSPlatform.Linux);

    public int LoadTextureFromBitmap(Bitmap bmp)
    {
        return LoadTexture(bmp, false);
    }

    public IAviWriter AviWriterCreate()
    {
        AviWriterCiCs avi = new();
        return avi;
    }

    public string PathStorage()
    {
        return GameStorePath.GetStorePath();
    }

    public string GetGameVersion()
    {
        return GameVersion.Version;
    }

    public void GzipDecompress(byte[] compressed, int compressedLength, byte[] ret)
    {
        // MemoryStream(byte[], int, int) wraps the existing array without copying.
        // GZipStream reads from it and writes the decompressed bytes directly into
        // ret via the Read loop — no intermediate byte[] allocation at any point.
        using var source = new MemoryStream(compressed, 0, compressedLength, writable: false);
        using var gz = new GZipStream(source, CompressionMode.Decompress);

        int totalRead = 0;
        int bytesRead;
        while ((bytesRead = gz.Read(ret, totalRead, ret.Length - totalRead)) > 0)
            totalRead += bytesRead;
    }

    public byte[] GzipCompress(byte[] data, int dataLength)
    {
        using var output = new MemoryStream();
        using (var gz = new GZipStream(output, CompressionMode.Compress, leaveOpen: true))
            gz.Write(data, 0, dataLength);
        // GZipStream must be disposed (flushed) before reading — leaveOpen keeps
        // output accessible after gz is done writing the GZip footer.
        byte[] result = output.ToArray();
        return result;
    }

    public bool ENABLE_CHATLOG = true;
    public string gamepathlogs() { return Path.Combine(PathStorage(), "Logs"); }
    private static string MakeValidFileName(string name)
    {
        string invalidChars = Regex.Escape(new string(Path.GetInvalidFileNameChars()));
        string invalidReStr = string.Format(@"[{0}]", invalidChars);
        return Regex.Replace(name, invalidReStr, "_");
    }
    public bool ChatLog(string servername, string p)
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
        File.AppendAllText(filename, string.Format("{0} {1}\n", DateTime.Now, p));
        return true;
    }

    public bool IsValidTypingChar(int c_)
    {
        char c = (char)c_;
        return (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)
                    || char.IsPunctuation(c) || char.IsSeparator(c) || char.IsSymbol(c))
                    && c != '\r' && c != '\t';
    }

    public void MessageBoxShowError(string text, string caption)
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
        public HttpResponse response;
    }

    public void WebClientUploadDataAsync(string url, byte[] data, int dataLength, HttpResponse response)
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
            d.response.Value = m.ToArray();
            d.response.Done = true;

            request.Abort();

        }
        catch
        {
            d.response.Error = true;
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

    public string FileOpenDialog(string extension, string extensionName, string initialDirectory)
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

    public void ApplicationDoEvents()
    {
        if (IsMono)
        {
            Application.DoEvents();
            Thread.Sleep(0);
        }
    }

    public void ThreadSpinWait(int iterations)
    {
        Thread.SpinWait(iterations);
    }

    public void ShowKeyboard(bool show)
    {
    }

    public bool IsFastSystem()
    {
        return false;
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

    public Preferences GetPreferences()
    {
        if (File.Exists(GetPreferencesFilePath()))
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
        File.WriteAllLines(GetPreferencesFilePath(), preferences.ToLines());
    }

    public bool IsMac = Environment.OSVersion.Platform == PlatformID.MacOSX;

    public bool MultithreadingAvailable()
    {
        return true;
    }

    public void QueueUserWorkItem(Action action)
    {
        ThreadPool.QueueUserWorkItem((a) => { action(); });
    }

    public bool IsSmallScreen()
    {
        return TouchTest;
    }

    public void OpenLinkInBrowser(string url)
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

    public void SaveAssetToCache(Asset tosave)
    {
        //Check if cache directory exists
        Checkcachedir();
        BinaryWriter bw = new(File.Create(Path.Combine(Cachepath(), tosave.md5)));
        bw.Write(tosave.name);
        bw.Write(tosave.dataLength);
        bw.Write(tosave.data);
        bw.Close();
    }

    public Asset LoadAssetFromCache(string md5)
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

    public bool IsCached(string md5)
    {
        if (!Directory.Exists(Cachepath()))
            return false;
        return File.Exists(Path.Combine(Cachepath(), md5));
    }

    public bool IsDebuggerAttached()
    {
        return Debugger.IsAttached;
    }

    public string QueryStringValue(string key)
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

    public AudioData AudioDataCreate(byte[] data, int dataLength)
    {
        StartAudio();
        return GetSampleFromArray(data);
    }

    public bool AudioDataLoaded(AudioData data)
    {
        return true;
    }

    public AudioTask AudioCreate(AudioData data)
    {
        return audio.CreateAudio(data);
    }

    public void AudioPlay(AudioTask audio_)
    {
        StartAudio();
        audio_.Play();
    }

    public void AudioPause(AudioTask audio_)
    {
        audio_.Pause();
    }

    public void AudioDelete(AudioTask audio_)
    {
        audio_.Stop();
    }

    public bool AudioFinished(AudioTask audio_)
    {
        return audio_.Finished;
    }

    public void AudioSetPosition(AudioTask audio_, float x, float y, float z)
    {
        audio_.position = new Vector3(x, y, z);
    }

    public void AudioUpdateListener(float posX, float posY, float posZ, float orientX, float orientY, float orientZ)
    {
        StartAudio();
        UpdateListener(new Vector3(posX, posY, posZ), new Vector3(orientX, orientY, orientZ));
    }

    #endregion

    #region ENet
    public bool TcpAvailable()
    {
        return true;
    }

    public bool EnetAvailable() => true;

    public EnetHost EnetCreateHost() => new EnetHostWrapper(new Host());

    public void EnetHostInitialize(EnetHost host, IpEndpoint? address, int peerLimit,
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
        ENet.Packet packet = default;
        packet.Create(payload.ToArray(), payload.Length, (PacketFlags)flags);
        ((EnetPeerWrapper)peer).Peer.Send((byte)channelId, ref packet);
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
        public override IpEndpoint GetRemoteAddress() =>
            IpEndpointDefault.Create(Peer.IP);
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
        private readonly ENet.Packet _p;
        internal EnetPacketWrapper(ENet.Packet p) => _p = p;

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

    public void WebSocketConnect(string ip, int port)
    {
    }

    public void WebSocketSend(byte[] data, int dataLength)
    {
    }

    public int WebSocketReceive(byte[] data, int dataLength)
    {
        return -1;
    }

    #endregion

    #region OpenGlImpl

    public GameWindow window;

    public int GetCanvasWidth()
    {
        return window.ClientSize.X;
    }

    public int GetCanvasHeight()
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

    public void SetVSync(bool enabled)
    {
        window.VSync = enabled ? VSyncMode.On : VSyncMode.Off;
    }

    private readonly Screenshot screenshot = new();

    public void SaveScreenshot()
    {
        screenshot.d_GameWindow = window;
        screenshot.SaveScreenshot();
    }

    public Bitmap GrabScreenshot()
    {
        screenshot.d_GameWindow = window;
        Bitmap bmp = screenshot.GrabScreenshot();
        return bmp;
    }

    public void WindowExit()
    {
        gameexit?.exit = true;
        window.Close();
    }

    public void SetTitle(string applicationname)
    {
        window.Title = applicationname;
    }

    public string KeyName(int key)
    {
        if (Enum.IsDefined(typeof(Keys), key))
        {
            return Enum.GetName(typeof(Keys), key)!;
        }
        return key.ToString();
    }

    private List<DisplayResolutionCi> resolutions;

    public List<DisplayResolutionCi> GetDisplayResolutions()
    {
        if (resolutions == null)
        {
            resolutions = [];
            foreach (var screen in Screen.AllScreens)
            {
                var resolution = new DisplayResolutionCi
                {
                    Width = screen.Bounds.Width,
                    Height = screen.Bounds.Height,
                    BitsPerPixel = screen.BitsPerPixel,
                    RefreshRate = 60 // Screen doesn't expose refresh rate
                };

                if (resolution.Width < 800 || resolution.Height < 600 || resolution.BitsPerPixel < 16)
                    continue;

                resolutions.Add(resolution);
            }
        }
        return resolutions;
    }

    public WindowState GetWindowState()
    {
        return window.WindowState;
    }

    public void SetWindowState(WindowState value)
    {
        window.WindowState = value;
    }

    public void ChangeResolution(int width, int height, int bitsPerPixel, float refreshRate)
    {
        window.Size = new Vector2i(width, height);
    }

    public DisplayResolutionCi GetDisplayResolutionDefault()
    {
        var screen = Screen.PrimaryScreen!;
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
    public void GlViewport(int x, int y, int width, int height)
    {
        GL.Viewport(x, y, width, height);
    }

    public void GlClearColorBufferAndDepthBuffer()
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public void GlDisableDepthTest()
    {
        GL.Disable(EnableCap.DepthTest);
    }

    public void BindTexture2d(int texture)
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

    public GeometryModel CreateModel(GeometryModel data)
    {
        int vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        // positions → attribute 0
        int vertexVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vertexVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Xyz.Length * sizeof(float), data.Xyz, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, 0, 0);
        GL.EnableVertexAttribArray(0);

        // colors → attribute 1
        int colorVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, colorVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Rgba.Length * sizeof(byte), data.Rgba, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(1, 4, VertexAttribPointerType.UnsignedByte, true, 0, 0);
        GL.EnableVertexAttribArray(1);

        // UVs → attribute 2
        int uvVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, uvVbo);
        GL.BufferData(BufferTarget.ArrayBuffer, data.Uv.Length * sizeof(float), data.Uv, BufferUsageHint.StaticDraw);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, 0, 0);
        GL.EnableVertexAttribArray(2);

        // indices
        int indexVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, indexVbo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, data.Indices.Length * sizeof(int), data.Indices, BufferUsageHint.StaticDraw);

        GL.BindVertexArray(0);

        data.VaoId = vao;
        data.VertexVboId = vertexVbo;
        data.ColorVboId = colorVbo;
        data.UvVboId = uvVbo;
        data.IndexVboId = indexVbo;
        return data;
    }

    public void UpdateModel(GeometryModel data)
    {
        if (data.VaoId == 0)
        {
            CreateModel(data);
            return;
        }

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.VertexVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.Xyz.Length * sizeof(float), data.Xyz);

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.ColorVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.Rgba.Length * sizeof(byte), data.Rgba);

        GL.BindBuffer(BufferTarget.ArrayBuffer, data.UvVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.Uv.Length * sizeof(float), data.Uv);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, data.IndexVboId);
        GL.BufferSubData(BufferTarget.ElementArrayBuffer, IntPtr.Zero,
            data.Indices.Length * sizeof(int), data.Indices);

        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public void UpdateModelColors(GeometryModel data)
    {
        if (data.VaoId == 0)
        {
            // first time - full upload
            CreateModel(data);
            return;
        }

        // re-upload only the color buffer
        GL.BindBuffer(BufferTarget.ArrayBuffer, data.ColorVboId);
        GL.BufferSubData(BufferTarget.ArrayBuffer, IntPtr.Zero,
            data.Rgba.Length * sizeof(byte), data.Rgba);
        GL.BindBuffer(BufferTarget.ArrayBuffer, 0);
    }

    public void DrawModelData(GeometryModel data)
    {
        GL.UseProgram(_shaderProgram);
        GL.UniformMatrix4(_uProjection, false, ref _projectionMatrix);
        GL.UniformMatrix4(_uModelView, false, ref _modelViewMatrix);
        GL.BindVertexArray(data.VaoId);
        PrimitiveType primitiveType = data.Mode == (int)DrawMode.Triangles
            ? PrimitiveType.Triangles
            : PrimitiveType.Lines;
        GL.DrawElements(primitiveType, data.IndicesCount, DrawElementsType.UnsignedInt, 0);
        GL.BindVertexArray(0);
    }

    public void DrawModel(GeometryModel model)
    {
        DrawModelData(model);
    }

    public void DrawModels(List<GeometryModel> models, int count)
    {
        for (int i = 0; i < count; i++)
        {
            DrawModelData(models[i]);
        }
    }

    public void InitShaders()
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

        // set initial values using cached locations
        GL.Uniform3(_uAmbientLight, 1f, 1f, 1f);
        GL.Uniform1(_uFogEnabled, 0);
        GL.Uniform1(_uFogDensity, _fogDensity);
        GL.Uniform4(_uFogColor, _fogColor);

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

    public void GlClearColorRgbaf(float r, float g, float b, float a)
    {
        GL.ClearColor(r, g, b, a);
    }

    public void GlEnableDepthTest()
    {
        GL.Enable(EnableCap.DepthTest);
    }

    public bool ALLOW_NON_POWER_OF_TWO = false;
    public bool ENABLE_MIPMAPS = true;
    public bool ENABLE_TRANSPARENCY = true;

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

    public void GlDisableCullFace()
    {
        GL.Disable(EnableCap.CullFace);
    }

    public void GlEnableCullFace()
    {
        GL.Enable(EnableCap.CullFace);
    }

    public void DeleteModel(GeometryModel model)
    {
        GeometryModel m = model;
        GL.DeleteVertexArray(m.VaoId);
        GL.DeleteBuffer(m.VertexVboId);
        GL.DeleteBuffer(m.ColorVboId);
        GL.DeleteBuffer(m.UvVboId);
        GL.DeleteBuffer(m.IndexVboId);
    }

    public void GLLineWidth(int width)
    {
        GL.LineWidth(width);
    }

    public void GLDeleteTexture(int id)
    {
        GL.DeleteTexture(id);
    }

    public void GlClearDepthBuffer()
    {
        GL.Clear(ClearBufferMask.DepthBufferBit);
    }

    private Vector3 _ambientLight = Vector3.One;
    private Vector4 _fogColor = Vector4.One;
    private float _fogDensity = 0.003f;

    public void GlLightModelAmbient(int r, int g, int b)
    {
        _ambientLight = new Vector3(r / 255f, g / 255f, b / 255f);
        if (_shaderProgram != -1)
            GL.Uniform3(GL.GetUniformLocation(_shaderProgram, "uAmbientLight"),
                _ambientLight.X, _ambientLight.Y, _ambientLight.Z);
    }

    public void GlEnableFog()
    {
        if (_shaderProgram != -1)
            GL.Uniform1(_uFogEnabled, 1);
    }

    public void GlDisableFog()
    {
        if (_shaderProgram != -1)
            GL.Uniform1(_uFogEnabled, 0);
    }

    public void GlFogFogColor(int r, int g, int b, int a)
    {
        _fogColor = new Vector4(r / 255f, g / 255f, b / 255f, a / 255f);
        if (_shaderProgram != -1)
            GL.Uniform4(_uFogColor, _fogColor);
    }

    public void GlFogFogDensity(float density)
    {
        _fogDensity = density;
        if (_shaderProgram != -1)
            GL.Uniform1(_uFogDensity, _fogDensity);
    }

    public int GlGetMaxTextureSize()
    {
        GL.GetInteger(GetPName.MaxTextureSize, out int size);
        return size;
    }

    public void GlDepthMask(bool flag)
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
    public bool SinglePlayerServerAvailable()
    {
        return singlePlayerServerAvailable;
    }

    public void SinglePlayerServerStart(string saveFilename)
    {
        singlepLayerServerExit = false;
        StartSinglePlayerServer(saveFilename);
    }

    public bool singlepLayerServerExit;
    public void SinglePlayerServerExit()
    {
        singlepLayerServerExit = true;
    }

    public System.Action<string> StartSinglePlayerServer;
    public bool singlePlayerServerLoaded;

    public bool SinglePlayerServerLoaded()
    {
        return singlePlayerServerLoaded;
    }
    public DummyNetwork singlePlayerServerDummyNetwork;
    public DummyNetwork SinglePlayerServerGetNetwork()
    {
        return singlePlayerServerDummyNetwork;
    }

    public void SinglePlayerServerDisable()
    {
        singlePlayerServerAvailable = false;
    }

    public EnetNetConnection CastToEnetNetConnection(NetConnection connection)
    {
        return (EnetNetConnection)connection;
    }

    public PlayerInterpolationState CastToPlayerInterpolationState(IInterpolatedObject a)
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

    public List<Action<KeyEventArgs>> keyDownHandlers { get; set; } = new();
    public List<Action<KeyEventArgs>> keyUpHandlers = new();
    public List<Action<KeyPressEventArgs>> keyPressHandlers = new();

    public event Action<TouchEventArgs> OnTouchStart;
    public event Action<TouchEventArgs> OnTouchMove;
    public event Action<TouchEventArgs> OnTouchEnd;

    public event Action<MouseEventArgs> OnMouseDown;
    public event Action<MouseEventArgs> OnMouseUp;
    public event Action<MouseEventArgs> OnMouseMove;
    public event Action<MouseWheelEventArgs> OnMouseWheel;

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
    public void AddOnKeyEvent(KeyEventHandler handler)
    {
        keyEventHandlers.Add(handler);
    }

    public void AddOnMouseEvent(
        Action<MouseEventArgs> onMouseDown,
        Action<MouseEventArgs> onMouseUp,
        Action<MouseEventArgs> onMouseMove,
        Action<MouseWheelEventArgs> onMouseWheel)
    {
        OnMouseDown += onMouseDown;
        OnMouseUp += onMouseUp;
        OnMouseMove += onMouseMove;
        OnMouseWheel += onMouseWheel;
    }

    public void AddOnTouchEvent(Action<TouchEventArgs> onTouchStart,
        Action<TouchEventArgs> onTouchMove,
        Action<TouchEventArgs> onTouchEnd)
    {
        OnTouchStart += onTouchStart;
        OnTouchMove += onTouchMove;
        OnTouchEnd += onTouchEnd;
    }

    public CrashReporter crashreporter;
    public void AddOnCrash(OnCrashHandler handler)
    {
        crashreporter.OnCrash += handler.OnCrash;
    }

    #endregion

    #region Input

    private bool mousePointerLocked;
    private bool mouseCursorVisible = true;

    public bool IsMousePointerLocked()
    {
        return mousePointerLocked;
    }

    public bool MouseCursorIsVisible()
    {
        return mouseCursorVisible;
    }

    public void SetWindowCursor(int hotx, int hoty, int sizex, int sizey, byte[] imgdata, int imgdataLength)
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

    public void RestoreWindowCursor()
    {
        window.Cursor = MouseCursor.Default;
    }

    public static int ToGlKey(Keys key)
    {
        return (int)key;
    }

    public void MouseCursorSetVisible(bool value)
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

    public void RequestMousePointerLock()
    {
        MouseCursorSetVisible(false);
        mousePointerLocked = true;
    }

    public void ExitMousePointerLock()
    {
        MouseCursorSetVisible(true);
        mousePointerLocked = false;
    }

    public bool Focused()
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
            return;

        var mouse = window.MouseState;
        float xdelta = mouse.Delta.X;
        float ydelta = mouse.Delta.Y;

        if (xdelta != 0 || ydelta != 0)
        {
            MouseEventArgs args = new();
            args.SetX((int)mouse.Position.X);
            args.SetY((int)mouse.Position.Y);
            args.SetMovementX((int)xdelta);
            args.SetMovementY((int)ydelta);
            args.SetEmulated(true);
            OnMouseMove?.Invoke(args);
        }
    }

    private void Mouse_WheelChanged(MouseWheelEventArgs e)
    {
        OnMouseWheel?.Invoke(e);
    }

    private void Mouse_ButtonDown(MouseButtonEventArgs e)
    {
        var pos = window.MousePosition;
        if (TouchTest)
        {
            TouchEventArgs args = new();
            args.SetX((int)pos.X);
            args.SetY((int)pos.Y);
            args.SetId(0);
            OnTouchStart?.Invoke(args);
        }
        else
        {
            MouseEventArgs args = new();
            args.SetX((int)pos.X);
            args.SetY((int)pos.Y);
            args.SetButton((int)e.Button);
            OnMouseDown?.Invoke(args);
        }
    }

    private void Mouse_ButtonUp(MouseButtonEventArgs e)
    {
        var pos = window.MousePosition;
        if (TouchTest)
        {
            TouchEventArgs args = new();
            args.SetX((int)pos.X);
            args.SetY((int)pos.Y);
            args.SetId(0);
            OnTouchEnd?.Invoke(args);
        }
        else
        {
            MouseEventArgs args = new();
            args.SetX((int)pos.X);
            args.SetY((int)pos.Y);
            args.SetButton((int)e.Button);
            OnMouseUp?.Invoke(args);
        }
    }

    private void Mouse_Move(MouseMoveEventArgs e)
    {
        if (TouchTest)
        {
            Console.WriteLine("TouchTest path");
            TouchEventArgs args = new();
            args.SetX((int)e.X);
            args.SetY((int)e.Y);
            args.SetId(0);
            OnTouchMove?.Invoke(args);
        }
        else
        {
            Console.WriteLine("Mouse path");
            MouseEventArgs args = new();
            args.SetX((int)e.X);
            args.SetY((int)e.Y);
            args.SetMovementX((int)e.DeltaX);
            args.SetMovementY((int)e.DeltaY);
            args.SetEmulated(false);
            OnMouseMove?.Invoke(args);
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

public class AviWriterCiCs : IAviWriter
{
    public AviWriterCiCs()
    {
        avi = new AviWriter();
    }

    public AviWriter avi;
    public Bitmap openbmp;

    public void Open(string filename, int framerate, int width, int height)
    {
        openbmp = avi.Open(filename, (uint)framerate, width, height);
    }

    public void AddFrame(Bitmap bitmap)
    {
        var bmp_ = bitmap;

        using (Graphics g = Graphics.FromImage(openbmp))
        {
            g.DrawImage(bmp_, 0, 0);
        }
        openbmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

        avi.AddFrame();
    }

    public void Close()
    {
        avi.Close();
    }
}

public class GameWindowNative : GameWindow
{
    public GameWindowNative()
        : base(
            new GameWindowSettings
            {

                UpdateFrequency = 45.0, // Cap at 60 updates/sec
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