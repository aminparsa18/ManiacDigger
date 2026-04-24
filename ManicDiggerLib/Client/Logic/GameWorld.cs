using ManicDigger;

public partial class Game
{
    // ── Constants ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Seconds before a speculative block placement is reverted if no server
    /// confirmation arrives.
    /// </summary>
    private const float SpeculativeTimeoutSeconds = 2f;

    /// <summary>
    /// Available view-distance steps for <see cref="ToggleFog"/>.
    /// Static so the array is allocated once rather than on every key press.
    /// </summary>
    private static readonly int[] FogDrawDistances = [32, 64, 128, 256, 512];

    // ── Block type queries ────────────────────────────────────────────────────

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="block"/> does not
    /// obstruct player movement (ladders and non-solid, non-fluid draw types).
    /// </summary>
    public static bool IsEmptyForPhysics(Packet_BlockType block) =>
        block.DrawType == DrawType.Ladder
        || (block.WalkableType != WalkableType.Solid
            && block.WalkableType != WalkableType.Fluid);

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="b"/> allows light
    /// to pass through it (everything except solid blocks and closed doors).
    /// </summary>
    public static bool IsTransparentForLight(Packet_BlockType b) =>
        b.DrawType != DrawType.Solid
        && b.DrawType != DrawType.ClosedDoor;

    /// <summary>Returns <see langword="true"/> when the block name contains "Water".</summary>
    /// <remarks>TODO: replace name-based check with a dedicated block property.</remarks>
    public bool IsWater(int blockType)
    {
        string name = BlockTypes[blockType].Name;
        return name != null && name.Contains("Water");
    }

    /// <summary>Returns <see langword="true"/> when the block name contains "Lava".</summary>
    /// <remarks>TODO: replace name-based check with a dedicated block property.</remarks>
    internal bool IsLava(int blockType)
    {
        string name = BlockTypes[blockType].Name;
        return name != null && name.Contains("Lava");
    }

    /// <summary>Returns <see langword="true"/> when the block at this ID has a name assigned.</summary>
    public bool IsValid(int blocktype) => BlockTypes[blocktype].Name != null;

    /// <summary>
    /// Returns <see langword="true"/> when <paramref name="blocktype"/> is one of
    /// the fill/cuboid tool blocks that should not be treated as real terrain.
    /// </summary>
    internal bool IsFillBlock(int blocktype) =>
        blocktype == BlockRegistry.BlockIdFillArea
        || blocktype == BlockRegistry.BlockIdFillStart
        || blocktype == BlockRegistry.BlockIdCuboid;

    /// <summary>
    /// Returns <see langword="true"/> when the block can be interacted with
    /// (rail tiles or blocks with the <c>IsUsable</c> flag).
    /// </summary>
    public bool IsUsableBlock(int blocktype) =>
        BlockRegistry.IsRailTile(blocktype) || BlockTypes[blocktype].IsUsable;

    /// <summary>
    /// Returns <see langword="true"/> when the tile at the given position does
    /// not physically obstruct the player (air, fill-area blocks, water, or
    /// out-of-bounds positions when in freemove mode).
    /// </summary>
    public bool IsTileEmptyForPhysics(int x, int y, int z)
    {
        if (z >= VoxelMap.MapSizeZ) return true;
        if (x < 0 || y < 0 || z < 0) return Controls.freemove;
        if (x >= VoxelMap.MapSizeX || y >= VoxelMap.MapSizeY) return Controls.freemove;

        int block = VoxelMap.GetBlockValid(x, y, z);
        return block == SpecialBlockId.Empty
            || block == BlockRegistry.BlockIdFillArea
            || IsWater(block);
    }

    /// <summary>
    /// Extended empty-for-physics check that also treats half-height blocks and
    /// blocks with <see cref="IsEmptyForPhysics"/> draw types as passable.
    /// Caches the block value and block-type lookup to avoid the double
    /// <c>IsValidPos</c> + double <c>GetBlock</c> call in the original.
    /// </summary>
    internal bool IsTileEmptyForPhysicsClose(int x, int y, int z)
    {
        if (IsTileEmptyForPhysics(x, y, z)) return true;
        if (!VoxelMap.IsValidPos(x, y, z)) return false;

        Packet_BlockType bt = BlockTypes[VoxelMap.GetBlock(x, y, z)];
        return bt.DrawType == DrawType.HalfHeight || IsEmptyForPhysics(bt);
    }

