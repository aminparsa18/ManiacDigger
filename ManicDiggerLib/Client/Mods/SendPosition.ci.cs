/// <summary>
/// Sends the local player's position and orientation to the server at most every 100ms.
/// </summary>
public class ModSendPosition : ModBase
{
    private const int SendIntervalMs = 100;

    public override void OnNewFrame(Game game, float args)
    {
        if (!game.spawned) return;
        if (game.platform.TimeMillisecondsFromStart - game.lastpositionsentMilliseconds <= SendIntervalMs) return;

        game.lastpositionsentMilliseconds = game.platform.TimeMillisecondsFromStart;

        var pos = game.player.position;
        game.SendPacketClient(ClientPackets.PositionAndOrientation(
            game, game.LocalPlayerId,
            pos.x, pos.y, pos.z,
            pos.rotx, pos.roty, pos.rotz,
            game.localstance));
    }
}