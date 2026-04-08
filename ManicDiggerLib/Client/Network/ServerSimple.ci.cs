// lightweight single-player / embedded server.
// Runs on a background thread via ModServerSimple.OnReadOnlyBackgroundThread.

using ManicDigger.Mods;

public class ServerSimple
{
    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    public ServerSimple()
    {
        _one = 1;
        clients = new ClientSimple[256];
        blockTypes = new Packet_BlockType[GlobalVar.MAX_BLOCKTYPES];
        mods = new ModSimple[128];
        chunks = new ChunkSimple[(MapSizeX / ChunkSize) * (MapSizeY / ChunkSize)][];
        _actions = new Queue<Action>();
        _mainThreadActions = new Queue<Action>();

        ModManagerSimple1 m = new();
        m.Start(this);

        mods[modsCount++] = new ModSimpleDefault();
        mods[modsCount++] = new ModSimpleWorldGenerator();
        for (int i = 0; i < modsCount; i++)
            mods[i].Start(m);

        spawnGlX = MapSizeX / 2;
        spawnGlY = MapSizeZ;
        for (int i = 0; i < modsCount; i++)
        {
            int h = mods[i].GetHeight();
            if (h != -1) spawnGlY = h;
        }
        spawnGlZ = MapSizeY / 2;
    }

    // -------------------------------------------------------------------------
    // Constants & static map size (shared with chunk generators)
    // -------------------------------------------------------------------------

    public const int ChunkSize = 32;
    public static int MapSizeX = 8192;
    public static int MapSizeY = 8192;
    public static int MapSizeZ = 128;

    // -------------------------------------------------------------------------
    // Fields
    // -------------------------------------------------------------------------

    private readonly float _one;
    private NetServer _server;
    private string _saveFilename;
    internal IGamePlatform platform;

    internal ModSimple[] mods;
    internal int modsCount;
    internal ClientSimple[] clients;
    internal int clientsCount;
    internal Packet_BlockType[] blockTypes;
    internal int blockTypesCount;
    internal ChunkSimple[][] chunks;
    internal int chunkdrawdistance = 4;

    private readonly int spawnGlX;
    private readonly int spawnGlY;
    private readonly int spawnGlZ;

    // Main-thread action queue — written from worker threads, drained on server tick.
    private readonly object _mainThreadActionsLock = new();
    private readonly Queue<Action> _mainThreadActions;
    private readonly Queue<Action> _actions;

    // -------------------------------------------------------------------------
    // Startup
    // -------------------------------------------------------------------------

    public void Start(NetServer server, string saveFilename, IGamePlatform platform)
    {
        _server = server;
        _saveFilename = saveFilename;
        this.platform = platform;
    }

    // -------------------------------------------------------------------------
    // Per-tick update (called from server thread)
    // -------------------------------------------------------------------------

    public void Update()
    {
        ProcessPackets();
        NotifyMap();
        NotifyInventory();
        NotifyPing();
        ProcessActions();
    }

    // -------------------------------------------------------------------------
    // Network — receive
    // -------------------------------------------------------------------------

    private void ProcessPackets()
    {
        while (_server.ReadMessage() is { } msg)
        {
            switch (msg.Type)
            {
                case NetworkMessageType.Connect:
                    clients[0] = new ClientSimple
                    {
                        MainSocket = _server,
                        Connection = msg.SenderConnection,
                        chunksseen = new bool[(MapSizeX / ChunkSize) * (MapSizeY / ChunkSize)][],
                    };
                    clientsCount = 1;
                    break;

                case NetworkMessageType.Data:
                    Packet_Client packet = new();
                    Packet_ClientSerializer.DeserializeBuffer(
                        msg.Payload.ToArray(), msg.Payload.Length, packet);
                    ProcessPacket(0, packet);
                    break;

                case NetworkMessageType.Disconnect:
                    break;
            }
        }
    }

