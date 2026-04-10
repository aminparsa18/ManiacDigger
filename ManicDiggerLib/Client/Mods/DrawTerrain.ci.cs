using OpenTK.Mathematics;

/// <summary>
/// Client-side mod responsible for tessellating, lighting, and drawing the voxel terrain.
/// Runs chunk redraw logic on a background thread and commits GPU uploads on the main thread.
/// </summary>
public class ModDrawTerrain : ModBase
{
    /// <summary>Maximum light level used throughout the lighting system.</summary>
    public static int MaxLight() => 15;

    /// <summary>
    /// Half of √3, used to compute the bounding-sphere radius of a chunk
    /// (<c>√3 / 2 × chunkSize</c> is the circumradius of a cube).
    /// </summary>
    private float _sqrt3Half;

    /// <summary>
    /// Sentinel value meaning "no chunk found" when returned via the
    /// <see cref="_tempNearestPos"/> array by <see cref="NearestDirty"/>.
    /// </summary>
    private const int NoChunk = -1;

    /// <summary>Substitute for <see cref="int.MaxValue"/> safe inside <c>unchecked</c> contexts.</summary>
    private const int IntMaxValue = 2147483647;

    /// <summary>
    /// Size of the extended (buffered) chunk buffer in one axis.
    /// A 16³ chunk needs one block of overlap on each side → 18.
    /// </summary>
    private const int BufferedChunkEdge = 18;

    /// <summary>Volume of the extended chunk buffer (<see cref="BufferedChunkEdge"/>³).</summary>
    private const int BufferedChunkVolume = BufferedChunkEdge * BufferedChunkEdge * BufferedChunkEdge;

    /// <summary>Reference to the current game instance, refreshed every frame.</summary>
    internal Game _game;

    /// <summary>Reusable lighting helper for computing per-block base light within a chunk.</summary>
    private readonly LightBase _lightBase;

    /// <summary>Reusable lighting helper for propagating light across chunk boundaries.</summary>
    private readonly LightBetweenChunks _lightBetweenChunks;

    /// <summary>
    /// <see langword="true"/> after <see cref="StartTerrain"/> has been called and
    /// the tessellator is ready to accept chunk redraw requests.
    /// </summary>
    private bool _terrainStarted;


    /// <summary>Chunk edge length in blocks, copied from <see cref="Game.chunksize"/> at startup.</summary>
    internal int chunksize;

    /// <summary>
    /// Extended chunk edge length (<c>chunksize + 2</c>).
    /// The +2 accounts for the one-block border needed on each side.
    /// </summary>
    internal int bufferedChunkSize;

    /// <summary>Reciprocal of <see cref="chunksize"/>, used to convert block coords to chunk coords.</summary>
    internal float invertedChunkSize;

    /// <summary>
    /// Pending chunk redraws produced on the background thread, waiting to be
    /// uploaded to the GPU on the main thread in <see cref="MainThreadCommit"/>.
    /// </summary>
    private readonly TerrainRendererRedraw[] _redrawQueue;

    /// <summary>Number of valid entries in <see cref="_redrawQueue"/>.</summary>
    private int _redrawQueueCount;

    /// <summary>
    /// Extended (18³) block-ID buffer for the chunk currently being tessellated.
    /// Filled by <see cref="GetExtendedChunk"/> before tessellation.
    /// </summary>
    private readonly int[] _currentChunk;

    /// <summary>
    /// Per-block light values for the chunk currently being tessellated.
    /// Filled by <see cref="CalculateShadows"/> before tessellation.
    /// </summary>
    private readonly byte[] _currentChunkShadows;

    /// <summary>Scratch array of batcher IDs built during <see cref="DoRedraw"/>.</summary>
    private readonly int[] _batcherIds;

    /// <summary>Number of valid entries in <see cref="_batcherIds"/>.</summary>
    private int _batcherIdsCount;

    /// <summary>Reusable output array for the nearest dirty chunk position (x, y, z).</summary>
    private readonly int[] _tempNearestPos;

    /// <summary>
    /// Per-block-type light radius, pre-fetched before shadow calculation to
    /// avoid repeated property lookups in the inner loop.
    /// </summary>
    private readonly int[] _shadowLightRadius;

    /// <summary>
    /// Per-block-type transparency flag for light propagation, pre-fetched before
    /// shadow calculation.
    /// </summary>
    private readonly bool[] _shadowIsTransparent;

    /// <summary>Total number of chunk tessellations performed since startup.</summary>
    private int _chunkUpdates;

