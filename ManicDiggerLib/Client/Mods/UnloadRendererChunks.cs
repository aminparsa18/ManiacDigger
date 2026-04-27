using OpenTK.Mathematics;

/// <summary>
/// Client-side mod that unloads rendered chunk geometry for chunks that have
/// scrolled outside the player's view distance.
/// Runs on the background thread and queues GPU-side cleanup on the main thread.
/// </summary>
public class ModUnloadRendererChunks : ModBase
{
    /// <summary>Reference to the current game instance, refreshed every background tick.</summary>
    private readonly IGameClient _game;

    private int _pendingUnloadIndex = -1;
    private Action _unloadAction;

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

    /// <summary>Reusable output for <see cref="VectorIndexUtil.PosInt"/> to avoid per-frame allocation.</summary>
    private Vector3i _unloadXyzTemp;

    public ModUnloadRendererChunks(IGameClient game, IGamePlatform platform)
    {
        _game = game;
        _unloadXyzTemp = new Vector3i();
        _unloadXyzTemp = new Vector3i();
        _unloadAction = ExecuteUnload; // allocated once, reused forever
    }

    /// <summary>
    /// Creates a main-thread <see cref="Action"/> that removes a single chunk's
    /// geometry from the batcher and resets its render state so it can be
    /// re-tessellated when the player approaches again.
    /// </summary>
    /// <remarks>
    /// Block data arrays (<c>data</c>, <c>dataInt</c>, <c>baseLight</c>) are returned to
    /// <see cref="System.Buffers.ArrayPool{T}.Shared"/> via <see cref="Chunk.Release"/> so
    /// they can be reused for incoming chunks rather than being garbage-collected.
    /// The chunk slot is then nulled so the <see cref="Chunk"/> object itself can be GC'd.
    /// </remarks>
    /// <param name="game">Game instance used to access the batcher and chunk map.</param>
    /// <param name="chunkFlatIndex">
    /// Flat index into <c>game.map.chunks</c> of the chunk to unload.
    /// Passing <c>-1</c> is a no-op.
    /// </param>
    /// <returns>An <see cref="Action"/> safe to enqueue via <see cref="Game.QueueActionCommit"/>.</returns>
    private void ExecuteUnload()
    {
        int chunkFlatIndex = _pendingUnloadIndex;
        if (chunkFlatIndex == -1) return;

        Chunk chunk = _game.VoxelMap.Chunks[chunkFlatIndex];
        if (chunk == null) return;

        RenderedChunk rendered = chunk.rendered;
        if (rendered != null)
        {
            for (int k = 0; k < rendered.IdsCount; k++)
                _game.Batcher.Remove(rendered.Ids[k]);
            rendered.Ids = null;
            rendered.Dirty = true;
            if (rendered.LightRented && rendered.Light != null)
            {
                System.Buffers.ArrayPool<byte>.Shared.Return(rendered.Light);
                rendered.LightRented = false;
            }
            rendered.Light = null;
        }

        chunk.Release();
        _game.VoxelMap.Chunks[chunkFlatIndex] = null;
    }

