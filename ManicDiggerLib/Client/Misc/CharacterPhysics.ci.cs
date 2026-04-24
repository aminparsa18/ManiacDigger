using ManicDigger;
using OpenTK.Mathematics;

/// <summary>
/// Defines the per-frame fixed-timestep update contract for entity behaviour scripts.
/// </summary>
public interface IEntityScript
{
    /// <summary>Called once per fixed physics tick for the given <paramref name="entity"/>.</summary>
    void OnNewFrameFixed(int entity, float dt);
}

/// <summary>
/// Implements walking, jumping, swimming, and collision-response physics for the local player.
/// Runs each fixed timestep via <see cref="IEntityScript.OnNewFrameFixed"/>.
/// </summary>
public class ScriptCharacterPhysics : IEntityScript
{
    // ── Constructor ───────────────────────────────────────────────────────────

    public ScriptCharacterPhysics(IGameClient game, IGamePlatform platform)
    {
        this.game = game;
        this.platform = platform;
        // Only non-default values need explicit initialisation.
        // (movedz, curspeed, jumpacceleration, isplayeronground, etc. are
        //  already zero/false by C# default for their types.)
        acceleration.SetDefault();
        constGravity = 0.3f;
        constWaterGravityMultiplier = 3;
        constEnableAcceleration = true;
        constJump = 2.1f;
    }

    // ── Dependencies ──────────────────────────────────────────────────────────

    /// <summary>Reference to the active game instance, assigned at the start of each tick.</summary>
    private readonly IGameClient game;
    private readonly IGamePlatform platform;

    // ── Per-frame physics state ───────────────────────────────────────────────

    /// <summary>Current vertical velocity (positive = upward). Modified by gravity and jumps each tick.</summary>
    internal float movedz;

    /// <summary>Current XYZ movement speed vector, attenuated by <see cref="acceleration"/> each tick.</summary>
    internal Vector3 curspeed;

    /// <summary>Remaining upward acceleration from an in-progress jump, halved each tick until exhausted.</summary>
    internal float jumpacceleration;

    /// <summary>True when the player is standing on solid ground this tick.</summary>
    internal bool isplayeronground;

    /// <summary>Tunable acceleration parameters (drag, friction, responsiveness) for the current tick.</summary>
    internal Acceleration acceleration;

    /// <summary>Full-height jump initial acceleration, derived from <see cref="constGravity"/> each tick.</summary>
    internal float jumpstartacceleration;

    /// <summary>Half-height jump initial acceleration (crouch-jump / swim surfacing).</summary>
    internal float jumpstartaccelerationhalf;

    /// <summary>Effective movement speed this tick, sourced from <see cref="Game.MoveSpeedNow"/>.</summary>
    internal float movespeednow;

    // ── Tunable constants ─────────────────────────────────────────────────────

    /// <summary>Base gravitational acceleration applied each tick.</summary>
    internal float constGravity;

    /// <summary>Factor by which gravity is multiplied while swimming.</summary>
    internal float constWaterGravityMultiplier;

    /// <summary>When false, <see cref="curspeed"/> is set directly from input rather than ramped.</summary>
    internal bool constEnableAcceleration;

    /// <summary>Multiplier applied to <see cref="jumpacceleration"/> each tick to produce upward displacement.</summary>
    internal float constJump;

    // ── IEntityScript ─────────────────────────────────────────────────────────

    /// <summary>
    /// Entry point called by the entity system each fixed timestep.
    /// Reads player input, resolves move speed, and delegates to <see cref="Update"/>.
    /// Does nothing while the map-loading screen is active.
    /// </summary>
    public void OnNewFrameFixed(int entity, float dt)
    {
        if (game.GuiState == GuiState.MapLoading) return;

        movespeednow = game.MoveSpeedNow();
        game.Controls.movedx = Math.Clamp(game.Controls.movedx, -1, 1);
        game.Controls.movedy = Math.Clamp(game.Controls.movedy, -1, 1);

        jumpstartacceleration = 13.333f * constGravity;
        jumpstartaccelerationhalf = 9f * constGravity;
        acceleration.SetDefault();
        game.soundnow = false; // was `new bool()` — Cito-ism for false

        // ── Follow mode: suppress local input ────────────────────────────────
        // FollowId() does a linear entity scan — call once and cache.
        int? followId = game.FollowId();
        Controls move = game.Controls;
        if (followId != null && followId == game.LocalPlayerId)
        {
            move.movedx = 0;
            move.movedy = 0;
            move.moveup = false;
            move.wantsjump = false;
        }

        Update(game.Player.position, move, dt,
            out bool soundNow,
            new Vector3(game.PushX, game.PushY, game.PushZ),
            game.Entities[game.LocalPlayerId].drawModel.ModelHeight);

        game.soundnow = soundNow;
    }

