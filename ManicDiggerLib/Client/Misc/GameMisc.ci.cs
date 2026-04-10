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
    internal LoginResult loginResult;

    public void Login(IGamePlatform platform, string user, string password, string publicServerKey, string token, LoginResult result, LoginData resultLoginData_)
    {
        loginResult = result;
        resultLoginData = resultLoginData_;
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
    private HttpResponse loginUrlResponse;
    private HttpResponse loginResponse;
    private LoginData resultLoginData;

    public void Update(IGamePlatform platform)
    {
        if (loginResult == null)
        {
            return;
        }

        if (loginUrlResponse == null && loginUrl == null)
        {
            loginUrlResponse = new HttpResponse();
            platform.WebClientDownloadDataAsync("http://manicdigger.sourceforge.net/login.php", loginUrlResponse);
        }
        if (loginUrlResponse != null && loginUrlResponse.Done)
        {
            loginUrl = Encoding.UTF8.GetString(loginUrlResponse.Value, 0, loginUrlResponse.ValueLength);
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
                loginResponse = new HttpResponse();
                platform.WebClientUploadDataAsync(loginUrl, byteArray, byteArray.Length, loginResponse);
            }
            if (loginResponse != null && loginResponse.Done)
            {
                string responseString = Encoding.UTF8.GetString(loginResponse.Value, 0, loginResponse.ValueLength);
                resultLoginData.PasswordCorrect = !(responseString.Contains("Wrong username") || responseString.Contains("Incorrect username"));
                resultLoginData.ServerCorrect = !responseString.Contains("server");
                if (resultLoginData.PasswordCorrect)
                {
                    loginResult = LoginResult.Ok;
                }
                else
                {
                    loginResult = LoginResult.Failed;
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
    internal IGamePlatform platform;
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

public class Bullet
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
        scripts = new IEntityScript[8];
        scriptsCount = 0;
    }
    internal Expires expires;
    internal Sprite sprite;
    internal Grenade grenade;
    internal Bullet bullet;
    internal Minecart minecart;
    internal PlayerDrawInfo playerDrawInfo;

    internal IEntityScript[] scripts;
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
    internal HttpResponse SkinDownloadResponse;
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
        int tiletype = game.VoxelMap.GetBlock(x, y, z);
        int railDirectionFlags = game.blocktypes[tiletype].Rail;
        int blocknear;
        if (x < game.VoxelMap.MapSizeX - 1)
        {
            blocknear = game.VoxelMap.GetBlock(x + 1, y, z);
            if (railDirectionFlags == RailDirectionFlags.Horizontal &&
                 blocknear != 0 && game.blocktypes[blocknear].Rail == 0)
            {
                return RailSlope.TwoRightRaised;
            }
        }
        if (x > 0)
        {
            blocknear = game.VoxelMap.GetBlock(x - 1, y, z);
            if (railDirectionFlags == RailDirectionFlags.Horizontal &&
                 blocknear != 0 && game.blocktypes[blocknear].Rail == 0)
            {
                return RailSlope.TwoLeftRaised;

            }
        }
        if (y > 0)
        {
            blocknear = game.VoxelMap.GetBlock(x, y - 1, z);
            if (railDirectionFlags == RailDirectionFlags.Vertical &&
                  blocknear != 0 && game.blocktypes[blocknear].Rail == 0)
            {
                return RailSlope.TwoUpRaised;
            }
        }
        if (y < game.VoxelMap.MapSizeY - 1)
        {
            blocknear = game.VoxelMap.GetBlock(x, y + 1, z);
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
        return direction switch
        {
            RailDirection.DownLeft => RailDirectionFlags.DownLeft,
            RailDirection.DownRight => RailDirectionFlags.DownRight,
            RailDirection.Horizontal => RailDirectionFlags.Horizontal,
            RailDirection.UpLeft => RailDirectionFlags.UpLeft,
            RailDirection.UpRight => RailDirectionFlags.UpRight,
            RailDirection.Vertical => RailDirectionFlags.Vertical,
            _ => 0,
        };
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

public class Grenade
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

public class ITerrainTextures
{
    internal Game game;

    public int TexturesPacked => Game.TexturesPacked;
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
    public static RectangleF TextureCoords2d(int textureId, int texturesPacked)
    {
        float step = 1f / texturesPacked;
        return new RectangleF
        {
            X = step * (textureId % texturesPacked),
            Y = step * (textureId / texturesPacked),
            Width = step,
            Height = step
        };
    }
}

public class CachedTexture
{
    internal int textureId;
    internal float sizeX;
    internal float sizeY;
    internal int lastuseMilliseconds;
}

public class CachedTextTexture
{
    internal TextStyle text;
    internal CachedTexture texture;
}

public class TextPart
{
    internal int color;
    internal string text;
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

    public void UseBlockTypes(IGamePlatform platform, Packet_BlockType[] blocktypes, int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (blocktypes[i] != null)
            {
                UseBlockType(platform, i, blocktypes[i]);
            }
        }
    }

    public void UseBlockType(IGamePlatform platform, int id, Packet_BlockType b)
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
        return one * p / 32;
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