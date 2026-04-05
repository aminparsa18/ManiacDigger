using System.Net.Sockets;
using System.Net;

public class TcpNetServer : NetServer
{
    public TcpNetServer()
    {
        messages = new Queue<NetIncomingMessage>();
        server = new ServerManager();
    }

    public override void Start()
    {
        server.StartServer(Port);
        server.Connected += new EventHandler<ConnectionEventArgs>(ServerConnected);
        server.ReceivedMessage += new EventHandler<MessageEventArgs>(ServerReceivedMessage);
        server.Disconnected += new EventHandler<ConnectionEventArgs>(ServerDisconnected);
    }

    private void ServerConnected(object? sender, ConnectionEventArgs e)
    {
        NetIncomingMessage msg = new()
        {
            Type = NetworkMessageType.Connect,
            SenderConnection = new TcpNetConnection() { peer = e.ClientId }
        };
        lock (messages)
        {
            messages.Enqueue(msg);
        }
    }

    private void ServerDisconnected(object? sender, ConnectionEventArgs e)
    {
        NetIncomingMessage msg = new()
        {
            Type = NetworkMessageType.Disconnect,
            SenderConnection = new TcpNetConnection() { peer = e.ClientId }
        };
        lock (messages)
        {
            messages.Enqueue(msg);
        }
    }

    private void ServerReceivedMessage(object? sender, MessageEventArgs e)
    {
        NetIncomingMessage msg = new()
        {
            Type = NetworkMessageType.Data,
            message = e.data,
            messageLength = e.data.Length,
            SenderConnection = new TcpNetConnection() { peer = e.ClientId }
        };
        lock (messages)
        {
            messages.Enqueue(msg);
        }
    }

    private readonly ServerManager server;

    public override NetIncomingMessage ReadMessage()
    {
        lock (messages)
        {
            if (messages.Count > 0)
            {
                return messages.Dequeue();
            }
        }

        return null;
    }
    private readonly Queue<NetIncomingMessage> messages;

    private int Port;

    public override void SetPort(int port)
    {
        Port = port;
    }
}

public class TcpNetConnection : NetConnection
{
    public TcpConnection peer;

    public override IPEndPointCi RemoteEndPoint()
    {
        return IPEndPointCiDefault.Create(peer.address);
    }

    public override void SendMessage(INetOutgoingMessage msg, MyNetDeliveryMethod method, int sequenceChannel)
    {
        INetOutgoingMessage msg1 = (INetOutgoingMessage)msg;
        byte[] data = new byte[msg1.messageLength];
        for (int i = 0; i < msg1.messageLength; i++)
        {
            data[i] = msg1.message[i];
        }
        peer.Send(data);
    }

    public override void Update()
    {
    }

    public override bool EqualsConnection(NetConnection connection)
    {
        return peer.sock == ((TcpNetConnection)connection).peer.sock;
    }
}

public class ServerManager
{
    private Socket sock;
    private readonly IPAddress addr = IPAddress.Any;
    public void StartServer(int port)
    {
        this.sock = new Socket(
            addr.AddressFamily,
            SocketType.Stream,
            ProtocolType.Tcp);
        sock.NoDelay = true;
        sock.Bind(new IPEndPoint(this.addr, port));
        this.sock.Listen(10);
        this.sock.BeginAccept(this.OnConnectRequest, sock);
    }

    private void OnConnectRequest(IAsyncResult result)
    {
        try
        {
            Socket sock = (Socket)result.AsyncState;

            TcpConnection newConn = new(sock.EndAccept(result));
            newConn.ReceivedMessage += new EventHandler<MessageEventArgs>(NewConnReceivedMessage);
            newConn.Disconnected += new EventHandler<ConnectionEventArgs>(NewConnDisconnected);
            sock.BeginAccept(this.OnConnectRequest, sock);
        }
        catch
        {
        }
    }

    private void NewConnDisconnected(object sender, ConnectionEventArgs e)
    {
        try
        {
            Disconnected(sender, e);
        }
        catch //(Exception ex)
        {
            // Console.WriteLine(ex.ToString());
        }
    }

    private void NewConnReceivedMessage(object sender, MessageEventArgs e)
    {
        try
        {
            if (Connected != null)
            {
                TcpConnection sender_ = (TcpConnection)sender;
                if (!sender_.connected)
                {
                    sender_.connected = true;
                    Connected(this, new ConnectionEventArgs() { ClientId = sender_ });
                }
            }
            ReceivedMessage(sender, e);
        }
        catch //(Exception ex)
        {
            // Console.WriteLine(ex.ToString());
        }
    }

    public event EventHandler<ConnectionEventArgs>? Connected;
    public event EventHandler<MessageEventArgs>? ReceivedMessage;
    public event EventHandler<ConnectionEventArgs>? Disconnected;

