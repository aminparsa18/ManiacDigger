// abstract network layer
// All implementations (Dummy, TCP, WebSocket) implement these abstractions.
// Game logic only ever touches these types; no implementation details leak upward.

/// <summary>
/// A received message from the network. Carries the raw payload and metadata.
/// </summary>
public class NetIncomingMessage
{
    public NetConnection SenderConnection { get; init; }
    public NetworkMessageType Type { get; init; } = NetworkMessageType.Data;
    public ReadOnlyMemory<byte> Payload { get; init; }
}

/// <summary>
/// Reliability/ordering mode for outgoing messages. Mirrors Lidgren's delivery
/// method enum so the real implementation can map directly.
/// </summary>
public enum MyNetDeliveryMethod
{
    Unreliable,
    UnreliableSequenced,
    ReliableUnordered,
    ReliableSequenced,
    ReliableOrdered,
}

/// <summary>
/// Type of a received message. Implementations synthesize Connect/Disconnect
/// events so callers can handle connection lifecycle uniformly.
/// </summary>
public enum NetworkMessageType
{
    Data,
    Connect,
    Disconnect,
}

// ---------------------------------------------------------------------------
// Abstract server / client / connection
// ---------------------------------------------------------------------------

/// <summary>
/// Listens for incoming connections and reads messages from all connected clients.
/// </summary>
public abstract class NetServer
{
    public abstract void SetPort(int port);
    public abstract void Start();

    /// <summary>
    /// Returns the next pending message, or null if none is available.
    /// May return Connect/Disconnect lifecycle messages as well as Data messages.
    /// </summary>
    public abstract NetIncomingMessage? ReadMessage();
}

/// <summary>
/// Connects to a server and exchanges messages with it.
/// </summary>
public abstract class NetClient
{
    public abstract void Start();

    /// <summary>
    /// Initiates a connection. Returns the connection handle immediately;
    /// the connection may not be fully established until a Connect message
    /// is received from <see cref="ReadMessage"/>.
    /// </summary>
    public abstract NetConnection Connect(string ip, int port);

    /// <summary>
    /// Returns the next pending message from the server, or null if none.
    /// </summary>
    public abstract NetIncomingMessage? ReadMessage();

    /// <summary>
    /// Sends a payload to the server with the specified delivery guarantee.
    /// </summary>
    public abstract void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method);
}

/// <summary>
/// Represents one end of an established connection. Used by the server side
/// to send messages back to a specific client.
/// </summary>
public abstract class NetConnection
{
    public abstract IpEndpoint RemoteEndPoint();

    /// <summary>
    /// Sends a payload to this specific connected peer.
    /// </summary>
    public abstract void SendMessage(ReadOnlyMemory<byte> payload, MyNetDeliveryMethod method, int sequenceChannel = 0);

    /// <summary>
    /// Called once per game tick. Implementations can use this for keep-alive,
    /// timeout checking, or any per-tick housekeeping.
    /// </summary>
    public abstract void Update();

    public abstract bool EqualsConnection(NetConnection other);
}

// ---------------------------------------------------------------------------
// IP endpoint helpers
// ---------------------------------------------------------------------------

public abstract class IpEndpoint
{
    public abstract string AddressToString();
}

public class IpEndpointDefault : IpEndpoint
{
    private readonly string _address;

    private IpEndpointDefault(string address) => _address = address;

    public static IpEndpointDefault Create(string address) => new(address);

    public override string AddressToString() => _address;
}