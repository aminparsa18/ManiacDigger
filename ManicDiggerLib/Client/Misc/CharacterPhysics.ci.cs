using ManicDigger;
using OpenTK.Mathematics;

/// <summary>
/// Defines the per-frame fixed-timestep update contract for entity behaviour scripts.
/// </summary>
public interface IEntityScript
{
    /// <summary>
    /// Called once per fixed physics tick for the given <paramref name="entity"/>.
    /// </summary>
    void OnNewFrameFixed(Game game, int entity, float dt);
}

/// <summary>
/// Implements walking, jumping, swimming, and collision-response physics for the local player.
/// Runs each fixed timestep via <see cref="IEntityScript.OnNewFrameFixed"/>.
/// </summary>
public class ScriptCharacterPhysics : IEntityScript
{
    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    public ScriptCharacterPhysics()
    {
        movedz = 0;
        curspeed = new Vector3();
        jumpacceleration = 0;
        isplayeronground = false;
        acceleration = new Acceleration();
        jumpstartacceleration = 0;
        jumpstartaccelerationhalf = 0;
        movespeednow = 0;

        tmpPlayerPosition = Vector3.Zero;

        constGravity = 0.3f;
        constWaterGravityMultiplier = 3;
        constEnableAcceleration = true;
        constJump = 2.1f;
    }

    // -------------------------------------------------------------------------
    // Dependencies
    // -------------------------------------------------------------------------

    /// <summary>Reference to the active game instance, assigned at the start of each tick.</summary>
    internal Game game;

    // -------------------------------------------------------------------------
    // Per-frame physics state
    // -------------------------------------------------------------------------

    /// <summary>Current vertical velocity component (positive = upward). Modified by gravity and jumps each tick.</summary>
    internal float movedz;

    /// <summary>Current XYZ movement speed vector, attenuated by <see cref="acceleration"/> each tick.</summary>
    internal Vector3 curspeed;

    /// <summary>Remaining upward acceleration from an in-progress jump, halved each tick until exhausted.</summary>
    internal float jumpacceleration;

    /// <summary>True when the player is standing on solid ground this tick.</summary>
    internal bool isplayeronground;

    /// <summary>Tunable acceleration parameters (drag, ramp-up, max-force) for the current tick.</summary>
    internal Acceleration acceleration;

    /// <summary>Full-height jump initial acceleration, derived from <see cref="constGravity"/> each tick.</summary>
    internal float jumpstartacceleration;

    /// <summary>Half-height jump initial acceleration (crouch-jump / swim surfacing), derived from <see cref="constGravity"/> each tick.</summary>
    internal float jumpstartaccelerationhalf;

    /// <summary>Effective movement speed this tick, sourced from <see cref="Game.MoveSpeedNow"/>.</summary>
    internal float movespeednow;

    // -------------------------------------------------------------------------
    // Tunable constants
    // -------------------------------------------------------------------------

    /// <summary>Base gravitational acceleration applied each tick. Increase to make the player fall faster.</summary>
    internal float constGravity;

    /// <summary>
    /// Factor by which gravity is multiplied while swimming.
    /// Higher values make the player sink faster in water.
    /// </summary>
    internal float constWaterGravityMultiplier;

    /// <summary>When false, <see cref="curspeed"/> is set directly from input rather than being ramped via <see cref="acceleration"/>.</summary>
    internal bool constEnableAcceleration;

    /// <summary>Multiplier applied to <see cref="jumpacceleration"/> each tick to produce upward displacement.</summary>
    internal float constJump;

    // -------------------------------------------------------------------------
    // IEntityScript
    // -------------------------------------------------------------------------

