using ENet;
using ManicDigger;
using ManicDigger.ClientNative;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Common.Input;
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;
using Monitor = System.Threading.Monitor;

public class GamePlatformNative : GamePlatform
{
    #region Primitive
    public override int FloatToInt(float value)
    {
        return (int)value;
    }

    public override float MathSin(float a)
    {
        return (float)Math.Sin(a);
    }

    public override float MathCos(float a)
    {
        return (float)Math.Cos(a);
    }

    public override float MathSqrt(float value)
    {
        return (float)Math.Sqrt(value);
    }

    public override float MathAcos(float p)
    {
        return (float)Math.Acos(p);
    }

    public override float MathTan(float p)
    {
        return (float)Math.Tan(p);
    }

    public override float FloatModulo(float a, int b)
    {
        return a % b;
    }

    public override int IntParse(string value)
    {
        return int.Parse(value);
    }

    public override float FloatParse(string value)
    {
        return float.Parse(value);
    }

    public override bool FloatTryParse(string s, FloatRef ret)
    {
        if (float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out float f))
        {
            ret.value = f;
            return true;
        }
        else
        {
            return false;
        }
    }

    public override string IntToString(int value)
    {
        return value.ToString();
    }

    public override string FloatToString(float value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }

    public override string StringToLower(string p)
    {
        return p.ToLowerInvariant();
    }

    public override int[] StringToCharArray(string s, IntRef length)
    {
        if (s == null)
        {
            length.value = 0;
            return [];
        }
        length.value = s.Length;
        int[] charArray = new int[s.Length];
        for (int i = 0; i < s.Length; i++)
        {
            charArray[i] = s[i];
        }
        return charArray;
    }

    public override string CharArrayToString(int[] charArray, int length)
    {
        StringBuilder s = new();
        for (int i = 0; i < length; i++)
        {
            s.Append((char)charArray[i]);
        }
        return s.ToString();
    }

    public override string[] StringSplit(string value, string separator, IntRef returnLength)
    {
        string[] ret = value.Split([separator[0]]);
        returnLength.value = ret.Length;
        return ret;
    }

    public override string StringJoin(string[] value, string separator)
    {
        return string.Join(separator, value);
    }

    public override bool StringEmpty(string data)
    {
        return string.IsNullOrEmpty(data);
    }

    public override string StringTrim(string value)
    {
        return value.Trim();
    }

    public override string StringFormat(string format, string arg0)
    {
        return string.Format(format, arg0);
    }

    public override string StringFormat2(string format, string arg0, string arg1)
    {
        return string.Format(format, arg0, arg1);
    }

    public override string StringFormat3(string format, string arg0, string arg1, string arg2)
    {
        return string.Format(format, arg0, arg1, arg2);
    }

    public override string StringFormat4(string format, string arg0, string arg1, string arg2, string arg3)
    {
        return string.Format(format, arg0, arg1, arg2, arg3);
    }

    public override byte[] StringToUtf8ByteArray(string s, IntRef retLength)
    {
        byte[] data = Encoding.UTF8.GetBytes(s);
        retLength.value = data.Length;
        return data;
    }

    public override string StringFromUtf8ByteArray(byte[] value, int valueLength)
    {
        string s = Encoding.UTF8.GetString(value, 0, valueLength);
        return s;
    }

    public override bool StringContains(string a, string b)
    {
        return a.Contains(b);
    }

    public override string StringReplace(string s, string from, string to)
    {
        return s.Replace(from, to);
    }

    public override bool StringStartsWithIgnoreCase(string a, string b)
    {
        return a.StartsWith(b, StringComparison.InvariantCultureIgnoreCase);
    }

    public override int StringIndexOf(string s, string p)
    {
        return s.IndexOf(p);
    }

    #endregion

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

    public override string Timestamp()
    {
        string time = string.Format("{0:yyyy-MM-dd_HH-mm-ss}", DateTime.Now);
        return time;
    }

    public override void ClipboardSetText(string s)
    {
        Clipboard.SetText(s);
    }

    private readonly ManicDigger.Renderers.TextRenderer r = new();
    private readonly Dictionary<TextAndSize, SizeF> textsizes = new();
    public SizeF TextSize(string text, float fontsize)
    {
        SizeF size;
        if (textsizes.TryGetValue(new TextAndSize() { text = text, size = fontsize }, out size))
        {
            return size;
        }
        size = textrenderer.MeasureTextSize(text, fontsize);
        textsizes[new TextAndSize() { text = text, size = fontsize }] = size;
        return size;
    }

    public override void TextSize(string text, float fontSize, IntRef outWidth, IntRef outHeight)
    {
        SizeF size = TextSize(text, fontSize);
        outWidth.value = (int)size.Width;
        outHeight.value = (int)size.Height;
    }

    public override void Exit()
    {
        Environment.Exit(0);
    }

    public override bool ExitAvailable()
    {
        return true;
    }

    public override string PathSavegames()
    {
        return Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
    }

    public override string PathCombine(string part1, string part2)
    {
        return Path.Combine(part1, part2);
    }

    public override string[] DirectoryGetFiles(string path, IntRef length)
    {
        if (!Directory.Exists(path))
        {
            length.value = 0;
            return [];
        }
        string[] files = Directory.GetFiles(path);
        length.value = files.Length;
        return files;
    }

    public override string[] FileReadAllLines(string path, IntRef length)
    {
        string[] lines = File.ReadAllLines(path);
        length.value = lines.Length;
        return lines;
    }

    public override void WebClientDownloadDataAsync(string url, HttpResponseCi response)
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

    public override void ThumbnailDownloadAsync(string ip, int port, ThumbnailResponseCi response)
    {
        ThumbnailDownloadArgs args = new()
        {
            ip = ip,
            port = port,
            response = response
        };
        ThreadPool.QueueUserWorkItem(DownloadServerThumbnail, args);
    }

    private void DownloadServerThumbnail(object o)
    {
        ThumbnailDownloadArgs args = (ThumbnailDownloadArgs)o;
        //Fetch server info from given adress
        QueryClient qClient = new();
        qClient.SetPlatform(this);
        qClient.PerformQuery(args.ip, args.port);
        if (qClient.querySuccess)
        {
            //Received a result
            QueryResult r = qClient.GetResult();
            args.response.data = r.ServerThumbnail;
            args.response.dataLength = r.ServerThumbnail.Length;
            args.response.serverMessage = qClient.GetServerMessage();
            args.response.done = true;
        }
        else
        {
            //Did not receive a response
            args.response.error = true;
        }
    }

    private class ThumbnailDownloadArgs
    {
        public string ip;
        public int port;
        public ThumbnailResponseCi response;
    }

    public override string FileName(string fullpath)
    {
        FileInfo info = new(fullpath);
        return info.Name.Replace(info.Extension, "");
    }

    public override string GetLanguageIso6391()
    {
        return CultureInfo.CurrentCulture.TwoLetterISOLanguageName;
    }

    private readonly Stopwatch start = new();

    public override int TimeMillisecondsFromStart()
    {
        return (int)start.ElapsedMilliseconds;
    }

    public override void ThrowException(string message)
    {
        throw new Exception(message);
    }

    public override BitmapCi BitmapCreate(int width, int height)
    {
        BitmapCiCs bmp = new()
        {
            bmp = new Bitmap(width, height)
        };
        return bmp;
    }

    public override void BitmapSetPixelsArgb(BitmapCi bmp, int[] pixels)
    {
        BitmapCiCs bmp_ = (BitmapCiCs)bmp;
        int width = bmp_.bmp.Width;
        int height = bmp_.bmp.Height;
        if (IsMono)
        {
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int color = pixels[x + y * width];
                    bmp_.bmp.SetPixel(x, y, Color.FromArgb(color));
                }
            }
        }
        else
        {
            FastBitmap fastbmp = new()
            {
                bmp = bmp_.bmp
            };
            fastbmp.Lock();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    fastbmp.SetPixel(x, y, pixels[x + y * width]);
                }
            }
            fastbmp.Unlock();
        }
    }

    public override BitmapCi BitmapCreateFromPng(byte[] data, int dataLength)
    {
        BitmapCiCs bmp = new();
        try
        {
            bmp.bmp = new Bitmap(new MemoryStream(data, 0, dataLength));
        }
        catch
        {
            bmp.bmp = new Bitmap(1, 1);
            bmp.bmp.SetPixel(0, 0, Color.Orange);
        }
        return bmp;
    }

    public bool IsMono = Type.GetType("Mono.Runtime") != null;

    public override void BitmapGetPixelsArgb(BitmapCi bitmap, int[] bmpPixels)
    {
        BitmapCiCs bmp = (BitmapCiCs)bitmap;
        int width = bmp.bmp.Width;
        int height = bmp.bmp.Height;
        if (IsMono)
        {
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bmpPixels[x + y * width] = bmp.bmp.GetPixel(x, y).ToArgb();
                }
            }
        }
        else
        {
            FastBitmap fastbmp = new()
            {
                bmp = bmp.bmp
            };
            fastbmp.Lock();
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    bmpPixels[x + y * width] = fastbmp.GetPixel(x, y);
                }
            }
            fastbmp.Unlock();
        }
    }

    public override int LoadTextureFromBitmap(BitmapCi bmp)
    {
        BitmapCiCs bmp_ = (BitmapCiCs)bmp;
        return LoadTexture(bmp_.bmp, false);
    }

    private readonly ManicDigger.Renderers.TextRenderer textrenderer = new();

    public override BitmapCi CreateTextTexture(Text_ t)
    {
        Bitmap bmp = textrenderer.MakeTextTexture(t);
        return new BitmapCiCs() { bmp = bmp };
    }

    public override void SetTextRendererFont(int fontID)
    {
        textrenderer.SetFont(fontID);
    }

    public override float BitmapGetWidth(BitmapCi bmp)
    {
        BitmapCiCs bmp_ = (BitmapCiCs)bmp;
        return bmp_.bmp.Width;
    }

    public override float BitmapGetHeight(BitmapCi bmp)
    {
        BitmapCiCs bmp_ = (BitmapCiCs)bmp;
        return bmp_.bmp.Height;
    }

    public override void BitmapDelete(BitmapCi bmp)
    {
        BitmapCiCs bmp_ = (BitmapCiCs)bmp;
        bmp_.bmp.Dispose();
    }

    public override void ConsoleWriteLine(string s)
    {
        Console.WriteLine(s);
    }

    public override MonitorObject MonitorCreate()
    {
        return new MonitorObject();
    }

    public override void MonitorEnter(MonitorObject monitorObject)
    {
        Monitor.Enter(monitorObject);
    }

    public override void MonitorExit(MonitorObject monitorObject)
    {
        Monitor.Exit(monitorObject);
    }

    public override AviWriterCi AviWriterCreate()
    {
        AviWriterCiCs avi = new();
        return avi;
    }

    public override UriCi ParseUri(string uri)
    {
        MyUri myuri = new(uri);

        UriCi ret = new()
        {
            url = myuri.Url,
            ip = myuri.Ip,
            port = myuri.Port,
            get = []
        };
        foreach (var k in myuri.Get)
        {
            ret.get[k.Key] = k.Value;
        }
        return ret;
    }

    public override RandomCi RandomCreate()
    {
        return new RandomNative();
    }

    public override string PathStorage()
    {
        return GameStorePath.GetStorePath();
    }

    public override string GetGameVersion()
    {
        return GameVersion.Version;
    }

    private readonly ICompression compression = new CompressionGzip();
    public override void GzipDecompress(byte[] compressed, int compressedLength, byte[] ret)
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
    public override byte[] GzipCompress(byte[] data, int dataLength, IntRef retLength)
    {
        byte[] data_ = new byte[dataLength];
        for (int i = 0; i < dataLength; i++)
        {
            data_[i] = data[i];
        }
        byte[] compressed = compression.Compress(data_);
        retLength.value = compressed.Length;
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
    public override bool ChatLog(string servername, string p)
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

    public override bool IsValidTypingChar(int c_)
    {
        char c = (char)c_;
        return (char.IsLetterOrDigit(c) || char.IsWhiteSpace(c)
                    || char.IsPunctuation(c) || char.IsSeparator(c) || char.IsSymbol(c))
                    && c != '\r' && c != '\t';
    }

    public override void MessageBoxShowError(string text, string caption)
    {
        MessageBox.Show(text, caption, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
    }

    public override int ByteArrayLength(byte[] arr)
    {
        return arr.Length;
    }

    public override string[] ReadAllLines(string p, IntRef retCount)
    {
        List<string> lines = new();
        StringReader reader = new(p);
        string line;
        while ((line = reader.ReadLine()) != null)
        {
            lines.Add(line);
        }
        retCount.value = lines.Count;
        return lines.ToArray();
    }

    public override bool ClipboardContainsText()
    {
        return Clipboard.ContainsText();
    }

    public override string ClipboardGetText()
    {
        return Clipboard.GetText();
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

    public override void WebClientUploadDataAsync(string url, byte[] data, int dataLength, HttpResponseCi response)
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

    public override string FileOpenDialog(string extension, string extensionName, string initialDirectory)
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

    public override void ApplicationDoEvents()
    {
        if (IsMono)
        {
            Application.DoEvents();
            Thread.Sleep(0);
        }
    }

    public override void ThreadSpinWait(int iterations)
    {
        Thread.SpinWait(iterations);
    }

    public override void ShowKeyboard(bool show)
    {
    }

    public override bool IsFastSystem()
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

    public override Preferences GetPreferences()
    {
        if (File.Exists(GetPreferencesFilePath()))
        {
            try
            {
                Preferences p = new()
                {
                    platform = this
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
                platform = this
            };
            return p;
        }
    }

    public override void SetPreferences(Preferences preferences)
    {
        var items = preferences.items;
        List<string> lines = [];
        foreach (var (key, value) in items)
        {
            lines.Add($"{key}={value}");
        }
        try
        {
            File.WriteAllLines(GetPreferencesFilePath(), [.. lines]);
        }
        catch
        {
        }
    }

    public bool IsMac = Environment.OSVersion.Platform == PlatformID.MacOSX;

    public override bool MultithreadingAvailable()
    {
        return true;
    }

    public override void QueueUserWorkItem(Action_ action)
    {
        ThreadPool.QueueUserWorkItem((a) => { action.Run(); });
    }

    private AssetLoader assetloader;
    public override void LoadAssetsAsyc(AssetList list, FloatRef progress)
    {
        assetloader ??= new AssetLoader(datapaths);
        assetloader.LoadAssetsAsync(list, progress);
    }

    public override bool IsSmallScreen()
    {
        return TouchTest;
    }

    public override void OpenLinkInBrowser(string url)
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

    public override void SaveAssetToCache(Asset tosave)
    {
        //Check if cache directory exists
        Checkcachedir();
        BinaryWriter bw = new(File.Create(Path.Combine(Cachepath(), tosave.md5)));
        bw.Write(tosave.name);
        bw.Write(tosave.dataLength);
        bw.Write(tosave.data);
        bw.Close();
    }

    public override Asset LoadAssetFromCache(string md5)
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

    public override bool IsCached(string md5)
    {
        if (!Directory.Exists(Cachepath()))
            return false;
        return File.Exists(Path.Combine(Cachepath(), md5));
    }

    public override bool IsChecksum(string checksum)
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

    public override string DecodeHTMLEntities(string htmlencodedstring)
    {
        return System.Web.HttpUtility.HtmlDecode(htmlencodedstring);
    }

    public override bool IsDebuggerAttached()
    {
        return Debugger.IsAttached;
    }

    public override string QueryStringValue(string key)
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

    public override AudioData AudioDataCreate(byte[] data, int dataLength)
    {
        StartAudio();
        return AudioOpenAl.GetSampleFromArray(data);
    }

    public override bool AudioDataLoaded(AudioData data)
    {
        return true;
    }

    public override AudioCi AudioCreate(AudioData data)
    {
        return audio.CreateAudio((AudioDataCs)data);
    }

    public override void AudioPlay(AudioCi audio_)
    {
        StartAudio();
        ((AudioOpenAl.AudioTask)audio_).Play();
    }

    public override void AudioPause(AudioCi audio_)
    {
        ((AudioOpenAl.AudioTask)audio_).Pause();
    }

    public override void AudioDelete(AudioCi audio_)
    {
        ((AudioOpenAl.AudioTask)audio_).Stop();
    }

    public override bool AudioFinished(AudioCi audio_)
    {
        return ((AudioOpenAl.AudioTask)audio_).Finished;
    }

    public override void AudioSetPosition(AudioCi audio_, float x, float y, float z)
    {
        ((AudioOpenAl.AudioTask)audio_).position = new Vector3(x, y, z);
    }

    public override void AudioUpdateListener(float posX, float posY, float posZ, float orientX, float orientY, float orientZ)
    {
        StartAudio();
        AudioOpenAl.UpdateListener(new Vector3(posX, posY, posZ), new Vector3(orientX, orientY, orientZ));
    }

    #endregion

    #region Tcp
    public override bool TcpAvailable()
    {
        return true;
    }

    public override void TcpConnect(string ip, int port, BoolRef connected)
    {
        this.connected = connected;
        sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            NoDelay = true
        };
        sock.BeginConnect(ip, port, OnConnect, sock);
    }
    private Socket sock;
    private BoolRef connected;
    private Connection c;
    private void OnConnect(IAsyncResult result)
    {
        Socket sock = (Socket)result.AsyncState;
        c = new Connection(sock);
        c.ReceivedData += new EventHandler<MessageEventArgs>(c_ReceivedData);
        if (tosend.Count > 0)
        {
            c.Send(tosend.ToArray());
            tosend.Clear();
        }
        connected.value = true;
    }

    private void c_ReceivedData(object sender, MessageEventArgs e)
    {
        lock (received)
        {
            for (int i = 0; i < e.data.Length; i++)
            {
                received.Enqueue(e.data[i]);
            }
        }
    }
    private readonly Queue<byte> tosend = new();
    public override void TcpSend(byte[] data, int length)
    {
        if (c == null)
        {
            for (int i = 0; i < length; i++)
            {
                tosend.Enqueue(data[i]);
            }
        }
        else
        {
            byte[] data1 = new byte[length];
            for (int i = 0; i < length; i++)
            {
                data1[i] = data[i];
            }
            c.Send(data1);
        }
    }
    private readonly Queue<byte> received = new();
    public override int TcpReceive(byte[] data, int dataLength)
    {
        if (c == null)
        {
            return 0;
        }
        int total = 0;
        lock (received)
        {
            for (int i = 0; i < dataLength; i++)
            {
                if (received.Count == 0)
                {
                    break;
                }
                data[i] = received.Dequeue();
                total++;
            }
        }
        return total;
    }

    public class Connection
    {
        public Socket sock;
        public string address;

        private readonly Encoding encoding = Encoding.UTF8;

        public Connection(Socket s)
        {
            this.sock = s;
            address = s.RemoteEndPoint.ToString();
            this.BeginReceive();
        }
        private readonly Stopwatch st = new();
        private void BeginReceive()
        {
            this.sock.BeginReceive(
                    this.dataRcvBuf, 0,
                    this.dataRcvBuf.Length,
                    SocketFlags.None,
                    new AsyncCallback(this.OnBytesReceived),
                    this);
        }
        private readonly byte[] dataRcvBuf = new byte[1024 * 8];
        protected void OnBytesReceived(IAsyncResult result)
        {
            int nBytesRec;
            try
            {
                nBytesRec = this.sock.EndReceive(result);
            }
            catch
            {
                try
                {
                    this.sock.Close();
                }
                catch
                {
                }
                Disconnected?.Invoke(null, new ConnectionEventArgs() { });
                return;
            }
            if (nBytesRec <= 0)
            {
                try
                {
                    this.sock.Close();
                }
                catch
                {
                }
                Disconnected?.Invoke(null, new ConnectionEventArgs() { });
                return;
            }

            byte[] receivedBytes = new byte[nBytesRec];
            for (int i = 0; i < nBytesRec; i++)
            {
                receivedBytes[i] = dataRcvBuf[i];
            }

            if (nBytesRec > 0)
            {
                ReceivedData.Invoke(this, new MessageEventArgs() { data = receivedBytes });
            }

            st.Reset();
            st.Start();

            this.sock.BeginReceive(
                this.dataRcvBuf, 0,
                this.dataRcvBuf.Length,
                SocketFlags.None,
                new AsyncCallback(this.OnBytesReceived),
                this);
        }
        public void Send(byte[] data)
        {
            try
            {
                sock.BeginSend(data, 0, data.Length, SocketFlags.None, new AsyncCallback(OnSend), null);
            }
            catch (Exception e)
            {
            }
        }
        private void OnSend(IAsyncResult result)
        {
            sock.EndSend(result);
        }
        public event EventHandler<MessageEventArgs> ReceivedData;
        public event EventHandler<ConnectionEventArgs> Disconnected;

        public override string ToString()
        {
            if (address != null)
            {
                return address.ToString();
            }
            return base.ToString();
        }
    }
    #endregion

    #region Enet
    public override bool EnetAvailable()
    {
        return true;
    }

    public override EnetHost EnetCreateHost()
    {
        return new EnetHostNative() { host = new Host() };
    }

    public override void EnetHostInitializeServer(EnetHost host, int port, int peerLimit)
    {
        EnetHostNative host_ = (EnetHostNative)host;
        host_.host.Create(port, peerLimit);
    }

    public override bool EnetHostService(EnetHost host, int timeout, EnetEventRef enetEvent)
    {
        EnetHostNative host_ = (EnetHostNative)host;
        int ret = host_.host.Service(timeout, out Event e);
        EnetEventNative ee = new()
        {
            e = e
        };
        enetEvent.e = ee;
        return ret > 0;
    }

    public override bool EnetHostCheckEvents(EnetHost host, EnetEventRef event_)
    {
        EnetHostNative host_ = (EnetHostNative)host;
        int ret = host_.host.CheckEvents(out Event e);
        EnetEventNative ee = new()
        {
            e = e
        };
        event_.e = ee;
        return ret > 0;
    }

    public override EnetPeer EnetHostConnect(EnetHost host, string hostName, int port, int data, int channelLimit)
    {
        EnetHostNative host_ = (EnetHostNative)host;

        Address address = new()
        {
            Port = (ushort)port
        };
        address.SetHost(hostName);

        Peer peer = host_.host.Connect(address, channelLimit, (uint)data);
        EnetPeerNative peer_ = new()
        {
            peer = peer
        };
        return peer_;
    }

    public override void EnetPeerSend(EnetPeer peer, byte channelID, byte[] data, int dataLength, int flags)
    {
        try
        {
            EnetPeerNative peer_ = (EnetPeerNative)peer;

            Packet packet = default;
            packet.Create(data, dataLength, (PacketFlags)flags);

            peer_.peer.Send(channelID, ref packet);
        }
        catch
        {
        }
    }

    public override void EnetHostInitialize(EnetHost host, IPEndPointCi address, int peerLimit, int channelLimit, int incomingBandwidth, int outgoingBandwidth)
    {
        if (address != null)
        {
            throw new Exception();
        }
        EnetHostNative host_ = (EnetHostNative)host;
        host_.host.Create(peerLimit, channelLimit, (uint)incomingBandwidth, (uint)outgoingBandwidth);
    }
    #endregion

    #region WebSocket

    public override bool WebSocketAvailable()
    {
        return false;
    }

    public override void WebSocketConnect(string ip, int port)
    {
    }

    public override void WebSocketSend(byte[] data, int dataLength)
    {
    }

    public override int WebSocketReceive(byte[] data, int dataLength)
    {
        return -1;
    }

    #endregion

    #region OpenGlImpl

    public GameWindow window;

    public override int GetCanvasWidth()
    {
        return window.ClientSize.X;
    }

    public override int GetCanvasHeight()
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

    }

    private void WindowClosed(CancelEventArgs e)
    {
        gameexit.exit = e.Cancel;
    }

    public override void SetVSync(bool enabled)
    {
        window.VSync = enabled ? VSyncMode.On : VSyncMode.Off;
    }

    private readonly Screenshot screenshot = new();

    public override void SaveScreenshot()
    {
        screenshot.d_GameWindow = window;
        screenshot.SaveScreenshot();
    }

    public override BitmapCi GrabScreenshot()
    {
        screenshot.d_GameWindow = window;
        Bitmap bmp = screenshot.GrabScreenshot();
        BitmapCiCs bmp_ = new()
        {
            bmp = bmp
        };
        return bmp_;
    }

    public override void WindowExit()
    {
        gameexit?.exit = true;
        window.Close();
    }

    public override void SetTitle(string applicationname)
    {
        window.Title = applicationname;
    }

    public override string KeyName(int key)
    {
        if (Enum.IsDefined(typeof(Keys), key))
        {
            return Enum.GetName(typeof(Keys), key)!;
        }
        return key.ToString();
    }

    private DisplayResolutionCi[] resolutions;
    private int resolutionsCount;
    public override DisplayResolutionCi[] GetDisplayResolutions(IntRef retResolutionsCount)
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
        retResolutionsCount.value = resolutionsCount;
        return resolutions;
    }

    public override WindowState GetWindowState()
    {
        return window.WindowState;
    }

    public override void SetWindowState(WindowState value)
    {
        window.WindowState = value;
    }

    public override void ChangeResolution(int width, int height, int bitsPerPixel, float refreshRate)
    {
        window.Size = new Vector2i(width, height);
    }

    public override DisplayResolutionCi GetDisplayResolutionDefault()
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
    public override void GlViewport(int x, int y, int width, int height)
    {
        GL.Viewport(x, y, width, height);
    }

    public override void GlClearColorBufferAndDepthBuffer()
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
    }

    public override void GlDisableDepthTest()
    {
        GL.Disable(EnableCap.DepthTest);
    }

    public override void BindTexture2d(int texture)
    {
        GL.BindTexture(TextureTarget.Texture2D, texture);
    }

    private readonly float[] xyz = new float[65536 * 3];
    private readonly float[] uv = new float[65536 * 2];
    private readonly byte[] rgba = new byte[65536 * 4];
    private readonly ushort[] indices = new ushort[65536];

    public override Model CreateModel(ModelData data)
    {
        int id = GL.GenLists(1);

        GL.NewList(id, ListMode.Compile);

        DrawModelData(data);

        GL.EndList();
        DisplayListModel m = new()
        {
            listId = id
        };
        return m;
    }

    public override void DrawModelData(ModelData data)
    {
        GL.EnableClientState(ArrayCap.VertexArray);
        GL.EnableClientState(ArrayCap.ColorArray);
        GL.EnableClientState(ArrayCap.TextureCoordArray);

        float[] dataXyz = data.getXyz();
        float[] dataUv = data.getUv();
        byte[] dataRgba = data.getRgba();

        for (int i = 0; i < data.GetXyzCount(); i++)
        {
            xyz[i] = dataXyz[i];
        }
        for (int i = 0; i < data.GetUvCount(); i++)
        {
            uv[i] = dataUv[i];
        }
        if (dataRgba == null)
        {
            for (int i = 0; i < data.GetRgbaCount(); i++)
            {
                rgba[i] = 255;
            }
        }
        else
        {
            for (int i = 0; i < data.GetRgbaCount(); i++)
            {
                rgba[i] = dataRgba[i];
            }
        }
        GL.VertexPointer(3, VertexPointerType.Float, 3 * 4, xyz);
        GL.ColorPointer(4, ColorPointerType.UnsignedByte, 4 * 1, rgba);
        GL.TexCoordPointer(2, TexCoordPointerType.Float, 2 * 4, uv);

        BeginMode beginmode = BeginMode.Triangles;
        if (data.getMode() == DrawModeEnum.Triangles)
        {
            beginmode = BeginMode.Triangles;
            GL.Enable(EnableCap.Texture2D);
        }
        else if (data.getMode() == DrawModeEnum.Lines)
        {
            beginmode = BeginMode.Lines;
            GL.Disable(EnableCap.Texture2D);
        }
        else
        {
            throw new Exception();
        }

        int[] dataIndices = data.getIndices();
        for (int i = 0; i < data.GetIndicesCount(); i++)
        {
            indices[i] = (ushort)dataIndices[i];
        }

        GL.DrawElements(beginmode, data.GetIndicesCount(), DrawElementsType.UnsignedShort, indices);

        GL.DisableClientState(ArrayCap.VertexArray);
        GL.DisableClientState(ArrayCap.ColorArray);
        GL.DisableClientState(ArrayCap.TextureCoordArray);
        GL.Disable(EnableCap.Texture2D);
    }

    private class DisplayListModel : Model
    {
        public int listId;
    }

    public override void DrawModel(Model model)
    {
        GL.CallList(((DisplayListModel)model).listId);
    }

    private int[] lists = new int[1024];

    public override void DrawModels(Model[] model, int count)
    {
        if (lists.Length < count)
        {
            lists = new int[count * 2];
        }
        for (int i = 0; i < count; i++)
        {
            lists[i] = ((DisplayListModel)model[i]).listId;
        }
        GL.CallLists(count, ListNameType.Int, lists);
    }

    public override void InitShaders()
    {
    }

    public override void SetMatrixUniformProjection(ref Matrix4 pMatrix)
    {
        GL.MatrixMode(MatrixMode.Projection);
        GL.LoadMatrix(ref pMatrix);
    }

    public override void SetMatrixUniformModelView(ref Matrix4 mvMatrix)
    {
        GL.MatrixMode(MatrixMode.Modelview);
        GL.LoadMatrix(ref mvMatrix);
    }

    public override void GlClearColorRgbaf(float r, float g, float b, float a)
    {
        GL.ClearColor(r, g, b, a);
    }

    public override void GlEnableDepthTest()
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
        if ((!ALLOW_NON_POWER_OF_TWO) &&
            (!(BitTools.IsPowerOfTwo(bmp.Width) && BitTools.IsPowerOfTwo(bmp.Height))))
        {
            Bitmap bmp2 = new(BitTools.NextPowerOfTwo(bmp.Width),
                BitTools.NextPowerOfTwo(bmp.Height));
            using (Graphics g = Graphics.FromImage(bmp2))
            {
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;
                g.DrawImage(bmp, 0, 0, bmp2.Width, bmp2.Height);
            }
            convertedbitmap = true;
            bmp = bmp2;
        }
        GL.Enable(EnableCap.Texture2D);
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
            OpenTK.Graphics.OpenGL.PixelFormat.Bgra, PixelType.UnsignedByte, bmp_data.Scan0);

        bmp.UnlockBits(bmp_data);

        GL.Enable(EnableCap.DepthTest);

        if (ENABLE_TRANSPARENCY)
        {
            GL.Enable(EnableCap.AlphaTest);
            GL.AlphaFunc(AlphaFunction.Greater, 0.5f);
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

    public override void GlDisableCullFace()
    {
        GL.Disable(EnableCap.CullFace);
    }

    public override void GlEnableCullFace()
    {
        GL.Enable(EnableCap.CullFace);
    }

    public override void DeleteModel(Model model)
    {
        DisplayListModel m = (DisplayListModel)model;
        GL.DeleteLists(m.listId, 1);
    }

    public override void GlEnableTexture2d()
    {
        GL.Enable(EnableCap.Texture2D);
    }

    public override void GLLineWidth(int width)
    {
        GL.LineWidth(width);
    }

    public override void GLDisableAlphaTest()
    {
        GL.Disable(EnableCap.AlphaTest);
    }

    public override void GLEnableAlphaTest()
    {
        GL.Enable(EnableCap.AlphaTest);
    }

    public override void GLDeleteTexture(int id)
    {
        GL.DeleteTexture(id);
    }

    public override void GlClearDepthBuffer()
    {
        GL.Clear(ClearBufferMask.DepthBufferBit);
    }

    public override void GlLightModelAmbient(int r, int g, int b)
    {
        float mult = 1f;
        float[] global_ambient = [r / 255f * mult, g / 255f * mult, b / 255f * mult, 1f];
        GL.LightModel(LightModelParameter.LightModelAmbient, global_ambient);
    }

    public override void GlEnableFog()
    {
        GL.Enable(EnableCap.Fog);
    }

    public override void GlHintFogHintNicest()
    {
        GL.Hint(HintTarget.FogHint, HintMode.Nicest);
    }

    public override void GlFogFogModeExp2()
    {
        GL.Fog(FogParameter.FogMode, (int)FogMode.Exp2);
    }

    public override void GlFogFogColor(int r, int g, int b, int a)
    {
        float[] fogColor = [(float)r / 255, (float)g / 255, (float)b / 255, (float)a / 255];
        GL.Fog(FogParameter.FogColor, fogColor);
    }

    public override void GlFogFogDensity(float density)
    {
        GL.Fog(FogParameter.FogDensity, density);
    }

    public override int GlGetMaxTextureSize()
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

    public override void GlDepthMask(bool flag)
    {
        GL.DepthMask(flag);
    }

    public override void GlCullFaceBack()
    {
        GL.CullFace(CullFaceMode.Back);
    }

    public override void GlEnableLighting()
    {
        GL.Enable(EnableCap.Lighting);
    }

    public override void GlEnableColorMaterial()
    {
        GL.Enable(EnableCap.ColorMaterial);
    }

    public override void GlColorMaterialFrontAndBackAmbientAndDiffuse()
    {
        GL.ColorMaterial(MaterialFace.FrontAndBack, ColorMaterialParameter.AmbientAndDiffuse);
    }

    public override void GlShadeModelSmooth()
    {
        GL.ShadeModel(ShadingModel.Smooth);
    }

    public override void GlDisableFog()
    {
        GL.Disable(EnableCap.Fog);
    }

    #endregion

    #region Game

    private bool singlePlayerServerAvailable = true;
    public override bool SinglePlayerServerAvailable()
    {
        return singlePlayerServerAvailable;
    }

    public override void SinglePlayerServerStart(string saveFilename)
    {
        singlepLayerServerExit = false;
        StartSinglePlayerServer(saveFilename);
    }

    public bool singlepLayerServerExit;
    public override void SinglePlayerServerExit()
    {
        singlepLayerServerExit = true;
    }

    public System.Action<string> StartSinglePlayerServer;
    public bool singlePlayerServerLoaded;

    public override bool SinglePlayerServerLoaded()
    {
        return singlePlayerServerLoaded;
    }
    public DummyNetwork singlePlayerServerDummyNetwork;
    public override DummyNetwork SinglePlayerServerGetNetwork()
    {
        return singlePlayerServerDummyNetwork;
    }

    public override void SinglePlayerServerDisable()
    {
        singlePlayerServerAvailable = false;
    }

    public override EnetNetConnection CastToEnetNetConnection(NetConnection connection)
    {
        return (EnetNetConnection)connection;
    }

    public override PlayerInterpolationState CastToPlayerInterpolationState(InterpolatedObject a)
    {
        return (PlayerInterpolationState)a;
    }

    #endregion

    #region Event handlers

    public List<NewFrameHandler> newFrameHandlers = new();
    public override void AddOnNewFrame(NewFrameHandler handler)
    {
        newFrameHandlers.Add(handler);
    }

    public List<KeyEventHandler> keyEventHandlers = new();
    public override void AddOnKeyEvent(KeyEventHandler handler)
    {
        keyEventHandlers.Add(handler);
    }

    public List<MouseEventHandler> mouseEventHandlers = new();
    public override void AddOnMouseEvent(MouseEventHandler handler)
    {
        mouseEventHandlers.Add(handler);
    }

    public List<TouchEventHandler> touchEventHandlers = new();
    public override void AddOnTouchEvent(TouchEventHandler handler)
    {
        touchEventHandlers.Add(handler);
    }

    public CrashReporter crashreporter;
    public override void AddOnCrash(OnCrashHandler handler)
    {
        crashreporter.OnCrash += handler.OnCrash;
    }

    #endregion

    #region Input

    private bool mousePointerLocked;
    private bool mouseCursorVisible = true;
    //private MouseState current, previous;
    private float lastX, lastY;

    public override bool IsMousePointerLocked()
    {
        return mousePointerLocked;
    }

    public override bool MouseCursorIsVisible()
    {
        return mouseCursorVisible;
    }

    public override void SetWindowCursor(int hotx, int hoty, int sizex, int sizey, byte[] imgdata, int imgdataLength)
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

    public override void RestoreWindowCursor()
    {
        window.Cursor = MouseCursor.Default;
    }

    public static int ToGlKey(Keys key)
    {
        return (int)key;
    }

    public override void MouseCursorSetVisible(bool value)
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

    public override void RequestMousePointerLock()
    {
        MouseCursorSetVisible(false);
        mousePointerLocked = true;
    }

    public override void ExitMousePointerLock()
    {
        MouseCursorSetVisible(true);
        mousePointerLocked = false;
    }

    public override bool Focused()
    {
        return window.IsFocused;
    }

    private static void Log(string msg)
    {
        File.AppendAllText("debug.log", $"{DateTime.Now}: {msg}\n");
    }

    private void WindowRenderFrame(FrameEventArgs e)
    {
        UpdateMousePosition();
        foreach (NewFrameHandler h in newFrameHandlers)
        {
            NewFrameEventArgs args = new();
            args.SetDt((float)e.Time);
            h.OnNewFrame(args);
        }
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
        foreach (KeyEventHandler h in keyEventHandlers)
        {
            KeyPressEventArgs args = new();
            args.SetKeyChar((char)e.Unicode);  // e.Unicode is an int in v4
            h.OnKeyPress(args);
        }
    }

    private void GameKeyDown(KeyboardKeyEventArgs e)
    {
        foreach (KeyEventHandler h in keyEventHandlers)
        {
            KeyEventArgs args = new();
            args.SetKeyCode(ToGlKey(e.Key));
            args.SetCtrlPressed(e.Modifiers == KeyModifiers.Control);
            args.SetShiftPressed(e.Modifiers == KeyModifiers.Shift);
            args.SetAltPressed(e.Modifiers == KeyModifiers.Alt);
            h.OnKeyDown(args);
        }
    }

    private void GameKeyUp(KeyboardKeyEventArgs e)
    {
        foreach (KeyEventHandler h in keyEventHandlers)
        {
            KeyEventArgs args = new();
            args.SetKeyCode(ToGlKey(e.Key));
            h.OnKeyUp(args);
        }
    }

    #endregion
}

