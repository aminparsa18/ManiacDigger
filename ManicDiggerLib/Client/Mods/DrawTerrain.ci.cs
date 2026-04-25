using System.Buffers;
using ManicDigger;
using OpenTK.Mathematics;

/// <summary>
/// Client-side mod responsible for tessellating, lighting, and drawing the voxel terrain.
/// Runs chunk redraw logic on a background thread and commits GPU uploads on the main thread.
/// </summary>
public class ModDrawTerrain : ModBase
{
    /// <summary>Maximum light level used throughout the lighting system.</summary>
    public const int MaxLight = 15;

    /// <summary>
    /// Half of √3, used to compute the bounding-sphere radius of a chunk
    /// (√3 / 2 × chunkSize is the circumradius of a cube).
    /// </summary>
    private float _sqrt3Half;

    /// <summary>Sentinel value meaning "no chunk found" in <see cref="NearestDirty"/>.</summary>
    private const int NoChunk = -1;

    /// <summary>
    /// Size of the extended (buffered) chunk buffer in one axis.
    /// A 16³ chunk needs one block of overlap on each side → 18.
    /// </summary>
    private const int BufferedChunkEdge = 18;

    /// <summary>Volume of the extended chunk buffer (<see cref="BufferedChunkEdge"/>³).</summary>
    private const int BufferedChunkVolume = BufferedChunkEdge * BufferedChunkEdge * BufferedChunkEdge;

    private readonly IGameClient _game;
    private readonly IGamePlatform _platform;

    private readonly LightBase _lightBase;
    private readonly LightBetweenChunks _lightBetweenChunks;

    private bool _terrainStarted;

    internal int chunksize;
    internal int bufferedChunkSize;
    internal float invertedChunkSize;

    private readonly TerrainRendererRedraw[] _redrawQueue;
    private int _redrawQueueCount;

    private readonly int[] _currentChunk;
    private readonly byte[] _currentChunkShadows;
    private readonly int[] _batcherIds;
    private int _batcherIdsCount;

    private readonly int[] _shadowLightRadius;
    private readonly bool[] _shadowIsTransparent;
    private bool _blockTypeCacheDirty = true;

    private int _chunkUpdates;
    private int _lastPerfUpdateMs;
    private int _lastChunkUpdatesSnapshot;

    private readonly Vector3i[] _blocksAround7Buffer = new Vector3i[7];
    private readonly HashSet<Vector3i> _chunksToRedrawSet = new();