    /// <summary>
    /// Entry point called by the entity system each fixed timestep.
    /// Reads player input, resolves move speed, and delegates to <see cref="Update"/>.
    /// Does nothing while the map-loading screen is active.
    /// </summary>
    public void OnNewFrameFixed(Game game_, int entity, float dt)
    {
        game = game_;
        if (game.guistate == GuiState.MapLoading)
        {
            return;
        }
        movespeednow = game.MoveSpeedNow();
        game.controls.movedx = Math.Clamp(game.controls.movedx, -1, 1);
        game.controls.movedy = Math.Clamp(game.controls.movedy, -1, 1);
        Controls move = game.controls;
        jumpstartacceleration = 13.333f * constGravity; // default
        jumpstartaccelerationhalf = 9 * constGravity;
        acceleration.SetDefault();
        game.soundnow = new bool();
        if (game.FollowId() != null && game.FollowId() == game.LocalPlayerId)
        {
            move.movedx = 0;
            move.movedy = 0;
            move.moveup = false;
            move.wantsjump = false;
        }
        Update(game.player.position, move, dt, out game.soundnow, new Vector3(game.pushX, game.pushY, game.pushZ), game.entities[game.LocalPlayerId].drawModel.ModelHeight);
    }

    // -------------------------------------------------------------------------
    // Core physics update
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies gravity, input forces, acceleration, collision response, and jump logic
    /// to <paramref name="stateplayerposition"/> for a single fixed timestep.
    /// </summary>
    /// <param name="stateplayerposition">Player position mutated in-place.</param>
    /// <param name="move">Input snapshot for this tick.</param>
    /// <param name="dt">Fixed timestep duration in seconds.</param>
    /// <param name="soundnow">Set to true when the player initiates a jump (footstep sound cue).</param>
    /// <param name="push">External impulse vector (e.g. explosions, conveyors).</param>
    /// <param name="modelheight">Entity model height in blocks; values ≥ 2 enable tall-player collision.</param>
    public void Update(EntityPosition_ stateplayerposition, Controls move, float dt, out bool soundnow, Vector3 push, float modelheight)
    {
        if (game.stopPlayerMove)
        {
            movedz = 0;
            game.stopPlayerMove = false;
        }

        // Reduce air control: high drag + low ramp-up while airborne.
        if (!isplayeronground)
        {
            acceleration.acceleration1 = 0.99f;
            acceleration.acceleration2 = 0.2f;
            acceleration.acceleration3 = 70;
        }

        // Trampoline block: force a super-jump when landing on it.
        {
            int blockunderplayer = game.BlockUnderPlayer();
            if (blockunderplayer != -1 && blockunderplayer == game.d_Data.BlockIdTrampoline
                && (!isplayeronground) && !game.controls.shiftkeydown)
            {
                game.controls.wantsjump = true;
                jumpstartacceleration = 20.666f * constGravity;
            }
        }

        // Ice and water share the same slippery acceleration profile.
        {
            int blockunderplayer = game.BlockUnderPlayer();
            if ((blockunderplayer != -1 && game.d_Data.IsSlipperyWalk[blockunderplayer]) || game.SwimmingBody())
            {
                acceleration.acceleration1 = 0.99f;
                acceleration.acceleration2 = 0.2f;
                acceleration.acceleration3 = 70;
            }
        }

        soundnow = false;

        // Convert input axes from local-player space to world space.
        Vector3 diff1 = new();
        VectorUtils.ToVectorInFixedSystem(
            move.movedx * movespeednow * dt,
            0,
            move.movedy * movespeednow * dt, stateplayerposition.rotx, stateplayerposition.roty, ref diff1);

        // Apply normalised external push.
        if (push.Length > 0.01f)
        {
            Vector3.Normalize(in push, out push);
            push.X *= 5;
            push.Y *= 5;
            push.Z *= 5;
        }
        diff1.X += push.X * dt;
        diff1.Y += push.Y * dt;
        diff1.Z += push.Z * dt;

        // Gravity is only applied once the chunk under the player has been received from the server.
        bool loaded = false;
        int cx = (int)(game.player.position.x / Game.chunksize);
        int cy = (int)(game.player.position.z / Game.chunksize);
        int cz = (int)(game.player.position.y / Game.chunksize);
        if (game.VoxelMap.IsValidChunkPos(cx, cy, cz))
        {
            if (game.VoxelMap.chunks[VectorIndexUtil.Index3d(cx, cy, cz,
                game.VoxelMap.MapSizeX / Game.chunksize,
                game.VoxelMap.MapSizeY / Game.chunksize)] != null)
            {
                loaded = true;
            }
        }
        else
        {
            loaded = true;
        }

        if ((!move.freemove) && loaded)
        {
            if (!game.SwimmingBody())
            {
                movedz += -constGravity;
            }
            else
            {
                movedz += -constGravity * constWaterGravityMultiplier;
            }
        }
        game.movedz = movedz;

        if (constEnableAcceleration)
        {
            // Drag
            curspeed.X *= acceleration.acceleration1;
            curspeed.Y *= acceleration.acceleration1;
            curspeed.Z *= acceleration.acceleration1;
            // Friction (independent of frame rate)
            curspeed.X = MakeCloserToZero(curspeed.X, acceleration.acceleration2 * dt);
            curspeed.Y = MakeCloserToZero(curspeed.Y, acceleration.acceleration2 * dt);
            curspeed.Z = MakeCloserToZero(curspeed.Z, acceleration.acceleration2 * dt);
            // Fly / swim vertical input
            diff1.Y += move.moveup ? 2 * movespeednow * dt : 0;
            diff1.Y -= move.movedown ? 2 * movespeednow * dt : 0;
            // Force ramp-up
            curspeed.X += diff1.X * acceleration.acceleration3 * dt;
            curspeed.Y += diff1.Y * acceleration.acceleration3 * dt;
            curspeed.Z += diff1.Z * acceleration.acceleration3 * dt;
            // Clamp to max speed
            if (curspeed.Length > movespeednow)
            {
                Vector3.Normalize(in curspeed, out curspeed);
                curspeed.X *= movespeednow;
                curspeed.Y *= movespeednow;
                curspeed.Z *= movespeednow;
            }
        }
        else
        {
            // Instant response: no ramp-up.
            if (diff1.Length > 0)
            {
                Vector3.Normalize(in diff1, out diff1);
            }
            curspeed.X = diff1.X * movespeednow;
            curspeed.Y = diff1.Y * movespeednow;
            curspeed.Z = diff1.Z * movespeednow;
        }

        Vector3 newposition = Vector3.Zero;
        if (!move.freemove)
        {
            newposition.X = stateplayerposition.x + curspeed.X;
            newposition.Y = stateplayerposition.y + curspeed.Y;
            newposition.Z = stateplayerposition.z + curspeed.Z;
            // Horizontal-only movement when not swimming (Y handled by movedz).
            if (!game.SwimmingBody())
            {
                newposition.Y = stateplayerposition.y;
            }
            // Re-normalise horizontal displacement then scale by actual speed.
            float diffx = newposition.X - stateplayerposition.x;
            float diffy = newposition.Y - stateplayerposition.y;
            float diffz = newposition.Z - stateplayerposition.z;
            float difflength = new Vector3(diffx, diffy, diffz).Length;
            if (difflength > 0)
            {
                diffx /= difflength;
                diffy /= difflength;
                diffz /= difflength;
                diffx *= curspeed.Length;
                diffy *= curspeed.Length;
                diffz *= curspeed.Length;
            }
            newposition.X = stateplayerposition.x + diffx * dt;
            newposition.Y = stateplayerposition.y + diffy * dt;
            newposition.Z = stateplayerposition.z + diffz * dt;
        }
        else
        {
            newposition.X = stateplayerposition.x + curspeed.X * dt;
            newposition.Y = stateplayerposition.y + curspeed.Y * dt;
            newposition.Z = stateplayerposition.z + curspeed.Z * dt;
        }

        // Apply accumulated vertical velocity (gravity + jump).
        newposition.Y += movedz * dt;

        if (!move.noclip)
        {
            var v = WallSlide(
                new Vector3(stateplayerposition.x, stateplayerposition.y, stateplayerposition.z),
                newposition,
                modelheight);
            stateplayerposition.x = v.X;
            stateplayerposition.y = v.Y;
            stateplayerposition.z = v.Z;
        }
        else
        {
            stateplayerposition.x = newposition.X;
            stateplayerposition.y = newposition.Y;
            stateplayerposition.z = newposition.Z;
        }

        if (!move.freemove)
        {
            if (isplayeronground || game.SwimmingBody())
            {
                jumpacceleration = 0;
                movedz = 0;
            }
            if ((move.wantsjump || move.wantsjumphalf) && ((jumpacceleration == 0 && isplayeronground) || game.SwimmingBody()) && loaded && (!game.SwimmingEyes()))
            {
                jumpacceleration = move.wantsjumphalf ? jumpstartaccelerationhalf : jumpstartacceleration;
                soundnow = true;
            }

            if (jumpacceleration > 0)
            {
                isplayeronground = false;
                jumpacceleration = jumpacceleration / 2;
            }

            movedz += jumpacceleration * constJump;
        }
        else
        {
            isplayeronground = true;
        }
        game.isplayeronground = isplayeronground;
    }

