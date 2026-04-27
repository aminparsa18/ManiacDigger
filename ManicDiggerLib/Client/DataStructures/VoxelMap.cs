using System.Buffers;
using OpenTK.Mathematics;

/// <summary>
/// Represents the voxel world, storing block data in a sparse grid of <see cref="Chunk"/> objects.
/// Block-space coordinates are in individual block units; chunk coordinates are derived by
/// dividing by <see cref="Game.chunksize"/> (or right-shifting by <see cref="Game.chunksizebits"/>).
/// </summary>
public class VoxelMap
{
    public Chunk[] Chunks { get; private set; }
    public readonly System.Collections.Concurrent.ConcurrentQueue<int> PhantomChunkIndices = new();

    internal int MapSizeX;
    internal int MapSizeY;
    internal int MapSizeZ;

    /// <summary>
    /// Converts 3D coordinates into a flat array index using the layout: <c>(h * sizeY + y) * sizeX + x</c>.
    /// Used for both block-space and chunk-space indexing depending on the size arguments passed.
    /// </summary>
    private static int Index3d(int x, int y, int h, int sizex, int sizey)
    {
        return (h * sizey + y) * sizex + x;
    }

    /// <summary>
    /// Returns the block type at the given block-space position without performing map-bounds validation.
    /// Returns 0 (air) if the owning chunk has not been allocated yet.
    /// Use <see cref="GetBlock"/> for safe access that also checks bounds.
    /// </summary>
    public int GetBlockValid(int x, int y, int z)
    {
        int cx = x >> Game.chunksizebits;
        int cy = y >> Game.chunksizebits;
        int cz = z >> Game.chunksizebits;
        int chunkpos = Index3d(cx, cy, cz, MapSizeX >> Game.chunksizebits, MapSizeY >> Game.chunksizebits);
        if (Chunks[chunkpos] == null)
        {
            return 0;
        }
        else
        {
            int pos = Index3d(x & (Game.chunksize - 1), y & (Game.chunksize - 1), z & (Game.chunksize - 1), Game.chunksize, Game.chunksize);
            return Chunks[chunkpos].GetBlock(pos);
        }
    }

    /// <summary>
    /// Returns the chunk that contains the block at block-space coordinates,
    /// allocating a new chunk if one does not already exist.
    /// </summary>
    public Chunk GetChunk(int x, int y, int z)
    {
        x = x / Game.chunksize;
        y = y / Game.chunksize;
        z = z / Game.chunksize;
        return GetChunk_(x, y, z);
    }

    /// <summary>
    /// Returns the chunk at the given chunk-space coordinates, allocating a new one if absent.
    /// Backing arrays are rented from <see cref="ArrayPool{T}.Shared"/> and zeroed before use
    /// so the caller always receives clean, initialised memory.
    /// </summary>
    public Chunk GetChunk_(int cx, int cy, int cz)
    {
        int mapsizexchunks = MapSizeX / Game.chunksize;
        int mapsizeychunks = MapSizeY / Game.chunksize;
        int flatIndex = Index3d(cx, cy, cz, mapsizexchunks, mapsizeychunks);
        Chunk chunk = Chunks[flatIndex];

        if (chunk == null)
        {
            int n = Game.chunksize * Game.chunksize * Game.chunksize;

            // Rent from the shared pool; Rent(n) may return a larger array — always use n as the
            // logical size. Clear before use because the pool may return dirty memory.
            byte[] data = ArrayPool<byte>.Shared.Rent(n);
            byte[] baseLight = ArrayPool<byte>.Shared.Rent(n);
            data.AsSpan(0, n).Clear();
            baseLight.AsSpan(0, n).Clear();

            chunk = new Chunk
            {
                data = data,
                baseLight = baseLight
            };
            Chunks[flatIndex] = chunk;
            PhantomChunkIndices.Enqueue(flatIndex);
        }

        return chunk;
    }

    /// <summary>
    /// Writes a block type directly into the map without marking any chunks dirty.
    /// Prefer this during bulk operations such as world generation to avoid redundant re-render triggers.
    /// For interactive single-block edits, dirty-marking must be handled separately via <see cref="SetBlockDirty"/>.
    /// </summary>
    public void SetBlockRaw(int x, int y, int z, int tileType)
    {
        Chunk chunk = GetChunk(x, y, z);
        int pos = Index3d(x % Game.chunksize, y % Game.chunksize, z % Game.chunksize, Game.chunksize, Game.chunksize);
        chunk.SetBlock(pos, tileType);
    }

