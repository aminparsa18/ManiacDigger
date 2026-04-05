public class ServerSystemNotifyEntities : ServerSystem
{
    private readonly int PlayerPositionUpdatesPerSecond = 10;
    private readonly int EntityPositionUpdatesPerSecond = 10;
    private readonly int SpawnMaxEntities = 32;

    public override void Update(Server server, float dt)
    {
        foreach (var k in server.clients)
        {
            if (k.Value.IsBot)
            {
                //Apply position overrides (to update bot positions)
                if (k.Value.positionOverride == null)
                {
                    continue;
                }
                else
                {
                    k.Value.entity.position = k.Value.positionOverride;
                    k.Value.positionOverride = null;
                }
                //Bots don't need to be sent other packets with other player's positions
                continue;
            }
            NotifyPlayers(server, k.Key);
            NotifyPlayerPositions(server, k.Key, dt);
            NotifyEntities(server, k.Key, dt);
        }
    }

    private static void NotifyPlayers(Server server, int clientid)
    {
        ClientOnServer c = server.clients[clientid];
        // EntitySpawn
        foreach (var k in server.clients)
        {
            if (k.Value.state != ClientStateOnServer.Playing)
            {
                continue;
            }
            if (c.playersDirty[k.Key])
            {
                Packet_ServerEntity e = ToNetworkEntity(server.serverPlatform, k.Value.entity);
                server.SendPacket(clientid, ServerPackets.EntitySpawn(k.Key, e));
                c.playersDirty[k.Key] = false;
            }
        }
    }

    // EntityPositionAndOrientation
    private void NotifyPlayerPositions(Server server, int clientid, float dt)
    {
        ClientOnServer c = server.clients[clientid];
        c.notifyPlayerPositionsAccum += dt;
        if (c.notifyPlayerPositionsAccum < (one / PlayerPositionUpdatesPerSecond))
        {
            return;
        }
        c.notifyPlayerPositionsAccum = 0;
        foreach (var k in server.clients)
        {
            if (k.Value.state != ClientStateOnServer.Playing)
            {
                continue;
            }
            if (!c.IsSpectator && k.Value.IsSpectator)
            {
                // Do not send position updates for spectating players if player is not spectator himself
                continue;
            }
            if (k.Key == clientid)
            {
                if (k.Value.positionOverride == null)
                {
                    continue;
                }
                else
                {
                    k.Value.entity.position = k.Value.positionOverride;
                    k.Value.positionOverride = null;
                }
            }
            else
            {
                if (Server.DistanceSquared(Server.PlayerBlockPosition(server.clients[k.Key]), Server.PlayerBlockPosition(server.clients[clientid])) > server.config.PlayerDrawDistance * server.config.PlayerDrawDistance)
                {
                    continue;
                }
            }
            Packet_PositionAndOrientation position = ToNetworkEntityPosition(server.serverPlatform, server.clients[k.Key].entity.position);
            server.SendPacket(clientid, ServerPackets.EntityPositionAndOrientation(k.Key, position));
        }
    }

    private void NotifyEntities(Server server, int clientid, float dt)
    {
        ClientOnServer c = server.clients[clientid];
        c.notifyEntitiesAccum += dt;
        if (c.notifyEntitiesAccum < (one / EntityPositionUpdatesPerSecond))
        {
            return;
        }
        c.notifyEntitiesAccum = 0;
        
        // find nearest entities
        int max = SpawnMaxEntities;
        ServerEntityId[] nearestEntities = new ServerEntityId[max];
        FindNearEntities(server, c, max, nearestEntities);

        // update entities
        for (int i = 0; i < max; i++)
        {
            ServerEntityId e = nearestEntities[i];
            if (e == null) { continue; }
            for (int k = 0; k < server.modEventHandlers.onupdateentity.Count; k++)
            {
                server.modEventHandlers.onupdateentity[k](e.chunkx, e.chunky, e.chunkz, e.id);
            }
        }

        // despawn old entities
        for (int i = 0; i < c.spawnedEntitiesCount; i++)
        {
            ServerEntityId e = c.spawnedEntities[i];
            if (e == null) { continue; }
            if (!Contains(nearestEntities, max, e))
            {
                int onClientId = i;
                c.spawnedEntities[onClientId] = null;
                server.SendPacket(clientid, ServerPackets.EntityDespawn(64 + onClientId));
            }
        }

        // spawn new entities
        for (int i = 0; i < max; i++)
        {
            ServerEntityId e = nearestEntities[i];
            if (e == null) { continue; }
            if (!Contains(c.spawnedEntities, max, e))
            {
                int onClientId = IndexOfNull(c.spawnedEntities, c.spawnedEntitiesCount);
                c.spawnedEntities[onClientId] = e.Clone();
                ServerChunk chunk = server.d_Map.GetChunk(e.chunkx * Server.chunksize, e.chunky * Server.chunksize, e.chunkz * Server.chunksize);
                ServerEntity ee = chunk.Entities[e.id];
                Packet_ServerEntity ne = ToNetworkEntity(server.serverPlatform, ee);
                server.SendPacket(clientid, ServerPackets.EntitySpawn(64 + onClientId, ne));
            }
        }

        for (int i = 0; i < max; i++)
        {
            if (c.updateEntity[i])
            {
                c.updateEntity[i] = false;
                ServerEntityId e = c.spawnedEntities[i];
                ServerChunk chunk = server.d_Map.GetChunk(e.chunkx * Server.chunksize, e.chunky * Server.chunksize, e.chunkz * Server.chunksize);
                ServerEntity ee = chunk.Entities[e.id];
                Packet_ServerEntity ne = ToNetworkEntity(server.serverPlatform, ee);
                server.SendPacket(clientid, ServerPackets.EntitySpawn(64 + i, ne));
            }
        }
    }

    private static int IndexOfNull(ServerEntityId[] list, int listCount)
    {
        for (int i = 0; i < listCount; i++)
        {
            ServerEntityId s = list[i];
            if (s == null)
            {
                return i;
            }
        }
        return -1;
    }

    private static bool Contains(ServerEntityId[] list, int listCount, ServerEntityId value)
    {
        for (int i = 0; i < listCount; i++)
        {
            ServerEntityId s = list[i];
            if (s == null)
            {
                continue;
            }
            if (s.chunkx == value.chunkx
                && s.chunky == value.chunky
                && s.chunkz == value.chunkz
                && s.id == value.id)
            {
                return true;
            }
        }
        return false;
    }

    private static void FindNearEntities(Server server, ClientOnServer c, int maxCount, ServerEntityId[] ret)
    {
        List<ServerEntityId> list = [];
        int playerx = c.PositionMul32GlX / 32;
        int playery = c.PositionMul32GlZ / 32;
        int playerz = c.PositionMul32GlY / 32;
        // Find all entities in 3x3x3 chunks around player.
        for (int xx = -1; xx < 2; xx++)
        {
            for (int yy = -1; yy < 2; yy++)
            {
                for (int zz = -1; zz < 2; zz++)
                {
                    int chunkx = playerx / Server.chunksize + xx;
                    int chunky = playery / Server.chunksize + yy;
                    int chunkz = playerz / Server.chunksize + zz;
                    if (!MapUtil.IsValidChunkPos(server.d_Map, chunkx, chunky, chunkz, Server.chunksize))
                    {
                        continue;
                    }
                    ServerChunk chunk = server.d_Map.GetChunk(chunkx * Server.chunksize, chunky * Server.chunksize, chunkz * Server.chunksize);
                    if (chunk == null)
                    {
                        continue;
                    }
                    if (chunk.Entities == null)
                    {
                        continue;
                    }
                    for (int i = 0; i < chunk.EntitiesCount; i++)
                    {
                        if (chunk.Entities[i] == null)
                        {
                            continue;
                        }
                        if (chunk.Entities[i].position == null)
                        {
                            continue;
                        }
                        ServerEntityId id = new()
                        {
                            chunkx = chunkx,
                            chunky = chunky,
                            chunkz = chunkz,
                            id = i
                        };
                        list.Add(id);
                    }
                }
            }
        }
        // Return maxCount of entities nearest to player.

        list.Sort((a, b) =>
        {
            var entityA = server.d_Map.GetChunk(a.chunkx * Server.chunksize, a.chunky * Server.chunksize, a.chunkz * Server.chunksize).Entities[a.id];
            var entityB = server.d_Map.GetChunk(b.chunkx * Server.chunksize, b.chunky * Server.chunksize, b.chunkz * Server.chunksize).Entities[b.id];

            Vector3i posA = new((int)entityA.position.x, (int)entityA.position.y, (int)entityA.position.z);
            Vector3i posB = new((int)entityB.position.x, (int)entityB.position.y, (int)entityB.position.z);
            Vector3i posPlayer = new(c.PositionMul32GlX / 32, c.PositionMul32GlY / 32, c.PositionMul32GlZ / 32);
            return Server.DistanceSquared(posA, posPlayer).CompareTo(Server.DistanceSquared(posB, posPlayer));
        }
        );

        int retCount = maxCount;
        if (list.Count < maxCount)
        {
            retCount = list.Count;
        }
        for (int i = 0; i < retCount; i++)
        {
            ret[i] = list[i];
        }
    }

    private static Packet_PositionAndOrientation ToNetworkEntityPosition(ServerPlatform platform, ServerEntityPositionAndOrientation position)
    {
        Packet_PositionAndOrientation p = new()
        {
            X = platform.FloatToInt(position.x * 32),
            Y = platform.FloatToInt(position.y * 32),
            Z = platform.FloatToInt(position.z * 32),
            Heading = position.heading,
            Pitch = position.pitch,
            Stance = position.stance
        };
        return p;
    }

    private static Packet_ServerEntity ToNetworkEntity(ServerPlatform platform, ServerEntity entity)
    {
        Packet_ServerEntity p = new();
        if (entity.position != null)
        {
            p.Position = ToNetworkEntityPosition(platform, entity.position);
        }
        if (entity.drawModel != null)
        {
            p.DrawModel = new Packet_ServerEntityAnimatedModel
            {
                EyeHeight = platform.FloatToInt(entity.drawModel.eyeHeight * 32),
                Model_ = entity.drawModel.model,
                ModelHeight = platform.FloatToInt(entity.drawModel.modelHeight * 32),
                Texture_ = entity.drawModel.texture,
                DownloadSkin = entity.drawModel.downloadSkin ? 1 : 0
            };
        }
        if (entity.drawName != null)
        {
            p.DrawName_ = new Packet_ServerEntityDrawName
            {
                Name = entity.drawName.name,
                Color = entity.drawName.color,
                OnlyWhenSelected = entity.drawName.onlyWhenSelected,
                ClientAutoComplete = entity.drawName.clientAutoComplete
            };
        }
        if (entity.drawText != null)
        {
            p.DrawText = new Packet_ServerEntityDrawText
            {
                Dx = platform.FloatToInt(entity.drawText.dx * 32),
                Dy = platform.FloatToInt(entity.drawText.dy * 32),
                Dz = platform.FloatToInt(entity.drawText.dz * 32),
                Rotx = platform.FloatToInt(entity.drawText.rotx),
                Roty = platform.FloatToInt(entity.drawText.roty),
                Rotz = platform.FloatToInt(entity.drawText.rotz),
                Text = entity.drawText.text
            };
        }
        if (entity.push != null)
        {
            p.Push = new Packet_ServerEntityPush
            {
                RangeFloat = platform.FloatToInt(entity.push.range * 32)
            };
        }
        p.Usable = entity.usable;
        if (entity.drawArea != null)
        {
            p.DrawArea = new Packet_ServerEntityDrawArea
            {
                X = entity.drawArea.x,
                Y = entity.drawArea.y,
                Z = entity.drawArea.z,
                Sizex = entity.drawArea.sizex,
                Sizey = entity.drawArea.sizey,
                Sizez = entity.drawArea.sizez,
                VisibleToClientId = entity.drawArea.visibleToClientId
            };
        }

        return p;
    }
}
