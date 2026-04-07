public class ModExpire : ClientMod
{
    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        for (int i = 0; i < game.entitiesCount; i++)
        {
            Entity entity = game.entities[i];
            if (entity == null) { continue; }
            if (entity.expires == null) { continue; }
            entity.expires.timeLeft -= args.GetDt();
            if (entity.expires.timeLeft <= 0)
            {
                if (entity.grenade != null)
                {
                    GrenadeExplosion(game, i);
                }
                game.entities[i] = null;
            }
        }
    }

    private static void GrenadeExplosion(Game game, int grenadeEntityId)
    {
        float LocalPlayerPositionX = game.player.position.x;
        float LocalPlayerPositionY = game.player.position.y;
        float LocalPlayerPositionZ = game.player.position.z;

        Entity grenadeEntity = game.entities[grenadeEntityId];
        Sprite grenadeSprite = grenadeEntity.sprite;
        Grenade_ grenade = grenadeEntity.grenade;

        game.AudioPlayAt("grenadeexplosion.ogg", grenadeSprite.positionX, grenadeSprite.positionY, grenadeSprite.positionZ);

        {
            Entity entity = new();

            Sprite spritenew = new()
            {
                image = "ani5.png",
                positionX = grenadeSprite.positionX,
                positionY = grenadeSprite.positionY + 1,
                positionZ = grenadeSprite.positionZ,
                size = 200,
                animationcount = 4
            };

            entity.sprite = spritenew;
            entity.expires = Expires.Create(1);
            game.EntityAddLocal(entity);
        }

        {
            Packet_ServerExplosion explosion = new()
            {
                XFloat = game.SerializeFloat(grenadeSprite.positionX),
                YFloat = game.SerializeFloat(grenadeSprite.positionZ),
                ZFloat = game.SerializeFloat(grenadeSprite.positionY),
                RangeFloat = game.blocktypes[grenade.block].ExplosionRangeFloat,
                IsRelativeToPlayerPosition = 0,
                TimeFloat = game.blocktypes[grenade.block].ExplosionTimeFloat
            };

            Entity entity = new()
            {
                push = explosion,
                expires = new Expires
                {
                    timeLeft = game.DeserializeFloat(game.blocktypes[grenade.block].ExplosionTimeFloat)
                }
            };
            game.EntityAddLocal(entity);
        }

        float dist = game.Dist(LocalPlayerPositionX, LocalPlayerPositionY, LocalPlayerPositionZ, grenadeSprite.positionX, grenadeSprite.positionY, grenadeSprite.positionZ);
        float dmg = (1 - dist / game.DeserializeFloat(game.blocktypes[grenade.block].ExplosionRangeFloat)) * game.DeserializeFloat(game.blocktypes[grenade.block].DamageBodyFloat);
        if (dmg > 0)
        {
            game.ApplyDamageToPlayer((int)(dmg), Packet_DeathReasonEnum.Explosion, grenade.sourcePlayer);
        }
    }
}
