using OpenTK.Mathematics;
using System.Text;

public class LoginData
{
    internal string ServerAddress;
    internal int Port;
    internal string AuthCode; //Md5(private server key + player name)
    internal string Token;

    internal bool PasswordCorrect;
    internal bool ServerCorrect;
}

public class LoginClientCi
{
    internal LoginResultRef loginResult;
    public void Login(GamePlatform platform, string user, string password, string publicServerKey, string token, LoginResultRef result, LoginData resultLoginData_)
    {
        loginResult = result;
        resultLoginData = resultLoginData_;
        result.value = LoginResult.Connecting;

        LoginUser = user;
        LoginPassword = password;
        LoginToken = token;
        LoginPublicServerKey = publicServerKey;
        shouldLogin = true;
    }
    private string LoginUser;
    private string LoginPassword;
    private string LoginToken;
    private string LoginPublicServerKey;

    private bool shouldLogin;
    private string loginUrl;
    private HttpResponseCi loginUrlResponse;
    private HttpResponseCi loginResponse;
    private LoginData resultLoginData;
    public void Update(GamePlatform platform)
    {
        if (loginResult == null)
        {
            return;
        }

        if (loginUrlResponse == null && loginUrl == null)
        {
            loginUrlResponse = new HttpResponseCi();
            platform.WebClientDownloadDataAsync("http://manicdigger.sourceforge.net/login.php", loginUrlResponse);
        }
        if (loginUrlResponse != null && loginUrlResponse.done)
        {
            loginUrl = Encoding.UTF8.GetString(loginUrlResponse.value, 0, loginUrlResponse.valueLength);
            loginUrlResponse = null;
        }

        if (loginUrl != null)
        {
            if (shouldLogin)
            {
                shouldLogin = false;
                string requestString = string.Format("username={0}&password={1}&server={2}&token={3}"
                    , LoginUser, LoginPassword, LoginPublicServerKey, LoginToken);
                byte[] byteArray = Encoding.UTF8.GetBytes(requestString);
                loginResponse = new HttpResponseCi();
                platform.WebClientUploadDataAsync(loginUrl, byteArray, byteArray.Length, loginResponse);
            }
            if (loginResponse != null && loginResponse.done)
            {
                string responseString = Encoding.UTF8.GetString(loginResponse.value, 0, loginResponse.valueLength);
                resultLoginData.PasswordCorrect = !(responseString.Contains("Wrong username") || responseString.Contains("Incorrect username"));
                resultLoginData.ServerCorrect = !responseString.Contains("server");
                if (resultLoginData.PasswordCorrect)
                {
                    loginResult.value = LoginResult.Ok;
                }
                else
                {
                    loginResult.value = LoginResult.Failed;
                }
                string[] lines = responseString.Split(Environment.NewLine);
                if (lines.Length >= 3)
                {
                    resultLoginData.AuthCode = lines[0];
                    resultLoginData.ServerAddress = lines[1];
                    resultLoginData.Port = int.Parse(lines[2]);
                    resultLoginData.Token = lines[3];
                }
                loginResponse = null;
            }
        }
    }
}

public class GameExit
{
    internal bool exit;
    internal bool restart;

    public void SetExit(bool p)
    {
        exit = p;
    }

    public bool GetExit()
    {
        return exit;
    }

    public void SetRestart(bool p)
    {
        restart = p;
    }

    public bool GetRestart()
    {
        return restart;
    }
}

public class TileEnterData
{
    internal int BlockPositionX;
    internal int BlockPositionY;
    internal int BlockPositionZ;
    internal TileEnterDirection EnterDirection;
}

public class UpDown
{
    public const int None = 0;
    public const int Up = 1;
    public const int Down = 2;
}

public class RenderHintEnum
{
    public const int Fast = 0;
    public const int Nice = 1;
}

public class Speculative
{
    internal int x;
    internal int y;
    internal int z;
    internal int timeMilliseconds;
    internal int blocktype;
}

public class TimerCi
{
    public TimerCi()
    {
        interval = 1;
        maxDeltaTime = -1;
    }
    internal float interval;
    internal float maxDeltaTime;

    internal float accumulator;
    public void Reset()
    {
        accumulator = 0;
    }
    public int Update(float dt)
    {
        accumulator += dt;
        float constDt = interval;
        if (maxDeltaTime != -1 && accumulator > maxDeltaTime)
        {
            accumulator = maxDeltaTime;
        }
        int updates = 0;
        while (accumulator >= constDt)
        {
            updates++;
            accumulator -= constDt;
        }
        return updates;
    }

    internal static TimerCi Create(int interval_, int maxDeltaTime_)
    {
        TimerCi timer = new()
        {
            interval = interval_,
            maxDeltaTime = maxDeltaTime_
        };
        return timer;
    }
}

public class Sprite
{
    public Sprite()
    {
        size = 40;
    }
    internal float positionX;
    internal float positionY;
    internal float positionZ;
    internal string image;
    internal int size;
    internal int animationcount;
}

public class PlayerDrawInfo
{
    public PlayerDrawInfo()
    {
    }

    internal NetworkInterpolation interpolation;
    internal float lastnetworkposX;
    internal float lastnetworkposY;
    internal float lastnetworkposZ;
    internal float lastcurposX;
    internal float lastcurposY;
    internal float lastcurposZ;
    internal float lastnetworkrotx;
    internal float lastnetworkroty;
    internal float lastnetworkrotz;
    internal Vector3 Velocity = Vector3.Zero;
    internal bool moves;
}

public class PlayerInterpolate : IInterpolation
{
    internal GamePlatform platform;
    public override InterpolatedObject Interpolate(InterpolatedObject a, InterpolatedObject b, float progress)
    {
        PlayerInterpolationState aa = platform.CastToPlayerInterpolationState(a);
        PlayerInterpolationState bb = platform.CastToPlayerInterpolationState(b);
        PlayerInterpolationState cc = new()
        {
            positionX = aa.positionX + (bb.positionX - aa.positionX) * progress,
            positionY = aa.positionY + (bb.positionY - aa.positionY) * progress,
            positionZ = aa.positionZ + (bb.positionZ - aa.positionZ) * progress,
            //cc.heading = Game.IntToByte(AngleInterpolation.InterpolateAngle256(platform, aa.heading, bb.heading, progress));
            //cc.pitch = Game.IntToByte(AngleInterpolation.InterpolateAngle256(platform, aa.pitch, bb.pitch, progress));
            rotx = float.DegreesToRadians(AngleInterpolation.InterpolateAngle360(float.RadiansToDegrees(aa.rotx), float.RadiansToDegrees(bb.rotx), progress)),
            roty = float.DegreesToRadians(AngleInterpolation.InterpolateAngle360(float.RadiansToDegrees(aa.roty), float.RadiansToDegrees(bb.roty), progress)),
            rotz = float.DegreesToRadians(AngleInterpolation.InterpolateAngle360(float.RadiansToDegrees(aa.rotz), float.RadiansToDegrees(bb.rotz), progress))
        };
        return cc;
    }
}

public class PlayerInterpolationState : InterpolatedObject
{
    internal float positionX;
    internal float positionY;
    internal float positionZ;
    internal float rotx;
    internal float roty;
    internal float rotz;
    internal byte heading;
    internal byte pitch;
}

public class Bullet_
{
    internal float fromX;
    internal float fromY;
    internal float fromZ;
    internal float toX;
    internal float toY;
    internal float toZ;
    internal float speed;
    internal float progress;
}

public class Expires
{
    internal static Expires Create(float p)
    {
        Expires expires = new()
        {
            totalTime = p,
            timeLeft = p
        };
        return expires;
    }

    internal float totalTime;
    internal float timeLeft;
}

public class DrawName
{
    internal float TextX;
    internal float TextY;
    internal float TextZ;
    internal string Name;
    internal bool DrawHealth;
    internal float Health;
    internal bool OnlyWhenSelected;
    internal bool ClientAutoComplete;
}

public class Entity
{
    public Entity()
    {
        scripts = new EntityScript[8];
        scriptsCount = 0;
    }
    internal Expires expires;
    internal Sprite sprite;
    internal Grenade_ grenade;
    internal Bullet_ bullet;
    internal Minecart minecart;
    internal PlayerDrawInfo playerDrawInfo;

    internal EntityScript[] scripts;
    internal int scriptsCount;

    // network
    internal EntityPosition_ networkPosition;
    internal EntityPosition_ position;
    internal DrawName drawName;
    internal EntityDrawModel drawModel;
    internal EntityDrawText drawText;
    internal Packet_ServerExplosion push;
    internal bool usable;
    internal Packet_ServerPlayerStats playerStats;
    internal EntityDrawArea drawArea;
}

public class EntityDrawArea
{
    internal int x;
    internal int y;
    internal int z;
    internal int sizex;
    internal int sizey;
    internal int sizez;
    internal bool visible;
}

public class EntityPosition_
{
    internal float x;
    internal float y;
    internal float z;
    internal float rotx;
    internal float roty;
    internal float rotz;

    internal bool PositionLoaded;
    internal int LastUpdateMilliseconds;
}