    private void ProcessPacket(int clientId, Packet_Client packet)
    {
        switch (packet.GetId())
        {
            case Packet_ClientIdEnum.PlayerIdentification:
                if (packet.Identification == null) return;
                SendPacket(clientId, ServerPackets.Identification(
                    0, MapSizeX, MapSizeY, MapSizeZ, platform.GetGameVersion()));
                clients[clientId].Name = packet.Identification.Username;
                break;

            case Packet_ClientIdEnum.RequestBlob:
                OnRequestBlob(clientId);
                break;

            case Packet_ClientIdEnum.Message:
                SendPacketToAll(ServerPackets.Message(
                    $"{clients[clientId].Name}: &f{packet.Message.Message}"));
                break;

            case Packet_ClientIdEnum.SetBlock:
                OnSetBlock(packet);
                break;

            case Packet_ClientIdEnum.PositionandOrientation:
                clients[clientId].glX = _one * packet.PositionAndOrientation.X / 32;
                clients[clientId].glY = _one * packet.PositionAndOrientation.Y / 32;
                clients[clientId].glZ = _one * packet.PositionAndOrientation.Z / 32;
                break;

            case Packet_ClientIdEnum.InventoryAction:
                // Inventory actions handled by mods — nothing here yet.
                break;
        }
    }

    private void OnRequestBlob(int clientId)
    {
        SendPacket(clientId, ServerPackets.LevelInitialize());

        for (int i = 0; i < blockTypesCount; i++)
            SendPacket(clientId, ServerPackets.BlockType(i, blockTypes[i] ?? new Packet_BlockType()));

        SendPacket(clientId, ServerPackets.BlockTypes());
        SendPacket(clientId, ServerPackets.LevelFinalize());

        for (int i = 0; i < clientsCount; i++)
        {
            if (clients[i] == null) continue;

            clients[i].glX = spawnGlX;
            clients[i].glY = spawnGlY;
            clients[i].glZ = spawnGlZ;

            Packet_PositionAndOrientation pos = new()
            {
                X = (int)(32 * clients[i].glX),
                Y = (int)(32 * clients[i].glY),
                Z = (int)(32 * clients[i].glZ),
                Pitch = 255 / 2,
            };

            Packet_ServerEntity entity = new()
            {
                DrawModel = new Packet_ServerEntityAnimatedModel
                {
                    Model_ = "player.txt",
                    ModelHeight = (int)((_one * 17 / 10) * 32),
                    EyeHeight = (int)((_one * 15 / 10) * 32),
                },
                Position = pos,
            };

            SendPacket(clientId, ServerPackets.EntitySpawn(0, entity));
            SendPacket(clientId, ServerPackets.PlayerStats(100, 100, 100, 100));
        }

        for (int i = 0; i < modsCount; i++)
            mods[i].OnPlayerJoin(clientId);

        clients[clientId].connected = true;
    }

    private void OnSetBlock(Packet_Client packet)
    {
        int x = packet.SetBlock.X;
        int y = packet.SetBlock.Y;
        int z = packet.SetBlock.Z;
        int mode = packet.SetBlock.Mode;

        if (mode == Packet_BlockSetModeEnum.Destroy)
            SendPacketToAll(ServerPackets.SetBlock(x, y, z, 0));
    }

    // -------------------------------------------------------------------------
    // Network — send
    // -------------------------------------------------------------------------

    public void SendPacket(int clientId, Packet_Server packet)
    {
        byte[] data = ServerPackets.Serialize(packet, out int length);
        clients[clientId].Connection.SendMessage(
            data.AsMemory(0, length), MyNetDeliveryMethod.ReliableOrdered);
    }

    private void SendPacketToAll(Packet_Server packet)
    {
        for (int i = 0; i < clientsCount; i++)
            SendPacket(i, packet);
    }

    // -------------------------------------------------------------------------
    // Notifications
    // -------------------------------------------------------------------------

    private void NotifyPing()
    {
        int now = platform.TimeMillisecondsFromStart();
        for (int i = 0; i < clientsCount; i++)
        {
            if (clients[i] == null) continue;
            if (now - clients[i].pingLastMilliseconds > 1000)
            {
                SendPacket(i, ServerPackets.Ping());
                clients[i].pingLastMilliseconds = now;
            }
        }
    }

