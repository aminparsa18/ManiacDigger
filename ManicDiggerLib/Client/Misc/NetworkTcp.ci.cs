public class TcpNetClient : NetClient
{
    public TcpNetClient()
    {
        incoming = new QueueByte();
        data = new byte[dataLength];
        connected = new BoolRef();
    }
    internal GamePlatform platform;
    public override void Start()
    {
        tosend = new();
    }

    private readonly BoolRef connected;

    public override NetConnection Connect(string ip, int port)
    {
        platform.TcpConnect(ip, port, connected);
        return null;
    }

    private readonly QueueByte incoming;
    public const int MaxPacketLength = 1024 * 4;

    private readonly byte[] data;
    private const int dataLength = 1024;
    public override NetIncomingMessage ReadMessage()
    {
        if (connected.value)
        {
            while (tosend.Count() > 0)
            {
                INetOutgoingMessage msg = tosend.Dequeue();
                DoSendPacket(msg);
            }
        }
        NetIncomingMessage message = GetMessage();
        if (message != null)
        {
            return message;
        }

        for (int k = 0; k < 1; k++)
        {
            int received = platform.TcpReceive(data, dataLength);
            if (received <= 0)
            {
                break;
            }
            for (int i = 0; i < received; i++)
            {
                incoming.Enqueue(data[i]);
            }
        }

        message = GetMessage();
        if (message != null)
        {
            return message;
        }

        return null;
    }

    private NetIncomingMessage GetMessage()
    {
        if (incoming.count >= 4)
        {
            byte[] length = new byte[4];
            incoming.PeekRange(length, 4);
            int messageLength = ReadInt(length, 0);
            if (incoming.count >= 4 + messageLength)
            {
                incoming.DequeueRange(new byte[4], 4);
                NetIncomingMessage msg = new()
                {
                    message = new byte[messageLength],
                    messageLength = messageLength
                };
                incoming.DequeueRange(msg.message, msg.messageLength);
                return msg;
            }
        }
        return null;
    }

    private static void WriteInt(byte[] writeBuf, int writePos, int n)
    {
        int a = (n >> 24) & 0xFF;
        int b = (n >> 16) & 0xFF;
        int c = (n >> 8) & 0xFF;
        int d = n & 0xFF;
        writeBuf[writePos] = Game.IntToByte(a);
        writeBuf[writePos + 1] = Game.IntToByte(b);
        writeBuf[writePos + 2] = Game.IntToByte(c);
        writeBuf[writePos + 3] = Game.IntToByte(d);
    }

    private static int ReadInt(byte[] readBuf, int readPos)
    {
        int n = readBuf[readPos] << 24;
        n |= readBuf[readPos + 1] << 16;
        n |= readBuf[readPos + 2] << 8;
        n |= readBuf[readPos + 3];
        return n;
    }

    private void DoSendPacket(INetOutgoingMessage msg)
    {
        byte[] packet = new byte[msg.messageLength + 4];
        WriteInt(packet, 0, msg.messageLength);
        for (int i = 0; i < msg.messageLength; i++)
        {
            packet[i + 4] = msg.message[i];
        }
        platform.TcpSend(packet, msg.messageLength + 4);
    }

    private Queue<INetOutgoingMessage> tosend;
    public override void SendMessage(INetOutgoingMessage message, MyNetDeliveryMethod method)
    {
        INetOutgoingMessage msg = message;
        if (!connected.value)
        {
            tosend.Enqueue(msg);
            return;
        }
        DoSendPacket(msg);
    }

    public void SetPlatform(GamePlatform platform_)
    {
        platform = platform_;
    }
}