    // -------------------------------------------------------------------------
    // Collision helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true when the block at the given block-space coordinates does not
    /// impede player movement (air, fluid, rail, or out-of-bounds above the map).
    /// </summary>
    public bool IsTileEmptyForPhysics(int x, int y, int z)
    {
        if (z >= game.VoxelMap.MapSizeZ)
        {
            return true;
        }
        bool enableFreemove = false;
        if (x < 0 || y < 0 || z < 0)
        {
            return enableFreemove;
        }
        if (x >= game.VoxelMap.MapSizeX || y >= game.VoxelMap.MapSizeY)
        {
            return enableFreemove;
        }
        int block = game.VoxelMap.GetBlockValid(x, y, z);
        if (block == 0)
        {
            return true;
        }
        Packet_BlockType blocktype = game.blocktypes[block];
        return blocktype.WalkableType == WalkableType.Fluid
            || Game.IsEmptyForPhysics(blocktype)
            || IsRail(blocktype);
    }

    /// <summary>Reusable scratch vector used inside <see cref="WallSlide"/> to avoid allocation.</summary>
    private Vector3 tmpPlayerPosition;

    /// <summary>
    /// Moves the player from <paramref name="oldposition"/> toward <paramref name="newposition"/>
    /// one axis at a time, stopping each axis independently on collision.
    /// Also detects ground contact, walls for auto-jump, and half-block step-ups.
    /// </summary>
    /// <param name="oldposition">Confirmed valid position from the previous tick.</param>
    /// <param name="newposition">Desired position after input and gravity this tick.</param>
    /// <param name="modelheight">Entity height in blocks; tall models require an extra empty block check.</param>
    /// <returns>The furthest collision-safe position reachable from <paramref name="oldposition"/>.</returns>
    public Vector3 WallSlide(Vector3 oldposition, Vector3 newposition, float modelheight)
    {
        bool high = modelheight >= 2;

        // Temporarily raise Y by wallDistance so ground collision uses the feet offset.
        oldposition.Y += game.constWallDistance;
        newposition.Y += game.constWallDistance;

        game.reachedwall = false;
        game.reachedwall_1blockhigh = false;
        game.reachedHalfBlock = false;

        tmpPlayerPosition.X = oldposition.X;
        tmpPlayerPosition.Y = oldposition.Y;
        tmpPlayerPosition.Z = oldposition.Z;

        // X axis
        if (IsEmptySpaceForPlayer(high, newposition.X, tmpPlayerPosition.Y, tmpPlayerPosition.Z, out int tmpBlockingBlockType))
        {
            tmpPlayerPosition.X = newposition.X;
        }
        else
        {
            game.reachedwall = true;
            if (IsEmptyPoint(newposition.X, tmpPlayerPosition.Y + 0.5f, tmpPlayerPosition.Z, out _))
            {
                game.reachedwall_1blockhigh = true;
                if (game.blocktypes[tmpBlockingBlockType].DrawType == DrawType.HalfHeight) { game.reachedHalfBlock = true; }
                if (StandingOnHalfBlock(newposition.X, tmpPlayerPosition.Y, tmpPlayerPosition.Z)) { game.reachedHalfBlock = true; }
            }
        }

        // Y axis
        if (IsEmptySpaceForPlayer(high, tmpPlayerPosition.X, newposition.Y, tmpPlayerPosition.Z, out tmpBlockingBlockType))
        {
            tmpPlayerPosition.Y = newposition.Y;
        }

        // Z axis
        if (IsEmptySpaceForPlayer(high, tmpPlayerPosition.X, tmpPlayerPosition.Y, newposition.Z, out tmpBlockingBlockType))
        {
            tmpPlayerPosition.Z = newposition.Z;
        }
        else
        {
            game.reachedwall = true;
            if (IsEmptyPoint(tmpPlayerPosition.X, tmpPlayerPosition.Y + 0.5f, newposition.Z, out _))
            {
                game.reachedwall_1blockhigh = true;
                if (game.blocktypes[tmpBlockingBlockType].DrawType == DrawType.HalfHeight) { game.reachedHalfBlock = true; }
                if (StandingOnHalfBlock(tmpPlayerPosition.X, tmpPlayerPosition.Y, newposition.Z)) { game.reachedHalfBlock = true; }
            }
        }

        // Ground detection: Y did not advance toward the desired lower position.
        isplayeronground = (tmpPlayerPosition.Y == oldposition.Y) && (newposition.Y < oldposition.Y);

        tmpPlayerPosition.Y -= game.constWallDistance;
        return tmpPlayerPosition;
    }

