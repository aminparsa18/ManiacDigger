
/// <summary>
/// Calculates base lighting for a single 16×16×16 chunk.
/// Handles sunlight seeding from the heightmap, sunlight flood-fill,
/// and light-emitting block propagation.
/// Does not spread light across chunk boundaries.
/// </summary>
public class LightBase
{
    /// <summary>Total number of blocks in a 16×16×16 chunk.</summary>
    private const int ChunkVolume = 16 * 16 * 16;

    /// <summary>Size of one chunk axis.</summary>
    private const int ChunkSize = 16;

    /// <summary>Reused flood-fill engine. Allocated once to avoid per-call allocation.</summary>
    private readonly LightFlood _flood;

    /// <summary>
    /// Reused flat block ID buffer, copied from the chunk before each light calculation
    /// so the original chunk data is never modified during lighting passes.
    /// </summary>
    private readonly int[] _workData;

    public LightBase()
    {
        _flood = new LightFlood();
        _workData = new int[ChunkVolume];
    }

    /// <summary>
    /// Recalculates base lighting for the chunk at chunk coordinates
    /// (<paramref name="cx"/>, <paramref name="cy"/>, <paramref name="cz"/>).
    /// Results are written directly into <c>chunk.baseLight</c>.
    /// </summary>
    /// <param name="game">The active game instance.</param>
    /// <param name="cx">Chunk X coordinate.</param>
    /// <param name="cy">Chunk Y coordinate.</param>
    /// <param name="cz">Chunk Z (vertical) coordinate.</param>
    /// <param name="dataLightRadius">Per-block-type light emission radius lookup.</param>
    /// <param name="transparentForLight">Per-block-type transparency flag lookup.</param>
    public void CalculateChunkBaseLight(
        Game game,
        int cx,
        int cy,
        int cz,
        int[] dataLightRadius,
        bool[] transparentForLight)
    {
        Chunk chunk = game.VoxelMap.GetChunk_(cx, cy, cz);

        // Copy block data into the working buffer via the chunk's unified accessor,
        // which handles both byte (data) and int (dataInt) storage transparently.
        for (int i = 0; i < ChunkVolume; i++)
            _workData[i] = chunk.GetBlockInChunk(i);

        byte[] workLight = chunk.baseLight;
        Array.Clear(workLight, 0, workLight.Length);

        Sunlight(game, cx, cy, cz, workLight, dataLightRadius, game.sunlight_);
        SunlightFlood(_workData, workLight, dataLightRadius, transparentForLight);
        LightEmitting(_workData, workLight, dataLightRadius, transparentForLight);
    }

    /// <summary>
    /// Converts a 3D grid coordinate into a flat array index using row-major order.
    /// </summary>
    /// <param name="x">Column index.</param>
    /// <param name="y">Row index.</param>
    /// <param name="z">Layer (depth) index.</param>
    /// <param name="sizeX">Number of columns per row.</param>
    /// <param name="sizeY">Number of rows per layer.</param>
    /// <returns>The corresponding flat array index.</returns>
    private static int Index3d(int x, int y, int z, int sizeX, int sizeY)
    {
        return (z * sizeY + y) * sizeX + x;
    }

    /// <summary>
    /// Seeds sunlight into the chunk from above, using the heightmap to determine
    /// where sunlight first enters each vertical column.
    /// Only columns where the light height falls within this chunk are affected.
    /// </summary>
    /// <param name="game">The active game instance.</param>
    /// <param name="cx">Chunk X coordinate.</param>
    /// <param name="cy">Chunk Y coordinate.</param>
    /// <param name="cz">Chunk Z coordinate.</param>
    /// <param name="workLight">The light buffer to seed into.</param>
    /// <param name="dataLightRadius">Per-block-type light emission radius lookup.</param>
    /// <param name="sunlight">The sunlight intensity value to seed.</param>
    private static void Sunlight(
        Game game,
        int cx,
        int cy,
        int cz,
        byte[] workLight,
        int[] dataLightRadius,
        int sunlight)
    {
        int baseHeight = cz * Game.chunksize;

        for (int xx = 0; xx < ChunkSize; xx++)
        {
            for (int yy = 0; yy < ChunkSize; yy++)
            {
                int height = GetLightHeight(game, cx, cy, xx, yy);

                // Convert world-space height to chunk-local Z.
                int z = height - baseHeight;
                if (z < 0) z = 0;
                if (z > ChunkSize) continue; // Sunlight enters above this chunk entirely.

                int pos = Index3d(xx, yy, z, ChunkSize, ChunkSize);

                // Fill all blocks from the entry point upward with full sunlight.
                for (int zz = z; zz < ChunkSize; zz++)
                {
                    workLight[pos] = (byte)(sunlight);
                    pos += LightFlood.ZPlus;
                }
            }
        }
    }

