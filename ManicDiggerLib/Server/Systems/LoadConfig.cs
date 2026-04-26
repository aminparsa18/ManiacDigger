using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Server system responsible for loading and saving the server configuration file
/// (<c>ServerConfig.txt</c>). On first run, if no config exists, it prompts the
/// server operator interactively via the console to set basic server parameters
/// before writing the initial config to disk.
/// </summary>
public class ServerSystemLoadConfig : ServerSystem
{
    private const string ConfigFilename = "ServerConfig.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads the configuration once on startup, then flushes any pending save
    /// on subsequent ticks when <see cref="Server.configNeedsSaving"/> is set.
    /// </summary>
    protected override void Initialize(Server server)
    {
        LoadConfig(server);
        server.OnConfigLoaded();
    }

    /// <inheritdoc/>
    protected override void OnUpdate(Server server, float dt)
    {
        if (server.configNeedsSaving)
        {
            server.configNeedsSaving = false;
            SaveConfig(server);
        }
    }

    // -------------------------------------------------------------------------
    // Load
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads <c>ServerConfig.txt</c> from the game config directory.
    /// <list type="bullet">
    ///   <item>If the file does not exist, a default config is written and the method returns.</item>
    ///   <item>If the file is in the current XML format, it is deserialized directly.</item>
    ///   <item>If the file is in the legacy XML format, each field is read individually and
    ///         the file is immediately re-saved in the current format.</item>
    ///   <item>If the file is unreadable, a <c>.old</c> backup is attempted and a fresh
    ///         default config is written.</item>
    /// </list>
    /// After a successful load the server language is switched to the operator's
    /// configured locale.
    /// </summary>
    public static void LoadConfig(Server server)
    {
        string path = Path.Combine(GameStorePath.gamepathconfig, ConfigFilename);

        if (!File.Exists(path))
        {
            Console.WriteLine(server.language.ServerConfigNotFound());
            SaveConfig(server);
            return;
        }

        if (!TryLoadCurrentFormat(server, path))
        {
            TryBackupAndReset(server, path);
            return;
        }

        // Switch to the operator-defined locale now that config is populated
        server.language.OverrideLanguage = server.config.ServerLanguage;
        Console.WriteLine(server.language.ServerConfigLoaded());
    }

