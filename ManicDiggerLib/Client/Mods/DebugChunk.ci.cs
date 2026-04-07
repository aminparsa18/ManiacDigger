public class ModDebugChunk : ClientMod
{
    public ModDebugChunk()
    {
        draw = false;
        lines = new DrawWireframeCube();
    }

    private bool draw;
    private readonly DrawWireframeCube lines;

    public override bool OnClientCommand(Game game, ClientCommandArgs args)
    {
        if (args.command == "chunk")
        {
            draw = !draw;
            return true;
        }
        return false;
    }

    public override void OnNewFrameDraw3d(Game game, float deltaTime)
    {
        if (draw)
        {
            lines.DrawWireframeCube_(game, (int)(game.player.position.x / Game.chunksize) * Game.chunksize + 8,
                (int)(game.player.position.y / Game.chunksize) * Game.chunksize + Game.chunksize / 2,
                (int)(game.player.position.z / Game.chunksize) * Game.chunksize + Game.chunksize / 2, Game.chunksize, Game.chunksize, Game.chunksize);
        }
    }
}
