public class ClientPackets
{
    public static Packet_Client CreateLoginPacket(IGamePlatform platform, string username, string verificationKey)
    {
        Packet_ClientIdentification p = new();
        {
            p.Username = username;
            p.MdProtocolVersion = platform.GetGameVersion();
            p.VerificationKey = verificationKey;
        }
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.PlayerIdentification,
            Identification = p
        };
        return pp;
    }

    public static Packet_Client CreateLoginPacket_(IGamePlatform platform, string username, string verificationKey, string serverPassword)
    {
        Packet_ClientIdentification p = new();
        {
            p.Username = username;
            p.MdProtocolVersion = platform.GetGameVersion();
            p.VerificationKey = verificationKey;
            p.ServerPassword = serverPassword;
        }
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.PlayerIdentification,
            Identification = p
        };
        return pp;
    }

    public static Packet_Client Oxygen(int currentOxygen)
    {
        Packet_Client packet = new()
        {
            Id = Packet_ClientIdEnum.Oxygen,
            Oxygen = new Packet_ClientOxygen
            {
                CurrentOxygen = currentOxygen
            }
        };
        return packet;
    }

    public static Packet_Client Reload()
    {
        Packet_Client p = new()
        {
            Id = Packet_ClientIdEnum.Reload,
            Reload = new Packet_ClientReload()
        };
        return p;
    }

    public static Packet_Client Chat(string s, int isTeamchat)
    {
        Packet_ClientMessage p = new()
        {
            Message = s,
            IsTeamchat = isTeamchat
        };
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.Message,
            Message = p
        };
        return pp;
    }

    public static Packet_Client PingReply()
    {
        Packet_ClientPingReply p = new();
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.PingReply,
            PingReply = p
        };
        return pp;
    }

    public static Packet_Client SetBlock(int x, int y, int z, int mode, int type, int materialslot)
    {
        Packet_ClientSetBlock p = new();
        {
            p.X = x;
            p.Y = y;
            p.Z = z;
            p.Mode = mode;
            p.BlockType = type;
            p.MaterialSlot = materialslot;
        }
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.SetBlock,
            SetBlock = p
        };
        return pp;
    }

    public static Packet_Client SpecialKeyRespawn()
    {
        Packet_Client p = new();
        {
            p.Id = Packet_ClientIdEnum.SpecialKey;
            p.SpecialKey_ = new Packet_ClientSpecialKey
            {
                Key_ = Packet_SpecialKeyEnum.Respawn
            };
        }
        return p;
    }

    public static Packet_Client FillArea(int startx, int starty, int startz, int endx, int endy, int endz, int blockType, int ActiveMaterial)
    {
        Packet_ClientFillArea p = new();
        {
            p.X1 = startx;
            p.Y1 = starty;
            p.Z1 = startz;
            p.X2 = endx;
            p.Y2 = endy;
            p.Z2 = endz;
            p.BlockType = blockType;
            p.MaterialSlot = ActiveMaterial;
        }
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.FillArea,
            FillArea = p
        };
        return pp;
    }

    public static Packet_Client InventoryClick(Packet_InventoryPosition pos)
    {
        Packet_ClientInventoryAction p = new()
        {
            A = pos,
            Action = Packet_InventoryActionTypeEnum.Click
        };
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.InventoryAction,
            InventoryAction = p
        };
        return pp;
    }

    public static Packet_Client WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to)
    {
        Packet_ClientInventoryAction p = new()
        {
            A = from,
            B = to,
            Action = Packet_InventoryActionTypeEnum.WearItem
        };
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.InventoryAction,
            InventoryAction = p
        };
        return pp;
    }

    public static Packet_Client MoveToInventory(Packet_InventoryPosition from)
    {
        Packet_ClientInventoryAction p = new()
        {
            A = from,
            Action = Packet_InventoryActionTypeEnum.MoveToInventory
        };
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.InventoryAction,
            InventoryAction = p
        };
        return pp;
    }

    public static Packet_Client Death(int reason, int sourcePlayer)
    {
        Packet_Client p = new()
        {
            Id = Packet_ClientIdEnum.Death,
            Death = new Packet_ClientDeath()
        };
        {
            p.Death.Reason = reason;
            p.Death.SourcePlayer = sourcePlayer;
        }
        return p;
    }

    public static Packet_Client Health(int currentHealth)
    {
        Packet_Client p = new();
        {
            p.Id = Packet_ClientIdEnum.Health;
            p.Health = new Packet_ClientHealth
            {
                CurrentHealth = currentHealth
            };
        }
        return p;
    }

    public static Packet_Client RequestBlob(Game game, string[] required, int requiredCount)
    {
        Packet_ClientRequestBlob p = new(); //{ RequestBlobMd5 = needed };
        if (GameVersionHelper.ServerVersionAtLeast(game.platform, game.serverGameVersion, 2014, 4, 13))
        {
            p.RequestedMd5 = new Packet_StringList();
            p.RequestedMd5.SetItems(required, requiredCount, requiredCount);
        }
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.RequestBlob,
            RequestBlob = p
        };
        return pp;
    }

    public static Packet_Client Leave(int reason)
    {
        Packet_Client p = new()
        {
            Id = Packet_ClientIdEnum.Leave,
            Leave = new Packet_ClientLeave
            {
                Reason = reason
            }
        };
        return p;
    }

    public static Packet_Client Craft(int x, int y, int z, int recipeId)
    {
        Packet_ClientCraft cmd = new()
        {
            X = x,
            Y = y,
            Z = z,
            RecipeId = recipeId
        };
        Packet_Client p = new()
        {
            Id = Packet_ClientIdEnum.Craft,
            Craft = cmd
        };
        return p;
    }

    public static Packet_Client DialogClick(string widgetId, string[] textValues, int textValuesCount)
    {
        Packet_Client p = new()
        {
            Id = Packet_ClientIdEnum.DialogClick,
            DialogClick_ = new Packet_ClientDialogClick
            {
                WidgetId = widgetId
            }
        };
        p.DialogClick_.SetTextBoxValue(textValues, textValuesCount, textValuesCount);
        return p;
    }

    public static Packet_Client GameResolution(int width, int height)
    {
        Packet_ClientGameResolution p = new()
        {
            Width = width,
            Height = height
        };
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.GameResolution,
            GameResolution = p
        };
        return pp;
    }

    public static Packet_Client SpecialKeyTabPlayerList()
    {
        Packet_Client p = new()
        {
            Id = Packet_ClientIdEnum.SpecialKey,
            SpecialKey_ = new Packet_ClientSpecialKey
            {
                Key_ = Packet_SpecialKeyEnum.TabPlayerList
            }
        };
        return p;
    }

    public static Packet_Client SpecialKeySelectTeam()
    {
        Packet_Client p = new();
        {
            p.Id = Packet_ClientIdEnum.SpecialKey;
            p.SpecialKey_ = new Packet_ClientSpecialKey
            {
                Key_ = Packet_SpecialKeyEnum.SelectTeam
            };
        }
        return p;
    }

    public static Packet_Client SpecialKeySetSpawn()
    {
        Packet_Client p = new();
        {
            p.Id = Packet_ClientIdEnum.SpecialKey;
            p.SpecialKey_ = new Packet_ClientSpecialKey
            {
                Key_ = Packet_SpecialKeyEnum.SetSpawn
            };
        }
        return p;
    }

    public static Packet_Client ActiveMaterialSlot(int ActiveMaterial)
    {
        Packet_Client p = new();
        {
            p.Id = Packet_ClientIdEnum.ActiveMaterialSlot;
            p.ActiveMaterialSlot = new Packet_ClientActiveMaterialSlot
            {
                ActiveMaterialSlot = ActiveMaterial
            };
        }
        return p;
    }

    public static Packet_Client MonsterHit(int damage)
    {
        Packet_ClientHealth p = new()
        {
            CurrentHealth = damage
        };
        Packet_Client packet = new()
        {
            Id = Packet_ClientIdEnum.MonsterHit,
            Health = p
        };
        return packet;
    }

    public static Packet_Client PositionAndOrientation(Game game, int playerId, float positionX, float positionY, float positionZ, float orientationX, float orientationY, float orientationZ, byte stance)
    {
        Packet_ClientPositionAndOrientation p = new();
        {
            p.PlayerId = playerId;
            p.X = (int)(positionX * 32);
            p.Y = (int)(positionY * 32);
            p.Z = (int)(positionZ * 32);
            p.Heading = (int)(RadToAngle256(orientationY));
            p.Pitch = (int)(RadToAngle256(orientationX));
            p.Stance = stance;
        }
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.PositionandOrientation,
            PositionAndOrientation = p
        };
        return pp;
    }

    public static Packet_Client ServerQuery()
    {
        Packet_ClientServerQuery p1 = new();
        Packet_Client pp = new()
        {
            Id = Packet_ClientIdEnum.ServerQuery,
            Query = p1
        };
        return pp;
    }

    internal static float RadToAngle256(float value) => value / (2 * MathF.PI) * 255;


    internal static Packet_Client UseEntity(int entityId)
    {
        Packet_Client p = new()
        {
            Id = Packet_ClientIdEnum.EntityInteraction,
            EntityInteraction = new Packet_ClientEntityInteraction
            {
                EntityId = entityId,
                InteractionType = Packet_EntityInteractionTypeEnum.Use
            }
        };
        return p;
    }

    internal static Packet_Client HitEntity(int entityId)
    {
        Packet_Client p = new()
        {
            Id = Packet_ClientIdEnum.EntityInteraction,
            EntityInteraction = new Packet_ClientEntityInteraction
            {
                EntityId = entityId,
                InteractionType = Packet_EntityInteractionTypeEnum.Hit
            }
        };
        return p;
    }
}

