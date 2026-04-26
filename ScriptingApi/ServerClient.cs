using System.Text.Json.Serialization;

namespace ManicDigger;

/// <summary>
/// Root configuration object for the server's client/group management system.
/// Serialized to and from XML (typically ServerClient.xml) to persist player groups,
/// registered clients, and spawn/fill-limit defaults across server restarts.
/// </summary>
public class ServerClient
{
    /// <summary>
    /// XML format version of this file. Used for forward-compatibility checks
    /// when loading older configs. Current version is 1.
    /// </summary>
    public int Format { get; set; }

    /// <summary>
    /// Name of the <see cref="Group"/> that unauthenticated (guest) players are
    /// assigned to when they connect. Defaults to "Guest".
    /// </summary>
    public string DefaultGroupGuests { get; set; }

    /// <summary>
    /// Name of the <see cref="Group"/> that players with a registered Manic Digger
    /// account are placed in. Defaults to "Guest" unless the server operator
    /// promotes registered players to a higher group.
    /// </summary>
    public string DefaultGroupRegistered { get; set; }

    /// <summary>
    /// All permission groups defined on this server (e.g. Guest, Builder,
    /// Moderator, Admin). Groups are ordered by <see cref="Group.Level"/> so
    /// higher-level groups have more privileges.
    /// </summary>
    public List<Group> Groups { get; set; }

    /// <summary>
    /// Per-player overrides. Each entry pins a named player to a specific group
    /// and can optionally override their spawn point and fill limit.
    /// Players not listed here fall back to <see cref="DefaultGroupGuests"/> or
    /// <see cref="DefaultGroupRegistered"/>.
    /// </summary>
    public List<Client> Clients { get; set; }

    /// <summary>
    /// Server-wide default spawn location. Applied to any player or group that
    /// does not have their own <see cref="Spawn"/> override. Null means the
    /// server uses its own built-in default spawn logic.
    /// </summary>
    public Spawn DefaultSpawn { get; set; }

    /// <summary>
    /// Server-wide default fill limit — the maximum number of blocks a player
    /// may place in a single fill/flood-fill operation. Individual groups and
    /// clients can override this. Null means no limit is enforced globally.
    /// </summary>
    public int? DefaultFillLimit { get; set; }

    /// <summary>
    /// Initialises a new <see cref="ServerClient"/> with safe defaults:
    /// format version 1, both default groups set to "Guest", and empty
    /// group/client lists ready to be populated.
    /// </summary>
    public ServerClient()
    {
        this.Format = 1;
        this.DefaultGroupGuests = "Guest";
        this.DefaultGroupRegistered = "Guest";
        this.Groups = [];
        this.Clients = [];
    }
}

/// <summary>
/// Defines a permission tier on the server. Players belong to exactly one group,
/// which determines what actions they are allowed to perform via
/// <see cref="GroupPrivileges"/>.
/// Groups are comparable and sortable by <see cref="Level"/> (ascending),
/// so a higher level always beats a lower one in privilege checks.
/// </summary>
public class Group : IComparable<Group>
{
    /// <summary>
    /// Human-readable name shown in chat, command output, and the HTTP status page.
    /// Must be unique across all groups on the server.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Numeric rank of this group. Higher values mean more authority.
    /// Used by <see cref="IsSuperior"/> and <see cref="CompareTo"/> to
    /// determine whether one group outranks another.
    /// Example: Guest=0, Builder=1, Moderator=2, Admin=3.
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Optional password required to self-assign this group via the /login command.
    /// Null or empty means the group cannot be joined by password — it must be
    /// assigned manually by an admin.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// Optional spawn point override for all members of this group.
    /// Takes precedence over <see cref="ServerClient.DefaultSpawn"/> but is
    /// overridden by a per-<see cref="Client"/> spawn if one exists.
    /// </summary>
    public Spawn Spawn { get; set; }