public class EntityDrawModel
{
    public EntityDrawModel()
    {
        CurrentTexture = -1;
    }
    internal float eyeHeight;
    internal string Model_;
    internal float ModelHeight;
    internal string Texture_;
    internal bool DownloadSkin;

    internal int CurrentTexture;
    internal HttpResponseCi SkinDownloadResponse;
    internal AnimatedModelRenderer renderer;
}

public class EntityDrawText
{
    internal float dx;
    internal float dy;
    internal float dz;
    internal float rotx;
    internal float roty;
    internal float rotz;
    internal string text;
}

public class VisibleDialog
{
    internal string key;
    internal Packet_Dialog value;
    internal GameScreen screen;
}

public class RailMapUtil
{
    internal Game game;
    public RailSlope GetRailSlope(int x, int y, int z)
    {
        int tiletype = game.map.GetBlock(x, y, z);
        int railDirectionFlags = game.blocktypes[tiletype].Rail;
        int blocknear;
        if (x < game.map.MapSizeX - 1)
        {
            blocknear = game.map.GetBlock(x + 1, y, z);
            if (railDirectionFlags == RailDirectionFlags.Horizontal &&
                 blocknear != 0 && game.blocktypes[blocknear].Rail == 0)
            {
                return RailSlope.TwoRightRaised;
            }
        }
        if (x > 0)
        {
            blocknear = game.map.GetBlock(x - 1, y, z);
            if (railDirectionFlags == RailDirectionFlags.Horizontal &&
                 blocknear != 0 && game.blocktypes[blocknear].Rail == 0)
            {
                return RailSlope.TwoLeftRaised;

            }
        }
        if (y > 0)
        {
            blocknear = game.map.GetBlock(x, y - 1, z);
            if (railDirectionFlags == RailDirectionFlags.Vertical &&
                  blocknear != 0 && game.blocktypes[blocknear].Rail == 0)
            {
                return RailSlope.TwoUpRaised;
            }
        }
        if (y < game.map.MapSizeY - 1)
        {
            blocknear = game.map.GetBlock(x, y + 1, z);
            if (railDirectionFlags == RailDirectionFlags.Vertical &&
                  blocknear != 0 && game.blocktypes[blocknear].Rail == 0)
            {
                return RailSlope.TwoDownRaised;
            }
        }
        return RailSlope.Flat;
    }
}

public class RailDirectionFlags
{
    public const int None = 0;
    public const int Horizontal = 1;
    public const int Vertical = 2;
    public const int UpLeft = 4;
    public const int UpRight = 8;
    public const int DownLeft = 16;
    public const int DownRight = 32;

    public const int Full = Horizontal | Vertical | UpLeft | UpRight | DownLeft | DownRight;
    public const int TwoHorizontalVertical = Horizontal | Vertical;
    public const int Corners = UpLeft | UpRight | DownLeft | DownRight;
}

public enum RailSlope
{
    Flat, TwoLeftRaised, TwoRightRaised, TwoUpRaised, TwoDownRaised
}

public enum RailDirection
{
    Horizontal,
    Vertical,
    UpLeft,
    UpRight,
    DownLeft,
    DownRight
}

public enum TileExitDirection
{
    Up,
    Down,
    Left,
    Right
}

public enum TileEnterDirection
{
    Up,
    Down,
    Left,
    Right
}

/// <summary>
/// Each RailDirection on tile can be traversed by train in two directions.
/// </summary>
/// <example>
/// RailDirection.Horizontal -> VehicleDirection12.HorizontalLeft (vehicle goes left and decreases x position),
/// and VehicleDirection12.HorizontalRight (vehicle goes right and increases x position).
/// </example>
public enum VehicleDirection12
{
    HorizontalLeft,
    HorizontalRight,
    VerticalUp,
    VerticalDown,

    UpLeftUp,
    UpLeftLeft,
    UpRightUp,
    UpRightRight,

    DownLeftDown,
    DownLeftLeft,
    DownRightDown,
    DownRightRight
}

public class VehicleDirection12Flags
{
    public const int None = 0;
    public const int HorizontalLeft = 1 << 0;
    public const int HorizontalRight = 1 << 1;
    public const int VerticalUp = 1 << 2;
    public const int VerticalDown = 1 << 3;

    public const int UpLeftUp = 1 << 4;
    public const int UpLeftLeft = 1 << 5;
    public const int UpRightUp = 1 << 6;
    public const int UpRightRight = 1 << 7;

    public const int DownLeftDown = 1 << 8;
    public const int DownLeftLeft = 1 << 9;
    public const int DownRightDown = 1 << 10;
    public const int DownRightRight = 1 << 11;
}

public class DirectionUtils
{
    /// <summary>
    /// VehicleDirection12.UpRightRight -> returns Direction4.Right
    /// </summary>
    /// <param name="direction"></param>
    /// <returns></returns>
    public static TileExitDirection ResultExit(VehicleDirection12 direction)
    {
        return direction switch
        {
            VehicleDirection12.HorizontalLeft => TileExitDirection.Left,
            VehicleDirection12.HorizontalRight => TileExitDirection.Right,
            VehicleDirection12.VerticalUp => TileExitDirection.Up,
            VehicleDirection12.VerticalDown => TileExitDirection.Down,
            VehicleDirection12.UpLeftUp => TileExitDirection.Up,
            VehicleDirection12.UpLeftLeft => TileExitDirection.Left,
            VehicleDirection12.UpRightUp => TileExitDirection.Up,
            VehicleDirection12.UpRightRight => TileExitDirection.Right,
            VehicleDirection12.DownLeftDown => TileExitDirection.Down,
            VehicleDirection12.DownLeftLeft => TileExitDirection.Left,
            VehicleDirection12.DownRightDown => TileExitDirection.Down,
            VehicleDirection12.DownRightRight => TileExitDirection.Right,
            _ => TileExitDirection.Down,
        };
    }

    public static RailDirection ToRailDirection(VehicleDirection12 direction)
    {
        return direction switch
        {
            VehicleDirection12.HorizontalLeft => RailDirection.Horizontal,
            VehicleDirection12.HorizontalRight => RailDirection.Horizontal,
            VehicleDirection12.VerticalUp => RailDirection.Vertical,
            VehicleDirection12.VerticalDown => RailDirection.Vertical,
            VehicleDirection12.UpLeftUp => RailDirection.UpLeft,
            VehicleDirection12.UpLeftLeft => RailDirection.UpLeft,
            VehicleDirection12.UpRightUp => RailDirection.UpRight,
            VehicleDirection12.UpRightRight => RailDirection.UpRight,
            VehicleDirection12.DownLeftDown => RailDirection.DownLeft,
            VehicleDirection12.DownLeftLeft => RailDirection.DownLeft,
            VehicleDirection12.DownRightDown => RailDirection.DownRight,
            VehicleDirection12.DownRightRight => RailDirection.DownRight,
            _ => RailDirection.DownLeft,
        };
    }

    public static int ToRailDirectionFlags(RailDirection direction)
    {
        switch (direction)
        {
            case RailDirection.DownLeft:
                return RailDirectionFlags.DownLeft;
            case RailDirection.DownRight:
                return RailDirectionFlags.DownRight;
            case RailDirection.Horizontal:
                return RailDirectionFlags.Horizontal;
            case RailDirection.UpLeft:
                return RailDirectionFlags.UpLeft;
            case RailDirection.UpRight:
                return RailDirectionFlags.UpRight;
            case RailDirection.Vertical:
                return RailDirectionFlags.Vertical;
            default:
                return 0;
        }
    }

    public static VehicleDirection12 Reverse(VehicleDirection12 direction)
    {
        return direction switch
        {
            VehicleDirection12.HorizontalLeft => VehicleDirection12.HorizontalRight,
            VehicleDirection12.HorizontalRight => VehicleDirection12.HorizontalLeft,
            VehicleDirection12.VerticalUp => VehicleDirection12.VerticalDown,
            VehicleDirection12.VerticalDown => VehicleDirection12.VerticalUp,
            VehicleDirection12.UpLeftUp => VehicleDirection12.UpLeftLeft,
            VehicleDirection12.UpLeftLeft => VehicleDirection12.UpLeftUp,
            VehicleDirection12.UpRightUp => VehicleDirection12.UpRightRight,
            VehicleDirection12.UpRightRight => VehicleDirection12.UpRightUp,
            VehicleDirection12.DownLeftDown => VehicleDirection12.DownLeftLeft,
            VehicleDirection12.DownLeftLeft => VehicleDirection12.DownLeftDown,
            VehicleDirection12.DownRightDown => VehicleDirection12.DownRightRight,
            VehicleDirection12.DownRightRight => VehicleDirection12.DownRightDown,
            _ => VehicleDirection12.DownLeftDown,
        };
    }

