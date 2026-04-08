public class InterpolationCi
{
    public static int InterpolateColor(GamePlatform platform, float progress, int[] colors, int colorsLength)
    {
        float one = 1;
        int colora = (int)((colorsLength - 1) * progress);
        if (colora < 0) { colora = 0; }
        if (colora >= colorsLength) { colora = colorsLength - 1; }
        int colorb = colora + 1;
        if (colorb >= colorsLength) { colorb = colorsLength - 1; }
        int a = colors[colora];
        int b = colors[colorb];
        float p = (progress - (one * colora) / (colorsLength - 1)) * (colorsLength - 1);
        int A = (int)(Game.ColorA(a) + (Game.ColorA(b) - Game.ColorA(a)) * p);
        int R = (int)(Game.ColorR(a) + (Game.ColorR(b) - Game.ColorR(a)) * p);
        int G = (int)(Game.ColorG(a) + (Game.ColorG(b) - Game.ColorG(a)) * p);
        int B = (int)(Game.ColorB(a) + (Game.ColorB(b) - Game.ColorB(a)) * p);
        return Game.ColorFromArgb(A, R, G, B);
    }
}

public class MiscCi
{
    public static bool ReadBool(string str)
    {
        if (str == null)
        {
            return false;
        }
        else
        {
            return (str != "0"
                && (str != "false")
                && (str != "False")
                && (str != "FALSE"));
        }
    }
    public static byte[] UshortArrayToByteArray(int[] input, int inputLength)
    {
        int outputLength = inputLength * 2;
        byte[] output = new byte[outputLength];
        for (int i = 0; i < inputLength; i++)
        {
            output[i * 2] = (byte)(input[i] & 255);
            output[i * 2 + 1] = (byte)((input[i] >> 8) & 255);
        }
        return output;
    }
}

public class ConnectData
{
    internal string Username;
    internal string Ip;
    internal int Port;
    internal string Auth;
    internal string ServerPassword;
    internal bool IsServePasswordProtected;
    public static ConnectData FromUri(Uri uri)
    {
        ConnectData c = new()
        {
            Ip = uri.Host,
            Port = uri.Port != -1 ? uri.Port : 25565,
            Username = "gamer"
        };

        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        if (query["user"] is string user) { c.Username = user; }
        if (query["auth"] is string auth) { c.Auth = auth; }
        if (query["serverPassword"] is string serverPassword) { c.IsServePasswordProtected = MiscCi.ReadBool(serverPassword); }

        return c;
    }

    public void SetIp(string value)
    {
        Ip = value;
    }

    public void SetPort(int value)
    {
        Port = value;
    }

    public void SetUsername(string value)
    {
        Username = value;
    }

    public void SetIsServePasswordProtected(bool value)
    {
        IsServePasswordProtected = value;
    }

    public bool GetIsServePasswordProtected()
    {
        return IsServePasswordProtected;
    }

    public void SetServerPassword(string value)
    {
        ServerPassword = value;
    }
}

public class Ping_
{
    public Ping_()
    {
        RoundtripTimeMilliseconds = 0;
        ready = true;
        timeSendMilliseconds = 0;
        timeout = 10;
    }

    private int RoundtripTimeMilliseconds;

    private bool ready;
    private int timeSendMilliseconds;
    private int timeout; //in seconds

    public int GetTimeoutValue()
    {
        return timeout;
    }
    public void SetTimeoutValue(int value)
    {
        timeout = value;
    }

    public bool Send(GamePlatform platform)
    {
        if (!ready)
        {
            return false;
        }
        ready = false;
        this.timeSendMilliseconds = platform.TimeMillisecondsFromStart();
        return true;
    }

    public bool Receive(GamePlatform platform)
    {
        if (ready)
        {
            return false;
        }
        this.RoundtripTimeMilliseconds = platform.TimeMillisecondsFromStart() - timeSendMilliseconds;
        ready = true;
        return true;
    }

    public bool Timeout(GamePlatform platform)
    {
        if ((platform.TimeMillisecondsFromStart() - timeSendMilliseconds) / 1000 > this.timeout)
        {
            this.ready = true;
            return true;
        }
        return false;
    }

    internal int RoundtripTimeTotalMilliseconds()
    {
        return RoundtripTimeMilliseconds;
    }
}

public class ConnectedPlayer
{
    internal int id;
    internal string name;
    internal int ping; // in ms
}

