/// <summary>
/// Handles environmental damage to the player from blocks (e.g. lava, fire) and drowning mechanics.
/// </summary>
public class ModBlockDamageToPlayer : ModBase
{
    public const int BlockDamageToPlayerEvery = 1;
    private readonly TimerCi blockDamageTimer;

    public ModBlockDamageToPlayer()
    {
        blockDamageTimer = TimerCi.Create(BlockDamageToPlayerEvery, BlockDamageToPlayerEvery * 2);
    }

    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        if (game.guistate == GuiState.MapLoading || game.FollowId() != null)
            return;

        UpdateBlockDamageToPlayer(game, args.GetDt());
    }

    internal void UpdateBlockDamageToPlayer(Game game, float dt)
    {
        UpdateBlockDamage(game, dt);
        UpdateDrowning(game);
    }

    /// <summary>Applies damage from damaging blocks (e.g. lava) the player is standing in or near.</summary>
    private void UpdateBlockDamage(Game game, float dt)
    {
        float pX = game.player.position.x;
        float pY = game.player.position.y + game.entities[game.LocalPlayerId].drawModel.eyeHeight;
        float pZ = game.player.position.z;

        int block1 = GetBlockAt(game, pX, pY, pZ);
        int block2 = GetBlockAt(game, pX, pY - 1, pZ);

        int damage = game.d_Data.DamageToPlayer()[block1] + game.d_Data.DamageToPlayer()[block2];
        if (damage <= 0) return;

        // Prefer eye-level block as damage source; fall back to feet block
        int hurtingBlock = (block1 != 0 && game.d_Data.DamageToPlayer()[block1] > 0) ? block1 : block2;

        int times = blockDamageTimer.Update(dt);
        for (int i = 0; i < times; i++)
            game.ApplyDamageToPlayer(damage, Packet_DeathReasonEnum.BlockDamage, hurtingBlock);
    }

    /// <summary>Drains oxygen while underwater and applies drowning damage when oxygen is depleted.</summary>
    private static void UpdateDrowning(Game game)
    {
        int deltaMs = game.platform.TimeMillisecondsFromStart() - game.lastOxygenTickMilliseconds;
        if (deltaMs < 1000) return;

        if (game.WaterSwimmingEyes())
        {
            game.PlayerStats.CurrentOxygen -= 1;
            if (game.PlayerStats.CurrentOxygen <= 0)
            {
                game.PlayerStats.CurrentOxygen = 0;
                int dmg = Math.Max(1, game.PlayerStats.MaxHealth / 10);
                game.ApplyDamageToPlayer(dmg, Packet_DeathReasonEnum.Drowning, 0);
            }
        }
        else
        {
            game.PlayerStats.CurrentOxygen = game.PlayerStats.MaxOxygen;
        }

        if (GameVersionHelper.ServerVersionAtLeast(game.platform, game.serverGameVersion, 2014, 3, 31))
            game.SendPacketClient(ClientPackets.Oxygen(game.PlayerStats.CurrentOxygen));

        game.lastOxygenTickMilliseconds = game.platform.TimeMillisecondsFromStart();
    }

    private static int GetBlockAt(Game game, float x, float y, float z)
    {
        int bx = (int)MathF.Floor(x);
        int by = (int)MathF.Floor(y);
        int bz = (int)MathF.Floor(z);
        return game.map.IsValidPos(bx, bz, by) ? game.map.GetBlock((int)x, (int)z, (int)y) : 0;
    }
}

public class DialogScreen : GameScreen
{
    public override void OnButton(MenuWidget w)
    {
        if (w.isbutton)
        {
            string[] textValues = new string[WidgetCount];
            for (int i = 0; i < WidgetCount; i++)
            {
                string s = widgets[i].text;
                s ??= "";
                textValues[i] = s;
            }
            game.SendPacketClient(ClientPackets.DialogClick(w.id, textValues, WidgetCount));
        }
    }
}