    public static int ToVehicleDirection12Flags(VehicleDirection12 direction)
    {
        return direction switch
        {
            VehicleDirection12.HorizontalLeft => VehicleDirection12Flags.HorizontalLeft,
            VehicleDirection12.HorizontalRight => VehicleDirection12Flags.HorizontalRight,
            VehicleDirection12.VerticalUp => VehicleDirection12Flags.VerticalUp,
            VehicleDirection12.VerticalDown => VehicleDirection12Flags.VerticalDown,
            VehicleDirection12.UpLeftUp => VehicleDirection12Flags.UpLeftUp,
            VehicleDirection12.UpLeftLeft => VehicleDirection12Flags.UpLeftLeft,
            VehicleDirection12.UpRightUp => VehicleDirection12Flags.UpRightUp,
            VehicleDirection12.UpRightRight => VehicleDirection12Flags.UpRightRight,
            VehicleDirection12.DownLeftDown => VehicleDirection12Flags.DownLeftDown,
            VehicleDirection12.DownLeftLeft => VehicleDirection12Flags.DownLeftLeft,
            VehicleDirection12.DownRightDown => VehicleDirection12Flags.DownRightDown,
            VehicleDirection12.DownRightRight => VehicleDirection12Flags.DownRightRight,
            _ => 0,
        };
    }

    public static TileEnterDirection ResultEnter(TileExitDirection direction)
    {
        return direction switch
        {
            TileExitDirection.Up => TileEnterDirection.Down,
            TileExitDirection.Down => TileEnterDirection.Up,
            TileExitDirection.Left => TileEnterDirection.Right,
            TileExitDirection.Right => TileEnterDirection.Left,
            _ => TileEnterDirection.Down,
        };
    }
    public static int RailDirectionFlagsCount(int railDirectionFlags)
    {
        int count = 0;
        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.DownLeft)) != 0) { count++; }
        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.DownRight)) != 0) { count++; }
        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.Horizontal)) != 0) { count++; }
        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.UpLeft)) != 0) { count++; }
        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.UpRight)) != 0) { count++; }
        if ((railDirectionFlags & ToRailDirectionFlags(RailDirection.Vertical)) != 0) { count++; }
        return count;
    }

    public static int ToVehicleDirection12Flags_(VehicleDirection12[] directions, int directionsCount)
    {
        int flags = VehicleDirection12Flags.None;
        for (int i = 0; i < directionsCount; i++)
        {
            VehicleDirection12 d = directions[i];
            flags = flags | ToVehicleDirection12Flags(d);
        }
        return flags;
    }

    /// <summary>
    /// Enter at TileEnterDirection.Left -> yields VehicleDirection12.UpLeftUp,
    /// VehicleDirection12.HorizontalRight,
    /// VehicleDirection12.DownLeftDown
    /// </summary>
    /// <param name="enter_at"></param>
    /// <returns></returns>
    public static VehicleDirection12[] PossibleNewRails3(TileEnterDirection enter_at)
    {
        VehicleDirection12[] ret = new VehicleDirection12[3];
        switch (enter_at)
        {
            case TileEnterDirection.Left:
                ret[0] = VehicleDirection12.UpLeftUp;
                ret[1] = VehicleDirection12.HorizontalRight;
                ret[2] = VehicleDirection12.DownLeftDown;
                break;
            case TileEnterDirection.Down:
                ret[0] = VehicleDirection12.DownLeftLeft;
                ret[1] = VehicleDirection12.VerticalUp;
                ret[2] = VehicleDirection12.DownRightRight;
                break;
            case TileEnterDirection.Up:
                ret[0] = VehicleDirection12.UpLeftLeft;
                ret[1] = VehicleDirection12.VerticalDown;
                ret[2] = VehicleDirection12.UpRightRight;
                break;
            case TileEnterDirection.Right:
                ret[0] = VehicleDirection12.UpRightUp;
                ret[1] = VehicleDirection12.HorizontalLeft;
                ret[2] = VehicleDirection12.DownRightDown;
                break;
            default:
                return null;
        }
        return ret;
    }
}

public enum CameraType
{
    Fpp,
    Tpp,
    Overhead
}

public enum TypingState
{
    None,
    Typing,
    Ready
}

public class Grenade_
{
    internal float velocityX;
    internal float velocityY;
    internal float velocityZ;
    internal int block;
    internal int sourcePlayer;
}

/// <summary>
/// Stores and provides the most recently set modelview and projection matrices.
/// Updated each frame by the rendering pipeline via <see cref="LastModelViewMatrix"/>
/// and <see cref="LastProjectionMatrix"/>.
/// </summary>
public class CameraMatrixProvider : ICameraMatrixProvider
{
    /// <summary>The most recently set modelview matrix.</summary>
    internal Matrix4 LastModelViewMatrix;

    /// <summary>The most recently set projection matrix.</summary>
    internal Matrix4 LastProjectionMatrix;

    /// <inheritdoc/>
    public Matrix4 GetModelViewMatrix() => LastModelViewMatrix;

    /// <inheritdoc/>
    public Matrix4 GetProjectionMatrix() => LastProjectionMatrix;
}

public class MenuState
{
    internal int selected;
}

public enum EscapeMenuState
{
    Main,
    Options,
    Graphics,
    Keys,
    Other
}

public class MapLoadingProgressEventArgs
{
    internal int ProgressPercent;
    internal int ProgressBytes;
    internal string ProgressStatus;
}

public class Draw2dData
{
    internal float x1;
    internal float y1;
    internal float width;
    internal float height;
    internal int inAtlasId;
    internal int color;
}

public class Chunk
{
    public Chunk()
    {
        baseLightDirty = true;
    }

    internal byte[] data;
    internal int[] dataInt;
    internal byte[] baseLight;
    internal bool baseLightDirty;
    internal RenderedChunk rendered;

    public int GetBlockInChunk(int pos)
    {
        if (dataInt != null)
        {
            return dataInt[pos];
        }
        else
        {
            return data[pos];
        }
    }

    public void SetBlockInChunk(int pos, int block)
    {
        if (dataInt == null)
        {
            if (block < 255)
            {
                data[pos] = (byte)(block);
            }
            else
            {
                int n = Game.chunksize * Game.chunksize * Game.chunksize;
                dataInt = new int[n];
                for (int i = 0; i < n; i++)
                {
                    dataInt[i] = data[i];
                }
                data = null;

                dataInt[pos] = block;
            }
        }
        else
        {
            dataInt[pos] = block;
        }
    }

    public bool ChunkHasData()
    {
        return data != null || dataInt != null;
    }
}

public class RenderedChunk
{
    public RenderedChunk()
    {
        dirty = true;
    }
    internal int[] ids;
    internal int idsCount;
    internal bool dirty;
    internal byte[] light;
}

public class ITerrainTextures
{
    internal Game game;

    public int TexturesPacked => Game.texturesPacked();
    public int TerrainTexture => game.terrainTexture;
    public int[] TerrainTextures1d => game.terrainTextures1d;
    public int TerrainTexturesPerAtlas => game.terrainTexturesPerAtlas;
}

public class Config3d
{
    public Config3d()
    {
        ENABLE_BACKFACECULLING = true;
        ENABLE_TRANSPARENCY = true;
        ENABLE_MIPMAPS = true;
        ENABLE_VISIBILITY_CULLING = false;
        viewdistance = 128;
    }
    internal bool ENABLE_BACKFACECULLING;
    internal bool ENABLE_TRANSPARENCY;
    internal bool ENABLE_MIPMAPS;
    internal bool ENABLE_VISIBILITY_CULLING;
    internal float viewdistance;
    public float GetViewDistance() { return viewdistance; }
    public void SetViewDistance(float value) { viewdistance = value; }
    public bool GetEnableTransparency() { return ENABLE_TRANSPARENCY; }
    public void SetEnableTransparency(bool value) { ENABLE_TRANSPARENCY = value; }
    public bool GetEnableMipmaps() { return ENABLE_MIPMAPS; }
    public void SetEnableMipmaps(bool value) { ENABLE_MIPMAPS = value; }
}

public class MapUtilCi
{
    public static int Index3d(int x, int y, int h, int sizex, int sizey)
    {
        return (h * sizey + y) * sizex + x;
    }

    public static int Index2d(int x, int y, int sizex)
    {
        return x + y * sizex;
    }

    public static Vector3 Pos(int index, int sizex, int sizey)
    {
        int x = index % sizex;
        int y = (index / sizex) % sizey;
        int h = index / (sizex * sizey);
        return new Vector3(x, y, h);
    }

    internal static void PosInt(int index, int sizex, int sizey, ref Vector3i ret)
    {
        int x = index % sizex;
        int y = (index / sizex) % sizey;
        int h = index / (sizex * sizey);
        ret.X = x;
        ret.Y = y;
        ret.Z = h;
    }

    public static int PosX(int index, int sizex, int sizey)
    {
        return index % sizex;
    }

    public static int PosY(int index, int sizex, int sizey)
    {
        return (index / sizex) % sizey;
    }

    public static int PosZ(int index, int sizex, int sizey)
    {
        return index / (sizex * sizey);
    }
}