public class ServerPackets
{
    public static Packet_Server Message(string text)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.Message,
            Message = new Packet_ServerMessage
            {
                Message = text
            }
        };
        return p;
    }

    public static Packet_Server LevelInitialize()
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.LevelInitialize,
            LevelInitialize = new Packet_ServerLevelInitialize()
        };
        return p;
    }

    public static Packet_Server LevelFinalize()
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.LevelFinalize,
            LevelFinalize = new Packet_ServerLevelFinalize()
        };
        return p;
    }

    public static Packet_Server Identification(int assignedClientId, int mapSizeX, int mapSizeY, int mapSizeZ, string version)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.ServerIdentification,
            Identification = new Packet_ServerIdentification
            {
                AssignedClientId = assignedClientId,
                MapSizeX = mapSizeX,
                MapSizeY = mapSizeY,
                MapSizeZ = mapSizeZ,
                ServerName = "Simple",
                MdProtocolVersion = version
            }
        };
        return p;
    }
    public static byte[] Serialize(Packet_Server packet, out int retLength)
    {
        CitoMemoryStream ms = new();
        Packet_ServerSerializer.Serialize(ms, packet);
        byte[] data = ms.ToArray();
        retLength = ms.Length();
        return data;
    }

    public static Packet_Server BlockType(int id, Packet_BlockType blockType)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.BlockType,
            BlockType = new Packet_ServerBlockType
            {
                Id = id,
                Blocktype = blockType
            }
        };
        return p;
    }

    public static Packet_Server BlockTypes()
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.BlockTypes,
            BlockTypes = new Packet_ServerBlockTypes()
        };
        return p;
    }

    public static Packet_Server Chunk_(int x, int y, int z, int chunksize)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.Chunk_,
            Chunk_ = new Packet_ServerChunk
            {
                X = x,
                Y = y,
                Z = z,
                SizeX = chunksize,
                SizeY = chunksize,
                SizeZ = chunksize
            }
        };
        return p;
    }

    public static Packet_Server ChunkPart(byte[] compressedChunkPart)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.ChunkPart,
            ChunkPart = new Packet_ServerChunkPart
            {
                CompressedChunkPart = compressedChunkPart
            }
        };
        return p;
    }

    internal static Packet_Server SetBlock(int x, int y, int z, int block)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.SetBlock,
            SetBlock = new Packet_ServerSetBlock
            {
                X = x,
                Y = y,
                Z = z,
                BlockType = block
            }
        };
        return p;
    }

    internal static Packet_Server PlayerStats(int health, int maxHealth, int oxygen, int maxOxygen)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.PlayerStats,
            PlayerStats = new Packet_ServerPlayerStats
            {
                CurrentHealth = health,
                MaxHealth = maxHealth,
                CurrentOxygen = oxygen,
                MaxOxygen = maxOxygen
            }
        };
        return p;
    }

    internal static Packet_Server Inventory(Packet_Inventory inventory)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.FiniteInventory,
            Inventory = new Packet_ServerInventory
            {
                Inventory = inventory
            }
        };
        return p;
    }

    internal static Packet_Server Ping()
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.Ping,
            Ping = new Packet_ServerPing()
        };
        return p;
    }

    internal static Packet_Server DisconnectPlayer(string disconnectReason)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.DisconnectPlayer,
            DisconnectPlayer = new Packet_ServerDisconnectPlayer
            {
                DisconnectReason = disconnectReason
            }
        };
        return p;
    }

    internal static Packet_Server AnswerQuery(Packet_ServerQueryAnswer answer)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.QueryAnswer,
            QueryAnswer = answer
        };
        return p;
    }

    internal static Packet_Server EntitySpawn(int id, Packet_ServerEntity entity)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.EntitySpawn,
            EntitySpawn = new Packet_ServerEntitySpawn
            {
                Id = id,
                Entity_ = entity
            }
        };
        return p;
    }

    internal static Packet_Server EntityPositionAndOrientation(int id, Packet_PositionAndOrientation positionAndOrientation)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.EntityPosition,
            EntityPosition = new Packet_ServerEntityPositionAndOrientation
            {
                Id = id,
                PositionAndOrientation = positionAndOrientation
            }
        };
        return p;
    }

    internal static Packet_Server EntityDespawn(int id)
    {
        Packet_Server p = new()
        {
            Id = Packet_ServerIdEnum.EntityDespawn,
            EntityDespawn = new Packet_ServerEntityDespawn
            {
                Id = id
            }
        };
        return p;
    }
}
