/// <summary>
/// Handles <see cref="Packet_ServerIdEnum.EntityPosition"/> packets,
/// updating networked entity positions and orientations each time the server
/// sends a movement update.
/// </summary>
public class ClientPacketHandlerEntityPosition : ClientPacketHandler
{
    public override void Handle(Game game, Packet_Server packet)
    {
        int id = packet.EntityPosition.Id;
        Entity entity = game.entities[id];
        Packet_PositionAndOrientation raw = packet.EntityPosition.PositionAndOrientation;

        if (id == game.LocalPlayerId)
        {
            // Local player: apply as an authoritative teleport directly onto
            // player.position. No EntityPosition_ object is needed here.
            game.player.position.x = raw.X / 32f;
            game.player.position.y = raw.Y / 32f;
            game.player.position.z = raw.Z / 32f;
            game.player.position.rotx = ClientPacketHandlerEntitySpawn.Angle256ToRad(raw.Pitch);
            game.player.position.roty = ClientPacketHandlerEntitySpawn.Angle256ToRad(raw.Heading);
            entity.networkPosition = null;
        }
        else if (entity.push != null)
        {
            // Entity has a push force — forward raw fixed-point coords directly.
            entity.push.XFloat = raw.X;
            entity.push.YFloat = raw.Z;
            entity.push.ZFloat = raw.Y;
        }
        else
        {
            // ── Update networkPosition in-place ───────────────────────────────
            // Previously called ToClientEntityPosition() which allocated a new
            // EntityPosition_ object on every packet. Position updates arrive at
            // network rate for every visible entity — this was the hottest
            // allocation in the entity system.
            // We allocate once (lazily) and overwrite fields on every update.
            entity.networkPosition ??= new EntityPosition_();
            entity.networkPosition.x = raw.X / 32f;
            entity.networkPosition.y = raw.Y / 32f;
            entity.networkPosition.z = raw.Z / 32f;
            entity.networkPosition.rotx = ClientPacketHandlerEntitySpawn.Angle256ToRad(raw.Pitch);
            entity.networkPosition.roty = ClientPacketHandlerEntitySpawn.Angle256ToRad(raw.Heading);
            entity.networkPosition.PositionLoaded = true;
            entity.networkPosition.LastUpdateMilliseconds = game.platform.TimeMillisecondsFromStart;
        }
    }
}