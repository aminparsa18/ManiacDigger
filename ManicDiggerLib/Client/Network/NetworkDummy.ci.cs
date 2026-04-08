// in-process network implementation for single-player.
//
// Instead of real sockets, a DummyNetwork holds two concurrent queues:
//   ServerInbox  — client writes here, server reads from here
//   ClientInbox  — server writes here, client reads from here
//
// Both sides live in the same process on different threads, so the queues
// are protected with standard lock(). No MonitorObject, no manual Enter/Exit.

using System.Collections.Concurrent;

// ---------------------------------------------------------------------------
// Shared in-process channel
// ---------------------------------------------------------------------------

/// <summary>
/// Shared state between DummyNetClient and DummyNetServer.
/// Create one instance per single-player session and hand it to both sides.
/// </summary>
public sealed class DummyNetwork
{
    // Thread-safe queues — no manual locking needed at call sites.
    internal readonly ConcurrentQueue<byte[]> ServerInbox = new();
    internal readonly ConcurrentQueue<byte[]> ClientInbox = new();

    /// <summary>
    /// Clears all pending messages from both queues.
    /// Call when starting a new session to avoid leftover data.
    /// </summary>
    public void Clear()
    {
        ServerInbox.Clear();
        ClientInbox.Clear();
    }
}

// ---------------------------------------------------------------------------
// Client side
// ---------------------------------------------------------------------------

public sealed class DummyNetClient : NetClient
{
    private readonly DummyNetwork _network;
    private DummyNetConnection? _connection;

    public DummyNetClient(DummyNetwork network)
    {
        _network = network;
    }

    public override void Start() { }

    public override NetConnection Connect(string ip, int port)
    {
        _connection = new DummyNetConnection(_network);
        return _connection;
    }

    public override NetIncomingMessage? ReadMessage()
    {
        if (_network.ClientInbox.TryDequeue(out byte[]? payload))
        {
            return new NetIncomingMessage
            {
                Payload = payload,
                SenderConnection = _connection,
            };
        }
        return null;
    }

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method)
    {
        _network.ServerInbox.Enqueue(payload.ToArray());
    }
}

// ---------------------------------------------------------------------------
// The connection handle the client holds (used by the server to reply)
// ---------------------------------------------------------------------------

public sealed class DummyNetConnection : NetConnection
{
    private readonly DummyNetwork _network;

    internal DummyNetConnection(DummyNetwork network)
    {
        _network = network;
    }

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method, int sequenceChannel = 0)
    {
        _network.ClientInbox.Enqueue(payload.ToArray());
    }

    public override IPEndPointCi RemoteEndPoint() => IPEndPointCiDefault.Create("127.0.0.1");

    public override void Update() { }

    public override bool EqualsConnection(NetConnection other) => other is DummyNetConnection;
}

// ---------------------------------------------------------------------------
// Server side
// ---------------------------------------------------------------------------

public sealed class DummyNetServer : NetServer
{
    private readonly DummyNetwork _network;

    // The one connection object we hand back in Connect lifecycle messages.
    // The server needs it so it can call SendMessage back to the client.
    private DummyNetConnection? _clientConnection;
    private bool _connectionAnnounced;

    public DummyNetServer(DummyNetwork network)
    {
        _network = network;
    }

    public override void SetPort(int port) { }

    public override void Start() { }

    public override NetIncomingMessage? ReadMessage()
    {
        if (_network.ServerInbox.IsEmpty)
            return null;

        if (!_connectionAnnounced)
        {
            _connectionAnnounced = true;
            _network.ServerInbox.TryDequeue(out _); // consume the trigger packet
            _clientConnection = new DummyNetConnection(_network);
            return new NetIncomingMessage
            {
                Type = NetworkMessageType.Connect,
                SenderConnection = _clientConnection,
            };
        }

        if (_network.ServerInbox.TryDequeue(out byte[]? payload))
        {
            return new NetIncomingMessage
            {
                Type = NetworkMessageType.Data,
                Payload = payload,
                SenderConnection = _clientConnection,
            };
        }

        return null;
    }

    /// <summary>
    /// Resets connection state so the server can accept a fresh connection,
    /// e.g. when the player starts a new single-player session.
    /// </summary>
    public void Reset()
    {
        _network.Clear();
        _connectionAnnounced = false;
        _clientConnection = null;
    }
}