    /// <summary>
    /// Timestamp (ms from start) of the last performance-info display update.
    /// </summary>
    private int _lastPerfUpdateMs;

    /// <summary>Value of <see cref="_chunkUpdates"/> at the previous performance-info update.</summary>
    private int _lastChunkUpdatesSnapshot;

    /// <summary>
    /// Allocates all fixed-size buffers. Call order: constructor → <see cref="Start"/>
    /// (via <see cref="ModBase"/>) → <see cref="StartTerrain"/> (deferred until first redraw).
    /// </summary>
    public ModDrawTerrain()
    {
        _currentChunk = new int[BufferedChunkVolume];
        _currentChunkShadows = new byte[BufferedChunkVolume];
        _tempNearestPos = new int[3];
        _batcherIds = new int[1024];
        _redrawQueue = new TerrainRendererRedraw[128];
        _shadowLightRadius = new int[GlobalVar.MAX_BLOCKTYPES];
        _shadowIsTransparent = new bool[GlobalVar.MAX_BLOCKTYPES];
        _lightBase = new LightBase();
        _lightBetweenChunks = new LightBetweenChunks();
    }

    /// <summary>Returns the total number of chunk tessellations performed since startup.</summary>
    public int ChunkUpdates() => _chunkUpdates;

    /// <summary>Returns the reciprocal of <see cref="chunksize"/>.</summary>
    internal float GetInvertedChunkSize() => invertedChunkSize;

    /// <summary>Returns the total number of triangles currently submitted to the batcher.</summary>
    public int TrianglesCount() => _game.d_Batcher.TotalTriangleCount();

    /// <summary>
    /// Converts a block coordinate to a chunk coordinate by multiplying by
    /// <see cref="invertedChunkSize"/>.
    /// </summary>
    internal int InvertChunk(int num) => (int)(num * invertedChunkSize);

    /// <inheritdoc/>
    public override void OnNewFrameDraw3d(Game game, float _)
    {
        _game = game;

        if (_game.shouldRedrawAllBlocks)
        {
            _game.shouldRedrawAllBlocks = false;
            RedrawAllBlocks();
        }

        DrawTerrain();
        UpdatePerformanceInfo();
    }

    /// <inheritdoc/>
    public override void OnReadOnlyBackgroundThread(Game game_, float dt)
    {
        _game = game_;
        UpdateTerrain();
        game_.QueueActionCommit(MainThreadCommit);
    }

    /// <inheritdoc/>
    public override void Dispose(Game game_)
    {
        Clear();
    }

    /// <summary>
    /// Initialises the tessellator and caches frequently used chunk-size values.
    /// Must be called before any chunk can be tessellated.
    /// Called lazily from <see cref="RedrawAllBlocks"/> if not already started.
    /// </summary>
    public void StartTerrain()
    {
        _sqrt3Half = MathF.Sqrt(3) * 0.5f;
        chunksize = Game.chunksize;
        bufferedChunkSize = chunksize + 2;
        invertedChunkSize = 1.0f / chunksize;
        _game.d_TerrainChunkTesselator.Start();
        _terrainStarted = true;
    }

    /// <summary>
    /// Marks every chunk in the map as dirty so the entire terrain is re-tessellated
    /// on the next background pass. Calls <see cref="StartTerrain"/> if necessary.
    /// </summary>
    public void RedrawAllBlocks()
    {
        if (!_terrainStarted)
        {
            StartTerrain();
        }

        int chunksLength = InvertChunk(_game.VoxelMap.MapSizeX)
                         * InvertChunk(_game.VoxelMap.MapSizeY)
                         * InvertChunk(_game.VoxelMap.MapSizeZ);

        unchecked
        {
            for (int i = 0; i < chunksLength; i++)
            {
                Chunk c = _game.VoxelMap.chunks[i];
                if (c == null) { continue; }

                c.rendered ??= new RenderedChunk();

                c.rendered.dirty = true;
                c.baseLightDirty = true;
            }
        }
    }

    /// <summary>
    /// Background-thread entry point. Re-tessellates chunks that were dirtied by
    /// block placements and then picks the nearest remaining dirty chunk for update.
    /// Results are queued in <see cref="_redrawQueue"/> for GPU upload on the main thread.
    /// </summary>
    public void UpdateTerrain()
    {
        if (!_terrainStarted) { return; }

        unchecked
        {
            RedrawChunksAroundLastPlacedBlock();

            // Pick one more dirty chunk per pass (nearest to the player).
            NearestDirty(_tempNearestPos);
            if (_tempNearestPos[0] != NoChunk)
            {
                RedrawChunk(_tempNearestPos[0], _tempNearestPos[1], _tempNearestPos[2]);
            }
        }
    }