    /// <summary>
    /// Optional fill-limit override for this group. Overrides
    /// <see cref="ServerClient.DefaultFillLimit"/>. Null means fall back to the
    /// server default.
    /// </summary>
    public int? FillLimit { get; set; }

    /// <summary>
    /// List of privilege strings that members of this group are granted.
    /// Use constants from <see cref="ServerClientMisc.Privilege"/> to avoid typos.
    /// Example: <c>new List&lt;string&gt; { Privilege.build, Privilege.chat }</c>
    /// </summary>
    public List<string> GroupPrivileges { get; set; }

    /// <summary>
    /// Colour used to display this group's players in chat.
    /// Serialised as a <see cref="ServerClientMisc.ClientColor"/> enum value and
    /// converted to a Minecraft-style colour code via
    /// <see cref="ServerClientMisc.ClientColorToString"/>.
    /// </summary>
    public ServerClientMisc.ClientColor GroupColor { get; set; }

    /// <summary>
    /// Initialises a new <see cref="Group"/> with empty name, level 0,
    /// no privileges, and white text colour.
    /// </summary>
    public Group()
    {
        this.Name = "";
        this.Level = 0;
        this.GroupPrivileges = [];
        this.GroupColor = ServerClientMisc.ClientColor.White;
    }

    /// <summary>
    /// Returns the Minecraft-style colour code string for this group's chat colour
    /// (e.g. <c>"&amp;2"</c> for Green). Delegates to
    /// <see cref="ServerClientMisc.ClientColorToString"/>.
    /// </summary>
    public string GroupColorString()
    {
        return ServerClientMisc.ClientColorToString(this.GroupColor);
    }

    /// <summary>
    /// Compares this group to another by <see cref="Level"/> (ascending).
    /// Used when sorting a list of groups from lowest to highest authority.
    /// </summary>
    public int CompareTo(Group other)
    {
        return Level.CompareTo(other.Level);
    }

    /// <summary>
    /// Returns <c>true</c> if this group's <see cref="Level"/> is strictly
    /// greater than <paramref name="clientGroup"/>'s level — i.e. this group
    /// has more authority and can moderate members of the other group.
    /// </summary>
    public bool IsSuperior(Group clientGroup)
    {
        return this.Level > clientGroup.Level;
    }

    /// <summary>
    /// Returns <c>true</c> if both groups share the same <see cref="Level"/>.
    /// Equal-level groups cannot moderate each other.
    /// </summary>
    public bool EqualLevel(Group clientGroup)
    {
        return this.Level == clientGroup.Level;
    }

    /// <summary>
    /// Returns a compact debug string: <c>Name:Level:Privileges:Color:PasswordFlag</c>.
    /// The password field shows "X" if no password is set, or is blank if one is set
    /// (the actual password is never exposed).
    /// </summary>
    public override string ToString()
    {
        string passwordString = "";
        if (string.IsNullOrEmpty(this.Password))
        {
            passwordString = "X";
        }
        return $"{this.Name}:{this.Level}:{ServerClientMisc.PrivilegesString(this.GroupPrivileges)}:{this.GroupColor.ToString()}:{passwordString}";
    }
}

/// <summary>
/// Represents a known/registered player entry in the server config.
/// Allows the server operator to pin a specific player to a group and
/// optionally override their spawn point and fill limit independently
/// of the group-level settings.
/// Clients are sorted by group name for display purposes.
/// </summary>
public class Client : IComparable<Client>
{
    /// <summary>
    /// The player's in-game username. Must match exactly (case-sensitive) for
    /// the server to apply this entry's overrides when the player connects.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Name of the <see cref="Group"/> this player is permanently assigned to,
    /// overriding the server's <see cref="ServerClient.DefaultGroupGuests"/> /
    /// <see cref="ServerClient.DefaultGroupRegistered"/> defaults.
    /// </summary>
    public string Group { get; set; }

    /// <summary>
    /// Optional per-player spawn point. Takes precedence over both the group
    /// spawn and <see cref="ServerClient.DefaultSpawn"/>.
    /// </summary>
    public Spawn Spawn { get; set; }

