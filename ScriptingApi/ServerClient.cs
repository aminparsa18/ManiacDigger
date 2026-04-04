using System.Xml.Serialization;

namespace ManicDigger;

	[XmlRoot(ElementName = "ManicDiggerServerClient")]
	public class ServerClient
	{
		public int Format { get; set; }
		public string DefaultGroupGuests { get; set; } // ~ players
		public string DefaultGroupRegistered { get; set; } // registered MD players

		[XmlArrayItem(ElementName = "Group")]
		public List<Group> Groups { get; set; }

		[XmlArrayItem(ElementName = "Client")]
		public List<Client> Clients { get; set; }

		[XmlElement(IsNullable = true)]
		public Spawn DefaultSpawn { get; set; }

		[XmlElement(IsNullable = true)]
		public int? DefaultFillLimit { get; set; }

		public ServerClient()
		{
			//Set Defaults
			this.Format = 1;
			this.DefaultGroupGuests = "Guest";
			this.DefaultGroupRegistered = "Guest";
			this.Groups = [];
			this.Clients = [];
		}
	}

	public class Group : IComparable<Group>
	{
		public string Name { get; set; }
		public int Level { get; set; }

		[XmlElement(IsNullable = true)]
		public string Password { get; set; }
		[XmlElement(IsNullable = true)]
		public Spawn Spawn { get; set; }
		[XmlElement(IsNullable = true)]
		public int? FillLimit { get; set; }

		[XmlArrayItem(ElementName = "Privilege")]
		public List<string> GroupPrivileges { get; set; }

		public ServerClientMisc.ClientColor GroupColor { get; set; }

		public Group()
		{
			this.Name = "";
			this.Level = 0;
			this.GroupPrivileges = [];
			this.GroupColor = ServerClientMisc.ClientColor.White;
		}

		public string GroupColorString()
		{
			return ServerClientMisc.ClientColorToString(this.GroupColor);
		}
		// Groups are sorted by levels (asc). Higher level groups are superior lower level groups.
		public int CompareTo(Group other)
		{
			return Level.CompareTo(other.Level);
		}

		public bool IsSuperior(Group clientGroup)
		{
			return this.Level > clientGroup.Level;
		}

		public bool EqualLevel(Group clientGroup)
		{
			return this.Level == clientGroup.Level;
		}

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

	public class Client : IComparable<Client>
	{
		public string Name { get; set; }
		public string Group { get; set; }
		[XmlElement(IsNullable = true)]
		public Spawn Spawn{ get; set; }
		[XmlElement(IsNullable = true)]
		public int? FillLimit { get; set; }

		public Client()
		{
			this.Name = "";
			this.Group = "";
		}

		public override string ToString()
		{
			return $"{this.Name}:{this.Group}";
		}

		// Clients are sorted by groups.
		public int CompareTo(Client other)
		{
			return Group.CompareTo(other.Group);
		}
	}

	public class Spawn
	{
		[XmlIgnore]
		public int x;
		[XmlIgnore]
		public int y;
		// z is optional
		[XmlIgnore]
		public int? z;

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
				catch(IndexOutOfRangeException ex)
				{
					throw new IndexOutOfRangeException("Invalid spawn position.", ex);
				}

				try
				{
					this.z = Convert.ToInt32(ss[2]);
				}
				catch(IndexOutOfRangeException)
				{
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
		public Spawn()
		{
			this.x = 0;
			this.y = 0;
		}
		public override string ToString()
		{
			return this.Coords;
		}
	}

	public class ServerClientMisc
	{
		public static string DefaultPlayerName = "Player name?";

		public enum ClientColor
		{
			Black,		// &0
			Blue,		// &1
			Green,		// &2
			Cyan,		// &3
			Red,		// &4
			Purple,		// &5
			Yellow,		// &6
			Grey,		// &7
			DarkGrey,	// &8
			LightBlue,	// &9
			LightGreen,	// &a
			LightCyan,	// &b
			LightRed,	// &c
			LightPink,	// &d
			LightYellow,// &e
			White		// &f
		};

		public class Privilege
		{
			public static string[] All()
			{
				return
                [
                    build,
					use,
					freemove,
					chat,
					pm,
					kick,
					kick_id,
					ban,
					ban_id,
					ban_id,
					banip,
					banip_id,
					ban_offline,
					unban,
					run,
					chgrp,
					chgrp_offline,
					remove_client,
					login,
					welcome,
					logging,
					list_clients,
					list_saved_clients,
					list_groups,
					list_banned_users,
					list_areas,
					give,
					giveall,
					monsters,
					area_add,
					area_delete,
					announcement,
					set_spawn,
					set_home,
					use_tnt,
					privilege_add,
					privilege_remove,
					restart,
					shutdown,
					tp,
					tp_pos,
					teleport_player,
					backup_database,
					reset_inventory,
					fill_limit,
					mode,
					load,
					time,
				];
			}
			public static string build = "build";
			public static string use = "use";
			public static string freemove = "freemove";
			public static string chat = "chat";
			public static string pm = "pm";
			public static string kick = "kick";
			public static string kick_id = "kick_id";
			public static string ban = "ban";
			public static string ban_id = "ban_id";
			public static string banip = "banip";
			public static string banip_id = "banip_id";
			public static string ban_offline = "ban_offline";
			public static string unban = "unban";
			public static string run = "run";
			public static string chgrp = "chgrp";
			public static string chgrp_offline = "chgrp_offline";
			public static string remove_client = "remove_client";
			public static string login = "login";
			public static string welcome = "welcome";
			public static string logging = "logging";
			public static string list_clients = "list_clients";
			public static string list_saved_clients = "list_saved_clients";
			public static string list_groups = "list_groups";
			public static string list_banned_users = "list_banned_users";
			public static string list_areas = "list_areas";
			public static string give = "give";
			public static string giveall = "giveall";
			public static string monsters = "monsters";
			public static string area_add = "area_add";
			public static string area_delete = "area_delete";
			public static string announcement = "announcement";
			public static string set_spawn = "set_spawn";
			public static string set_home = "set_home";
			public static string use_tnt = "use_tnt";
			public static string privilege_add = "privilege_add";
			public static string privilege_remove = "privilege_remove";
			public static string restart = "restart";
			public static string shutdown = "shutdown";
			public static string tp = "tp";
			public static string tp_pos = "tp_pos";
			public static string teleport_player = "teleport_player";
			public static string backup_database = "backup_database";
			public static string reset_inventory = "reset_inventory";
			public static string fill_limit = "fill_limit";
			public static string mode = "mode";
			public static string load = "load";
			public static string time = "time";
		};

		public static List<Group> getDefaultGroups()
		{
			List<Group > defaultGroups = [];
        // default guest group
        Group guest = new()
        {
            Name = "Guest",
            Level = 0,
            GroupPrivileges =
            [
                Privilege.chat,
                    Privilege.pm,
                    Privilege.build,
                    Privilege.use,
                    Privilege.login,
                    Privilege.tp,
                    Privilege.tp_pos,
                    Privilege.freemove,
                ],
            GroupColor = ClientColor.Grey
        };
        defaultGroups.Add(guest);
        // default builder group
        Group builder = new()
        {
            Name = "Builder",
            Level = 1,
            GroupPrivileges =
            [
                Privilege.chat,
                    Privilege.pm,
                    Privilege.build,
                    Privilege.use,
                    Privilege.login,
                    Privilege.tp,
                    Privilege.tp_pos,
                    Privilege.set_home,
                    Privilege.freemove,
                ],
            GroupColor = ClientColor.Green
        };
        defaultGroups.Add(builder);
        // default moderator group
        Group moderator = new()
        {
            Name = "Moderator",
            Level = 2,
            GroupPrivileges =
            [
                Privilege.chat,
                    Privilege.pm,
                    Privilege.build,
                    Privilege.use,
                    Privilege.freemove,
                    Privilege.kick,
                    Privilege.ban,
                    Privilege.banip,
                    Privilege.ban_offline,
                    Privilege.unban,
                    Privilege.list_clients,
                    Privilege.list_saved_clients,
                    Privilege.list_groups,
                    Privilege.list_banned_users,
                    Privilege.list_areas,
                    Privilege.chgrp,
                    Privilege.chgrp_offline,
                    Privilege.remove_client,
                    Privilege.use_tnt,
                    Privilege.restart,
                    Privilege.login,
                    Privilege.tp,
                    Privilege.tp_pos,
                    Privilege.set_home,
                    Privilege.mode,
                ],
            GroupColor = ClientColor.Cyan
        };
        defaultGroups.Add(moderator);
        // default admin group
        Group admin = new()
        {
            Name = "Admin",
            Level = 3,
            GroupPrivileges =
            [
                Privilege.chat,
                    Privilege.pm,
                    Privilege.build,
                    Privilege.use,
                    Privilege.freemove,
                    Privilege.kick,
                    Privilege.ban,
                    Privilege.banip,
                    Privilege.ban_offline,
                    Privilege.unban,
                    Privilege.announcement,
                    Privilege.welcome,
                    Privilege.list_clients,
                    Privilege.list_saved_clients,
                    Privilege.list_groups,
                    Privilege.list_banned_users,
                    Privilege.list_areas,
                    Privilege.chgrp,
                    Privilege.chgrp_offline,
                    Privilege.remove_client,
                    Privilege.monsters,
                    Privilege.give,
                    Privilege.giveall,
                    Privilege.use_tnt,
                    Privilege.area_add,
                    Privilege.area_delete,
                    Privilege.restart,
                    Privilege.login,
                    Privilege.tp,
                    Privilege.tp_pos,
                    Privilege.set_home,
                    Privilege.mode,
                    Privilege.load,
                    Privilege.time,
                    "revert",
                ],
            GroupColor = ClientColor.Yellow
        };
        defaultGroups.Add(admin);

			defaultGroups.Sort();
			return defaultGroups;
		}

		public static List<Client> getDefaultClients()
		{
			List<Client > defaultClients = [];
        Client defaultClient = new()
        {
            Name = DefaultPlayerName,
            Group = getDefaultGroups()[0].Name
        };
        defaultClients.Add(defaultClient);

			return defaultClients;
		}

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
            _ => "&f",// white
        };
    }
	}