public class AssetLoader
{
    public AssetLoader(string[] datapaths_)
    {
        this.datapaths = datapaths_;
    }
    private readonly string[] datapaths;
    public void LoadAssetsAsync(AssetList list, FloatRef progress)
    {
        List<Asset> assets = new();
        foreach (string path in datapaths)
        {
            try
            {
                if (!Directory.Exists(path))
                {
                    continue;
                }
                foreach (string s in Directory.GetFiles(path, "*.*", SearchOption.AllDirectories))
                {
                    try
                    {
                        FileInfo f = new(s);
                        if (f.Name.Equals("thumbs.db", StringComparison.InvariantCultureIgnoreCase))
                        {
                            continue;
                        }
                        Asset a = new()
                        {
                            data = File.ReadAllBytes(s)
                        };
                        a.dataLength = a.data.Length;
                        a.name = f.Name.ToLowerInvariant();
                        a.md5 = Md5(a.data);
                        assets.Add(a);
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
        }
        progress.value = 1;
        list.count = assets.Count;
        list.items = new Asset[2048];
        for (int i = 0; i < assets.Count; i++)
        {
            list.items[i] = assets[i];
        }
    }

    private readonly MD5CryptoServiceProvider sha1 = new();
    private string Md5(byte[] data)
    {
        string hash = ToHex(sha1.ComputeHash(data), false);
        return hash;
    }

    public static string ToHex(byte[] bytes, bool upperCase)
    {
        StringBuilder result = new(bytes.Length * 2);

        for (int i = 0; i < bytes.Length; i++)
        {
            result.Append(bytes[i].ToString(upperCase ? "X2" : "x2"));
        }

        return result.ToString();
    }
}

public class RandomNative : RandomCi
{
    public Random rnd = new();
    public override float NextFloat()
    {
        return (float)rnd.NextDouble();
    }

    public override int Next()
    {
        return rnd.Next();
    }

    public override int MaxNext(int range)
    {
        return rnd.Next(range);
    }
}

public class MyUri
{
    public MyUri(string uri)
    {
        //string url = "md://publichash:123/?user=a&auth=123";
        var a = new Uri(uri);
        Ip = a.Host;
        Port = a.Port;
        Get = ParseGet(uri);
    }
    internal string Url { get; private set; }
    internal string Ip { get; private set; }
    internal int Port { get; private set; }
    internal Dictionary<string, string> Get { get; private set; }
    private static Dictionary<string, string> ParseGet(string url)
    {
        try
        {
            Dictionary<string, string> d;
            d = new Dictionary<string, string>();
            if (url.Contains("?"))
            {
                string url2 = url.Substring(url.IndexOf("?") + 1);
                var ss = url2.Split(['&']);
                for (int i = 0; i < ss.Length; i++)
                {
                    var ss2 = ss[i].Split(['=']);
                    d[ss2[0]] = ss2[1];
                }
            }
            return d;
        }
        catch
        {
            //throw new FormatException("Invalid address: " + url);
            return null;
        }
    }
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

    public override void AddFrame(BitmapCi bitmap)
    {
        var bmp_ = (BitmapCiCs)bitmap;

        using (Graphics g = Graphics.FromImage(openbmp))
        {
            g.DrawImage(bmp_.bmp, 0, 0);
        }
        openbmp.RotateFlip(RotateFlipType.RotateNoneFlipY);

        avi.AddFrame();
    }

    public override void Close()
    {
        avi.Close();
    }
}

public class EnetHostNative : EnetHost
{
    public Host host;
}

public class EnetEventNative : EnetEvent
{
    public Event e;
    public override EnetEventType Type()
    {
        return (EnetEventType)e.Type;
    }

    public override EnetPeer Peer()
    {
        EnetPeerNative peer = new()
        {
            peer = e.Peer
        };
        return peer;
    }

    public override EnetPacket Packet()
    {
        EnetPacketNative packet = new()
        {
            packet = e.Packet
        };
        return packet;
    }
}

public class EnetPacketNative : EnetPacket
{
    internal Packet packet;
    public override int GetBytesCount()
    {
        return packet.Length;
    }

    public override byte[] GetBytes()
    {
        // GetBytes() is gone, manually copy from native pointer
        byte[] bytes = new byte[packet.Length];
        System.Runtime.InteropServices.Marshal.Copy(packet.Data, bytes, 0, packet.Length);
        return bytes;
    }

    public override void Dispose()
    {
        packet.Dispose();
    }
}

public class EnetPeerNative : EnetPeer
{
    public Peer peer;
    public override int UserData()
    {
        return peer.Data.ToInt32();
    }

    public override void SetUserData(int value)
    {
        peer.Data = new IntPtr(value);
    }

    public override IPEndPointCi GetRemoteAddress()
    {
        // GetRemoteAddress() -> separate IP and Port properties
        return IPEndPointCiDefault.Create(peer.IP);
    }
}

public class BitmapCiCs : BitmapCi
{
    public Bitmap bmp;
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
                Size = new Vector2i(1280, 720),
                Title = "",
                WindowState = WindowState.Normal,
                Profile = ContextProfile.Compatability,
                APIVersion = new Version(3, 3),
            })
    {
    }
}