    private void NotifyInventory()
    {
        for (int i = 0; i < clientsCount; i++)
        {
            if (clients[i] == null) continue;
            if (!clients[i].connected) continue;
            if (!clients[i].inventoryDirty) continue;
            SendPacket(i, ServerPackets.Inventory(clients[i].inventory));
            clients[i].inventoryDirty = false;
        }
    }

    private void NotifyMap()
    {
        for (int i = 0; i < clientsCount; i++)
        {
            if (clients[i] == null) continue;
            if (!clients[i].connected) continue;
            if (clients[i].notifyMapAction != null) continue;
            clients[i].notifyMapAction = CreateNotifyMapAction(this, i);
            platform.QueueUserWorkItem(clients[i].notifyMapAction);
        }
    }

    // -------------------------------------------------------------------------
    // Chunk loading
    // -------------------------------------------------------------------------

    internal Action CreateNotifyMapAction(ServerSimple server, int clientId)
    {
        return () =>
        {
            int[] nearest = new int[3];
            ClientSimple client = clients[clientId];
            server.NearestDirty(clientId, (int)client.glX, (int)client.glZ, (int)client.glY, nearest);

            if (nearest[0] != -1)
                LoadAndSendChunk(nearest[0], nearest[1], nearest[2], clientId);

            clients[clientId].notifyMapAction = null;
        };
    }

    private void LoadAndSendChunk(int x, int y, int z, int clientId)
    {
        ClientSimple c = clients[clientId];
        int pos = MapUtilCi.Index2d(x, y, MapSizeX / ChunkSize);

        c.chunksseen[pos] ??= new bool[MapSizeZ / ChunkSize];
        c.chunksseen[pos][z] = true;

        int[] chunk = new int[32 * 32 * 32];
        for (int i = 0; i < modsCount; i++)
            mods[i].GenerateChunk(x, y, z, chunk);

        byte[] chunkBytes = MiscCi.UshortArrayToByteArray(chunk, 32 * 32 * 32);
        byte[] chunkCompressed = platform.GzipCompress(chunkBytes, 32 * 32 * 32 * 2, out int _);

        QueueMainThreadAction(() => SendPacket(clientId, ServerPackets.ChunkPart(chunkCompressed)));
        QueueMainThreadAction(() => SendPacket(clientId, ServerPackets.Chunk_(x * ChunkSize, y * ChunkSize, z * ChunkSize, ChunkSize)));
    }

    private void NearestDirty(int clientId, int playerX, int playerY, int playerZ, int[] retNearest)
    {
        const int intMaxValue = int.MaxValue;
        int nearestDist = intMaxValue;
        retNearest[0] = retNearest[1] = retNearest[2] = -1;

        int px = playerX / ChunkSize;
        int py = playerY / ChunkSize;
        int pz = playerZ / ChunkSize;

        int chunksXY = MapAreaSize() / ChunkSize / 2;
        int chunksZ = MapAreaSizeZ() / ChunkSize / 2;

        int startX = Math.Max(px - chunksXY, 0);
        int endX = Math.Min(px + chunksXY, MapSizeX / ChunkSize - 1);
        int startY = Math.Max(py - chunksXY, 0);
        int endY = Math.Min(py + chunksXY, MapSizeY / ChunkSize - 1);
        int startZ = Math.Max(pz - chunksZ, 0);
        int endZ = Math.Min(pz + chunksZ, MapSizeZ / ChunkSize - 1);

        ClientSimple client = clients[clientId];
        for (int x = startX; x <= endX; x++)
        {
            for (int y = startY; y <= endY; y++)
            {
                int pos = MapUtilCi.Index2d(x, y, MapSizeX / ChunkSize);
                client.chunksseen[pos] ??= new bool[MapSizeZ / ChunkSize];

                for (int z = startZ; z <= endZ; z++)
                {
                    if (client.chunksseen[pos][z]) continue;

                    int dx = px - x, dy = py - y, dz = pz - z;
                    int dist = dx * dx + dy * dy + dz * dz;
                    if (dist < nearestDist)
                    {
                        nearestDist = dist;
                        retNearest[0] = x;
                        retNearest[1] = y;
                        retNearest[2] = z;
                    }
                }
            }
        }
    }