    /// <summary>
    /// If the player placed or destroyed a block last frame, marks the chunk
    /// containing it and its six axis-aligned neighbours as needing a redraw,
    /// then clears the pending block position.
    /// </summary>
    private void RedrawChunksAroundLastPlacedBlock()
    {
        if (_game.lastplacedblockX == NoChunk
         && _game.lastplacedblockY == NoChunk
         && _game.lastplacedblockZ == NoChunk)
        {
            return;
        }

        int mapSizeX = InvertChunk(_game.VoxelMap.MapSizeX);
        int mapSizeY = InvertChunk(_game.VoxelMap.MapSizeY);
        int mapSizeZ = InvertChunk(_game.VoxelMap.MapSizeZ);
        int mapsizexchunks = MapsizeXChunks();
        int mapsizeychunks = MapsizeYChunks();

        HashSet<Vector3i> chunksToRedraw = [];
        Vector3i[] around = BlocksAround7(new(_game.lastplacedblockX, _game.lastplacedblockY, _game.lastplacedblockZ));
        for (int i = 0; i < 7; i++)
        {
            Vector3i a = around[i];
            chunksToRedraw.Add(new(InvertChunk(a.X), InvertChunk(a.Y), InvertChunk(a.Z)));
        }

        foreach (Vector3i chunk3 in chunksToRedraw)
        {
            int xx = chunk3.X, yy = chunk3.Y, zz = chunk3.Z;
            if (xx < 0 || yy < 0 || zz < 0
             || xx >= mapSizeX || yy >= mapSizeY || zz >= mapSizeZ)
            {
                continue;
            }

            Chunk chunk = _game.VoxelMap.chunks[Index3d(xx, yy, zz, mapsizexchunks, mapsizeychunks)];
            if (chunk?.rendered == null) { continue; }

            if (chunk.rendered.dirty)
            {
                RedrawChunk(xx, yy, zz);
            }
        }

        _game.lastplacedblockX = NoChunk;
        _game.lastplacedblockY = NoChunk;
        _game.lastplacedblockZ = NoChunk;
    }

    /// <summary>
    /// Returns the block position and its six axis-aligned neighbours (7 total).
    /// Used to determine which chunks may be affected by a single block change.
    /// </summary>
    /// <param name="pos">The changed block position in world block coordinates.</param>
    /// <returns>Array of 7 positions: the block itself followed by its six neighbours.</returns>
    public static Vector3i[] BlocksAround7(Vector3i pos) =>
    [
        pos,
        new(pos.X + 1, pos.Y,     pos.Z),
        new(pos.X - 1, pos.Y,     pos.Z),
        new(pos.X,     pos.Y + 1, pos.Z),
        new(pos.X,     pos.Y - 1, pos.Z),
        new(pos.X,     pos.Y,     pos.Z + 1),
        new(pos.X,     pos.Y,     pos.Z - 1),
    ];

