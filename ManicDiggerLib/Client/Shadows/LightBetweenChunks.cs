/// <summary>
/// Calculates final lighting for a chunk by sampling base light from the
/// 3×3×3 neighbourhood of chunks surrounding it, flooding light across
/// chunk boundaries, then writing the result into the chunk's render buffer.
/// </summary>
public class LightBetweenChunks
{
    /// <summary>Number of chunks along one axis of the neighbourhood.</summary>
    private const int NeighbourhoodSize = 3;

    /// <summary>Total number of chunks in the 3×3×3 neighbourhood.</summary>
    private const int NeighbourhoodVolume = NeighbourhoodSize * NeighbourhoodSize * NeighbourhoodSize;

    /// <summary>Number of blocks along one axis of a chunk.</summary>
    private const int ChunkSize = 16;

    /// <summary>Total number of blocks in a single chunk.</summary>
    private const int ChunkVolume = ChunkSize * ChunkSize * ChunkSize;

    /// <summary>
    /// Size of the output light buffer per axis (chunk + 1 block border on each side).
    /// </summary>
    private const int OutputSize = 18;

    /// <summary>Reused flood-fill engine. Allocated once to avoid per-call allocation.</summary>
    private readonly LightFlood _flood;

    /// <summary>
    /// Per-chunk light buffers for the 3×3×3 neighbourhood.
    /// Indexed by <see cref="Index3d"/> with size 3.
    /// </summary>
    private readonly byte[][] _chunksLight;

    /// <summary>
    /// Per-chunk block ID buffers for the 3×3×3 neighbourhood.
    /// Indexed by <see cref="Index3d"/> with size 3.
    /// </summary>
    private readonly int[][] _chunksData;

    /// <summary>
    /// Initialises the calculator, pre-allocating all neighbourhood buffers
    /// to avoid per-frame allocation.
    /// </summary>
    public LightBetweenChunks()
    {
        _flood = new LightFlood();

        _chunksLight = new byte[NeighbourhoodVolume][];
        _chunksData = new int[NeighbourhoodVolume][];

        for (int i = 0; i < NeighbourhoodVolume; i++)
        {
            _chunksLight[i] = new byte[ChunkVolume];
            _chunksData[i] = new int[ChunkVolume];
        }
    }

