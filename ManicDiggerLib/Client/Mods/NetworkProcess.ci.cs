using System.Buffers;
using System.Runtime.InteropServices;

namespace ManicDigger.Mods;

public class ModNetworkProcess : ModBase
{
    public ModNetworkProcess()
    {
        CurrentChunk = new byte[1024 * 64];
        CurrentChunkCount = 0;
        receivedchunk = new int[32 * 32 * 32];
        decompressedchunk = new byte[32 * 32 * 32 * 2];
    }

    internal byte[] CurrentChunk;
    internal int CurrentChunkCount;
    private readonly int[] receivedchunk;
    private readonly byte[] decompressedchunk;

    // ── Scratch buffers ──────────────────────────────────────────────────────
    // Static so they are shared across all calls to the static ProcessPacket.
    // Each is only touched on the main thread, so there is no concurrency risk.

    /// <summary>
    /// Scratch buffer used when building the per-block-type texture ID list in
    /// <see cref="ProcessPacket"/> (BlockTypes case).
    /// Avoids allocating <c>new string[7]</c> for each of the 1,024 block types
    /// every time block-type data arrives from the server.
    /// </summary>
    private static readonly string[] s_textureIdScratch = new string[7];

#if CITO
    macro Index3d(x, y, h, sizex, sizey) ((((((h) * (sizey)) + (y))) * (sizex)) + (x))
#else
    private static int Index3d(int x, int y, int h, int sizex, int sizey)
    {
        return (h * sizey + y) * sizex + x;
    }
#endif

    private static Game game;
    public override void OnReadOnlyBackgroundThread(Game game_, float dt)
    {
        game = game_;
        NetworkProcess();
    }

    public void NetworkProcess()
    {
        game.currentTimeMilliseconds = game.platform.TimeMillisecondsFromStart;
        if (game.main == null)
        {
            return;
        }

        NetIncomingMessage msg;
        for (; ; )
        {
            if (game.invalidVersionPacketIdentification != null) { break; }

            msg = game.main.ReadMessage();
            if (msg == null) { break; }

            // ── Payload copy via ArrayPool ────────────────────────────────────
            // msg.Payload.ToArray() previously allocated a new byte[] on every
            // received message. We instead rent a buffer from the shared pool,
            // copy the payload into it, and return it immediately after
            // TryReadPacket completes (deserialization is synchronous).
            int payloadLength = msg.Payload.Length;
            byte[] rentedPayload = ArrayPool<byte>.Shared.Rent(payloadLength);
            try
            {
                // CopyTo works for both ArraySegment<byte> and byte[].
                msg.Payload.CopyTo(rentedPayload);
                TryReadPacket(rentedPayload, payloadLength);
            }
            finally
            {
                // Safe to return here: DeserializeBuffer in TryReadPacket is
                // synchronous and the Packet_Server it populates lives in the
                // closure — not in rentedPayload.
                ArrayPool<byte>.Shared.Return(rentedPayload);
            }
        }
    }

    public void TryReadPacket(byte[] data, int dataLength)
    {
        Packet_Server packet = new();
        Packet_ServerSerializer.DeserializeBuffer(data, dataLength, packet);

        ProcessInBackground(packet);

        game.QueueActionCommit(CreateProcessPacketTask(game, packet));

        game.LastReceivedMilliseconds = game.currentTimeMilliseconds;
    }