    /// <summary>
    /// Looks up the world-space light entry height for column (<paramref name="xx"/>, <paramref name="yy"/>)
    /// within the chunk at (<paramref name="cx"/>, <paramref name="cy"/>) from the heightmap.
    /// </summary>
    /// <param name="game">The active game instance.</param>
    /// <param name="cx">Chunk X coordinate.</param>
    /// <param name="cy">Chunk Y coordinate.</param>
    /// <param name="xx">Local column X (0–15).</param>
    /// <param name="yy">Local column Y (0–15).</param>
    /// <returns>
    ///     The world-space Z at which sunlight first enters this column,
    ///     or 0 if the heightmap chunk is not yet loaded.
    /// </returns>
    private static int GetLightHeight(Game game, int cx, int cy, int xx, int yy)
    {
        int[] heightmapChunk = game.d_Heightmap.GetChunk(cx * ChunkedMap2d.ChunkSize, cy * ChunkedMap2d.ChunkSize);

        if (heightmapChunk == null)
            return 0;

        return heightmapChunk[
            VectorIndexUtil.Index2d(xx % Game.chunksize, yy % Game.chunksize, Game.chunksize)];
    }

    /// <summary>
    /// Propagates sunlight across transparent block boundaries within the chunk.
    /// For each transparent block, checks its X+ and Y+ neighbours — if a light
    /// level mismatch exists between two adjacent transparent blocks, floods both
    /// to reconcile the difference.
    /// </summary>
    /// <param name="workData">Flat block ID buffer for this chunk.</param>
    /// <param name="workLight">Light level buffer, modified in place.</param>
    /// <param name="dataLightRadius">Per-block-type light emission radius lookup.</param>
    /// <param name="dataTransparent">Per-block-type transparency flag lookup.</param>
    private void SunlightFlood(int[] workData, byte[] workLight, int[] dataLightRadius, bool[] dataTransparent)
    {
        for (int xx = 0; xx < ChunkSize; xx++)
        {
            for (int yy = 0; yy < ChunkSize; yy++)
            {
                for (int zz = 0; zz < ChunkSize; zz++)
                {
                    int pos = Index3d(xx, yy, zz, ChunkSize, ChunkSize);

                    // Only propagate through transparent blocks.
                    if (!dataTransparent[workData[pos]])
                        continue;

                    int curLight = workLight[pos];
                    int posXNeighbour = pos + LightFlood.XPlus;
                    int posYNeighbour = pos + LightFlood.YPlus;

                    // Flood across X+ boundary if neighbour is transparent and has a different light level.
                    if (xx + 1 < ChunkSize
                        && workLight[posXNeighbour] != curLight
                        && dataTransparent[workData[posXNeighbour]])
                    {
                        _flood.FloodLight(workData, workLight, xx, yy, zz, dataLightRadius, dataTransparent);
                        _flood.FloodLight(workData, workLight, xx + 1, yy, zz, dataLightRadius, dataTransparent);
                    }

                    // Flood across Y+ boundary if neighbour is transparent and has a different light level.
                    if (yy + 1 < ChunkSize
                        && workLight[posYNeighbour] != curLight
                        && dataTransparent[workData[posYNeighbour]])
                    {
                        _flood.FloodLight(workData, workLight, xx, yy, zz, dataLightRadius, dataTransparent);
                        _flood.FloodLight(workData, workLight, xx, yy + 1, zz, dataLightRadius, dataTransparent);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Seeds light from emissive blocks and floods it outward.
    /// Skips blocks with ID below 10 as an optimisation (no block below that threshold emits light).
    /// Only floods from a block if its emission radius exceeds the current light level at that position.
    /// </summary>
    /// <param name="workData">Flat block ID buffer for this chunk.</param>
    /// <param name="workLight">Light level buffer, modified in place.</param>
    /// <param name="dataLightRadius">Per-block-type light emission radius lookup.</param>
    /// <param name="dataTransparent">Per-block-type transparency flag lookup.</param>
    private void LightEmitting(int[] workData, byte[] workLight, int[] dataLightRadius, bool[] dataTransparent)
    {
        for (int pos = 0; pos < ChunkVolume; pos++)
        {
            int blockId = workData[pos];

            // Optimisation: no block with ID < 10 emits light.
            if (blockId < 10)
                continue;

            int emitRadius = dataLightRadius[blockId];
            if (emitRadius == 0)
                continue;

            // Only flood if the block emits more light than is already present.
            if (emitRadius <= workLight[pos])
                continue;

            int xx = VectorIndexUtil.PosX(pos, ChunkSize, ChunkSize);
            int yy = VectorIndexUtil.PosY(pos, ChunkSize, ChunkSize);
            int zz = VectorIndexUtil.PosZ(pos, ChunkSize, ChunkSize);

            workLight[pos] = (byte)(Math.Max(emitRadius, workLight[pos]));
            _flood.FloodLight(workData, workLight, xx, yy, zz, dataLightRadius, dataTransparent);
        }
    }
}
