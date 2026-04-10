using System.Net.Http.Headers;

public class ServerSystemHeartbeat : ServerSystem
{
    private float elapsed;
    private readonly ServerHeartbeat d_Heartbeat;
    private bool writtenServerKey = false;
    public string hashPrefix = "server=";

    public ServerSystemHeartbeat()
    {
        d_Heartbeat = new ServerHeartbeat();
        elapsed = 60;
    }

    internal static Action CreateSendHeartbeatAction(ServerSystemHeartbeat s, Server server)
    {
        return async () => await s.SendHeartbeat(server);
    }

    public override void Update(Server server, float dt)
    {
        elapsed += dt;
        while (elapsed >= 60)
        {
            elapsed -= 60;
            if (server.Public && server.config.Public)
            {
                d_Heartbeat.GameMode = server.gameMode;
                server.serverPlatform.QueueUserWorkItem(CreateSendHeartbeatAction(this, server));
            }
        }
    }

    public async Task SendHeartbeat(Server server)
    {
        if (server.config == null)
        {
            return;
        }
        if (server.config.Key == null)
        {
            return;
        }
        d_Heartbeat.Name = server.config.Name;
        d_Heartbeat.MaxClients = server.config.MaxClients;
        d_Heartbeat.PasswordProtected = server.config.IsPasswordProtected();
        d_Heartbeat.AllowGuests = server.config.AllowGuests;
        d_Heartbeat.Port = server.config.Port;
        d_Heartbeat.Version = GameVersion.Version;
        d_Heartbeat.Key = server.config.Key;
        d_Heartbeat.Motd = server.config.Motd;
        List<string> playernames = [];
        lock (server.clients)
        {
            foreach (var k in server.clients)
            {
                if (k.Value.IsBot)
                {
                    //Exclude bot players from appearing on server list
                    continue;
                }
                playernames.Add(k.Value.playername);
            }
        }
        d_Heartbeat.Players = playernames;
        d_Heartbeat.UsersCount = playernames.Count;
        try
        {
            await d_Heartbeat.SendHeartbeatAsync();
            server.ReceivedKey = d_Heartbeat.ReceivedKey;
            if (!writtenServerKey)
            {
                Console.WriteLine($"hash: {GetHash(d_Heartbeat.ReceivedKey)}");
                writtenServerKey = true;
            }
            Console.WriteLine(server.language.ServerHeartbeatSent());
        }
        catch (Exception e)
        {
            #if DEBUG
                // Only display full error message when running in Debug mode
                Console.WriteLine(e.ToString());
            #endif
            // Short error output when running normally
            Console.WriteLine("{0} ({1})", server.language.ServerHeartbeatError(), e.Message);
        }
    }

    private string GetHash(string hash)
    {
        try
        {
            if (hash.Contains(hashPrefix))
            {
                hash = hash.Substring(hash.IndexOf(hashPrefix) + hashPrefix.Length);
            }
        }
        catch
        {
            return "";
        }
        return hash;
    }
}

public class ServerHeartbeat
{
    public ServerHeartbeat()
    {
        this.Name = "";
        this.Key = Guid.NewGuid().ToString();
        this.MaxClients = 16;
        this.Public = true;
        this.AllowGuests = true;
        this.Port = 25565;
        this.Version = "Unknown";
        this.Players = new List<string>();
        this.UsersCount = 0;
        this.Motd = "";
    }

    private string fListUrl = null;

    public string Name { get; set; }
    public string Key { get; set; }
    public int MaxClients { get; set; }
    public bool Public { get; set; }
    public bool PasswordProtected { get; set; }
    public bool AllowGuests { get; set; }
    public int Port { get; set; }
    public string Version { get; set; }
    public List<string> Players { get; set; }
    public int UsersCount { get; set; }
    public string Motd { get; set; }
    public string GameMode { get; set; }
    public string ReceivedKey { get; set; }

    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15),
        DefaultRequestHeaders = { CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true } }
    };

    public async Task SendHeartbeatAsync()
    {
        fListUrl ??= await _httpClient.GetStringAsync("http://manicdigger.sourceforge.net/heartbeat.txt");

        var formData = new Dictionary<string, string>
        {
            ["name"] = Name,
            ["max"] = MaxClients.ToString(),
            ["public"] = Public.ToString(),
            ["passwordProtected"] = PasswordProtected.ToString(),
            ["allowGuests"] = AllowGuests.ToString(),
            ["port"] = Port.ToString(),
            ["version"] = Version.ToString(),
            ["fingerprint"] = Key.Replace("-", ""),
            ["users"] = UsersCount.ToString(),
            ["motd"] = Motd,
            ["gamemode"] = GameMode,
            ["players"] = string.Join(",", Players),
        };

        using var content = new FormUrlEncodedContent(formData);
        using var response = await _httpClient.PostAsync(fListUrl, content);
        response.EnsureSuccessStatusCode();
        ReceivedKey = await response.Content.ReadAsStringAsync();
    }
}
