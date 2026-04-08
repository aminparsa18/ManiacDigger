public partial class Game
{
    internal NetClient main;
    internal bool IsTeamchat;
    internal int ActiveMaterial;
    private int packetLen;

    // -------------------------------------------------------------------------
    // Packet serialization / sending
    // -------------------------------------------------------------------------

    public static byte[] Serialize(Packet_Client packet, out int retLength)
    {
        CitoMemoryStream ms = new();
        Packet_ClientSerializer.Serialize(ms, packet);
        retLength = ms.Length();
        return ms.ToArray();
    }

    public void SendPacket(byte[] packet, int packetLength)
    {
        main.SendMessage(packet.AsMemory(0, packetLength), MyNetDeliveryMethod.ReliableOrdered);
    }

    public void SendPacketClient(Packet_Client packetClient)
    {
        byte[] packet = Serialize(packetClient, out packetLen);
        SendPacket(packet, packetLen);
    }

    // -------------------------------------------------------------------------
    // Game actions → packets
    // -------------------------------------------------------------------------

    internal void SendChat(string s)
    {
        SendPacketClient(ClientPackets.Chat(s, IsTeamchat ? 1 : 0));
    }

    internal void SendPingReply()
    {
        SendPacketClient(ClientPackets.PingReply());
    }

    internal void SendSetBlock(int x, int y, int z, int mode, int type, int materialslot)
    {
        SendPacketClient(ClientPackets.SetBlock(x, y, z, mode, type, materialslot));
    }

    internal void SendFillArea(int startx, int starty, int startz, int endx, int endy, int endz, int blockType)
    {
        SendPacketClient(ClientPackets.FillArea(startx, starty, startz, endx, endy, endz, blockType, ActiveMaterial));
    }

    internal void SendRequestBlob(string[] required, int requiredCount)
    {
        SendPacketClient(ClientPackets.RequestBlob(this, required, requiredCount));
    }

    internal void SendGameResolution()
    {
        SendPacketClient(ClientPackets.GameResolution(Width(), Height()));
    }

    internal void SendLeave(int reason)
    {
        SendPacketClient(ClientPackets.Leave(reason));
    }

    internal void Respawn()
    {
        SendPacketClient(ClientPackets.SpecialKeyRespawn());
        stopPlayerMove = true;
    }

    // -------------------------------------------------------------------------
    // Inventory actions → packets
    // -------------------------------------------------------------------------

    internal void InventoryClick(Packet_InventoryPosition pos)
    {
        SendPacketClient(ClientPackets.InventoryClick(pos));
    }

    internal void WearItem(Packet_InventoryPosition from, Packet_InventoryPosition to)
    {
        SendPacketClient(ClientPackets.WearItem(from, to));
    }

    internal void MoveToInventory(Packet_InventoryPosition from)
    {
        SendPacketClient(ClientPackets.MoveToInventory(from));
    }

    // -------------------------------------------------------------------------
    // Connection
    // -------------------------------------------------------------------------

    internal void Connect__()
    {
        if (string.IsNullOrEmpty(connectdata.ServerPassword))
            Connect(connectdata.Ip, connectdata.Port, connectdata.Username, connectdata.Auth);
        else
            Connect_(connectdata.Ip, connectdata.Port, connectdata.Username, connectdata.Auth, connectdata.ServerPassword);

        MapLoadingStart();
    }

    internal void Connect(string serverAddress, int port, string username, string auth)
    {
        main.Start();
        main.Connect(serverAddress, port);
        SendPacketClient(ClientPackets.CreateLoginPacket(platform, username, auth));
    }

    internal void Connect_(string serverAddress, int port, string username, string auth, string serverPassword)
    {
        main.Start();
        main.Connect(serverAddress, port);
        SendPacketClient(ClientPackets.CreateLoginPacket_(platform, username, auth, serverPassword));
    }

    internal void Reconnect()
    {
        reconnect = true;
    }
}