    /// <summary>
    /// Finds the dirty chunk nearest to the player within the current view distance
    /// and writes its chunk coordinates into <paramref name="nearestPos"/>.
    /// Writes <c>(-1, -1, -1)</c> when no dirty chunk is found.
    /// </summary>
    /// <param name="nearestPos">Output array of length 3 receiving (x, y, z).</param>
    private void NearestDirty(int[] nearestPos)
    {
        unchecked
        {
            int nearestDist = IntMaxValue;
            nearestPos[0] = nearestPos[1] = nearestPos[2] = NoChunk;

            int px = InvertChunk((int)_game.player.position.x);
            int py = InvertChunk((int)_game.player.position.z);
            int pz = InvertChunk((int)_game.player.position.y);

            int halfXY = InvertChunk(MapAreaSize()) / 2;
            int halfZ = InvertChunk(MapAreaSizeZ()) / 2;

            int startX = Math.Max(px - halfXY, 0);
            int startY = Math.Max(py - halfXY, 0);
            int startZ = Math.Max(pz - halfZ, 0);
            int endX = Math.Min(px + halfXY, MapsizeXChunks() - 1);
            int endY = Math.Min(py + halfXY, MapsizeYChunks() - 1);
            int endZ = Math.Min(pz + halfZ, MapsizeZChunks() - 1);

            int mapsizexchunks = MapsizeXChunks();
            int mapsizeychunks = MapsizeYChunks();

            for (int x = startX; x <= endX; x++)
                for (int y = startY; y <= endY; y++)
                    for (int z = startZ; z <= endZ; z++)
                    {
                        Chunk c = _game.VoxelMap.chunks[Index3d(x, y, z, mapsizexchunks, mapsizeychunks)];
                        if (c?.rendered == null || !c.rendered.dirty) { continue; }

                        int dx = px - x, dy = py - y, dz = pz - z;
                        int dist = dx * dx + dy * dy + dz * dz;
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearestPos[0] = x;
                            nearestPos[1] = y;
                            nearestPos[2] = z;
                        }
                    }
        }
    }

    /// <summary>
    /// Uploads all pending chunk geometry from <see cref="_redrawQueue"/> to the GPU.
    /// Must be called on the main (render) thread.
    /// </summary>
    public void MainThreadCommit()
    {
        for (int i = 0; i < _redrawQueueCount; i++)
        {
            DoRedraw(_redrawQueue[i]);
        }
        _redrawQueueCount = 0;
    }

    /// <summary>
    /// Removes the old batcher entries for the chunk described by <paramref name="r"/>,
    /// then uploads the new geometry and stores the resulting batcher IDs on the chunk.
    /// </summary>
    /// <param name="r">Redraw descriptor produced by <see cref="RedrawChunk"/>.</param>
    private void DoRedraw(TerrainRendererRedraw r)
    {
        unchecked
        {
            _batcherIdsCount = 0;
            RenderedChunk rendered = r.Chunk.rendered;

            // Remove previous geometry for this chunk from the batcher.
            if (rendered.ids != null)
            {
                for (int i = 0; i < rendered.idsCount; i++)
                {
                    _game.d_Batcher.Remove(rendered.ids[i]);
                }
            }

            // Upload each non-empty sub-mesh and record the new batcher ID.
            for (int i = 0; i < r.DataCount; i++)
            {
                VerticesIndicesToLoad submesh = r.Data[i];
                if (submesh.modelData.IndicesCount == 0) { continue; }

                float cx = submesh.positionX + chunksize * 0.5f;
                float cy = submesh.positionZ + chunksize * 0.5f;
                float cz = submesh.positionY + chunksize * 0.5f;
                float radius = _sqrt3Half * chunksize;

                _batcherIds[_batcherIdsCount++] = _game.d_Batcher.Add(
                    submesh.modelData, submesh.transparent, submesh.texture,
                    cx, cy, cz, radius);
            }

            // Write the new IDs back onto the chunk.
            int[] idsArr = new int[_batcherIdsCount];
            for (int i = 0; i < _batcherIdsCount; i++) { idsArr[i] = _batcherIds[i]; }
            rendered.ids = idsArr;
            rendered.idsCount = _batcherIdsCount;
        }
    }

    /// <summary>
    /// Tessellates the chunk at (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>)
    /// in chunk coordinates and enqueues the result for GPU upload.
    /// Skips tessellation entirely for chunks where every block is the same (solid-fill optimisation).
    /// </summary>
    private void RedrawChunk(int x, int y, int z)
    {
        unchecked
        {
            Chunk c = _game.VoxelMap.chunks[VectorIndexUtil.Index3d(x, y, z, MapsizeXChunks(), MapsizeYChunks())];
            if (c == null) { return; }

            c.rendered ??= new RenderedChunk();
            c.rendered.dirty = false;
            _chunkUpdates++;

            GetExtendedChunk(x, y, z);

            VerticesIndicesToLoad[] meshData = Array.Empty<VerticesIndicesToLoad>();
            int meshCount = 0;

            if (!IsSolidChunk(_currentChunk, BufferedChunkVolume))
            {
                CalculateShadows(x, y, z);
                VerticesIndicesToLoad[] meshes = _game.d_TerrainChunkTesselator.MakeChunk(
                    x, y, z, _currentChunk, _currentChunkShadows,
                    _game.mLightLevels, out meshCount);

                meshData = new VerticesIndicesToLoad[meshCount];
                for (int i = 0; i < meshCount; i++)
                {
                    meshData[i] = CloneVerticesIndicesToLoad(meshes[i]);
                }
            }

            _redrawQueue[_redrawQueueCount++] = new(c, meshData, meshCount);
        }
    }

    /// <summary>
    /// Copies the extended (18³) block-ID data centred on the given chunk into
    /// <see cref="_currentChunk"/> so the tessellator can access all border blocks.
    /// </summary>
    /// <param name="x">Chunk X coordinate.</param>
    /// <param name="y">Chunk Y coordinate.</param>
    /// <param name="z">Chunk Z coordinate.</param>
    private void GetExtendedChunk(int x, int y, int z)
    {
        _game.VoxelMap.GetMapPortion(
            _currentChunk,
            x * chunksize - 1, y * chunksize - 1, z * chunksize - 1,
            bufferedChunkSize, bufferedChunkSize, bufferedChunkSize);
    }

    /// <summary>
    /// Returns <see langword="true"/> when every entry in <paramref name="chunk"/> is
    /// identical (all-air or all-same-solid block). Such chunks produce no visible faces
    /// and can skip tessellation entirely.
    /// </summary>
    /// <param name="chunk">Extended chunk block-ID buffer.</param>
    /// <param name="length">Number of elements to check.</param>
    private static bool IsSolidChunk(int[] chunk, int length)
    {
        int first = chunk[0];
        unchecked
        {
            for (int i = 1; i < length; i++)
            {
                if (chunk[i] != first) { return false; }
            }
        }
        return true;
    }

    /// <summary>
    /// Computes the light values for the 18³ region around the chunk at
    /// (<paramref name="cx"/>, <paramref name="cy"/>, <paramref name="cz"/>) and
    /// writes them into <see cref="_currentChunkShadows"/>.
    /// Also triggers base-light recalculation for any of the 3×3×3 surrounding
    /// chunks whose base light is marked dirty.
    /// </summary>
    private void CalculateShadows(int cx, int cy, int cz)
    {
        unchecked
        {
            // Pre-fetch per-block-type lighting properties to avoid repeated lookups.
            for (int i = 0; i < GlobalVar.MAX_BLOCKTYPES; i++)
            {
                if (_game.blocktypes[i] == null) { continue; }
                _shadowLightRadius[i] = _game.blocktypes[i].LightRadius;
                _shadowIsTransparent[i] = IsTransparentForLight(i);
            }

            // Ensure base light is fresh for all 27 chunks in the 3×3×3 neighbourhood.
            for (int xx = 0; xx < 3; xx++)
                for (int yy = 0; yy < 3; yy++)
                    for (int zz = 0; zz < 3; zz++)
                    {
                        int cx1 = cx + xx - 1;
                        int cy1 = cy + yy - 1;
                        int cz1 = cz + zz - 1;
                        if (!_game.VoxelMap.IsValidChunkPos(cx1, cy1, cz1)) { continue; }

                        Chunk neighbour = _game.VoxelMap.GetChunk(cx1 * chunksize, cy1 * chunksize, cz1 * chunksize);
                        if (neighbour.baseLightDirty)
                        {
                            _lightBase.CalculateChunkBaseLight(
                                _game, cx1, cy1, cz1,
                                _shadowLightRadius, _shadowIsTransparent);
                            neighbour.baseLightDirty = false;
                        }
                    }

            // Initialise the chunk's light buffer to full brightness on first use.
            RenderedChunk rendered = _game.VoxelMap.GetChunk(cx * chunksize, cy * chunksize, cz * chunksize).rendered;
            if (rendered.light == null)
            {
                rendered.light = new byte[BufferedChunkVolume];
                for (int i = 0; i < BufferedChunkVolume; i++) { rendered.light[i] = 15; }
            }

            _lightBetweenChunks.CalculateLightBetweenChunks(
                _game, cx, cy, cz, _shadowLightRadius, _shadowIsTransparent);

            // Copy the computed light values into the per-frame scratch buffer.
            for (int i = 0; i < BufferedChunkVolume; i++)
            {
                _currentChunkShadows[i] = rendered.light[i];
            }
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> when the given block type allows light to pass through it.
    /// Solid blocks and closed doors are opaque; everything else is transparent for lighting.
    /// </summary>
    /// <param name="blockId">Block type ID to test.</param>
    public bool IsTransparentForLight(int blockId)
    {
        Packet_BlockType b = _game.blocktypes[blockId];
        return b.DrawType != Packet_DrawTypeEnum.Solid
            && b.DrawType != Packet_DrawTypeEnum.ClosedDoor;
    }

    /// <summary>
    /// Submits all currently loaded chunk geometry to the batcher for this frame.
    /// </summary>
    public void DrawTerrain()
    {
        _game.d_Batcher.Draw(
            _game.player.position.x,
            _game.player.position.y,
            _game.player.position.z);
    }

    /// <summary>Removes all chunk geometry from the batcher.</summary>
    internal void Clear() => _game.d_Batcher.Clear();

    /// <summary>
    /// Updates the on-screen chunk-update and triangle-count statistics once per second.
    /// </summary>
    /// <param name="dt">Frame delta time in seconds (unused; wall-clock ms is used instead).</param>
    internal void UpdatePerformanceInfo()
    {
        const float MsToSeconds = 1f / 1000f;
        float elapsed = (_game.platform.TimeMillisecondsFromStart - _lastPerfUpdateMs) * MsToSeconds;

        if (elapsed < 1f) { return; }

        _lastPerfUpdateMs = _game.platform.TimeMillisecondsFromStart;

        int updatesThisPeriod = _chunkUpdates - _lastChunkUpdatesSnapshot;
        _lastChunkUpdatesSnapshot = _chunkUpdates;

        _game.performanceinfo["chunk updates"] = string.Format(
            _game.language.ChunkUpdates(), updatesThisPeriod.ToString());
        _game.performanceinfo["triangles"] = string.Format(
            _game.language.Triangles(), TrianglesCount().ToString());
    }

    /// <summary>View-distance-based side length of the active map area in blocks.</summary>
    private int MapAreaSize() => (int)(_game.d_Config3d.viewdistance) * 2;

    /// <summary>Vertical counterpart of <see cref="MapAreaSize"/>.</summary>
    private int MapAreaSizeZ() => MapAreaSize();

    /// <summary>Map width in chunks.</summary>
    private int MapsizeXChunks() => _game.VoxelMap.Mapsizexchunks;

    /// <summary>Map depth in chunks.</summary>
    private int MapsizeYChunks() => _game.VoxelMap.Mapsizeychunks;

    /// <summary>Map height in chunks.</summary>
    private int MapsizeZChunks() => _game.VoxelMap.Mapsizezchunks;

    /// <summary>
    /// Converts 3-D chunk coordinates to a flat array index using row-major order.
    /// </summary>
    private static int Index3d(int x, int y, int h, int sizeX, int sizeY)
        => (h * sizeY + y) * sizeX + x;

    /// <summary>
    /// Performs a deep copy of a <see cref="VerticesIndicesToLoad"/> so the background
    /// thread's data can be handed off to the main thread without aliasing.
    /// </summary>
    private static VerticesIndicesToLoad CloneVerticesIndicesToLoad(VerticesIndicesToLoad source)
        => new()
        {
            modelData = CloneModelData(source.modelData),
            positionX = source.positionX,
            positionY = source.positionY,
            positionZ = source.positionZ,
            texture = source.texture,
            transparent = source.transparent,
        };

    /// <summary>
    /// Performs a deep copy of a <see cref="GeometryModel"/> instance, duplicating all
    /// vertex position, UV, colour, and index arrays.
    /// Required because the background thread writes into shared tessellator buffers
    /// that are overwritten on the next pass.
    /// </summary>
    private static GeometryModel CloneModelData(GeometryModel source)
    {
        GeometryModel dest = new();
        unchecked
        {
            dest.Xyz = new float[source.XyzCount];
            for (int i = 0; i < source.XyzCount; i++) { dest.Xyz[i] = source.Xyz[i]; }

            dest.Uv = new float[source.UvCount];
            for (int i = 0; i < source.UvCount; i++) { dest.Uv[i] = source.Uv[i]; }
            dest.Rgba = new byte[source.RgbaCount];
            for (int i = 0; i < source.RgbaCount; i++) { dest.Rgba[i] = source.Rgba[i]; }

            dest.Indices = new int[source.IndicesCount];
            for (int i = 0; i < source.IndicesCount; i++) { dest.Indices[i] = source.Indices[i]; }
            dest.VerticesCount = source.VerticesCount;
            dest.IndicesCount = source.IndicesCount;
        }
        return dest;
    }
}

/// <summary>
/// Carries the tessellated geometry for one chunk from the background thread
/// to the main thread for GPU upload.
/// </summary>
/// <param name="Chunk">The chunk whose geometry was tessellated.</param>
/// <param name="Data">Array of sub-meshes produced by the tessellator.</param>
/// <param name="DataCount">Number of valid entries in <see cref="Data"/>.</param>
internal readonly record struct TerrainRendererRedraw(
    Chunk Chunk,
    VerticesIndicesToLoad[] Data,
    int DataCount);