    private void ProcessInBackground(Packet_Server packet)
    {
        switch (packet.Id)
        {
            case Packet_ServerIdEnum.ChunkPart:
                {
                    byte[] arr = packet.ChunkPart.CompressedChunkPart;

                    // ── Guard against CurrentChunk overflow ───────────────────────
                    // If accumulated parts would exceed the buffer, discard and reset.
                    // The matching Chunk_ packet will receive CurrentChunkCount == 0
                    // and zero-fill receivedchunk, which is safer than a corrupt decompress.
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

                    // ── Always reset CurrentChunkCount, even on decompression failure ──
                    // Previously, a ZLibException from GzipDecompress left CurrentChunkCount
                    // non-zero. The next Chunk_ packet would then try to decompress the same
                    // stale bytes → second ZLibException → cascade under memory pressure.
                    // We capture the count, reset immediately, then attempt decompression.
                    int compressedLength = CurrentChunkCount;
                    CurrentChunkCount = 0;

                    if (compressedLength != 0)
                    {
                        try
                        {
                            game.platform.GzipDecompress(CurrentChunk, compressedLength, decompressedchunk);
                        }
                        catch (Exception ex)
                        {
                            // Decompression failed — log and skip this chunk.
                            // CurrentChunkCount is already 0 so the next Chunk_ is clean.
                            game.ChatLog(string.Format("[NET] Chunk decompression failed: {0}", ex.Message));
                            break;
                        }

                        int i = 0;
                        for (int zz = 0; zz < p.SizeZ; zz++)
                        {
                            for (int yy = 0; yy < p.SizeY; yy++)
                            {
                                for (int xx = 0; xx < p.SizeX; xx++)
                                {
                                    int block = (decompressedchunk[i + 1] << 8) + decompressedchunk[i];
                                    if (block < GlobalVar.MAX_BLOCKTYPES)
                                    {
                                        receivedchunk[Index3d(xx, yy, zz, p.SizeX, p.SizeY)] = block;
                                    }
                                    i += 2;
                                }
                            }
                        }
                    }
                    else
                    {
                        int size = p.SizeX * p.SizeY * p.SizeZ;
                        for (int i = 0; i < size; i++) { receivedchunk[i] = 0; }
                    }

                    game.VoxelMap.SetMapPortion(p.X, p.Y, p.Z, receivedchunk, p.SizeX, p.SizeY, p.SizeZ);
                    game.ReceivedMapLength += compressedLength;
                    break;
                }

            case Packet_ServerIdEnum.HeightmapChunk:
                {
                    Packet_ServerHeightmapChunk p = packet.HeightmapChunk;
                    game.platform.GzipDecompress(p.CompressedHeightmap, p.CompressedHeightmap.Length, decompressedchunk);
                    ReadOnlySpan<ushort> decompressedchunk1 = MemoryMarshal.Cast<byte, ushort>(
                        decompressedchunk.AsSpan(0, p.SizeX * p.SizeY * 2));
                    for (int xx = 0; xx < p.SizeX; xx++)
                    {
                        for (int yy = 0; yy < p.SizeY; yy++)
                        {
                            int height = decompressedchunk1[VectorIndexUtil.Index2d(xx, yy, p.SizeX)];
                            game.d_Heightmap.SetBlock(p.X + xx, p.Y + yy, height);
                        }
                    }
                    break;
                }
        }
    }

    public static Action CreateProcessPacketTask(Game game, Packet_Server packet_)
    {
        // NOTE: this lambda allocates a new Action delegate per packet because it
        // closes over packet_. If packet throughput becomes a bottleneck, replace
        // QueueActionCommit with a ConcurrentQueue<Packet_Server> drained by the
        // main thread — that would eliminate this allocation entirely.
        return () => ProcessPacket(packet_);
    }