public class InfiniteMapChunked2d
{
    internal Game d_Map;
    public const int chunksize = 16;
    internal int[][] chunks;
    public int GetBlock(int x, int y)
    {
        int[] chunk = GetChunk(x, y);
        return chunk[MapUtilCi.Index2d(x % chunksize, y % chunksize, chunksize)];
    }
    public int[] GetChunk(int x, int y)
    {
        int[] chunk = null;
        int kx = x / chunksize;
        int ky = y / chunksize;
        if (chunks[MapUtilCi.Index2d(kx, ky, d_Map.map.MapSizeX / chunksize)] == null)
        {
            chunk = new int[chunksize * chunksize];// (byte*)Marshal.AllocHGlobal(chunksize * chunksize);
            for (int i = 0; i < chunksize * chunksize; i++)
            {
                chunk[i] = 0;
            }
            chunks[MapUtilCi.Index2d(kx, ky, d_Map.map.MapSizeX / chunksize)] = chunk;
        }
        chunk = chunks[MapUtilCi.Index2d(kx, ky, d_Map.map.MapSizeX / chunksize)];
        return chunk;
    }
    public void SetBlock(int x, int y, int blocktype)
    {
        GetChunk(x, y)[MapUtilCi.Index2d(x % chunksize, y % chunksize, chunksize)] = blocktype;
    }
    public void Restart()
    {
        //chunks = new byte[d_Map.MapSizeX / chunksize, d_Map.MapSizeY / chunksize][,];
        int n = (d_Map.map.MapSizeX / chunksize) * (d_Map.map.MapSizeY / chunksize);
        chunks = new int[n][];//(byte**)Marshal.AllocHGlobal(n * sizeof(IntPtr));
        for (int i = 0; i < n; i++)
        {
            chunks[i] = null;
        }
    }
    public void ClearChunk(int x, int y)
    {
        int px = x / chunksize;
        int py = y / chunksize;
        chunks[MapUtilCi.Index2d(px, py, d_Map.map.MapSizeX / chunksize)] = null;
    }
}

public abstract class ClientModManager
{
    public abstract void MakeScreenshot();
    public abstract void SetLocalPosition(float glx, float gly, float glz);
    public abstract float GetLocalPositionX();
    public abstract float GetLocalPositionY();
    public abstract float GetLocalPositionZ();
    public abstract void SetLocalOrientation(float glx, float gly, float glz);
    public abstract float GetLocalOrientationX();
    public abstract float GetLocalOrientationY();
    public abstract float GetLocalOrientationZ();
    public abstract void DisplayNotification(string message);
    public abstract void SendChatMessage(string message);
    public abstract GamePlatform GetPlatform();
    public abstract void ShowGui(int level);
    public abstract void SetFreemove(int level);
    public abstract int GetFreemove();
    public abstract Bitmap GrabScreenshot();
    public abstract AviWriterCi AviWriterCreate();
    public abstract int GetWindowWidth();
    public abstract int GetWindowHeight();
    public abstract bool IsFreemoveAllowed();
    public abstract void EnableCameraControl(bool enable);
    public abstract int WhiteTexture();
    public abstract void Draw2dTexture(int textureid, float x1, float y1, float width, float height, int inAtlasId, int color);
    public abstract void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureId);
    public abstract void Draw2dText(string text, float x, float y, float fontsize);
    public abstract void OrthoMode();
    public abstract void PerspectiveMode();
    public abstract Dictionary<string, string> GetPerformanceInfo();
}

public class ClientModManager1 : ClientModManager
{
    internal Game game;

    public override void MakeScreenshot()
    {
        game.platform.SaveScreenshot();
    }

    public override void SetLocalPosition(float glx, float gly, float glz)
    {
        game.player.position.x = glx;
        game.player.position.y = gly;
        game.player.position.z = glz;
    }

    public override float GetLocalPositionX()
    {
        return game.player.position.x;
    }

    public override float GetLocalPositionY()
    {
        return game.player.position.y;
    }

    public override float GetLocalPositionZ()
    {
        return game.player.position.z;
    }

    public override void SetLocalOrientation(float glx, float gly, float glz)
    {
        game.player.position.rotx = glx;
        game.player.position.roty = gly;
        game.player.position.rotz = glz;
    }

    public override float GetLocalOrientationX()
    {
        return game.player.position.rotx;
    }

    public override float GetLocalOrientationY()
    {
        return game.player.position.roty;
    }

    public override float GetLocalOrientationZ()
    {
        return game.player.position.rotz;
    }

    public override void DisplayNotification(string message)
    {
        game.AddChatline(message);
    }

    public override void SendChatMessage(string message)
    {
        game.SendChat(message);
    }

    public override GamePlatform GetPlatform()
    {
        return game.platform;
    }

    public override void ShowGui(int level)
    {
        if (level == 0)
        {
            game.ENABLE_DRAW2D = false;
        }
        else
        {
            game.ENABLE_DRAW2D = true;
        }
    }

    public override void SetFreemove(int level)
    {
        if (level == FreemoveLevelEnum.None)
        {
            game.controls.freemove = false;
            game.controls.noclip = false;
        }

        if (level == FreemoveLevelEnum.Freemove)
        {
            game.controls.freemove = true;
            game.controls.noclip = false;
        }

        if (level == FreemoveLevelEnum.Noclip)
        {
            game.controls.freemove = true;
            game.controls.noclip = true;
        }
    }

    public override int GetFreemove()
    {
        if (!game.controls.freemove)
        {
            return FreemoveLevelEnum.None;
        }
        if (game.controls.noclip)
        {
            return FreemoveLevelEnum.Noclip;
        }
        else
        {
            return FreemoveLevelEnum.Freemove;
        }
    }

    public override Bitmap GrabScreenshot()
    {
        return game.platform.GrabScreenshot();
    }

    public override AviWriterCi AviWriterCreate()
    {
        return game.platform.AviWriterCreate();
    }

    public override int GetWindowWidth()
    {
        return game.platform.GetCanvasWidth();
    }

    public override int GetWindowHeight()
    {
        return game.platform.GetCanvasHeight();
    }

    public override bool IsFreemoveAllowed()
    {
        return game.AllowFreemove;
    }

    public override void EnableCameraControl(bool enable)
    {
        game.enableCameraControl = enable;
    }

    public override int WhiteTexture()
    {
        return game.WhiteTexture();
    }

    public override void Draw2dTexture(int textureid, float x1, float y1, float width, float height, int inAtlasId, int color)
    {
        int a = Game.ColorA(color);
        int r = Game.ColorR(color);
        int g = Game.ColorG(color);
        int b = Game.ColorB(color);
        game.Draw2dTexture(textureid, (int)(x1), (int)(y1),
            (int)(width), (int)(height),
             inAtlasId, 0, Game.ColorFromArgb(a, r, g, b), false);
    }

    public override void Draw2dTextures(Draw2dData[] todraw, int todrawLength, int textureId)
    {
        game.Draw2dTextures(todraw, todrawLength, textureId);
    }


    public override void Draw2dText(string text, float x, float y, float fontsize)
    {
        FontCi font = new()
        {
            family = "Arial",
            size = fontsize
        };
        game.Draw2dText(text, font, x, y, null, false);
    }

    public override void OrthoMode()
    {
        game.OrthoMode(GetWindowWidth(), GetWindowHeight());
    }

    public override void PerspectiveMode()
    {
        game.PerspectiveMode();
    }

    public override Dictionary<string, string> GetPerformanceInfo()
    {
        return game.performanceinfo;
    }
}

public abstract class AviWriterCi
{
    public abstract void Open(string filename, int framerate, int width, int height);
    public abstract void AddFrame(Bitmap bitmap);
    public abstract void Close();
}

public class FreemoveLevelEnum
{
    public const int None = 0;
    public const int Freemove = 1;
    public const int Noclip = 2;
}

public class ModDrawMain : ModBase
{
    public override void OnReadOnlyMainThread(Game game, float dt)
    {
        game.MainThreadOnRenderFrame(dt);
    }
}

public class ModUpdateMain : ModBase
{
    // Should use ReadWrite to be correct but that would be too slow
    public override void OnReadOnlyMainThread(Game game, float dt)
    {
        game.Update(dt);
    }
}

public abstract class EntityScript
{
    public virtual void OnNewFrameFixed(Game game, int entity, float dt) { }
}

public class OnUseEntityArgs
{
    internal int entityId;
}

public class ClientCommandArgs
{
    internal string command;
    internal string arguments;
}

public class TextureAtlasCi
{
    public static void TextureCoords2d(int textureId, int texturesPacked, RectangleF r)
    {
        float one = 1;
        r.Y = (one / texturesPacked * (textureId / texturesPacked));
        r.X = (one / texturesPacked * (textureId % texturesPacked));
        r.Width = one / texturesPacked;
        r.Height = one / texturesPacked;
    }
}

public class CachedTexture
{
    internal int textureId;
    internal float sizeX;
    internal float sizeY;
    internal int lastuseMilliseconds;
}

public class Text_
{
    internal string text;
    internal float fontsize;
    internal int color;
    internal string fontfamily;
    internal int fontstyle;

    internal bool Equals_(Text_ t)
    {
        return this.text == t.text
            && this.fontsize == t.fontsize
            && this.color == t.color
            && this.fontfamily == t.fontfamily
            && this.fontstyle == t.fontstyle;
    }

    public string GetText() { return text; }
    public void SetText(string value) { text = value; }
    public float GetFontSize() { return fontsize; }
    public void SetFontSize(float value) { fontsize = value; }
    public int GetColor() { return color; }
    public void SetColor(int value) { color = value; }
    public string GetFontFamily() { return fontfamily; }
    public void SetFontFamily(string value) { fontfamily = value; }
    public int GetFontStyle() { return fontstyle; }
    public void SetFontStyle(int value) { fontstyle = value; }
}