public class ServerInformation
{
    public ServerInformation()
    {
        ServerName = "";
        ServerMotd = "";
        connectdata = new ConnectData();
        ServerPing = new Ping_();
    }

    internal string ServerName;
    internal string ServerMotd;
    internal ConnectData connectdata;
    internal Ping_ ServerPing;
}

public class BitmapData_
{
    public static BitmapData_ Create(int width, int height)
    {
        BitmapData_ b = new()
        {
            width = width,
            height = height,
            argb = new int[width * height]
        };
        return b;
    }
    public static BitmapData_ CreateFromBitmap(GamePlatform p, Bitmap atlas2d_)
    {
        BitmapData_ b = new()
        {
            width = (int)(p.BitmapGetWidth(atlas2d_)),
            height = (int)(p.BitmapGetHeight(atlas2d_))
        };
        b.argb = new int[b.width * b.height];
        p.BitmapGetPixelsArgb(atlas2d_, b.argb);
        return b;
    }

    internal int[] argb;
    internal int width;
    internal int height;

    public void SetPixel(int x, int y, int color)
    {
        argb[x + y * width] = color;
    }
    public int GetPixel(int x, int y)
    {
        return argb[x + y * width];
    }

    public Bitmap ToBitmap(GamePlatform p)
    {
        Bitmap bmp = new(width, height);
        p.BitmapSetPixelsArgb(bmp, argb);
        return bmp;
    }
}

public class TextureAtlasConverter
{
    //tiles = 16 means 16 x 16 atlas
    public static Bitmap[] Atlas2dInto1d(GamePlatform p, Bitmap atlas2d_, int tiles, int atlassizezlimit, out int retCount)
    {
        BitmapData_ orig = BitmapData_.CreateFromBitmap(p, atlas2d_);

        int tilesize = orig.width / tiles;

        int atlasescount = Math.Max(1, (tiles * tiles * tilesize) / atlassizezlimit);
        Bitmap[] atlases = new Bitmap[128];
        int atlasesCount = 0;

        BitmapData_ atlas1d = null;

        for (int i = 0; i < tiles * tiles; i++)
        {
            int x = i % tiles;
            int y = i / tiles;
            int tilesinatlas = (tiles * tiles / atlasescount);
            if (i % tilesinatlas == 0)
            {
                if (atlas1d != null)
                {
                    atlases[atlasesCount++] = atlas1d.ToBitmap(p);
                }
                atlas1d = BitmapData_.Create(tilesize, atlassizezlimit);
            }
            for (int xx = 0; xx < tilesize; xx++)
            {
                for (int yy = 0; yy < tilesize; yy++)
                {
                    int c = orig.GetPixel(x * tilesize + xx, y * tilesize + yy);
                    atlas1d.SetPixel(xx, (i % tilesinatlas) * tilesize + yy, c);
                }
            }
        }
        atlases[atlasesCount++] = atlas1d.ToBitmap(p);
        retCount = atlasescount;
        return atlases;
    }
}

public class VecCito3i
{
    public int x;
    public int y;
    public int z;

    public static VecCito3i CitoCtr(int _x, int _y, int _z)
    {
        VecCito3i v = new()
        {
            x = _x,
            y = _y,
            z = _z
        };

        return v;
    }

    public void Add(int _x, int _y, int _z, VecCito3i result)
    {
        result.x = x + _x;
        result.y = y + _y;
        result.z = z + _z;
    }
}

public class GameVersionHelper
{
    public static bool ServerVersionAtLeast(GamePlatform platform, string serverGameVersion, int year, int month, int day)
    {
        if (serverGameVersion == null)
        {
            return true;
        }
        if (VersionToInt(platform, serverGameVersion) < DateToInt(year, month, day))
        {
            return false;
        }
        return true;
    }

    private static bool IsVersionDate(GamePlatform platform, string version)
    {
        if (version.Length >= 10)
        {
            if (version[4] == 45 && version[7] == 45) // '-'
            {
                return true;
            }
        }
        return false;
    }

    private static int VersionToInt(GamePlatform platform, string version)
    {
        int max = 1000 * 1000 * 1000;
        if (!IsVersionDate(platform, version))
        {
            return max;
        }
        if (DateTime.TryParseExact(version[..10], "yyyy.MM.dd", null, System.Globalization.DateTimeStyles.None, out DateTime date))
        {
            return date.Year * 10000 + date.Month * 100 + date.Day;
        }
        return max;
    }

    private static int DateToInt(int year, int month, int day)
    {
        return year * 10000 + month * 100 + day;
    }
}