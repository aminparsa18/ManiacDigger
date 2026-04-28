//This class is the network packet processor — it sits between the raw network socket and the rest of the game.
//Every frame on a background thread it reads incoming messages from the server,
//deserializes them, and splits work into two phases:
//Background thread(ProcessInBackground) handles data-heavy operations that don't touch game state:
//assembling chunk data from multiple compressed parts, decompressing them, and filling the voxel map.
//Also handles heightmap chunks the same way.
//Main thread (ProcessPacket) handles everything that mutates visible game state:
//player stats, block type definitions, chat messages, disconnects, sound, entities,
//seasons, inventory — essentially every game event the server can send.
//The split exists because chunk decompression is expensive and safe to do off the main thread,
//while UI/game-state mutations must happen on the main thread

using MemoryPack;
using System.Buffers;
using System.Runtime.InteropServices;

namespace ManicDigger.Mods;

public class ModNetworkProcess : ModBase
{
    private readonly IGameClient _game;
    private readonly IGamePlatform _platform;

    public ModNetworkProcess(IGameClient game, IGamePlatform gamePlatform)
    {
        _game = game;
        _platform = gamePlatform;
        CurrentChunk = new byte[1024 * 64];
        CurrentChunkCount = 0;
        receivedchunk = new int[32 * 32 * 32];
        decompressedchunk = new byte[32 * 32 * 32 * 2];
    }

    internal byte[] CurrentChunk;
    internal int CurrentChunkCount;
    private readonly int[] receivedchunk;
    private readonly byte[] decompressedchunk;

    // ── Fix #6: instance field instead of static ──────────────────────────────
    // Was static — safe today because ProcessPacket runs on the main thread,
    // but fragile if a second instance ever ran concurrently. Instance field
    // makes the threading contract explicit.
    private readonly string[] _textureIdScratch = new string[7];

    private static int Index3d(int x, int y, int h, int sizex, int sizey)
        => (h * sizey + y) * sizex + x;

    public override void OnReadOnlyBackgroundThread(float dt)
        => NetworkProcess();

    public void NetworkProcess()
    {
        _game.CurrentTimeMilliseconds = _platform.TimeMillisecondsFromStart;
        if (_game.NetClient == null) return;

        // ── Fix #7: while loop instead of for(;;) with two break conditions ───
        NetIncomingMessage msg;
        while (_game.InvalidVersionPacketIdentification == null
            && (msg = _game.NetClient.ReadMessage()) != null)
        {
            int payloadLength = msg.Payload.Length;
            byte[] rentedPayload = ArrayPool<byte>.Shared.Rent(payloadLength);
            try
            {
                msg.Payload.CopyTo(rentedPayload);
                TryReadPacket(rentedPayload, payloadLength);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(rentedPayload);
            }
        }
    }

    public void TryReadPacket(byte[] data, int dataLength)
    {
        Packet_Server packet;
        packet = MemoryPackSerializer.Deserialize<Packet_Server>(
            data.AsSpan(0, dataLength));
        ProcessInBackground(packet);
        _game.QueueActionCommit(() => ProcessPacket(packet));
        _game.LastReceivedMilliseconds = _game.CurrentTimeMilliseconds;
    }

