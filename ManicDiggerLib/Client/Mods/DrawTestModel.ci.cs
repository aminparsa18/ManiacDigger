using System.Text;

public class ModDrawTestModel : ModBase
{
    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        if (game.guistate == GuiState.MapLoading)
        {
            return;
        }

        DrawTestModel(game, deltaTime);
    }

    private void DrawTestModel(Game game, float deltaTime)
    {
        if (!game.ENABLE_DRAW_TEST_CHARACTER)
        {
            return;
        }
        if (testmodel == null)
        {
            testmodel = new AnimatedModelRenderer();
            byte[] data = game.GetAssetFile("player.txt");
            int dataLength = game.GetAssetFileLength("player.txt");
            string dataString = Encoding.UTF8.GetString(data, 0, dataLength);
            AnimatedModel model = AnimatedModelSerializer.Deserialize(dataString);
            testmodel.Start(game, model);
        }
        game.GLPushMatrix();
        game.GLTranslate(game.map.MapSizeX / 2, game.Blockheight(game.map.MapSizeX / 2, game.map.MapSizeY / 2 - 2, 128), game.map.MapSizeY / 2 - 2);
        game.platform.BindTexture2d(game.GetTexture("mineplayer.png"));
        testmodel.Render(deltaTime, 0, true, true, 1);
        game.GLPopMatrix();
    }
    private AnimatedModelRenderer testmodel;

    public override bool OnClientCommand(Game game, ClientCommandArgs args)
    {
        if (args.command == "testmodel")
        {
            game.ENABLE_DRAW_TEST_CHARACTER = game.BoolCommandArgument(args.arguments);
            return true;
        }
        return false;
    }
}
