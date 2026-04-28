using ManicDigger;
using OpenTK.Mathematics;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using static ManicDigger.Mods.ModNetworkProcess;

public partial class Server : ICurrentTime, IDropItem
{
    private readonly IGamePlatform gameplatform;

    public List<ServerSystem> Systems { get; set; }

    public Server()
    {
        Systems =
        [
            // This ServerSystem should always be loaded first
            new ServerSystemLoadFirst(),
            // Regular ServerSystems
            new ServerSystemLoadConfig(),
            new ServerSystemHeartbeat(),
            new ServerSystemHttpServer(),
            new ServerSystemUnloadUnusedChunks(),
            new ServerSystemNotifyMap(),
            new ServerSystemNotifyPing(),
            new ServerSystemChunksSimulation(),
            new ServerSystemBanList(),
            new ServerSystemModLoader(),
            new ServerSystemLoadServerClient(),
            new ServerSystemNotifyEntities(),
            new ServerSystemMonsterWalk(),
            // This ServerSystem should always be loaded last
            new ServerSystemLoadLast(),
        ];
        // Not finished
        // systems[systemsCount++] = new ServerSystemSign();
        // systems[systemsCount++] = new ServerSystemPermissionSign();

        //Load translations
        gameplatform = new GamePlatformNative();
        Language.LoadTranslations();

        MainSockets = new NetServer[3];
    }

    public GameExit GameExit { get; set; }
    public ServerMapStorage Map { get; set; }
    public BlockTypeRegistry BlockTypeRegistry { get; set; }
    public CraftingTableTool CraftingTableTool { get; set; }
    public IChunkDb ChunkDb { get; set; }
    public ICompression NetworkCompression { get; set; }
    public NetServer[] MainSockets { get; set; }

    private bool _localConnectionsOnly;
    private int _singlePlayerPort = 25570;
    private string _serverPathLogs = Path.Combine(GameStorePath.GetStorePath(), "Logs");

    public void ServerEventLog(string p)
    {
        if (!Config.ServerEventLogging)
        {
            return;
        }
        if (!Directory.Exists(_serverPathLogs))
        {
            Directory.CreateDirectory(_serverPathLogs);
        }
        string filename = Path.Combine(_serverPathLogs, "DiagLog.Write.txt");
        File.AppendAllText(filename, string.Format("{0} {1}\n", DateTime.Now, p));
    }

    public bool EnableShadows { get; set; } = true;
    public Language Language { get; set; } = new();

    private readonly DateTimeOffset startedAt = DateTimeOffset.UtcNow;
    public TimeSpan Uptime => DateTimeOffset.UtcNow - startedAt;
    private long lastTimestamp = Stopwatch.GetTimestamp();

    public void Process()
    {
        long now = Stopwatch.GetTimestamp();
        float dt = (float)((double)(now - lastTimestamp) / Stopwatch.Frequency);
        lastTimestamp = now;

        for (int i = 0; i < Systems.Count; i++)
        {
            Systems[i].Update(this, dt);
        }

        //Save data
        ProcessSave();
        //Do server stuff
        ProcessMain();

        //When a value of 0 or less is given, don't restart
        if (Config.AutoRestartCycle > 0 && Uptime.TotalHours >= Config.AutoRestartCycle)
        {
            //Restart interval elapsed
            Restart();
        }
    }

    private DateTime lastSave = DateTime.UtcNow;
    private void ProcessSave()
    {
        if ((DateTime.UtcNow - lastSave).TotalMinutes > 2)
        {
            DateTime start = DateTime.UtcNow;
            SaveGlobalData();
            DiagLog.Write(Language.ServerGameSaved(), DateTime.UtcNow - start);
            lastSave = DateTime.UtcNow;
        }
    }