    private void ProcessInBackground(Packet_Server packet)
    {
        switch (packet.Id)
        {
            case Packet_ServerIdEnum.ChunkPart:
                {
                    byte[] arr = packet.ChunkPart.CompressedChunkPart;
                    if (CurrentChunkCount + arr.Length > CurrentChunk.Length)
                    {
                        CurrentChunkCount = 0;
                        break;
                    }
                    Buffer.BlockCopy(arr, 0, CurrentChunk, CurrentChunkCount, arr.Length);
                    CurrentChunkCount += arr.Length;
                    break;
                }

            case Packet_ServerIdEnum.Chunk_:
                {
                    Packet_ServerChunk p = packet.Chunk_;

                    int compressedLength = CurrentChunkCount;
                    CurrentChunkCount = 0;

                    if (compressedLength != 0)
                    {
                        _platform.GzipDecompress(CurrentChunk, compressedLength, decompressedchunk);

                        int i = 0;
                        for (int zz = 0; zz < p.SizeZ; zz++)
                            for (int yy = 0; yy < p.SizeY; yy++)
                                for (int xx = 0; xx < p.SizeX; xx++)
                                {
                                    int block = (decompressedchunk[i + 1] << 8) + decompressedchunk[i];
                                    if (block < GlobalVar.MAX_BLOCKTYPES)
                                        receivedchunk[Index3d(xx, yy, zz, p.SizeX, p.SizeY)] = block;
                                    i += 2;
                                }
                    }
                    else
                    {
                        Array.Clear(receivedchunk, 0, p.SizeX * p.SizeY * p.SizeZ);
                    }

                    _game.VoxelMap.SetMapPortion(p.X, p.Y, p.Z, receivedchunk, p.SizeX, p.SizeY, p.SizeZ);
                    _game.ReceivedMapLength += compressedLength;
                    break;
                }

            case Packet_ServerIdEnum.HeightmapChunk:
                {
                    Packet_ServerHeightmapChunk p = packet.HeightmapChunk;
                    _platform.GzipDecompress(
                        p.CompressedHeightmap, p.CompressedHeightmap.Length, decompressedchunk);
                    ReadOnlySpan<ushort> heights = MemoryMarshal.Cast<byte, ushort>(
                        decompressedchunk.AsSpan(0, p.SizeX * p.SizeY * 2));
                    for (int xx = 0; xx < p.SizeX; xx++)
                        for (int yy = 0; yy < p.SizeY; yy++)
                            _game.Heightmap.SetBlock(
                                p.X + xx, p.Y + yy,
                                heights[VectorIndexUtil.Index2d(xx, yy, p.SizeX)]);
                    break;
                }
        }
    }

