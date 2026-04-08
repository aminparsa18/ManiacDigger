using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

/// <summary>
/// Handles rendering and input for in-game dialogs (normal and modal).
/// </summary>
public class ModDialog : ModBase
{
    private static readonly string[] Empty = [];
    private const string TypableChars = "abcdefghijklmnopqrstuvwxyz1234567890\t ";

    private readonly ClientPacketHandler packetHandler = new ClientPacketHandlerDialog();

    public override void OnNewFrameDraw2d(Game game, float deltaTime)
    {
        game.packetHandlers[Packet_ServerIdEnum.Dialog] = packetHandler;
        DrawDialogs(game);
    }

    internal static void DrawDialogs(Game game)
    {
        for (int i = 0; i < game.dialogs.Length; i++)
        {
            VisibleDialog d = game.dialogs[i];
            if (d == null) continue;

            d.screen.screenx = game.Width() / 2 - d.value.Width / 2;
            d.screen.screeny = game.Height() / 2 - d.value.Height_ / 2;
            d.screen.DrawWidgets();
        }
    }

    public override void OnKeyPress(Game game, KeyPressEventArgs args)
    {
        if (game.guistate != GuiState.ModalDialog && game.guistate != GuiState.Normal) return;
        if (game.IsTyping) return;

        ForEachDialog(game, d => d.screen.OnKeyPress(game, args));

        for (int k = 0; k < game.dialogs.Length; k++)
        {
            VisibleDialog d = game.dialogs[k];
            if (d == null) continue;

            for (int i = 0; i < d.value.WidgetsCount; i++)
            {
                Packet_Widget w = d.value.Widgets[i];
                if (w == null) continue;

                // Only typeable characters are handled here; special characters use KeyDown
                if (TypableChars.Contains((char)w.ClickKey) && args.GetKeyChar() == w.ClickKey)
                {
                    game.SendPacketClient(ClientPackets.DialogClick(w.Id, Empty, 0));
                    return;
                }
            }
        }
    }

    public override void OnKeyDown(Game game, KeyEventArgs args)
    {
        ForEachDialog(game, d => d.screen.OnKeyDown(game, args));

        bool isEsc = args.GetKeyCode() == game.GetKey(Keys.Escape);

        if (game.guistate == GuiState.Normal && isEsc)
        {
            for (int i = 0; i < game.dialogs.Length; i++)
            {
                VisibleDialog d = game.dialogs[i];
                if (d == null) continue;
                if (d.value.IsModal != 0)
                {
                    game.dialogs[i] = null;
                    return;
                }
            }
            game.ShowEscapeMenu();
            args.SetHandled(true);
            return;
        }

        if (game.guistate == GuiState.ModalDialog)
        {
            if (isEsc)
            {
                // Close all modal dialogs
                for (int i = 0; i < game.dialogs.Length; i++)
                {
                    if (game.dialogs[i]?.value.IsModal != 0)
                        game.dialogs[i] = null;
                }
                game.SendPacketClient(ClientPackets.DialogClick("Esc", Empty, 0));
                game.GuiStateBackToGame();
                args.SetHandled(true);
            }
            else if (args.GetKeyCode() == game.GetKey(Keys.Tab))
            {
                game.SendPacketClient(ClientPackets.DialogClick("Tab", Empty, 0));
                args.SetHandled(true);
            }
        }
    }

    public override void OnKeyUp(Game game, KeyEventArgs args) =>
        ForEachDialog(game, d => d.screen.OnKeyUp(game, args));

    public override void OnMouseDown(Game game, MouseEventArgs args) =>
        ForEachDialog(game, d => d.screen.OnMouseDown(game, args));

    public override void OnMouseUp(Game game, MouseEventArgs args) =>
        ForEachDialog(game, d => d.screen.OnMouseUp(game, args));

    /// <summary>Iterates all non-null dialogs and applies an action to each.</summary>
    private static void ForEachDialog(Game game, Action<VisibleDialog> action)
    {
        for (int i = 0; i < game.dialogs.Length; i++)
        {
            if (game.dialogs[i] != null)
                action(game.dialogs[i]);
        }
    }
}