using OpenTK.Mathematics;

/// <summary>
/// Updates grenade physics including gravity, movement, and collision bounce each fixed frame.
/// </summary>
public class ModGrenade : ModBase
{
    private const float ProjectileGravity = 20f;
    private const float BounceSpeedMultiply = 0.5f;
    private const float WallDistance = 0.3f;

    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        float dt = args.GetDt();
        for (int i = 0; i < game.entitiesCount; i++)
        {
            Entity entity = game.entities[i];
            if (entity?.grenade == null) continue;
            UpdateGrenade(game, i, dt);
        }
    }

    internal void UpdateGrenade(Game game, int grenadeEntityId, float dt)
    {
        Entity grenadeEntity = game.entities[grenadeEntityId];
        Sprite grenadeSprite = grenadeEntity.sprite;
        Grenade_ grenade = grenadeEntity.grenade;

        Vector3 oldPos = new(grenadeSprite.positionX, grenadeSprite.positionY, grenadeSprite.positionZ);
        Vector3 newPos = oldPos + new Vector3(grenade.velocityX, grenade.velocityY, grenade.velocityZ) * dt;
        grenade.velocityY -= ProjectileGravity * dt;

        Vector3 velocity = new(grenade.velocityX, grenade.velocityY, grenade.velocityZ);
        Vector3 finalPos = GrenadeBounce(game, oldPos, newPos, ref velocity, dt);

        grenade.velocityX = velocity.X;
        grenade.velocityY = velocity.Y;
        grenade.velocityZ = velocity.Z;
        grenadeSprite.positionX = finalPos.X;
        grenadeSprite.positionY = finalPos.Y;
        grenadeSprite.positionZ = finalPos.Z;
    }

    internal Vector3 GrenadeBounce(Game game, Vector3 oldPos, Vector3 newPos, ref Vector3 velocity, float dt)
    {
        bool isMoving = velocity.Length > 100 * dt;

        oldPos.Y += WallDistance;
        newPos.Y += WallDistance;

        Vector3 pos = newPos;

        // Left (+Z)
        if (newPos.Z > oldPos.Z)
            TryBounceAxis(game, newPos, new Vector3(0, 0, WallDistance), ref velocity, ref pos, isMoving, axis: 2);

        // Right (-Z)
        if (newPos.Z < oldPos.Z)
            TryBounceAxis(game, newPos, new Vector3(0, 0, -WallDistance), ref velocity, ref pos, isMoving, axis: 2);

        // Front (+X)
        if (newPos.X > oldPos.X)
            TryBounceAxis(game, newPos, new Vector3(WallDistance, 0, 0), ref velocity, ref pos, isMoving, axis: 0);

        // Back (-X)
        if (newPos.X < oldPos.X)
            TryBounceAxis(game, newPos, new Vector3(-WallDistance, 0, 0), ref velocity, ref pos, isMoving, axis: 0);

        // Bottom (falling down)
        if (newPos.Y < oldPos.Y)
            TryBounceFloor(game, newPos, oldPos, ref velocity, ref pos, isMoving);

        // Top (moving up)
        if (newPos.Y > oldPos.Y)
            TryBounceCeiling(game, newPos, ref velocity, ref pos, isMoving);

        pos.Y -= WallDistance;
        return pos;
    }

    /// <summary>Checks and applies a bounce for X or Z axis wall collisions.</summary>
    private static void TryBounceAxis(Game game, Vector3 newPos, Vector3 offset, ref Vector3 velocity, ref Vector3 pos, bool isMoving, int axis)
    {
        Vector3 probe = newPos + offset;
        int px = (int)MathF.Floor(probe.X);
        int py = (int)MathF.Floor(probe.Z);
        int pz = (int)MathF.Floor(probe.Y);

        bool empty = game.IsTileEmptyForPhysics(px, py, pz)
                  && game.IsTileEmptyForPhysics(px, py, pz + 1);
        if (empty) return;

        velocity[axis] = -velocity[axis];
        ApplyBounce(game, ref velocity, newPos, isMoving);
    }

    /// <summary>Checks and applies a bounce when the grenade hits a floor (moving down).</summary>
    private static void TryBounceFloor(Game game, Vector3 newPos, Vector3 oldPos, ref Vector3 velocity, ref Vector3 pos, bool isMoving)
    {
        float a = WallDistance;
        Vector3 probe = new(newPos.X, newPos.Y - WallDistance, newPos.Z);
        int x = (int)MathF.Floor(probe.X);
        int y = (int)MathF.Floor(probe.Z);
        int z = (int)MathF.Floor(probe.Y);

        float fracX = probe.X - x;
        float fracZ = probe.Z - y;

        bool full = !game.IsTileEmptyForPhysics(x, y, z)
            || (fracX <= a && !game.IsTileEmptyForPhysics(x - 1, y, z) && game.IsTileEmptyForPhysics(x - 1, y, z + 1))
            || (fracX >= 1 - a && !game.IsTileEmptyForPhysics(x + 1, y, z) && game.IsTileEmptyForPhysics(x + 1, y, z + 1))
            || (fracZ <= a && !game.IsTileEmptyForPhysics(x, y - 1, z) && game.IsTileEmptyForPhysics(x, y - 1, z + 1))
            || (fracZ >= 1 - a && !game.IsTileEmptyForPhysics(x, y + 1, z) && game.IsTileEmptyForPhysics(x, y + 1, z + 1));

        if (!full) return;
        velocity.Y = -velocity.Y;
        ApplyBounce(game, ref velocity, newPos, isMoving);
    }

    /// <summary>Checks and applies a bounce when the grenade hits a ceiling (moving up).</summary>
    private void TryBounceCeiling(Game game, Vector3 newPos, ref Vector3 velocity, ref Vector3 pos, bool isMoving)
    {
        Vector3 probe = new(newPos.X, newPos.Y + WallDistance, newPos.Z);
        bool empty = game.IsTileEmptyForPhysics(
            (int)MathF.Floor(probe.X),
            (int)MathF.Floor(probe.Z),
            (int)MathF.Floor(probe.Y));

        if (empty) return;
        velocity.Y = -velocity.Y;
        ApplyBounce(game, ref velocity, newPos, isMoving);
    }

    /// <summary>Applies bounce speed damping and plays bounce sound if the grenade is moving.</summary>
    private static void ApplyBounce(Game game, ref Vector3 velocity, Vector3 pos, bool isMoving)
    {
        velocity *= BounceSpeedMultiply;
        if (isMoving)
            game.PlayAudioAt("grenadebounce.ogg", pos.X, pos.Y, pos.Z);
    }
}