    public ModDrawTerrain(IGameClient game, IGamePlatform platform)
    {
        _game = game;
        _platform = platform;
        _currentChunk = new int[BufferedChunkVolume];
        _currentChunkShadows = new byte[BufferedChunkVolume];
        _batcherIds = new int[1024];
        _redrawQueue = new TerrainRendererRedraw[128];
        _shadowLightRadius = new int[GlobalVar.MAX_BLOCKTYPES];
        _shadowIsTransparent = new bool[GlobalVar.MAX_BLOCKTYPES];
        _lightBase = new LightBase();
        _lightBetweenChunks = new LightBetweenChunks();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public int ChunkUpdates() => _chunkUpdates;
    public int TrianglesCount() => _game.Batcher.TotalTriangleCount();
    internal float GetInvertedChunkSize() => invertedChunkSize;
    internal int InvertChunk(int num) => (int)(num * invertedChunkSize);

    /// <summary>
    /// Call this whenever block type definitions change so the lighting cache
    /// is rebuilt before the next chunk tessellation.
    /// </summary>
    public void InvalidateBlockTypeCache() => _blockTypeCacheDirty = true;

    // ── ModBase overrides ─────────────────────────────────────────────────────

    public override void OnNewFrameDraw3d(float _)
    {
        if (_game.ShouldRedrawAllBlocks)
        {
            _game.ShouldRedrawAllBlocks = false;
            RedrawAllBlocks();
        }
        DrawTerrain();
        UpdatePerformanceInfo();
    }

    public override void OnReadOnlyBackgroundThread(float dt)
    {
        UpdateTerrain();
        _game.QueueActionCommit(MainThreadCommit);
    }

    public override void Dispose() => Clear();

    // ── Initialisation ────────────────────────────────────────────────────────

    public void StartTerrain()
    {
        _sqrt3Half = MathF.Sqrt(3) * 0.5f;
        chunksize = Game.chunksize;
        bufferedChunkSize = chunksize + 2;
        invertedChunkSize = 1.0f / chunksize;
        _game.TerrainChunkTesselator.Start();
        _terrainStarted = true;
    }

    public void RedrawAllBlocks()
    {
        if (!_terrainStarted)
            StartTerrain();

        int chunksLength = InvertChunk(_game.MapSizeX)
                         * InvertChunk(_game.MapSizeY)
                         * InvertChunk(_game.MapSizeZ);
        unchecked
        {
            for (int i = 0; i < chunksLength; i++)
            {
                Chunk c = _game.VoxelMap.Chunks[i];
                if (c == null) continue;
                c.rendered ??= new RenderedChunk();
                c.rendered.Dirty = true;
                c.baseLightDirty = true;
            }
        }
    }

    // ── Background thread ─────────────────────────────────────────────────────

    public void UpdateTerrain()
    {
        if (!_terrainStarted) return;
        unchecked
        {
            RedrawChunksAroundLastPlacedBlock();

            // ── Fix #5: NearestDirty returns a nullable tuple ─────────────────
            var nearest = NearestDirty();
            if (nearest.HasValue)
                RedrawChunk(nearest.Value.x, nearest.Value.y, nearest.Value.z);
        }
    }

    private void RedrawChunksAroundLastPlacedBlock()
    {
        if (_game.LastplacedblockX == NoChunk
         && _game.LastplacedblockY == NoChunk
         && _game.LastplacedblockZ == NoChunk)
            return;

        int mapSizeX = InvertChunk(_game.VoxelMap.MapSizeX);
        int mapSizeY = InvertChunk(_game.VoxelMap.MapSizeY);
        int mapSizeZ = InvertChunk(_game.VoxelMap.MapSizeZ);
        int mapsizexchunks = MapsizeXChunks();
        int mapsizeychunks = MapsizeYChunks();

        _chunksToRedrawSet.Clear();
        BlocksAround7Inplace(
            new(_game.LastplacedblockX, _game.LastplacedblockY, _game.LastplacedblockZ),
            _blocksAround7Buffer);

        for (int i = 0; i < 7; i++)
        {
            Vector3i a = _blocksAround7Buffer[i];
            _chunksToRedrawSet.Add(new(InvertChunk(a.X), InvertChunk(a.Y), InvertChunk(a.Z)));
        }

        foreach (Vector3i chunk3 in _chunksToRedrawSet)
        {
            int xx = chunk3.X, yy = chunk3.Y, zz = chunk3.Z;
            if (xx < 0 || yy < 0 || zz < 0
             || xx >= mapSizeX || yy >= mapSizeY || zz >= mapSizeZ)
                continue;

            Chunk chunk = _game.VoxelMap.Chunks[Index3d(xx, yy, zz, mapsizexchunks, mapsizeychunks)];
            if (chunk?.rendered == null) continue;
            if (chunk.rendered.Dirty) RedrawChunk(xx, yy, zz);
        }

        _game.LastplacedblockX = NoChunk;
        _game.LastplacedblockY = NoChunk;
        _game.LastplacedblockZ = NoChunk;
    }

    private static void BlocksAround7Inplace(Vector3i pos, Vector3i[] buffer)
    {
        buffer[0] = pos;
        buffer[1] = new(pos.X + 1, pos.Y, pos.Z);
        buffer[2] = new(pos.X - 1, pos.Y, pos.Z);
        buffer[3] = new(pos.X, pos.Y + 1, pos.Z);
        buffer[4] = new(pos.X, pos.Y - 1, pos.Z);
        buffer[5] = new(pos.X, pos.Y, pos.Z + 1);
        buffer[6] = new(pos.X, pos.Y, pos.Z - 1);
    }

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
    /// Finds the dirty chunk nearest to the player within the current view distance.
    /// Returns <see langword="null"/> when no dirty chunk exists.
    /// </summary>
    private (int x, int y, int z)? NearestDirty()
    {
        unchecked
        {
            // ── Fix #4: int.MaxValue directly — IntMaxValue constant removed ──
            int nearestDist = int.MaxValue;
            int nearestX = NoChunk, nearestY = NoChunk, nearestZ = NoChunk;

            int px = InvertChunk((int)_game.LocalPositionX);
            int py = InvertChunk((int)_game.LocalPositionZ);
            int pz = InvertChunk((int)_game.LocalPositionY);

            // ── Fix #3: MapAreaSizeZ() removed — use MapAreaSize() directly ───
            int halfXY = InvertChunk(MapAreaSize()) / 2;
            int halfZ = halfXY; // currently identical; change here if vertical distance diverges

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
                        Chunk c = _game.VoxelMap.Chunks[Index3d(x, y, z, mapsizexchunks, mapsizeychunks)];
                        if (c?.rendered == null || !c.rendered.Dirty) continue;

                        int dx = px - x, dy = py - y, dz = pz - z;
                        int dist = dx * dx + dy * dy + dz * dz;
                        if (dist < nearestDist)
                        {
                            nearestDist = dist;
                            nearestX = x; nearestY = y; nearestZ = z;
                        }
                    }

            return nearestX == NoChunk ? null : (nearestX, nearestY, nearestZ);
        }
    }

