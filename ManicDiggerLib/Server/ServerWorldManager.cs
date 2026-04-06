using PointG = System.Drawing.Point;
using ProtoBuf;
using OpenTK.Mathematics;

public partial class Server
{
    internal int mapsizexchunks() { return d_Map.MapSizeX / chunksize; }
    internal int mapsizeychunks() { return d_Map.MapSizeY / chunksize; }
    internal int mapsizezchunks() { return d_Map.MapSizeZ / chunksize; }

    // generates a new spawn near initial spawn if initial spawn is in water
    public Vector3i DontSpawnPlayerInWater(Vector3i initialSpawn)
    {
        if (IsPlayerPositionDry(initialSpawn.X, initialSpawn.Y, initialSpawn.Z))
        {
            return initialSpawn;
        }

        //find shore
        //bonus +10 because player shouldn't be spawned too close to shore.
        bool bonusset = false;
        int bonus = -1;
        Vector3i pos = initialSpawn;
        for (int i = 0; i < playerareasize / 4 - 5; i++)
        {
            if (IsPlayerPositionDry(pos.X, pos.Y, pos.Z))
            {
                if (!bonusset)
                {
                    bonus = 10;
                    bonusset = true;
                }
            }
            if (bonusset && bonus-- < 0)
            {
                break;
            }
            pos.X++;
            int newblockheight = MapUtil.blockheight(d_Map, 0, pos.X, pos.Y);
            pos.Z = newblockheight + 1;
        }
        return pos;
    }

    private bool IsPlayerPositionDry(int x, int y, int z)
    {
        for (int i = 0; i < 4; i++)
        {
            if (MapUtil.IsValidPos(d_Map, x, y, z - i))
            {
                int blockUnderPlayer = d_Map.GetBlock(x, y, z - i);
                if (BlockTypes[blockUnderPlayer].IsFluid())
                {
                    return false;
                }
            }
        }
        return true;
    }

    public int playerareasize = 256;
    public int centerareasize = 128;

    private PointG PlayerArea(int playerId)
    {
        return MapUtil.PlayerArea(playerareasize, centerareasize, PlayerBlockPosition(clients[playerId]));
    }

    private IEnumerable<Vector3i> PlayerAreaChunks(int playerId)
    {
        PointG p = PlayerArea(playerId);
        for (int x = 0; x < playerareasize / chunksize; x++)
        {
            for (int y = 0; y < playerareasize / chunksize; y++)
            {
                for (int z = 0; z < d_Map.MapSizeZ / chunksize; z++)
                {
                    var v = new Vector3i(p.X + x * chunksize, p.Y + y * chunksize, z * chunksize);
                    if (MapUtil.IsValidPos(d_Map, v.X, v.Y, v.Z))
                    {
                        yield return v;
                    }
                }
            }
        }
    }

    // Interfaces to manipulate server's map.
    public void SetBlock(int x, int y, int z, int blocktype)
    {
        if (MapUtil.IsValidPos(d_Map, x, y, z))
        {
            SetBlockAndNotify(x, y, z, blocktype);
        }
    }

    public int GetBlock(int x, int y, int z)
    {
        if (MapUtil.IsValidPos(d_Map, x, y, z))
        {
            return d_Map.GetBlock(x, y, z);
        }
        return 0;
    }

    public int GetHeight(int x, int y)
    {
        return MapUtil.blockheight(d_Map, 0, x, y);
    }

    public void SetChunk(int x, int y, int z, ushort[] data)
    {
        if (MapUtil.IsValidPos(d_Map, x, y, z))
        {
            x /= chunksize;
            y /= chunksize;
            z /= chunksize;
            ServerChunk c = d_Map.GetChunkValid(x, y, z);
            c ??= new ServerChunk();
            c.data = data;
            c.DirtyForSaving = true;
            d_Map.SetChunkValid(x, y, z, c);
            // update related chunk at clients
            foreach (var k in clients)
            {
                //todo wrong
                //k.Value.chunksseen.Clear();
                Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
            }
        }
    }

    public void SetChunks(Dictionary<Xyz, ushort[]> chunks)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        foreach (var k in chunks)
        {
            if (k.Value == null)
            {
                continue;
            }

            // TODO: check bounds.
            ServerChunk c = d_Map.GetChunkValid(k.Key.X, k.Key.Y, k.Key.Z);
            c ??= new ServerChunk();
            c.data = k.Value;
            c.DirtyForSaving = true;
            d_Map.SetChunkValid(k.Key.X, k.Key.Y, k.Key.Z, c);
        }