    internal static void ProcessPacket(Packet_Server packet)
    {
        game.packetHandlers[packet.Id]?.Handle(game, packet);
        switch (packet.Id)
        {
            case Packet_ServerIdEnum.ServerIdentification:
                {
                    string invalidversionstr = game.language.InvalidVersionConnectAnyway();
                    game.serverGameVersion = packet.Identification.MdProtocolVersion;
                    if (game.serverGameVersion != game.platform.GetGameVersion())
                    {
                        game.ChatLog("[GAME] Different game versions");
                        string q = string.Format(invalidversionstr, game.platform.GetGameVersion(), game.serverGameVersion);
                        game.invalidVersionDrawMessage = q;
                        game.invalidVersionPacketIdentification = packet;
                    }
                    else
                    {
                        game.ProcessServerIdentification(packet);
                    }
                    game.ReceivedMapLength = 0;
                    break;
                }
            case Packet_ServerIdEnum.Ping:
                {
                    game.SendPingReply();
                    game.ServerInfo.ServerPing.Send(game.platform.TimeMillisecondsFromStart);
                    break;
                }
            case Packet_ServerIdEnum.PlayerPing:
                {
                    game.ServerInfo.ServerPing.Receive(game.platform);
                    break;
                }
            case Packet_ServerIdEnum.LevelInitialize:
                {
                    game.ChatLog("[GAME] Initialized map loading");
                    game.ReceivedMapLength = 0;
                    game.InvokeMapLoadingProgress(0, 0, game.language.Connecting());
                    break;
                }
            case Packet_ServerIdEnum.LevelDataChunk:
                {
                    game.InvokeMapLoadingProgress(
                        packet.LevelDataChunk.PercentComplete,
                        game.ReceivedMapLength,
                        packet.LevelDataChunk.Status);
                    break;
                }
            case Packet_ServerIdEnum.LevelFinalize:
                {
                    game.ChatLog("[GAME] Finished map loading");
                    break;
                }
            case Packet_ServerIdEnum.SetBlock:
                {
                    game.SetTileAndUpdate(packet.SetBlock.X, packet.SetBlock.Y, packet.SetBlock.Z, packet.SetBlock.BlockType);
                    break;
                }
            case Packet_ServerIdEnum.FillArea:
                {
                    int ax = packet.FillArea.X1;
                    int ay = packet.FillArea.Y1;
                    int az = packet.FillArea.Z1;
                    int bx = packet.FillArea.X2;
                    int by = packet.FillArea.Y2;
                    int bz = packet.FillArea.Z2;

                    int startx = Math.Min(ax, bx);
                    int endx = Math.Max(ax, bx);
                    int starty = Math.Min(ay, by);
                    int endy = Math.Max(ay, by);
                    int startz = Math.Min(az, bz);
                    int endz = Math.Max(az, bz);

                    int blockCount = packet.FillArea.BlockCount;
                    for (int x = startx; x <= endx; x++)
                    {
                        for (int y = starty; y <= endy; y++)
                        {
                            for (int z = startz; z <= endz; z++)
                            {
                                if (blockCount == 0) { return; }
                                game.SetTileAndUpdate(x, y, z, packet.FillArea.BlockType);
                                blockCount--;
                            }
                        }
                    }
                    break;
                }
            case Packet_ServerIdEnum.FillAreaLimit:
                {
                    game.fillAreaLimit = Math.Min(packet.FillAreaLimit.Limit, 100000);
                    break;
                }
            case Packet_ServerIdEnum.Freemove:
                {
                    game.AllowFreemove = packet.Freemove.IsEnabled != 0;
                    if (!game.AllowFreemove)
                    {
                        game.controls.freemove = false;
                        game.controls.noclip = false;
                        game.movespeed = game.basemovespeed;
                        game.Log(game.language.MoveNormal());
                    }
                    break;
                }
            case Packet_ServerIdEnum.PlayerSpawnPosition:
                {
                    int x = packet.PlayerSpawnPosition.X;
                    int y = packet.PlayerSpawnPosition.Y;
                    int z = packet.PlayerSpawnPosition.Z;
                    game.playerPositionSpawnX = x;
                    game.playerPositionSpawnY = z;
                    game.playerPositionSpawnZ = y;
                    game.Log(string.Format(game.language.SpawnPositionSetTo(),
                        string.Format("{0},{1},{2}", x, y, z)));
                    break;
                }
            case Packet_ServerIdEnum.Message:
                {
                    game.AddChatline(packet.Message.Message);
                    game.ChatLog(packet.Message.Message);
                    break;
                }
            case Packet_ServerIdEnum.DisconnectPlayer:
                {
                    game.ChatLog(string.Format("[GAME] Disconnected by the server ({0})", packet.DisconnectPlayer.DisconnectReason));
                    if (game.platform.IsMousePointerLocked())
                    {
                        game.platform.ExitMousePointerLock();
                    }
                    game.platform.MessageBoxShowError(packet.DisconnectPlayer.DisconnectReason, "Disconnected from server");
                    game.ExitToMainMenu_();
                    break;
                }
            case Packet_ServerIdEnum.PlayerStats:
                {
                    game.PlayerStats = packet.PlayerStats;
                    break;
                }
            case Packet_ServerIdEnum.FiniteInventory:
                {
                    if (packet.Inventory.Inventory != null)
                    {
                        game.UseInventory(packet.Inventory.Inventory);
                    }
                    break;
                }
            case Packet_ServerIdEnum.Season:
                {
                    packet.Season.Hour -= 1;
                    if (packet.Season.Hour < 0) { packet.Season.Hour = 12 * Game.HourDetail; }
                    int sunlight = game.NightLevels[packet.Season.Hour];
                    game.SkySphereNight = sunlight < 8;
                    game.d_SunMoonRenderer.day_length_in_seconds = 60 * 60 * 24 / packet.Season.DayNightCycleSpeedup;
                    int hour = packet.Season.Hour / Game.HourDetail;
                    if (game.d_SunMoonRenderer.GetHour() != hour)
                    {
                        game.d_SunMoonRenderer.SetHour(hour);
                    }
                    if (game.sunlight_ != sunlight)
                    {
                        game.sunlight_ = sunlight;
                        game.RedrawAllBlocks();
                    }
                    break;
                }
            case Packet_ServerIdEnum.BlobInitialize:
                {
                    game.blobdownload = new MemoryStream();
                    game.blobdownloadname = packet.BlobInitialize.Name;
                    game.blobdownloadmd5 = packet.BlobInitialize.Md5;
                    break;
                }
            case Packet_ServerIdEnum.BlobPart:
                {
                    int length = packet.BlobPart.Data.Length;
                    game.blobdownload.Write(packet.BlobPart.Data, 0, length);
                    game.ReceivedMapLength += length;
                    break;
                }
            case Packet_ServerIdEnum.BlobFinalize:
                {
                    byte[] downloaded = game.blobdownload.ToArray();
                    if (game.blobdownloadname != null)
                    {
                        game.SetFile(game.blobdownloadname, game.blobdownloadmd5, downloaded, (int)game.blobdownload.Length);
                    }
                    game.blobdownload = null;
                    break;
                }
            case Packet_ServerIdEnum.Sound:
                {
                    game.PlayAudio(packet.Sound.Name, packet.Sound.X, packet.Sound.Y, packet.Sound.Z);
                    break;
                }
            case Packet_ServerIdEnum.RemoveMonsters:
                {
                    for (int i = Game.entityMonsterIdStart; i < Game.entityMonsterIdStart + Game.entityMonsterIdCount; i++)
                    {
                        game.entities[i] = null;
                    }
                    break;
                }
            case Packet_ServerIdEnum.Translation:
                game.language.Override(packet.Translation.Lang, packet.Translation.Id, packet.Translation.Translation);
                break;
            case Packet_ServerIdEnum.BlockType:
                game.NewBlockTypes[packet.BlockType.Id] = packet.BlockType.Blocktype;
                break;
            case Packet_ServerIdEnum.SunLevels:
                game.NightLevels = packet.SunLevels.Sunlevels;
                break;
            case Packet_ServerIdEnum.LightLevels:
                for (int i = 0; i < packet.LightLevels.LightlevelsCount; i++)
                {
                    game.mLightLevels[i] = game.DecodeFixedPoint(packet.LightLevels.Lightlevels[i]);
                }
                break;
            case Packet_ServerIdEnum.Follow:
                game.Follow = packet.Follow.Client;
                if (packet.Follow.Tpp != 0)
                {
                    game.SetCamera(CameraType.Overhead);
                    game.player.position.rotx = MathF.PI;
                    game.GuiStateBackToGame();
                }
                else
                {
                    game.SetCamera(CameraType.Fpp);
                }
                break;
            case Packet_ServerIdEnum.Bullet:
                game.EntityAddLocal(Game.CreateBulletEntity(
                    game.DecodeFixedPoint(packet.Bullet.FromXFloat),
                    game.DecodeFixedPoint(packet.Bullet.FromYFloat),
                    game.DecodeFixedPoint(packet.Bullet.FromZFloat),
                    game.DecodeFixedPoint(packet.Bullet.ToXFloat),
                    game.DecodeFixedPoint(packet.Bullet.ToYFloat),
                    game.DecodeFixedPoint(packet.Bullet.ToZFloat),
                    game.DecodeFixedPoint(packet.Bullet.SpeedFloat)));
                break;
            case Packet_ServerIdEnum.Ammo:
                {
                    if (!game.ammostarted)
                    {
                        game.ammostarted = true;
                        for (int i = 0; i < packet.Ammo.TotalAmmoCount; i++)
                        {
                            Packet_IntInt k = packet.Ammo.TotalAmmo[i];
                            game.LoadedAmmo[k.Key_] = Math.Min(k.Value_, game.blocktypes[k.Key_].AmmoMagazine);
                        }
                    }

                    // ── Reuse TotalAmmo array instead of allocating a new one ────
                    // The old code did `game.TotalAmmo = new int[MAX_BLOCKTYPES]` every
                    // Ammo packet. We allocate once and clear-in-place thereafter.
                    if (game.TotalAmmo == null)
                        game.TotalAmmo = new int[GlobalVar.MAX_BLOCKTYPES];
                    else
                        Array.Clear(game.TotalAmmo, 0, GlobalVar.MAX_BLOCKTYPES);

                    for (int i = 0; i < packet.Ammo.TotalAmmoCount; i++)
                    {
                        game.TotalAmmo[packet.Ammo.TotalAmmo[i].Key_] = packet.Ammo.TotalAmmo[i].Value_;
                    }
                    break;
                }
            case Packet_ServerIdEnum.Explosion:
                {
                    Entity entity = new()
                    {
                        expires = new Expires { timeLeft = game.DecodeFixedPoint(packet.Explosion.TimeFloat) },
                        push = packet.Explosion
                    };
                    game.EntityAddLocal(entity);
                    break;
                }
            case Packet_ServerIdEnum.Projectile:
                {
                    Entity entity = new();
                    Sprite sprite = new()
                    {
                        image = "ChemicalGreen.png",
                        size = 14,
                        animationcount = 0,
                        positionX = game.DecodeFixedPoint(packet.Projectile.FromXFloat),
                        positionY = game.DecodeFixedPoint(packet.Projectile.FromYFloat),
                        positionZ = game.DecodeFixedPoint(packet.Projectile.FromZFloat)
                    };
                    entity.sprite = sprite;
                    Grenade grenade = new()
                    {
                        velocityX = game.DecodeFixedPoint(packet.Projectile.VelocityXFloat),
                        velocityY = game.DecodeFixedPoint(packet.Projectile.VelocityYFloat),
                        velocityZ = game.DecodeFixedPoint(packet.Projectile.VelocityZFloat),
                        block = packet.Projectile.BlockId,
                        sourcePlayer = packet.Projectile.SourcePlayerID
                    };
                    entity.grenade = grenade;
                    entity.expires = Expires.Create(game.DecodeFixedPoint(packet.Projectile.ExplodesAfterFloat));
                    game.EntityAddLocal(entity);
                    break;
                }
            case Packet_ServerIdEnum.BlockTypes:
                {
                    game.blocktypes = game.NewBlockTypes;
                    game.NewBlockTypes = new Packet_BlockType[GlobalVar.MAX_BLOCKTYPES];

                    string[] textureInAtlasIds = new string[1024];
                    int textureInAtlasIdsCount = 1024;
                    int lastTextureId = 0;

                    // ── Use static scratch buffer instead of new string[7] per block ──
                    // The old code allocated a fresh string[7] inside this loop for
                    // every block type — up to 1,024 temporary arrays from one packet.
                    // s_textureIdScratch is a static field reused each iteration.
                    string[] scratch = s_textureIdScratch;

                    for (int i = 0; i < GlobalVar.MAX_BLOCKTYPES; i++)
                    {
                        if (game.blocktypes[i] != null)
                        {
                            scratch[0] = game.blocktypes[i].TextureIdLeft;
                            scratch[1] = game.blocktypes[i].TextureIdRight;
                            scratch[2] = game.blocktypes[i].TextureIdFront;
                            scratch[3] = game.blocktypes[i].TextureIdBack;
                            scratch[4] = game.blocktypes[i].TextureIdTop;
                            scratch[5] = game.blocktypes[i].TextureIdBottom;
                            scratch[6] = game.blocktypes[i].TextureIdForInventory;

                            for (int k = 0; k < 7; k++)
                            {
                                if (!Contains(textureInAtlasIds, textureInAtlasIdsCount, scratch[k]))
                                {
                                    textureInAtlasIds[lastTextureId++] = scratch[k];
                                }
                            }
                        }
                    }

                    game.d_Data.UseBlockTypes(game.platform, game.blocktypes, GlobalVar.MAX_BLOCKTYPES);
                    for (int i = 0; i < GlobalVar.MAX_BLOCKTYPES; i++)
                    {
                        Packet_BlockType b = game.blocktypes[i];
                        if (b == null) { continue; }
                        if (textureInAtlasIds != null)
                        {
                            game.TextureId[i][0] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdTop);
                            game.TextureId[i][1] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdBottom);
                            game.TextureId[i][2] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdFront);
                            game.TextureId[i][3] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdBack);
                            game.TextureId[i][4] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdLeft);
                            game.TextureId[i][5] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdRight);
                            game.TextureIdForInventory[i] = IndexOf(textureInAtlasIds, textureInAtlasIdsCount, b.TextureIdForInventory);
                        }
                    }
                    game.UseTerrainTextures(textureInAtlasIds, textureInAtlasIdsCount);
                    game.handRedraw = true;
                    game.RedrawAllBlocks();
                    break;
                }
            case Packet_ServerIdEnum.ServerRedirect:
                game.ChatLog("[GAME] Received server redirect");
                game.SendLeave(PacketLeaveReason.Leave);
                game.ExitAndSwitchServer(packet.Redirect);
                break;
        }
    }

    private static bool Contains(string[] arr, int arrLength, string value)
        => IndexOf(arr, arrLength, value) != -1;

    private static int IndexOf(string[] arr, int arrLength, string value)
    {
        for (int i = 0; i < arrLength; i++)
        {
            if (string.Equals(arr[i], value))
                return i;
        }
        return -1;
    }
}