    // ── Main-thread commit ────────────────────────────────────────────────────

    public void MainThreadCommit()
    {
        for (int i = 0; i < _redrawQueueCount; i++)
        {
            DoRedraw(_redrawQueue[i]);
            if (_redrawQueue[i].DataRented)
                ArrayPool<VerticesIndicesToLoad>.Shared.Return(_redrawQueue[i].Data);
        }
        _redrawQueueCount = 0;
    }

    private void DoRedraw(TerrainRendererRedraw r)
    {
        unchecked
        {
            _batcherIdsCount = 0;
            RenderedChunk rendered = r.Chunk.rendered;

            if (rendered.Ids != null)
                for (int i = 0; i < rendered.IdsCount; i++)
                    _game.Batcher.Remove(rendered.Ids[i]);

            for (int i = 0; i < r.DataCount; i++)
            {
                VerticesIndicesToLoad submesh = r.Data[i];
                if (submesh.ModelData.IndicesCount == 0)
                {
                    ReturnModelArrays(submesh.ModelData);
                    continue;
                }

                float cx = submesh.PositionX + chunksize * 0.5f;
                float cy = submesh.PositionZ + chunksize * 0.5f;
                float cz = submesh.PositionY + chunksize * 0.5f;
                float radius = _sqrt3Half * chunksize;

                _batcherIds[_batcherIdsCount++] = _game.Batcher.Add(
                    submesh.ModelData, submesh.Transparent, submesh.Texture,
                    cx, cy, cz, radius);

                ReturnModelArrays(submesh.ModelData);
            }

            if (rendered.Ids == null || rendered.Ids.Length != _batcherIdsCount)
                rendered.Ids = new int[_batcherIdsCount];

            for (int i = 0; i < _batcherIdsCount; i++)
                rendered.Ids[i] = _batcherIds[i];

            rendered.IdsCount = _batcherIdsCount;
        }
    }

    private static void ReturnModelArrays(GeometryModel model)
    {
        if (model.Xyz != null) { ArrayPool<float>.Shared.Return(model.Xyz); model.Xyz = null; }
        if (model.Uv != null) { ArrayPool<float>.Shared.Return(model.Uv); model.Uv = null; }
        if (model.Rgba != null) { ArrayPool<byte>.Shared.Return(model.Rgba); model.Rgba = null; }
        if (model.Indices != null) { ArrayPool<int>.Shared.Return(model.Indices); model.Indices = null; }
    }

    // ── Tessellation ──────────────────────────────────────────────────────────