    /// <summary>
    /// Returns true when the block directly beneath the given XZ position is a half-height block,
    /// used to determine whether a step-up should be treated as a half-block climb.
    /// </summary>
    private bool StandingOnHalfBlock(float x, float y, float z)
    {
        int under = game.VoxelMap.GetBlock((int)x, (int)z, (int)y);
        return game.blocktypes[under].DrawType == DrawType.HalfHeight;
    }

    /// <summary>
    /// Returns true when the column at (<paramref name="x"/>, <paramref name="z"/>) is passable
    /// for a player of the given height: checks 2 blocks (3 for tall models) via <see cref="IsEmptyPoint"/>.
    /// </summary>
    private bool IsEmptySpaceForPlayer(bool high, float x, float y, float z, out int blockingBlockType)
    {
        return IsEmptyPoint(x, y, z, out blockingBlockType)
            && IsEmptyPoint(x, y + 1, z, out blockingBlockType)
            && (!high || IsEmptyPoint(x, y + 2, z, out blockingBlockType));
    }

    /// <summary>
    /// Returns true when no solid block exists within <see cref="Game.constWallDistance"/> of the point.
    /// Tests a 3×3×3 neighbourhood and uses Chebyshev distance against each block's bounding box.
    /// </summary>
    /// <param name="blockingBlocktype">Set to the block type that caused rejection, or 0 if empty.</param>
    private bool IsEmptyPoint(float x, float y, float z, out int blockingBlocktype)
    {
        for (int xx = 0; xx < 3; xx++)
        {
            for (int yy = 0; yy < 3; yy++)
            {
                for (int zz = 0; zz < 3; zz++)
                {
                    if (!IsTileEmptyForPhysics((int)(x + xx - 1), (int)(z + zz - 1), (int)(y + yy - 1)))
                    {
                        float minX = x + xx - 1;
                        float minY = y + yy - 1;
                        float minZ = z + zz - 1;
                        float maxX = minX + 1;
                        float maxY = minY + game.Getblockheight((int)(x + xx - 1), (int)(z + zz - 1), (int)(y + yy - 1));
                        float maxZ = minZ + 1;

                        if (BoxPointDistance(minX, minY, minZ, maxX, maxY, maxZ, x, y, z) < game.constWallDistance)
                        {
                            blockingBlocktype = game.VoxelMap.GetBlock((int)(x + xx - 1), (int)(z + zz - 1), (int)(y + yy - 1));
                            return false;
                        }
                    }
                }
            }
        }
        blockingBlocktype = 0;
        return true;
    }

