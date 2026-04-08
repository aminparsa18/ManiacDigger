using Serilog;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Xml;
using System.Xml.XPath;

/// <summary>
/// Crash reporter backed by Serilog.
/// Drop-in replacement for the original CrashReporter — same public API,
/// same usage pattern in ManicDiggerProgram.
///
/// NuGet packages required:
///   Serilog
///   Serilog.Sinks.File
///   Serilog.Sinks.Console   (optional, for console output)
/// </summary>
public class CrashReporter
{
    // ── Configuration ────────────────────────────────────────────────────────

    /// <summary>Default crash log filename (no path — stored under GameStorePath).</summary>
    public static string DefaultFileName { get; set; } = "ManicDiggerCrash.txt";

    /// <summary>
    /// Maximum time (ms) to wait for the OnCrash delegate before aborting.
    /// Prevents a hung cleanup callback from blocking shutdown forever.
    /// </summary>
    public static int OnCrashTimeoutMs { get; set; } = 5_000;

    // ── State ────────────────────────────────────────────────────────────────

    private static bool s_isConsole;
    private static ILogger s_globalLogger = Serilog.Core.Logger.None;

    private readonly ILogger m_logger;
    private readonly string m_crashFilePath;

    /// <summary>Optional cleanup hook — called (with timeout) before shutdown.</summary>
    public Action? OnCrash { get; set; }

    // ── Constructors ─────────────────────────────────────────────────────────

    /// <summary>Writes crash report to <see cref="DefaultFileName"/> under the game store path.</summary>
    public CrashReporter() : this(DefaultFileName) { }

