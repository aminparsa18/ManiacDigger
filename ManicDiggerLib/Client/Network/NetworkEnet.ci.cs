// ENet implementation of the abstract network layer.
//
// ENet is a reliable UDP library. The platform abstraction owns the actual
// ENet host/peer handles; this layer translates ENet events into the
// NetIncomingMessage model the rest of the game uses.

// ---------------------------------------------------------------------------
// ENet primitives (platform-provided abstractions, unchanged except cleanup)
// ---------------------------------------------------------------------------

using ENet;

public class EnetHost { }

public abstract class EnetEvent
{
    public abstract EnetEventType Type();
    public abstract EnetPeer Peer();
    public abstract EnetPacket Packet();
}

public enum EnetEventType
{
    None,
    Connect,
    Disconnect,
    Receive,
}

/// <summary>
/// ENet packet flag constants. Kept as named constants rather than an enum
/// because ENet itself treats these as a bitmask passed to its C API.
/// </summary>
public static class EnetPacketFlags
{
    public const int None = 0;
    public const int Reliable = 1;
    public const int Unsequenced = 2;
    public const int NoAllocate = 4;
    public const int UnreliableFragment = 8;
}

public abstract class EnetPeer
{
    public abstract int UserData();
    public abstract void SetUserData(int value);
    public abstract IPEndPointCi GetRemoteAddress();
}

public abstract class EnetPacket
{
    public abstract int GetBytesCount();
    public abstract byte[] GetBytes();
    public abstract void Dispose();
}

public class EnetHostNative : EnetHost
{
    public Host host;
}

// ---------------------------------------------------------------------------
// Connection handle
// ---------------------------------------------------------------------------

public sealed class EnetNetConnection : NetConnection
{
    private readonly IGamePlatform _platform;
    internal readonly EnetPeer Peer;

    internal EnetNetConnection(IGamePlatform platform, EnetPeer peer)
    {
        _platform = platform;
        Peer = peer;
    }

    public override IPEndPointCi RemoteEndPoint() =>
        IPEndPointCiDefault.Create(Peer.GetRemoteAddress().AddressToString());

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method, int sequenceChannel = 0)
    {
        int flags = ToEnetFlags(method);
        _platform.EnetPeerSend(Peer, sequenceChannel, payload, flags);
    }

    public override void Update() { }

    public override bool EqualsConnection(NetConnection other) =>
        other is EnetNetConnection e && e.Peer.UserData() == Peer.UserData();

    // CastToEnetNetConnection on IGamePlatform is no longer needed —
    // callers can just cast directly or use pattern matching as above.

    private static int ToEnetFlags(MyNetDeliveryMethod method) => method switch
    {
        MyNetDeliveryMethod.ReliableOrdered
            or MyNetDeliveryMethod.ReliableSequenced
            or MyNetDeliveryMethod.ReliableUnordered => EnetPacketFlags.Reliable,
        MyNetDeliveryMethod.UnreliableSequenced => EnetPacketFlags.Unsequenced,
        _ => EnetPacketFlags.None,
    };
}

// ---------------------------------------------------------------------------
// Client
// ---------------------------------------------------------------------------

public sealed class EnetNetClient : NetClient
{
    private readonly IGamePlatform _platform;

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

    public EnetNetClient(IGamePlatform platform)
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
        EnetPeer peer = _platform.EnetHostConnect(_host!, ip, port, channelCount: 1234, data: 200);
        _connection = new EnetNetConnection(_platform, peer);
        _connecting = true;
        return _connection;
    }

    public override NetIncomingMessage? ReadMessage()
    {
        if (!_connecting)
            return null;

        // Flush pending sends now that we know the handshake is complete.
        if (_handshakeComplete)
        {
            while (_pendingSend.TryDequeue(out ReadOnlyMemory<byte> pending))
                _connection!.SendMessage(pending, MyNetDeliveryMethod.ReliableOrdered);
        }

        // Return any already-queued messages before polling ENet again.
        if (_inbox.TryDequeue(out NetIncomingMessage? queued))
            return queued;

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
            return;

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

// ---------------------------------------------------------------------------
// Server
// ---------------------------------------------------------------------------

public sealed class EnetNetServer : NetServer
{
    private readonly IGamePlatform _platform;

    private EnetHost? _host;
    private int _port;
    private int _nextClientId;

    private readonly Queue<NetIncomingMessage> _inbox = new();

    // Connections keyed by the integer UserData we assigned at connect time.
    // This means every event for the same peer reuses the same connection
    // object rather than allocating a new one per event.
    private readonly Dictionary<int, EnetNetConnection> _connections = new();

    public EnetNetServer(IGamePlatform platform)
    {
        _platform = platform;
    }

    public override void SetPort(int port) => _port = port;

    public override void Start()
    {
        _host = _platform.EnetCreateHost();
        _platform.EnetHostInitializeServer(_host, _port, 256);
    }

    public override NetIncomingMessage? ReadMessage()
    {
        if (_inbox.TryDequeue(out NetIncomingMessage? queued))
            return queued;

        PollEnetEvents();

        _inbox.TryDequeue(out NetIncomingMessage? next);
        return next;
    }

    private void PollEnetEvents()
    {
        EnetEvent? ev = _platform.EnetHostService(_host!, timeout: 0);
        if (ev is null)
            return;

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
                {
                    EnetPeer peer = ev.Peer();
                    int id = _nextClientId++;
                    peer.SetUserData(id);
                    EnetNetConnection conn = new(_platform, peer);
                    _connections[id] = conn;
                    _inbox.Enqueue(new NetIncomingMessage
                    {
                        Type = NetworkMessageType.Connect,
                        SenderConnection = conn,
                    });
                    break;
                }

            case EnetEventType.Receive:
                {
                    EnetPacket packet = ev.Packet();
                    ReadOnlyMemory<byte> payload = packet.GetBytes().AsMemory(0, packet.GetBytesCount());
                    packet.Dispose();
                    EnetNetConnection conn = GetConnection(ev.Peer());
                    _inbox.Enqueue(new NetIncomingMessage
                    {
                        Type = NetworkMessageType.Data,
                        Payload = payload,
                        SenderConnection = conn,
                    });
                    break;
                }

            case EnetEventType.Disconnect:
                {
                    EnetNetConnection conn = GetConnection(ev.Peer());
                    _connections.Remove(conn.Peer.UserData());
                    _inbox.Enqueue(new NetIncomingMessage
                    {
                        Type = NetworkMessageType.Disconnect,
                        SenderConnection = conn,
                    });
                    break;
                }
        }
    }

    private EnetNetConnection GetConnection(EnetPeer peer)
    {
        int id = peer.UserData();
        if (!_connections.TryGetValue(id, out EnetNetConnection? conn))
        {
            // Defensive fallback — should not happen in normal operation.
            conn = new EnetNetConnection(_platform, peer);
            _connections[id] = conn;
        }
        return conn;
    }
}