public class CachedTextTexture
{
    internal Text_ text;
    internal CachedTexture texture;
}

public class FontCi
{
    internal string family;
    internal float size;
    internal int style;

    internal static FontCi Create(string family_, float size_, int style_)
    {
        FontCi f = new()
        {
            family = family_,
            size = size_,
            style = style_
        };
        return f;
    }
}

public class TextPart
{
    internal int color;
    internal string text;
}

public class TextColorRenderer
{
    internal GamePlatform platform;

    internal Bitmap CreateTextTexture(Text_ t)
    {
        TextPart[] parts = DecodeColors(t.text, t.color, out int partsCount);

        float totalwidth = 0;
        float totalheight = 0;
        int[] sizesX = new int[partsCount];
        int[] sizesY = new int[partsCount];

        for (int i = 0; i < partsCount; i++)
        {
            platform.TextSize(parts[i].text, t.fontsize, out int outWidth, out int outHeight);

            sizesX[i] = outWidth;
            sizesY[i] = outHeight;

            totalwidth += outWidth;
            totalheight = Math.Max(totalheight, outHeight);
        }

        int size2X = NextPowerOfTwo((int)(totalwidth) + 1);
        int size2Y = NextPowerOfTwo((int)(totalheight) + 1);
        Bitmap bmp2 = new(size2X, size2Y);
        int[] bmp2Pixels = new int[size2X * size2Y];

        float currentwidth = 0;
        for (int i = 0; i < partsCount; i++)
        {
            int sizeiX = sizesX[i];
            int sizeiY = sizesY[i];
            if (sizeiX == 0 || sizeiY == 0)
            {
                continue;
            }
            Text_ partText = new()
            {
                text = parts[i].text,
                color = parts[i].color,
                fontsize = t.fontsize,
                fontstyle = t.fontstyle,
                fontfamily = t.fontfamily
            };
            Bitmap partBmp = platform.CreateTextTexture(partText);
            int partWidth = (int)(platform.BitmapGetWidth(partBmp));
            int partHeight = (int)(platform.BitmapGetHeight(partBmp));
            int[] partBmpPixels = new int[partWidth * partHeight];
            platform.BitmapGetPixelsArgb(partBmp, partBmpPixels);
            for (int x = 0; x < partWidth; x++)
            {
                for (int y = 0; y < partHeight; y++)
                {
                    if (x + currentwidth >= size2X) { continue; }
                    if (y >= size2Y) { continue; }
                    int c = partBmpPixels[MapUtilCi.Index2d(x, y, partWidth)];
                    if (Game.ColorA(c) > 0)
                    {
                        bmp2Pixels[MapUtilCi.Index2d((int)(currentwidth) + x, y, size2X)] = c;
                    }
                }
            }
            currentwidth += sizeiX;
        }
        platform.BitmapSetPixelsArgb(bmp2, bmp2Pixels);
        return bmp2;
    }

    public static TextPart[] DecodeColors(string s, int defaultcolor, out int retLength)
    {
        TextPart[] parts = new TextPart[256];
        int partsCount = 0;

        int currentcolor = defaultcolor;
        int[] currenttext = new int[256];
        int currenttextLength = 0;


        for (int i = 0; i < s.Length; i++)
        {
            if (s[i] == '&')
            {
                if (i + 1 < s.Length)
                {
                    int color = HexToInt(s[i + 1]);
                    if (color != -1)
                    {
                        if (currenttextLength != 0)
                        {
                            TextPart part = new()
                            {
                                text = StringUtils.CharArrayToString(currenttext, currenttextLength),
                                color = currentcolor
                            };
                            parts[partsCount++] = part;
                        }

                        currenttextLength = 0;
                        currentcolor = GetColor(color);
                        i++;
                    }
                    else
                    {
                        currenttext[currenttextLength++] = s[i];
                    }
                }
                else
                {
                    currenttext[currenttextLength++] = s[i];
                }
            }
            else
            {
                currenttext[currenttextLength++] = s[i];
            }
        }

        if (currenttextLength != 0)
        {
            TextPart part = new()
            {
                text = StringUtils.CharArrayToString(currenttext, currenttextLength),
                color = currentcolor
            };
            parts[partsCount++] = part;
        }

        retLength = partsCount;
        return parts;
    }

    private static int NextPowerOfTwo(int x)
    {
        x--;
        x |= x >> 1;  // handle  2 bit numbers
        x |= x >> 2;  // handle  4 bit numbers
        x |= x >> 4;  // handle  8 bit numbers
        x |= x >> 8;  // handle 16 bit numbers
        //x |= x >> 16; // handle 32 bit numbers
        x++;
        return x;
    }

    private static int GetColor(int currentcolor)
    {
        switch (currentcolor)
        {
            case 0: { return Game.ColorFromArgb(255, 0, 0, 0); }
            case 1: { return Game.ColorFromArgb(255, 0, 0, 191); }
            case 2: { return Game.ColorFromArgb(255, 0, 191, 0); }
            case 3: { return Game.ColorFromArgb(255, 0, 191, 191); }
            case 4: { return Game.ColorFromArgb(255, 191, 0, 0); }
            case 5: { return Game.ColorFromArgb(255, 191, 0, 191); }
            case 6: { return Game.ColorFromArgb(255, 191, 191, 0); }
            case 7: { return Game.ColorFromArgb(255, 191, 191, 191); }
            case 8: { return Game.ColorFromArgb(255, 40, 40, 40); }
            case 9: { return Game.ColorFromArgb(255, 64, 64, 255); }
            case 10: { return Game.ColorFromArgb(255, 64, 255, 64); }
            case 11: { return Game.ColorFromArgb(255, 64, 255, 255); }
            case 12: { return Game.ColorFromArgb(255, 255, 64, 64); }
            case 13: { return Game.ColorFromArgb(255, 255, 64, 255); }
            case 14: { return Game.ColorFromArgb(255, 255, 255, 64); }
            case 15: { return Game.ColorFromArgb(255, 255, 255, 255); }
            default: return Game.ColorFromArgb(255, 255, 255, 255);
        }
    }

    private static int HexToInt(int c)
    {
        if (c == '0') { return 0; }
        if (c == '1') { return 1; }
        if (c == '2') { return 2; }
        if (c == '3') { return 3; }
        if (c == '4') { return 4; }
        if (c == '5') { return 5; }
        if (c == '6') { return 6; }
        if (c == '7') { return 7; }
        if (c == '8') { return 8; }
        if (c == '9') { return 9; }
        if (c == 'a') { return 10; }
        if (c == 'b') { return 11; }
        if (c == 'c') { return 12; }
        if (c == 'd') { return 13; }
        if (c == 'e') { return 14; }
        if (c == 'f') { return 15; }
        return -1;
    }
}

public class CameraMove
{
    internal bool TurnLeft;
    internal bool TurnRight;
    internal bool DistanceUp;
    internal bool DistanceDown;
    internal bool AngleUp;
    internal bool AngleDown;
    internal int MoveX;
    internal int MoveY;
    internal float Distance;
}

public class Kamera
{
    public Kamera()
    {
        one = 1;
        distance = 5;
        Angle = 45;
        MinimumDistance = 2;
        tt = 0;
        MaximumAngle = 89;
        MinimumAngle = 0;
        Center = new Vector3();
    }
    private readonly float one;
    public void GetPosition(GamePlatform platform, ref Vector3 ret)
    {
        float cx = MathF.Cos(tt * one / 2) * GetFlatDistance(platform) + Center.X;
        float cy = MathF.Sin(tt * one / 2) * GetFlatDistance(platform) + Center.Z;
        ret.X = cx;
        ret.Y = Center.Y + GetCameraHeightFromCenter(platform);
        ret.Z = cy;
    }
    private float distance;
    public float GetDistance() { return distance; }
    public void SetDistance(float value)
    {
        distance = value;
        if (distance < MinimumDistance)
        {
            distance = MinimumDistance;
        }
    }
    internal float Angle;
    internal float MinimumDistance;
    private float GetCameraHeightFromCenter(GamePlatform platform)
    {
        return MathF.Sin(Angle * MathF.PI / 180) * distance;
    }
    private float GetFlatDistance(GamePlatform platform)
    {
        return MathF.Cos(Angle * MathF.PI / 180) * distance;
    }
    internal Vector3 Center;
    internal float tt;
    public float GetT()
    {
        return tt;
    }
    public void SetT(float value)
    {
        tt = value;
    }
    public void TurnLeft(float p)
    {
        tt += p;
    }
    public void TurnRight(float p)
    {
        tt -= p;
    }
    public void Move(CameraMove camera_move, float p)
    {
        p *= 2;
        p *= 2;
        if (camera_move.TurnLeft)
        {
            TurnLeft(p);
        }
        if (camera_move.TurnRight)
        {
            TurnRight(p);
        }
        if (camera_move.DistanceUp)
        {
            SetDistance(GetDistance() + p);
        }
        if (camera_move.DistanceDown)
        {
            SetDistance(GetDistance() - p);
        }
        if (camera_move.AngleUp)
        {
            Angle += p * 10;
        }
        if (camera_move.AngleDown)
        {
            Angle -= p * 10;
        }
        SetDistance(camera_move.Distance);
        //if (MaximumAngle < MinimumAngle) { throw new Exception(); }
        SetValidAngle();
    }