        // update related chunk at clients
        foreach (var k in clients)
        {
            //TODO wrong
            //k.Value.chunksseen.Clear();
            Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
        }
    }

    public void SetChunks(int offsetX, int offsetY, int offsetZ, Dictionary<Xyz, ushort[]> chunks)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        foreach (var k in chunks)
        {
            if (k.Value == null)
            {
                continue;
            }

            // TODO: check bounds.
            ServerChunk c = d_Map.GetChunkValid(k.Key.X + offsetX, k.Key.Y + offsetY, k.Key.Z + offsetZ);
            c ??= new ServerChunk();
            c.data = k.Value;
            c.DirtyForSaving = true;
            d_Map.SetChunkValid(k.Key.X + offsetX, k.Key.Y + offsetY, k.Key.Z + offsetZ, c);
        }

        // update related chunk at clients
        foreach (var k in clients)
        {
            //TODO wrong
            //k.Value.chunksseen.Clear();
            Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
        }
    }

    public ushort[] GetChunk(int x, int y, int z)
    {
        if (MapUtil.IsValidPos(d_Map, x, y, z))
        {
            x /= chunksize;
            y /= chunksize;
            z /= chunksize;
            return d_Map.GetChunkValid(x, y, z).data;
        }
        return null;
    }

    public void DeleteChunk(int x, int y, int z)
    {
        if (MapUtil.IsValidPos(d_Map, x, y, z))
        {
            x /= chunksize;
            y /= chunksize;
            z /= chunksize;
            ChunkDb.DeleteChunk(d_ChunkDb, x, y, z);
            d_Map.SetChunkValid(x, y, z, null);
            // update related chunk at clients
            foreach (var k in clients)
            {
                //todo wrong
                //k.Value.chunksseen.Clear();
                Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
            }
        }
    }

    public void DeleteChunks(List<Vector3i> chunkPositions)
    {
        List<Xyz> chunks = [];
        foreach (Vector3i pos in chunkPositions)
        {
            if (MapUtil.IsValidPos(d_Map, pos.X, pos.Y, pos.Z))
            {
                int x = pos.X / chunksize;
                int y = pos.Y / chunksize;
                int z = pos.Z / chunksize;
                d_Map.SetChunkValid(x, y, z, null);
                chunks.Add(new Xyz() { X = x, Y = y, Z = z });
            }
        }
        if (chunks.Count != 0)
        {
            ChunkDb.DeleteChunks(d_ChunkDb, chunks);
            // force to update chunks at clients
            foreach (var k in clients)
            {
                //todo wrong
                //k.Value.chunksseen.Clear();
                Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
            }
        }
    }

    public int[] GetMapSize()
    {
        return [d_Map.MapSizeX, d_Map.MapSizeY, d_Map.MapSizeZ];
    }

    public ushort[] GetChunkFromDatabase(int x, int y, int z, string filename)
    {
        if (MapUtil.IsValidPos(d_Map, x, y, z))
        {
            if (!GameStorePath.IsValidName(filename))
            {
                Console.WriteLine($"Invalid backup filename: {filename}");
                return null;
            }
            if (!Directory.Exists(GameStorePath.gamepathbackup))
            {
                Directory.CreateDirectory(GameStorePath.gamepathbackup);
            }
            string finalFilename = Path.Combine(GameStorePath.gamepathbackup, $"{filename}{MapManipulator.BinSaveExtension}");

            x /= chunksize;
            y /= chunksize;
            z /= chunksize;

            byte[] serializedChunk = ChunkDb.GetChunkFromFile(d_ChunkDb, x, y, z, finalFilename);
            if (serializedChunk != null)
            {
                ServerChunk c = DeserializeChunk(serializedChunk);
                return c.data;
            }
        }
        return null;
    }

    public Dictionary<Xyz, ushort[]> GetChunksFromDatabase(List<Xyz> chunks, string filename)
    {
        if (chunks == null)
        {
            return null;
        }

        if (!GameStorePath.IsValidName(filename))
        {
            Console.WriteLine("Invalid backup filename: " + filename);
            return null;
        }
        if (!Directory.Exists(GameStorePath.gamepathbackup))
        {
            Directory.CreateDirectory(GameStorePath.gamepathbackup);
        }
        string finalFilename = Path.Combine(GameStorePath.gamepathbackup, $"{filename}{MapManipulator.BinSaveExtension}");

        Dictionary<Xyz, ushort[]> deserializedChunks = [];
        Dictionary<Xyz, byte[]> serializedChunks = ChunkDb.GetChunksFromFile(d_ChunkDb, chunks, finalFilename);

        foreach (var k in serializedChunks)
        {
            ServerChunk c = null;
            if (k.Value != null)
            {
                c = DeserializeChunk(k.Value);
            }
            deserializedChunks.Add(k.Key, c.data);
        }
        return deserializedChunks;
    }

    private static ServerChunk DeserializeChunk(byte[] serializedChunk)
    {
        ServerChunk c = Serializer.Deserialize<ServerChunk>(new MemoryStream(serializedChunk));
        //convert savegame to new format
        if (c.dataOld != null)
        {
            c.data = new ushort[chunksize * chunksize * chunksize];
            for (int i = 0; i < c.dataOld.Length; i++)
            {
                c.data[i] = c.dataOld[i];
            }
            c.dataOld = null;
        }
        return c;
    }

    public void SaveChunksToDatabase(List<Vector3i> chunkPositions, string filename)
    {
        if (!GameStorePath.IsValidName(filename))
        {
            Console.WriteLine("Invalid backup filename: " + filename);
            return;
        }
        if (!Directory.Exists(GameStorePath.gamepathbackup))
        {
            Directory.CreateDirectory(GameStorePath.gamepathbackup);
        }
        string finalFilename = Path.Combine(GameStorePath.gamepathbackup, filename + MapManipulator.BinSaveExtension);

        List<DbChunk> dbchunks = [];
        foreach (Vector3i pos in chunkPositions)
        {
            int dx = pos.X / chunksize;
            int dy = pos.Y / chunksize;
            int dz = pos.Z / chunksize;

            ServerChunk cc = new() { data = this.GetChunk(pos.X, pos.Y, pos.Z) };
            MemoryStream ms = new();
            Serializer.Serialize(ms, cc);
            dbchunks.Add(new DbChunk() { Position = new Xyz() { X = dx, Y = dy, Z = dz }, Chunk = ms.ToArray() });
        }
        if (dbchunks.Count != 0)
        {
            IChunkDb d_ChunkDb = new ChunkDbCompressed() { d_ChunkDb = new ChunkDbSqlite(), d_Compression = new CompressionGzip() };
            d_ChunkDb.SetChunksToFile(dbchunks, finalFilename);
        }
        else
        {
            Console.WriteLine(string.Format("0 chunks selected. Nothing to do."));
        }
        Console.WriteLine(string.Format("Saved {0} chunk(s) to database.", dbchunks.Count));
    }
}