    /// <inheritdoc/>
    public override void OnReadOnlyBackgroundThread(float dt)
    {
        RefreshChunkGridDimensions();

        // ── Drain phantom chunks first — these have no rendered geometry
        // and were created only for lighting neighbour reads.
        // They must be freed as soon as the lighting pass that created them
        // is done, which is always before this background tick runs.
        int phantomDrainLimit = 64;
        while (phantomDrainLimit-- > 0
            && _game.VoxelMap.PhantomChunkIndices.TryDequeue(out int phantomIndex))
        {
            Chunk phantom = _game.VoxelMap.Chunks[phantomIndex];
            if (phantom == null) continue;
            // Only free if it hasn't been promoted to a real chunk
            // (real chunks have rendered != null or were sent by the server)
            if (phantom.rendered != null) continue;
            _game.QueueActionCommit(CreatePhantomUnloadCommit(phantomIndex));
        }

        // Compute the view-distance box in chunk coordinates.
        int px = (int)(_game.LocalPositionX * _invertedChunk);
        int py = (int)(_game.LocalPositionZ * _invertedChunk);
        int pz = (int)(_game.LocalPositionY * _invertedChunk);

        int halfXY = (int)(MapAreaSize() * _invertedChunk * 0.5f);
        int halfZ = (int)(MapAreaSizeZ() * _invertedChunk * 0.5f);

        int startX = Math.Max(px - halfXY, 0);
        int startY = Math.Max(py - halfXY, 0);
        int startZ = Math.Max(pz - halfZ, 0);
        int endX = Math.Min(px + halfXY, _mapSizeXChunks - 1);
        int endY = Math.Min(py + halfXY, _mapSizeYChunks - 1);
        int endZ = Math.Min(pz + halfZ, _mapSizeZChunks - 1);

        int totalChunks = _mapSizeXChunks * _mapSizeYChunks * _mapSizeZChunks;

        // scan full array over ~10 frames regardless of machine speed
        // At 75 ticks/sec this completes a full pass every ~133ms, plenty fast enough
        int checksPerTick = Math.Max(100, totalChunks / (5 * 75));

        for (int i = 0; i < checksPerTick; i++)
        {
            // Advance and wrap the round-robin iterator.
            if (++_unloadIterator >= totalChunks) { _unloadIterator = 0; }

            VectorIndexUtil.PosInt(_unloadIterator, _mapSizeXChunks, _mapSizeYChunks, ref _unloadXyzTemp);
            int x = _unloadXyzTemp.X;
            int y = _unloadXyzTemp.Y;
            int z = _unloadXyzTemp.Z;

            int flatIndex = VectorIndexUtil.Index3d(x, y, z, _mapSizeXChunks, _mapSizeYChunks);
            Chunk chunk = _game.VoxelMap.Chunks[flatIndex];

            // Skip empty slots — nothing to free.
            if (chunk == null) { continue; }

            // Determine whether this chunk holds any state worth freeing.
            // Previously only rendered chunks (rendered.Ids != null) were unloaded.
            // That left two invisible sources of unbounded memory growth:
            //   1. Chunks received from the server but outside render distance —
            //      they have block data but are never tessellated, so Ids stays null.
            //   2. Phantom chunks allocated by CalculateShadows when it called
            //      GetChunk() on 26 neighbours to check base-light: those neighbours
            //      may not exist yet so GetChunk_ rents two byte[4096] arrays each.
            // Both categories must be unloaded when outside the view-distance box.
            bool hasRenderedGeometry = chunk.rendered?.Ids != null;
            bool hasBlockData = chunk.HasData();
            if (!hasRenderedGeometry && !hasBlockData) { continue; }

            // If the chunk is outside the view-distance box, queue its removal.
            if (x < startX || y < startY || z < startZ
             || x > endX || y > endY || z > endZ)
            {
                _pendingUnloadIndex = flatIndex;
                _game.QueueActionCommit(_unloadAction);
                // Rate-limit only for rendered chunks (GPU removal needed).
                // Data-only chunks are CPU-only and cheap — continue scanning
                // so we can drain multiple per tick and keep pace with the
                // server's chunk send rate during fast exploration.
                if (hasRenderedGeometry) { break; }
            }
        }
    }

    private Action CreatePhantomUnloadCommit(int flatIndex)
    {
        return () =>
        {
            Chunk chunk = _game.VoxelMap.Chunks[flatIndex];
            if (chunk == null || chunk.rendered != null) return;
            chunk.Release();
            _game.VoxelMap.Chunks[flatIndex] = null;
        };
    }

    /// <summary>
    /// Refreshes all chunk-grid dimension fields from the current map and chunk size.
    /// Called at the start of every background tick because the map may have changed.
    /// </summary>
    private void RefreshChunkGridDimensions()
    {
        _chunkSize = Game.chunksize;
        _invertedChunk = 1.0f / _chunkSize;
        _mapSizeXChunks = (int)(_game.VoxelMap.MapSizeX * _invertedChunk);
        _mapSizeYChunks = (int)(_game.VoxelMap.MapSizeY * _invertedChunk);
        _mapSizeZChunks = (int)(_game.VoxelMap.MapSizeZ * _invertedChunk);
    }

    /// <summary>View-distance-based side length of the active area in blocks.</summary>
    private int MapAreaSize() => (int)_game.Config3d.ViewDistance * 2;

    /// <summary>Vertical counterpart of <see cref="MapAreaSize"/>.</summary>
    private int MapAreaSizeZ() => MapAreaSize();
}