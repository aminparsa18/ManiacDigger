//This handles two types of environmental damage to the player:
//Block damage — every second, checks the block at eye level and the block at foot level.
//If either deals damage (like lava), it adds them together and calls ApplyDamageToPlayer.
//A timer ensures it only ticks once per second rather than every frame.
//Drowning — every second, if the player's eyes are underwater it decrements their oxygen.
//When oxygen hits zero it deals damage equal to 10% of max health. When they surface, oxygen instantly refills.
//The current oxygen level is also sent to the server if it's new enough to support that packet.

using ManicDigger;

/// <summary>
/// Handles environmental damage to the player from blocks (e.g. lava, fire) and drowning mechanics.
/// </summary>
public class ModBlockDamageToPlayer : ModBase
{
    public const int BlockDamageToPlayerEvery = 1;
    private int lastOxygenTickMilliseconds;
    private readonly DamageTimer blockDamageTimer;

    private readonly IGameClient _client;
    private readonly IGamePlatform _platform;

    public ModBlockDamageToPlayer(IGameClient client, IGamePlatform platform)
    {
        _client = client;
        _platform = platform;
        blockDamageTimer = new DamageTimer(BlockDamageToPlayerEvery, BlockDamageToPlayerEvery * 2);
    }

    public override void OnNewFrameFixed(float args)
    {
        if (_client.GuiState == GuiState.MapLoading || _client.FollowId() != null)
            return;

        UpdateBlockDamageToPlayer(args);
    }

    private void UpdateBlockDamageToPlayer(float dt)
    {
        UpdateBlockDamage(dt);
        UpdateDrowning();
    }

    /// <summary>Applies damage from damaging blocks (e.g. lava) the player is standing in or near.</summary>
    private void UpdateBlockDamage(float dt)
    {
        float pX = _client.LocalPositionX;
        float pY = _client.LocalPositionY + _client.LocalEyeHeight;
        float pZ = _client.LocalPositionZ;

        int block1 = GetBlockAt(pX, pY, pZ);
        int block2 = GetBlockAt(pX, pY - 1, pZ);

        int damage = _client.BlockRegistry.DamageToPlayer[block1] + _client.BlockRegistry.DamageToPlayer[block2];
        if (damage <= 0) return;

        // Prefer eye-level block as damage source; fall back to feet block
        int hurtingBlock = (block1 != 0 && _client.BlockRegistry.DamageToPlayer[block1] > 0) ? block1 : block2;
        int times = blockDamageTimer.Update(dt);
        for (int i = 0; i < times; i++)
            _client.ApplyDamageToPlayer(damage, DeathReason.BlockDamage, hurtingBlock);
    }

    /// <summary>Drains oxygen while underwater and applies drowning damage when oxygen is depleted.</summary>
    private void UpdateDrowning()
    {
        int deltaMs = _platform.TimeMillisecondsFromStart - lastOxygenTickMilliseconds;
        if (deltaMs < 1000) return;

        if (_client.WaterSwimmingEyes())
        {
            _client.PlayerStats.CurrentOxygen -= 1;
            if (_client.PlayerStats.CurrentOxygen <= 0)
            {
                _client.PlayerStats.CurrentOxygen = 0;
                int dmg = Math.Max(1, _client.PlayerStats.MaxHealth / 10);
                _client.ApplyDamageToPlayer(dmg, DeathReason.Drowning, 0);
            }
        }
        else
        {
            _client.PlayerStats.CurrentOxygen = _client.PlayerStats.MaxOxygen;
        }

        if (GameVersionHelper.ServerVersionAtLeast(_client.ServerGameVersion, 2014, 3, 31))
            _client.SendPacketClient(ClientPackets.Oxygen(_client.PlayerStats.CurrentOxygen));
        lastOxygenTickMilliseconds = _platform.TimeMillisecondsFromStart;
    }

    private int GetBlockAt(float x, float y, float z)
    {
        int bx = (int)MathF.Floor(x);
        int by = (int)MathF.Floor(y);
        int bz = (int)MathF.Floor(z);
        return _client.IsValidPos(bx, bz, by) ? _client.GetBlock((int)x, (int)z, (int)y) : 0;
    }
}

public class DialogScreen : GameScreen
{
    private readonly IGameClient game;

    public DialogScreen(IGameClient game, IGamePlatform _) : base(game, _)
    {
        this.game = game;
    }

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

public class DamageTimer
{
    public float Interval { get; }
    public float? MaxDeltaTime { get; }

    private float _accumulator;

    public DamageTimer(float interval, float? maxDeltaTime = null)
    {
        if (interval <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(interval));
        }

        Interval = interval;
        MaxDeltaTime = maxDeltaTime;
    }

    public void Reset() => _accumulator = 0;

    public int Update(float dt)
    {
        if (dt < 0)
            return 0; // or throw, depending on your engine philosophy

        _accumulator += dt;

        if (MaxDeltaTime.HasValue && _accumulator > MaxDeltaTime.Value)
            _accumulator = MaxDeltaTime.Value;

        int updates = (int)(_accumulator / Interval);
        _accumulator -= updates * Interval;

        return updates;
    }
}