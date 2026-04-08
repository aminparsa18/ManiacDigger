using System.Numerics;


/// <summary>
/// Accumulates push forces from nearby explosion/push entities onto the local player each fixed frame.
/// </summary>
public class ModPush : ModBase
{
    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        game.pushX = 0;
        game.pushY = 0;
        game.pushZ = 0;

        float pX = game.player.position.x;
        float pY = game.player.position.y;
        float pZ = game.player.position.z;

        for (int i = 0; i < game.entitiesCount; i++)
        {
            Entity entity = game.entities[i];
            if (entity?.push == null) continue;
            if (entity.networkPosition != null && !entity.networkPosition.PositionLoaded) continue;

            float kX = game.DecodeFixedPoint(entity.push.XFloat);
            float kY = game.DecodeFixedPoint(entity.push.ZFloat);
            float kZ = game.DecodeFixedPoint(entity.push.YFloat);

            if (entity.push.IsRelativeToPlayerPosition != 0)
            {
                kX += pX;
                kY += pY;
                kZ += pZ;
            }

            if (Vector3.Distance(new Vector3(kX, kY, kZ), new Vector3(pX, pY, pZ)) < game.DecodeFixedPoint(entity.push.RangeFloat))
            {
                game.pushX += pX - kX;
                game.pushY += pY - kY;
                game.pushZ += pZ - kZ;
            }
        }
    }
}