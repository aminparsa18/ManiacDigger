public class ClientPacketHandlerDialog : ClientPacketHandler
{
    public override void Handle(Game game, Packet_Server packet)
    {
        Packet_ServerDialog d = packet.Dialog;
        if (d.Dialog == null)
        {
            if (game.GetDialogId(d.DialogId) != -1 && game.dialogs[game.GetDialogId(d.DialogId)].value.IsModal != 0)
            {
                game.GuiStateBackToGame();
            }
            if (game.GetDialogId(d.DialogId) != -1)
            {
                game.dialogs[game.GetDialogId(d.DialogId)] = null;
            }
            if (game.DialogsCount == 0)
            {
                game.SetFreeMouse(false);
            }
        }
        else
        {
            VisibleDialog d2 = new()
            {
                key = d.DialogId,
                value = d.Dialog
            };
            d2.screen = ConvertDialog(game, d2.value);
            d2.screen.game = game;
            if (game.GetDialogId(d.DialogId) == -1)
            {
                for (int i = 0; i < game.dialogs.Count(); i++)
                {
                    if (game.dialogs[i] == null)
                    {
                        game.dialogs[i] = d2;
                        break;
                    }
                }
            }
            else
            {
                game.dialogs[game.GetDialogId(d.DialogId)] = d2;
            }
            if (d.Dialog.IsModal != 0)
            {
                game.guistate = GuiState.ModalDialog;
                game.SetFreeMouse(true);
            }
        }
    }

    private static GameScreen ConvertDialog(Game game, Packet_Dialog p)
    {
        DialogScreen s = new()
        {
            widgets = new MenuWidget[p.WidgetsCount],
            WidgetCount = p.WidgetsCount
        };
        for (int i = 0; i < p.WidgetsCount; i++)
        {
            Packet_Widget a = p.Widgets[i];
            MenuWidget b = new();
            if (a.Type == Packet_WidgetTypeEnum.Text)
            {
                b.type = WidgetType.Label;
            }
            if (a.Type == Packet_WidgetTypeEnum.Image)
            {
                b.type = WidgetType.Button;
            }
            if (a.Type == Packet_WidgetTypeEnum.TextBox)
            {
                b.type = WidgetType.Textbox;
            }
            b.x = a.X;
            b.y = a.Y;
            b.sizex = a.Width;
            b.sizey = a.Height_;
            b.text = a.Text;
            if (b.text != null)
            {
                b.text = b.text.Replace("!SERVER_IP!", game.ServerInfo.connectdata.Ip);
            }
            if (b.text != null)
            {
                b.text = b.text.Replace("!SERVER_PORT!", game.ServerInfo.connectdata.Port.ToString());
            }
            b.color = a.Color;
            if (a.Font != null)
            {
                b.font = new FontCi
                {
                    family = game.ValidFont(a.Font.FamilyName),
                    size = game.DecodeFixedPoint(a.Font.SizeFloat),
                    style = a.Font.FontStyle
                };
            }
            b.id = a.Id;
            b.isbutton = a.ClickKey != 0;
            if (a.Image == "Solid")
            {
                b.image = null;
            }
            else if (a.Image != null)
            {
                b.image = string.Concat(a.Image, ".png");
            }
            s.widgets[i] = b;
        }
        for (int i = 0; i < s.WidgetCount; i++)
        {
            if (s.widgets[i] == null) { continue; }
            if (s.widgets[i].type == WidgetType.Textbox)
            {
                s.widgets[i].editing = true;
                break;
            }
        }
        return s;
    }
}