    private void SetValidAngle()
    {
        if (Angle > MaximumAngle) { Angle = MaximumAngle; }
        if (Angle < MinimumAngle) { Angle = MinimumAngle; }
    }

    internal int MaximumAngle;
    internal int MinimumAngle;

    public float GetAngle()
    {
        return Angle;
    }

    public void SetAngle(float value)
    {
        Angle = value;
    }

    public Vector3 GetCenter()
    {
        return new Vector3(Center.X, Center.Y, Center.Z);
    }

    public void TurnUp(float p)
    {
        Angle += p;
        SetValidAngle();
    }
}

public abstract class IMapStorage2
{
    public abstract int GetMapSizeX();
    public abstract int GetMapSizeY();
    public abstract int GetMapSizeZ();
    public abstract int GetBlock(int x, int y, int z);
    public abstract void SetBlock(int x, int y, int z, int tileType);
}

public class MapStorage2 : IMapStorage2
{
    public static MapStorage2 Create(Game game)
    {
        MapStorage2 s = new()
        {
            game = game
        };
        return s;
    }
    private Game game;
    public override int GetMapSizeX()
    {
        return game.map.MapSizeX;
    }

    public override int GetMapSizeY()
    {
        return game.map.MapSizeY;
    }

    public override int GetMapSizeZ()
    {
        return game.map.MapSizeZ;
    }

    public override int GetBlock(int x, int y, int z)
    {
        return game.map.GetBlock(x, y, z);
    }

    public override void SetBlock(int x, int y, int z, int tileType)
    {
        game.SetBlock(x, y, z, tileType);
    }
}

public class GameDataMonsters
{
    public GameDataMonsters()
    {
        int n = 5;
        MonsterCode = new string[n];
        MonsterName = new string[n];
        MonsterSkin = new string[n];
        MonsterCode[0] = "imp.txt";
        MonsterName[0] = "Imp";
        MonsterSkin[0] = "imp.png";
        MonsterCode[1] = "imp.txt";
        MonsterName[1] = "Fire Imp";
        MonsterSkin[1] = "impfire.png";
        MonsterCode[2] = "dragon.txt";
        MonsterName[2] = "Dragon";
        MonsterSkin[2] = "dragon.png";
        MonsterCode[3] = "zombie.txt";
        MonsterName[3] = "Zombie";
        MonsterSkin[3] = "zombie.png";
        MonsterCode[4] = "cyclops.txt";
        MonsterName[4] = "Cyclops";
        MonsterSkin[4] = "cyclops.png";
    }
    internal string[] MonsterName;
    internal string[] MonsterCode;
    internal string[] MonsterSkin;
}

public enum GuiState
{
    Normal,
    EscapeMenu,
    Inventory,
    MapLoading,
    CraftingRecipes,
    ModalDialog
}

public enum FontType
{
    Nice,
    Simple,
    BlackBackground,
    Default
}

public class SpecialBlockId
{
    public const int Empty = 0;
}

public class GameData
{
    public GameData()
    {
        mBlockIdEmpty = 0;
        mBlockIdDirt = -1;
        mBlockIdSponge = -1;
        mBlockIdTrampoline = -1;
        mBlockIdAdminium = -1;
        mBlockIdCompass = -1;
        mBlockIdLadder = -1;
        mBlockIdEmptyHand = -1;
        mBlockIdCraftingTable = -1;
        mBlockIdLava = -1;
        mBlockIdStationaryLava = -1;
        mBlockIdFillStart = -1;
        mBlockIdCuboid = -1;
        mBlockIdFillArea = -1;
        mBlockIdMinecart = -1;
        mBlockIdRailstart = -128; // 64 rail tiles
    }

    public void Start()
    {
        Initialize(GlobalVar.MAX_BLOCKTYPES);
    }

    private void Initialize(int count)
    {
        mWhenPlayerPlacesGetsConvertedTo = new int[count];
        mIsFlower = new bool[count];
        mRail = new int[count];
        mWalkSpeed = new float[count];
        for (int i = 0; i < count; i++)
        {
            mWalkSpeed[i] = 1;
        }
        mIsSlipperyWalk = new bool[count];
        mWalkSound = new string[count][];
        for (int i = 0; i < count; i++)
        {
            mWalkSound[i] = new string[SoundCount];
        }
        mBreakSound = new string[count][];
        for (int i = 0; i < count; i++)
        {
            mBreakSound[i] = new string[SoundCount];
        }
        mBuildSound = new string[count][];
        for (int i = 0; i < count; i++)
        {
            mBuildSound[i] = new string[SoundCount];
        }
        mCloneSound = new string[count][];
        for (int i = 0; i < count; i++)
        {
            mCloneSound[i] = new string[SoundCount];
        }
        mLightRadius = new int[count];
        mStartInventoryAmount = new int[count];
        mStrength = new float[count];
        mDamageToPlayer = new int[count];
        mWalkableType = new int[count];

        mDefaultMaterialSlots = new int[10];
    }

    public int[] WhenPlayerPlacesGetsConvertedTo() { return mWhenPlayerPlacesGetsConvertedTo; }
    public bool[] IsFlower() { return mIsFlower; }
    public int[] Rail() { return mRail; }
    public float[] WalkSpeed() { return mWalkSpeed; }
    public bool[] IsSlipperyWalk() { return mIsSlipperyWalk; }
    public string[][] WalkSound() { return mWalkSound; }
    public string[][] BreakSound() { return mBreakSound; }
    public string[][] BuildSound() { return mBuildSound; }
    public string[][] CloneSound() { return mCloneSound; }
    public int[] LightRadius() { return mLightRadius; }
    public int[] StartInventoryAmount() { return mStartInventoryAmount; }
    public float[] Strength() { return mStrength; }
    public int[] DamageToPlayer() { return mDamageToPlayer; }
    public int[] WalkableType1() { return mWalkableType; }

    public int[] DefaultMaterialSlots() { return mDefaultMaterialSlots; }

    private int[] mWhenPlayerPlacesGetsConvertedTo;
    private bool[] mIsFlower;
    private int[] mRail;
    private float[] mWalkSpeed;
    private bool[] mIsSlipperyWalk;
    private string[][] mWalkSound;
    private string[][] mBreakSound;
    private string[][] mBuildSound;
    private string[][] mCloneSound;
    private int[] mLightRadius;
    private int[] mStartInventoryAmount;
    private float[] mStrength;
    private int[] mDamageToPlayer;
    private int[] mWalkableType;

    private int[] mDefaultMaterialSlots;

    // TODO: hardcoded IDs
    // few code sections still expect some hardcoded IDs
    private int mBlockIdEmpty;
    private int mBlockIdDirt;
    private int mBlockIdSponge;
    private int mBlockIdTrampoline;
    private int mBlockIdAdminium;
    private int mBlockIdCompass;
    private int mBlockIdLadder;
    private int mBlockIdEmptyHand;
    private int mBlockIdCraftingTable;
    private int mBlockIdLava;
    private int mBlockIdStationaryLava;
    private int mBlockIdFillStart;
    private int mBlockIdCuboid;
    private int mBlockIdFillArea;
    private int mBlockIdMinecart;
    private int mBlockIdRailstart; // 64 rail tiles

    public int BlockIdEmpty() { return mBlockIdEmpty; }
    public int BlockIdDirt() { return mBlockIdDirt; }
    public int BlockIdSponge() { return mBlockIdSponge; }
    public int BlockIdTrampoline() { return mBlockIdTrampoline; }
    public int BlockIdAdminium() { return mBlockIdAdminium; }
    public int BlockIdCompass() { return mBlockIdCompass; }
    public int BlockIdLadder() { return mBlockIdLadder; }
    public int BlockIdEmptyHand() { return mBlockIdEmptyHand; }
    public int BlockIdCraftingTable() { return mBlockIdCraftingTable; }
    public int BlockIdLava() { return mBlockIdLava; }
    public int BlockIdStationaryLava() { return mBlockIdStationaryLava; }
    public int BlockIdFillStart() { return mBlockIdFillStart; }
    public int BlockIdCuboid() { return mBlockIdCuboid; }
    public int BlockIdFillArea() { return mBlockIdFillArea; }
    public int BlockIdMinecart() { return mBlockIdMinecart; }
    public int BlockIdRailstart() { return mBlockIdRailstart; }

