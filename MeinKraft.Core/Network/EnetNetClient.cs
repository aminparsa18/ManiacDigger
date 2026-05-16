// ENet implementation of the abstract network layer.
//
// ENet is a reliable UDP library. The platform abstraction owns the actual
// ENet host/peer handles; this layer translates ENet events into the
// NetIncomingMessage model the rest of the game uses.

// ---------------------------------------------------------------------------
// ENet primitives (platform-provided abstractions, unchanged except cleanup)
// ---------------------------------------------------------------------------

// ---------------------------------------------------------------------------
// Client
// ---------------------------------------------------------------------------

public sealed class EnetNetClient : NetClient
{
    private readonly INetworkService _platform;

    private EnetHost? _host;
    private EnetNetConnection? _connection;

    // Two-phase connection: EnetHostConnect fires immediately (connected),
    // but the ENet handshake completes asynchronously (ready to send).
    private bool _connecting;
    private bool _handshakeComplete;

    // Messages that arrived before the caller polled ReadMessage.
    private readonly Queue<NetIncomingMessage> _inbox = new();

    // Outgoing messages queued before the ENet handshake completed.
    private readonly Queue<ReadOnlyMemory<byte>> _pendingSend = new();

    public EnetNetClient(INetworkService platform)
    {
        _platform = platform;
    }

    public override void Start()
    {
        _host = _platform.EnetCreateHost();
        _platform.EnetHostInitialize(_host, null, 1, 0, 0, 0);
    }

    public override NetConnection Connect(string ip, int port)
    {
        EnetPeer peer = _platform.EnetHostConnect(_host!, ip, port, channelCount: 2, data: 0);
        _connection = new EnetNetConnection(_platform, peer);
        _connecting = true;
        return _connection;
    }

    public override NetIncomingMessage? ReadMessage()
    {
        if (!_connecting)
        {
            return null;
        }

        // Flush pending sends now that we know the handshake is complete.
        if (_handshakeComplete)
        {
            while (_pendingSend.TryDequeue(out ReadOnlyMemory<byte> pending))
            {
                _connection!.SendMessage(pending, MyNetDeliveryMethod.ReliableOrdered);
            }
        }

        // Return any already-queued messages before polling ENet again.
        if (_inbox.TryDequeue(out NetIncomingMessage? queued))
        {
            return queued;
        }

        PollEnetEvents();

        _inbox.TryDequeue(out NetIncomingMessage? next);
        return next;
    }

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method)
    {
        if (!_handshakeComplete)
        {
            _pendingSend.Enqueue(payload);
            return;
        }

        _connection!.SendMessage(payload, method);
    }

    private void PollEnetEvents()
    {
        EnetEvent? ev = _platform.EnetHostService(_host!, timeout: 0);
        if (ev is null)
        {
            return;
        }

        do
        {
            HandleEvent(ev);
            ev = _platform.EnetHostCheckEvents(_host!);
        }
        while (ev is not null);
    }

    private void HandleEvent(EnetEvent ev)
    {
        switch (ev.Type())
        {
            case EnetEventType.Connect:
                _handshakeComplete = true;
                break;

            case EnetEventType.Receive:
                EnetPacket packet = ev.Packet();
                ReadOnlyMemory<byte> payload = packet.GetBytes().AsMemory(0, packet.GetBytesCount());
                packet.Dispose();
                _inbox.Enqueue(new NetIncomingMessage
                {
                    Type = NetworkMessageType.Data,
                    Payload = payload,
                    SenderConnection = _connection,
                });
                break;

            case EnetEventType.Disconnect:
                _inbox.Enqueue(new NetIncomingMessage
                {
                    Type = NetworkMessageType.Disconnect,
                    SenderConnection = _connection,
                });
                _handshakeComplete = false;
                _connecting = false;
                break;
        }
    }
}