    // ── Block manipulation ────────────────────────────────────────────────────

    /// <summary>
    /// Writes a block directly into the map, marks the owning chunk dirty,
    /// updates shadow heights, and records the position for the next chunk redraw.
    /// </summary>
    public void SetBlock(int x, int y, int z, int tileType)
    {
        VoxelMap.SetBlockRaw(x, y, z, tileType);
        VoxelMap.SetChunkDirty(x / chunksize, y / chunksize, z / chunksize, true, true);
        ShadowsOnSetBlock(x, y, z);
        LastplacedblockX = x;
        LastplacedblockY = y;
        LastplacedblockZ = z;
    }

    /// <summary>Writes a block and immediately marks all affected neighbour chunks for redraw.</summary>
    public void SetTileAndUpdate(int x, int y, int z, int type)
    {
        SetBlock(x, y, z, type);
        RedrawBlock(x, y, z);
    }

    /// <summary>Marks the chunk containing the given block as dirty for re-tessellation.</summary>
    internal void RedrawBlock(int x, int y, int z) => VoxelMap.SetBlockDirty(x, y, z);

    /// <summary>Schedules a full-world re-tessellation on the next frame.</summary>
    public void RedrawAllBlocks() => ShouldRedrawAllBlocks = true;

    // ── Lighting / shadows ────────────────────────────────────────────────────

    /// <summary>
    /// Returns the baked light level at a block position, falling back to
    /// sunlight when above the heightmap or to <see cref="minlight"/> when
    /// lighting data is unavailable.
    /// </summary>
    public int GetLight(int x, int y, int z)
    {
        int light = VoxelMap.MaybeGetLight(x, y, z);
        if (light != -1) return light;

        if (x >= 0 && x < VoxelMap.MapSizeX
         && y >= 0 && y < VoxelMap.MapSizeY
         && z >= Heightmap.GetBlock(x, y))
            return Sunlight;

        return minlight;
    }

    /// <summary>
    /// Recalculates the heightmap entry for column (<paramref name="x"/>,
    /// <paramref name="y"/>) by scanning downward for the first opaque block.
    /// </summary>
    internal void UpdateColumnHeight(int x, int y)
    {
        // TODO: optimize — full column scan on every block change is O(MapSizeZ).
        int height = VoxelMap.MapSizeZ - 1;
        for (int i = VoxelMap.MapSizeZ - 1; i >= 0; i--)
        {
            height = i;
            if (!IsTransparentForLight(BlockTypes[VoxelMap.GetBlock(x, y, i)]))
                break;
        }
        Heightmap.SetBlock(x, y, height);
    }

    /// <summary>
    /// Updates heightmap and marks all chunks whose lighting may have changed
    /// due to a block placement or removal at (<paramref name="x"/>,
    /// <paramref name="y"/>, <paramref name="z"/>).
    /// </summary>
    internal void ShadowsOnSetBlock(int x, int y, int z)
    {
        int oldheight = Heightmap.GetBlock(x, y);
        UpdateColumnHeight(x, y);
        int newheight = Heightmap.GetBlock(x, y);

        // Dirty chunks between the old and new heightmap values in this column.
        int min = Math.Min(oldheight, newheight);
        int max = Math.Max(oldheight, newheight);
        for (int i = min; i < max; i++)
        {
            if (i / chunksize != z / chunksize)
                VoxelMap.SetChunkDirty(x / chunksize, y / chunksize, i / chunksize, true, true);
        }

        // TODO: too many redraws — placing a block currently updates 27 chunks,
        // each recalculating light from 27 chunks (729× overhead).
        for (int xx = 0; xx < 3; xx++)
            for (int yy = 0; yy < 3; yy++)
                for (int zz = 0; zz < 3; zz++)
                {
                    int cx = x / chunksize + xx - 1;
                    int cy = y / chunksize + yy - 1;
                    int cz = z / chunksize + zz - 1;
                    if (VoxelMap.IsValidChunkPos(cx, cy, cz))
                        VoxelMap.SetChunkDirty(cx, cy, cz, true, false);
                }
    }