    // TODO: atm it sets sepcial block id from block name - better use new block property
    public bool SetSpecialBlock(Packet_BlockType b, int id)
    {
        switch (b.Name)
        {
            case "Empty":
                this.mBlockIdEmpty = id;
                return true;
            case "Dirt":
                this.mBlockIdDirt = id;
                return true;
            case "Sponge":
                this.mBlockIdSponge = id;
                return true;
            case "Trampoline":
                this.mBlockIdTrampoline = id;
                return true;
            case "Adminium":
                this.mBlockIdAdminium = id;
                return true;
            case "Compass":
                this.mBlockIdCompass = id;
                return true;
            case "Ladder":
                this.mBlockIdLadder = id;
                return true;
            case "EmptyHand":
                this.mBlockIdEmptyHand = id;
                return true;
            case "CraftingTable":
                this.mBlockIdCraftingTable = id;
                return true;
            case "Lava":
                this.mBlockIdLava = id;
                return true;
            case "StationaryLava":
                this.mBlockIdStationaryLava = id;
                return true;
            case "FillStart":
                this.mBlockIdFillStart = id;
                return true;
            case "Cuboid":
                this.mBlockIdCuboid = id;
                return true;
            case "FillArea":
                this.mBlockIdFillArea = id;
                return true;
            case "Minecart":
                this.mBlockIdMinecart = id;
                return true;
            case "Rail0":
                this.mBlockIdRailstart = id;
                return true;
            default:
                return false;
        }
    }

    public bool IsRailTile(int id)
    {
        return id >= BlockIdRailstart() && id < BlockIdRailstart() + 64;
    }

    public void UseBlockTypes(GamePlatform platform, Packet_BlockType[] blocktypes, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (blocktypes[i] != null)
            {
                UseBlockType(platform, i, blocktypes[i]);
            }
        }
    }

    public void UseBlockType(GamePlatform platform, int id, Packet_BlockType b)
    {
        if (b.Name == null)//!b.IsValid)
        {
            return;
        }
        //public bool[] IsWater { get { return mIsWater; } }
        //            public bool[] IsTransparentForLight { get { return mIsTransparentForLight; } }
        //public bool[] IsEmptyForPhysics { get { return mIsEmptyForPhysics; } }

        if (b.WhenPlacedGetsConvertedTo != 0)
        {
            mWhenPlayerPlacesGetsConvertedTo[id] = b.WhenPlacedGetsConvertedTo;
        }
        else
        {
            mWhenPlayerPlacesGetsConvertedTo[id] = id;
        }
        IsFlower()[id] = b.DrawType == Packet_DrawTypeEnum.Plant;
        Rail()[id] = b.Rail;
        WalkSpeed()[id] = DeserializeFloat(b.WalkSpeedFloat);
        IsSlipperyWalk()[id] = b.IsSlipperyWalk;
        WalkSound()[id] = new string[SoundCount];
        BreakSound()[id] = new string[SoundCount];
        BuildSound()[id] = new string[SoundCount];
        CloneSound()[id] = new string[SoundCount];
        if (b.Sounds != null)
        {
            for (int i = 0; i < b.Sounds.WalkCount; i++)
            {
                WalkSound()[id][i] = string.Concat(platform, b.Sounds.Walk[i], ".wav");
            }
            for (int i = 0; i < b.Sounds.Break1Count; i++)
            {
                BreakSound()[id][i] = string.Concat(platform, b.Sounds.Break1[i], ".wav");
            }
            for (int i = 0; i < b.Sounds.BuildCount; i++)
            {
                BuildSound()[id][i] = string.Concat(platform, b.Sounds.Build[i], ".wav");
            }
            for (int i = 0; i < b.Sounds.CloneCount; i++)
            {
                CloneSound()[id][i] = string.Concat(platform, b.Sounds.Clone[i], ".wav");
            }
        }
        LightRadius()[id] = b.LightRadius;
        //StartInventoryAmount { get; }
        Strength()[id] = b.Strength;
        DamageToPlayer()[id] = b.DamageToPlayer;
        WalkableType1()[id] = b.WalkableType;
        SetSpecialBlock(b, id);
    }

    public const int SoundCount = 8;

    private static float DeserializeFloat(int p)
    {
        float one = 1;
        return (one * p) / 32;
    }
}

public class OnCrashHandlerLeave : OnCrashHandler
{
    public static OnCrashHandlerLeave Create(Game game)
    {
        OnCrashHandlerLeave oncrash = new()
        {
            g = game
        };
        return oncrash;
    }
    private Game g;
    public override void OnCrash()
    {
        g.SendLeave(Packet_LeaveReasonEnum.Crash);
    }
}

public class OptionsCi
{
    public OptionsCi()
    {
        float one = 1;
        Shadows = false;
        Font = 0;
        DrawDistance = 32;
        UseServerTextures = true;
        EnableSound = true;
        EnableAutoJump = false;
        ClientLanguage = "";
        Framerate = 0;
        Resolution = 0;
        Fullscreen = false;
        Smoothshadows = true;
        BlockShadowSave = one * 6 / 10;
        EnableBlockShadow = true;
        Keys = new int[360];
    }
    internal bool Shadows;
    internal int Font;
    internal int DrawDistance;
    internal bool UseServerTextures;
    internal bool EnableSound;
    internal bool EnableAutoJump;
    internal string ClientLanguage;
    internal int Framerate;
    internal int Resolution;
    internal bool Fullscreen;
    internal bool Smoothshadows;
    internal float BlockShadowSave;
    internal bool EnableBlockShadow;
    internal int[] Keys;
}

public class TextureAtlas
{
    public static RectangleF TextureCoords2d(int textureId, int texturesPacked)
    {
        float one = 1;
        RectangleF r = new()
        {
            Y = (one / texturesPacked * (textureId / texturesPacked)),
            X = (one / texturesPacked * (textureId % texturesPacked)),
            Width = one / texturesPacked,
            Height = one / texturesPacked
        };
        return r;
    }
}

public class Map
{
    internal Chunk[] chunks;
    internal int MapSizeX;
    internal int MapSizeY;
    internal int MapSizeZ;

    private static int Index3d(int x, int y, int h, int sizex, int sizey)
    {
        return (h * sizey + y) * sizex + x;
    }

    public int GetBlockValid(int x, int y, int z)
    {
        int cx = x >> Game.chunksizebits;
        int cy = y >> Game.chunksizebits;
        int cz = z >> Game.chunksizebits;
        int chunkpos = Index3d(cx, cy, cz, MapSizeX >> Game.chunksizebits, MapSizeY >> Game.chunksizebits);
        if (chunks[chunkpos] == null)
        {
            return 0;
        }
        else
        {
            int pos = Index3d(x & (Game.chunksize - 1), y & (Game.chunksize - 1), z & (Game.chunksize - 1), Game.chunksize, Game.chunksize);
            return chunks[chunkpos].GetBlockInChunk(pos);
        }
    }

    public Chunk GetChunk(int x, int y, int z)
    {
        x = x / Game.chunksize;
        y = y / Game.chunksize;
        z = z / Game.chunksize;
        return GetChunk_(x, y, z);
    }

    public Chunk GetChunk_(int cx, int cy, int cz)
    {
        int mapsizexchunks = MapSizeX / Game.chunksize;
        int mapsizeychunks = MapSizeY / Game.chunksize;
        Chunk chunk = chunks[Index3d(cx, cy, cz, mapsizexchunks, mapsizeychunks)];
        if (chunk == null)
        {
            Chunk c = new()
            {
                data = new byte[Game.chunksize * Game.chunksize * Game.chunksize],
                baseLight = new byte[Game.chunksize * Game.chunksize * Game.chunksize]
            };
            chunks[Index3d(cx, cy, cz, mapsizexchunks, mapsizeychunks)] = c;
            return chunks[Index3d(cx, cy, cz, mapsizexchunks, mapsizeychunks)];
        }
        return chunk;
    }

    public void SetBlockRaw(int x, int y, int z, int tileType)
    {
        Chunk chunk = GetChunk(x, y, z);
        int pos = Index3d(x % Game.chunksize, y % Game.chunksize, z % Game.chunksize, Game.chunksize, Game.chunksize);
        chunk.SetBlockInChunk(pos, tileType);
    }

    public static void CopyChunk(Chunk chunk, int[] output)
    {
        int n = Game.chunksize * Game.chunksize * Game.chunksize;
        if (chunk.dataInt != null)
        {
            for (int i = 0; i < n; i++)
            {
                output[i] = chunk.dataInt[i];
            }
        }
        else
        {
            for (int i = 0; i < n; i++)
            {
                output[i] = chunk.data[i];
            }
        }
    }

    public void Reset(int sizex, int sizey, int sizez)
    {
        MapSizeX = sizex;
        MapSizeY = sizey;
        MapSizeZ = sizez;
        chunks = new Chunk[(sizex / Game.chunksize) * (sizey / Game.chunksize) * (sizez / Game.chunksize)];
    }