    private void RedrawChunk(int x, int y, int z)
    {
        unchecked
        {
            Chunk c = _game.VoxelMap.Chunks[
                VectorIndexUtil.Index3d(x, y, z, MapsizeXChunks(), MapsizeYChunks())];
            if (c == null) return;

            c.rendered ??= new RenderedChunk();
            c.rendered.Dirty = false;
            _chunkUpdates++;

            GetExtendedChunk(x, y, z);

            int meshCount = 0;
            VerticesIndicesToLoad[] meshData = null;
            bool dataRented = false;

            if (!IsUniformChunk(_currentChunk, BufferedChunkVolume))
            {
                CalculateShadows(x, y, z);
                VerticesIndicesToLoad[] meshes = _game.TerrainChunkTesselator.MakeChunk(
                    x, y, z, _currentChunk, _currentChunkShadows,
                    _game.LightLevels, out meshCount);

                if (meshCount > 0)
                {
                    meshData = ArrayPool<VerticesIndicesToLoad>.Shared.Rent(meshCount);
                    dataRented = true;
                    for (int i = 0; i < meshCount; i++)
                        meshData[i] = CloneVerticesIndicesToLoad(meshes[i]);
                }
            }

            if (meshData == null)
            {
                meshData = Array.Empty<VerticesIndicesToLoad>();
                dataRented = false;
            }

            _redrawQueue[_redrawQueueCount++] = new(c, meshData, meshCount, dataRented);
        }
    }

    private void GetExtendedChunk(int x, int y, int z)
    {
        _game.VoxelMap.GetMapPortion(
            _currentChunk,
            x * chunksize - 1, y * chunksize - 1, z * chunksize - 1,
            bufferedChunkSize, bufferedChunkSize, bufferedChunkSize);
    }

    /// <summary>
    /// Returns <see langword="true"/> when every entry in <paramref name="chunk"/>
    /// is identical. Uniform chunks produce no visible faces and skip tessellation.
    /// </summary>
    private static bool IsUniformChunk(int[] chunk, int length)
    {
        int first = chunk[0];
        unchecked
        {
            for (int i = 1; i < length; i++)
                if (chunk[i] != first) return false;
        }
        return true;
    }

    private void CalculateShadows(int cx, int cy, int cz)
    {
        unchecked
        {
            if (_blockTypeCacheDirty)
            {
                for (int i = 0; i < GlobalVar.MAX_BLOCKTYPES; i++)
                {
                    if (_game.BlockTypes[i] == null) continue;
                    _shadowLightRadius[i] = _game.BlockTypes[i].LightRadius;
                    _shadowIsTransparent[i] = IsTransparentForLight(i);
                }
                _blockTypeCacheDirty = false;
            }

            for (int xx = 0; xx < 3; xx++)
                for (int yy = 0; yy < 3; yy++)
                    for (int zz = 0; zz < 3; zz++)
                    {
                        int cx1 = cx + xx - 1;
                        int cy1 = cy + yy - 1;
                        int cz1 = cz + zz - 1;
                        if (!_game.VoxelMap.IsValidChunkPos(cx1, cy1, cz1)) continue;

                        int nIdx = VectorIndexUtil.Index3d(
                            cx1, cy1, cz1,
                            _game.VoxelMap.Mapsizexchunks,
                            _game.VoxelMap.Mapsizeychunks);
                        Chunk neighbour = _game.VoxelMap.Chunks[nIdx];
                        if (neighbour == null) continue;

                        if (neighbour.baseLightDirty)
                        {
                            _lightBase.CalculateChunkBaseLight(
                                _game, cx1, cy1, cz1,
                                _shadowLightRadius, _shadowIsTransparent);
                            neighbour.baseLightDirty = false;
                        }
                    }

            RenderedChunk rendered = _game.VoxelMap
                .GetChunk(cx * chunksize, cy * chunksize, cz * chunksize).rendered;

            if (rendered.Light == null)
            {
                rendered.Light = ArrayPool<byte>.Shared.Rent(BufferedChunkVolume);
                rendered.LightRented = true;
                rendered.Light.AsSpan(0, BufferedChunkVolume).Fill(15);
            }

            _lightBetweenChunks.CalculateLightBetweenChunks(
                _game, cx, cy, cz, _shadowLightRadius, _shadowIsTransparent);

            rendered.Light.AsSpan(0, BufferedChunkVolume)
                          .CopyTo(_currentChunkShadows.AsSpan(0, BufferedChunkVolume));
        }
    }

