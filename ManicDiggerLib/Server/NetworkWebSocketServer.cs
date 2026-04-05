using WebSocketSharp;
using WebSocketSharp.Server;

public class WebSocketNetServer : NetServer
{
    public WebSocketNetServer()
    {
        incoming = new QueueNetIncomingMessage();
        singleton = this;
    }

    private WebSocketServer server;

    public static WebSocketNetServer singleton;

    public override void Start()
    {
        server = new WebSocketServer(Port);
        server.AddWebSocketService<WebSocketGameServer>("/Game");
        server.Start();
        if (server.IsListening)
        {
            Console.WriteLine("Listening on port {0}, and providing WebSocket services:", server.Port);
            foreach (var path in server.WebSocketServices.Paths)
                Console.WriteLine("- {0}", path);
        }
    }

    public override NetIncomingMessage ReadMessage()
    {
        lock (incoming)
        {
            if (incoming.Count() != 0)
            {
                return incoming.Dequeue();
            }
        }
        return null;
    }

    internal QueueNetIncomingMessage incoming;

    private int Port;

    public override void SetPort(int port)
    {
        Port = port;
    }
}

public class WebSocketConnection : NetConnection
{
    internal WebSocketGameServer? server;

    public override IPEndPointCi RemoteEndPoint()
    {
        try
        {
            return IPEndPointCiDefault.Create(server.Context.UserEndPoint.Address.ToString());
        }
        catch
        {
            return IPEndPointCiDefault.Create("unknown");
        }
    }

    public override void SendMessage(INetOutgoingMessage msg, MyNetDeliveryMethod method, int sequenceChannel)
    {
        byte[] message = new byte[msg.messageLength];
        for (int i = 0; i < msg.messageLength; i++)
        {
            message[i] = msg.message[i];
        }
        server.Send1(message);
    }

    public override void Update()
    {
    }

    public override bool EqualsConnection(NetConnection connection)
    {
        var a = this;
        var b = (WebSocketConnection)connection;
        return a.server == b.server;
    }
}

public class WebSocketGameServer : WebSocketBehavior
{
    public WebSocketGameServer()
    {
        IgnoreExtensions = true;
        connection = new WebSocketConnection
        {
            server = this
        };
    }
    private readonly WebSocketConnection connection;

    protected override void OnOpen()
    {
        NetIncomingMessage m = new()
        {
            Type = NetworkMessageType.Connect,
            SenderConnection = connection
        };
        Enqueue(m);
    }

    protected override void OnMessage(WebSocketSharp.MessageEventArgs e)
    {
        NetIncomingMessage m = new()
        {
            message = e.RawData,
            messageLength = e.RawData.Length,
            Type = NetworkMessageType.Data,
            SenderConnection = connection
        };
        Enqueue(m);
    }

    protected override void OnClose(CloseEventArgs e)
    {
        NetIncomingMessage m = new()
        {
            Type = NetworkMessageType.Disconnect,
            SenderConnection = connection
        };
        Enqueue(m);
    }

    private static void Enqueue(NetIncomingMessage m)
    {
        lock (WebSocketNetServer.singleton.incoming)
        {
            WebSocketNetServer.singleton.incoming.Enqueue(m);
        }
    }

    private readonly Queue<byte[]> toSend = new();
    private bool isSending;
    private readonly object sendLock = new();
    public void Send1(byte[] data)
    {
        lock (sendLock)
        {
            if (isSending)
            {
                toSend.Enqueue(data);
            }
            else
            {
                isSending = true;
                SendAsync(data, F);
            }
        }
    }

    private void F(bool completed)
    {
        lock (sendLock)
        {
            if (toSend.Count > 0)
            {
                byte[] data = toSend.Dequeue();
                SendAsync(data, F);
            }
            else
            {
                isSending = false;
            }
        }
    }
}
