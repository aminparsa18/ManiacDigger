using OpenTK.Mathematics;

/// <summary>
/// Removes entities when their lifetime expires, triggering grenade explosions where applicable.
/// </summary>
public class ModExpire : ModBase
{
    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        float dt = args.GetDt();
        for (int i = 0; i < game.entitiesCount; i++)
        {
            Entity entity = game.entities[i];
            if (entity?.expires == null) continue;

            entity.expires.timeLeft -= dt;
            if (entity.expires.timeLeft > 0) continue;

            if (entity.grenade != null)
                GrenadeExplosion(game, i);

            game.entities[i] = null;
        }
    }

    private static void GrenadeExplosion(Game game, int grenadeEntityId)
    {
        Entity grenadeEntity = game.entities[grenadeEntityId];
        Sprite sprite = grenadeEntity.sprite;
        Grenade_ grenade = grenadeEntity.grenade;
        var blockType = game.blocktypes[grenade.block];

        float posX = sprite.positionX;
        float posY = sprite.positionY;
        float posZ = sprite.positionZ;

        game.PlayAudioAt("grenadeexplosion.ogg", posX, posY, posZ);

        // Spawn explosion animation sprite
        game.EntityAddLocal(new Entity
        {
            sprite = new Sprite
            {
                image = "ani5.png",
                positionX = posX,
                positionY = posY + 1,
                positionZ = posZ,
                size = 200,
                animationcount = 4
            },
            expires = Expires.Create(1)
        });

        // Spawn explosion push entity
        float explosionTime = game.DecodeFixedPoint(blockType.ExplosionTimeFloat);
        float explosionRange = game.DecodeFixedPoint(blockType.ExplosionRangeFloat);

        game.EntityAddLocal(new Entity
        {
            push = new Packet_ServerExplosion
            {
                XFloat = Game.EncodeFixedPoint(posX),
                YFloat = Game.EncodeFixedPoint(posZ),
                ZFloat = Game.EncodeFixedPoint(posY),
                RangeFloat = blockType.ExplosionRangeFloat,
                IsRelativeToPlayerPosition = 0,
                TimeFloat = blockType.ExplosionTimeFloat
            },
            expires = new Expires { timeLeft = explosionTime }
        });

        // Apply damage to local player based on distance
        float dist = Vector3.Distance(new Vector3(game.player.position.x, game.player.position.y, game.player.position.z), new Vector3(posX, posY, posZ));
        float dmg = (1f - dist / explosionRange) * game.DecodeFixedPoint(blockType.DamageBodyFloat);
        if (dmg > 0)
            game.ApplyDamageToPlayer((int)dmg, Packet_DeathReasonEnum.Explosion, grenade.sourcePlayer);
    }
}