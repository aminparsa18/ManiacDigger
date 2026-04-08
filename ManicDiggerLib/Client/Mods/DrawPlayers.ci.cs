using OpenTK.Mathematics;
using System.Text;

/// <summary>
/// Renders animated player/entity models in the 3D world each frame.
/// </summary>
public class ModDrawPlayers : ModBase
{
    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        game.totaltimeMilliseconds = game.platform.TimeMillisecondsFromStart();

        for (int i = 0; i < game.entitiesCount; i++)
        {
            Entity p = game.entities[i];
            if (p?.drawModel == null) continue;
            if (i == game.LocalPlayerId && !game.ENABLE_TPP_VIEW) continue;
            if (p.networkPosition != null && !p.networkPosition.PositionLoaded) continue;
            if (!game.d_FrustumCulling.SphereInFrustum(p.position.x, p.position.y, p.position.z, 3)) continue;
            if (p.drawModel.CurrentTexture == -1) continue;

            int cx = (int)p.position.x / Game.chunksize;
            int cy = (int)p.position.z / Game.chunksize;
            int cz = (int)p.position.y / Game.chunksize;
            if (game.map.IsValidChunkPos(cx, cy, cz) && !game.map.IsChunkRendered(cx, cy, cz)) continue;

            p.playerDrawInfo ??= new PlayerDrawInfo();

            float shadow = (float)game.GetLight((int)p.position.x, (int)p.position.z, (int)p.position.y) / Game.maxlight;
            float speed = i == game.LocalPlayerId ? GetLocalPlayerSpeed(game) : GetNetworkPlayerSpeed(game, p, deltaTime);

            EnsureRenderer(game, p);
            DrawEntity(game, p, deltaTime, shadow, speed);
        }
    }

    /// <summary>Calculates movement speed for the local player based on physics velocity.</summary>
    private static float GetLocalPlayerSpeed(Game game)
    {
        game.player.playerDrawInfo ??= new PlayerDrawInfo();

        float speed = new Vector3(
            game.playervelocity.X / 60f,
            game.playervelocity.Y / 60f,
            game.playervelocity.Z / 60f).Length * 1.5f;

        game.player.playerDrawInfo.moves = speed != 0;
        return speed;
    }

    /// <summary>Calculates movement speed for a network entity based on interpolated velocity.</summary>
    private static float GetNetworkPlayerSpeed(Game game, Entity p, float dt)
    {
        return p.playerDrawInfo.Velocity.Length / dt * 0.04f;
    }

    /// <summary>Loads and initializes the animated model renderer for an entity if not already done.</summary>
    private static void EnsureRenderer(Game game, Entity p)
    {
        if (p.drawModel.renderer != null) return;

        p.drawModel.renderer = new AnimatedModelRenderer();
        byte[] data = game.GetAssetFile(p.drawModel.Model_);
        int dataLength = game.GetAssetFileLength(p.drawModel.Model_);
        if (data == null) return;

        string dataString = Encoding.UTF8.GetString(data, 0, dataLength);
        AnimatedModel model = AnimatedModelSerializer.Deserialize(dataString);
        p.drawModel.renderer.Start(game, model);
    }

    /// <summary>Renders the entity's animated model at its current world position and orientation.</summary>
    private static void DrawEntity(Game game, Entity p, float dt, float shadow, float speed)
    {
        game.GLPushMatrix();
        game.GLTranslate(p.position.x, p.position.y, p.position.z);
        game.GLRotate(float.RadiansToDegrees(-p.position.roty + MathF.PI), 0, 1, 0);
        game.platform.BindTexture2d(p.drawModel.CurrentTexture);
        p.drawModel.renderer.Render(dt, float.RadiansToDegrees(p.position.rotx + MathF.PI), true, p.playerDrawInfo.moves, shadow);
        game.GLPopMatrix();
    }
}