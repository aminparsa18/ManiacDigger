/// <summary>
/// Draws a wireframe outline around the currently selected block or entity.
/// </summary>
public class ModDrawLinesAroundSelectedBlock : ModBase
{
    private const float SelectionScale = 1.02f;

    private readonly DrawWireframeCube lines = new();

    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        if (!game.ENABLE_DRAW2D) return;

        if (game.SelectedEntityId != -1)
            DrawEntityOutline(game);
        else if (game.SelectedBlockPositionX != -1)
            DrawBlockOutline(game);
    }

    private void DrawEntityOutline(Game game)
    {
        Entity e = game.entities[game.SelectedEntityId];
        if (e == null) return;

        float height = e.drawModel.ModelHeight;
        lines.DrawWireframeCube_(game,
            e.position.x, e.position.y + height / 2, e.position.z,
            SelectionScale, SelectionScale * height, SelectionScale);
    }

    private void DrawBlockOutline(Game game)
    {
        int x = game.SelectedBlockPositionX;
        int y = game.SelectedBlockPositionY;
        int z = game.SelectedBlockPositionZ;
        float blockHeight = game.Getblockheight(x, z, y);

        lines.DrawWireframeCube_(game,
            x + 0.5f, y + blockHeight * 0.5f, z + 0.5f,
            SelectionScale, SelectionScale * blockHeight, SelectionScale);
    }
}

/// <summary>
/// Renders a wireframe cube at a given position and scale using GL line rendering.
/// </summary>
public class DrawWireframeCube
{
    private ModelData wireframeCube;

    public void DrawWireframeCube_(Game game, float posX, float posY, float posZ, float scaleX, float scaleY, float scaleZ)
    {
        game.platform.GLLineWidth(2);
        game.platform.BindTexture2d(0);

        wireframeCube ??= game.platform.CreateModel(WireframeCube.GetWireframeCubeModelData());

        game.GLPushMatrix();
        game.GLTranslate(posX, posY, posZ);
        game.GLScale(scaleX * 0.5f, scaleY * 0.5f, scaleZ * 0.5f);
        game.DrawModel(wireframeCube);
        game.GLPopMatrix();
    }
}