    /// <summary>
    /// Copies all block data from <paramref name="chunk"/> into <paramref name="output"/>.
    /// Uses <see cref="Chunk.dataInt"/> when available, otherwise falls back to <see cref="Chunk.data"/>.
    /// </summary>
    public static void CopyChunk(Chunk chunk, int[] output)
    {
        int n = Game.chunksize * Game.chunksize * Game.chunksize;
        if (chunk.dataInt != null)
        {
            for (int i = 0; i < n; i++)
                output[i] = chunk.dataInt[i];
        }
        else
        {
            for (int i = 0; i < n; i++)
                output[i] = chunk.data[i];
        }
    }

    /// <summary>
    /// Reinitialises the map with new dimensions, discarding all existing chunk data.
    /// All sizes must be exact multiples of <see cref="Game.chunksize"/>.
    /// </summary>
    /// <remarks>
    /// Every live chunk's pooled arrays are returned to <see cref="ArrayPool{T}.Shared"/>
    /// before the chunk array is replaced, preventing pool leaks on world reload.
    /// </remarks>
    public void Reset(int sizex, int sizey, int sizez)
    {
        // Release pooled arrays from any existing chunks before discarding the array.
        if (Chunks != null)
        {
            for (int i = 0; i < Chunks.Length; i++)
            {
                Chunks[i]?.Release();
                Chunks[i] = null;
            }
        }

        MapSizeX = sizex;
        MapSizeY = sizey;
        MapSizeZ = sizez;
        Chunks = new Chunk[sizex / Game.chunksize * (sizey / Game.chunksize) * (sizez / Game.chunksize)];
    }

    /// <summary>
    /// Reads a rectangular sub-region of the map into a flat array.
    /// Unallocated or out-of-bounds positions are written as 0 (air).
    /// Output array layout: <c>index = (z * sizeY + y) * sizeX + x</c>.
    /// </summary>
    public void GetMapPortion(int[] outPortion, int x, int y, int z, int portionsizex, int portionsizey, int portionsizez)
    {
        int outPortionCount = portionsizex * portionsizey * portionsizez;
        for (int i = 0; i < outPortionCount; i++)
        {
            outPortion[i] = 0;
        }

        int mapchunksx = MapSizeX / Game.chunksize;
        int mapchunksy = MapSizeY / Game.chunksize;
        int mapchunksz = MapSizeZ / Game.chunksize;
        int mapsizechunks = mapchunksx * mapchunksy * mapchunksz;

        for (int xx = 0; xx < portionsizex; xx++)
        {
            for (int yy = 0; yy < portionsizey; yy++)
            {
                for (int zz = 0; zz < portionsizez; zz++)
                {
                    int cx = (x + xx) >> Game.chunksizebits;
                    int cy = (y + yy) >> Game.chunksizebits;
                    int cz = (z + zz) >> Game.chunksizebits;
                    int cpos = (cz * mapchunksy + cy) * mapchunksx + cx;
                    if (cpos < 0 || cpos >= mapsizechunks)
                    {
                        continue;
                    }
                    Chunk chunk = Chunks[cpos];
                    if (chunk == null || !chunk.HasData())
                    {
                        continue;
                    }
                    int chunkGlobalX = cx << Game.chunksizebits;
                    int chunkGlobalY = cy << Game.chunksizebits;
                    int chunkGlobalZ = cz << Game.chunksizebits;

                    int inChunkX = x + xx - chunkGlobalX;
                    int inChunkY = y + yy - chunkGlobalY;
                    int inChunkZ = z + zz - chunkGlobalZ;

                    int pos = (((inChunkZ << Game.chunksizebits) + inChunkY) << Game.chunksizebits) + inChunkX;

                    int block = chunk.GetBlock(pos);
                    outPortion[(zz * portionsizey + yy) * portionsizex + xx] = block;
                }
            }
        }
    }