    /// <summary>
    /// Attempts to deserialize the config file using the current <see cref="XmlSerializer"/> format.
    /// </summary>
    /// <returns><c>true</c> on success; <c>false</c> if the file could not be parsed.</returns>
    private static bool TryLoadCurrentFormat(Server server, string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            server.config = JsonSerializer.Deserialize<ServerConfig>(json, JsonOptions)
                            ?? new ServerConfig();
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Called when both load attempts fail. Tries to copy the corrupt file to
    /// <c>ServerConfig.txt.old</c>, logs a message either way, then resets
    /// <see cref="Server.config"/> to <c>null</c> and writes a fresh default config.
    /// </summary>
    private static void TryBackupAndReset(Server server, string path)
    {
        try
        {
            File.Copy(path, path + ".old");
            Console.WriteLine(server.language.ServerConfigCorruptBackup());
        }
        catch
        {
            Console.WriteLine(server.language.ServerConfigCorruptNoBackup());
        }

        server.config = null;
        SaveConfig(server);
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serializes <see cref="Server.config"/> to <c>ServerConfig.txt</c> in the
    /// game config directory.
    /// <para>
    /// If <see cref="Server.config"/> is <c>null</c> (i.e. first run), the operator
    /// is prompted interactively on the console to supply basic server settings
    /// before the file is written.
    /// </para>
    /// </summary>
    public static void SaveConfig(Server server)
    {
        Directory.CreateDirectory(GameStorePath.gamepathconfig);

        if (server.config == null)
            server.config = CreateConfigInteractively(server);

        if (server.config.Areas.Count == 0)
            server.config.Areas = ServerConfigMisc.getDefaultAreas();

        File.WriteAllText(
            Path.Combine(GameStorePath.gamepathconfig, ConfigFilename),
            JsonSerializer.Serialize(server.config, JsonOptions));
    }

    // -------------------------------------------------------------------------
    // Interactive first-run setup
    // -------------------------------------------------------------------------

    /// <summary>
    /// Prompts the server operator via <see cref="Console"/> to configure basic
    /// server parameters on first start. All prompts are optional — pressing
    /// Enter without input leaves the default value in place.
    /// </summary>
    /// <returns>A new <see cref="ServerConfig"/> populated with the operator's choices.</returns>
    private static ServerConfig CreateConfigInteractively(Server server)
    {
        Language lang = server.language;

        var config = new ServerConfig
        {
            // Default to the host machine's locale
            ServerLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName
        };

        Console.WriteLine(lang.ServerSetupFirstStart());
        Console.WriteLine(lang.ServerSetupQuestion());

        if (!ReadAccept(lang))
            return config;

        config.Public = PromptBool(lang.ServerSetupPublic(), lang);
        config.Name = PromptString(lang.ServerSetupName());
        config.Motd = PromptString(lang.ServerSetupMOTD());
        config.WelcomeMessage = PromptString(lang.ServerSetupWelcomeMessage());
        config.Port = PromptPort(lang);
        config.MaxClients = PromptMaxClients(lang);
        config.EnableHTTPServer = PromptBool(lang.ServerSetupEnableHTTP(), lang);

        return config;
    }

    /// <summary>
    /// Reads a yes/no answer from the console and returns <c>true</c> if the
    /// input matches the localised accept word (case-insensitive).
    /// </summary>
    private static bool ReadAccept(Language lang)
    {
        string line = Console.ReadLine();
        return !string.IsNullOrEmpty(line) &&
               line.Equals(lang.ServerSetupAccept(), StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>Prompts for a boolean yes/no answer and returns the result.</summary>
    private static bool PromptBool(string prompt, Language lang)
    {
        Console.WriteLine(prompt);
        return ReadAccept(lang);
    }

    /// <summary>
    /// Prompts for a non-empty string. Returns <c>null</c> if the operator
    /// presses Enter without input (leaving the config default intact at the call site).
    /// </summary>
    private static string PromptString(string prompt)
    {
        Console.WriteLine(prompt);
        string line = Console.ReadLine();
        return string.IsNullOrEmpty(line) ? null : line;
    }

    /// <summary>
    /// Prompts the operator for a TCP port number in the range 1–65565.
    /// Returns the <see cref="ServerConfig"/> default if the input is empty,
    /// out of range, or non-numeric.
    /// </summary>
    private static int PromptPort(Language lang)
    {
        Console.WriteLine(lang.ServerSetupPort());
        string line = Console.ReadLine();
        if (string.IsNullOrEmpty(line)) return new ServerConfig().Port;

        if (int.TryParse(line, out int port))
        {
            if (port > 0 && port <= 65565)
                return port;
            Console.WriteLine(lang.ServerSetupPortInvalidValue());
        }
        else
        {
            Console.WriteLine(lang.ServerSetupPortInvalidInput());
        }
        return new ServerConfig().Port;
    }

    /// <summary>
    /// Prompts the operator for a maximum player count (must be &gt; 0).
    /// Returns the <see cref="ServerConfig"/> default if input is empty,
    /// non-positive, or non-numeric.
    /// </summary>
    private static int PromptMaxClients(Language lang)
    {
        Console.WriteLine(lang.ServerSetupMaxClients());
        string line = Console.ReadLine();
        if (string.IsNullOrEmpty(line)) return new ServerConfig().MaxClients;

        if (int.TryParse(line, out int players))
        {
            if (players > 0)
                return players;
            Console.WriteLine(lang.ServerSetupMaxClientsInvalidValue());
        }
        else
        {
            Console.WriteLine(lang.ServerSetupMaxClientsInvalidInput());
        }
        return new ServerConfig().MaxClients;
    }
}