    // -------------------------------------------------------------------------
    // Static helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the Chebyshev distance between point (<paramref name="pX"/>, <paramref name="pY"/>, <paramref name="pZ"/>)
    /// and the axis-aligned box defined by its min/max corners.
    /// Returns 0 when the point is inside the box.
    /// </summary>
    public static float BoxPointDistance(float minX, float minY, float minZ, float maxX, float maxY, float maxZ, float pX, float pY, float pZ)
    {
        float dx = Max3(minX - pX, 0, pX - maxX);
        float dy = Max3(minY - pY, 0, pY - maxY);
        float dz = Max3(minZ - pZ, 0, pZ - maxZ);
        return Max3(dx, dy, dz);
    }

    /// <summary>
    /// Moves <paramref name="a"/> toward zero by at most <paramref name="b"/>, never crossing zero.
    /// Used for friction: decelerates a speed component without reversing it.
    /// </summary>
    public static float MakeCloserToZero(float a, float b)
    {
        if (a > 0)
        {
            float c = a - b;
            if (c < 0) { c = 0; }
            return c;
        }
        else
        {
            float c = a + b;
            if (c > 0) { c = 0; }
            return c;
        }
    }

    /// <summary>Returns the largest of three float values.</summary>
    private static float Max3(float a, float b, float c)
    {
        return Math.Max(Math.Max(a, b), c);
    }

