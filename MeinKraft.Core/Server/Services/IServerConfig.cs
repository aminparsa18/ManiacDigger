/// <summary>
/// Contract for the server's runtime configuration. Covers networking, gameplay
/// rules, map dimensions, area permissions, and persistence flags.
/// Implemented by <see cref="ServerConfig"/>, which is serialised to and from XML.
/// </summary>
public interface IServerConfig
{
    /// <summary>XML format version. Used for forward-compatibility checks on load.</summary>
    int Format { get; set; }

    /// <summary>Human-readable name shown in the server browser.</summary>
    string Name { get; set; }

    /// <summary>Message of the day, broadcast to all players on connect.</summary>
    string Motd { get; set; }

    /// <summary>Message displayed privately to each player when they log in.</summary>
    string WelcomeMessage { get; set; }

    /// <summary>TCP/UDP port the server listens on. Default is <c>25565</c>.</summary>
    int Port { get; set; }

    /// <summary>Maximum number of simultaneously connected players.</summary>
    int MaxClients { get; set; }

    /// <summary>
    /// Interval in hours between automatic server restarts. <c>0</c> disables
    /// automatic restarts.
    /// </summary>
    int AutoRestartCycle { get; set; }

    /// <summary>Whether the built-in HTTP status monitor is enabled.</summary>
    bool ServerMonitor { get; set; }

    /// <summary>
    /// Seconds before a client that has connected but not yet authenticated is
    /// dropped.
    /// </summary>
    int ClientConnectionTimeout { get; set; }

    /// <summary>
    /// Seconds of inactivity before a playing client is considered timed out.
    /// </summary>
    int ClientPlayingTimeout { get; set; }

    /// <summary>Whether block placement and removal is written to the build log.</summary>
    bool BuildLogging { get; set; }

    /// <summary>Whether server events (joins, kicks, etc.) are written to a log file.</summary>
    bool ServerEventLogging { get; set; }

    /// <summary>Whether chat messages are written to a log file.</summary>
    bool ChatLogging { get; set; }

    /// <summary>Whether server-side Lua/scripting mods are permitted to run.</summary>
    bool AllowScripting { get; set; }

    /// <summary>
    /// GUID that uniquely identifies this server instance. Generated once on
    /// first run and persisted in the config file.
    /// </summary>
    string Key { get; set; }

    /// <summary>
    /// When <see langword="true"/> the server runs in creative (free-build) mode:
    /// players have unlimited blocks and no resource costs.
    /// </summary>
    bool IsCreative { get; set; }

    /// <summary>
    /// When <see langword="true"/> the server advertises itself to the public
    /// server browser.
    /// </summary>
    bool Public { get; set; }

    /// <summary>
    /// Optional password required to join. Empty or <see langword="null"/> means
    /// the server is open. See <see cref="IsPasswordProtected"/>.
    /// </summary>
    string Password { get; set; }

    /// <summary>Whether unauthenticated (guest) players are allowed to connect.</summary>
    bool AllowGuests { get; set; }

    /// <summary>Whether monster spawning is enabled.</summary>
    bool Monsters { get; set; }

    /// <summary>Map width in blocks along the X axis.</summary>
    int MapSizeX { get; set; }

    /// <summary>Map depth in blocks along the Y axis.</summary>
    int MapSizeY { get; set; }

    /// <summary>Map height in blocks along the Z axis.</summary>
    int MapSizeZ { get; set; }

    /// <summary>
    /// Protected and public build areas. Each entry restricts which groups or
    /// players may place blocks within a defined bounding box.
    /// </summary>
    List<AreaConfig> Areas { get; set; }

    /// <summary>
    /// Fixed world-generation seed. Only used when <see cref="RandomSeed"/> is
    /// <see langword="false"/>.
    /// </summary>
    int Seed { get; set; }

    /// <summary>
    /// When <see langword="true"/> the server picks a random seed each time a new
    /// world is generated, ignoring <see cref="Seed"/>.
    /// </summary>
    bool RandomSeed { get; set; }

    /// <summary>Whether the built-in HTTP API server is enabled.</summary>
    bool EnableHTTPServer { get; set; }

    /// <summary>Whether spectators are allowed to interact with the world (use items).</summary>
    bool AllowSpectatorUse { get; set; }

    /// <summary>Whether spectators are allowed to place or remove blocks.</summary>
    bool AllowSpectatorBuild { get; set; }

    /// <summary>
    /// BCP 47 language tag used to select server-side localisation strings
    /// (e.g. <c>"en"</c>, <c>"de"</c>).
    /// </summary>
    string ServerLanguage { get; set; }

    /// <summary>
    /// Maximum distance in blocks at which other players are visible and
    /// networked. Higher values increase bandwidth usage.
    /// </summary>
    int PlayerDrawDistance { get; set; }

    /// <summary>Whether players can physically push each other by walking into them.</summary>
    bool EnablePlayerPushing { get; set; }

    /// <summary>
    /// Whether the in-memory config differs from the persisted XML file and
    /// needs to be written to disk on the next save pass.
    /// </summary>
    bool ConfigNeedsSaving { get; set; }

    /// <summary>
    /// Returns <see langword="true"/> if a non-empty <see cref="Password"/> has
    /// been set, meaning players must supply the correct password to join.
    /// </summary>
    bool IsPasswordProtected();

    /// <summary>
    /// Returns <see langword="true"/> if <paramref name="client"/> is permitted to
    /// place or remove blocks at (<paramref name="x"/>, <paramref name="y"/>,
    /// <paramref name="z"/>) according to the configured <see cref="Areas"/> rules.
    /// </summary>
    /// <param name="client">The player attempting to build.</param>
    /// <param name="x">World-space X coordinate of the target block.</param>
    /// <param name="y">World-space Y coordinate of the target block.</param>
    /// <param name="z">World-space Z coordinate of the target block.</param>
    bool CanUserBuild(ServerPlayer client, int x, int y, int z);

    /// <summary>
    /// Copies all settings from <paramref name="source"/> into this instance.
    /// Used to apply a freshly deserialised config without replacing the
    /// registered singleton reference.
    /// </summary>
    /// <param name="source">The config object to copy values from.</param>
    void CopyFrom(ServerConfig source);
}