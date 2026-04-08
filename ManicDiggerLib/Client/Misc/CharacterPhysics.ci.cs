using OpenTK.Mathematics;

public class ScriptCharacterPhysics : EntityScript
{
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

    internal Game game;

    internal float movedz;
    internal Vector3 curspeed;
    internal float jumpacceleration;
    internal bool isplayeronground;
    internal Acceleration acceleration;
    internal float jumpstartacceleration;
    internal float jumpstartaccelerationhalf;
    internal float movespeednow;

    internal float constGravity;
    internal float constWaterGravityMultiplier;
    internal bool constEnableAcceleration;
    internal float constJump;

    public override void OnNewFrameFixed(Game game_, int entity, float dt)
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

    public void Update(EntityPosition_ stateplayerposition, Controls move, float dt, out bool soundnow, Vector3 push, float modelheight)
    {
        if (game.stopPlayerMove)
        {
            movedz = 0;
            game.stopPlayerMove = false;
        }

        // No air control
        if (!isplayeronground)
        {
            acceleration.acceleration1 = 0.99f;
            acceleration.acceleration2 = 0.2f;
            acceleration.acceleration3 = 70;
        }

        // Trampoline
        {
            int blockunderplayer = game.BlockUnderPlayer();
            if (blockunderplayer != -1 && blockunderplayer == game.d_Data.BlockIdTrampoline()
                && (!isplayeronground) && !game.controls.shiftkeydown)
            {
                game.controls.wantsjump = true;
                jumpstartacceleration = 20.666f * constGravity;
            }
        }

        // Slippery walk on ice and when swimming
        {
            int blockunderplayer = game.BlockUnderPlayer();
            if ((blockunderplayer != -1 && game.d_Data.IsSlipperyWalk()[blockunderplayer]) || game.SwimmingBody())
            {
                acceleration.acceleration1 = 0.99f;
                acceleration.acceleration2 = 0.2f;
                acceleration.acceleration3 = 70;
            }
        }

        soundnow = false;
        Vector3 diff1 = new();
        VectorUtils.ToVectorInFixedSystem(
            move.movedx * movespeednow * dt,
            0,
            move.movedy * movespeednow * dt, stateplayerposition.rotx, stateplayerposition.roty, ref diff1);

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

        bool loaded = false;
        int cx = (int)(game.player.position.x / Game.chunksize);
        int cy = (int)(game.player.position.z / Game.chunksize);
        int cz = (int)(game.player.position.y / Game.chunksize);
        if (game.map.IsValidChunkPos(cx, cy, cz))
        {
            if (game.map.chunks[MapUtilCi.Index3d(cx, cy, cz,
                game.map.MapSizeX / Game.chunksize,
                game.map.MapSizeY / Game.chunksize)] != null)
            {
                loaded = true;
            }
        }
        else
        {
            loaded = true;
        }
        if ((!(move.freemove)) && loaded)
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
            curspeed.X *= acceleration.acceleration1;
            curspeed.Y *= acceleration.acceleration1;
            curspeed.Z *= acceleration.acceleration1;
            curspeed.X = MakeCloserToZero(curspeed.X, acceleration.acceleration2 * dt);
            curspeed.Y = MakeCloserToZero(curspeed.Y, acceleration.acceleration2 * dt);
            curspeed.Z = MakeCloserToZero(curspeed.Z, acceleration.acceleration2 * dt);
            diff1.Y += move.moveup ? 2 * movespeednow * dt : 0;
            diff1.Y -= move.movedown ? 2 * movespeednow * dt : 0;
            curspeed.X += diff1.X * acceleration.acceleration3 * dt;
            curspeed.Y += diff1.Y * acceleration.acceleration3 * dt;
            curspeed.Z += diff1.Z * acceleration.acceleration3 * dt;
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
            if (diff1.Length > 0)
            {
                Vector3.Normalize(in diff1, out diff1);
            }
            curspeed.X = diff1.X * movespeednow;
            curspeed.Y = diff1.Y * movespeednow;
            curspeed.Z = diff1.Z * movespeednow;
        }
        Vector3 newposition = Vector3.Zero;
        if (!(move.freemove))
        {
            newposition.X = stateplayerposition.x + curspeed.X;
            newposition.Y = stateplayerposition.y + curspeed.Y;
            newposition.Z = stateplayerposition.z + curspeed.Z;
            if (!game.SwimmingBody())
            {
                newposition.Y = stateplayerposition.y;
            }
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
        newposition.Y += movedz * dt;
        Vector3 previousposition = new Vector3(stateplayerposition.x, stateplayerposition.y, stateplayerposition.z);
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
        if (!(move.freemove))
        {
            if ((isplayeronground) || game.SwimmingBody())
            {
                jumpacceleration = 0;
                movedz = 0;
            }
            if ((move.wantsjump || move.wantsjumphalf) && (((jumpacceleration == 0 && isplayeronground) || game.SwimmingBody()) && loaded) && (!game.SwimmingEyes()))
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

    public bool IsTileEmptyForPhysics(int x, int y, int z)
    {
        if (z >= game.map.MapSizeZ)
        {
            return true;
        }
        bool enableFreemove = false;
        if (x < 0 || y < 0 || z < 0)// || z >= mapsizez)
        {
            return enableFreemove;
        }
        if (x >= game.map.MapSizeX || y >= game.map.MapSizeY)// || z >= mapsizez)
        {
            return enableFreemove;
        }
        int block = game.map.GetBlockValid(x, y, z);
        if (block == 0)
        {
            return true;
        }
        Packet_BlockType blocktype = game.blocktypes[block];
        return blocktype.WalkableType == Packet_WalkableTypeEnum.Fluid
            || Game.IsEmptyForPhysics(blocktype)
            || IsRail(blocktype);
    }

    private Vector3 tmpPlayerPosition;		//Temporarily stores the player's position. Used in WallSlide()
    public Vector3 WallSlide(Vector3 oldposition, Vector3 newposition, float modelheight)
    {
        bool high = false;
        if (modelheight >= 2) { high = true; }  // Set high to true if player model is bigger than standard height
        oldposition.Y += game.constWallDistance;        // Add walldistance temporarily for ground collisions
        newposition.Y += game.constWallDistance;        // Add walldistance temporarily for ground collisions

        game.reachedwall = false;
        game.reachedwall_1blockhigh = false;
        game.reachedHalfBlock = false;

        tmpPlayerPosition.X = oldposition.X;
        tmpPlayerPosition.Y = oldposition.Y;
        tmpPlayerPosition.Z = oldposition.Z;

        // X
        if (IsEmptySpaceForPlayer(high, newposition.X, tmpPlayerPosition.Y, tmpPlayerPosition.Z, out int tmpBlockingBlockType))
        {
            tmpPlayerPosition.X = newposition.X;
        }
        else
        {
            // For autojump
            game.reachedwall = true;
            if (IsEmptyPoint(newposition.X, tmpPlayerPosition.Y + 0.5f, tmpPlayerPosition.Z, out _))
            {
                game.reachedwall_1blockhigh = true;
                if (game.blocktypes[tmpBlockingBlockType].DrawType == Packet_DrawTypeEnum.HalfHeight) { game.reachedHalfBlock = true; }
                if (StandingOnHalfBlock(newposition.X, tmpPlayerPosition.Y, tmpPlayerPosition.Z)) { game.reachedHalfBlock = true; }
            }
        }
        // Y
        if (IsEmptySpaceForPlayer(high, tmpPlayerPosition.X, newposition.Y, tmpPlayerPosition.Z, out tmpBlockingBlockType))
        {
            tmpPlayerPosition.Y = newposition.Y;
        }
        // Z
        if (IsEmptySpaceForPlayer(high, tmpPlayerPosition.X, tmpPlayerPosition.Y, newposition.Z, out tmpBlockingBlockType))
        {
            tmpPlayerPosition.Z = newposition.Z;
        }
        else
        {
            // For autojump
            game.reachedwall = true;
            if (IsEmptyPoint(tmpPlayerPosition.X, tmpPlayerPosition.Y + 0.5f, newposition.Z, out _))
            {
                game.reachedwall_1blockhigh = true;
                if (game.blocktypes[tmpBlockingBlockType].DrawType == Packet_DrawTypeEnum.HalfHeight) { game.reachedHalfBlock = true; }
                if (StandingOnHalfBlock(tmpPlayerPosition.X, tmpPlayerPosition.Y, newposition.Z)) { game.reachedHalfBlock = true; }
            }
        }

        isplayeronground = (tmpPlayerPosition.Y == oldposition.Y) && (newposition.Y < oldposition.Y);

        tmpPlayerPosition.Y -= game.constWallDistance;  // Remove the temporary walldistance again
        return tmpPlayerPosition;   // Return valid position
    }

    private bool StandingOnHalfBlock(float x, float y, float z)
    {
        int under = game.map.GetBlock((int)(x),
            (int)(z),
            (int)(y));
        return game.blocktypes[under].DrawType == Packet_DrawTypeEnum.HalfHeight;
    }

    private bool IsEmptySpaceForPlayer(bool high, float x, float y, float z, out int blockingBlockType)
    {
        return IsEmptyPoint(x, y, z, out blockingBlockType)
            && IsEmptyPoint(x, y + 1, z, out blockingBlockType)
            && (!high || IsEmptyPoint(x, y + 2, z, out blockingBlockType));
    }

    // Checks if there are no solid blocks in walldistance area around the point
    private bool IsEmptyPoint(float x, float y, float z, out int blockingBlocktype)
    {
        // Test 3x3x3 blocks around the point
        for (int xx = 0; xx < 3; xx++)
        {
            for (int yy = 0; yy < 3; yy++)
            {
                for (int zz = 0; zz < 3; zz++)
                {
                    if (!IsTileEmptyForPhysics((int)(x + xx - 1), (int)(z + zz - 1), (int)(y + yy - 1)))
                    {
                        // Found a solid block

                        // Get bounding box of the block
                        float minX = (x + xx - 1);
                        float minY = (y + yy - 1);
                        float minZ = (z + zz - 1);
                        float maxX = minX + 1;
                        float maxY = minY + game.Getblockheight((int)(x + xx - 1), (int)(z + zz - 1), (int)(y + yy - 1));
                        float maxZ = minZ + 1;

                        // Check if the block is too close
                        if (BoxPointDistance(minX, minY, minZ, maxX, maxY, maxZ, x, y, z) < game.constWallDistance)
                        {
                            blockingBlocktype = game.map.GetBlock((int)(x + xx - 1), (int)(z + zz - 1), (int)(y + yy - 1));
                            return false;
                        }
                    }
                }
            }
        }
        blockingBlocktype = 0;
        return true;
    }

    // Using chebyshev distance
    public static float BoxPointDistance(float minX, float minY, float minZ, float maxX, float maxY, float maxZ, float pX, float pY, float pZ)
    {
        float dx = Max3(minX - pX, 0, pX - maxX);
        float dy = Max3(minY - pY, 0, pY - maxY);
        float dz = Max3(minZ - pZ, 0, pZ - maxZ);
        return Max3(dx, dy, dz);
    }

    public static float MakeCloserToZero(float a, float b)
    {
        if (a > 0)
        {
            float c = a - b;
            if (c < 0)
            {
                c = 0;
            }
            return c;
        }
        else
        {
            float c = a + b;
            if (c > 0)
            {
                c = 0;
            }
            return c;
        }
    }

    private static float Max3(float a, float b, float c)
    {
        return Math.Max(Math.Max(a, b), c);
    }

    public static bool IsRail(Packet_BlockType block)
    {
        return block.Rail > 0;	//Does not include Rail0, but this can't be placed.
    }
}

public class Acceleration
{
    public Acceleration()
    {
        SetDefault();
    }

    internal float acceleration1;
    internal float acceleration2;
    internal float acceleration3;

    public void SetDefault()
    {
        acceleration1 = 0.9f;
        acceleration2 = 2;
        acceleration3 = 700;
    }
}

public class Controls
{
    internal float movedx;
    internal float movedy;
    internal bool wantsjump;
    internal bool wantsjumphalf;
    internal bool moveup;
    internal bool movedown;
    internal bool shiftkeydown;
    internal bool freemove;
    internal bool noclip;
}