    /// <summary>
    /// Optional per-player fill limit, overriding both the group fill limit and
    /// <see cref="ServerClient.DefaultFillLimit"/>.
    /// </summary>
    public int? FillLimit { get; set; }

    /// <summary>
    /// Initialises a new <see cref="Client"/> with empty name and group strings.
    /// </summary>
    public Client()
    {
        this.Name = "";
        this.Group = "";
    }

    /// <summary>
    /// Returns a compact debug string: <c>Name:Group</c>.
    /// </summary>
    public override string ToString()
    {
        return $"{this.Name}:{this.Group}";
    }

    /// <summary>
    /// Compares this client to another by group name (alphabetical).
    /// Used when sorting the client list for display in admin commands.
    /// </summary>
    public int CompareTo(Client other)
    {
        return Group.CompareTo(other.Group);
    }
}

/// <summary>
/// Represents a world-space spawn coordinate.
/// X and Y are required; Z (height) is optional and defaults to null,
/// meaning the server will place the player at the surface.
/// Coordinates are serialised as a single comma-separated string
/// (e.g. <c>"128,64"</c> or <c>"128,64,32"</c>) via the <see cref="Coords"/> property
/// so XML storage stays compact and human-readable.
/// </summary>
public class Spawn
{
    /// <remarks>Stored as <see cref="JsonIgnore"/> fields because serialisation
    /// goes through the <see cref="Coords"/> string property instead.</remarks>
    [JsonIgnore] public int x;
    [JsonIgnore] public int y;
    /// <summary>Optional height (Z axis). Null means use surface level.</summary>
    [JsonIgnore] public int? z;

    /// <summary>
    /// Gets or sets the spawn position as a comma-separated string.
    /// <para>Get: returns <c>"x,y"</c> or <c>"x,y,z"</c> depending on whether
    /// <see cref="z"/> is set.</para>
    /// <para>Set: parses the string back into <see cref="x"/>, <see cref="y"/>,
    /// and optionally <see cref="z"/>. Throws <see cref="FormatException"/> or
    /// <see cref="IndexOutOfRangeException"/> on malformed input so the caller can
    /// surface a meaningful config-load error rather than silently using 0,0.</para>
    /// </summary>
    public string Coords
    {
        get
        {
            string zString = "";
            if (this.z != null)
            {
                zString = $",{this.z}";
            }
            return $"{this.x},{this.y}{zString}";
        }
        set
        {
            string coords = value;
            string[] ss = coords.Split([',']);

            try
            {
                this.x = Convert.ToInt32(ss[0]);
                this.y = Convert.ToInt32(ss[1]);
            }
            catch (FormatException e)
            {
                throw new FormatException("Invalid spawn position.", e);
            }
            catch (OverflowException e)
            {
                throw new FormatException("Invalid spawn position.", e);
            }
            catch (IndexOutOfRangeException ex)
            {
                throw new IndexOutOfRangeException("Invalid spawn position.", ex);
            }

            try
            {
                this.z = Convert.ToInt32(ss[2]);
            }
            catch (IndexOutOfRangeException)
            {
                // Z component is optional — no third segment means no height override
                this.z = null;
            }
            catch (FormatException e)
            {
                throw new FormatException("Invalid spawn position.", e);
            }
            catch (OverflowException e)
            {
                throw new FormatException("Invalid spawn position.", e);
            }
        }
    }

    /// <summary>
    /// Initialises a new <see cref="Spawn"/> at the world origin (0, 0, no Z).
    /// </summary>
    public Spawn()
    {
        this.x = 0;
        this.y = 0;
    }

    /// <inheritdoc cref="Coords"/>
    public override string ToString()
    {
        return this.Coords;
    }
}