    // ── Core physics update ───────────────────────────────────────────────────

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
    public void Update(EntityPosition_ stateplayerposition, Controls move, float dt,
        out bool soundnow, Vector3 push, float modelheight)
    {
        if (game.stopPlayerMove)
        {
            movedz = 0;
            game.stopPlayerMove = false;
        }

        // ── Cache per-tick queries called multiple times ───────────────────────
        bool swimmingBody = game.SwimmingBody();
        int blockUnder = game.BlockUnderPlayer();

        // ── Air control: high drag + low ramp-up while airborne ───────────────
        if (!isplayeronground)
        {
            acceleration.acceleration1 = 0.99f;
            acceleration.acceleration2 = 0.2f;
            acceleration.acceleration3 = 70;
        }

        // ── Trampoline: force a super-jump on landing ─────────────────────────
        if (blockUnder != -1
            && blockUnder == game.BlockRegistry.BlockIdTrampoline
            && !isplayeronground
            && !game.Controls.shiftkeydown)
        {
            game.Controls.wantsjump = true;
            jumpstartacceleration = 20.666f * constGravity;
        }

        // ── Ice / water: slippery acceleration profile ────────────────────────
        if ((blockUnder != -1 && game.BlockRegistry.IsSlipperyWalk[blockUnder]) || swimmingBody)
        {
            acceleration.acceleration1 = 0.99f;
            acceleration.acceleration2 = 0.2f;
            acceleration.acceleration3 = 70;
        }

        soundnow = false;

        // ── Convert input from player-local to world space ────────────────────
        Vector3 diff1 = new();
        VectorUtils.ToVectorInFixedSystem(
            move.movedx * movespeednow * dt,
            0,
            move.movedy * movespeednow * dt,
            stateplayerposition.rotx, stateplayerposition.roty, ref diff1);

        // ── Apply normalised external push ────────────────────────────────────
        // Use LengthSquared to avoid sqrt for the magnitude check.
        if (push.LengthSquared > 0.0001f) // equivalent to Length > 0.01f
        {
            push = Vector3.Normalize(push);
            push.X *= 5;
            push.Y *= 5;
            push.Z *= 5;
        }
        diff1.X += push.X * dt;
        diff1.Y += push.Y * dt;
        diff1.Z += push.Z * dt;

        // ── Gravity: only after the chunk under the player has arrived ─────────
        bool loaded = false;
        int cx = (int)(game.Player.position.x / Game.chunksize);
        int cy = (int)(game.Player.position.z / Game.chunksize);
        int cz = (int)(game.Player.position.y / Game.chunksize);
        if (game.VoxelMap.IsValidChunkPos(cx, cy, cz))
        {
            // Use cached chunk-count properties instead of recomputing / Game.chunksize.
            if (game.VoxelMap.Chunks[VectorIndexUtil.Index3d(
                    cx, cy, cz,
                    game.VoxelMap.Mapsizexchunks,
                    game.VoxelMap.Mapsizeychunks)] != null)
                loaded = true;
        }
        else
        {
            loaded = true;
        }

        if (!move.freemove && loaded)
        {
            movedz += swimmingBody
                ? -constGravity * constWaterGravityMultiplier
                : -constGravity;
        }
        game.MovedZ = movedz;

        if (constEnableAcceleration)
        {
            // Drag
            curspeed.X *= acceleration.acceleration1;
            curspeed.Y *= acceleration.acceleration1;
            curspeed.Z *= acceleration.acceleration1;
            // Friction (frame-rate independent)
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
            // Clamp to max speed — LengthSquared avoids sqrt for the comparison.
            if (curspeed.LengthSquared > movespeednow * movespeednow)
            {
                curspeed = Vector3.Normalize(curspeed);
                curspeed.X *= movespeednow;
                curspeed.Y *= movespeednow;
                curspeed.Z *= movespeednow;
            }
        }
        else
        {
            // Instant response: no ramp-up.
            // LengthSquared avoids sqrt for the zero-check.
            if (diff1.LengthSquared > 0)
                diff1 = Vector3.Normalize(diff1);

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
            // Horizontal-only movement when not swimming (vertical handled by movedz).
            if (!swimmingBody)
                newposition.Y = stateplayerposition.y;

            // Re-normalise horizontal displacement then scale by actual speed.
            float diffx = newposition.X - stateplayerposition.x;
            float diffy = newposition.Y - stateplayerposition.y;
            float diffz = newposition.Z - stateplayerposition.z;
            float difflength = MathF.Sqrt(diffx * diffx + diffy * diffy + diffz * diffz);
            if (difflength > 0)
            {
                float inv = curspeed.Length / difflength;
                diffx *= inv;
                diffy *= inv;
                diffz *= inv;
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
            Vector3 v = WallSlide(
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
            if (isplayeronground || swimmingBody)
            {
                jumpacceleration = 0;
                movedz = 0;
            }
            if ((move.wantsjump || move.wantsjumphalf)
                && (jumpacceleration == 0 && isplayeronground || swimmingBody)
                && loaded
                && !game.SwimmingEyes())
            {
                jumpacceleration = move.wantsjumphalf
                    ? jumpstartaccelerationhalf
                    : jumpstartacceleration;
                soundnow = true;
            }

            if (jumpacceleration > 0)
            {
                isplayeronground = false;
                jumpacceleration /= 2;
            }

            movedz += jumpacceleration * constJump;
        }
        else
        {
            isplayeronground = true;
        }

        game.IsPlayerOnGround = isplayeronground;
    }

    // ── Collision helpers ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when the block at the given block-space coordinates does not
    /// impede player movement (air, fluid, rail, or out-of-bounds above the map).
    /// </summary>
    public bool IsTileEmptyForPhysics(int x, int y, int z)
    {
        if (z >= game.VoxelMap.MapSizeZ) return true;
        if (x < 0 || y < 0 || z < 0) return false;
        if (x >= game.VoxelMap.MapSizeX || y >= game.VoxelMap.MapSizeY) return false;

        int block = game.VoxelMap.GetBlockValid(x, y, z);
        if (block == 0) return true;

        Packet_BlockType blocktype = game.BlockTypes[block];
        return blocktype.WalkableType == WalkableType.Fluid
            || Game.IsEmptyForPhysics(blocktype)
            || IsRail(blocktype);
    }

    /// <summary>Reusable scratch vector used inside <see cref="WallSlide"/> to avoid stack allocation per call.</summary>
    private Vector3 tmpPlayerPosition;

    /// <summary>
    /// Moves the player from <paramref name="oldposition"/> toward <paramref name="newposition"/>
    /// one axis at a time, stopping each axis independently on collision.
    /// Also detects ground contact, walls for auto-jump, and half-block step-ups.
    /// </summary>
    public Vector3 WallSlide(Vector3 oldposition, Vector3 newposition, float modelheight)
    {
        bool high = modelheight >= 2;

        oldposition.Y += game.WallDistance;
        newposition.Y += game.WallDistance;

        game.ReachedWall = false;
        game.ReachedWall1BlockHigh = false;
        game.ReachedHalfBlock = false;

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
            game.ReachedWall = true;
            if (IsEmptyPoint(newposition.X, tmpPlayerPosition.Y + 0.5f, tmpPlayerPosition.Z, out _))
            {
                game.ReachedWall1BlockHigh = true;
                if (game.BlockTypes[tmpBlockingBlockType].DrawType == DrawType.HalfHeight) game.ReachedHalfBlock = true;
                if (StandingOnHalfBlock(newposition.X, tmpPlayerPosition.Y, tmpPlayerPosition.Z)) game.ReachedHalfBlock = true;
            }
        }

        // Y axis
        if (IsEmptySpaceForPlayer(high, tmpPlayerPosition.X, newposition.Y, tmpPlayerPosition.Z, out _))
            tmpPlayerPosition.Y = newposition.Y;

        // Z axis
        if (IsEmptySpaceForPlayer(high, tmpPlayerPosition.X, tmpPlayerPosition.Y, newposition.Z, out tmpBlockingBlockType))
        {
            tmpPlayerPosition.Z = newposition.Z;
        }
        else
        {
            game.ReachedWall = true;
            if (IsEmptyPoint(tmpPlayerPosition.X, tmpPlayerPosition.Y + 0.5f, newposition.Z, out _))
            {
                game.ReachedWall1BlockHigh = true;
                if (game.BlockTypes[tmpBlockingBlockType].DrawType == DrawType.HalfHeight) game.ReachedHalfBlock = true;
                if (StandingOnHalfBlock(tmpPlayerPosition.X, tmpPlayerPosition.Y, newposition.Z)) game.ReachedHalfBlock = true;
            }
        }

        // Ground detection: Y did not advance toward the desired lower position.
        isplayeronground = tmpPlayerPosition.Y == oldposition.Y && newposition.Y < oldposition.Y;

        tmpPlayerPosition.Y -= game.WallDistance;
        return tmpPlayerPosition;
    }

    private bool StandingOnHalfBlock(float x, float y, float z)
    {
        int under = game.VoxelMap.GetBlock((int)x, (int)z, (int)y);
        return game.BlockTypes[under].DrawType == DrawType.HalfHeight;
    }

    private bool IsEmptySpaceForPlayer(bool high, float x, float y, float z, out int blockingBlockType)
    {
        return IsEmptyPoint(x, y, z, out blockingBlockType)
            && IsEmptyPoint(x, y + 1, z, out blockingBlockType)
            && (!high || IsEmptyPoint(x, y + 2, z, out blockingBlockType));
    }

    private bool IsEmptyPoint(float x, float y, float z, out int blockingBlocktype)
    {
        for (int xx = 0; xx < 3; xx++)
            for (int yy = 0; yy < 3; yy++)
                for (int zz = 0; zz < 3; zz++)
                {
                    if (!IsTileEmptyForPhysics((int)(x + xx - 1), (int)(z + zz - 1), (int)(y + yy - 1)))
                    {
                        float minX = x + xx - 1, maxX = minX + 1;
                        float minY = y + yy - 1;
                        float maxY = minY + game.Getblockheight((int)(x + xx - 1), (int)(z + zz - 1), (int)(y + yy - 1));
                        float minZ = z + zz - 1, maxZ = minZ + 1;

                        if (BoxPointDistance(minX, minY, minZ, maxX, maxY, maxZ, x, y, z) < game.WallDistance)
                        {
                            blockingBlocktype = game.VoxelMap.GetBlock((int)(x + xx - 1), (int)(z + zz - 1), (int)(y + yy - 1));
                            return false;
                        }
                    }
                }
        blockingBlocktype = 0;
        return true;
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the Chebyshev distance between a point and an AABB.
    /// Returns 0 when the point is inside the box.
    /// </summary>
    public static float BoxPointDistance(
        float minX, float minY, float minZ,
        float maxX, float maxY, float maxZ,
        float pX, float pY, float pZ)
    {
        float dx = Max3(minX - pX, 0, pX - maxX);
        float dy = Max3(minY - pY, 0, pY - maxY);
        float dz = Max3(minZ - pZ, 0, pZ - maxZ);
        return Max3(dx, dy, dz);
    }

    /// <summary>
    /// Moves <paramref name="a"/> toward zero by at most <paramref name="b"/>, never crossing zero.
    /// Used for friction — decelerates a speed component without reversing it.
    /// </summary>
    public static float MakeCloserToZero(float a, float b)
    {
        if (a > 0) return Math.Max(a - b, 0);
        else return Math.Min(a + b, 0);
    }

    /// <summary>Returns the largest of three float values.</summary>
    private static float Max3(float a, float b, float c) => Math.Max(Math.Max(a, b), c);

    /// <summary>Returns true when <paramref name="block"/> has a non-zero rail value.</summary>
    public static bool IsRail(Packet_BlockType block) => block.Rail > 0;
}

/// <summary>
/// Tunable acceleration parameters governing drag, friction, and movement responsiveness.
/// Stored as a struct — embedded directly in <see cref="ScriptCharacterPhysics"/> with
/// no separate heap allocation. Reset to defaults each physics tick before
/// surface/air overrides are applied.
/// </summary>
public struct Acceleration
{
    /// <summary>Per-tick velocity multiplier (drag). Values below 1 bleed off speed over time.</summary>
    internal float acceleration1;

    /// <summary>Flat per-second deceleration toward zero (friction), scaled by dt.</summary>
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
        acceleration2 = 2f;
        acceleration3 = 700f;
    }
}

/// <summary>
/// Snapshot of player input for a single physics tick.
/// Stored as a struct so that <c>Controls move = game.controls</c> produces
/// a cheap copy — local modifications (e.g. zeroing movement in follow mode)
/// do not affect the original <c>game.controls</c> state.
/// </summary>
public class Controls
{
    /// <summary>Lateral strafe input in [-1, 1] (negative = left, positive = right).</summary>
    internal float movedx;

    /// <summary>Forward/backward input in [-1, 1] (negative = back, positive = forward).</summary>
    internal float movedy;

    /// <summary>True when the player pressed the jump key this tick.</summary>
    internal bool wantsjump;

    /// <summary>True when a reduced-height jump was requested (e.g. crouch-jump).</summary>
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