    // a private Handle*() method to make this switch a clean dispatch table
    // and enable per-handler unit testing. Deferred to keep this diff focused.
    internal void ProcessPacket(Packet_Server packet)
    {
        _game.PacketHandlers[(int)packet.Id]?.Handle(_game, packet);
        switch (packet.Id)
        {
            case Packet_ServerIdEnum.ServerIdentification:
                {
                    string invalidversionstr = _game.Language.InvalidVersionConnectAnyway();
                    _game.ServerGameVersion = packet.Identification.MdProtocolVersion;
                    if (_game.ServerGameVersion != _platform.GetGameVersion())
                    {
                        _game.ChatLog("[GAME] Different game versions");
                        string q = string.Format(invalidversionstr,
                            _platform.GetGameVersion(), _game.ServerGameVersion);
                        _game.InvalidVersionDrawMessage = q;
                        _game.InvalidVersionPacketIdentification = packet;
                    }
                    else
                    {
                        _game.ProcessServerIdentification(packet);
                    }
                    _game.ReceivedMapLength = 0;
                    break;
                }

            case Packet_ServerIdEnum.Ping:
                _game.SendPingReply();
                _game.ServerInfo.ServerPing.Send(_platform.TimeMillisecondsFromStart);
                break;

            case Packet_ServerIdEnum.PlayerPing:
                _game.ServerInfo.ServerPing.Receive(_platform);
                break;

            case Packet_ServerIdEnum.LevelInitialize:
                _game.ChatLog("[GAME] Initialized map loading");
                _game.ReceivedMapLength = 0;
                _game.InvokeMapLoadingProgress(0, 0, _game.Language.Connecting());
                break;

            case Packet_ServerIdEnum.LevelDataChunk:
                _game.InvokeMapLoadingProgress(
                    packet.LevelDataChunk.PercentComplete,
                    _game.ReceivedMapLength,
                    packet.LevelDataChunk.Status);
                break;

            case Packet_ServerIdEnum.LevelFinalize:
                _game.ChatLog("[GAME] Finished map loading");
                break;

            case Packet_ServerIdEnum.SetBlock:
                _game.SetTileAndUpdate(
                    packet.SetBlock.X, packet.SetBlock.Y, packet.SetBlock.Z,
                    packet.SetBlock.BlockType);
                break;

            case Packet_ServerIdEnum.FillArea:
                {
                    int startx = Math.Min(packet.FillArea.X1, packet.FillArea.X2);
                    int endx = Math.Max(packet.FillArea.X1, packet.FillArea.X2);
                    int starty = Math.Min(packet.FillArea.Y1, packet.FillArea.Y2);
                    int endy = Math.Max(packet.FillArea.Y1, packet.FillArea.Y2);
                    int startz = Math.Min(packet.FillArea.Z1, packet.FillArea.Z2);
                    int endz = Math.Max(packet.FillArea.Z1, packet.FillArea.Z2);
                    int blockCount = packet.FillArea.BlockCount;

                    for (int x = startx; x <= endx && blockCount > 0; x++)
                        for (int y = starty; y <= endy && blockCount > 0; y++)
                            for (int z = startz; z <= endz && blockCount > 0; z++)
                            {
                                _game.SetTileAndUpdate(x, y, z, packet.FillArea.BlockType);
                                blockCount--;
                            }
                    break;
                }

            case Packet_ServerIdEnum.FillAreaLimit:
                _game.FillAreaLimit = Math.Min(packet.FillAreaLimit.Limit, 100000);
                break;

            case Packet_ServerIdEnum.Freemove:
                _game.AllowFreeMove = packet.Freemove.IsEnabled != 0;
                if (!_game.AllowFreeMove)
                {
                    _game.Controls.FreeMove = false;
                    _game.Controls.NoClip = false;
                    _game.MoveSpeed = _game.Basemovespeed;
                    _game.AddChatLine(_game.Language.MoveNormal());
                }
                break;

            case Packet_ServerIdEnum.PlayerSpawnPosition:
                {
                    int x = packet.PlayerSpawnPosition.X;
                    int y = packet.PlayerSpawnPosition.Y;
                    int z = packet.PlayerSpawnPosition.Z;
                    _game.PlayerPositionSpawnX = x;
                    _game.PlayerPositionSpawnY = z;
                    _game.PlayerPositionSpawnZ = y;
                    _game.AddChatLine(string.Format(
                        _game.Language.SpawnPositionSetTo(), $"{x},{y},{z}"));
                    break;
                }

            case Packet_ServerIdEnum.Message:
                _game.AddChatLine(packet.Message.Message);
                _game.ChatLog(packet.Message.Message);
                break;

            case Packet_ServerIdEnum.DisconnectPlayer:
                _game.ChatLog($"[GAME] Disconnected by the server ({packet.DisconnectPlayer.DisconnectReason})");
                if (_platform.IsMousePointerLocked())
                    _platform.ExitMousePointerLock();
                _platform.MessageBoxShowError(
                    packet.DisconnectPlayer.DisconnectReason, "Disconnected from server");
                _game.ExitToMainMenu();
                break;

            case Packet_ServerIdEnum.PlayerStats:
                _game.PlayerStats = packet.PlayerStats;
                break;

            case Packet_ServerIdEnum.FiniteInventory:
                if (packet.Inventory.Inventory != null)
                    _game.UseInventory(packet.Inventory.Inventory);
                break;

            case Packet_ServerIdEnum.Season:
                {
                    packet.Season.Hour -= 1;
                    if (packet.Season.Hour < 0) packet.Season.Hour = 12 * Game.HourDetail;
                    if (_game.NightLevels == null) break;
                    if (packet.Season.Hour >= _game.NightLevels.Length) break;
                    int sunlight = _game.NightLevels[packet.Season.Hour];
                    _game.SkySphereNight = sunlight < 8;
                    _game.SunMoonRenderer.day_length_in_seconds =
                        60 * 60 * 24 / packet.Season.DayNightCycleSpeedup;
                    int hour = packet.Season.Hour / Game.HourDetail;
                    if (_game.SunMoonRenderer.GetHour() != hour)
                        _game.SunMoonRenderer.SetHour(hour);
                    if (_game.Sunlight != sunlight)
                    {
                        _game.Sunlight = sunlight;
                        _game.RedrawAllBlocks();
                    }
                    break;
                }

            case Packet_ServerIdEnum.BlobInitialize:
                _game.BlobDownload = new MemoryStream();
                _game.BlobDownloadName = packet.BlobInitialize.Name;
                _game.BlobDownloadMd5 = packet.BlobInitialize.Md5;
                break;

            case Packet_ServerIdEnum.BlobPart:
                {
                    int length = packet.BlobPart.Data.Length;
                    _game.BlobDownload.Write(packet.BlobPart.Data, 0, length);
                    _game.ReceivedMapLength += length;
                    break;
                }

            case Packet_ServerIdEnum.BlobFinalize:
                {
                    byte[] downloaded = _game.BlobDownload.ToArray();
                    if (_game.BlobDownloadName != null)
                        _game.SetFile(_game.BlobDownloadName, _game.BlobDownloadMd5,
                            downloaded, (int)_game.BlobDownload.Length);
                    _game.BlobDownload = null;
                    break;
                }

            case Packet_ServerIdEnum.Sound:
                _game.PlayAudio(
                    packet.Sound.Name,
                    packet.Sound.X, packet.Sound.Y, packet.Sound.Z);
                break;

            case Packet_ServerIdEnum.RemoveMonsters:
                for (int i = Game.entityMonsterIdStart;
                     i < Game.entityMonsterIdStart + Game.entityMonsterIdCount; i++)
                    _game.Entities[i] = null;
                break;

            case Packet_ServerIdEnum.Translation:
                _game.Language.Override(
                    packet.Translation.Lang,
                    packet.Translation.Id,
                    packet.Translation.Translation);
                break;

            case Packet_ServerIdEnum.BlockType:
                _game.NewBlockTypes[packet.BlockType.Id] = packet.BlockType.Blocktype;
                break;

            case Packet_ServerIdEnum.SunLevels:
                _game.NightLevels = packet.SunLevels.Sunlevels;
                break;

            case Packet_ServerIdEnum.LightLevels:
                for (int i = 0; i < packet.LightLevels.Lightlevels.Length; i++)
                    _game.LightLevels[i] = _game.DecodeFixedPoint(packet.LightLevels.Lightlevels[i]);
                break;

            case Packet_ServerIdEnum.Follow:
                _game.Follow = packet.Follow.Client;
                if (packet.Follow.Tpp != 0)
                {
                    _game.SetCamera(CameraType.Overhead);
                    _game.LocalOrientationX = MathF.PI;
                    _game.GuiStateBackToGame();
                }
                else
                {
                    _game.SetCamera(CameraType.Fpp);
                }
                break;

            case Packet_ServerIdEnum.Bullet:
                _game.EntityAddLocal(Game.CreateBulletEntity(
                    _game.DecodeFixedPoint(packet.Bullet.FromXFloat),
                    _game.DecodeFixedPoint(packet.Bullet.FromYFloat),
                    _game.DecodeFixedPoint(packet.Bullet.FromZFloat),
                    _game.DecodeFixedPoint(packet.Bullet.ToXFloat),
                    _game.DecodeFixedPoint(packet.Bullet.ToYFloat),
                    _game.DecodeFixedPoint(packet.Bullet.ToZFloat),
                    _game.DecodeFixedPoint(packet.Bullet.SpeedFloat)));
                break;

            case Packet_ServerIdEnum.Ammo:
                {
                    if (!_game.AmmoStarted)
                    {
                        _game.AmmoStarted = true;
                        for (int i = 0; i < packet.Ammo.TotalAmmo.Length; i++)
                        {
                            Packet_IntInt k = packet.Ammo.TotalAmmo[i];
                            _game.LoadedAmmo[k.Key_] = Math.Min(
                                k.Value_, _game.BlockTypes[k.Key_].AmmoMagazine);
                        }
                    }

                    if (_game.TotalAmmo == null)
                        _game.TotalAmmo = new int[GlobalVar.MAX_BLOCKTYPES];
                    else
                        Array.Clear(_game.TotalAmmo, 0, GlobalVar.MAX_BLOCKTYPES);

                    for (int i = 0; i < packet.Ammo.TotalAmmo.Length; i++)
                        _game.TotalAmmo[packet.Ammo.TotalAmmo[i].Key_] = packet.Ammo.TotalAmmo[i].Value_;
                    break;
                }

            case Packet_ServerIdEnum.Explosion:
                _game.EntityAddLocal(new Entity
                {
                    expires = new Expires
                    {
                        timeLeft = _game.DecodeFixedPoint(packet.Explosion.TimeFloat)
                    },
                    push = packet.Explosion,
                });
                break;

            case Packet_ServerIdEnum.Projectile:
                _game.EntityAddLocal(new Entity
                {
                    sprite = new Sprite
                    {
                        image = "ChemicalGreen.png",
                        size = 14,
                        animationcount = 0,
                        positionX = _game.DecodeFixedPoint(packet.Projectile.FromXFloat),
                        positionY = _game.DecodeFixedPoint(packet.Projectile.FromYFloat),
                        positionZ = _game.DecodeFixedPoint(packet.Projectile.FromZFloat),
                    },
                    grenade = new Grenade
                    {
                        velocityX = _game.DecodeFixedPoint(packet.Projectile.VelocityXFloat),
                        velocityY = _game.DecodeFixedPoint(packet.Projectile.VelocityYFloat),
                        velocityZ = _game.DecodeFixedPoint(packet.Projectile.VelocityZFloat),
                        block = packet.Projectile.BlockId,
                        sourcePlayer = packet.Projectile.SourcePlayerID,
                    },
                    expires = Expires.Create(_game.DecodeFixedPoint(packet.Projectile.ExplodesAfterFloat)),
                });
                break;

            case Packet_ServerIdEnum.BlockTypes:
                {
                    _game.BlockTypes = _game.NewBlockTypes;
                    _game.NewBlockTypes = [];

                    // Old code: Contains() + IndexOf() scanned a 1024-entry array
                    // for every texture of every block type (up to 7168 scans).
                    // Old code also passed textureInAtlasIdsCount=1024 even though
                    // only lastTextureId entries were populated — scanning nulls.
                    // HashSet gives O(1) membership checks; a parallel List
                    // preserves insertion order for IndexOf lookups.
                    var textureSet = new HashSet<string>(StringComparer.Ordinal);
                    var textureList = new List<string>();

                    string[] scratch = _textureIdScratch;

                    foreach (var (_, blockType) in _game.BlockTypes)
                    {
                        scratch[0] = blockType.TextureIdLeft;
                        scratch[1] = blockType.TextureIdRight;
                        scratch[2] = blockType.TextureIdFront;
                        scratch[3] = blockType.TextureIdBack;
                        scratch[4] = blockType.TextureIdTop;
                        scratch[5] = blockType.TextureIdBottom;
                        scratch[6] = blockType.TextureIdForInventory;

                        for (int k = 0; k < 7; k++)
                            if (textureSet.Add(scratch[k]))
                                textureList.Add(scratch[k]);
                    }

                    // Convert to array for downstream APIs that expect string[].
                    string[] textureInAtlasIds = textureList.ToArray();
                    int textureInAtlasIdsCount = textureInAtlasIds.Length;

                    _game.BlockRegistry.UseBlockTypes(_game.BlockTypes);

                    foreach (var (id, b) in _game.BlockTypes)
                    {
                        _game.TextureId[id] = [
                            textureList.IndexOf(b.TextureIdTop),
                            textureList.IndexOf(b.TextureIdBottom),
                            textureList.IndexOf(b.TextureIdFront),
                            textureList.IndexOf(b.TextureIdBack),
                            textureList.IndexOf(b.TextureIdLeft),
                            textureList.IndexOf(b.TextureIdRight),
                        ];
                        _game.TextureIdForInventory[id] = textureList.IndexOf(b.TextureIdForInventory);
                    }

                    _game.UseTerrainTextures(textureInAtlasIds, textureInAtlasIdsCount);
                    _game.HandRedraw = true;
                    _game.RedrawAllBlocks();
                    break;
                }

            case Packet_ServerIdEnum.ServerRedirect:
                _game.ChatLog("[GAME] Received server redirect");
                _game.SendLeave(PacketLeaveReason.Leave);
                _game.ExitAndSwitchServer(packet.Redirect);
                break;
        }
    }

