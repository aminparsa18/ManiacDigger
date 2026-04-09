public partial class Game
{
    // -------------------------------------------------------------------------
    // Block type queries
    // -------------------------------------------------------------------------

    public static bool IsEmptyForPhysics(Packet_BlockType block) =>
        block.DrawType == Packet_DrawTypeEnum.Ladder
        || (block.WalkableType != Packet_WalkableTypeEnum.Solid
            && block.WalkableType != Packet_WalkableTypeEnum.Fluid);

    public static bool IsTransparentForLight(Packet_BlockType b) =>
        b.DrawType != Packet_DrawTypeEnum.Solid
        && b.DrawType != Packet_DrawTypeEnum.ClosedDoor;

    internal bool IsWater(int blockType)
    {
        string name = blocktypes[blockType].Name;
        return name != null && name.Contains("Water"); // TODO: use block property instead of name
    }

    internal bool IsLava(int blockType)
    {
        string name = blocktypes[blockType].Name;
        return name != null && name.Contains("Lava"); // TODO: use block property instead of name
    }

    internal bool IsValid(int blocktype) => blocktypes[blocktype].Name != null;

    internal bool IsFillBlock(int blocktype) =>
        blocktype == d_Data.BlockIdFillArea()
        || blocktype == d_Data.BlockIdFillStart()
        || blocktype == d_Data.BlockIdCuboid();

    internal bool IsUsableBlock(int blocktype) =>
        d_Data.IsRailTile(blocktype) || blocktypes[blocktype].IsUsable;

    internal bool IsTileEmptyForPhysics(int x, int y, int z)
    {
        if (z >= map.MapSizeZ) return true;
        if (x < 0 || y < 0 || z < 0) return controls.freemove;
        if (x >= map.MapSizeX || y >= map.MapSizeY) return controls.freemove;

        int block = map.GetBlockValid(x, y, z);
        return block == SpecialBlockId.Empty
            || block == d_Data.BlockIdFillArea()
            || IsWater(block);
    }

    internal bool IsTileEmptyForPhysicsClose(int x, int y, int z) =>
        IsTileEmptyForPhysics(x, y, z)
        || (map.IsValidPos(x, y, z) && blocktypes[map.GetBlock(x, y, z)].DrawType == Packet_DrawTypeEnum.HalfHeight)
        || (map.IsValidPos(x, y, z) && IsEmptyForPhysics(blocktypes[map.GetBlock(x, y, z)]));

    // -------------------------------------------------------------------------
    // Block manipulation
    // -------------------------------------------------------------------------

    internal void SetBlock(int x, int y, int z, int tileType)
    {
        map.SetBlockRaw(x, y, z, tileType);
        map.SetChunkDirty(x / chunksize, y / chunksize, z / chunksize, true, true);
        ShadowsOnSetBlock(x, y, z);
        lastplacedblockX = x;
        lastplacedblockY = y;
        lastplacedblockZ = z;
    }

    internal void SetTileAndUpdate(int x, int y, int z, int type)
    {
        SetBlock(x, y, z, type);
        RedrawBlock(x, y, z);
    }

    internal void RedrawBlock(int x, int y, int z) => map.SetBlockDirty(x, y, z);

    internal void RedrawAllBlocks() => shouldRedrawAllBlocks = true;

    // -------------------------------------------------------------------------
    // Lighting / shadows
    // -------------------------------------------------------------------------

    public int GetLight(int x, int y, int z)
    {
        int light = map.MaybeGetLight(x, y, z);
        if (light != -1)
            return light;

        if (x >= 0 && x < map.MapSizeX
            && y >= 0 && y < map.MapSizeY
            && z >= d_Heightmap.GetBlock(x, y))
            return sunlight_;

        return minlight;
    }

    internal void UpdateColumnHeight(int x, int y)
    {
        // TODO: optimize
        int height = map.MapSizeZ - 1;
        for (int i = map.MapSizeZ - 1; i >= 0; i--)
        {
            height = i;
            if (!IsTransparentForLight(blocktypes[map.GetBlock(x, y, i)]))
                break;
        }
        d_Heightmap.SetBlock(x, y, height);
    }

    internal void ShadowsOnSetBlock(int x, int y, int z)
    {
        int oldheight = d_Heightmap.GetBlock(x, y);
        UpdateColumnHeight(x, y);
        int newheight = d_Heightmap.GetBlock(x, y);

        int min = Math.Min(oldheight, newheight);
        int max = Math.Max(oldheight, newheight);
        for (int i = min; i < max; i++)
        {
            if (i / chunksize != z / chunksize)
                map.SetChunkDirty(x / chunksize, y / chunksize, i / chunksize, true, true);
        }

        // TODO: too many redraws — placing a block currently updates 27 chunks,
        // each recalculating light from 27 chunks (729x overhead).
        for (int xx = 0; xx < 3; xx++)
            for (int yy = 0; yy < 3; yy++)
                for (int zz = 0; zz < 3; zz++)
                {
                    int cx = x / chunksize + xx - 1;
                    int cy = y / chunksize + yy - 1;
                    int cz = z / chunksize + zz - 1;
                    if (map.IsValidChunkPos(cx, cy, cz))
                        map.SetChunkDirty(cx, cy, cz, true, false);
                }
    }

    // -------------------------------------------------------------------------
    // Block health
    // -------------------------------------------------------------------------

    internal float GetCurrentBlockHealth(int x, int y, int z)
    {
        if (blockHealth.TryGetValue((x, y, z), out float health))
            return health;

        return d_Data.Strength()[map.GetBlock(x, y, z)];
    }

    // -------------------------------------------------------------------------
    // Speculative block placement
    // -------------------------------------------------------------------------

    internal void SendSetBlockAndUpdateSpeculative(int material, int x, int y, int z, int mode)
    {
        SendSetBlock(x, y, z, mode, material, ActiveMaterial);

        Packet_Item item = d_Inventory.RightHand[ActiveMaterial];
        if (item == null || item.ItemClass != Packet_ItemClassEnum.Block)
            return;

        int blockid = mode == Packet_BlockSetModeEnum.Destroy ? SpecialBlockId.Empty : material;
        AddSpeculative(new Speculative
        {
            x = x,
            y = y,
            z = z,
            blocktype = map.GetBlock(x, y, z),
            timeMilliseconds = platform.TimeMillisecondsFromStart()
        });
        SetBlock(x, y, z, blockid);
        RedrawBlock(x, y, z);
    }

    private void AddSpeculative(Speculative s_)
    {
        for (int i = 0; i < speculativeCount; i++)
        {
            if (speculative[i] == null)
            {
                speculative[i] = s_;
                return;
            }
        }
        speculative[speculativeCount++] = s_;
    }

    internal void RevertSpeculative(float dt)
    {
        for (int i = 0; i < speculativeCount; i++)
        {
            Speculative s_ = speculative[i];
            if (s_ == null) continue;

            if ((platform.TimeMillisecondsFromStart() - s_.timeMilliseconds) / 1000f > 2)
            {
                RedrawBlock(s_.x, s_.y, s_.z);
                speculative[i] = null;
            }
        }
    }

    // -------------------------------------------------------------------------
    // Fog
    // -------------------------------------------------------------------------

    internal void ToggleFog()
    {
        int[] drawDistances = [32, 64, 128, 256, 512];
        int count = drawDistances.Count(d => d <= maxdrawdistance || d == 32);

        for (int i = 0; i < count; i++)
        {
            if (d_Config3d.viewdistance == drawDistances[i])
            {
                d_Config3d.viewdistance = drawDistances[(i + 1) % count];
                RedrawAllBlocks();
                return;
            }
        }
        d_Config3d.viewdistance = drawDistances[0];
        RedrawAllBlocks();
    }

    internal void SetFog()
    {
        if (d_Config3d.viewdistance >= 512)
            return;

        float density = one * 25 / 10000; // 0.0025f

        int fogR, fogG, fogB, fogA;
        if (SkySphereNight && !shadowssimple)
        {
            fogR = fogG = fogB = 0;
            fogA = 255;
        }
        else
        {
            fogR = clearcolorR;
            fogG = clearcolorG;
            fogB = clearcolorB;
            fogA = clearcolorA;
        }

        platform.GlEnableFog();
        platform.GlFogFogColor(fogR, fogG, fogB, fogA);
        platform.GlFogFogDensity(density);
    }

    // -------------------------------------------------------------------------
    // Map events
    // -------------------------------------------------------------------------

    internal void MapLoaded()
    {
        RedrawAllBlocks();
        materialSlots = d_Data.DefaultMaterialSlots();
        GuiStateBackToGame();

        playerPositionSpawnX = player.position.x;
        playerPositionSpawnY = player.position.y;
        playerPositionSpawnZ = player.position.z;
    }

    public float WaterLevel() => map.MapSizeZ / 2f;
}