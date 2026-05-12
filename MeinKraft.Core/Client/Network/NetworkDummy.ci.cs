// In-process network implementation for single-player.
//
// DummyNetwork holds three channels:
//   ConnectChannel — client signals a new connection (capacity 1, no data lost)
//   ServerInbox    — client writes data, server reads
//   ClientInbox    — server writes data, client reads
//
// Both sides live in the same process on different threads. System.Threading.Channels
// handles all synchronisation internally — no manual locking at call sites.

using System.Threading.Channels;

// ---------------------------------------------------------------------------
// Shared in-process channel
// ---------------------------------------------------------------------------

/// <summary>
/// Shared state between <see cref="DummyNetClient"/> and <see cref="DummyNetServer"/>.
/// Create one instance per single-player session and inject it into both sides.
/// Call <see cref="Reset"/> when starting a fresh session.
/// </summary>
public sealed class DummyNetwork : IDummyNetwork
{
    // Data channels hold at most this many pending messages before dropping the oldest.
    // At 60 fps a burst of more than 512 unread messages indicates a frozen consumer.
    private const int DataCapacity = 4096;

    private Channel<bool> _connectChannel;
    private Channel<byte[]> _serverInbox;
    private Channel<byte[]> _clientInbox;

    // --- IDummyNetwork surface --------------------------------------------------

    /// <summary>Client writes <c>true</c> once to signal a connection attempt.</summary>
    public ChannelWriter<bool> ConnectWriter => _connectChannel.Writer;
    /// <summary>Server reads to learn a client has connected.</summary>
    public ChannelReader<bool> ConnectReader => _connectChannel.Reader;

    /// <summary>Client sends packets here; server reads from here.</summary>
    public ChannelWriter<byte[]> ServerWriter => _serverInbox.Writer;
    public ChannelReader<byte[]> ServerReader => _serverInbox.Reader;

    /// <summary>Server sends packets here; client reads from here.</summary>
    public ChannelWriter<byte[]> ClientWriter => _clientInbox.Writer;
    public ChannelReader<byte[]> ClientReader => _clientInbox.Reader;

    // ---------------------------------------------------------------------------

    public DummyNetwork() => Allocate();

    /// <summary>
    /// Discards all pending messages and replaces all channels with fresh ones.
    /// Both <see cref="DummyNetClient"/> and <see cref="DummyNetServer"/> automatically
    /// pick up the new channels on their next read/write because they always call
    /// through this object's properties rather than caching channel references.
    /// </summary>
    public void Reset() => Allocate();

    // ---------------------------------------------------------------------------

    private void Allocate()
    {
        // Connect signal: bounded to 1. DropWrite means a second Connect() before
        // the server has processed the first is silently ignored — correct behaviour
        // for a single-player session where reconnect replaces the previous attempt.
        _connectChannel = Channel.CreateBounded<bool>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropWrite,
            SingleReader = true,   // only DummyNetServer reads
            SingleWriter = true,   // only DummyNetClient writes
        });

        _serverInbox = CreateDataChannel(singleReader: true, singleWriter: true);
        _clientInbox = CreateDataChannel(singleReader: true, singleWriter: true);
    }

    private static Channel<byte[]> CreateDataChannel(bool singleReader, bool singleWriter) =>
        Channel.CreateBounded<byte[]>(new BoundedChannelOptions(DataCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = singleReader,
            SingleWriter = singleWriter,
        });
}

public interface IDummyNetwork
{
    ChannelWriter<bool> ConnectWriter { get; }
    ChannelReader<bool> ConnectReader { get; }
    ChannelWriter<byte[]> ServerWriter { get; }
    ChannelReader<byte[]> ServerReader { get; }
    ChannelWriter<byte[]> ClientWriter { get; }
    ChannelReader<byte[]> ClientReader { get; }
    void Reset();
}

// ---------------------------------------------------------------------------
// Client side
// ---------------------------------------------------------------------------

public sealed class DummyNetClient : NetClient
{
    private readonly IDummyNetwork _network;
    private DummyNetConnection? _connection;

    public DummyNetClient(IDummyNetwork network)
    {
        _network = network;
    }

    public override void Start() { }

    public override NetConnection Connect(string ip, int port)
    {
        // Write the connect signal on its own channel — no data packet is consumed.
        // DropWrite mode on the channel means calling Connect() twice is harmless.
        _network.ConnectWriter.TryWrite(true);
        _connection = new DummyNetConnection(_network);
        return _connection;
    }

    public override NetIncomingMessage? ReadMessage()
    {
        if (_network.ClientReader.TryRead(out byte[]? payload))
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
        // TryWrite returns false when the channel is full and dropped the write,
        // which matches DropOldest semantics — the oldest enqueued message was
        // evicted, not this one. Either way we don't block the game thread.
        _network.ServerWriter.TryWrite(payload.ToArray());
    }

    /// <summary>
    /// Resets client-side connection state. Call in concert with
    /// <see cref="DummyNetServer.Reset"/> and <see cref="IDummyNetwork.Reset"/>
    /// when starting a new single-player session.
    /// </summary>
    public void Reset()
    {
        _connection = null;
    }
}

// ---------------------------------------------------------------------------
// Connection handle (used by server to send replies to the client)
// ---------------------------------------------------------------------------

public sealed class DummyNetConnection : NetConnection
{
    private static int _nextId;
    private readonly int _id = Interlocked.Increment(ref _nextId);

    private readonly IDummyNetwork _network;

    internal DummyNetConnection(IDummyNetwork network)
    {
        _network = network;
    }

    public override void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method, int sequenceChannel = 0)
        => _network.ClientWriter.TryWrite(payload.ToArray());

    public override IpEndpoint RemoteEndPoint() => IpEndpointDefault.Create("127.0.0.1");

    public override void Update() { }

    /// <summary>
    /// Two connections are equal only if they are literally the same object.
    /// Each <see cref="DummyNetConnection"/> gets a unique auto-incremented ID
    /// so that reconnections and future multi-client scenarios are not confused.
    /// </summary>
    public override bool EqualsConnection(NetConnection other)
        => other is DummyNetConnection c && c._id == _id;
}

// ---------------------------------------------------------------------------
// Server side
// ---------------------------------------------------------------------------

public sealed class DummyNetServer : NetServer
{
    private readonly IDummyNetwork _network;

    private DummyNetConnection? _clientConnection;

    public DummyNetServer(IDummyNetwork network)
    {
        _network = network;
    }

    public override void SetPort(int port) { }

    public override void Start() { }

    public override NetIncomingMessage? ReadMessage()
    {
        // Check for a new connection first.
        // ConnectChannel is separate from data so no packet is ever dropped.
        if (_network.ConnectReader.TryRead(out _))
        {
            _clientConnection = new DummyNetConnection(_network);
            return new NetIncomingMessage
            {
                Type = NetworkMessageType.Connect,
                SenderConnection = _clientConnection,
            };
        }

        if (_network.ServerReader.TryRead(out byte[]? payload))
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
    /// Resets server-side connection state and flushes all channels via
    /// <see cref="IDummyNetwork.Reset"/>. Also call <see cref="DummyNetClient.Reset"/>
    /// on the client side so both ends start clean.
    /// </summary>
    public void Reset()
    {
        _network.Reset();      // recreates all three channels
        _clientConnection = null;
    }
}