    private int MapAreaSize() => chunkdrawdistance * ChunkSize * 2;
    private int MapAreaSizeZ() => MapAreaSize();

    // -------------------------------------------------------------------------
    // Main-thread action queue
    // -------------------------------------------------------------------------

    public void QueueMainThreadAction(Action action)
    {
        lock (_mainThreadActionsLock)
            _mainThreadActions.Enqueue(action);
    }

    private void ProcessActions()
    {
        // Move from the shared queue to a local queue under lock,
        // then drain the local queue without holding the lock.
        lock (_mainThreadActionsLock)
        {
            while (_mainThreadActions.TryDequeue(out Action? a))
                _actions.Enqueue(a);
        }

        while (_actions.TryDequeue(out Action? a))
            a();
    }
}

// -------------------------------------------------------------------------
// Mod host — bridges ServerSimple into the game's mod system
// -------------------------------------------------------------------------

public class ModServerSimple : ModBase
{
    internal ServerSimple server;

    public override void OnReadOnlyBackgroundThread(Game game, float dt)
    {
        server.Update();
    }
}

// -------------------------------------------------------------------------
// Client state (server-side view of one connected player)
// -------------------------------------------------------------------------

public class ClientSimple
{
    public ClientSimple()
    {
        inventory = new Packet_Inventory();
        inventory.SetRightHand(new Packet_Item[10], 10, 10);
        for (int i = 0; i < 10; i++)
            inventory.RightHand[i] = new Packet_Item();
    }

    internal string Name;
    internal NetConnection Connection;
    internal NetServer MainSocket;
    internal bool[][] chunksseen;
    internal Action notifyMapAction;
    internal float glX, glY, glZ;
    internal bool connected;
    internal Packet_Inventory inventory;
    internal bool inventoryDirty;
    internal int pingLastMilliseconds;
}

public class ChunkSimple
{
    private readonly int[] data;
}

// -------------------------------------------------------------------------
// Mod manager
// -------------------------------------------------------------------------

public abstract class ModManagerSimple
{
    public abstract BlockTypeSimple CreateBlockType(string name);
    public abstract int GetBlockTypeId(string name);
    public abstract void AddToInventory(int playerId, string block, int amount);
}

public class ModManagerSimple1 : ModManagerSimple
{
    private ServerSimple _server;

    public void Start(ServerSimple server) => _server = server;

    public override BlockTypeSimple CreateBlockType(string name)
    {
        BlockTypeSimple b = new();
        b.SetName(name);
        _server.blockTypes[_server.blockTypesCount++] = b.block;
        return b;
    }

    public override int GetBlockTypeId(string name)
    {
        for (int i = 0; i < _server.blockTypesCount; i++)
        {
            if (_server.blockTypes[i] != null &&
                string.Equals(_server.blockTypes[i].Name, name))
                return i;
        }
        return -1;
    }

    public override void AddToInventory(int playerId, string block, int amount)
    {
        Packet_Inventory inv = _server.clients[playerId].inventory;
        for (int i = 0; i < 10; i++)
        {
            if (inv.RightHand[i].BlockId == 0)
            {
                inv.RightHand[i].BlockId = GetBlockTypeId(block);
                inv.RightHand[i].BlockCount = amount;
                break;
            }
        }
        _server.clients[playerId].inventoryDirty = true;
    }
}

// -------------------------------------------------------------------------
// Mod base & built-in mods
// -------------------------------------------------------------------------

public abstract class ModSimple
{
    public abstract void Start(ModManagerSimple manager);
    public virtual void GenerateChunk(int cx, int cy, int cz, int[] chunk) { }
    public virtual int GetHeight() => -1;
    public virtual void OnPlayerJoin(int playerId) { }
}

public class BlockTypeSimple
{
    internal readonly Packet_BlockType block = new();

    public void SetAllTextures(string texture)
    {
        block.TextureIdTop = block.TextureIdBottom = block.TextureIdFront =
        block.TextureIdBack = block.TextureIdLeft = block.TextureIdRight =
        block.TextureIdForInventory = texture;
    }

