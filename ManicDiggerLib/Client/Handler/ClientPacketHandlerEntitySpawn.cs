/// <summary>
/// Handles <see cref="Packet_ServerIdEnum.EntitySpawn"/> packets,
/// creating or updating entities and bootstrapping the local player on first spawn.
/// </summary>
public class ClientPacketHandlerEntitySpawn : ClientPacketHandler
{
    public override void Handle(Game game, Packet_Server packet)
    {
        int id = packet.EntitySpawn.Id;
        Entity entity = game.entities[id] ?? new Entity();

        ToClientEntity(game, packet.EntitySpawn.Entity_, entity,
            updatePosition: id != game.LocalPlayerId);

        game.entities[id] = entity;

        if (id == game.LocalPlayerId)
        {
            entity.networkPosition = null;
            game.player = entity;
            if (!game.spawned)
            {
                entity.scripts[entity.scriptsCount++] = new ScriptCharacterPhysics();
                game.MapLoaded();
                game.spawned = true;
            }
        }
    }

    /// <summary>Converts a heading/pitch encoded as 0–255 to radians.</summary>
    internal static float Angle256ToRad(int value) => value / 255f * MathF.PI * 2;

    /// <summary>
    /// Decodes a fixed-point <see cref="Packet_PositionAndOrientation"/> into an
    /// <see cref="EntityPosition_"/>. Used on the spawn path (cold) and by
    /// <see cref="ToClientEntity"/> when initialising a newly arrived entity.
    /// Position-update packets use in-place field mutation instead — see
    /// <see cref="ClientPacketHandlerEntityPosition"/>.
    /// </summary>
    public static EntityPosition_ ToClientEntityPosition(Packet_PositionAndOrientation pos)
    {
        return new EntityPosition_
        {
            x = pos.X / 32f,
            y = pos.Y / 32f,
            z = pos.Z / 32f,
            rotx = Angle256ToRad(pos.Pitch),
            roty = Angle256ToRad(pos.Heading),
        };
    }

    /// <summary>
    /// Applies all fields of a server entity descriptor onto an existing
    /// <paramref name="old"/> entity object, allocating sub-objects only when
    /// the corresponding server field is present.
    /// </summary>
    public static Entity ToClientEntity(Game game, Packet_ServerEntity entity, Entity old, bool updatePosition)
    {
        if (entity.Position != null && (old.position == null || updatePosition))
        {
            old.networkPosition = ToClientEntityPosition(entity.Position);
            old.networkPosition.PositionLoaded = true;
            old.networkPosition.LastUpdateMilliseconds = game.platform.TimeMillisecondsFromStart;
            old.position = ToClientEntityPosition(entity.Position);
        }

        if (entity.DrawModel != null)
        {
            old.drawModel = new EntityDrawModel
            {
                eyeHeight = game.DecodeFixedPoint(entity.DrawModel.EyeHeight),
                ModelHeight = game.DecodeFixedPoint(entity.DrawModel.ModelHeight),
                Texture_ = entity.DrawModel.Texture_,
                Model_ = entity.DrawModel.Model_ ?? "player.txt",
                DownloadSkin = entity.DrawModel.DownloadSkin != 0,
            };
        }

        if (entity.DrawName_ != null)
        {
            string name = entity.DrawName_.Color != null
                ? string.Format("{0}{1}", entity.DrawName_.Color, entity.DrawName_.Name)
                : entity.DrawName_.Name;

            if (!name.StartsWith("&", StringComparison.InvariantCultureIgnoreCase))
                name = string.Format("&f{0}", name);

            old.drawName = new DrawName
            {
                Name = name,
                OnlyWhenSelected = entity.DrawName_.OnlyWhenSelected,
                ClientAutoComplete = entity.DrawName_.ClientAutoComplete,
            };
        }

        if (entity.DrawText != null)
        {
            old.drawText = new EntityDrawText
            {
                text = entity.DrawText.Text,
                dx = entity.DrawText.Dx / 32f,
                dy = entity.DrawText.Dy / 32f,
                dz = entity.DrawText.Dz / 32f,
            };
        }
        else
        {
            old.drawText = null;
        }

        old.push = entity.Push != null
            ? new Packet_ServerExplosion { RangeFloat = entity.Push.RangeFloat }
            : null;

        old.usable = entity.Usable;

        old.drawArea = entity.DrawArea != null
            ? new EntityDrawArea
            {
                x = entity.DrawArea.X,
                y = entity.DrawArea.Y,
                z = entity.DrawArea.Z,
                sizex = entity.DrawArea.Sizex,
                sizey = entity.DrawArea.Sizey,
                sizez = entity.DrawArea.Sizez,
            }
            : null;

        return old;
    }
}