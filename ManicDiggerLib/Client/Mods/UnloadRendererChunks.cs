using OpenTK.Mathematics;

/// <summary>
/// Client-side mod that unloads rendered chunk geometry for chunks that have
/// scrolled outside the player's view distance.
/// Runs on the background thread and queues GPU-side cleanup on the main thread.
/// </summary>
public class ModUnloadRendererChunks : ModBase
{
    /// <summary>Reference to the current game instance, refreshed every background tick.</summary>
    private Game _game;

    /// <summary>Edge length of one chunk in blocks.</summary>
    private int _chunkSize;

    /// <summary>Reciprocal of <see cref="_chunkSize"/>, used to convert block coords to chunk coords.</summary>
    private float _invertedChunk;

    /// <summary>Map width in chunks.</summary>
    private int _mapSizeXChunks;

    /// <summary>Map depth in chunks.</summary>
    private int _mapSizeYChunks;

    /// <summary>Map height in chunks.</summary>
    private int _mapSizeZChunks;

    /// <summary>
    /// Flat chunk index advanced each background tick to walk the full chunk array
    /// in a round-robin fashion, checking a fixed number of chunks per pass.
    /// </summary>
    private int _unloadIterator;

    /// <summary>Reusable output for <see cref="MapUtilCi.PosInt"/> to avoid per-frame allocation.</summary>
    private Vector3i _unloadXyzTemp;

    public ModUnloadRendererChunks()
    {
        _unloadXyzTemp = new Vector3i();
    }

    /// <summary>
    /// Creates a main-thread <see cref="Action"/> that removes a single chunk's
    /// geometry from the batcher and resets its render state so it can be
    /// re-tessellated when the player approaches again.
    /// </summary>
    /// <param name="game">Game instance used to access the batcher and chunk map.</param>
    /// <param name="chunkFlatIndex">
    /// Flat index into <c>game.map.chunks</c> of the chunk to unload.
    /// Passing <c>-1</c> is a no-op.
    /// </param>
    /// <returns>An <see cref="Action"/> safe to enqueue via <see cref="Game.QueueActionCommit"/>.</returns>
    public static Action CreateUnloadCommit(Game game, int chunkFlatIndex)
    {
        return () =>
        {
            if (chunkFlatIndex == -1) { return; }

            RenderedChunk rendered = game.map.chunks[chunkFlatIndex].rendered;
            for (int k = 0; k < rendered.idsCount; k++)
            {
                game.d_Batcher.Remove(rendered.ids[k]);
            }
            rendered.ids = null;
            rendered.dirty = true;
            rendered.light = null;
        };
    }

    /// <inheritdoc/>
    public override void OnReadOnlyBackgroundThread(Game game_, float dt)
    {
        _game = game_;

        RefreshChunkGridDimensions();

        // Compute the view-distance box in chunk coordinates.
        int px = (int)(game_.player.position.x * _invertedChunk);
        int py = (int)(game_.player.position.z * _invertedChunk);
        int pz = (int)(game_.player.position.y * _invertedChunk);

        int halfXY = (int)(MapAreaSize() * _invertedChunk * 0.5f);
        int halfZ = (int)(MapAreaSizeZ() * _invertedChunk * 0.5f);

        int startX = Math.Max(px - halfXY, 0);
        int startY = Math.Max(py - halfXY, 0);
        int startZ = Math.Max(pz - halfZ, 0);
        int endX = Math.Min(px + halfXY, _mapSizeXChunks - 1);
        int endY = Math.Min(py + halfXY, _mapSizeYChunks - 1);
        int endZ = Math.Min(pz + halfZ, _mapSizeZChunks - 1);

        int totalChunks = _mapSizeXChunks * _mapSizeYChunks * _mapSizeZChunks;

        // Fast systems check more chunks per tick to keep unloading responsive.
        int checksPerTick = game_.platform.IsFastSystem() ? 1000 : 250;

        for (int i = 0; i < checksPerTick; i++)
        {
            // Advance and wrap the round-robin iterator.
            if (++_unloadIterator >= totalChunks) { _unloadIterator = 0; }

            MapUtilCi.PosInt(_unloadIterator, _mapSizeXChunks, _mapSizeYChunks, ref _unloadXyzTemp);
            int x = _unloadXyzTemp.X;
            int y = _unloadXyzTemp.Y;
            int z = _unloadXyzTemp.Z;

            int flatIndex = MapUtilCi.Index3d(x, y, z, _mapSizeXChunks, _mapSizeYChunks);
            Chunk chunk = _game.map.chunks[flatIndex];

            // Skip chunks that have no geometry to unload.
            if (chunk?.rendered?.ids == null) { continue; }

            // If the chunk is outside the view-distance box, queue its geometry for removal.
            if (x < startX || y < startY || z < startZ
             || x > endX || y > endY || z > endZ)
            {
                _game.QueueActionCommit(CreateUnloadCommit(_game, flatIndex));
                break;
            }
        }
    }

    /// <summary>
    /// Refreshes all chunk-grid dimension fields from the current map and chunk size.
    /// Called at the start of every background tick because the map may have changed.
    /// </summary>
    private void RefreshChunkGridDimensions()
    {
        _chunkSize = Game.chunksize;
        _invertedChunk = 1.0f / _chunkSize;
        _mapSizeXChunks = (int)(_game.map.MapSizeX * _invertedChunk);
        _mapSizeYChunks = (int)(_game.map.MapSizeY * _invertedChunk);
        _mapSizeZChunks = (int)(_game.map.MapSizeZ * _invertedChunk);
    }

    /// <summary>View-distance-based side length of the active area in blocks.</summary>
    private int MapAreaSize() => (int)_game.d_Config3d.viewdistance * 2;

    /// <summary>Vertical counterpart of <see cref="MapAreaSize"/>.</summary>
    private int MapAreaSizeZ() => MapAreaSize();
}