    public void GetMapPortion(int[] outPortion, int x, int y, int z, int portionsizex, int portionsizey, int portionsizez)
    {
        int outPortionCount = portionsizex * portionsizey * portionsizez;
        for (int i = 0; i < outPortionCount; i++)
        {
            outPortion[i] = 0;
        }

        //int chunksizebits = p.FloatToInt(p.MathLog(chunksize, 2));

        int mapchunksx = MapSizeX / Game.chunksize;
        int mapchunksy = MapSizeY / Game.chunksize;
        int mapchunksz = MapSizeZ / Game.chunksize;
        int mapsizechunks = mapchunksx * mapchunksy * mapchunksz;

        for (int xx = 0; xx < portionsizex; xx++)
        {
            for (int yy = 0; yy < portionsizey; yy++)
            {
                for (int zz = 0; zz < portionsizez; zz++)
                {
                    //Find chunk.
                    int cx = (x + xx) >> Game.chunksizebits;
                    int cy = (y + yy) >> Game.chunksizebits;
                    int cz = (z + zz) >> Game.chunksizebits;
                    //int cpos = MapUtil.Index3d(cx, cy, cz, MapSizeX / chunksize, MapSizeY / chunksize);
                    int cpos = (cz * mapchunksy + cy) * mapchunksx + cx;
                    //if (cpos < 0 || cpos >= ((MapSizeX / chunksize) * (MapSizeY / chunksize) * (MapSizeZ / chunksize)))
                    if (cpos < 0 || cpos >= mapsizechunks)
                    {
                        continue;
                    }
                    Chunk chunk = chunks[cpos];
                    if (chunk == null || !chunk.ChunkHasData())
                    {
                        continue;
                    }
                    //int pos = MapUtil.Index3d((x + xx) % chunksize, (y + yy) % chunksize, (z + zz) % chunksize, chunksize, chunksize);
                    int chunkGlobalX = cx << Game.chunksizebits;
                    int chunkGlobalY = cy << Game.chunksizebits;
                    int chunkGlobalZ = cz << Game.chunksizebits;

                    int inChunkX = (x + xx) - chunkGlobalX;
                    int inChunkY = (y + yy) - chunkGlobalY;
                    int inChunkZ = (z + zz) - chunkGlobalZ;

                    //int pos = MapUtil.Index3d(inChunkX, inChunkY, inChunkZ, chunksize, chunksize);
                    int pos = (((inChunkZ << Game.chunksizebits) + inChunkY) << Game.chunksizebits) + inChunkX;

                    int block = chunk.GetBlockInChunk(pos);
                    //outPortion[MapUtil.Index3d(xx, yy, zz, portionsizex, portionsizey)] = (byte)block;
                    outPortion[(zz * portionsizey + yy) * portionsizex + xx] = block;
                }
            }
        }
    }

    public bool IsValidPos(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0)
        {
            return false;
        }
        if (x >= MapSizeX || y >= MapSizeY || z >= MapSizeZ)
        {
            return false;
        }
        return true;
    }

    public bool IsValidChunkPos(int cx, int cy, int cz)
    {
        return cx >= 0 && cy >= 0 && cz >= 0
            && cx < MapSizeX / Game.chunksize
            && cy < MapSizeY / Game.chunksize
            && cz < MapSizeZ / Game.chunksize;
    }

    public int GetBlock(int x, int y, int z)
    {
        if (!IsValidPos(x, y, z))
        {
            return 0;
        }
        return GetBlockValid(x, y, z);
    }

    public void SetChunkDirty(int cx, int cy, int cz, bool dirty, bool blockschanged)
    {
        if (!IsValidChunkPos(cx, cy, cz))
        {
            return;
        }

        Chunk c = chunks[MapUtilCi.Index3d(cx, cy, cz, Mapsizexchunks, Mapsizeychunks)];
        if (c == null)
        {
            return;
        }
        c.rendered ??= new RenderedChunk();
        c.rendered.dirty = dirty;
        if (blockschanged)
        {
            c.baseLightDirty = true;
        }
    }

    public int Mapsizexchunks => MapSizeX >> Game.chunksizebits;
    public int Mapsizeychunks => MapSizeY >> Game.chunksizebits;
    public int Mapsizezchunks => MapSizeZ >> Game.chunksizebits;

    public void SetChunksAroundDirty(int cx, int cy, int cz)
    {
        if (IsValidChunkPos(cx, cy, cz)) { SetChunkDirty(cx - 1, cy, cz, true, false); }
        if (IsValidChunkPos(cx - 1, cy, cz)) { SetChunkDirty(cx - 1, cy, cz, true, false); }
        if (IsValidChunkPos(cx + 1, cy, cz)) { SetChunkDirty(cx + 1, cy, cz, true, false); }
        if (IsValidChunkPos(cx, cy - 1, cz)) { SetChunkDirty(cx, cy - 1, cz, true, false); }
        if (IsValidChunkPos(cx, cy + 1, cz)) { SetChunkDirty(cx, cy + 1, cz, true, false); }
        if (IsValidChunkPos(cx, cy, cz - 1)) { SetChunkDirty(cx, cy, cz - 1, true, false); }
        if (IsValidChunkPos(cx, cy, cz + 1)) { SetChunkDirty(cx, cy, cz + 1, true, false); }
    }

    public void SetMapPortion(int x, int y, int z, int[] chunk, int sizeX, int sizeY, int sizeZ)
    {
        int chunksizex = sizeX;
        int chunksizey = sizeY;
        int chunksizez = sizeZ;
        //if (chunksizex % chunksize != 0) { platform.ThrowException(""); }
        //if (chunksizey % chunksize != 0) { platform.ThrowException(""); }
        //if (chunksizez % chunksize != 0) { platform.ThrowException(""); }
        int chunksize = Game.chunksize;
        Chunk[] localchunks = new Chunk[(chunksizex / chunksize) * (chunksizey / chunksize) * (chunksizez / chunksize)];
        for (int cx = 0; cx < chunksizex / chunksize; cx++)
        {
            for (int cy = 0; cy < chunksizey / chunksize; cy++)
            {
                for (int cz = 0; cz < chunksizex / chunksize; cz++)
                {
                    localchunks[Index3d(cx, cy, cz, (chunksizex / chunksize), (chunksizey / chunksize))] = GetChunk(x + cx * chunksize, y + cy * chunksize, z + cz * chunksize);
                    FillChunk(localchunks[Index3d(cx, cy, cz, (chunksizex / chunksize), (chunksizey / chunksize))], chunksize, cx * chunksize, cy * chunksize, cz * chunksize, chunk, sizeX, sizeY, sizeZ);
                }
            }
        }
        for (int xxx = 0; xxx < chunksizex; xxx += chunksize)
        {
            for (int yyy = 0; yyy < chunksizex; yyy += chunksize)
            {
                for (int zzz = 0; zzz < chunksizex; zzz += chunksize)
                {
                    SetChunkDirty((x + xxx) / chunksize, (y + yyy) / chunksize, (z + zzz) / chunksize, true, true);
                    SetChunksAroundDirty((x + xxx) / chunksize, (y + yyy) / chunksize, (z + zzz) / chunksize);
                }
            }
        }
    }

    public static void FillChunk(Chunk destination, int destinationchunksize, int sourcex, int sourcey, int sourcez, int[] source, int sourcechunksizeX, int sourcechunksizeY, int sourcechunksizeZ)
    {
        for (int x = 0; x < destinationchunksize; x++)
        {
            for (int y = 0; y < destinationchunksize; y++)
            {
                for (int z = 0; z < destinationchunksize; z++)
                {
                    //if (x + sourcex < source.GetUpperBound(0) + 1
                    //    && y + sourcey < source.GetUpperBound(1) + 1
                    //    && z + sourcez < source.GetUpperBound(2) + 1)
                    {
                        destination.SetBlockInChunk(Index3d(x, y, z, destinationchunksize, destinationchunksize)
                            , source[Index3d(x + sourcex, y + sourcey, z + sourcez, sourcechunksizeX, sourcechunksizeY)]);
                    }
                }
            }
        }
    }

    public int MaybeGetLight(int x, int y, int z)
    {
        int light = -1;
        int cx = x / Game.chunksize;
        int cy = y / Game.chunksize;
        int cz = z / Game.chunksize;
        if (IsValidPos(x, y, z) && IsValidChunkPos(cx, cy, cz))
        {
            Chunk c = chunks[MapUtilCi.Index3d(cx, cy, cz, Mapsizexchunks, Mapsizeychunks)];
            if (c == null
                || c.rendered == null
                || c.rendered.light == null)
            {
                light = -1;
            }
            else
            {
                light = c.rendered.light[MapUtilCi.Index3d((x % Game.chunksize) + 1, (y % Game.chunksize) + 1, (z % Game.chunksize) + 1, Game.chunksize + 2, Game.chunksize + 2)];
            }
        }
        return light;
    }

    public void SetBlockDirty(int x, int y, int z)
    {
        Vector3i[] around = ModDrawTerrain.BlocksAround7(new Vector3i(x, y, z));
        for (int i = 0; i < 7; i++)
        {
            Vector3i a = around[i];
            int xx = a.X;
            int yy = a.Y;
            int zz = a.Z;
            if (xx < 0 || yy < 0 || zz < 0 || xx >= MapSizeX || yy >= MapSizeY || zz >= MapSizeZ)
            {
                return;
            }
            SetChunkDirty((xx / Game.chunksize), (yy / Game.chunksize), (zz / Game.chunksize), true, true);
        }
    }

    public bool IsChunkRendered(int cx, int cy, int cz)
    {
        Chunk c = chunks[MapUtilCi.Index3d(cx, cy, cz, Mapsizexchunks, Mapsizeychunks)];
        if (c == null)
        {
            return false;
        }
        return c.rendered != null && c.rendered.ids != null;
    }
}
