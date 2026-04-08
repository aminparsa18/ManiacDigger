/// <summary>
/// Handles weapon reload timing and the R key reload trigger.
/// </summary>
public class ModReloadAmmo : ModBase
{
    public override void OnNewFrameFixed(Game game, NewFrameEventArgs args)
    {
        if (game.reloadstartMilliseconds == 0) return;

        float elapsed = (game.platform.TimeMillisecondsFromStart() - game.reloadstartMilliseconds) / 1000f;
        float reloadDelay = game.DecodeFixedPoint(game.blocktypes[game.reloadblock].ReloadDelayFloat);
        if (elapsed <= reloadDelay) return;

        int blockId = game.reloadblock;
        game.LoadedAmmo[blockId] = Math.Min(game.blocktypes[blockId].AmmoMagazine, game.TotalAmmo[blockId]);
        game.reloadstartMilliseconds = 0;
        game.reloadblock = -1;
    }

    public override void OnKeyDown(Game game, KeyEventArgs args)
    {
        if (game.guistate != GuiState.Normal || game.GuiTyping != TypingState.None) return;
        if (args.GetKeyCode() != game.GetKey(OpenTK.Windowing.GraphicsLibraryFramework.Keys.R)) return;

        Packet_Item item = game.d_Inventory.RightHand[game.ActiveMaterial];
        if (item == null || item.ItemClass != Packet_ItemClassEnum.Block) return;
        if (!game.blocktypes[item.BlockId].IsPistol) return;
        if (game.reloadstartMilliseconds != 0) return;

        var sounds = game.blocktypes[item.BlockId].Sounds;
        int sound = game.rnd.Next() % sounds.ReloadCount;
        game.PlayAudio(sounds.Reload[sound] + ".ogg");
        game.reloadstartMilliseconds = game.platform.TimeMillisecondsFromStart();
        game.reloadblock = item.BlockId;
        game.SendPacketClient(ClientPackets.Reload());
    }
}