    /// <summary>
    /// Simple append-only file logger for diagnostics.
    /// Thread-safe via lock. Writes to a fixed path next to the executable.
    /// </summary>
    public static class DiagLog
    {
        private static readonly string _path = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "server.log");

        private static readonly object _lock = new();

        static DiagLog()
        {
            try { File.WriteAllText(_path, $"=== DiagLog started {DateTime.Now:yyyy-MM-dd HH:mm:ss} ==={Environment.NewLine}"); }
            catch { }
        }

        public static void Write(string message)
        {
            lock (_lock)
            {
                try { File.AppendAllText(_path, $"[{DateTime.Now:HH:mm:ss.fff}] {message}{Environment.NewLine}"); }
                catch { }
            }
        }

        public static void Write(string format, params object[] args)
        {
            string message = format;

            // First pass: replace named placeholders {Name} sequentially
            // e.g. "{Img}" with args[0], "{Len}" with args[1], etc.
            int argIndex = 0;
            System.Text.StringBuilder sb = new(format.Length + 64);
            int i = 0;

            while (i < format.Length)
            {
                if (format[i] == '{' && i + 1 < format.Length)
                {
                    // Find closing brace
                    int close = format.IndexOf('}', i + 1);
                    if (close != -1)
                    {
                        string placeholder = format.Substring(i + 1, close - i - 1);

                        // Check if it's already indexed {0}, {1}...
                        if (int.TryParse(placeholder, out int explicitIndex))
                        {
                            sb.Append(explicitIndex < args.Length
                                ? args[explicitIndex]?.ToString() ?? "null"
                                : $"{{{placeholder}}}");
                        }
                        else
                        {
                            // Named placeholder — consume next arg in order
                            sb.Append(argIndex < args.Length
                                ? args[argIndex++]?.ToString() ?? "null"
                                : $"{{{placeholder}}}");
                        }

                        i = close + 1;
                        continue;
                    }
                }

                sb.Append(format[i++]);
            }

            Write(sb.ToString());
        }
    }

}