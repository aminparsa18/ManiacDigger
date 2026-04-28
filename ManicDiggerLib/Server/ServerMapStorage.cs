public class ServerMapStorage : IMapStorage
{
    internal Server server;
    internal IChunkDb d_ChunkDb;
    internal ICurrentTime d_CurrentTime;
    internal ServerChunk[][] chunks;
    internal bool wasChunkGenerated;

    internal int mapSizeX;
    internal int mapSizeY;
    internal int mapSizeZ;
    public int MapSizeX => mapSizeX;
    public int MapSizeY => mapSizeY;
    public int MapSizeZ => mapSizeZ;

    public int GetBlock(int x, int y, int z)
    {
        ServerChunk chunk = GetChunk(x, y, z);
        return chunk.Data[VectorIndexUtil.Index3d(BlockInChunk(x), BlockInChunk(y), BlockInChunk(z), chunksize, chunksize)];
    }

    public void SetBlock(int x, int y, int z, int tileType)
    {
        ServerChunk chunk = GetChunk(x, y, z);
        unchecked
        {
            chunk.Data[VectorIndexUtil.Index3d(BlockInChunk(x), BlockInChunk(y), BlockInChunk(z), chunksize, chunksize)] = (ushort)tileType;
        }
        chunk.LastChange = d_CurrentTime.GetSimulationCurrentFrame();
        chunk.DirtyForSaving = true;
        UpdateColumnHeight(x, y);
    }

    private void UpdateColumnHeight(int x, int y)
    {
        //todo faster
        int height = mapSizeZ - 1;
        for (int i = mapSizeZ - 1; i >= 0; i--)
        {
            height = i;
            if (!Game.IsTransparentForLight(server.BlockTypes[GetBlock(x, y, i)]))
            {
                break;
            }
        }
        Heightmap.SetBlock(x, y, height);
    }

    public void SetBlockNotMakingDirty(int x, int y, int z, int tileType)
    {
        ServerChunk chunk = GetChunk(x, y, z);
        unchecked
        {
            chunk.Data[VectorIndexUtil.Index3d(BlockInChunk(x), BlockInChunk(y), BlockInChunk(z), chunksize, chunksize)] = (ushort)tileType;
        }
        chunk.DirtyForSaving = true;
        UpdateColumnHeight(x, y);
    }

    public void LoadChunk(int cx, int cy, int cz)
    {
        ServerChunk chunk = GetChunkValid(cx, cy, cz);
        if (chunk == null)
        {
            GetChunk(cx * chunksize, cy * chunksize, cz * chunksize);
        }
    }

    public ServerChunk GetChunk_(int chunkx, int chunky, int chunkz)
    {
        return GetChunk(chunkx * chunksize, chunky * chunksize, chunkz * chunksize);
    }

    public ServerChunk GetChunk(int x, int y, int z)
    {
        x = BlockToChunk(x);
        y = BlockToChunk(y);
        z = BlockToChunk(z);
        ServerChunk chunk = GetChunkValid(x, y, z);
        if (chunk != null)
        {
            int existingNonZero = 0;
            if (chunk.Data != null)
                for (int i = 0; i < chunk.Data.Length; i++)
                    if (chunk.Data[i] != 0) existingNonZero++;
        }
        if (chunk == null)
        {
            wasChunkGenerated = true;
            unchecked
            {
                byte[] serializedChunk = ChunkDbHelper.GetChunk(d_ChunkDb, x, y, z);

                if (serializedChunk != null)
                {
                    ServerChunk deserialized;
                    try
                    {
                        deserialized = DeserializeChunk(serializedChunk);
                        int deserNonZero = 0;
                        if (deserialized.Data != null)
                            for (int i = 0; i < deserialized.Data.Length; i++)
                                if (deserialized.Data[i] != 0) deserNonZero++;
                    }
                    catch (Exception ex)
                    {
                        return null;
                    }

                    SetChunkValid(x, y, z, deserialized);
                    UpdateChunkHeight(x, y, z);
                    return GetChunkValid(x, y, z);
                }

                ushort[] newchunk = new ushort[chunksize * chunksize * chunksize];
                for (int i = 0; i < server.ModEventHandlers.getchunk.Count; i++)
                    server.ModEventHandlers.getchunk[i](x, y, z, newchunk);

                int genNonZero = 0;
                for (int i = 0; i < newchunk.Length; i++)
                    if (newchunk[i] != 0) genNonZero++;

                SetChunkValid(x, y, z, new ServerChunk() { Data = newchunk });
                GetChunkValid(x, y, z).DirtyForSaving = true;
                UpdateChunkHeight(x, y, z);
                return GetChunkValid(x, y, z);
            }
        }
        return chunk;
    }

    private void UpdateChunkHeight(int x, int y, int z)
    {
        unchecked
        {
            for (int xx = 0; xx < chunksize; xx++)
            {
                for (int yy = 0; yy < chunksize; yy++)
                {
                    int inChunkHeight = GetColumnHeightInChunk(GetChunkValid(x, y, z).Data, xx, yy);
                    if (inChunkHeight != 0)
                    {
                        int oldHeight = Heightmap.GetBlock(x * chunksize + xx, y * chunksize + yy);
                        Heightmap.SetBlock(x * chunksize + xx, y * chunksize + yy, Math.Max(oldHeight, inChunkHeight + z * chunksize));
                    }
                }
            }
        }
    }

    private int GetColumnHeightInChunk(ushort[] chunk, int xx, int yy)
    {
        int height = chunksize - 1;
        unchecked
        {
            for (int i = chunksize - 1; i >= 0; i--)
            {
                height = i;
                if (!Game.IsTransparentForLight(server.BlockTypes[chunk[VectorIndexUtil.Index3d(xx, yy, i, chunksize, chunksize)]]))
                {
                    break;
                }
            }
            return height;
        }
    }

    private ServerChunk DeserializeChunk(byte[] serializedChunk)
    {
        ServerChunk c = MemoryPackSerializer.Deserialize<ServerChunk>(serializedChunk);
        unchecked
        {
            if (c.DataOld != null)
            {
                c.Data = new ushort[chunksize * chunksize * chunksize];
                for (int i = 0; i < c.DataOld.Length; i++)
                {
                    c.Data[i] = c.DataOld[i];
                }
                c.DataOld = null;
            }
            if (c.Entities != null)
            {
                c.EntitiesCount = c.Entities.Length;
            }
            return c;
        }
    }

    private int chunksize = 16;
    private double invertedChunkSize = 1.0 / 16;
    private bool isPower2Chunk = true;
    public int ChunkSize
    {
        get { return chunksize; }
        set
        {
            chunksize = value;
            invertedChunkSize = 1.0 / chunksize;
            isPower2Chunk = (chunksize & (chunksize - 1)) == 0 && chunksize != 0;
        }
    }

    public int BlockInChunk(int num)
    {
        if (isPower2Chunk)
            return num & (chunksize - 1);
        else
            return num % chunksize;
    }

    public int BlockToChunk(int num)
    {
        return (int)(num * invertedChunkSize);
    }

    public void Reset(int sizex, int sizey, int sizez)
    {
        mapSizeX = sizex;
        mapSizeY = sizey;
        mapSizeZ = sizez;
        chunks = new ServerChunk[BlockToChunk(sizex) * BlockToChunk(sizey)][];
        Heightmap.Restart();
    }

    public InfiniteMapChunked2dServer Heightmap;

    public ServerChunk GetChunkValid(int cx, int cy, int cz)
    {
        unchecked
        {
            ServerChunk[] column = chunks[VectorIndexUtil.Index2d(cx, cy, BlockToChunk(mapSizeX))];
            if (column == null)
            {
                return null;
            }
            return column[cz];
        }
    }

    public void SetChunkValid(int cx, int cy, int cz, ServerChunk chunk)
    {
        unchecked
        {
            ServerChunk[] column = chunks[VectorIndexUtil.Index2d(cx, cy, BlockToChunk(mapSizeX))];
            if (column == null)
            {
                column = new ServerChunk[BlockToChunk(mapSizeZ)];
                chunks[VectorIndexUtil.Index2d(cx, cy, BlockToChunk(mapSizeX))] = column;
            }
            column[cz] = chunk;
        }
    }

    public void Clear()
    {
        Array.Clear(chunks, 0, chunks.Length);
    }
}