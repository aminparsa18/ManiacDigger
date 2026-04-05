public class ModInterpolatePositions : ClientMod
{
    public override void OnNewFrame(Game game, NewFrameEventArgs args)
    {
        InterpolatePositions(game, args.GetDt());
    }
    internal static void InterpolatePositions(Game game, float dt)
    {
        for (int i = 0; i < game.entitiesCount; i++)
        {
            Entity e = game.entities[i];
            if (e == null) { continue; }
            if (e.networkPosition == null) { continue; }
            if (i == game.LocalPlayerId) { continue; }
            if (!e.networkPosition.PositionLoaded) { continue; }

            if (e.playerDrawInfo == null)
            {
                e.playerDrawInfo = new PlayerDrawInfo();
            }
            if(e.playerDrawInfo.interpolation==null)
            {
                NetworkInterpolation n = new();
                PlayerInterpolate playerInterpolate = new()
                {
                    platform = game.platform
                };
                n.req = playerInterpolate;
                n.DELAYMILLISECONDS = 500;
                n.EXTRAPOLATE = false;
                n.EXTRAPOLATION_TIMEMILLISECONDS = 300;
                e.playerDrawInfo.interpolation = n;
            }
            e.playerDrawInfo.interpolation.DELAYMILLISECONDS = MathCi.MaxInt(100, game.ServerInfo.ServerPing.RoundtripTimeTotalMilliseconds());
            Entity p = e;

            PlayerDrawInfo info = p.playerDrawInfo;
            float networkposX = p.networkPosition.x;
            float networkposY = p.networkPosition.y;
            float networkposZ = p.networkPosition.z;
            if ((!Game.Vec3Equal(networkposX, networkposY, networkposZ,
                            info.lastnetworkposX, info.lastnetworkposY, info.lastnetworkposZ))
                || p.networkPosition.rotx != info.lastnetworkrotx
                || p.networkPosition.roty != info.lastnetworkroty
                || p.networkPosition.rotz != info.lastnetworkrotz)
            {
                PlayerInterpolationState state = new()
                {
                    positionX = networkposX,
                    positionY = networkposY,
                    positionZ = networkposZ,
                    rotx = p.networkPosition.rotx,
                    roty = p.networkPosition.roty,
                    rotz = p.networkPosition.rotz
                };
                info.interpolation.AddNetworkPacket(state, game.totaltimeMilliseconds);
            }
            PlayerInterpolationState curstate = game.platform.CastToPlayerInterpolationState(info.interpolation.InterpolatedState(game.totaltimeMilliseconds));
            if (curstate == null)
            {
                curstate = new PlayerInterpolationState();
            }
            //do not interpolate player position if player is controlled by game world
            if (Game.EnablePlayerUpdatePositionContainsKey(i) && !Game.EnablePlayerUpdatePosition(i))
            {
                curstate.positionX = p.networkPosition.x;
                curstate.positionY = p.networkPosition.y;
                curstate.positionZ = p.networkPosition.z;
            }
            float curposX = curstate.positionX;
            float curposY = curstate.positionY;
            float curposZ = curstate.positionZ;
            info.velocityX = curposX - info.lastcurposX;
            info.velocityY = curposY - info.lastcurposY;
            info.velocityZ = curposZ - info.lastcurposZ;
            info.moves = (!Game.Vec3Equal(curposX, curposY, curposZ, info.lastcurposX, info.lastcurposY, info.lastcurposZ));
            info.lastcurposX = curposX;
            info.lastcurposY = curposY;
            info.lastcurposZ = curposZ;
            info.lastnetworkposX = networkposX;
            info.lastnetworkposY = networkposY;
            info.lastnetworkposZ = networkposZ;
            info.lastnetworkrotx = p.networkPosition.rotx;
            info.lastnetworkroty = p.networkPosition.roty;
            info.lastnetworkrotz = p.networkPosition.rotz;

            p.position.x = curposX;
            p.position.y = curposY;
            p.position.z = curposZ;
            p.position.rotx = curstate.rotx;
            p.position.roty = curstate.roty;
            p.position.rotz = curstate.rotz;
        }
    }
}
