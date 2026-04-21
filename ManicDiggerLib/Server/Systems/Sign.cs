using ManicDigger;

public class ServerSystemSign : ServerSystem
{
    private Server server;

    protected override void Initialize(Server server)
    {
        this.server = server;
        server.modManager.RegisterOnBlockUseWithTool(OnUseWithTool);
        server.modEventHandlers.onupdateentity.Add(UpdateEntity);
        server.modEventHandlers.onuseentity.Add(OnUseEntity);
        server.modEventHandlers.ondialogclick2.Add(OnDialogClick);
    }

    private void OnUseWithTool(int player, int x, int y, int z, int tool)
    {
        if (server.modManager.GetBlockName(tool) != "Sign") return;
        if (server.d_Map.GetChunk(x, y, z) == null) return;
        if (!server.CheckBuildPrivileges(player, x, y, z, PacketBlockSetMode.Create)) return;

        var e = new ServerEntity
        {
            position = new ServerEntityPositionAndOrientation
            {
                x = x + One / 2,
                y = z,
                z = y + One / 2
            },
            sign = new ServerEntitySign { text = "Hello world!" }
        };

        e.position.heading = EntityHeading.GetHeading(
            server.modManager.GetPlayerPositionX(player),
            server.modManager.GetPlayerPositionY(player),
            e.position.x,
            e.position.z
        );

        server.AddEntity(x, y, z, e);
    }

    private void UpdateEntity(int chunkx, int chunky, int chunkz, int id)
    {
        ServerEntity e = server.GetEntity(chunkx, chunky, chunkz, id);
        if (e.sign == null) return;

        e.drawModel ??= new ServerEntityAnimatedModel();
        e.drawModel.model = "signmodel.txt";
        e.drawModel.texture = "signmodel.png";
        e.drawModel.modelHeight = One * 13 / 10;

        e.drawText ??= new ServerEntityDrawText();
        e.drawText.text = e.sign.text;
        e.drawText.dx = One * 3 / 32;
        e.drawText.dy = One * 36 / 32;
        e.drawText.dz = One * 3 / 32;

        e.usable = true;
        e.drawName ??= new ServerEntityDrawName
        {
            name = "Sign",
            onlyWhenSelected = true
        };
    }

    private void OnUseEntity(int player, int chunkx, int chunky, int chunkz, int id)
    {
        ServerEntity e = server.GetEntity(chunkx, chunky, chunkz, id);
        if (e.sign == null) return;
        if (!server.CheckBuildPrivileges(player, (int)e.position.x, (int)e.position.z, (int)e.position.y, PacketBlockSetMode.Use)) return;

        var font = new DialogFont("Verdana", 11f, DialogFontStyle.Bold);

        Widget okButton = Widget.MakeSolid(100, 100, 100, 50, ColorUtils.ColorFromArgb(255, 100, 100, 100));
        okButton.ClickKey = (char)13;
        okButton.Id = "UseSign_OK";

        var d = new Dialog
        {
            Width = 400,
            Height = 200,
            IsModal = true,
            Widgets = new Widget[]
            {
            Widget.MakeSolid(0, 0, 300, 200, ColorUtils.ColorFromArgb(255, 50, 50, 50)),
            Widget.MakeTextBox(e.sign.text, font, 50, 50, 200, 50, ColorUtils.ColorFromArgb(255, 0, 0, 0)),
            okButton,
            Widget.MakeText("OK", font, 100, 100, ColorUtils.ColorFromArgb(255, 0, 0, 0))
            }
        };

        server.clients[player].editingSign = new ServerEntityId
        {
            chunkx = chunkx,
            chunky = chunky,
            chunkz = chunkz,
            id = id
        };

        server.SendDialog(player, "UseSign", d);
    }

    private void OnDialogClick(DialogClickArgs args)
    {
        if (args.WidgetId != "UseSign_OK") return;

        var client = server.clients[args.Player];
        string newText = args.TextBoxValue[1];
        ServerEntityId id = client.editingSign;

        client.editingSign = null;

        if (newText != "")
        {
            ServerEntity e = server.GetEntity(id.chunkx, id.chunky, id.chunkz, id.id);
            e.sign.text = newText;
            server.SetEntityDirty(id);
        }
        else
        {
            server.DespawnEntity(id);
        }

        server.SendDialog(args.Player, "UseSign", null);
    }
}