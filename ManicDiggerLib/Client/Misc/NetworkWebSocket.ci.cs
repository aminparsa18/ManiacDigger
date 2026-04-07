public class WebSocketClient : NetClient
{
    public WebSocketClient()
    {
        incomingData = new byte[incomingDataMax];
    }

    public override void Start()
    {
        c = new WebSocketClientConnection();
    }

    public override NetConnection Connect(string ip, int port)
    {
        p.WebSocketConnect(ip, port);
        c.address = string.Format("{0}:{1}", ip, port.ToString());
        c.platform = p;
        return c;
    }

    private WebSocketClientConnection c;

    private readonly byte[] incomingData;
    private const int incomingDataMax = 16 * 1024;

    public override NetIncomingMessage ReadMessage()
    {
        int received = p.WebSocketReceive(incomingData, incomingDataMax);
        if (received == -1)
        {
            return null;
        }
        else
        {
            NetIncomingMessage msg = new()
            {
                message = incomingData,
                messageLength = received,
                SenderConnection = c
            };
            return msg;
        }
    }

    public override void SendMessage(INetOutgoingMessage message, MyNetDeliveryMethod method)
    {
        c.SendMessage(message, method, 0);
    }

    private GamePlatform p;

    internal void SetPlatform(GamePlatform platform)
    {
        p = platform;
    }
}

public class WebSocketClientConnection : NetConnection
{
    internal string address;
    internal GamePlatform platform;

    public override IPEndPointCi RemoteEndPoint()
    {
        return IPEndPointCiDefault.Create(address);
    }

    public override void SendMessage(INetOutgoingMessage msg, MyNetDeliveryMethod method, int sequenceChannel)
    {
        platform.WebSocketSend(msg.message, msg.messageLength);
    }

    public override void Update()
    {
    }

    public override bool EqualsConnection(NetConnection connection)
    {
        return true;
    }
}
