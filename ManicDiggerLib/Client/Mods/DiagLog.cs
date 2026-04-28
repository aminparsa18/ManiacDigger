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

   
}