    // ── Block health ──────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the remaining health of the block at the given position,
    /// falling back to the block type's base strength when not yet damaged.
    /// </summary>
    public float GetCurrentBlockHealth(int x, int y, int z) =>
        blockHealth.TryGetValue((x, y, z), out float health)
            ? health
            : BlockRegistry.Strength[VoxelMap.GetBlock(x, y, z)];

    // ── Speculative block placement ───────────────────────────────────────────

    /// <summary>
    /// Sends a block-set packet to the server and applies the change
    /// speculatively on the client so the player sees immediate feedback.
    /// The speculative change is reverted by <see cref="RevertSpeculative"/>
    /// if the server does not confirm it within <see cref="SpeculativeTimeoutSeconds"/>.
    /// </summary>
    internal void SendSetBlockAndUpdateSpeculative(int material, int x, int y, int z, PacketBlockSetMode mode)
    {
        SendSetBlock(x, y, z, mode, material, ActiveMaterial);

        Packet_Item item = Inventory.RightHand[ActiveMaterial];
        if (item == null || item.ItemClass != ItemClass.Block)
            return;

        int blockid = mode == PacketBlockSetMode.Destroy ? SpecialBlockId.Empty : material;
        AddSpeculative(new Speculative
        {
            x = x,
            y = y,
            z = z,
            blocktype = VoxelMap.GetBlock(x, y, z),
            timeMilliseconds = Platform.TimeMillisecondsFromStart,
        });
        SetBlock(x, y, z, blockid);
        RedrawBlock(x, y, z);
    }

    /// <summary>
    /// Adds a speculative entry to the first free slot or appends one if
    /// all existing slots are occupied.
    /// </summary>
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

    /// <summary>
    /// Reverts any speculative block placements that have not been confirmed
    /// by the server within <see cref="SpeculativeTimeoutSeconds"/>.
    /// Called once per fixed tick.
    /// </summary>
    internal void RevertSpeculative(float dt)
    {
        int now = Platform.TimeMillisecondsFromStart;
        for (int i = 0; i < speculativeCount; i++)
        {
            Speculative s_ = speculative[i];
            if (s_ == null) continue;

            if ((now - s_.timeMilliseconds) / 1000f > SpeculativeTimeoutSeconds)
            {
                RedrawBlock(s_.x, s_.y, s_.z);
                speculative[i] = null;
            }
        }
    }

    // ── Fog ───────────────────────────────────────────────────────────────────

    /// <summary>
    /// Cycles the view distance through <see cref="FogDrawDistances"/>, skipping
    /// values that exceed <see cref="maxdrawdistance"/> (except 32, which is
    /// always available as the minimum step).
    /// </summary>
    public void ToggleFog()
    {
        int count = FogDrawDistances.Count(d => d <= maxdrawdistance || d == 32);
        for (int i = 0; i < count; i++)
        {
            if (Config3d.ViewDistance == FogDrawDistances[i])
            {
                Config3d.ViewDistance = FogDrawDistances[(i + 1) % count];
                RedrawAllBlocks();
                return;
            }
        }
        Config3d.ViewDistance = FogDrawDistances[0];
        RedrawAllBlocks();
    }

    /// <summary>
    /// Applies the current fog colour and density to OpenGL.
    /// Fog is disabled at maximum draw distance.
    /// At night (and with full shadows enabled) the fog colour is black.
    /// </summary>
    internal void SetFog()
    {
        if (Config3d.ViewDistance >= 512) return;

        const float density = 0.0025f; // 25 / 10000

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

        Platform.GlEnableFog();
        Platform.GlFogFogColor(fogR, fogG, fogB, fogA);
        Platform.GlFogFogDensity(density);
    }

    // ── Map events ────────────────────────────────────────────────────────────

    /// <summary>
    /// Called when the server finishes sending map data.
    /// Triggers a full redraw, resets the material bar, and records the
    /// initial spawn position.
    /// </summary>
    public void MapLoaded()
    {
        RedrawAllBlocks();
        materialSlots = BlockRegistry.DefaultMaterialSlots;
        GuiStateBackToGame();

        PlayerPositionSpawnX = Player.position.x;
        PlayerPositionSpawnY = Player.position.y;
        PlayerPositionSpawnZ = Player.position.z;
    }

    /// <summary>Returns the Z coordinate of the water surface (half the map height).</summary>
    public float WaterLevel() => VoxelMap.MapSizeZ / 2f;
}