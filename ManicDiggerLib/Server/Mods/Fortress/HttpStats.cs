using FragLabs.HTTP;
using System.Diagnostics;

namespace ManicDigger.Mods.Fortress;

public class HttpStats : IMod
{
    public void PreStart(IModManager m) { }

    public void Start(IModManager m)
    {
        var module = new HttpInfoModule
        {
            m = m
        };
        m.InstallHttpModule("Stats", () => "Basic server stats", module);
    }
}

public class HttpInfoModule : IHttpModule
{
    private Stopwatch stopwatch;
    public IModManager m;
    private int pageViews;

    public void Installed(HttpServer server)
    {
        stopwatch = new Stopwatch();
        stopwatch.Start();
    }

    public void Uninstalled(HttpServer server)
    {
    }

    public bool ResponsibleForRequest(HttpRequest request)
    {
        if (request.Uri.AbsolutePath.Equals("/stats", StringComparison.CurrentCultureIgnoreCase))
            return true;
        return false;
    }

    public bool ProcessAsync(ProcessRequestEventArgs args)
    {
        pageViews++;
        var process = Process.GetCurrentProcess();
        double cpu = process.TotalProcessorTime.TotalSeconds / stopwatch.Elapsed.TotalSeconds;
        var html = $"""
            <html>
            <h1>System Statistics</h1>
            <ul>
            <li>Uptime: {ToReadableString(stopwatch.Elapsed)}<br/></li>
            <li>CPU usage: {cpu:P2}<br/></li>
            <li>Total processor time: {ToReadableString(process.TotalProcessorTime)}<br/></li>
            <li>Working set: {BytesToString(process.WorkingSet64)}<br/></li>
            <li>Total bytes downloaded: {BytesToString(m.TotalReceivedBytes)}<br/></li>
            <li>Total bytes uploaded: {BytesToString(m.TotalSentBytes)}<br/></li>
            </ul>
            Page accessed <b>{pageViews}</b> times.<br/>
            </html>
            """;
        args.Response.Producer = new BufferedProducer(html);
        return false;
    }

    //http://stackoverflow.com/questions/281640/how-do-i-get-a-human-readable-file-size-in-bytes-abbreviation-using-net
    private static string BytesToString(long byteCount)
    {
        string[] suf = { "B", "KB", "MB", "GB", "TB", "PB", "EB" }; //Longs run out around EB
        if (byteCount == 0)
            return $"0 {suf[0]}";
        long bytes = Math.Abs(byteCount);
        int place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
        double num = Math.Round(bytes / Math.Pow(1024, place), 1);
        return $"{Math.Sign(byteCount) * num} {suf[place]}";
    }

    public static string ToReadableString(TimeSpan span)
    {
        string formatted = string.Format("{0}{1}{2}{3}",
                                         span.Duration().Days > 0 ? string.Format("{0:0} day{1}, ", span.Days, span.Days == 1 ? string.Empty : "s") : string.Empty,
                                         span.Duration().Hours > 0 ? string.Format("{0:0} hour{1}, ", span.Hours, span.Hours == 1 ? string.Empty : "s") : string.Empty,
                                         span.Duration().Minutes > 0 ? string.Format("{0:0} minute{1}, ", span.Minutes, span.Minutes == 1 ? string.Empty : "s") : string.Empty,
                                         span.Duration().Seconds > 0 ? string.Format("{0:0} second{1}", span.Seconds, span.Seconds == 1 ? string.Empty : "s") : string.Empty);

        if (formatted.EndsWith(", ")) formatted = formatted[..^2];

        if (string.IsNullOrEmpty(formatted)) formatted = "0 seconds";

        return formatted;
    }
}