/// <summary>
/// Static helpers and nested types shared across the server client system:
/// colour codes, privilege name constants, and default group/client factories.
/// </summary>
public class ServerClientMisc
{
    /// <summary>
    /// Placeholder username shown in the default client entry before a real
    /// player name is configured.
    /// </summary>
    public static string DefaultPlayerName = "Player name?";

    /// <summary>
    /// Chat colour palette supported by the Manic Digger client.
    /// Maps to Minecraft-style <c>&amp;0</c>–<c>&amp;f</c> colour codes via
    /// <see cref="ClientColorToString"/>.
    /// </summary>
    public enum ClientColor
    {
        Black,       // &0
        Blue,        // &1
        Green,       // &2
        Cyan,        // &3
        Red,         // &4
        Purple,      // &5
        Yellow,      // &6
        Grey,        // &7
        DarkGrey,    // &8
        LightBlue,   // &9
        LightGreen,  // &a
        LightCyan,   // &b
        LightRed,    // &c
        LightPink,   // &d
        LightYellow, // &e
        White        // &f
    };

    /// <summary>
    /// Centralised list of all recognised privilege strings.
    /// Use these constants (e.g. <c>Privilege.build</c>) everywhere instead of
    /// raw strings to prevent silent typo bugs.
    /// <see cref="All"/> returns every privilege in one array, useful when
    /// initialising a superadmin group or validating user input.
    /// </summary>
    public class Privilege
    {
        /// <summary>Returns an array of every defined privilege string.</summary>
        public static string[] All()
        {
            return
            [
                build, use, freemove, chat, pm,
                kick, kick_id, ban, ban_id, ban_id,
                banip, banip_id, ban_offline, unban,
                run, chgrp, chgrp_offline, remove_client,
                login, welcome, logging,
                list_clients, list_saved_clients, list_groups,
                list_banned_users, list_areas,
                give, giveall, monsters,
                area_add, area_delete, announcement,
                set_spawn, set_home, use_tnt,
                privilege_add, privilege_remove,
                restart, shutdown,
                tp, tp_pos, teleport_player,
                backup_database, reset_inventory,
                fill_limit, mode, load, time,
            ];
        }

