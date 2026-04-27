using static ManicDigger.Mods.ModNetworkProcess;

public class ModDiagLog : ModBase
{
    private readonly IGameClient game;
    private float logTimer = 0f;
    private const float LogInterval = 5f;

    private long lastGen0 = 0, lastGen1 = 0, lastGen2 = 0;
    private long lastTotalMemory = 0;

    public ModDiagLog(IGameClient game)
    {
        this.game = game;
        DiagLog.Write("ModDiagLog started");
    }

    public override void OnReadWriteMainThread(float dt)
    {
        logTimer += dt;
        if (logTimer < LogInterval) return;
        logTimer = 0f;

        long totalMemory = GC.GetTotalMemory(false);
        long gen0 = GC.CollectionCount(0);
        long gen1 = GC.CollectionCount(1);
        long gen2 = GC.CollectionCount(2);
        int commitPending = game.commitActions.Count;

        int totalChunks = 0;
        int loadedChunks = 0;
        int renderedChunks = 0;
        int dirtyChunks = 0;
        int lightLeaks = 0;

        var chunks = game.VoxelMap.Chunks;
        if (chunks != null)
        {
            totalChunks = chunks.Length;
            for (int i = 0; i < chunks.Length; i++)
            {
                var c = chunks[i];
                if (c == null) continue;
                loadedChunks++;
                if (c.rendered == null) continue;
                if (c.rendered.Ids != null) renderedChunks++;
                if (c.rendered.Dirty) dirtyChunks++;
                if (c.rendered.LightRented && c.rendered.Light != null)
                    lightLeaks++;
            }
        }

        long memDeltaKb = (totalMemory - lastTotalMemory) / 1024;
        long gen0Delta = gen0 - lastGen0;
        long gen1Delta = gen1 - lastGen1;
        long gen2Delta = gen2 - lastGen2;

        DiagLog.Write("--- DIAG ---");
        DiagLog.Write("  Memory     : {MemMB} MB  (delta: {Delta} KB)",
            totalMemory / 1024 / 1024, memDeltaKb);
        DiagLog.Write("  GC Gen0/1/2: {G0} / {G1} / {G2} collections since last",
            gen0Delta, gen1Delta, gen2Delta);
        DiagLog.Write("  CommitQueue: {Q} pending", commitPending);
        DiagLog.Write("  Chunks     : {Loaded}/{Total} loaded | {Rendered} rendered | {Dirty} dirty",
            loadedChunks, totalChunks, renderedChunks, dirtyChunks);
        DiagLog.Write("  LightLeaks : {L}", lightLeaks);
        DiagLog.Write("  Entities   : {E}", game.Entities.Count);

        lastTotalMemory = totalMemory;
        lastGen0 = gen0;
        lastGen1 = gen1;
        lastGen2 = gen2;
    }
}