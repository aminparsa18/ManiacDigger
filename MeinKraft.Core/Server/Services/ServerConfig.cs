/// <summary>
/// Concrete implementation of <see cref="IServerConfig"/>. Serialised to and
/// from XML (typically <c>ServerConfig.xml</c>) so settings persist across
/// server restarts. Construct with defaults via <see cref="ServerConfig()"/>,
/// then call <see cref="CopyFrom"/> to apply a freshly deserialised config
/// without replacing the registered singleton reference.
/// </summary>
public class ServerConfig : IServerConfig
{
    /// <inheritdoc/>
    public int Format { get; set; }

    /// <inheritdoc/>
    public string Name { get; set; }

    /// <inheritdoc/>
    public string Motd { get; set; }

    /// <inheritdoc/>
    public string WelcomeMessage { get; set; }

    /// <inheritdoc/>
    public int Port { get; set; }

    /// <inheritdoc/>
    public int MaxClients { get; set; }

    /// <inheritdoc/>
    public int AutoRestartCycle { get; set; }

    /// <inheritdoc/>
    public bool ServerMonitor { get; set; }

    /// <inheritdoc/>
    public int ClientConnectionTimeout { get; set; }

    /// <inheritdoc/>
    public int ClientPlayingTimeout { get; set; }

    /// <inheritdoc/>
    public bool BuildLogging { get; set; }

    /// <inheritdoc/>
    public bool ServerEventLogging { get; set; }

    /// <inheritdoc/>
    public bool ChatLogging { get; set; }

    /// <inheritdoc/>
    public bool AllowScripting { get; set; }

    /// <inheritdoc/>
    public string Key { get; set; }

    /// <inheritdoc/>
    public bool IsCreative { get; set; }

    /// <inheritdoc/>
    public bool Public { get; set; }

    /// <inheritdoc/>
    public string Password { get; set; }

    /// <inheritdoc/>
    public bool AllowGuests { get; set; }

    /// <inheritdoc/>
    public bool Monsters { get; set; }

    /// <inheritdoc/>
    public int MapSizeX { get; set; }

    /// <inheritdoc/>
    public int MapSizeY { get; set; }

    /// <inheritdoc/>
    public int MapSizeZ { get; set; }

    /// <inheritdoc/>
    public List<AreaConfig> Areas { get; set; }

    /// <inheritdoc/>
    public int Seed { get; set; }

    /// <inheritdoc/>
    public bool RandomSeed { get; set; }

    /// <inheritdoc/>
    public bool EnableHTTPServer { get; set; }

    /// <inheritdoc/>
    public bool AllowSpectatorUse { get; set; }

    /// <inheritdoc/>
    public bool AllowSpectatorBuild { get; set; }

    /// <inheritdoc/>
    public string ServerLanguage { get; set; }

    /// <inheritdoc/>
    public int PlayerDrawDistance { get; set; }

    /// <inheritdoc/>
    public bool EnablePlayerPushing { get; set; }

    /// <inheritdoc/>
    public bool ConfigNeedsSaving { get; set; }

    /// <inheritdoc/>
    public bool IsPasswordProtected() => !string.IsNullOrEmpty(Password);

