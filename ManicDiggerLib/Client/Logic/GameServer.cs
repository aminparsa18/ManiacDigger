public partial class Game
{
    internal Packet_ServerRedirect redirectTo;

    // -------------------------------------------------------------------------
    // Server identification
    // -------------------------------------------------------------------------

    internal void ProcessServerIdentification(Packet_Server packet)
    {
        LocalPlayerId = packet.Identification.AssignedClientId;
        ServerInfo.connectdata = connectdata;
        ServerInfo.ServerName = packet.Identification.ServerName;
        ServerInfo.ServerMotd = packet.Identification.ServerMotd;
        d_TerrainChunkTesselator.ENABLE_TEXTURE_TILING = packet.Identification.RenderHint_ == RenderHintEnum.Fast;

        Packet_StringList requiredMd5 = packet.Identification.RequiredBlobMd5;
        Packet_StringList requiredName = packet.Identification.RequiredBlobName;

        ChatLog("[GAME] Processed server identification");

        int getCount = 0;
        if (requiredMd5 != null)
        {
            ChatLog(string.Format("[GAME] Server has {0} assets", requiredMd5.ItemsCount));
            for (int i = 0; i < requiredMd5.ItemsCount; i++)
            {
                string md5 = requiredMd5.Items[i];
                if (platform.IsCached(md5))
                {
                    Asset cachedAsset = platform.LoadAssetFromCache(md5);
                    string name = requiredName != null ? requiredName.Items[i] : cachedAsset.name;
                    SetFile(name, cachedAsset.md5, cachedAsset.data, cachedAsset.dataLength);
                }
                else
                {
                    if (requiredName != null)
                    {
                        if (!HasAsset(md5, requiredName.Items[i]))
                            getAsset[getCount++] = md5;
                    }
                    else
                    {
                        getAsset[getCount++] = md5;
                    }
                }
            }
            ChatLog(string.Format("[GAME] Will download {0} missing assets", getCount));
        }

        SendGameResolution();
        ChatLog("[GAME] Sent window resolution to server");
        sendResize = true;

        SendRequestBlob(getAsset, getCount);
        ChatLog("[GAME] Sent BLOB request");

        if (packet.Identification.MapSizeX != map.MapSizeX
            || packet.Identification.MapSizeY != map.MapSizeY
            || packet.Identification.MapSizeZ != map.MapSizeZ)
        {
            map.Reset(packet.Identification.MapSizeX,
                packet.Identification.MapSizeY,
                packet.Identification.MapSizeZ);
            d_Heightmap.Restart();
        }

        shadowssimple = packet.Identification.DisableShadows == 1;
        maxdrawdistance = 256;
        ChatLog("[GAME] Map initialized");
    }

    internal void InvalidVersionAllow()
    {
        if (invalidVersionDrawMessage != null)
        {
            invalidVersionDrawMessage = null;
            ProcessServerIdentification(invalidVersionPacketIdentification);
            invalidVersionPacketIdentification = null;
        }
    }

    // -------------------------------------------------------------------------
    // Server redirect / exit
    // -------------------------------------------------------------------------

    internal void ExitAndSwitchServer(Packet_ServerRedirect newServer)
    {
        if (issingleplayer)
            platform.SinglePlayerServerExit();

        redirectTo = newServer;
        exitToMainMenu = true;
    }

    internal void ExitToMainMenu_()
    {
        if (issingleplayer)
            platform.SinglePlayerServerExit();

        redirectTo = null;
        exitToMainMenu = true;
    }

    internal Packet_ServerRedirect GetRedirect() => redirectTo;

    // -------------------------------------------------------------------------
    // Chat log
    // -------------------------------------------------------------------------

    internal void ChatLog(string p)
    {
        if (!platform.ChatLog(ServerInfo.ServerName, p))
            platform.ConsoleWriteLine(string.Format(language.CannotWriteChatLog(), ServerInfo.ServerName));
    }
}