    /// <summary>Writes crash report to <paramref name="fileName"/> under the game store path.</summary>
    public CrashReporter(string fileName)
    {
        string dir = GameStorePath.GetStorePath();

        m_crashFilePath = Path.Combine(dir, fileName);

        m_logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.File(
                path: m_crashFilePath,
                rollingInterval: RollingInterval.Month,      // one file per month, auto-rotated
                retainedFileCountLimit: 6,                     // keep last 6 months
                outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}",
                shared: true)                        // safe for multi-thread append
            .CreateLogger();
    }

    // ── Global exception handling ─────────────────────────────────────────────

    /// <summary>
    /// Registers a global unhandled-exception handler.
    /// Call once from Main(), before anything else runs.
    /// </summary>
    /// <param name="isConsole">
    ///   true  → errors printed to stdout.<br/>
    ///   false → errors shown in a MessageBox.
    /// </param>
    public static void EnableGlobalExceptionHandling(bool isConsole)
    {
        s_isConsole = isConsole;

        // Build a lightweight global logger (console / debug) for the static handler.
        // The per-instance file logger is created when CrashReporter() is newed up.
        var cfg = new LoggerConfiguration().MinimumLevel.Fatal();

        if (isConsole)
            cfg = cfg.WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}");

        s_globalLogger = cfg.CreateLogger();

        AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
    }

    private static void OnUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        if (s_isConsole)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine("Unhandled exception — application is terminating.");
            Console.ResetColor();
        }

        var ex = e.ExceptionObject as Exception;
        s_globalLogger.Fatal(ex, "Unhandled exception");

        // Create a full crash report using a default instance
        new CrashReporter().Crash(ex);
    }

    // ── Core crash logic ──────────────────────────────────────────────────────

    /// <summary>
    /// Logs the exception, displays an error to the user, then exits the process.
    /// </summary>
    public void Crash(Exception? exCrash)
    {
        // 1. Log to file via Serilog
        try
        {
            m_logger.Fatal(exCrash, "Critical error — application is terminating");

            // Walk inner exceptions so each gets its own structured entry
            for (Exception? inner = exCrash?.InnerException; inner != null; inner = inner.InnerException)
                m_logger.Fatal(inner, "Inner exception");

            // 2. Run caller-supplied cleanup (with timeout)
            RunOnCrashCallback();

            // 3. Flush — critical for async sinks so nothing is lost before Exit()
            (m_logger as IDisposable)?.Dispose();
        }
        catch (Exception logEx)
        {
            // Logging itself failed — last resort: stderr
            Console.Error.WriteLine("CrashReporter: failed to write log: " + logEx);
        }
        finally
        {
            // 4. Tell the user
            string summary = BuildSummary(exCrash);
            DisplayToUser(summary);

            if (s_isConsole)
            {
                Console.WriteLine("Press any key to shut down...");
                Console.ReadLine();
            }

            Environment.Exit(1);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void RunOnCrashCallback()
    {
        if (OnCrash == null) return;

        // Run on a separate thread so we can enforce a hard timeout
        var task = Task.Run(() => OnCrash());
        if (!task.Wait(OnCrashTimeoutMs))
            m_logger.Warning("OnCrash() did not complete within {Timeout} ms — skipped", OnCrashTimeoutMs);

        if (task.IsFaulted)
            m_logger.Error(task.Exception, "OnCrash() threw an exception");
    }

    private string BuildSummary(Exception? ex)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  Critical Error");

        if (ex != null)
            sb.AppendLine(ex.Message);

        sb.AppendLine();
        sb.AppendLine($"Crash report written to:\n  {m_crashFilePath}");
        return sb.ToString();
    }

    private static void DisplayToUser(string message)
    {
        try
        {
            if (s_isConsole)
            {
                Console.Error.WriteLine(message);
            }
            else
            {
                // Nudge the cursor visible in case the game hid it
                for (int i = 0; i < 3; i++) { Cursor.Show(); Thread.Sleep(50); Application.DoEvents(); }
                MessageBox.Show(message, "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
        catch
        {
            // If the UI is broken we can't do much — the log file is the safety net
        }
    }
}

public static class GameStorePath
{
    public static bool IsMono = Type.GetType("Mono.Runtime") != null;

    public static string GetStorePath()
    {
        string apppath = Path.GetDirectoryName(Application.ExecutablePath);
        try
        {
            var di = new DirectoryInfo(apppath);
            if (di.Name.Equals("AutoUpdaterTemp", StringComparison.InvariantCultureIgnoreCase))
            {
                apppath = di.Parent.FullName;
            }
        }
        catch
        {
        }
        string mdfolder = "UserData";
        if (apppath.Contains(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)) && !IsMono)
        {
            string mdpath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                mdfolder);
            return mdpath;
        }
        else
        {
            return Path.Combine(apppath, mdfolder);
        }
    }

    public static string gamepathconfig = Path.Combine(GetStorePath(), "Configuration");
    public static string gamepathsaves = Path.Combine(GetStorePath(), "Saves");
    public static string gamepathbackup = Path.Combine(GetStorePath(), "Backup");

    public static bool IsValidName(string s)
    {
        if (s.Length < 1 || s.Length > 32)
        {
            return false;
        }
        for (int i = 0; i < s.Length; i++)
        {
            if (!AllowedNameChars.Contains(s[i].ToString()))
            {
                return false;
            }
        }
        return true;
    }
    public static string AllowedNameChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz1234567890_-";
}

public static class GameVersion
{
    private static string gameversion;
    public static string Version
    {
        get
        {
            if (gameversion == null)
            {
                gameversion = "unknown";
                if (File.Exists("version.txt"))
                {
                    gameversion = File.ReadAllText("version.txt").Trim();
                }
            }
            return gameversion;
        }
    }
}

public interface ICompression
{
    byte[] Compress(ReadOnlySpan<byte> data);
    byte[] Decompress(byte[] data);
}

public class CompressionGzip : ICompression
{
    public byte[] Compress(ReadOnlySpan<byte> data)
    {
        MemoryStream output = new();
        using (GZipStream compress = new(output, CompressionMode.Compress))
        {
            compress.Write(data);
        }
        return output.ToArray();
    }

    public byte[] Decompress(byte[] fi)
    {
        MemoryStream ms = new();
        // Get the stream of the source file.
        using (MemoryStream inFile = new(fi))
        {
            using GZipStream Decompress = new(inFile,
                    CompressionMode.Decompress);
            //Copy the decompression stream into the output file.
            byte[] buffer = new byte[4096];
            int numRead;
            while ((numRead = Decompress.Read(buffer, 0, buffer.Length)) != 0)
            {
                ms.Write(buffer, 0, numRead);
            }
        }
        return ms.ToArray();
    }

    public static byte[] Decompress(FileInfo fi)
    {
        MemoryStream ms = new();
        // Get the stream of the source file.
        using (FileStream inFile = fi.OpenRead())
        {
            // Get original file extension, for example "doc" from report.doc.gz.
            string curFile = fi.FullName;
            string origName = curFile[..^fi.Extension.Length];

            //Create the decompressed file.
            //using (FileStream outFile = File.Create(origName))
            {
                using GZipStream Decompress = new(inFile,
                        CompressionMode.Decompress);
                //Copy the decompression stream into the output file.
                byte[] buffer = new byte[4096];
                int numRead;
                while ((numRead = Decompress.Read(buffer, 0, buffer.Length)) != 0)
                {
                    ms.Write(buffer, 0, numRead);
                }
                //Console.WriteLine("Decompressed: {0}", fi.Name);
            }
        }
        return ms.ToArray();
    }
}

internal struct TextAndSize
{
    public string text;
    public float size;
    public override readonly int GetHashCode()
    {
        if (text == null)
        {
            return 0;
        }
        return text.GetHashCode() ^ size.GetHashCode();
    }
    public override readonly bool Equals(object? obj)
    {
        if (obj is TextAndSize other)
        {
            return this.text == other.text && this.size == other.size;
        }
        return base.Equals(obj);
    }
}

public static class Misc
{
    public static bool ReadBool(string str)
    {
        if (str == null)
        {
            return false;
        }
        else
        {
            return (str != "0"
                && (!str.Equals(bool.FalseString, StringComparison.InvariantCultureIgnoreCase)));
        }
    }
}

public class XmlTool
{
    public static string XmlVal(XmlDocument d, string path)
    {
        XPathNavigator navigator = d.CreateNavigator();
        XPathNodeIterator iterator = navigator.Select(path);
        foreach (XPathNavigator n in iterator)
        {
            return n.Value;
        }
        return null;
    }

    public static IEnumerable<string> XmlVals(XmlDocument d, string path)
    {
        XPathNavigator navigator = d.CreateNavigator();
        XPathNodeIterator iterator = navigator.Select(path);
        foreach (XPathNavigator n in iterator)
        {
            yield return n.Value;
        }
    }

    public static string X(string name, string value)
    {
        return string.Format("<{0}>{1}</{0}>", name, value);
    }
}
