/// <summary>
/// Interpolates network entity positions each frame for smooth remote player movement.
/// </summary>
public class ModInterpolatePositions : ModBase
{
    private const int ExtrapolationTimeMs = 300;
    private const int MinDelayMs = 100;

    public override void OnNewFrame(Game game, NewFrameEventArgs args)
    {
        InterpolatePositions(game, args.GetDt());
    }

    internal static void InterpolatePositions(Game game, float dt)
    {
        for (int i = 0; i < game.entitiesCount; i++)
        {
            Entity e = game.entities[i];
            if (e?.networkPosition == null) continue;
            if (i == game.LocalPlayerId) continue;
            if (!e.networkPosition.PositionLoaded) continue;

            e.playerDrawInfo ??= new PlayerDrawInfo();
            EnsureInterpolation(game, e);

            e.playerDrawInfo.interpolation.DELAYMILLISECONDS =
                Math.Max(MinDelayMs, game.ServerInfo.ServerPing.RoundtripTimeTotalMilliseconds());

            UpdateInterpolation(game, i, e);
        }
    }

    /// <summary>Initializes the network interpolation state for an entity if not already set up.</summary>
    private static void EnsureInterpolation(Game game, Entity e)
    {
        if (e.playerDrawInfo.interpolation != null) return;

        e.playerDrawInfo.interpolation = new NetworkInterpolation
        {
            req = new PlayerInterpolate { platform = game.platform },
            DELAYMILLISECONDS = 500,
            EXTRAPOLATE = false,
            EXTRAPOLATION_TIMEMILLISECONDS = ExtrapolationTimeMs
        };
    }

    private static void UpdateInterpolation(Game game, int entityId, Entity e)
    {
        PlayerDrawInfo info = e.playerDrawInfo;
        EntityPosition_ net = e.networkPosition;

        float netX = net.x, netY = net.y, netZ = net.z;

        // Feed a new state packet when network position or rotation has changed
        bool posChanged = !(netX == info.lastnetworkposX && netY == info.lastnetworkposY && netZ == info.lastnetworkposZ);
        bool rotChanged = net.rotx != info.lastnetworkrotx || net.roty != info.lastnetworkroty || net.rotz != info.lastnetworkrotz;

        if (posChanged || rotChanged)
        {
            info.interpolation.AddNetworkPacket(new PlayerInterpolationState
            {
                positionX = netX,
                positionY = netY,
                positionZ = netZ,
                rotx = net.rotx,
                roty = net.roty,
                rotz = net.rotz
            }, game.totaltimeMilliseconds);
        }

        PlayerInterpolationState cur =
            game.platform.CastToPlayerInterpolationState(info.interpolation.InterpolatedState(game.totaltimeMilliseconds))
            ?? new PlayerInterpolationState();

        // Bypass interpolation if the game world is controlling this entity's position
        if (Game.EnablePlayerUpdatePositionContainsKey(entityId) && !Game.EnablePlayerUpdatePosition(entityId))
        {
            cur.positionX = net.x;
            cur.positionY = net.y;
            cur.positionZ = net.z;
        }

        info.Velocity = new (
            cur.positionX - info.lastcurposX,
            cur.positionY - info.lastcurposY,
            cur.positionZ - info.lastcurposZ
        );
        info.moves = !(cur.positionX == info.lastcurposX && cur.positionY == info.lastcurposY && cur.positionZ == info.lastcurposZ);
        info.lastcurposX = cur.positionX;
        info.lastcurposY = cur.positionY;
        info.lastcurposZ = cur.positionZ;
        info.lastnetworkposX = netX; info.lastnetworkposY = netY; info.lastnetworkposZ = netZ;
        info.lastnetworkrotx = net.rotx; info.lastnetworkroty = net.roty; info.lastnetworkrotz = net.rotz;

        e.position.x = cur.positionX;
        e.position.y = cur.positionY;
        e.position.z = cur.positionZ;
        e.position.rotx = cur.rotx;
        e.position.roty = cur.roty;
        e.position.rotz = cur.rotz;
    }
}