/// <summary>
/// Applies fall damage and manages the falling wind sound effect for the local player.
/// </summary>
public class ModFallDamageToPlayer : ModBase
{
    private const float FallDamageCooldownSeconds = 1f;

    private bool fallSoundPlaying;
    private int lastFallDamageTimeMilliseconds;

    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        if (game.guistate == GuiState.MapLoading) return;

        if (game.controls.freemove)
        {
            if (fallSoundPlaying) SetFallSoundActive(game, false);
            return;
        }

        if (game.FollowId() == null)
            UpdateFallDamageToPlayer(game, args.GetDt());
    }

    internal void UpdateFallDamageToPlayer(Game game, float dt)
    {
        float fallSpeed = game.movedz / -game.basemovespeed;

        int posX = game.GetPlayerEyesBlockX();
        int posY = game.GetPlayerEyesBlockY();
        int posZ = game.GetPlayerEyesBlockZ();

        // Play falling wind sound when high up or falling fast
        bool highUp = game.Blockheight(posX, posY, posZ) < posZ - 8;
        SetFallSoundActive(game, (highUp || fallSpeed > 3) && fallSpeed > 2);

        ApplyFallDamage(game, posX, posY, posZ, fallSpeed);
    }

    private void ApplyFallDamage(Game game, int posX, int posY, int posZ, float fallSpeed)
    {
        if (fallSpeed < 4f) return;
        if (!game.map.IsValidPos(posX, posY, posZ - 3)) return;

        int blockBelow = game.map.GetBlock(posX, posY, posZ - 3);
        if (blockBelow == 0 || game.IsWater(blockBelow)) return;

        // fallspeed 4 ≈ 10 blocks high, 5.5 ≈ 20 blocks high
        float severity = fallSpeed switch
        {
            < 4.5f => 0.3f,
            < 5.0f => 0.5f,
            < 5.5f => 0.6f,
            < 6.0f => 0.8f,
            _ => 1.0f
        };

        int now = game.platform.TimeMillisecondsFromStart();
        if ((now - lastFallDamageTimeMilliseconds) / 1000f < FallDamageCooldownSeconds) return;

        lastFallDamageTimeMilliseconds = now;
        game.ApplyDamageToPlayer((int)(severity * game.PlayerStats.MaxHealth), Packet_DeathReasonEnum.FallDamage, 0);
    }

    internal void SetFallSoundActive(Game game, bool active)
    {
        game.AudioPlayLoop("fallloop.wav", active, true);
        fallSoundPlaying = active;
    }
}