        /// <summary>Place and destroy blocks in the world.</summary>
        public static string build = "build";
        /// <summary>Interact with objects (doors, buttons, etc.).</summary>
        public static string use = "use";
        /// <summary>Enable free/fly movement mode.</summary>
        public static string freemove = "freemove";
        /// <summary>Send public chat messages.</summary>
        public static string chat = "chat";
        /// <summary>Send private messages to other players.</summary>
        public static string pm = "pm";
        /// <summary>Kick an online player by name.</summary>
        public static string kick = "kick";
        /// <summary>Kick an online player by connection ID.</summary>
        public static string kick_id = "kick_id";
        /// <summary>Ban a player by name.</summary>
        public static string ban = "ban";
        /// <summary>Ban a player by connection ID.</summary>
        public static string ban_id = "ban_id";
        /// <summary>Ban a player's IP address by name.</summary>
        public static string banip = "banip";
        /// <summary>Ban a player's IP address by connection ID.</summary>
        public static string banip_id = "banip_id";
        /// <summary>Ban a player who is currently offline.</summary>
        public static string ban_offline = "ban_offline";
        /// <summary>Lift an existing ban.</summary>
        public static string unban = "unban";
        /// <summary>Execute server-side scripts or run commands.</summary>
        public static string run = "run";
        /// <summary>Change the group of an online player.</summary>
        public static string chgrp = "chgrp";
        /// <summary>Change the group of an offline player.</summary>
        public static string chgrp_offline = "chgrp_offline";
        /// <summary>Remove a client entry from the server config.</summary>
        public static string remove_client = "remove_client";
        /// <summary>Authenticate to a password-protected group via /login.</summary>
        public static string login = "login";
        /// <summary>Set or change the server welcome message.</summary>
        public static string welcome = "welcome";
        /// <summary>Access server logs.</summary>
        public static string logging = "logging";
        /// <summary>List currently connected players.</summary>
        public static string list_clients = "list_clients";
        /// <summary>List all players saved in the server config.</summary>
        public static string list_saved_clients = "list_saved_clients";
        /// <summary>List all defined permission groups.</summary>
        public static string list_groups = "list_groups";
        /// <summary>View the ban list.</summary>
        public static string list_banned_users = "list_banned_users";
        /// <summary>List all defined world areas/zones.</summary>
        public static string list_areas = "list_areas";
        /// <summary>Give items to a specific player.</summary>
        public static string give = "give";
        /// <summary>Give items to all connected players.</summary>
        public static string giveall = "giveall";
        /// <summary>Control monster spawning.</summary>
        public static string monsters = "monsters";
        /// <summary>Create a new protected area/zone.</summary>
        public static string area_add = "area_add";
        /// <summary>Delete an existing protected area/zone.</summary>
        public static string area_delete = "area_delete";
        /// <summary>Broadcast a server-wide announcement.</summary>
        public static string announcement = "announcement";
        /// <summary>Set the global or group spawn point.</summary>
        public static string set_spawn = "set_spawn";
        /// <summary>Set the player's personal home spawn.</summary>
        public static string set_home = "set_home";
        /// <summary>Use TNT explosives.</summary>
        public static string use_tnt = "use_tnt";
        /// <summary>Grant a privilege to a group or player.</summary>
        public static string privilege_add = "privilege_add";
        /// <summary>Revoke a privilege from a group or player.</summary>
        public static string privilege_remove = "privilege_remove";
        /// <summary>Restart the server.</summary>
        public static string restart = "restart";
        /// <summary>Shut the server down cleanly.</summary>
        public static string shutdown = "shutdown";
        /// <summary>Teleport yourself to another player.</summary>
        public static string tp = "tp";
        /// <summary>Teleport yourself to specific world coordinates.</summary>
        public static string tp_pos = "tp_pos";
        /// <summary>Teleport another player to a location.</summary>
        public static string teleport_player = "teleport_player";
        /// <summary>Trigger a manual database backup.</summary>
        public static string backup_database = "backup_database";
        /// <summary>Reset a player's inventory to its default state.</summary>
        public static string reset_inventory = "reset_inventory";
        /// <summary>Set the maximum blocks allowed in a single fill operation.</summary>
        public static string fill_limit = "fill_limit";
        /// <summary>Switch between game modes (e.g. survival / creative).</summary>
        public static string mode = "mode";
        /// <summary>Load a saved world or map.</summary>
        public static string load = "load";
        /// <summary>Get or set the in-game time of day.</summary>
        public static string time = "time";
    };