    /// <inheritdoc/>
    public bool CanUserBuild(ServerPlayer client, int x, int y, int z)
    {
        // TODO: replace with a spatial tree for O(log n) area lookup.
        foreach (AreaConfig area in Areas)
        {
            if (area.IsInCoords(x, y, z) && area.CanUserBuild(client))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Initialises a new <see cref="ServerConfig"/> with safe out-of-the-box
    /// defaults suitable for a typical public creative server.
    /// </summary>
    public ServerConfig()
    {
        Format = 1;
        Name = "Manic Digger server";
        Motd = "MOTD";
        WelcomeMessage = "Welcome to my Manic Digger server!";
        Port = 25565;
        MaxClients = 16;
        ServerMonitor = true;
        ClientConnectionTimeout = 600;
        ClientPlayingTimeout = 60;
        BuildLogging = false;
        ServerEventLogging = false;
        ChatLogging = false;
        AllowScripting = false;
        Key = Guid.NewGuid().ToString();
        IsCreative = true;
        Public = true;
        AllowGuests = true;
        Monsters = false;
        MapSizeX = 9984;
        MapSizeY = 9984;
        MapSizeZ = 128;
        Areas = [];
        AutoRestartCycle = 6;
        Seed = 0;
        RandomSeed = true;
        EnableHTTPServer = false;
        AllowSpectatorUse = false;
        AllowSpectatorBuild = false;
        ServerLanguage = "en";
        PlayerDrawDistance = 128;
        EnablePlayerPushing = true;
    }

    /// <inheritdoc/>
    public void CopyFrom(ServerConfig source)
    {
        Format = source.Format;
        Name = source.Name;
        Motd = source.Motd;
        WelcomeMessage = source.WelcomeMessage;
        Port = source.Port;
        MaxClients = source.MaxClients;
        AutoRestartCycle = source.AutoRestartCycle;
        ServerMonitor = source.ServerMonitor;
        ClientConnectionTimeout = source.ClientConnectionTimeout;
        ClientPlayingTimeout = source.ClientPlayingTimeout;
        BuildLogging = source.BuildLogging;
        ServerEventLogging = source.ServerEventLogging;
        ChatLogging = source.ChatLogging;
        AllowScripting = source.AllowScripting;
        Key = source.Key;
        IsCreative = source.IsCreative;
        Public = source.Public;
        Password = source.Password;
        AllowGuests = source.AllowGuests;
        Monsters = source.Monsters;
        MapSizeX = source.MapSizeX;
        MapSizeY = source.MapSizeY;
        MapSizeZ = source.MapSizeZ;
        Areas = source.Areas;
        Seed = source.Seed;
        RandomSeed = source.RandomSeed;
        EnableHTTPServer = source.EnableHTTPServer;
        AllowSpectatorUse = source.AllowSpectatorUse;
        AllowSpectatorBuild = source.AllowSpectatorBuild;
        ServerLanguage = source.ServerLanguage;
        PlayerDrawDistance = source.PlayerDrawDistance;
        EnablePlayerPushing = source.EnablePlayerPushing;
    }
}

/// <summary>
/// Defines a rectangular bounding box within the world in which build permissions
/// are restricted to specific groups or players. Areas are evaluated in order;
/// the first matching area that grants permission wins.
/// </summary>
public class AreaConfig
{
    /// <summary>Unique numeric identifier for this area. <c>-1</c> means unassigned.</summary>
    public int Id { get; set; }

    private int x1, x2, y1, y2, z1, z2;
    private string coords;

    /// <summary>
    /// Names of groups whose members are unconditionally allowed to build in this
    /// area, regardless of their group level.
    /// </summary>
    public List<string> PermittedGroups { get; set; }

    /// <summary>
    /// Usernames of individual players who are unconditionally allowed to build
    /// in this area, regardless of their group.
    /// </summary>
    public List<string> PermittedUsers { get; set; }

    /// <summary>
    /// Minimum group level required to build in this area. Players whose group
    /// level is greater than or equal to this value are permitted even if their
    /// group or username is not explicitly listed.
    /// <see langword="null"/> means no level-based bypass is applied.
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// Initialises a new <see cref="AreaConfig"/> with default coordinates
    /// (origin to origin) and empty permission lists.
    /// </summary>
    public AreaConfig()
    {
        Id = -1;
        Coords = "0,0,0,0,0,0";
        PermittedGroups = [];
        PermittedUsers = [];
    }

    /// <summary>
    /// Gets or sets the bounding box as a comma-separated string in the format
    /// <c>x1,y1,z1,x2,y2,z2</c>. Setting this property immediately parses and
    /// caches the six individual coordinate values.
    /// </summary>
    public string Coords
    {
        get => coords;
        set
        {
            coords = value;
            string[] parts = value.Split(',');
            x1 = Convert.ToInt32(parts[0]);
            x2 = Convert.ToInt32(parts[3]);
            y1 = Convert.ToInt32(parts[1]);
            y2 = Convert.ToInt32(parts[4]);
            z1 = Convert.ToInt32(parts[2]);
            z2 = Convert.ToInt32(parts[5]);
        }
    }

    /// <summary>
    /// Returns <see langword="true"/> if the point
    /// (<paramref name="x"/>, <paramref name="y"/>, <paramref name="z"/>) lies
    /// within or on the boundary of this area.
    /// </summary>
    public bool IsInCoords(int x, int y, int z)
        => x >= x1 && x <= x2 && y >= y1 && y <= y2 && z >= z1 && z <= z2;

    /// <summary>
    /// Returns <see langword="true"/> if the axis-aligned bounding box defined by
    /// the given min/max corners is entirely contained within this area.
    /// </summary>
    public bool ContainsBox(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
        => minX >= x1 && maxX <= x2 && minY >= y1 && maxY <= y2 && minZ >= z1 && maxZ <= z2;

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="client"/> is permitted to
    /// build in this area based on group level, group name, or explicit username.
    /// </summary>
    /// <param name="client">The player requesting build permission.</param>
    public bool CanUserBuild(ServerPlayer client)
    {
        if (Level != null && client.ClientGroup.Level >= Level)
            return true;

        if (PermittedGroups.Contains(client.ClientGroup.Name))
            return true;

        if (PermittedUsers.Any(u => u.Equals(client.PlayerName, StringComparison.InvariantCultureIgnoreCase)))
            return true;

        return false;
    }

    /// <summary>
    /// Returns a compact string representation of this area in the format
    /// <c>id:coords:groups:users:level</c>, suitable for logging.
    /// </summary>
    public override string ToString()
    {
        string groups = string.Join(",", PermittedGroups);
        string users = string.Join(",", PermittedUsers);
        string level = Level?.ToString() ?? "";
        return $"{Id}:{Coords}:{groups}:{users}:{level}";
    }

    /// <summary>
    /// Returns the default two-area setup used when no <c>ServerConfig.xml</c>
    /// exists: a public guest-accessible zone covering the lower half of the map,
    /// and a protected builder/moderator/admin zone covering the full map height.
    /// </summary>
    public static List<AreaConfig> GetDefaultAreas()
    {
        AreaConfig publicArea = new() { Id = 1, Coords = "0,0,1,9984,5000,128" };
        publicArea.PermittedGroups.Add("Guest");

        AreaConfig protectedArea = new() { Id = 2, Coords = "0,0,1,9984,9984,128" };
        protectedArea.PermittedGroups.Add("Builder");
        protectedArea.PermittedGroups.Add("Moderator");
        protectedArea.PermittedGroups.Add("Admin");

        return [publicArea, protectedArea];
    }
}