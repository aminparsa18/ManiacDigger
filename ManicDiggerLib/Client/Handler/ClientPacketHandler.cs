/// <summary>
/// Base class for all server-packet handlers.
/// Implementations receive a fully-deserialized <see cref="Packet_Server"/> and
/// are responsible for applying its effect to the game state.
/// </summary>
public abstract class ClientPacketHandler
{
    /// <summary>Applies the effect of <paramref name="packet"/> to <paramref name="game"/>.</summary>
    public abstract void Handle(Game game, Packet_Server packet);
}