    public bool IsTransparentForLight(int blockId)
    {
        Packet_BlockType b = _game.BlockTypes[blockId];
        return b.DrawType != DrawType.Solid
            && b.DrawType != DrawType.ClosedDoor;
    }

    // ── Drawing ───────────────────────────────────────────────────────────────

    public void DrawTerrain()
    {
        _game.Batcher.Draw(
            _game.LocalPositionX,
            _game.LocalPositionY,
            _game.LocalPositionZ);
    }

    internal void Clear() => _game.Batcher.Clear();

    /// <summary>Updates chunk-update and triangle-count statistics once per second.</summary>
    internal void UpdatePerformanceInfo()
    {
        const float MsToSeconds = 1f / 1000f;
        float elapsed = (_platform.TimeMillisecondsFromStart - _lastPerfUpdateMs) * MsToSeconds;
        if (elapsed < 1f) return;

        _lastPerfUpdateMs = _platform.TimeMillisecondsFromStart;
        int updatesThisPeriod = _chunkUpdates - _lastChunkUpdatesSnapshot;
        _lastChunkUpdatesSnapshot = _chunkUpdates;

        _game.PerformanceInfo["chunk updates"] = string.Format(
            _game.Language.ChunkUpdates(), updatesThisPeriod.ToString());
        _game.PerformanceInfo["triangles"] = string.Format(
            _game.Language.Triangles(), TrianglesCount().ToString());
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>View-distance-based side length of the active map area in blocks.</summary>
    private int MapAreaSize() => (int)_game.Config3d.ViewDistance * 2;

    private int MapsizeXChunks() => _game.VoxelMap.Mapsizexchunks;
    private int MapsizeYChunks() => _game.VoxelMap.Mapsizeychunks;
    private int MapsizeZChunks() => _game.VoxelMap.Mapsizezchunks;

    private static int Index3d(int x, int y, int h, int sizeX, int sizeY)
        => (h * sizeY + y) * sizeX + x;

    private static VerticesIndicesToLoad CloneVerticesIndicesToLoad(VerticesIndicesToLoad source)
        => new()
        {
            ModelData = CloneModelData(source.ModelData),
            PositionX = source.PositionX,
            PositionY = source.PositionY,
            PositionZ = source.PositionZ,
            Texture = source.Texture,
            Transparent = source.Transparent,
        };

    private static GeometryModel CloneModelData(GeometryModel source)
    {
        GeometryModel dest = new();
        unchecked
        {
            dest.Xyz = ArrayPool<float>.Shared.Rent(source.XyzCount);
            source.Xyz.AsSpan(0, source.XyzCount).CopyTo(dest.Xyz);

            dest.Uv = ArrayPool<float>.Shared.Rent(source.UvCount);
            source.Uv.AsSpan(0, source.UvCount).CopyTo(dest.Uv);

            dest.Rgba = ArrayPool<byte>.Shared.Rent(source.RgbaCount);
            source.Rgba.AsSpan(0, source.RgbaCount).CopyTo(dest.Rgba);

            dest.Indices = ArrayPool<int>.Shared.Rent(source.IndicesCount);
            source.Indices.AsSpan(0, source.IndicesCount).CopyTo(dest.Indices);

            dest.VerticesCount = source.VerticesCount;
            dest.IndicesCount = source.IndicesCount;
        }
        return dest;
    }
}

/// <summary>
/// Carries tessellated geometry for one chunk from the background thread
/// to the main thread for GPU upload.
/// </summary>
internal readonly record struct TerrainRendererRedraw(
    Chunk Chunk,
    VerticesIndicesToLoad[] Data,
    int DataCount,
    bool DataRented);