    /// <summary>
    /// Calculates final lighting for the chunk at
    /// (<paramref name="cx"/>, <paramref name="cy"/>, <paramref name="cz"/>)
    /// by loading base light from its 3×3×3 neighbourhood, flooding light
    /// across chunk boundaries, then writing the result into the chunk's render buffer.
    /// </summary>
    /// <param name="game">The active game instance.</param>
    /// <param name="cx">Chunk X coordinate of the target chunk.</param>
    /// <param name="cy">Chunk Y coordinate of the target chunk.</param>
    /// <param name="cz">Chunk Z coordinate of the target chunk.</param>
    /// <param name="dataLightRadius">Per-block-type light emission radius lookup.</param>
    /// <param name="dataTransparent">Per-block-type transparency flag lookup.</param>
    public void CalculateLightBetweenChunks(
        Game game,
        int cx,
        int cy,
        int cz,
        int[] dataLightRadius,
        bool[] dataTransparent)
    {
        Input(game, cx, cy, cz);
        FloodBetweenChunks(dataLightRadius, dataTransparent);
        Output(game, cx, cy, cz);
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
    /// Loads block and base light data from each chunk in the 3×3×3 neighbourhood
    /// into the working buffers. Out-of-bounds or unloaded chunks are zeroed.
    /// </summary>
    /// <param name="game">The active game instance.</param>
    /// <param name="cx">Target chunk X coordinate.</param>
    /// <param name="cy">Target chunk Y coordinate.</param>
    /// <param name="cz">Target chunk Z coordinate.</param>
    private void Input(Game game, int cx, int cy, int cz)
    {
        for (int x = 0; x < NeighbourhoodSize; x++)
        {
            for (int y = 0; y < NeighbourhoodSize; y++)
            {
                for (int z = 0; z < NeighbourhoodSize; z++)
                {
                    int slotIndex = Index3d(x, y, z, NeighbourhoodSize, NeighbourhoodSize);
                    int pcx = cx + x - 1;
                    int pcy = cy + y - 1;
                    int pcz = cz + z - 1;

                    // Zero out buffers for missing or out-of-bounds chunks.
                    if (!game.VoxelMap.IsValidChunkPos(pcx, pcy, pcz))
                    {
                        Array.Clear(_chunksData[slotIndex], 0, ChunkVolume);
                        Array.Clear(_chunksLight[slotIndex], 0, ChunkVolume);
                        continue;
                    }

                    Chunk chunk = game.VoxelMap.GetChunk_(pcx, pcy, pcz);
                    int[] dataSlot = _chunksData[slotIndex];
                    byte[] lightSlot = _chunksLight[slotIndex];

                    // Copy block data via the chunk's unified accessor.
                    for (int i = 0; i < ChunkVolume; i++)
                        dataSlot[i] = chunk.GetBlock(i);

                    // Copy base light computed by LightBase.
                    Array.Copy(chunk.baseLight, lightSlot, ChunkVolume);
                }
            }
        }
    }

    /// <summary>
    /// Floods light across all 6 faces of each chunk in the neighbourhood,
    /// running two passes to allow light to propagate further than one chunk boundary.
    /// </summary>
    /// <param name="dataLightRadius">Per-block-type light emission radius lookup.</param>
    /// <param name="dataTransparent">Per-block-type transparency flag lookup.</param>
    private void FloodBetweenChunks(int[] dataLightRadius, bool[] dataTransparent)
    {
        // Two passes ensure light can propagate across more than one chunk boundary.
        for (int pass = 0; pass < 2; pass++)
        {
            for (int x = 0; x < NeighbourhoodSize; x++)
            {
                for (int y = 0; y < NeighbourhoodSize; y++)
                {
                    for (int z = 0; z < NeighbourhoodSize; z++)
                    {
                        byte[] cLight = _chunksLight[Index3d(x, y, z, NeighbourhoodSize, NeighbourhoodSize)];

                        // Z+ face: top edge of this chunk → bottom edge of chunk above.
                        if (z < 2)
                        {
                            byte[] dcLight = _chunksLight[Index3d(x, y, z + 1, NeighbourhoodSize, NeighbourhoodSize)];
                            for (int xx = 0; xx < ChunkSize; xx++)
                                for (int yy = 0; yy < ChunkSize; yy++)
                                    FloodAcrossBoundary(cLight, dcLight, x, y, z, x, y, z + 1, xx, yy, 15, xx, yy, 0, dataLightRadius, dataTransparent);
                        }

                        // Z- face: bottom edge of this chunk → top edge of chunk below.
                        if (z > 0)
                        {
                            byte[] dcLight = _chunksLight[Index3d(x, y, z - 1, NeighbourhoodSize, NeighbourhoodSize)];
                            for (int xx = 0; xx < ChunkSize; xx++)
                                for (int yy = 0; yy < ChunkSize; yy++)
                                    FloodAcrossBoundary(cLight, dcLight, x, y, z, x, y, z - 1, xx, yy, 0, xx, yy, 15, dataLightRadius, dataTransparent);
                        }

                        // X+ face: right edge → left edge of chunk to the right.
                        if (x < 2)
                        {
                            byte[] dcLight = _chunksLight[Index3d(x + 1, y, z, NeighbourhoodSize, NeighbourhoodSize)];
                            for (int yy = 0; yy < ChunkSize; yy++)
                                for (int zz = 0; zz < ChunkSize; zz++)
                                    FloodAcrossBoundary(cLight, dcLight, x, y, z, x + 1, y, z, 15, yy, zz, 0, yy, zz, dataLightRadius, dataTransparent);
                        }

                        // X- face: left edge → right edge of chunk to the left.
                        if (x > 0)
                        {
                            byte[] dcLight = _chunksLight[Index3d(x - 1, y, z, NeighbourhoodSize, NeighbourhoodSize)];
                            for (int yy = 0; yy < ChunkSize; yy++)
                                for (int zz = 0; zz < ChunkSize; zz++)
                                    FloodAcrossBoundary(cLight, dcLight, x, y, z, x - 1, y, z, 0, yy, zz, 15, yy, zz, dataLightRadius, dataTransparent);
                        }

                        // Y+ face: front edge → back edge of chunk in front.
                        if (y < 2)
                        {
                            byte[] dcLight = _chunksLight[Index3d(x, y + 1, z, NeighbourhoodSize, NeighbourhoodSize)];
                            for (int xx = 0; xx < ChunkSize; xx++)
                                for (int zz = 0; zz < ChunkSize; zz++)
                                    FloodAcrossBoundary(cLight, dcLight, x, y, z, x, y + 1, z, xx, 15, zz, xx, 0, zz, dataLightRadius, dataTransparent);
                        }

                        // Y- face: back edge → front edge of chunk behind.
                        if (y > 0)
                        {
                            byte[] dcLight = _chunksLight[Index3d(x, y - 1, z, NeighbourhoodSize, NeighbourhoodSize)];
                            for (int xx = 0; xx < ChunkSize; xx++)
                                for (int zz = 0; zz < ChunkSize; zz++)
                                    FloodAcrossBoundary(cLight, dcLight, x, y, z, x, y - 1, z, xx, 0, zz, xx, 15, zz, dataLightRadius, dataTransparent);
                        }
                    }
                }
            }
        }
    }

    /// <summary>
    /// Propagates light from a single source cell on one chunk face into the
    /// adjacent cell on the neighbouring chunk face, then flood-fills from
    /// the destination if the light level improves.
    /// </summary>
    /// <param name="srcLight">Light buffer of the source chunk.</param>
    /// <param name="dstLight">Light buffer of the destination chunk.</param>
    /// <param name="scx">Source chunk neighbourhood X.</param>
    /// <param name="scy">Source chunk neighbourhood Y.</param>
    /// <param name="scz">Source chunk neighbourhood Z.</param>
    /// <param name="dcx">Destination chunk neighbourhood X.</param>
    /// <param name="dcy">Destination chunk neighbourhood Y.</param>
    /// <param name="dcz">Destination chunk neighbourhood Z.</param>
    /// <param name="xx">Source cell local X.</param>
    /// <param name="yy">Source cell local Y.</param>
    /// <param name="zz">Source cell local Z.</param>
    /// <param name="dxx">Destination cell local X.</param>
    /// <param name="dyy">Destination cell local Y.</param>
    /// <param name="dzz">Destination cell local Z.</param>
    /// <param name="dataLightRadius">Per-block-type light emission radius lookup.</param>
    /// <param name="dataTransparent">Per-block-type transparency flag lookup.</param>
    private void FloodAcrossBoundary(
        byte[] srcLight,
        byte[] dstLight,
        int scx, int scy, int scz,
        int dcx, int dcy, int dcz,
        int xx, int yy, int zz,
        int dxx, int dyy, int dzz,
        int[] dataLightRadius,
        bool[] dataTransparent)
    {
        int srcIndex = Index3d(xx, yy, zz, ChunkSize, ChunkSize);
        int dstIndex = Index3d(dxx, dyy, dzz, ChunkSize, ChunkSize);

        int sourceLight = srcLight[srcIndex];
        int targetLight = dstLight[dstIndex];

        // Only propagate if the destination would gain light.
        if (targetLight >= sourceLight - 1)
            return;

        dstLight[dstIndex] = (byte)(sourceLight - 1);
        _flood.FloodLight(
            _chunksData[Index3d(dcx, dcy, dcz, NeighbourhoodSize, NeighbourhoodSize)],
            dstLight, dxx, dyy, dzz, dataLightRadius, dataTransparent);
    }

    /// <summary>
    /// Writes the final computed light values from the neighbourhood buffers
    /// into the target chunk's <see cref="RenderedChunk.light"/> buffer,
    /// covering an 18×18×18 region (chunk + 1 block border on each side).
    /// </summary>
    /// <param name="game">The active game instance.</param>
    /// <param name="cx">Target chunk X coordinate.</param>
    /// <param name="cy">Target chunk Y coordinate.</param>
    /// <param name="cz">Target chunk Z coordinate.</param>
    private void Output(Game game, int cx, int cy, int cz)
    {
        Chunk chunk = game.VoxelMap.GetChunk_(cx, cy, cz);

        for (int x = 0; x < OutputSize; x++)
        {
            for (int y = 0; y < OutputSize; y++)
            {
                for (int z = 0; z < OutputSize; z++)
                {
                    // Map output buffer coordinates to neighbourhood chunk + local position.
                    int globalX = Game.chunksize - 1 + x;
                    int globalY = Game.chunksize - 1 + y;
                    int globalZ = Game.chunksize - 1 + z;

                    int ncx = globalX / ChunkSize;
                    int ncy = globalY / ChunkSize;
                    int ncz = globalZ / ChunkSize;

                    int localX = globalX % ChunkSize;
                    int localY = globalY % ChunkSize;
                    int localZ = globalZ % ChunkSize;

                    byte light = _chunksLight
                        [Index3d(ncx, ncy, ncz, NeighbourhoodSize, NeighbourhoodSize)]
                        [Index3d(localX, localY, localZ, ChunkSize, ChunkSize)];

                    chunk.rendered.Light[Index3d(x, y, z, OutputSize, OutputSize)] = light;
                }
            }
        }
    }
}