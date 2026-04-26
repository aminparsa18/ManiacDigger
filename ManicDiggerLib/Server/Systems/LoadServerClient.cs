using ManicDigger;
using OpenTK.Mathematics;
using System.Text.Json;
using System.Text.Json.Serialization;

/// <summary>
/// Server system responsible for loading and saving <c>ServerClient.txt</c>, which
/// stores player groups, registered client entries, and the default world spawn point.
/// <para>
/// On first run, if no file exists, defaults are generated and written to disk.
/// Like <c>ServerConfig.txt</c>, the file supports a legacy XML format that is
/// automatically upgraded to the current serializer format on load.
/// </para>
/// </summary>
public class ServerSystemLoadServerClient : ServerSystem
{
    private const string ClientFilename = "ServerClient.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    // -------------------------------------------------------------------------
    // Lifecycle
    // -------------------------------------------------------------------------

    /// <inheritdoc/>
    protected override void Initialize(Server server)
    {
        LoadServerClient(server);
    }

    /// <inheritdoc/>
    protected override void OnUpdate(Server server, float dt)
    {
        if (server.serverClientNeedsSaving)
        {
            server.serverClientNeedsSaving = false;
            SaveServerClient(server);
        }
    }

    // -------------------------------------------------------------------------
    // Load
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads <c>ServerClient.txt</c> from the game config directory, then resolves
    /// the default player spawn point and the default guest/registered groups.
    /// <list type="bullet">
    ///   <item>If the file does not exist, defaults are written and the method continues.</item>
    ///   <item>If the file uses the current XML format it is deserialized directly.</item>
    ///   <item>If the file uses the legacy format, known fields are read via XPath and
    ///         the file is immediately re-saved in the current format.</item>
    /// </list>
    /// </summary>
    /// <exception cref="Exception">
    /// Thrown if the guest or registered default group name in the config does not
    /// match any group defined in <c>ServerClient.txt</c>.
    /// </exception>
    public static void LoadServerClient(Server server)
    {
        string path = Path.Combine(GameStorePath.gamepathconfig, ClientFilename);

        if (!File.Exists(path))
        {
            Console.WriteLine(server.language.ServerClientConfigNotFound());
            SaveServerClient(server);
        }
        else
        {
            TryLoadCurrentFormat(server, path);
        }

        ResolveSpawn(server);
        ResolveDefaultGroups(server);
        Console.WriteLine(server.language.ServerClientConfigLoaded());
    }

    /// <summary>
    /// Attempts to deserialize <c>ServerClient.txt</c> using the current
    /// <see cref="XmlSerializer"/> format. Groups are sorted after a successful load.
    /// </summary>
    /// <returns><c>true</c> on success; <c>false</c> if deserialization fails.</returns>
    private static bool TryLoadCurrentFormat(Server server, string path)
    {
        try
        {
            string json = File.ReadAllText(path);
            server.serverClient = JsonSerializer.Deserialize<ServerClient>(json, JsonOptions)
                                  ?? new ServerClient();
            server.serverClient.Groups.Sort();
            SaveServerClient(server);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // -------------------------------------------------------------------------
    // Spawn resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves <see cref="Server.defaultPlayerSpawn"/> from the loaded config.
    /// <list type="bullet">
    ///   <item>If no spawn is configured, the spawn is placed at the surface above
    ///         the centre of the map.</item>
    ///   <item>If a spawn is configured but has no Z component, the surface height
    ///         at the (X, Y) position is used.</item>
    ///   <item>If all three coordinates are present, they are used verbatim.</item>
    /// </list>
    /// When no configured spawn exists, <see cref="Server.DontSpawnPlayerInWater"/>
    /// is applied to push the position above any water surface.
    /// </summary>
    private static void ResolveSpawn(Server server)
    {
        if (server.serverClient.DefaultSpawn == null)
        {
            int x = server.d_Map.MapSizeX / 2;
            int y = server.d_Map.MapSizeY / 2;
            int z = MapUtil.blockheight(server.d_Map, 0, x, y);
            server.defaultPlayerSpawn = server.DontSpawnPlayerInWater(new Vector3i(x, y, z));
            return;
        }

        var spawn = server.serverClient.DefaultSpawn;
        int spawnZ = spawn.z ?? MapUtil.blockheight(server.d_Map, 0, spawn.x, spawn.y);
        server.defaultPlayerSpawn = new Vector3i(spawn.x, spawn.y, spawnZ);
    }

    // -------------------------------------------------------------------------
    // Group resolution
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves <see cref="Server.defaultGroupGuest"/> and
    /// <see cref="Server.defaultGroupRegistered"/> by matching the group names
    /// stored in the config against the loaded group list.
    /// </summary>
    /// <exception cref="Exception">
    /// Thrown if either group name cannot be found, indicating a misconfigured
    /// <c>ServerClient.txt</c>.
    /// </exception>
    private static void ResolveDefaultGroups(Server server)
    {
        server.defaultGroupGuest = server.serverClient.Groups
            .Find(g => g.Name.Equals(server.serverClient.DefaultGroupGuests))
            ?? throw new Exception(server.language.ServerClientConfigGuestGroupNotFound());

        server.defaultGroupRegistered = server.serverClient.Groups
            .Find(g => g.Name.Equals(server.serverClient.DefaultGroupRegistered))
            ?? throw new Exception(server.language.ServerClientConfigRegisteredGroupNotFound());
    }

    // -------------------------------------------------------------------------
    // Save
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serializes <see cref="Server.serverClient"/> to <c>ServerClient.txt</c>.
    /// If the object has not yet been initialized, or if its groups or client
    /// lists are empty, defaults are populated before saving.
    /// </summary>
    public static void SaveServerClient(Server server)
    {
        Directory.CreateDirectory(GameStorePath.gamepathconfig);

        server.serverClient ??= new ServerClient();

        if (server.serverClient.Groups.Count == 0)
            server.serverClient.Groups = ServerClientMisc.getDefaultGroups();

        if (server.serverClient.Clients.Count == 0)
            server.serverClient.Clients = ServerClientMisc.getDefaultClients();

        server.serverClient.Clients.Sort();

        File.WriteAllText(
            Path.Combine(GameStorePath.gamepathconfig, ClientFilename),
            JsonSerializer.Serialize(server.serverClient, JsonOptions));
    }
}