    /// <summary>Returns true when <paramref name="block"/> has a non-zero rail value (excludes unplaceable Rail0).</summary>
    public static bool IsRail(Packet_BlockType block)
    {
        return block.Rail > 0;
    }
}

/// <summary>
/// Tunable acceleration parameters governing how quickly the player ramps up, brakes, and is pushed.
/// Reset to defaults each physics tick before surface/air overrides are applied.
/// </summary>
public class Acceleration
{
    public Acceleration()
    {
        SetDefault();
    }

    /// <summary>Per-tick velocity multiplier (drag). Values below 1 bleed off speed over time.</summary>
    internal float acceleration1;

    /// <summary>Flat per-second deceleration applied toward zero (friction), scaled by dt.</summary>
    internal float acceleration2;

    /// <summary>Force multiplier applied to input direction, scaled by dt (movement responsiveness).</summary>
    internal float acceleration3;

    /// <summary>
    /// Restores normal ground-movement values:
    /// moderate drag (0.9), light friction (2), high responsiveness (700).
    /// </summary>
    public void SetDefault()
    {
        acceleration1 = 0.9f;
        acceleration2 = 2;
        acceleration3 = 700;
    }
}

/// <summary>
/// Snapshot of player input for a single physics tick.
/// Populated by input-handling mods and consumed by <see cref="ScriptCharacterPhysics"/>.
/// </summary>
public class Controls
{
    /// <summary>Lateral strafe input in [-1, 1] (negative = left, positive = right).</summary>
    internal float movedx;

    /// <summary>Forward/backward input in [-1, 1] (negative = back, positive = forward).</summary>
    internal float movedy;

    /// <summary>True when the player pressed the jump key this tick.</summary>
    internal bool wantsjump;

    /// <summary>True when a reduced-height jump was requested (e.g. crouch-jump binding).</summary>
    internal bool wantsjumphalf;

    /// <summary>True while the fly/swim ascend key is held.</summary>
    internal bool moveup;

    /// <summary>True while the fly/swim descend key is held.</summary>
    internal bool movedown;

    /// <summary>True while the shift (sneak) key is held; suppresses trampoline super-jump.</summary>
    internal bool shiftkeydown;

    /// <summary>When true, gravity is disabled and the player moves freely in all three axes.</summary>
    internal bool freemove;

    /// <summary>When true, collision detection is disabled (no-clip / ghost mode).</summary>
    internal bool noclip;
}