    /// <summary>
    /// Tell the clients the time
    /// </summary>
    private void NotifySeason(int clientid)
    {
        if (Clients[clientid].State == ClientStateOnServer.Connecting)
        {
            return;
        }

        Packet_ServerSeason p = new()
        {
            Hour = _gameTimer.GetQuarterHourPartOfDay(),

            //DayNightCycleSpeedup is used by the client like this:
            //day_length_in_seconds = SecondsInADay / packet.Season.DayNightCycleSpeedup;

            //Set it to 1 if we froze the time, to prevent a division by zero
            DayNightCycleSpeedup = (_gameTimer.SpeedOfTime != 0) ? _gameTimer.SpeedOfTime : 1,
            Moon = 0,
        };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Season, Season = p }));
    }

    private readonly GameTimer _gameTimer = new();
    private int _nLastHourChangeNotify = 0;

    public void ProcessMain()
    {
        if (MainSockets == null)
        {
            return;
        }

        if (_gameTimer.Tick() && _gameTimer.GetQuarterHourPartOfDay() != _nLastHourChangeNotify)
        {
            _nLastHourChangeNotify = _gameTimer.GetQuarterHourPartOfDay();

            foreach (var c in Clients)
            {
                NotifySeason(c.Key);
            }
        }

        double currenttime = GetTime() - starttime;
        double deltaTime = currenttime - oldtime;
        accumulator += deltaTime;
        double dt = SIMULATION_STEP_LENGTH;
        while (accumulator > dt)
        {
            SimulationCurrentFrame++;
            accumulator -= dt;
        }
        oldtime = currenttime;

        NetIncomingMessage msg;
        long tickStart = Stopwatch.GetTimestamp();

        //Process client packets
        for (int i = 0; i < MainSockets.Length; i++)
        {
            NetServer mainSocket = MainSockets[i];
            if (mainSocket == null)
            {
                continue;
            }
            while ((msg = mainSocket.ReadMessage()) != null)
            {
                ProcessNetMessage(msg, mainSocket);
            }
        }
        foreach (var k in Clients)
        {
            k.Value.Socket.Update();
        }

        //Send updates to player
        foreach (var k in Clients)
        {
            //k.Value.notifyMapTimer.Update(delegate { NotifyMapChunks(k.Key, 1); });
            NotifyInventory(k.Key);
            NotifyPlayerStats(k.Key);
        }

        //Process Mod timers
        foreach (var k in Timers)
        {
            k.Key.Update(k.Value);
        }

        //Reset data displayed in /stat
        if ((DateTime.UtcNow - statsupdate).TotalSeconds >= 2)
        {
            statsupdate = DateTime.UtcNow;
            StatTotalPackets = 0;
            StatTotalPacketsLength = 0;
        }

        //Determine how long it took all operations to finish
        lastServerTick = (Stopwatch.GetTimestamp() - tickStart) * 1000 / Stopwatch.Frequency;
        if (lastServerTick > 500)
        {
            //Print an error if the value gets too big - TODO: Adjust
            DiagLog.Write("Server process takes too long! Overloaded? ({0}ms)", lastServerTick);
        }
    }

    public void OnConfigLoaded()
    {
        //Initialize server map
        var map = new ServerMapStorage
        {
            server = this,
            d_CurrentTime = this,
            ChunkSize = 32
        };
        BlockTypes = [];
        map.Heightmap = new InfiniteMapChunked2dServer() { d_Map = map };
        map.Reset(Config.MapSizeX, Config.MapSizeY, Config.MapSizeZ);
        Map = map;

        //Load assets (textures, sounds, etc.)
        string[] datapathspublic = [Path.Combine(PathHelper.DataRoot, "public"), Path.Combine("data", "public")];
        assetLoader = new AssetLoader(datapathspublic);
        LoadAssets();

        //Initialize game components
        var data = new BlockTypeRegistry();
        data.Start();
        BlockTypeRegistry = data;
        CraftingTableTool = new CraftingTableTool() { d_Map = map, d_Data = data };
        _localConnectionsOnly = true;
        var chunkdb = new ChunkDbCompressed() { InnerChunkDb = new ChunkDbSqlite(), Compression = new CompressionGzip() };
        ChunkDb = chunkdb;
        map.d_ChunkDb = chunkdb;
        NetworkCompression = new CompressionGzip();
        _dataItems = new GameDataItemsBlocks() { d_Data = data };
        if (MainSockets == null)
        {
            MainSockets = new NetServer[3];
            MainSockets[0] = new EnetNetServer(gameplatform);
            if (MainSockets[1] == null)
            {
                MainSockets[1] = new WebSocketNetServer();
            }
            if (MainSockets[2] == null)
            {
                MainSockets[2] = new TcpNetServer();
            }
        }

        AllPrivileges.AddRange(ServerClientMisc.Privilege.All());

        //Load the savegame file
        if (!Directory.Exists(GameStorePath.gamepathsaves))
        {
            Directory.CreateDirectory(GameStorePath.gamepathsaves);
        }
        DiagLog.Write(Language.ServerLoadingSavegame());
        if (!File.Exists(GetSaveFilename()))
        {
            DiagLog.Write(Language.ServerCreatingSavegame());
        }
        LoadGame(GetSaveFilename());
        DiagLog.Write(Language.ServerLoadedSavegame() + GetSaveFilename());

        if (_localConnectionsOnly)
        {
            Config.Port = _singlePlayerPort;
        }
        Start(Config.Port);

        // server monitor
        if (Config.ServerMonitor)
        {
            this.serverMonitor = new ServerMonitor(this, GameExit);
            this.serverMonitor.Start();
        }

        // set up server console interpreter
        this.ServerConsoleClient = new ClientOnServer()
        {
            Id = ServerConsoleId,
            PlayerName = "Server",
            QueryClient = false
        };
        ManicDigger.Group serverGroup = new()
        {
            Name = "Server",
            Level = 255,
            GroupPrivileges = []
        };
        serverGroup.GroupPrivileges = AllPrivileges;
        serverGroup.GroupColor = ServerClientMisc.ClientColor.Red;
        ServerConsoleClient.AssignGroup(serverGroup);

        if (Config.AutoRestartCycle > 0)
        {
            DiagLog.Write("AutoRestartInterval: {0}", Config.AutoRestartCycle);
        }
        else
        {
            DiagLog.Write("AutoRestartInterval: DISABLED");
        }
    }

    private void Start(int port)
    {
        Port = port;
        MainSockets[0].SetPort(port);
        MainSockets[0].Start();
        if (MainSockets[1] != null)
        {
            MainSockets[1].SetPort(port);
            MainSockets[1].Start();
        }
        if (MainSockets[2] != null)
        {
            MainSockets[2].SetPort(port + 2);
            MainSockets[2].Start();
        }
    }

    public int Port { get; set; }
    public void Stop()
    {
        DiagLog.Write("[SERVER] Doing last tick...");
        ProcessMain();
        //Maybe inform mods about shutdown?
        DiagLog.Write("[SERVER] Saving data...");
        DateTime start = DateTime.UtcNow;
        SaveGlobalData();
        DiagLog.Write(Language.ServerGameSaved(), DateTime.UtcNow - start);
        DiagLog.Write("[SERVER] Stopped the server!");
    }

    public void Restart()
    {
        //Server shall exit and be restarted
        GameExit.SetRestart(true);
        GameExit.SetExit(true);
    }

    public void Exit()
    {
        //Server shall be shutdown
        GameExit.SetRestart(false);
        GameExit.SetExit(true);
    }

    private ServerMonitor serverMonitor;

    public List<string> AllPrivileges { get; set; } = [];
    public List<string> ModPaths { get; set; } = [];
    public ModManager1 ModManager { get; set; }
    public string GameMode { get; set; } = "Fortress";
    public int ServerConsoleId { get; } = -1;
    public ClientOnServer ServerConsoleClient { get; set; }
    public int Seed { get; set; }

    public void ReceiveServerConsole(string message)
    {
        if (string.IsNullOrEmpty(message)) return;

        if (message.StartsWith('/'))
        {
            int spaceIndex = message.IndexOf(' ');
            string command = (spaceIndex < 0 ? message[1..] : message[1..spaceIndex]);
            string argument = spaceIndex < 0 ? "" : message[(spaceIndex + 1)..];
            CommandInterpreter(ServerConsoleId, command, argument);
            return;
        }

        if (message.StartsWith('.'))
        {
            // client commands not handled server-side
            return;
        }

        // chat message
        SendMessageToAll(string.Format("{0}: {1}", ServerConsoleClient.ColoredPlayername(colorNormal), message));
        ChatLog(string.Format("{0}: {1}", ServerConsoleClient.PlayerName, message));
    }

    private void LoadGame(string filename)
    {
        ChunkDb.Open(filename);
        byte[] globaldata = ChunkDb.GetGlobalData();
        if (globaldata == null)
        {
            //no savegame yet
            if (Config.RandomSeed)
            {
                Seed = new Random().Next();
            }
            else
            {
                Seed = Config.Seed;
            }
            ChunkDb.SetGlobalData(SaveGame());
            this._gameTimer.Init(TimeSpan.Parse("08:00").Ticks);
            return;
        }
        ManicDiggerSave save = MemoryPackSerializer.Deserialize<ManicDiggerSave>(globaldata);
        Seed = save.Seed;
        Map.Reset(Map.MapSizeX, Map.MapSizeY, Map.MapSizeZ);
        if (Config.IsCreative) this.Inventory = Inventory = new Dictionary<string, Inventory>(StringComparer.InvariantCultureIgnoreCase);
        else this.Inventory = save.Inventory;
        this.PlayerStats = save.PlayerStats;
        this.SimulationCurrentFrame = (int)save.SimulationCurrentFrame;
        this._gameTimer.Init(save.TimeOfDay);
        this.LastMonsterId = save.LastMonsterId;
        this.ModData = save.ModData;
    }

    public void ChatLog(string p)
    {
        if (!Config.ChatLogging)
        {
            return;
        }
        if (!Directory.Exists(_serverPathLogs))
        {
            Directory.CreateDirectory(_serverPathLogs);
        }
        string filename = Path.Combine(_serverPathLogs, "ChatLog.txt");
        File.AppendAllText(filename, string.Format("{0} {1}\n", DateTime.Now, p));
    }

    public List<Action> OnLoad { get; set; } = [];
    public List<Action> OnSave { get; set; } = [];
    public int LastMonsterId { get; set; }
    public Dictionary<string, Inventory> Inventory { get; set; } = new(StringComparer.InvariantCultureIgnoreCase);
    public Dictionary<string, byte[]> ModData { get; set; } = [];

    private Dictionary<string, PacketServerPlayerStats> PlayerStats = new(StringComparer.InvariantCultureIgnoreCase);

    private byte[] SaveGame()
    {
        for (int i = 0; i < OnSave.Count; i++)
            OnSave[i]();

        ManicDiggerSave save = new()
        {
            Seed = Seed,
            SimulationCurrentFrame = SimulationCurrentFrame,
            TimeOfDay = _gameTimer.Time.Ticks,
            LastMonsterId = LastMonsterId,
            PlayerStats = PlayerStats,
            ModData = ModData,
        };

        SaveAllLoadedChunks();

        if (!Config.IsCreative)
            save.Inventory = Inventory;

        return MemoryPackSerializer.Serialize(save);
    }

    public bool BackupDatabase(string backupFilename)
    {
        if (!GameStorePath.IsValidName(backupFilename))
        {
            DiagLog.Write($"{Language.ServerInvalidBackupName()}{backupFilename}");
            return false;
        }
        if (!Directory.Exists(GameStorePath.gamepathbackup))
        {
            Directory.CreateDirectory(GameStorePath.gamepathbackup);
        }
        string finalFilename = Path.Combine(GameStorePath.gamepathbackup, $"{backupFilename}{FileConstatns.DbFileExtension}");
        ChunkDb.Backup(finalFilename);
        return true;
    }

    public bool LoadDatabase(string filename)
    {
        Map.d_ChunkDb = ChunkDb;
        SaveAll();
        if (filename != GetSaveFilename())
        {
            //todo load
        }
        var dbcompressed = (ChunkDbCompressed)Map.d_ChunkDb;
        var db = (ChunkDbSqlite)dbcompressed.InnerChunkDb;
        db.ClearTemporaryChunks();
        Map.Clear();
        LoadGame(filename);
        foreach (var k in Clients)
        {
            //SendLevelInitialize(k.Key);
            Array.Clear(k.Value.chunksseen, 0, k.Value.chunksseen.Length);
            k.Value.chunksseenTime.Clear();
        }
        return true;
    }

    private void SaveAllLoadedChunks()
    {
        List<DbChunk> tosave = [];
        for (int cx = 0; cx < Map.MapSizeX / ChunkSize; cx++)
        {
            for (int cy = 0; cy < Map.MapSizeY / ChunkSize; cy++)
            {
                for (int cz = 0; cz < Map.MapSizeZ / ChunkSize; cz++)
                {
                    ServerChunk c = Map.GetChunkValid(cx, cy, cz);
                    if (c == null)
                    {
                        continue;
                    }
                    if (!c.DirtyForSaving)
                    {
                        continue;
                    }
                    c.DirtyForSaving = false;
                    tosave.Add(new DbChunk() { Position = new Vector3i() { X = cx, Y = cy, Z = cz }, Chunk = MemoryPackSerializer.Serialize(c) });
                    if (tosave.Count > 200)
                    {
                        ChunkDb.SetChunks(tosave);
                        tosave.Clear();
                    }
                }
            }
        }
        ChunkDb.SetChunks(tosave);
    }

    private const string SaveFilenameWithoutExtension = "default";
    public string SaveFilenameOverride { get; set; }

    public string GetSaveFilename()
    {
        if (SaveFilenameOverride != null)
        {
            return SaveFilenameOverride;
        }
        return Path.Combine(GameStorePath.gamepathsaves, SaveFilenameWithoutExtension + FileConstatns.DbFileExtension);
    }

    private void SaveGlobalData()
    {
        ChunkDb.SetGlobalData(SaveGame());
    }

    public ServerConfig Config { get; set; }
    public ServerBanlist BanList { get; set; }
    public bool ConfigNeedsSaving { get; set; }

    public void Dispose()
    {
        if (!disposed)
        {
            //d_MainSocket.Disconnect(false);
        }
        disposed = true;
    }

    private bool disposed = false;
    private readonly double starttime = GetTime();

    private static double GetTime()
    {
        return (double)DateTime.UtcNow.Ticks / (10 * 1000 * 1000);
    }

    public int SimulationCurrentFrame { get; set; }

    private double oldtime;
    private double accumulator;
    private float lastServerTick;
    private int lastClientId;

    public int GenerateClientId()
    {
        int i = 0;
        while (Clients.ContainsKey(i))
        {
            i++;
        }
        return i;
    }

    public GamePlatformNative Platform { get; set; } = new();

    private void ProcessNetMessage(NetIncomingMessage msg, NetServer mainSocket)
    {
        if (msg.SenderConnection == null)
        {
            return;
        }
        int clientid = -1;
        foreach (var k in Clients)
        {
            if (k.Value.MainSocket != mainSocket)
            {
                continue;
            }
            if (k.Value.Socket.EqualsConnection(msg.SenderConnection))
            {
                clientid = k.Key;
            }
        }
        switch (msg.Type)
        {
            case NetworkMessageType.Connect:
                //new connection
                NetConnection client1 = msg.SenderConnection;

                ClientOnServer c = new()
                {
                    MainSocket = mainSocket,
                    Socket = client1
                };

                c.Ping.Timeout = TimeSpan.FromSeconds(Config?.ClientConnectionTimeout ?? 30);
                c.chunksseen = new bool[Map.MapSizeX / ChunkSize * Map.MapSizeY / ChunkSize * Map.MapSizeZ / ChunkSize];
                lock (Clients)
                {
                    this.lastClientId = this.GenerateClientId();
                    c.Id = lastClientId;
                    Clients[lastClientId] = c;
                }
                //clientid = c.Id;
                c.NotifyMapTimer = new Timer()
                {
                    INTERVAL = 1.0 / SEND_CHUNKS_PER_SECOND,
                };
                c.NotifyMonstersTimer = new Timer()
                {
                    INTERVAL = 1.0 / SEND_MONSTER_UDAPTES_PER_SECOND,
                };
                break;
            case NetworkMessageType.Data:
                if (clientid == -1)
                {
                    break;
                }
                // process packet
                TotalReceivedBytes += msg.Payload.Length;
                TryReadPacket(clientid, msg.Payload.ToArray());
                break;
            case NetworkMessageType.Disconnect:
                DiagLog.Write("Client disconnected.");
                KillPlayer(clientid);
                break;
        }
    }

    private DateTime statsupdate;

    public Dictionary<Timer, Timer.Tick> Timers { get; set; } = [];

    private void NotifyPing(int targetClientId, int ping)
    {
        foreach (var k in Clients)
        {
            SendPlayerPing(k.Key, targetClientId, ping);
        }
    }

    private void SendPlayerPing(int recipientClientId, int targetClientId, int ping)
    {
        Packet_ServerPlayerPing p = new()
        {
            ClientId = targetClientId,
            Ping = ping
        };
        SendPacket(recipientClientId, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.PlayerPing, PlayerPing = p }));
    }

    //on exit
    private void SaveAll()
    {
        for (int x = 0; x < Map.MapSizeX / ChunkSize; x++)
        {
            for (int y = 0; y < Map.MapSizeY / ChunkSize; y++)
            {
                for (int z = 0; z < Map.MapSizeZ / ChunkSize; z++)
                {
                    if (Map.GetChunkValid(x, y, z) != null)
                    {
                        DoSaveChunk(x, y, z, Map.GetChunkValid(x, y, z));
                    }
                }
            }
        }
        SaveGlobalData();
    }

    internal void DoSaveChunk(int x, int y, int z, ServerChunk c)
    {
        ChunkDbHelper.SetChunk(ChunkDb, x, y, z, MemoryPackSerializer.Serialize(c));
    }

    private const int SEND_CHUNKS_PER_SECOND = 10;
    private const int SEND_MONSTER_UDAPTES_PER_SECOND = 3;

    public void LoadChunk(int cx, int cy, int cz)
    {
        Map.LoadChunk(cx, cy, cz);
    }

    public const string InvalidPlayerName = "invalid";

    public void NotifyInventory(int clientid)
    {
        ClientOnServer c = Clients[clientid];
        if (c.IsInventoryDirty && c.PlayerName != InvalidPlayerName && !c.UsingFill)
        {
            Packet_ServerInventory p = ConvertInventory(GetPlayerInventory(c.PlayerName));
            SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.FiniteInventory, Inventory = p }));
            c.IsInventoryDirty = false;
        }
    }

    private static Packet_ServerInventory ConvertInventory(Inventory inv)
    {
        if (inv == null)
        {
            return null;
        }
        Packet_ServerInventory p = new();
        if (inv != null)
        {
            p.Inventory = new Packet_Inventory
            {
                Boots = inv.Boots,
                DragDropItem = inv.DragDropItem,
                Gauntlet = inv.Gauntlet,
                Helmet = inv.Helmet,
                // todo
                //p.Inventory.Items = inv.Inventory.Items;
                Items = new Packet_PositionItem[inv.Items.Count],
            };
            {
                int i = 0;
                foreach (var k in inv.Items)
                {
                    Packet_PositionItem item = new()
                    {
                        Key_ = $"{k.Key.X} {k.Key.Y}",
                        Value_ = k.Value,
                        X = k.Key.X,
                        Y = k.Key.Y
                    };
                    p.Inventory.Items[i++] = item;
                }
            }
            p.Inventory.MainArmor = inv.MainArmor;
            p.Inventory.RightHand = new InventoryItem[10];
            for (int i = 0; i < inv.RightHand.Length; i++)
            {
                if (inv.RightHand[i] == null)
                {
                    p.Inventory.RightHand[i] = new InventoryItem();
                }
                else
                {
                    p.Inventory.RightHand[i] = inv.RightHand[i];
                }
            }
        }
        return p;
    }

    public void NotifyPlayerStats(int clientid)
    {
        ClientOnServer c = Clients[clientid];
        if (c.IsPlayerStatsDirty && c.PlayerName != InvalidPlayerName)
        {
            PacketServerPlayerStats stats = GetPlayerStats(c.PlayerName);
            SendPacket(clientid, ServerPackets.PlayerStats(stats.CurrentHealth, stats.MaxHealth, stats.CurrentOxygen, stats.MaxOxygen));
            c.IsPlayerStatsDirty = false;
        }
    }

    private void HitMonsters(int clientid, int health)
    {
        ClientOnServer c = Clients[clientid];
        int mapx = c.PositionMul32GlX / 32;
        int mapy = c.PositionMul32GlZ / 32;
        int mapz = c.PositionMul32GlY / 32;
        //3x3x3 chunks
        for (int xx = -1; xx < 2; xx++)
        {
            for (int yy = -1; yy < 2; yy++)
            {
                for (int zz = -1; zz < 2; zz++)
                {
                    int cx = (mapx / ChunkSize) + xx;
                    int cy = (mapy / ChunkSize) + yy;
                    int cz = (mapz / ChunkSize) + zz;
                    if (!VectorUtils.IsValidChunkPos(Map, cx, cy, cz, ChunkSize))
                    {
                        continue;
                    }
                    ServerChunk chunk = Map.GetChunkValid(cx, cy, cz);
                    if (chunk == null || chunk.Monsters == null)
                    {
                        continue;
                    }
                    foreach (Monster m in chunk.Monsters)
                    {
                        Vector3i mpos = new() { X = m.X, Y = m.Y, Z = m.Z };
                        Vector3i ppos = new()
                        {
                            X = Clients[clientid].PositionMul32GlX / 32,
                            Y = Clients[clientid].PositionMul32GlZ / 32,
                            Z = Clients[clientid].PositionMul32GlY / 32
                        };
                        if (VectorUtils.DistanceSquared(mpos, ppos) < 15)
                        {
                            m.Health -= health;
                            //DiagLog.Write("HIT! -2 = " + m.Health);
                            if (m.Health <= 0)
                            {
                                chunk.Monsters.Remove(m);
                                SendSound(clientid, "death.wav", m.X, m.Y, m.Z);
                                break;
                            }
                            SendSound(clientid, "grunt2.wav", m.X, m.Y, m.Z);
                            break;
                        }
                    }
                }
            }
        }
    }

    public Inventory GetPlayerInventory(string playername)
    {
        Inventory ??= new Dictionary<string, Inventory>(StringComparer.InvariantCultureIgnoreCase);
        if (!Inventory.TryGetValue(playername, out Inventory? value))
        {
            value = StartInventory();
            Inventory[playername] = value;
        }
        return value;
    }

    public void ResetPlayerInventory(ClientOnServer client)
    {
        Inventory ??= new Dictionary<string, Inventory>(StringComparer.InvariantCultureIgnoreCase);
        this.Inventory[client.PlayerName] = StartInventory();
        client.IsInventoryDirty = true;
        NotifyInventory(client.Id);
    }

    public PacketServerPlayerStats GetPlayerStats(string playername)
    {
        PlayerStats ??= new Dictionary<string, PacketServerPlayerStats>(StringComparer.InvariantCultureIgnoreCase);
        if (!PlayerStats.TryGetValue(playername, out PacketServerPlayerStats? value))
        {
            value = StartPlayerStats();
            PlayerStats[playername] = value;
        }
        return value;
    }

    private Inventory StartInventory()
    {
        Inventory inv = ManicDigger.Inventory.Create();
        int x = 0;
        int y = 0;
        InventoryUtil util = GetInventoryUtil(inv);

        foreach (var (id, blockType) in BlockTypes)
        {
            BlockTypeRegistry.StartInventoryAmount.TryGetValue(id, out int amount);

            bool shouldAdd = Config.IsCreative
                ? amount > 0 || blockType.IsBuildable
                : amount > 0;

            if (!shouldAdd) continue;

            inv.Items.Add(new GridPoint(x, y), new InventoryItem
            {
                InventoryItemType = InventoryItemType.Block,
                BlockId = id,
                BlockCount = Config.IsCreative ? 0 : amount
            });

            x++;
            if (x >= util.CellCountX)
            {
                x = 0;
                y++;
            }
        }
        return inv;
    }

    private static PacketServerPlayerStats StartPlayerStats()
    {
        var p = new PacketServerPlayerStats
        {
            CurrentHealth = 20,
            MaxHealth = 20,
            CurrentOxygen = 10,
            MaxOxygen = 10
        };
        return p;
    }

    public static Vector3i PlayerBlockPosition(ClientOnServer c)
    {
        return new Vector3i(c.PositionMul32GlX / 32, c.PositionMul32GlZ / 32, c.PositionMul32GlY / 32);
    }

    public void KillPlayer(int clientid)
    {
        if (!Clients.TryGetValue(clientid, out ClientOnServer? value))
        {
            return;
        }
        if (value.QueryClient)
        {
            Clients.Remove(clientid);
            this.serverMonitor.RemoveMonitorClient(clientid);
            return;
        }
        for (int i = 0; i < ModEventHandlers.onplayerleave.Count; i++)
        {
            ModEventHandlers.onplayerleave[i](clientid);
        }
        for (int i = 0; i < ModEventHandlers.onplayerdisconnect.Count; i++)
        {
            ModEventHandlers.onplayerdisconnect[i](clientid);
        }
        string coloredName = Clients[clientid].ColoredPlayername(colorNormal);
        string name = Clients[clientid].PlayerName;
        Clients.Remove(clientid);
        if (Config.ServerMonitor)
        {
            this.serverMonitor.RemoveMonitorClient(clientid);
        }
        foreach (var k in Clients)
        {
            SendPacket(k.Key, ServerPackets.EntityDespawn(clientid));
        }
        if (name != "invalid")
        {
            SendMessageToAll(string.Format(Language.ServerPlayerDisconnect(), coloredName));
            DiagLog.Write(string.Format("{0} disconnects.", name));
        }
    }

    public string ReceivedKey { get; set; }
    private DateTime lastQuery = DateTime.UtcNow;
    private int pistolcycle;

    private void TryReadPacket(int clientid, byte[] data)
    {
        ClientOnServer c = Clients[clientid];
        Packet_Client packet = MemoryPackSerializer.Deserialize<Packet_Client>(data.AsSpan(0, data.Length));
        if (c.QueryClient)
        {
            if (!(packet.Id == PacketType.ServerQuery || packet.Id == PacketType.PlayerIdentification))
            {
                //Reject all packets other than ServerQuery or PlayerIdentification
                DiagLog.Write("Rejected packet from not authenticated client");
                SendPacket(clientid, ServerPackets.DisconnectPlayer("Either send PlayerIdentification or ServerQuery!"));
                KillPlayer(clientid);
                return;
            }
        }
        if (Config.ServerMonitor && !this.serverMonitor.CheckPacket(clientid, packet))
        {
            DiagLog.Write("Server monitor rejected packet");
            return;
        }
        int realPlayers = 0;
        switch (packet.Id)
        {
            case PacketType.PingReply:
                Clients[clientid].Ping.Receive(Platform);
                Clients[clientid].LastPing = (float)Clients[clientid].Ping.RoundtripMilliseconds / 1000;
                this.NotifyPing(clientid, Clients[clientid].Ping.RoundtripMilliseconds);
                break;
            case PacketType.PlayerIdentification:
                {
                    foreach (var cl in Clients)
                    {
                        if (cl.Value.IsBot)
                        {
                            continue;
                        }
                        realPlayers++;
                    }
                    if (realPlayers > Config.MaxClients)
                    {
                        SendPacket(clientid, ServerPackets.DisconnectPlayer(Language.ServerTooManyPlayers()));
                        KillPlayer(clientid);
                        break;
                    }
                    if (Config.IsPasswordProtected() && packet.Identification.ServerPassword != Config.Password)
                    {
                        DiagLog.Write(string.Format("{0} fails to join (invalid server password).", packet.Identification.Username));
                        DiagLog.Write(string.Format("{0} fails to join (invalid server password).", packet.Identification.Username));
                        SendPacket(clientid, ServerPackets.DisconnectPlayer(Language.ServerPasswordInvalid()));
                        KillPlayer(clientid);
                        break;
                    }
                    SendServerIdentification(clientid);
                    string username = packet.Identification.Username;

                    // allowed characters in username: a-z,A-Z,0-9,-,_ length: 1-16
                    Regex allowedUsername = new(@"^(\w|-){1,16}$");

                    if (string.IsNullOrEmpty(username) || !allowedUsername.IsMatch(username))
                    {
                        SendPacket(clientid, ServerPackets.DisconnectPlayer(Language.ServerUsernameInvalid()));
                        DiagLog.Write(string.Format("{0} can't join (invalid username: {1}).", c.Socket.RemoteEndPoint().AddressToString(), username));
                        KillPlayer(clientid);
                        break;
                    }

                    bool isClientLocalhost = c.Socket.RemoteEndPoint().AddressToString() == "127.0.0.1";
                    bool verificationFailed = false;

                    if ((ComputeMd5(Config.Key.Replace("-", "") + username) != packet.Identification.VerificationKey)
                        && (!isClientLocalhost))
                    {
                        //Account verification failed.
                        username = $"~{username}";
                        verificationFailed = true;
                    }

                    if (!Config.AllowGuests && verificationFailed)
                    {
                        SendPacket(clientid, ServerPackets.DisconnectPlayer(Language.ServerNoGuests()));
                        KillPlayer(clientid);
                        break;
                    }

                    //When a duplicate user connects, append a number to name.
                    foreach (var k in Clients)
                    {
                        if (k.Value.PlayerName.Equals(username, StringComparison.InvariantCultureIgnoreCase))
                        {
                            // If duplicate is a registered user, kick duplicate. It is likely that the user lost connection before.
                            if (!verificationFailed && !isClientLocalhost)
                            {
                                KillPlayer(k.Key);
                                break;
                            }

                            // Duplicates are handled as guests.
                            username = GenerateUsername(username);
                            if (!username.StartsWith('~')) { username = $"~{username}"; }
                            break;
                        }
                    }
                    Clients[clientid].PlayerName = username;

                    // Assign group to new client
                    //Check if client is in ServerClient.txt and assign corresponding group.
                    bool exists = false;
                    foreach (Client client in ServerClient.Clients)
                    {
                        if (client.Name.Equals(username, StringComparison.InvariantCultureIgnoreCase))
                        {
                            foreach (ManicDigger.Group clientGroup in ServerClient.Groups)
                            {
                                if (clientGroup.Name.Equals(client.Group))
                                {
                                    exists = true;
                                    Clients[clientid].AssignGroup(clientGroup);
                                    break;
                                }
                            }
                            break;
                        }
                    }
                    if (!exists)
                    {
                        //Assign admin group if client connected from localhost
                        if (isClientLocalhost)
                        {
                            Clients[clientid].AssignGroup(ServerClient.Groups.Find(v => v.Name == "Admin"));
                        }
                        else if (Clients[clientid].PlayerName.StartsWith("~"))
                        {
                            Clients[clientid].AssignGroup(this.DefaultGroupGuest);
                        }
                        else
                        {
                            Clients[clientid].AssignGroup(this.DefaultGroupRegistered);
                        }
                    }
                    this.SetFillAreaLimit(clientid);
                    this.SendFreemoveState(clientid, Clients[clientid].Privileges.Contains(ServerClientMisc.Privilege.freemove));
                    c.QueryClient = false;
                    Clients[clientid].Entity.DrawName.Name = username;
                    if (Config.EnablePlayerPushing)
                    {
                        // Player pushing
                        Clients[clientid].Entity.Push = new ServerEntityPush
                        {
                            Range = 1
                        };
                    }
                    PlayerEntitySetDirty(clientid);
                }
                break;
            case PacketType.RequestBlob:
                {
                    // Set player's spawn position
                    Vector3i position = GetPlayerSpawnPositionMul32(clientid);

                    Clients[clientid].PositionMul32GlX = position.X;
                    Clients[clientid].PositionMul32GlY = position.Y + (int)(0.5 * 32);
                    Clients[clientid].PositionMul32GlZ = position.Z;

                    string ip = Clients[clientid].Socket.RemoteEndPoint().AddressToString();
                    SendMessageToAll(string.Format(Language.ServerPlayerJoin(), Clients[clientid].ColoredPlayername(colorNormal)));
                    DiagLog.Write(string.Format("{0} {1} joins.", Clients[clientid].PlayerName, ip));
                    SendMessage(clientid, colorSuccess + Config.WelcomeMessage);
                    SendBlobs(clientid, packet.RequestBlob.RequestedMd5);
                    SendBlockTypes(clientid);
                    SendTranslations(clientid);
                    SendSunLevels(clientid);
                    SendLightLevels(clientid);
                    SendCraftingRecipes(clientid);

                    for (int i = 0; i < ModEventHandlers.onplayerjoin.Count; i++)
                    {
                        ModEventHandlers.onplayerjoin[i](clientid);
                    }

                    SendPacket(clientid, ServerPackets.LevelFinalize());
                    Clients[clientid].State = ClientStateOnServer.Playing;
                    NotifySeason(clientid);
                }
                break;
            case PacketType.SetBlock:
                {
                    int x = packet.SetBlock.X;
                    int y = packet.SetBlock.Y;
                    int z = packet.SetBlock.Z;
                    if (packet.SetBlock.Mode == PacketBlockSetMode.Use)	//Check if player only uses block
                    {
                        if (!CheckUsePrivileges(clientid, x, y, z))
                        {
                            break;
                        }
                        DoCommandBuild(clientid, true, packet.SetBlock);
                    }
                    else	//Player builds, deletes or uses block with tool
                    {
                        if (!CheckBuildPrivileges(clientid, x, y, z, packet.SetBlock.Mode))
                        {
                            SendSetBlock(clientid, x, y, z, Map.GetBlock(x, y, z)); //revert
                            break;
                        }
                        if (!DoCommandBuild(clientid, true, packet.SetBlock))
                        {
                            SendSetBlock(clientid, x, y, z, Map.GetBlock(x, y, z)); //revert
                        }
                        //Only log when building/destroying blocks. Prevents VandalFinder entries
                        if (packet.SetBlock.Mode != PacketBlockSetMode.UseWithTool)
                            BuildLog(string.Format("{0} {1} {2} {3} {4} {5}", x, y, z, c.PlayerName, c.Socket.RemoteEndPoint().AddressToString(), Map.GetBlock(x, y, z)));
                    }
                }
                break;
            case PacketType.FillArea:
                {
                    if (!Clients[clientid].Privileges.Contains(ServerClientMisc.Privilege.build))
                    {
                        SendMessage(clientid, colorError + Language.ServerNoBuildPrivilege());
                        break;
                    }
                    if (Clients[clientid].IsSpectator && !Config.AllowSpectatorBuild)
                    {
                        SendMessage(clientid, colorError + Language.ServerNoSpectatorBuild());
                        break;
                    }
                    Vector3i a = new(packet.FillArea.X1, packet.FillArea.Y1, packet.FillArea.Z1);
                    Vector3i b = new(packet.FillArea.X2, packet.FillArea.Y2, packet.FillArea.Z2);

                    int blockCount = (Math.Abs(a.X - b.X) + 1) * (Math.Abs(a.Y - b.Y) + 1) * (Math.Abs(a.Z - b.Z) + 1);

                    if (blockCount > Clients[clientid].FillLimit)
                    {
                        SendMessage(clientid, colorError + Language.ServerFillAreaTooLarge());
                        break;
                    }
                    if (!this.IsFillAreaValid(Clients[clientid], a, b))
                    {
                        SendMessage(clientid, colorError + Language.ServerFillAreaInvalid());
                        break;
                    }
                    this.DoFillArea(clientid, packet.FillArea, blockCount);

                    BuildLog(string.Format("{0} {1} {2} - {3} {4} {5} {6} {7} {8}", a.X, a.Y, a.Z, b.X, b.Y, b.Z,
                        c.PlayerName, c.Socket.RemoteEndPoint().AddressToString(),
                        Map.GetBlock(a.X, a.Y, a.Z)));
                }
                break;
            case PacketType.PositionAndOrientation:
                {
                    var p = packet.PositionAndOrientation;
                    Clients[clientid].PositionMul32GlX = p.X;
                    Clients[clientid].PositionMul32GlY = p.Y;
                    Clients[clientid].PositionMul32GlZ = p.Z;
                    Clients[clientid].PositionHeading = p.Heading;
                    Clients[clientid].PositionPitch = p.Pitch;
                    Clients[clientid].Stance = (byte)p.Stance;
                }
                break;
            case PacketType.Message:
                {
                    packet.Message.Message = packet.Message.Message.Trim();
                    // empty message
                    if (string.IsNullOrEmpty(packet.Message.Message))
                    {
                        //Ignore empty messages
                        break;
                    }
                    // server command
                    if (packet.Message.Message.StartsWith("/"))
                    {
                        string[] ss = packet.Message.Message.Split([' ']);
                        string command = ss[0].Replace("/", "");
                        string argument = packet.Message.Message.IndexOf(' ') < 0 ? "" : packet.Message.Message.Substring(packet.Message.Message.IndexOf(" ") + 1);
                        try
                        {
                            //Try to execute the given command
                            this.CommandInterpreter(clientid, command, argument);
                        }
                        catch (Exception ex)
                        {
                            //This will notify client of error instead of kicking him in case of an error
                            SendMessage(clientid, "Server error while executing command!", MessageType.Error);
                            SendMessage(clientid, "Details on server console!", MessageType.Error);
                            DiagLog.Write("Client {0} caused a command error.", clientid);
                            DiagLog.Write("Command: /{0}", command);
                            DiagLog.Write("Arguments: {0}", argument);
                            DiagLog.Write(ex.Message);
                            DiagLog.Write(ex.StackTrace);
                        }
                    }
                    // client command
                    else if (packet.Message.Message.StartsWith("."))
                    {
                        //Ignore clientside commands
                        break;
                    }
                    // chat message
                    else
                    {
                        string message = packet.Message.Message;
                        for (int i = 0; i < ModEventHandlers.onplayerchat.Count; i++)
                        {
                            message = ModEventHandlers.onplayerchat[i](clientid, message, packet.Message.IsTeamchat != 0);
                        }
                        if (Clients[clientid].Privileges.Contains(ServerClientMisc.Privilege.chat))
                        {
                            if (message == null)
                            {
                                break;
                            }
                            SendMessageToAll(string.Format("{0}: {1}", Clients[clientid].ColoredPlayername(colorNormal), message));
                            ChatLog(string.Format("{0}: {1}", Clients[clientid].PlayerName, message));
                        }
                        else
                        {
                            SendMessage(clientid, string.Format(Language.ServerNoChatPrivilege(), colorError));
                        }
                    }
                }
                break;
            case PacketType.Craft:
                DoCommandCraft(true, packet.Craft);
                break;
            case PacketType.InventoryAction:
                DoCommandInventory(clientid, packet.InventoryAction);
                break;
            case PacketType.Health:
                {
                    //todo server side
                    var stats = GetPlayerStats(Clients[clientid].PlayerName);
                    stats.CurrentHealth = packet.Health.CurrentHealth;
                    if (stats.CurrentHealth < 1)
                    {
                        //death - reset health. More stuff done in Death packet handling
                        stats.CurrentHealth = stats.MaxHealth;
                    }
                    Clients[clientid].IsPlayerStatsDirty = true;
                }
                break;
            case PacketType.Death:
                {
                    //DiagLog.Write("Death Packet Received. Client: {0}, Reason: {1}, Source: {2}", clientid, packet.Death.Reason, packet.Death.SourcePlayer);
                    for (int i = 0; i < ModEventHandlers.onplayerdeath.Count; i++)
                    {
                        try
                        {
                            ModEventHandlers.onplayerdeath[i](clientid, (DeathReason)packet.Death.Reason, packet.Death.SourcePlayer);
                        }
                        catch (Exception ex)
                        {
                            DiagLog.Write("Mod exception: OnPlayerDeath");
                            DiagLog.Write(ex.Message);
                            DiagLog.Write(ex.StackTrace);
                        }
                    }
                }
                break;
            case PacketType.Oxygen:
                {
                    //todo server side
                    var stats = GetPlayerStats(Clients[clientid].PlayerName);
                    stats.CurrentOxygen = packet.Oxygen.CurrentOxygen;
                    Clients[clientid].IsPlayerStatsDirty = true;
                }
                break;
            case PacketType.MonsterHit:
                HitMonsters(clientid, packet.Health.CurrentHealth);
                break;
            case PacketType.DialogClick:
                for (int i = 0; i < ModEventHandlers.ondialogclick.Count; i++)
                {
                    ModEventHandlers.ondialogclick[i](clientid, packet.DialogClick_.WidgetId);
                }
                for (int i = 0; i < ModEventHandlers.ondialogclick2.Count; i++)
                {
                    DialogClickArgs args = new();
                    args.Player = clientid;
                    args.WidgetId = packet.DialogClick_.WidgetId;
                    args.TextBoxValue = packet.DialogClick_.TextBoxValue;
                    ModEventHandlers.ondialogclick2[i](args);
                }
                break;
            case PacketType.Shot:
                int shootSoundIndex = pistolcycle++ % BlockTypes[packet.Shot.WeaponBlock].Sounds.ShootEnd.Length;	//Cycle all given ShootEnd sounds
                PlaySoundAtExceptPlayer((int)DeserializeFloat(packet.Shot.FromX), (int)DeserializeFloat(packet.Shot.FromZ), (int)DeserializeFloat(packet.Shot.FromY), BlockTypes[packet.Shot.WeaponBlock].Sounds.ShootEnd[shootSoundIndex] + ".ogg", clientid);
                if (BlockTypes[packet.Shot.WeaponBlock].ProjectileSpeed == 0)
                {
                    SendBullet(clientid, DeserializeFloat(packet.Shot.FromX), DeserializeFloat(packet.Shot.FromY), DeserializeFloat(packet.Shot.FromZ),
                       DeserializeFloat(packet.Shot.ToX), DeserializeFloat(packet.Shot.ToY), DeserializeFloat(packet.Shot.ToZ), 150);
                }
                else
                {
                    Vector3 from = new(DeserializeFloat(packet.Shot.FromX), DeserializeFloat(packet.Shot.FromY), DeserializeFloat(packet.Shot.FromZ));
                    Vector3 to = new(DeserializeFloat(packet.Shot.ToX), DeserializeFloat(packet.Shot.ToY), DeserializeFloat(packet.Shot.ToZ));
                    Vector3 v = to - from;
                    v.Normalize();
                    v *= BlockTypes[packet.Shot.WeaponBlock].ProjectileSpeed;
                    SendProjectile(clientid, DeserializeFloat(packet.Shot.FromX), DeserializeFloat(packet.Shot.FromY), DeserializeFloat(packet.Shot.FromZ),
                        v.X, v.Y, v.Z, packet.Shot.WeaponBlock, DeserializeFloat(packet.Shot.ExplodesAfter));
                    //Handle OnWeaponShot so grenade ammo is correct
                    for (int i = 0; i < ModEventHandlers.onweaponshot.Count; i++)
                    {
                        ModEventHandlers.onweaponshot[i](clientid, packet.Shot.WeaponBlock);
                    }
                    return;
                }
                for (int i = 0; i < ModEventHandlers.onweaponshot.Count; i++)
                {
                    ModEventHandlers.onweaponshot[i](clientid, packet.Shot.WeaponBlock);
                }
                if (Clients[clientid].LastPing < 0.3)
                {
                    if (packet.Shot.HitPlayer != -1)
                    {
                        //client-side shooting
                        for (int i = 0; i < ModEventHandlers.onweaponhit.Count; i++)
                        {
                            ModEventHandlers.onweaponhit[i](clientid, packet.Shot.HitPlayer, packet.Shot.WeaponBlock, packet.Shot.IsHitHead != 0);
                        }
                    }
                    return;
                }
                foreach (var k in Clients)
                {
                    if (k.Key == clientid)
                    {
                        continue;
                    }
                    Line3D pick = new()
                    {
                        Start = new Vector3(DeserializeFloat(packet.Shot.FromX), DeserializeFloat(packet.Shot.FromY), DeserializeFloat(packet.Shot.FromZ)),
                        End = new Vector3(DeserializeFloat(packet.Shot.ToX), DeserializeFloat(packet.Shot.ToY), DeserializeFloat(packet.Shot.ToZ))
                    };

                    Vector3 feetpos = new((float)k.Value.PositionMul32GlX / 32, (float)k.Value.PositionMul32GlY / 32, (float)k.Value.PositionMul32GlZ / 32);
                    //var p = PlayerPositionSpawn;
                    float headsize = (k.Value.ModelHeight - k.Value.EyeHeight) * 2; //0.4f;
                    float h = k.Value.ModelHeight - headsize;
                    float r = 0.35f;

                    Box3 bodybox = new(
                        new Vector3(feetpos.X - r, feetpos.Y, feetpos.Z - r),
                        new Vector3(feetpos.X + r, feetpos.Y + h, feetpos.Z + r)
                    );

                    Box3 headbox = new(
                        new Vector3(feetpos.X - r, feetpos.Y + h, feetpos.Z - r),
                        new Vector3(feetpos.X + r, feetpos.Y + h + headsize, feetpos.Z + r)
                    );

                    if (Intersection.CheckLineBoxExact(pick, headbox) != null)
                    {
                        for (int i = 0; i < ModEventHandlers.onweaponhit.Count; i++)
                        {
                            ModEventHandlers.onweaponhit[i](clientid, k.Key, packet.Shot.WeaponBlock, true);
                        }
                    }
                    else if (Intersection.CheckLineBoxExact(pick, bodybox) != null)
                    {
                        for (int i = 0; i < ModEventHandlers.onweaponhit.Count; i++)
                        {
                            ModEventHandlers.onweaponhit[i](clientid, k.Key, packet.Shot.WeaponBlock, false);
                        }
                    }
                }
                break;
            case PacketType.SpecialKey:
                for (int i = 0; i < ModEventHandlers.onspecialkey.Count; i++)
                {
                    ModEventHandlers.onspecialkey[i](clientid, (SpecialKey)packet.SpecialKey_.Key_);
                }
                break;
            case PacketType.ActiveMaterialSlot:
                Clients[clientid].ActiveMaterialSlot = packet.ActiveMaterialSlot.ActiveMaterialSlot;
                for (int i = 0; i < ModEventHandlers.changedactivematerialslot.Count; i++)
                {
                    ModEventHandlers.changedactivematerialslot[i](clientid);
                }
                break;
            case PacketType.Leave:
                //0: Leave - 1: Crash
                DiagLog.Write("Disconnect reason: {0}", packet.Leave.Reason);
                KillPlayer(clientid);
                break;
            case PacketType.Reload:
                break;
            case PacketType.ServerQuery:
                //Flood/DDoS-abuse protection
                if ((DateTime.UtcNow - lastQuery) < TimeSpan.FromMilliseconds(200))
                {
                    DiagLog.Write("ServerQuery rejected (too many requests)");
                    SendPacket(clientid, ServerPackets.DisconnectPlayer("Too many requests!"));
                    KillPlayer(clientid);
                    return;
                }
                DiagLog.Write("ServerQuery processed.");
                lastQuery = DateTime.UtcNow;
                //Client only wants server information. No real client.
                List<string> playernames = [];
                lock (Clients)
                {
                    foreach (var k in Clients)
                    {
                        if (k.Value.QueryClient || k.Value.IsBot)
                        {
                            //Exclude bot players and query clients
                            continue;
                        }
                        playernames.Add(k.Value.PlayerName);
                    }
                }
                //Create query answer
                Packet_ServerQueryAnswer answer = new()
                {
                    Name = Config.Name,
                    MOTD = Config.Motd,
                    PlayerCount = playernames.Count,
                    MaxPlayers = Config.MaxClients,
                    PlayerList = string.Join(",", playernames.ToArray()),
                    Port = Config.Port,
                    GameMode = GameMode,
                    Password = Config.IsPasswordProtected(),
                    PublicHash = ReceivedKey,
                    ServerVersion = GameVersion.Version,
                    MapSizeX = Map.MapSizeX,
                    MapSizeY = Map.MapSizeY,
                    MapSizeZ = Map.MapSizeZ,
                    ServerThumbnail = GenerateServerThumbnail(),
                };
                //Send answer
                SendPacket(clientid, ServerPackets.AnswerQuery(answer));
                //Directly disconnect client after request.
                SendPacket(clientid, ServerPackets.DisconnectPlayer("Query success."));
                KillPlayer(clientid);
                break;
            case PacketType.GameResolution:
                //Update client information
                Clients[clientid].WindowSize = new int[] { packet.GameResolution.Width, packet.GameResolution.Height };
                //DiagLog.Write("client:{0} --> {1}x{2}", clientid, clients[clientid].WindowSize[0], clients[clientid].WindowSize[1]);
                break;
            case PacketType.EntityInteraction:
                switch (packet.EntityInteraction.InteractionType)
                {
                    case PacketEntityInteractionType.Use:
                        for (int i = 0; i < ModEventHandlers.onuseentity.Count; i++)
                        {
                            ServerEntityId id = c.SpawnedEntities[packet.EntityInteraction.EntityId - 64];
                            ModEventHandlers.onuseentity[i](clientid, id.ChunkX, id.ChunkY, id.ChunkZ, id.Id);
                        }
                        break;
                    case PacketEntityInteractionType.Hit:
                        for (int i = 0; i < ModEventHandlers.onhitentity.Count; i++)
                        {
                            ServerEntityId id = c.SpawnedEntities[packet.EntityInteraction.EntityId - 64];
                            ModEventHandlers.onhitentity[i](clientid, id.ChunkX, id.ChunkY, id.ChunkZ, id.Id);
                        }
                        break;
                    default:
                        DiagLog.Write("Unknown EntityInteractionType: {0}, clientid: {1}", packet.EntityInteraction.InteractionType, clientid);
                        break;
                }
                break;
            default:
                DiagLog.Write("Invalid packet: {0}, clientid:{1}", packet.Id, clientid);
                break;
        }
    }

    private void BuildLog(string p)
    {
        if (!Config.BuildLogging)
        {
            return;
        }
        if (!Directory.Exists(_serverPathLogs))
        {
            Directory.CreateDirectory(_serverPathLogs);
        }
        string filename = Path.Combine(_serverPathLogs, "BuildLog.txt");
        File.AppendAllText(filename, string.Format("{0} {1}\n", DateTime.Now, p));
    }

    public bool CheckBuildPrivileges(int player, int x, int y, int z, PacketBlockSetMode mode)
    {
        Server server = this;
        if (!server.PlayerHasPrivilege(player, ServerClientMisc.Privilege.build))
        {
            server.SendMessage(player, server.colorError + server.Language.ServerNoBuildPrivilege());
            return false;
        }
        if (server.Clients[player].IsSpectator && !server.Config.AllowSpectatorBuild)
        {
            server.SendMessage(player, server.colorError + server.Language.ServerNoSpectatorBuild());
            return false;
        }
        for (int i = 0; i < server.ModEventHandlers.onpermission.Count; i++)
        {
            PermissionArgs args = new()
            {
                Player = player,
                X = x,
                Y = y,
                Z = z
            };
            server.ModEventHandlers.onpermission[i](args);
            if (args.Allowed)
            {
                return true;
            }
        }
        if (!server.Config.CanUserBuild(server.Clients[player], x, y, z)
            && !server.ExtraPrivileges.ContainsKey(ServerClientMisc.Privilege.build))
        {
            server.SendMessage(player, server.colorError + server.Language.ServerNoBuildPermissionHere());
            return false;
        }
        bool retval = true;
        if (mode == PacketBlockSetMode.Create)
        {
            for (int i = 0; i < ModEventHandlers.checkonbuild.Count; i++)
            {
                // All handlers must return true for operation to be permitted.
                retval = retval && ModEventHandlers.checkonbuild[i](player, x, y, z);
            }
        }
        else if (mode == PacketBlockSetMode.Destroy)
        {
            for (int i = 0; i < ModEventHandlers.checkondelete.Count; i++)
            {
                // All handlers must return true for operation to be permitted.
                retval = retval && ModEventHandlers.checkondelete[i](player, x, y, z);
            }
        }
        return retval;
    }

    private bool CheckUsePrivileges(int player, int x, int y, int z)
    {
        Server server = this;
        if (!server.PlayerHasPrivilege(player, ServerClientMisc.Privilege.use))
        {
            SendMessage(player, colorError + server.Language.ServerNoUsePrivilege());
            return false;
        }
        if (server.Clients[player].IsSpectator && !server.Config.AllowSpectatorUse)
        {
            SendMessage(player, colorError + server.Language.ServerNoSpectatorUse());
            return false;
        }
        bool retval = true;
        for (int i = 0; i < ModEventHandlers.checkonuse.Count; i++)
        {
            // All handlers must return true for operation to be permitted.
            retval = retval && ModEventHandlers.checkonuse[i](player, x, y, z);
        }
        return retval;
    }

    public void SendServerRedirect(int clientid, string ip_, int port_)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.ServerRedirect,
            Redirect = new Packet_ServerRedirect()
            {
                IP = ip_,
                Port = port_,
            }
        };
        SendPacket(clientid, p);
    }

    private static byte[] GenerateServerThumbnail()
    {
        string filename = Path.Combine(Path.Combine("data", "public"), "thumbnail.png");
        Bitmap bmp;
        if (File.Exists(filename))
        {
            try
            {
                bmp = new Bitmap(filename);
            }
            catch
            {
                //Create empty bitmap in case of failure
                bmp = new Bitmap(64, 64);
            }
        }
        else
        {
            bmp = new Bitmap(64, 64);
        }
        Bitmap bmp2 = bmp;
        if (bmp.Width != 64 || bmp.Height != 64)
        {
            //Resize the image if it does not have the proper size
            bmp2 = new Bitmap(bmp, 64, 64);
        }
        using MemoryStream ms = new();
        //Convert image to a byte[] for transfer
        bmp2.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
        return ms.ToArray();
    }

    private static float DeserializeFloat(int p)
    {
        return (float)p / 32;
    }

    private void SendProjectile(int player, float fromx, float fromy, float fromz, float velocityx, float velocityy, float velocityz, int block, float explodesafter)
    {
        foreach (var k in Clients)
        {
            if (k.Key == player)
            {
                continue;
            }
            Packet_Server p = new()
            {
                Id = Packet_ServerIdEnum.Projectile,
                Projectile = new Packet_ServerProjectile()
                {
                    FromXFloat = SerializeFloat(fromx),
                    FromYFloat = SerializeFloat(fromy),
                    FromZFloat = SerializeFloat(fromz),
                    VelocityXFloat = SerializeFloat(velocityx),
                    VelocityYFloat = SerializeFloat(velocityy),
                    VelocityZFloat = SerializeFloat(velocityz),
                    BlockId = block,
                    ExplodesAfterFloat = SerializeFloat(explodesafter),
                    SourcePlayerID = player,
                }
            };
            SendPacket(k.Key, Serialize(p));
        }
    }

    private void SendBullet(int player, float fromx, float fromy, float fromz, float tox, float toy, float toz, float speed)
    {
        foreach (var k in Clients)
        {
            if (k.Key == player)
            {
                continue;
            }
            Packet_Server p = new()
            {
                Id = Packet_ServerIdEnum.Bullet,
                Bullet = new Packet_ServerBullet()
                {
                    FromXFloat = SerializeFloat(fromx),
                    FromYFloat = SerializeFloat(fromy),
                    FromZFloat = SerializeFloat(fromz),
                    ToXFloat = SerializeFloat(tox),
                    ToYFloat = SerializeFloat(toy),
                    ToZFloat = SerializeFloat(toz),
                    SpeedFloat = SerializeFloat(speed)
                }
            };
            SendPacket(k.Key, Serialize(p));
        }
    }

    public Vector3i GetPlayerSpawnPositionMul32(int clientid)
    {
        Vector3i position;
        Spawn playerSpawn = null;
        // Check if there is a spawn entry for his assign group
        if (Clients[clientid].ClientGroup.Spawn != null)
        {
            playerSpawn = Clients[clientid].ClientGroup.Spawn;
        }
        // Check if there is an entry in clients with spawn member (overrides group spawn).
        foreach (Client client in ServerClient.Clients)
        {
            if (client.Name.Equals(Clients[clientid].PlayerName, StringComparison.InvariantCultureIgnoreCase))
            {
                if (client.Spawn != null)
                {
                    playerSpawn = client.Spawn;
                }
                break;
            }
        }

        if (playerSpawn == null)
        {
            position = new Vector3i(this.DefaultPlayerSpawn.X * 32, this.DefaultPlayerSpawn.Z * 32, this.DefaultPlayerSpawn.Y * 32);
        }
        else
        {
            position = this.SpawnToVector3i(playerSpawn);
        }
        return position;
    }

    private void RunInClientSandbox(string script, int clientid)
    {
        var client = GetClient(clientid);
        if (!Config.AllowScripting)
        {
            SendMessage(clientid, "Server scripts disabled.", MessageType.Error);
            return;
        }
        if (!client.Privileges.Contains(ServerClientMisc.Privilege.run))
        {
            SendMessage(clientid, "Insufficient privileges to access this command.", MessageType.Error);
            return;
        }
        DiagLog.Write(string.Format("{0} runs script:\n{1}", client.PlayerName, script));
        if (client.Interpreter == null)
        {
            client.Interpreter = new JavaScriptInterpreter();
            client.Console = new ScriptConsole(this, clientid);
            client.Console.InjectConsoleCommands(client.Interpreter);
            client.Interpreter.SetVariables(new Dictionary<string, object>() { { "client", client }, { "server", this }, });
            client.Interpreter.Execute("function inspect(obj) { for( property in obj) { out(property)}}");
        }
        var interpreter = client.Interpreter;
        object result;
        SendMessage(clientid, colorNormal + script);
        if (interpreter.Execute(script, out result))
        {
            try
            {
                SendMessage(clientid, $"{colorSuccess} => {result}");
            }
            catch (FormatException e) // can happen
            {
                SendMessage(clientid, $"{colorError}Error. {e.Message}");
            }
            return;
        }
        SendMessage(clientid, $"{colorError}Error.");
    }

    public string colorNormal = "&f"; //white
    public string colorHelp = "&4"; //red
    public string colorOpUsername = "&2"; //green
    public string colorSuccess = "&2"; //green
    public string colorError = "&4"; //red
    public string colorImportant = "&4"; // red
    public string colorAdmin = "&e"; //yellow
    public enum MessageType { Normal, Important, Help, OpUsername, Success, Error, Admin, White, Red, Green, Yellow }
    private string MessageTypeToString(MessageType type)
    {
        return type switch
        {
            MessageType.Normal or MessageType.White => colorNormal,
            MessageType.Important => colorImportant,
            MessageType.Help or MessageType.Red => colorHelp,
            MessageType.OpUsername or MessageType.Green => colorOpUsername,
            MessageType.Error => colorError,
            MessageType.Success => colorSuccess,
            MessageType.Admin or MessageType.Yellow => colorAdmin,
            _ => colorNormal,
        };
    }

    private void NotifyBlock(int x, int y, int z, int blocktype)
    {
        foreach (var k in Clients)
        {
            SendSetBlock(k.Key, x, y, z, blocktype);
        }
    }

    private bool DoCommandCraft(bool execute, Packet_ClientCraft cmd)
    {
        if (Map.GetBlock(cmd.X, cmd.Y, cmd.Z) != BlockTypeRegistry.BlockIdCraftingTable)
        {
            return false;
        }
        if (cmd.RecipeId < 0 || cmd.RecipeId >= CraftingRecipes.Count)
        {
            return false;
        }
        Vector3i[] table = CraftingTableTool.GetTable(cmd.X, cmd.Y, cmd.Z, out int tableCount);
        int[] ontable = CraftingTableTool.GetOnTable(table, tableCount, out int ontableCount);
        List<int> outputtoadd = [];
        int i = cmd.RecipeId;
        {
            //try apply recipe. if success then try until fail.
            for (; ; )
            {
                //check if ingredients available
                foreach (Ingredient ingredient in CraftingRecipes[i].Ingredients)
                {
                    if (ontable.AsSpan(0, tableCount).Count(ingredient.Type) < ingredient.Amount)
                    {
                        goto nextrecipe;
                    }
                }
                //remove ingredients
                foreach (Ingredient ingredient in CraftingRecipes[i].Ingredients)
                {
                    for (int ii = 0; ii < ingredient.Amount; ii++)
                    {
                        //replace on table
                        ReplaceOne(ontable, ontableCount, ingredient.Type, BlockTypeRegistry.BlockIdEmpty);
                    }
                }
                //add output
                for (int z = 0; z < CraftingRecipes[i].Output.Amount; z++)
                {
                    outputtoadd.Add(CraftingRecipes[i].Output.Type);
                }
            }
        nextrecipe:
            ;
        }
        foreach (var v in outputtoadd)
        {
            ReplaceOne(ontable, ontableCount, BlockTypeRegistry.BlockIdEmpty, v);
        }
        int zz = 0;
        if (execute)
        {
            for (int k = 0; k < tableCount; k++)
            {
                Vector3i v = table[k];
                SetBlockAndNotify(v.X, v.Y, v.Z + 1, ontable[zz]);
                zz++;
            }
        }
        return true;
    }

    private static void ReplaceOne<T>(T[] l, int lCount, T from, T to) where T : IEquatable<T>
    {
        Span<T> span = l.AsSpan(0, lCount);
        int index = span.IndexOf(from);
        if (index >= 0) span[index] = to;
    }

    private IGameDataItems _dataItems;
    public InventoryUtil GetInventoryUtil(Inventory inventory)
    {
        return new()
        {
            d_Inventory = inventory,
            d_Items = _dataItems
        };
    }

    private void DoCommandInventory(int player_id, Packet_ClientInventoryAction cmd)
    {
        Inventory inventory = GetPlayerInventory(Clients[player_id].PlayerName);
        var s = new InventoryServer
        {
            d_Inventory = inventory,
            d_InventoryUtil = GetInventoryUtil(inventory),
            d_Items = _dataItems,
            d_DropItem = this
        };

        switch (cmd.Action)
        {
            case PacketInventoryActionType.Click:
                s.InventoryClick(cmd.A);
                break;
            case PacketInventoryActionType.MoveToInventory:
                s.MoveToInventory(cmd.A);
                break;
            case PacketInventoryActionType.WearItem:
                s.WearItem(cmd.A, cmd.B);
                break;
            default:
                break;
        }
        Clients[player_id].IsInventoryDirty = true;
        NotifyInventory(player_id);
    }

    private bool IsFillAreaValid(ClientOnServer client, Vector3i a, Vector3i b)
    {
        if (!VectorUtils.IsValidPos(this.Map, a.X, a.Y, a.Z) || !VectorUtils.IsValidPos(this.Map, b.X, b.Y, b.Z))
        {
            return false;
        }

        int minX = Math.Min(a.X, b.X), maxX = Math.Max(a.X, b.X);
        int minY = Math.Min(a.Y, b.Y), maxY = Math.Max(a.Y, b.Y);
        int minZ = Math.Min(a.Z, b.Z), maxZ = Math.Max(a.Z, b.Z);

        return Config.Areas.Any(area =>
            area.CanUserBuild(client) &&
            area.ContainsBox(minX, minY, minZ, maxX, maxY, maxZ));
    }

    private bool DoFillArea(int player_id, Packet_ClientFillArea fill, int blockCount)
    {
        Vector3i a = new(fill.X1, fill.Y1, fill.Z1);
        Vector3i b = new(fill.X2, fill.Y2, fill.Z2);

        int startx = Math.Min(a.X, b.X);
        int endx = Math.Max(a.X, b.X);
        int starty = Math.Min(a.Y, b.Y);
        int endy = Math.Max(a.Y, b.Y);
        int startz = Math.Min(a.Z, b.Z);
        int endz = Math.Max(a.Z, b.Z);

        int blockType = fill.BlockType;
        blockType = BlockTypeRegistry.WhenPlayerPlacesGetsConvertedTo[blockType];

        Inventory inventory = GetPlayerInventory(Clients[player_id].PlayerName);
        var item = inventory.RightHand[fill.MaterialSlot];
        if (item == null)
        {
            return false;
        }
        //This prevents the player's inventory from getting sent to them while using fill (causes excessive bandwith usage)
        Clients[player_id].UsingFill = true;
        for (int x = startx; x <= endx; ++x)
        {
            for (int y = starty; y <= endy; ++y)
            {
                for (int z = startz; z <= endz; ++z)
                {
                    Packet_ClientSetBlock cmd = new()
                    {
                        X = x,
                        Y = y,
                        Z = z,
                        MaterialSlot = fill.MaterialSlot
                    };
                    if (GetBlock(x, y, z) != 0)
                    {
                        cmd.Mode = PacketBlockSetMode.Destroy;
                        DoCommandBuild(player_id, true, cmd);
                    }
                    if (blockType != BlockTypeRegistry.BlockIdFillArea)
                    {
                        cmd.Mode = PacketBlockSetMode.Create;
                        DoCommandBuild(player_id, true, cmd);
                    }
                }
            }
        }
        Clients[player_id].UsingFill = false;
        return true;
    }

    /// <summary>
    /// Determines if a given client can see the specified chunk<br/>
    /// <b>Attention!</b> Chunk coordinates are NOT world coordinates!<br/>
    /// chunk position = (world position / chunk size)
    /// </summary>
    /// <param name="clientid">Client ID</param>
    /// <param name="vx">Chunk x coordinate</param>
    /// <param name="vy">Chunk y coordinate</param>
    /// <param name="vz">Chunk z coordinate</param>
    /// <returns>true if client can see the chunk, false otherwise</returns>
    public bool ClientSeenChunk(int clientid, int vx, int vy, int vz)
    {
        int pos = VectorIndexUtil.Index3d(vx, vy, vz, Map.MapSizeX / ChunkSize, Map.MapSizeY / ChunkSize);
        return Clients[clientid].chunksseen[pos];
    }

    /// <summary>
    /// Sets a given chunk as seen by the client<br/>
    /// <b>Attention!</b> Chunk coordinates are NOT world coordinates!<br/>
    /// chunk position = (world position / chunk size)
    /// </summary>
    /// <param name="clientid">Client ID</param>
    /// <param name="vx">Chunk x coordinate</param>
    /// <param name="vy">Chunk y coordinate</param>
    /// <param name="vz">Chunk z coordinate</param>
    /// <param name="time"></param>
    public void ClientSeenChunkSet(int clientid, int vx, int vy, int vz, int time)
    {
        int pos = VectorIndexUtil.Index3d(vx, vy, vz, Map.MapSizeX / ChunkSize, Map.MapSizeY / ChunkSize);
        Clients[clientid].chunksseen[pos] = true;
        Clients[clientid].chunksseenTime[pos] = time;
        //DiagLog.Write("SeenChunk:   {0},{1},{2} Client: {3}", vx, vy, vz, clientid);
    }

    /// <summary>
    /// Sets a given chunk as unseen by the client<br/>
    /// <b>Attention!</b> Chunk coordinates are NOT world coordinates!<br/>
    /// chunk position = (world position / chunk size)
    /// </summary>
    /// <param name="clientid">Client ID</param>
    /// <param name="vx">Chunk x coordinate</param>
    /// <param name="vy">Chunk y coordinate</param>
    /// <param name="vz">Chunk z coordinate</param>
    public void ClientSeenChunkRemove(int clientid, int vx, int vy, int vz)
    {
        int pos = VectorIndexUtil.Index3d(vx, vy, vz, Map.MapSizeX / ChunkSize, Map.MapSizeY / ChunkSize);
        Clients[clientid].chunksseen[pos] = false;
        Clients[clientid].chunksseenTime[pos] = 0;
        //DiagLog.Write("UnseenChunk: {0},{1},{2} Client: {3}", vx, vy, vz, clientid);
    }

    private void SetFillAreaLimit(int clientid)
    {
        ClientOnServer client = GetClient(clientid);
        if (client == null)
        {
            return;
        }

        int maxFill = 500;
        if (ServerClient.DefaultFillLimit != null)
        {
            maxFill = ServerClient.DefaultFillLimit.Value;
        }

        // Check if there is a fill-limit entry for his assigned group.
        if (client.ClientGroup.FillLimit != null)
        {
            maxFill = client.ClientGroup.FillLimit.Value;
        }

        // Check if there is an entry in clients with fill-limit member (overrides group fill-limit).
        foreach (Client clientConfig in ServerClient.Clients)
        {
            if (clientConfig.Name.Equals(client.PlayerName, StringComparison.InvariantCultureIgnoreCase))
            {
                if (clientConfig.FillLimit != null)
                {
                    maxFill = clientConfig.FillLimit.Value;
                }
                break;
            }
        }
        client.FillLimit = maxFill;
        SendFillAreaLimit(clientid, maxFill);
    }

    private void SendFillAreaLimit(int clientid, int limit)
    {
        Packet_ServerFillAreaLimit p = new()
        {
            Limit = limit
        };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.FillAreaLimit, FillAreaLimit = p }));
    }

    private bool DoCommandBuild(int player_id, bool execute, Packet_ClientSetBlock cmd)
    {
        Vector3 v = new(cmd.X, cmd.Y, cmd.Z);
        Inventory inventory = GetPlayerInventory(Clients[player_id].PlayerName);
        if (cmd.Mode == PacketBlockSetMode.Use)
        {
            for (int i = 0; i < ModEventHandlers.onuse.Count; i++)
            {
                ModEventHandlers.onuse[i](player_id, cmd.X, cmd.Y, cmd.Z);
            }
            return true;
        }
        if (cmd.Mode == PacketBlockSetMode.UseWithTool)
        {
            for (int i = 0; i < ModEventHandlers.onusewithtool.Count; i++)
            {
                ModEventHandlers.onusewithtool[i](player_id, cmd.X, cmd.Y, cmd.Z, cmd.BlockType);
            }
            return true;
        }
        if (cmd.Mode == PacketBlockSetMode.Create
            && BlockTypeRegistry.Rail[cmd.BlockType] != 0)
        {
            return DoCommandBuildRail(player_id, cmd);
        }
        if (cmd.Mode == PacketBlockSetMode.Destroy
            && BlockTypeRegistry.Rail[Map.GetBlock(cmd.X, cmd.Y, cmd.Z)] != 0)
        {
            return DoCommandRemoveRail(player_id, execute, cmd);
        }
        if (cmd.Mode == PacketBlockSetMode.Create)
        {
            int oldblock = Map.GetBlock(cmd.X, cmd.Y, cmd.Z);
            if (!(oldblock == 0 || BlockTypes[oldblock].IsFluid()))
            {
                return false;
            }
            var item = inventory.RightHand[cmd.MaterialSlot];
            if (item == null)
            {
                return false;
            }
            switch (item.InventoryItemType)
            {
                case InventoryItemType.Block:
                    item.BlockCount--;
                    if (item.BlockCount == 0)
                    {
                        inventory.RightHand[cmd.MaterialSlot] = null;
                    }
                    if (BlockTypeRegistry.Rail[item.BlockId] != 0)
                    {
                    }
                    SetBlockAndNotify(cmd.X, cmd.Y, cmd.Z, item.BlockId);
                    for (int i = 0; i < ModEventHandlers.onbuild.Count; i++)
                    {
                        ModEventHandlers.onbuild[i](player_id, cmd.X, cmd.Y, cmd.Z);
                    }
                    break;
                default:
                    //TODO
                    return false;
            }
        }
        else
        {
            var item = new InventoryItem
            {
                InventoryItemType = InventoryItemType.Block
            };
            int blockid = Map.GetBlock(cmd.X, cmd.Y, cmd.Z);
            item.BlockId = BlockTypeRegistry.WhenPlayerPlacesGetsConvertedTo[blockid];
            if (!Config.IsCreative)
            {
                GetInventoryUtil(inventory).GrabItem(item, cmd.MaterialSlot);
            }
            SetBlockAndNotify(cmd.X, cmd.Y, cmd.Z, SpecialBlockId.Empty);
            for (int i = 0; i < ModEventHandlers.ondelete.Count; i++)
            {
                ModEventHandlers.ondelete[i](player_id, cmd.X, cmd.Y, cmd.Z, blockid);
            }
        }
        Clients[player_id].IsInventoryDirty = true;
        NotifyInventory(player_id);
        return true;
    }

    private bool DoCommandBuildRail(int player_id, Packet_ClientSetBlock cmd)
    {
        Inventory inventory = GetPlayerInventory(Clients[player_id].PlayerName);
        int oldblock = Map.GetBlock(cmd.X, cmd.Y, cmd.Z);
        if (!(oldblock == SpecialBlockId.Empty || BlockTypeRegistry.IsRailTile(oldblock)))
        {
            return false;
        }

        //count how many rails will be created
        int oldrailcount = 0;
        if (BlockTypeRegistry.IsRailTile(oldblock))
        {
            oldrailcount = DirectionUtils.RailDirectionFlagsCount(
                oldblock - BlockTypeRegistry.BlockIdRailStart);
        }
        int newrailcount = DirectionUtils.RailDirectionFlagsCount(
            cmd.BlockType - BlockTypeRegistry.BlockIdRailStart);
        int blockstoput = newrailcount - oldrailcount;

        InventoryItem item = inventory.RightHand[cmd.MaterialSlot];
        if (!(item.InventoryItemType == InventoryItemType.Block && BlockTypeRegistry.Rail[item.BlockId] != 0))
        {
            return false;
        }
        item.BlockCount -= blockstoput;
        if (item.BlockCount == 0)
        {
            inventory.RightHand[cmd.MaterialSlot] = null;
        }
        SetBlockAndNotify(cmd.X, cmd.Y, cmd.Z, cmd.BlockType);
        for (int i = 0; i < ModEventHandlers.onbuild.Count; i++)
        {
            ModEventHandlers.onbuild[i](player_id, cmd.X, cmd.Y, cmd.Z);
        }

        Clients[player_id].IsInventoryDirty = true;
        NotifyInventory(player_id);
        return true;
    }

    private bool DoCommandRemoveRail(int player_id, bool execute, Packet_ClientSetBlock cmd)
    {
        Inventory inventory = GetPlayerInventory(Clients[player_id].PlayerName);
        //add to inventory
        int blockid = Map.GetBlock(cmd.X, cmd.Y, cmd.Z);
        int blocktype = BlockTypeRegistry.WhenPlayerPlacesGetsConvertedTo[blockid];
        if ((!IsValid(blocktype))
            || blocktype == SpecialBlockId.Empty)
        {
            return false;
        }
        int blockstopick = 1;
        if (BlockTypeRegistry.IsRailTile(blocktype))
        {
            blockstopick = DirectionUtils.RailDirectionFlagsCount(
                blocktype - BlockTypeRegistry.BlockIdRailStart);
        }

        var item = new InventoryItem
        {
            InventoryItemType = InventoryItemType.Block,
            BlockId = BlockTypeRegistry.WhenPlayerPlacesGetsConvertedTo[blocktype],
            BlockCount = blockstopick
        };
        if (!Config.IsCreative)
        {
            GetInventoryUtil(inventory).GrabItem(item, cmd.MaterialSlot);
        }
        SetBlockAndNotify(cmd.X, cmd.Y, cmd.Z, SpecialBlockId.Empty);
        for (int i = 0; i < ModEventHandlers.ondelete.Count; i++)
        {
            ModEventHandlers.ondelete[i](player_id, cmd.X, cmd.Y, cmd.Z, blockid);
        }

        Clients[player_id].IsInventoryDirty = true;
        NotifyInventory(player_id);
        return true;
    }

    private bool IsValid(int blocktype)
    {
        return BlockTypes[blocktype].Name != null;
    }

    public void SetBlockAndNotify(int x, int y, int z, int blocktype)
    {
        Map.SetBlockNotMakingDirty(x, y, z, blocktype);
        NotifyBlock(x, y, z, blocktype);
    }

    public static byte[] Serialize(Packet_Server p)
    {
        return MemoryPackSerializer.Serialize(p);
    }

    private string GenerateUsername(string name)
    {
        int appendNumber = 1;
        while (Clients.Values.Any(c => c.PlayerName.Equals($"{name}{appendNumber}", StringComparison.OrdinalIgnoreCase)))
            appendNumber++;
        return $"{name}{appendNumber}";
    }

    public void ServerMessageToAll(string message, MessageType color)
    {
        this.SendMessageToAll(MessageTypeToString(color) + message);
        DiagLog.Write(string.Format("SERVER MESSAGE: {0}.", message));
    }

    public void SendMessageToAll(string message)
    {
        DiagLog.Write("Message to all: " + message);
        foreach (var k in Clients)
        {
            SendMessage(k.Key, message);
        }
    }

    private void SendSetBlock(int clientid, int x, int y, int z, int blocktype)
    {
        if (!ClientSeenChunk(clientid, x / ChunkSize, y / ChunkSize, z / ChunkSize))
        {
            // don't send block updates for chunks a player can not see
            return;
        }
        Packet_ServerSetBlock p = new() { X = x, Y = y, Z = z, BlockType = blocktype };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.SetBlock, SetBlock = p }));
    }

    public void SendSound(int clientid, string name, int x, int y, int z)
    {
        Packet_ServerSound p = new() { Name = name, X = x, Y = y, Z = z };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Sound, Sound = p }));
    }

    private void SendPlayerSpawnPosition(int clientid, int x, int y, int z)
    {
        Packet_ServerPlayerSpawnPosition p = new()
        {
            X = x,
            Y = y,
            Z = z
        };
        SendPacket(clientid, Serialize(new Packet_Server()
        {
            Id = Packet_ServerIdEnum.PlayerSpawnPosition,
            PlayerSpawnPosition = p,
        }));
    }

    public void SendMessage(int clientid, string message, MessageType color)
    {
        SendMessage(clientid, MessageTypeToString(color) + message);
    }

    public void SendMessage(int clientid, string message)
    {
        if (clientid == ServerConsoleId)
        {
            ServerConsole.Receive(message);
            return;
        }

        SendPacket(clientid, ServerPackets.Message(message));
    }

    private int StatTotalPackets = 0;
    private int StatTotalPacketsLength = 0;
    public long TotalSentBytes { get; set; }
    public long TotalReceivedBytes { get; set; }

    public void SendPacket(int clientid, Packet_Server packet)
    {
        SendPacket(clientid, Serialize(packet));
    }

    public void SendPacket(int clientid, byte[] packet)
    {
        if (Clients[clientid].IsBot)
        {
            return;
        }
        StatTotalPackets++;
        StatTotalPacketsLength += packet.Length;
        TotalSentBytes += packet.Length;
        Clients[clientid].Socket.SendMessage(packet.AsMemory(), MyNetDeliveryMethod.ReliableOrdered);
    }

    public int DrawDistance { get; set; } = 128;
    public static int ChunkSize { get; set;} = 32;

    public static double InvertedChunkSize { get; set; } = 1.0 / 32;

    public static int InvertChunk(int num)
    {
        return (int)(num * InvertedChunkSize);
    }

    public int ChunkDrawDistance { get { return DrawDistance / ChunkSize; } }

    public byte[] CompressChunkNetwork(ushort[] chunk)
    {
        return NetworkCompression.Compress(MemoryMarshal.AsBytes(chunk.AsSpan()));
    }

    private string[] GetRequiredBlobMd5()
        => [.. assets.Select(a => a.md5)];

    private string[] GetRequiredBlobName()
        => [.. assets.Select(a => a.name)];

    private AssetLoader assetLoader;
    private List<Asset> assets = [];

    private readonly int blobPartLength = 1024;

    private void SendBlobs(int clientid, string[] requestedMd5)
    {
        SendPacket(clientid, ServerPackets.LevelInitialize());
        LoadAssets();

        List<Asset> tosend = [];
        for (int i = 0; i < assets.Count; i++)
        {
            Asset f = assets[i];
            for (int k = 0; k < requestedMd5.Length; k++)
            {
                if (f.md5 == requestedMd5[k])
                {
                    tosend.Add(f);
                }
            }
        }

        for (int i = 0; i < tosend.Count; i++)
        {
            Asset f = tosend[i];
            SendBlobInitialize(clientid, f.md5, f.name);
            byte[] blob = f.data;
            int totalsent = 0;
            foreach (byte[] part in Parts(blob, blobPartLength))
            {
                SendLevelProgress(clientid,
                    (int)(((float)i / tosend.Count
                        + (float)totalsent / blob.Length / tosend.Count) * 100),
                    Language.ServerProgressDownloadingData());
                SendBlobPart(clientid, part);
                totalsent += part.Length;
            }
            SendBlobFinalize(clientid);
        }
        SendLevelProgress(clientid, 0, Language.ServerProgressGenerating());
    }

    private void LoadAssets()
    {
        assets = assetLoader.LoadAssetsAsync(out float progress);
        while (progress < 1)
        {
            Thread.Sleep(1);
        }
    }

    public static IEnumerable<byte[]> Parts(byte[] blob, int partsize)
    {
        for (int i = 0; i < blob.Length; i += partsize)
            yield return blob[i..Math.Min(i + partsize, blob.Length)];
    }

    private void SendBlobInitialize(int clientid, string hash, string name)
    {
        Packet_ServerBlobInitialize p = new() { Name = name, Md5 = hash };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.BlobInitialize, BlobInitialize = p }));
    }

    private void SendBlobPart(int clientid, byte[] data)
    {
        Packet_ServerBlobPart p = new() { Data = data };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.BlobPart, BlobPart = p }));
    }

    private void SendBlobFinalize(int clientid)
    {
        Packet_ServerBlobFinalize p = new() { };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.BlobFinalize, BlobFinalize = p }));
    }

    public Dictionary<int, BlockType> BlockTypes { get; set; } = [];

    public void SendBlockTypes(int clientid)
    {
        foreach (var (id, blockType) in BlockTypes)
        {
            Packet_ServerBlockType p1 = new() { Id = id, Blocktype = blockType };
            SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.BlockType, BlockType = p1 }));
        }
        Packet_ServerBlockTypes p = new() { };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.BlockTypes, BlockTypes = p }));
    }

    private void SendTranslations(int clientid)
    {
        //Read all lines from server translation and send them to the client
        foreach (var ((lang, id), translated) in Language.AllStrings())
        {
            Packet_ServerTranslatedString p = new()
            {
                Lang = lang,
                Id = id,
                Translation = translated
            };
            SendPacket(clientid, Serialize(new Packet_Server { Id = Packet_ServerIdEnum.Translation, Translation = p }));
        }
    }

    public static int SerializeFloat(float p)
    {
        return (int)(p * 32);
    }

    private void SendSunLevels(int clientid)
    {
        Packet_ServerSunLevels p = new();
        p.Sunlevels = sunlevels;
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.SunLevels, SunLevels = p }));
    }

    private void SendLightLevels(int clientid)
    {
        Packet_ServerLightLevels p = new();
        int[] l = new int[lightlevels.Length];
        for (int i = 0; i < lightlevels.Length; i++)
        {
            l[i] = SerializeFloat(lightlevels[i]);
        }
        p.Lightlevels = l;
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.LightLevels, LightLevels = p }));
    }

    private void SendCraftingRecipes(int clientid)
    {
        Packet_ServerCraftingRecipes p = new()
        {
            CraftingRecipes = [.. CraftingRecipes]
        };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.CraftingRecipes, CraftingRecipes = p }));
    }

    private void SendLevelProgress(int clientid, int percentcomplete, string status)
    {
        Packet_ServerLevelProgress p = new() { PercentComplete = percentcomplete, Status = status };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.LevelDataChunk, LevelDataChunk = p }));
    }

    public RenderHint RenderHint { get; set; } = RenderHint.Fast;

    private void SendServerIdentification(int clientid)
    {
        Packet_ServerIdentification p = new()
        {
            MdProtocolVersion = GameVersion.Version,
            AssignedClientId = clientid,
            ServerName = Config.Name,
            ServerMotd = Config.Motd,
            MapSizeX = Map.MapSizeX,
            MapSizeY = Map.MapSizeY,
            MapSizeZ = Map.MapSizeZ,
            DisableShadows = EnableShadows ? 0 : 1,
            PlayerAreaSize = playerareasize,
            RenderHint_ = (int)RenderHint,
            RequiredBlobMd5 = GetRequiredBlobMd5(),
            RequiredBlobName = GetRequiredBlobName(),
        };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.ServerIdentification, Identification = p }));
    }

    public void SendFreemoveState(int clientid, bool isEnabled)
    {
        Packet_ServerFreemove p = new()
        {
            IsEnabled = isEnabled ? 1 : 0
        };
        SendPacket(clientid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Freemove, Freemove = p }));
    }

    private static string ComputeMd5(string input)
    {
        byte[] hash = MD5.HashData(Encoding.ASCII.GetBytes(input));
        return Convert.ToHexString(hash).ToLower();
    }

    public float SIMULATION_STEP_LENGTH { get; set; } = 1f / 64f;
    public Dictionary<int, ClientOnServer> Clients { get; set; } = [];
    public Dictionary<string, bool> Disabledprivileges { get; set; } = [];
    public Dictionary<string, bool> ExtraPrivileges { get; set; } = [];

    public ClientOnServer GetClient(int id)
    {
        if (id == ServerConsoleId)
        {
            return this.ServerConsoleClient;
        }
        return !Clients.TryGetValue(id, out ClientOnServer? value) ? null : value;
    }

    public ClientOnServer GetClient(string name)
    {
        if (ServerConsoleClient.PlayerName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
        {
            return ServerConsoleClient;
        }
        foreach (var k in Clients)
        {
            if (k.Value.PlayerName.Equals(name, StringComparison.InvariantCultureIgnoreCase))
            {
                return k.Value;
            }
        }
        return null;
    }

    public bool ServerClientNeedsSaving { get; set; }
    public ServerClient ServerClient { get; set; }
    public ManicDigger.Group? DefaultGroupGuest { get; set; }
    public ManicDigger.Group DefaultGroupRegistered { get; set; }
    public Vector3i DefaultPlayerSpawn { get; set; }

    private Vector3i SpawnToVector3i(Spawn spawn)
    {
        int x = spawn.x;
        int y = spawn.y;
        int z;
        if (!VectorUtils.IsValidPos(Map, x, y))
        {
            throw new Exception(Language.ServerInvalidSpawnCoordinates());
        }

        if (spawn.z == null)
        {
            z = VectorUtils.BlockHeight(Map, 0, x, y);
        }
        else
        {
            z = spawn.z.Value;
            if (!VectorUtils.IsValidPos(Map, x, y, z))
            {
                throw new Exception(Language.ServerInvalidSpawnCoordinates());
            }
        }
        return new Vector3i(x * 32, z * 32, y * 32);
    }

    private const int dumpmax = 30;
    public void DropItem(ref InventoryItem item, Vector3i pos)
    {
        switch (item.InventoryItemType)
        {
            case InventoryItemType.Block:
                for (int i = 0; i < dumpmax; i++)
                {
                    if (item.BlockCount == 0) { break; }
                    //find empty position that is nearest to dump place AND has a block under.
                    Vector3i? nearpos = FindDumpPlace(pos);
                    if (nearpos == null)
                    {
                        break;
                    }
                    SetBlockAndNotify(nearpos.Value.X, nearpos.Value.Y, nearpos.Value.Z, item.BlockId);
                    item.BlockCount--;
                }
                if (item.BlockCount == 0)
                {
                    item = null;
                }
                break;
            default:
                //todo
                break;
        }
    }

    private Vector3i? FindDumpPlace(Vector3i pos)
    {
        List<Vector3i> l = [];
        for (int x = 0; x < 10; x++)
        {
            for (int y = 0; y < 10; y++)
            {
                for (int z = 0; z < 10; z++)
                {
                    int xx = pos.X + x - 10 / 2;
                    int yy = pos.Y + y - 10 / 2;
                    int zz = pos.Z + z - 10 / 2;
                    if (!VectorUtils.IsValidPos(Map, xx, yy, zz))
                    {
                        continue;
                    }
                    if (Map.GetBlock(xx, yy, zz) == SpecialBlockId.Empty
                        && Map.GetBlock(xx, yy, zz - 1) != SpecialBlockId.Empty)
                    {
                        bool playernear = false;
                        foreach (var player in Clients)
                        {
                            if (VectorUtils.DistanceSquared(PlayerBlockPosition(player.Value), new Vector3i(xx, yy, zz)) < 9)
                            {
                                playernear = true;
                            }
                        }
                        if (!playernear)
                        {
                            l.Add(new Vector3i(xx, yy, zz));
                        }
                    }
                }
            }
        }
        l.Sort((a, b) => VectorUtils.DistanceSquared(a, pos).CompareTo(VectorUtils.DistanceSquared(b, pos)));
        if (l.Count > 0)
        {
            return l[0];
        }
        return null;
    }

    public void SetBlockType(int id, string name, BlockType block)
    {
        BlockTypes[id] = block;
        block.Name = name;
        BlockTypeRegistry.RegisterBlockType(id, block);
    }

    public void SetBlockType(string name, BlockType block)
    {
        int id = BlockTypes.Count == 0 ? 0 : BlockTypes.Keys.Max() + 1;
        SetBlockType(id, name, block);
    }

    private int[] sunlevels = [];
    public void SetSunLevels(int[] sunLevels)
    {
        this.sunlevels = sunLevels;
    }

    private float[] lightlevels = [];
    public void SetLightLevels(float[] lightLevels)
    {
        this.lightlevels = lightLevels;
    }

    public List<CraftingRecipe> CraftingRecipes { get; set; } = [];

    public bool IsSinglePlayer
    {
        get { return MainSockets[0].GetType() == typeof(DummyNetServer); }
    }

    public void SendDialog(int player, string id, Dialog dialog)
    {
        Packet_ServerDialog p = new()
        {
            DialogId = id,
            Dialog = dialog,
        };
        SendPacket(player, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Dialog, Dialog = p }));
    }

    public bool PlayerHasPrivilege(int player, string privilege)
    {
        if (ExtraPrivileges.ContainsKey(privilege))
        {
            return true;
        }
        if (Disabledprivileges.ContainsKey(privilege))
        {
            return false;
        }
        return GetClient(player).Privileges.Contains(privilege);
    }

    public void PlaySoundAt(int posx, int posy, int posz, string sound)
    {
        PlaySoundAtExceptPlayer(posx, posy, posz, sound, null);
    }

    private void PlaySoundAtExceptPlayer(int posx, int posy, int posz, string sound, int? player)
    {
        Vector3i pos = new(posx, posy, posz);
        foreach (var k in Clients)
        {
            if (player != null && player == k.Key)
            {
                continue;
            }
            int distance = VectorUtils.DistanceSquared(new Vector3i(k.Value.PositionMul32GlX / 32, k.Value.PositionMul32GlZ / 32, k.Value.PositionMul32GlY / 32), pos);
            if (distance < 64 * 64)
            {
                SendSound(k.Key, sound, pos.X, posy, posz);
            }
        }
    }

    public void PlaySoundAt(int posx, int posy, int posz, string sound, int range)
    {
        PlaySoundAtExceptPlayer(posx, posy, posz, sound, null, range);
    }

    private void PlaySoundAtExceptPlayer(int posx, int posy, int posz, string sound, int? player, int range)
    {
        Vector3i pos = new(posx, posy, posz);
        foreach (var k in Clients)
        {
            if (player != null && player == k.Key)
            {
                continue;
            }
            int distance = VectorUtils.DistanceSquared(new Vector3i(k.Value.PositionMul32GlX / 32, k.Value.PositionMul32GlZ / 32, k.Value.PositionMul32GlY / 32), pos);
            if (distance < range)
            {
                SendSound(k.Key, sound, pos.X, posy, posz);
            }
        }
    }

    public void SendPacketFollow(int player, int target, bool tpp)
    {
        Packet_ServerFollow p = new()
        {
            Client = target == -1 ? null : Clients[target].PlayerName,
            Tpp = tpp ? 1 : 0,
        };
        SendPacket(player, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Follow, Follow = p }));
    }

    public void SendAmmo(int playerid, Dictionary<int, int> totalAmmo)
    {
        Packet_ServerAmmo p = new();
        Packet_IntInt[] t = new Packet_IntInt[totalAmmo.Count];
        int i = 0;
        foreach (var k in totalAmmo)
        {
            t[i++] = new Packet_IntInt() { Key_ = k.Key, Value_ = k.Value };
        }
        p.TotalAmmo = t;
        SendPacket(playerid, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Ammo, Ammo = p }));
    }

    public void SendExplosion(int player, float x, float y, float z, bool relativeposition, float range, float time)
    {
        Packet_ServerExplosion p = new()
        {
            XFloat = SerializeFloat(x),
            YFloat = SerializeFloat(y),
            ZFloat = SerializeFloat(z),
            IsRelativeToPlayerPosition = relativeposition ? 1 : 0,
            RangeFloat = SerializeFloat(range),
            TimeFloat = SerializeFloat(time)
        };
        SendPacket(player, Serialize(new Packet_Server() { Id = Packet_ServerIdEnum.Explosion, Explosion = p }));
    }

    public string GetGroupColor(int playerid)
    {
        return GetClient(playerid).ClientGroup.GroupColorString();
    }

    public string GetGroupName(int playerid)
    {
        return GetClient(playerid).ClientGroup.Name;
    }

    public void InstallHttpModule(string name, Func<string> description, IHttpModule module)
    {
        ActiveHttpModule m = new()
        {
            name = name,
            description = description,
            module = module
        };
        HttpModules.Add(m);
    }

    public List<ActiveHttpModule> HttpModules { get; set; } = [];

    public ModEventHandlers ModEventHandlers { get; set; } = new();

    public int GetSimulationCurrentFrame()
    {
        return SimulationCurrentFrame;
    }

    public GameTimer GetTimer()
    {
        return _gameTimer;
    }

    public void PlayerEntitySetDirty(int player)
    {
        foreach (var k in Clients.Values)
        {
            k.PlayersDirty[player] = true;
        }
    }

    public ServerEntity GetEntity(int chunkx, int chunky, int chunkz, int id)
    {
        ServerChunk c = Map.GetChunk(chunkx * ChunkSize, chunky * ChunkSize, chunkz * ChunkSize);
        return c.Entities[id];
    }

    public void SetEntityDirty(ServerEntityId id)
    {
        foreach (var k in Clients)
        {
            for (int i = 0; i < k.Value.SpawnedEntities.Length; i++)
            {
                ServerEntityId s = k.Value.SpawnedEntities[i];
                if (s != null &&
                    s.ChunkX == id.ChunkX &&
                    s.ChunkY == id.ChunkY &&
                    s.ChunkZ == id.ChunkZ &&
                    s.Id == id.Id)
                {
                    k.Value.UpdateEntity[i] = true;
                }
            }
        }
        ServerChunk chunk = Map.GetChunk(id.ChunkX * ChunkSize, id.ChunkY * ChunkSize, id.ChunkZ * ChunkSize);
        chunk.DirtyForSaving = true;
    }

    public void DespawnEntity(ServerEntityId id)
    {
        ServerChunk chunk = Map.GetChunk(id.ChunkX * ChunkSize, id.ChunkY * ChunkSize, id.ChunkZ * ChunkSize);
        chunk.Entities.Remove(id.Id);
        chunk.DirtyForSaving = true;
    }

    public void AddEntity(int x, int y, int z, ServerEntity e)
    {
        ServerChunk c = Map.GetChunk(x, y, z);
        int id = c.Entities.Count == 0 ? 0 : c.Entities.Keys.Max() + 1;
        c.Entities[id] = e;
        c.DirtyForSaving = true;
    }
}

public interface ICurrentTime
{
    int GetSimulationCurrentFrame();
}

public class Timer
{
    public double INTERVAL { get { return interval; } set { interval = value; } }
    public double MaxDeltaTime { get { return maxDeltaTime; } set { maxDeltaTime = value; } }
    private double interval = 1;
    private double maxDeltaTime = double.PositiveInfinity;

    private double starttime;
    private double oldtime;
    public double accumulator;
    public Timer()
    {
        Reset();
    }
    public void Reset()
    {
        starttime = GetTime();
    }
    public delegate void Tick();
    public void Update(Tick tick)
    {
        double currenttime = GetTime() - starttime;
        double deltaTime = currenttime - oldtime;
        accumulator += deltaTime;
        double dt = INTERVAL;
        if (MaxDeltaTime != double.PositiveInfinity && accumulator > MaxDeltaTime)
        {
            accumulator = MaxDeltaTime;
        }
        while (accumulator >= dt)
        {
            tick();
            accumulator -= dt;
        }
        oldtime = currenttime;
    }

    private static double GetTime()
    {
        return (double)DateTime.UtcNow.Ticks / (10 * 1000 * 1000);
    }
}