    public static void Send(object sender, byte[] data)
    {
        try
        {
            ((TcpConnection)sender).Send(data);
        }
        catch
        {
        }
    }
}

public class TcpConnection
{
    public Socket sock;
    public string address;

    public TcpConnection(Socket s)
    {
        this.sock = s;
        address = s.RemoteEndPoint.ToString();
        this.BeginReceive();
    }

    private void BeginReceive()
    {
        try
        {
            this.sock.BeginReceive(
                    this.dataRcvBuf, 0,
                    this.dataRcvBuf.Length,
                    SocketFlags.None,
                    new AsyncCallback(this.OnBytesReceived),
                    this);
        }
        catch
        {
            InvokeDisconnected();
        }
    }

    public bool connected;
    private readonly byte[] dataRcvBuf = new byte[1024 * 8];
    protected void OnBytesReceived(IAsyncResult result)
    {
        try
        {
            int nBytesRec;
            try
            {
                nBytesRec = this.sock.EndReceive(result);
            }
            catch
            {
                try
                {
                    this.sock.Close();
                }
                catch
                {
                }
                InvokeDisconnected();
                return;
            }
            if (nBytesRec <= 0)
            {
                try
                {
                    this.sock.Close();
                }
                catch
                {
                }
                InvokeDisconnected();
                return;
            }

            for (int i = 0; i < nBytesRec; i++)
            {
                receivedBytes.Add(dataRcvBuf[i]);
            }

            //packetize
            while (receivedBytes.Count >= 4)
            {
                byte[] receivedBytesArray = receivedBytes.ToArray();
                int packetLength = ReadInt(receivedBytesArray, 0);
                if (receivedBytes.Count >= 4 + packetLength)
                {
                    //read packet
                    byte[] packet = new byte[packetLength];
                    for (int i = 0; i < packetLength; i++)
                    {
                        packet[i] = receivedBytesArray[4 + i];
                    }
                    receivedBytes.RemoveRange(0, 4 + packetLength);
                    ReceivedMessage.Invoke(this, new MessageEventArgs() { ClientId = this, data = packet });
                }
                else
                {
                    break;
                }
            }

            this.sock.BeginReceive(
                this.dataRcvBuf, 0,
                this.dataRcvBuf.Length,
                SocketFlags.None,
                new AsyncCallback(this.OnBytesReceived),
                this);
        }
        catch
        {
            InvokeDisconnected();
        }
    }

    private void InvokeDisconnected()
    {
        if (Disconnected != null)
        {
            if (sock != null)
            {
                sock.Close();
                sock = null;
                if (connected)
                {
                    Disconnected(null, new ConnectionEventArgs() { ClientId = this });
                }
            }
        }
    }

    public void Send(byte[] data)
    {
        try
        {
            int length = data.Length;
            byte[] data2 = new byte[length + 4];
            WriteInt(data2, 0, length);
            for (int i = 0; i < length; i++)
            {
                data2[4 + i] = data[i];
            }
            sock.BeginSend(data2, 0, data2.Length, SocketFlags.None, new AsyncCallback(OnSend), null);
        }
        catch
        {
            InvokeDisconnected();
        }
    }

    private void OnSend(IAsyncResult result)
    {
        try
        {
            sock.EndSend(result);
        }
        catch
        {
            InvokeDisconnected();
        }
    }

    private readonly List<byte> receivedBytes = [];
    public event EventHandler<MessageEventArgs> ReceivedMessage;
    public event EventHandler<ConnectionEventArgs> Disconnected;

    private static void WriteInt(byte[] writeBuf, int writePos, int n)
    {
        int a = (n >> 24) & 0xFF;
        int b = (n >> 16) & 0xFF;
        int c = (n >> 8) & 0xFF;
        int d = n & 0xFF;
        writeBuf[writePos] = (byte)(a);
        writeBuf[writePos + 1] = (byte)(b);
        writeBuf[writePos + 2] = (byte)(c);
        writeBuf[writePos + 3] = (byte)(d);
    }

    private static int ReadInt(byte[] readBuf, int readPos)
    {
        int n = readBuf[readPos] << 24;
        n |= readBuf[readPos + 1] << 16;
        n |= readBuf[readPos + 2] << 8;
        n |= readBuf[readPos + 3];
        return n;
    }

    public override string ToString()
    {
        if (address != null)
        {
            return address.ToString();
        }
        return base.ToString();
    }
}

public class ConnectionEventArgs : EventArgs
{
    public TcpConnection? ClientId;
}

public class MessageEventArgs : EventArgs
{
    public TcpConnection? ClientId;
    public byte[]? data;
}