    /// <summary>
    /// Creates and returns the four default groups (Guest, Builder, Moderator, Admin)
    /// with their canonical privilege sets and colours. Called when generating a
    /// fresh server config or resetting groups to defaults.
    /// The returned list is sorted by <see cref="Group.Level"/> ascending.
    /// </summary>
    public static List<Group> getDefaultGroups()
    {
        List<Group> defaultGroups = [];

        Group guest = new()
        {
            Name = "Guest",
            Level = 0,
            GroupPrivileges =
            [
                Privilege.chat, Privilege.pm, Privilege.build, Privilege.use,
                Privilege.login, Privilege.tp, Privilege.tp_pos, Privilege.freemove,
            ],
            GroupColor = ClientColor.Grey
        };
        defaultGroups.Add(guest);

        Group builder = new()
        {
            Name = "Builder",
            Level = 1,
            GroupPrivileges =
            [
                Privilege.chat, Privilege.pm, Privilege.build, Privilege.use,
                Privilege.login, Privilege.tp, Privilege.tp_pos,
                Privilege.set_home, Privilege.freemove,
            ],
            GroupColor = ClientColor.Green
        };
        defaultGroups.Add(builder);

        Group moderator = new()
        {
            Name = "Moderator",
            Level = 2,
            GroupPrivileges =
            [
                Privilege.chat, Privilege.pm, Privilege.build, Privilege.use,
                Privilege.freemove, Privilege.kick, Privilege.ban,
                Privilege.banip, Privilege.ban_offline, Privilege.unban,
                Privilege.list_clients, Privilege.list_saved_clients,
                Privilege.list_groups, Privilege.list_banned_users, Privilege.list_areas,
                Privilege.chgrp, Privilege.chgrp_offline, Privilege.remove_client,
                Privilege.use_tnt, Privilege.restart, Privilege.login,
                Privilege.tp, Privilege.tp_pos, Privilege.set_home, Privilege.mode,
            ],
            GroupColor = ClientColor.Cyan
        };
        defaultGroups.Add(moderator);

        Group admin = new()
        {
            Name = "Admin",
            Level = 3,
            GroupPrivileges =
            [
                Privilege.chat, Privilege.pm, Privilege.build, Privilege.use,
                Privilege.freemove, Privilege.kick, Privilege.ban,
                Privilege.banip, Privilege.ban_offline, Privilege.unban,
                Privilege.announcement, Privilege.welcome,
                Privilege.list_clients, Privilege.list_saved_clients,
                Privilege.list_groups, Privilege.list_banned_users, Privilege.list_areas,
                Privilege.chgrp, Privilege.chgrp_offline, Privilege.remove_client,
                Privilege.monsters, Privilege.give, Privilege.giveall,
                Privilege.use_tnt, Privilege.area_add, Privilege.area_delete,
                Privilege.restart, Privilege.login,
                Privilege.tp, Privilege.tp_pos, Privilege.set_home,
                Privilege.mode, Privilege.load, Privilege.time,
                "revert",
            ],
            GroupColor = ClientColor.Yellow
        };
        defaultGroups.Add(admin);

        defaultGroups.Sort();
        return defaultGroups;
    }

    /// <summary>
    /// Creates and returns a default client list containing a single placeholder
    /// entry assigned to the Guest group. Used when generating a fresh server config.
    /// </summary>
    public static List<Client> getDefaultClients()
    {
        List<Client> defaultClients = [];
        Client defaultClient = new()
        {
            Name = DefaultPlayerName,
            Group = getDefaultGroups()[0].Name
        };
        defaultClients.Add(defaultClient);
        return defaultClients;
    }

    /// <summary>
    /// Joins a list of privilege strings into a single comma-separated string
    /// suitable for display in admin command output or debug logs.
    /// Returns an empty string if the list is empty.
    /// </summary>
    public static string PrivilegesString(List<string> privileges)
    {
        string privilegesString = "";
        if (privileges.Count > 0)
        {
            privilegesString = privileges[0].ToString();
            for (int i = 1; i < privileges.Count; i++)
            {
                privilegesString += "," + privileges[i].ToString();
            }
        }
        return privilegesString;
    }

    /// <summary>
    /// Converts a <see cref="ClientColor"/> enum value to its Minecraft-style
    /// colour code string (e.g. <c>ClientColor.Green</c> → <c>"&amp;2"</c>).
    /// Returns <c>"&amp;f"</c> (white) for any unrecognised value.
    /// </summary>
    public static string ClientColorToString(ClientColor color)
    {
        return color switch
        {
            ClientColor.Black => "&0",
            ClientColor.Blue => "&1",
            ClientColor.Green => "&2",
            ClientColor.Cyan => "&3",
            ClientColor.Red => "&4",
            ClientColor.Purple => "&5",
            ClientColor.Yellow => "&6",
            ClientColor.Grey => "&7",
            ClientColor.DarkGrey => "&8",
            ClientColor.LightBlue => "&9",
            ClientColor.LightGreen => "&a",
            ClientColor.LightCyan => "&b",
            ClientColor.LightRed => "&c",
            ClientColor.LightPink => "&d",
            ClientColor.LightYellow => "&e",
            ClientColor.White => "&f",
            _ => "&f",
        };
    }
}