using MeinKraft;
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
    private readonly ILanguageService _languageService;
    private readonly IServerConfig _serverConfig;
    private readonly Server server;

    public ServerSystemLoadConfig(Server server, IModEvents modEvents, ILanguageService languageService, IServerConfig serverConfig) : base(modEvents)
    {
        this.server = server;
        _languageService = languageService;
        _serverConfig = serverConfig;
    }

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads the configuration once on startup, then flushes any pending save
    /// on subsequent ticks when <see cref="Server.ConfigNeedsSaving"/> is set.
    /// </summary>
    protected override void Initialize()
    {
        LoadConfig();
        server.OnConfigLoaded();
    }

    /// <inheritdoc/>
    protected override void OnUpdate(Server server, float dt)
    {
        if (_serverConfig.ConfigNeedsSaving)
        {
            _serverConfig.ConfigNeedsSaving = false;
            SaveConfig();
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
    public void LoadConfig()
    {
        string path = Path.Combine(GameStorePath.gamepathconfig, ConfigFilename);

        if (!File.Exists(path))
        {
            Console.WriteLine(_languageService.ServerConfigNotFound());
            SaveConfig();
            return;
        }

        if (!TryLoadCurrentFormat(path))
        {
            TryBackupAndReset(path);
            return;
        }

        // Switch to the operator-defined locale now that config is populated
        _languageService.OverrideLanguage = _serverConfig.ServerLanguage;
        Console.WriteLine(_languageService.ServerConfigLoaded());
    }

    /// <summary>
    /// Attempts to deserialize the config file using the current <see cref="XmlSerializer"/> format.
    /// </summary>
    /// <returns><c>true</c> on success; <c>false</c> if the file could not be parsed.</returns>
    private bool TryLoadCurrentFormat(string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            ServerConfig loaded = JsonSerializer.Deserialize<ServerConfig>(json, JsonOptions)
                                  ?? new ServerConfig();
            _serverConfig.CopyFrom(loaded);   // mutate in place — DI consumers see the update
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
    /// <see cref="Server._config"/> to <c>null</c> and writes a fresh default config.
    /// </summary>
    private void TryBackupAndReset( string path)
    {
        try
        {
            File.Copy(path, $"{path}.old");
            Console.WriteLine(_languageService.ServerConfigCorruptBackup());
        }
        catch
        {
            Console.WriteLine(_languageService.ServerConfigCorruptNoBackup());
        }

        _serverConfig.CopyFrom(new ServerConfig());
        SaveConfig();
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serializes <see cref="Server._config"/> to <c>ServerConfig.txt</c> in the
    /// game config directory.
    /// <para>
    /// If <see cref="Server._config"/> is <c>null</c> (i.e. first run), the operator
    /// is prompted interactively on the console to supply basic server settings
    /// before the file is written.
    /// </para>
    /// </summary>
    public void SaveConfig()
    {
        Directory.CreateDirectory(GameStorePath.gamepathconfig);

        // First run — no config file exists yet. Prompt the operator and
        // copy the answers into the live DI-registered instance.
        if (!File.Exists(Path.Combine(GameStorePath.gamepathconfig, ConfigFilename)))
        {
            _serverConfig.CopyFrom(CreateConfigInteractively());
        }

        if (_serverConfig.Areas.Count == 0)
        {
            _serverConfig.Areas = AreaConfig.GetDefaultAreas();
        }

        File.WriteAllText(
            Path.Combine(GameStorePath.gamepathconfig, ConfigFilename),
            JsonSerializer.Serialize(_serverConfig, JsonOptions));
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
    private ServerConfig CreateConfigInteractively()
    {
        ServerConfig config = new()
        {
            // Default to the host machine's locale
            ServerLanguage = CultureInfo.CurrentCulture.TwoLetterISOLanguageName
        };

        Console.WriteLine(_languageService.ServerSetupFirstStart());
        Console.WriteLine(_languageService.ServerSetupQuestion());

        if (!ReadAccept())
        {
            return config;
        }

        config.Public = PromptBool(_languageService.ServerSetupPublic());
        config.Name = PromptString(_languageService.ServerSetupName());
        config.Motd = PromptString(_languageService.ServerSetupMOTD());
        config.WelcomeMessage = PromptString(_languageService.ServerSetupWelcomeMessage());
        config.Port = PromptPort();
        config.MaxClients = PromptMaxClients();
        config.EnableHTTPServer = PromptBool(_languageService.ServerSetupEnableHTTP());

        return config;
    }

    /// <summary>
    /// Reads a yes/no answer from the console and returns <c>true</c> if the
    /// input matches the localised accept word (case-insensitive).
    /// </summary>
    private bool ReadAccept()
    {
        string line = Console.ReadLine();
        return !string.IsNullOrEmpty(line) &&
               line.Equals(_languageService.ServerSetupAccept(), StringComparison.InvariantCultureIgnoreCase);
    }

    /// <summary>Prompts for a boolean yes/no answer and returns the result.</summary>
    private bool PromptBool(string prompt)
    {
        Console.WriteLine(prompt);
        return ReadAccept();
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
    private int PromptPort()
    {
        Console.WriteLine(_languageService.ServerSetupPort());
        string line = Console.ReadLine();
        if (string.IsNullOrEmpty(line))
        {
            return new ServerConfig().Port;
        }

        if (int.TryParse(line, out int port))
        {
            if (port is > 0 and <= 65565)
            {
                return port;
            }

            Console.WriteLine(_languageService.ServerSetupPortInvalidValue());
        }
        else
        {
            Console.WriteLine(_languageService.ServerSetupPortInvalidInput());
        }

        return new ServerConfig().Port;
    }

    /// <summary>
    /// Prompts the operator for a maximum player count (must be &gt; 0).
    /// Returns the <see cref="ServerConfig"/> default if input is empty,
    /// non-positive, or non-numeric.
    /// </summary>
    private int PromptMaxClients()
    {
        Console.WriteLine(_languageService.ServerSetupMaxClients());
        string line = Console.ReadLine();
        if (string.IsNullOrEmpty(line))
        {
            return new ServerConfig().MaxClients;
        }

        if (int.TryParse(line, out int players))
        {
            if (players > 0)
            {
                return players;
            }

            Console.WriteLine(_languageService.ServerSetupMaxClientsInvalidValue());
        }
        else
        {
            Console.WriteLine(_languageService.ServerSetupMaxClientsInvalidInput());
        }

        return new ServerConfig().MaxClients;
    }
}