    /// <summary>Returns true when block-space coordinates fall within map bounds.</summary>
    public bool IsValidPos(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0)
            return false;
        if (x >= MapSizeX || y >= MapSizeY || z >= MapSizeZ)
            return false;
        return true;
    }

    /// <summary>Returns true when chunk-space coordinates fall within map bounds.</summary>
    public bool IsValidChunkPos(int cx, int cy, int cz)
    {
        return cx >= 0 && cy >= 0 && cz >= 0
            && cx < MapSizeX / Game.chunksize
            && cy < MapSizeY / Game.chunksize
            && cz < MapSizeZ / Game.chunksize;
    }

    /// <summary>
    /// Returns the block type at the given block-space position,
    /// or 0 (air) when the position is outside map bounds.
    /// </summary>
    public int GetBlock(int x, int y, int z)
    {
        if (!IsValidPos(x, y, z))
            return 0;
        return GetBlockValid(x, y, z);
    }

    /// <summary>
    /// Marks the chunk at chunk-space coordinates as dirty, optionally flagging that its block
    /// data has changed (which also invalidates base lighting via <see cref="Chunk.baseLightDirty"/>).
    /// Does nothing if the position is out of range or the chunk is null.
    /// </summary>
    public void SetChunkDirty(int cx, int cy, int cz, bool dirty, bool blockschanged)
    {
        if (!IsValidChunkPos(cx, cy, cz))
            return;

        Chunk c = Chunks[VectorIndexUtil.Index3d(cx, cy, cz, Mapsizexchunks, Mapsizeychunks)];
        if (c == null)
            return;

        c.rendered ??= new RenderedChunk();
        c.rendered.Dirty = dirty;
        if (blockschanged)
            c.baseLightDirty = true;
    }

    /// <summary>Width of the map measured in chunks.</summary>
    public int Mapsizexchunks => MapSizeX >> Game.chunksizebits;

    /// <summary>Depth of the map measured in chunks.</summary>
    public int Mapsizeychunks => MapSizeY >> Game.chunksizebits;

    /// <summary>Height of the map measured in chunks.</summary>
    public int Mapsizezchunks => MapSizeZ >> Game.chunksizebits;

    /// <summary>
    /// Marks the six face-adjacent neighbour chunks of the chunk at the given chunk-space coordinates
    /// as dirty for re-rendering (without invalidating lighting).
    /// </summary>
    public void SetChunksAroundDirty(int cx, int cy, int cz)
    {
        if (IsValidChunkPos(cx - 1, cy, cz)) SetChunkDirty(cx - 1, cy, cz, true, false);
        if (IsValidChunkPos(cx + 1, cy, cz)) SetChunkDirty(cx + 1, cy, cz, true, false);
        if (IsValidChunkPos(cx, cy - 1, cz)) SetChunkDirty(cx, cy - 1, cz, true, false);
        if (IsValidChunkPos(cx, cy + 1, cz)) SetChunkDirty(cx, cy + 1, cz, true, false);
        if (IsValidChunkPos(cx, cy, cz - 1)) SetChunkDirty(cx, cy, cz - 1, true, false);
        if (IsValidChunkPos(cx, cy, cz + 1)) SetChunkDirty(cx, cy, cz + 1, true, false);
    }

    /// <summary>
    /// Writes a rectangular block of data into the map and marks all affected chunks
    /// (and their neighbours) dirty for re-rendering.
    /// Input array layout must match <see cref="GetMapPortion"/>.
    /// </summary>
    public void SetMapPortion(int x, int y, int z, int[] chunk, int sizeX, int sizeY, int sizeZ)
    {
        int chunksizex = sizeX;
        int chunksizey = sizeY;
        int chunksizez = sizeZ;
        int chunksize = Game.chunksize;
        Chunk[] localchunks = new Chunk[chunksizex / chunksize * (chunksizey / chunksize) * (chunksizez / chunksize)];
        for (int cx = 0; cx < chunksizex / chunksize; cx++)
        {
            for (int cy = 0; cy < chunksizey / chunksize; cy++)
            {
                for (int cz = 0; cz < chunksizez / chunksize; cz++)
                {
                    localchunks[Index3d(cx, cy, cz, chunksizex / chunksize, chunksizey / chunksize)] = GetChunk(x + cx * chunksize, y + cy * chunksize, z + cz * chunksize);
                    FillChunk(localchunks[Index3d(cx, cy, cz, chunksizex / chunksize, chunksizey / chunksize)], chunksize, cx * chunksize, cy * chunksize, cz * chunksize, chunk, sizeX, sizeY, sizeZ);
                }
            }
        }
        for (int xxx = 0; xxx < chunksizex; xxx += chunksize)
        {
            for (int yyy = 0; yyy < chunksizey; yyy += chunksize)
            {
                for (int zzz = 0; zzz < chunksizez; zzz += chunksize)
                {
                    SetChunkDirty((x + xxx) / chunksize, (y + yyy) / chunksize, (z + zzz) / chunksize, true, true);
                    SetChunksAroundDirty((x + xxx) / chunksize, (y + yyy) / chunksize, (z + zzz) / chunksize);
                }
            }
        }
    }

    /// <summary>
    /// Copies a sub-cube of <paramref name="source"/> (offset by the given source coordinates)
    /// into <paramref name="destination"/>, filling every block position in the destination chunk.
    /// </summary>
    public static void FillChunk(Chunk destination, int destinationchunksize, int sourcex, int sourcey, int sourcez, int[] source, int sourcechunksizeX, int sourcechunksizeY, int sourcechunksizeZ)
    {
        for (int x = 0; x < destinationchunksize; x++)
        {
            for (int y = 0; y < destinationchunksize; y++)
            {
                for (int z = 0; z < destinationchunksize; z++)
                {
                    destination.SetBlock(
                        Index3d(x, y, z, destinationchunksize, destinationchunksize),
                        source[Index3d(x + sourcex, y + sourcey, z + sourcez, sourcechunksizeX, sourcechunksizeY)]);
                }
            }
        }
    }

    /// <summary>
    /// Attempts to read the baked light value at a block-space position.
    /// Returns -1 if the position is invalid, the chunk is unallocated,
    /// or the chunk's light buffer has not been computed yet.
    /// </summary>
    public int MaybeGetLight(int x, int y, int z)
    {
        int light = -1;
        int cx = x / Game.chunksize;
        int cy = y / Game.chunksize;
        int cz = z / Game.chunksize;
        if (IsValidPos(x, y, z) && IsValidChunkPos(cx, cy, cz))
        {
            Chunk c = Chunks[VectorIndexUtil.Index3d(cx, cy, cz, Mapsizexchunks, Mapsizeychunks)];
            if (c == null || c.rendered == null || c.rendered.Light == null)
            {
                light = -1;
            }
            else
            {
                light = c.rendered.Light[VectorIndexUtil.Index3d(
                    (x % Game.chunksize) + 1,
                    (y % Game.chunksize) + 1,
                    (z % Game.chunksize) + 1,
                    Game.chunksize + 2,
                    Game.chunksize + 2)];
            }
        }
        return light;
    }

    /// <summary>
    /// Marks the chunk containing the given block and all chunks containing its 6 face-adjacent
    /// neighbours as dirty with light invalidation. Call after any single-block edit.
    /// </summary>
    public void SetBlockDirty(int x, int y, int z)
    {
        Vector3i[] around = ModDrawTerrain.BlocksAround7(new Vector3i(x, y, z));
        for (int i = 0; i < 7; i++)
        {
            Vector3i a = around[i];
            int xx = a.X;
            int yy = a.Y;
            int zz = a.Z;
            if (xx < 0 || yy < 0 || zz < 0 || xx >= MapSizeX || yy >= MapSizeY || zz >= MapSizeZ)
                return;
            SetChunkDirty(xx / Game.chunksize, yy / Game.chunksize, zz / Game.chunksize, true, true);
        }
    }

    /// <summary>
    /// Returns true when the chunk has been rendered at least once
    /// (i.e. its render mesh exists and contains vertex/index data).
    /// </summary>
    public bool IsChunkRendered(int cx, int cy, int cz)
    {
        Chunk c = Chunks[VectorIndexUtil.Index3d(cx, cy, cz, Mapsizexchunks, Mapsizeychunks)];
        if (c == null)
            return false;
        return c.rendered != null && c.rendered.Ids != null;
    }
}