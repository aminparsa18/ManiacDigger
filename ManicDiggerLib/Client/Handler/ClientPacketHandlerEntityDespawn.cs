/// <summary>
/// Handles <see cref="Packet_ServerIdEnum.EntityDespawn"/> packets,
/// cleaning up downloaded player skin textures before removing the entity.
/// </summary>
public class ClientPacketHandlerEntityDespawn : ClientPacketHandler
{
    public override void Handle(Game game, Packet_Server packet)
    {
        int id = packet.EntityDespawn.Id;
        Entity entity = game.entities[id];

        // Clean up a downloaded player skin if one was loaded for this entity.
        if (entity?.drawModel?.DownloadSkin == true)
        {
            int currentTex = entity.drawModel.CurrentTexture;
            if (currentTex > 0 && currentTex != game.GetTexture("mineplayer.png"))
            {
                entity.drawModel.CurrentTexture = -1;
                game.DeleteTexture(entity.drawName.Name);
            }
        }

        game.entities[id] = null;
    }
}