    public void SetDrawType(int p) => block.DrawType = p;
    public void SetWalkableType(int p) => block.WalkableType = p;
    public void SetName(string name) => block.Name = name;
    public void SetTextureTop(string p) => block.TextureIdTop = p;
    public void SetTextureBack(string p) => block.TextureIdBack = p;
    public void SetTextureFront(string p) => block.TextureIdFront = p;
    public void SetTextureLeft(string p) => block.TextureIdLeft = p;
    public void SetTextureRight(string p) => block.TextureIdRight = p;
    public void SetTextureBottom(string p) => block.TextureIdBottom = p;
}

public class ModSimpleDefault : ModSimple
{
    private ModManagerSimple _m;

    public override void Start(ModManagerSimple manager)
    {
        _m = manager;

        Add("Empty", Packet_DrawTypeEnum.Empty, Packet_WalkableTypeEnum.Empty);
        AddTextured("Stone", "Stone");
        AddTextured("Dirt", "Dirt");

        BlockTypeSimple grass = Add("Grass", Packet_DrawTypeEnum.Solid, Packet_WalkableTypeEnum.Solid);
        grass.SetTextureTop("Grass");
        grass.SetTextureFront("GrassSide"); grass.SetTextureBack("GrassSide");
        grass.SetTextureLeft("GrassSide"); grass.SetTextureRight("GrassSide");
        grass.SetTextureBottom("Dirt");

        AddTextured("Wood", "OakWood");
        AddTextured("Brick", "Brick");

        // Special blocks
        manager.CreateBlockType("Sponge");
        manager.CreateBlockType("Trampoline");
        AddTextured("Adminium", "Adminium");
        manager.CreateBlockType("Compass");
        manager.CreateBlockType("Ladder");
        manager.CreateBlockType("EmptyHand");
        manager.CreateBlockType("CraftingTable");
        manager.CreateBlockType("Lava");
        manager.CreateBlockType("StationaryLava");
        manager.CreateBlockType("FillStart");
        manager.CreateBlockType("Cuboid");
        manager.CreateBlockType("FillArea");
        manager.CreateBlockType("Minecart");
        manager.CreateBlockType("Rail0");
    }

    private BlockTypeSimple Add(string name, int drawType, int walkableType)
    {
        BlockTypeSimple b = _m.CreateBlockType(name);
        b.SetDrawType(drawType);
        b.SetWalkableType(walkableType);
        return b;
    }

    private void AddTextured(string name, string texture)
    {
        BlockTypeSimple b = Add(name, Packet_DrawTypeEnum.Solid, Packet_WalkableTypeEnum.Solid);
        b.SetAllTextures(texture);
    }

    public override void OnPlayerJoin(int playerId)
    {
        _m.AddToInventory(playerId, "Dirt", 0);
        _m.AddToInventory(playerId, "Stone", 0);
        _m.AddToInventory(playerId, "Wood", 0);
        _m.AddToInventory(playerId, "Brick", 0);
    }
}

public class ModSimpleWorldGenerator : ModSimple
{
    private ModManagerSimple _m;

    public override void Start(ModManagerSimple manager) => _m = manager;

    public override int GetHeight() => 33;

    public override void GenerateChunk(int cx, int cy, int cz, int[] chunk)
    {
        int grass = _m.GetBlockTypeId("Grass");
        int dirt = _m.GetBlockTypeId("Dirt");
        int stone = _m.GetBlockTypeId("Stone");
        int adminium = _m.GetBlockTypeId("Adminium");
        const int height = 32;

        for (int xx = 0; xx < 32; xx++)
        {
            for (int yy = 0; yy < 32; yy++)
            {
                for (int zz = 0; zz < 32; zz++)
                {
                    int z = cz * ServerSimple.ChunkSize + zz;
                    int block = z switch
                    {
                        0 => adminium,
                        _ when z > height => 0,
                        _ when z == height => grass,
                        _ when z > height - 5 => dirt,
                        _ => stone,
                    };
                    chunk[Index3d(xx, yy, zz, ServerSimple.ChunkSize, ServerSimple.ChunkSize)] = block;
                }
            }
        }
    }

    private static int Index3d(int x, int y, int h, int sizeX, int sizeY) =>
        (h * sizeY + y) * sizeX + x;
}