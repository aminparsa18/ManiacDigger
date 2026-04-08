using OpenTK.Mathematics;
using System.Diagnostics;
using System.Runtime.InteropServices;

//The main function for loading, unloading and sending chunks to players.
public class ServerSystemNotifyMap : ServerSystem
{
    private const int intMaxValue = 2147483647;

    public override void Update(Server server, float dt)
    {
        Stopwatch s = new();
        s.Start();
        int[] retNearest = new int[3];
        bool loaded = true;
        while (loaded && (s.ElapsedMilliseconds < 10))
        {
            loaded = false;
            foreach (var k in server.clients)
            {
                if (k.Value.state == ClientStateOnServer.Connecting)
                {
                    continue;
                }
                Vector3i playerpos = Server.PlayerBlockPosition(k.Value);

                NearestDirty(server, k.Key, playerpos.X, playerpos.Y, playerpos.Z, retNearest);

                if (retNearest[0] != -1)
                {
                    LoadAndSendChunk(server, k.Key, retNearest[0], retNearest[1], retNearest[2], s);
                    loaded = true;
                }
            }
        }
    }

    private static void NearestDirty(Server server, int clientid, int playerx, int playery, int playerz, int[] retNearest)
    {
        int nearestdist = intMaxValue;
        retNearest[0] = -1;
        retNearest[1] = -1;
        retNearest[2] = -1;
        int px = playerx / Server.chunksize;
        int py = playery / Server.chunksize;
        int pz = playerz / Server.chunksize;

        int chunksxy = MapAreaSize(server) / Server.chunksize / 2;
        int chunksz = MapAreaSizeZ(server) / Server.chunksize / 2;

        int startx = px - chunksxy;
        int endx = px + chunksxy;
        int starty = py - chunksxy;
        int endy = py + chunksxy;
        int startz = pz - chunksz;
        int endz = pz + chunksz;

        if (startx < 0) { startx = 0; }
        if (starty < 0) { starty = 0; }
        if (startz < 0) { startz = 0; }
        if (endx >= server.mapsizexchunks()) { endx = server.mapsizexchunks() - 1; }
        if (endy >= server.mapsizeychunks()) { endy = server.mapsizeychunks() - 1; }
        if (endz >= server.mapsizezchunks()) { endz = server.mapsizezchunks() - 1; }

        for (int x = startx; x <= endx; x++)
        {
            for (int y = starty; y <= endy; y++)
            {
                for (int z = startz; z <= endz; z++)
                {
                    if (server.ClientSeenChunk(clientid, x, y, z))
                    {
                        continue;
                    }
                    {
                        int dx = px - x;
                        int dy = py - y;
                        int dz = pz - z;
                        int dist = dx * dx + dy * dy + dz * dz;
                        if (dist < nearestdist)
                        {
                            nearestdist = dist;
                            retNearest[0] = x;
                            retNearest[1] = y;
                            retNearest[2] = z;
                        }
                    }
                }
            }
        }
    }

    private static void LoadAndSendChunk(Server server, int clientid, int vx, int vy, int vz, Stopwatch s)
    {
        //load
        server.LoadChunk(vx, vy, vz);
        //send
        if (!server.ClientSeenChunk(clientid, vx, vy, vz))
        {
            // only send chunks that haven't been sent yet
            SendChunk(server, clientid, new Vector3i(vx * Server.chunksize, vy * Server.chunksize, vz * Server.chunksize), new Vector3i(vx, vy, vz));
        }
    }

    public static void SendChunk(Server server, int clientid, Vector3i globalpos, Vector3i chunkpos)
    {
        ClientOnServer c = server.clients[clientid];
        ServerChunk chunk = server.d_Map.GetChunk(globalpos.X, globalpos.Y, globalpos.Z);
        server.ClientSeenChunkSet(clientid, chunkpos.X, chunkpos.Y, chunkpos.Z, server.simulationcurrentframe);
        //sent++;
        byte[] compressedchunk;
        if (MapUtil.IsSolidChunk(chunk.data) && chunk.data[0] == 0)
        {
            //don't send empty chunk.
            compressedchunk = null;
        }
        else
        {
            compressedchunk = server.CompressChunkNetwork(chunk.data);
            //todo!
            //commented because it was being sent too early, before full column was generated.
            //if (!c.heightmapchunksseen.ContainsKey(new Vector2i(v.x, v.y)))
            {
                ReadOnlySpan<byte> heightmapchunk = MemoryMarshal.AsBytes(server.d_Map.GetHeightmapChunk(globalpos.X, globalpos.Y).AsSpan());
                byte[] compressedHeightmapChunk = server.d_NetworkCompression.Compress(heightmapchunk);
                Packet_ServerHeightmapChunk p1 = new()
                {
                    X = globalpos.X,
                    Y = globalpos.Y,
                    SizeX = Server.chunksize,
                    SizeY = Server.chunksize,
                    CompressedHeightmap = compressedHeightmapChunk,
                };
                server.SendPacket(clientid, Server.Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.HeightmapChunk, HeightmapChunk = p1 }));
                c.heightmapchunksseen[new Vector2i(globalpos.X, globalpos.Y)] = server.simulationcurrentframe;
            }
        }
        if (compressedchunk != null)
        {
            foreach (byte[] part in Server.Parts(compressedchunk, 1024))
            {
                Packet_ServerChunkPart p1 = new()
                {
                    CompressedChunkPart = part,
                };
                server.SendPacket(clientid, Server.Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.ChunkPart, ChunkPart = p1 }));
            }
        }
        Packet_ServerChunk p = new()
        {
            X = globalpos.X,
            Y = globalpos.Y,
            Z = globalpos.Z,
            SizeX = Server.chunksize,
            SizeY = Server.chunksize,
            SizeZ = Server.chunksize,
        };
        server.SendPacket(clientid, Server.Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Chunk_, Chunk_ = p }));
    }

    public static int MapAreaSize(Server server) { return server.chunkdrawdistance * Server.chunksize * 2; }
    public static int MapAreaSizeZ(Server server) { return MapAreaSize(server); }
}
