namespace ManicDigger.Worker;

public sealed class SeasonBroadcastTask : IScheduledTask
{
    public TimeSpan Interval => TimeSpan.FromMinutes(1);

    private readonly Server _server;
    private int _lastQuarterHour = -1;

    public SeasonBroadcastTask(Server server) => _server = server;

    public Task ExecuteAsync(CancellationToken ct)
    {
        int current = _server.GetTimer().GetQuarterHourPartOfDay();
        if (current == _lastQuarterHour) return Task.CompletedTask;

        _lastQuarterHour = current;
        _server.BroadcastSeason();
        return Task.CompletedTask;
    }
}