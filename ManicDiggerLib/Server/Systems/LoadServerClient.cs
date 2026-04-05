using System.Xml.Serialization;
using ManicDigger;
using System.Xml;

//Load server groups and spawnpoints
public class ServerSystemLoadServerClient : ServerSystem
{
    private bool loaded;
   
    public override void Update(Server server, float dt)
    {
        if (!loaded)
        {
            loaded = true;
            LoadServerClient(server);
        }
        if (server.serverClientNeedsSaving)
        {
            server.serverClientNeedsSaving = false;
            SaveServerClient(server);
        }
    }

    public static void LoadServerClient(Server server)
    {
        string filename = "ServerClient.txt";
        if (!File.Exists(Path.Combine(GameStorePath.gamepathconfig, filename)))
        {
            Console.WriteLine(server.language.ServerClientConfigNotFound());
            SaveServerClient(server);
        }
        else
        {
            try
            {
                using TextReader textReader = new StreamReader(Path.Combine(GameStorePath.gamepathconfig, filename));
                XmlSerializer deserializer = new(typeof(ServerClient));
                server.serverClient = (ServerClient)deserializer.Deserialize(textReader);
                textReader.Close();
                server.serverClient.Groups.Sort();
                SaveServerClient(server);
            }
            catch //This if for the original format
            {
                using (Stream s = new MemoryStream(File.ReadAllBytes(Path.Combine(GameStorePath.gamepathconfig, filename))))
                {
                    server.serverClient = new ServerClient();
                    StreamReader sr = new(s);
                    XmlDocument d = new();
                    d.Load(sr);
                    server.serverClient.Format = int.Parse(XmlTool.XmlVal(d, "/ManicDiggerServerClient/Format"));
                    server.serverClient.DefaultGroupGuests = XmlTool.XmlVal(d, "/ManicDiggerServerClient/DefaultGroupGuests");
                    server.serverClient.DefaultGroupRegistered = XmlTool.XmlVal(d, "/ManicDiggerServerClient/DefaultGroupRegistered");
                }
                //Save with new version.
                SaveServerClient(server);
            }
        }
        if (server.serverClient.DefaultSpawn == null)
        {
            // server sets a default spawn (middle of map)
            int x = server.d_Map.MapSizeX / 2;
            int y = server.d_Map.MapSizeY / 2;
            server.defaultPlayerSpawn = server.DontSpawnPlayerInWater(new Vector3i(x, y, MapUtil.blockheight(server.d_Map, 0, x, y)));
        }
        else
        {
            int z;
            if (server.serverClient.DefaultSpawn.z == null)
            {
                z = MapUtil.blockheight(server.d_Map, 0, server.serverClient.DefaultSpawn.x, server.serverClient.DefaultSpawn.y);
            }
            else
            {
                z = server.serverClient.DefaultSpawn.z.Value;
            }
            server.defaultPlayerSpawn = new Vector3i(server.serverClient.DefaultSpawn.x, server.serverClient.DefaultSpawn.y, z);
        }

        server.defaultGroupGuest = server.serverClient.Groups.Find(
            delegate (Group grp)
            {
                return grp.Name.Equals(server.serverClient.DefaultGroupGuests);
            }
        ) ?? throw new Exception(server.language.ServerClientConfigGuestGroupNotFound());
        server.defaultGroupRegistered = server.serverClient.Groups.Find(
            delegate (Group grp)
            {
                return grp.Name.Equals(server.serverClient.DefaultGroupRegistered);
            }
        ) ?? throw new Exception(server.language.ServerClientConfigRegisteredGroupNotFound());
        Console.WriteLine(server.language.ServerClientConfigLoaded());
    }

    public static void SaveServerClient(Server server)
    {
        //Verify that we have a directory to place the file into.
        if (!Directory.Exists(GameStorePath.gamepathconfig))
        {
            Directory.CreateDirectory(GameStorePath.gamepathconfig);
        }

        XmlSerializer serializer = new(typeof(ServerClient));
        TextWriter textWriter = new StreamWriter(Path.Combine(GameStorePath.gamepathconfig, "ServerClient.txt"));

        //Check to see if config has been initialized
        server.serverClient ??= new ServerClient();
        if (server.serverClient.Groups.Count == 0)
        {
            server.serverClient.Groups = ServerClientMisc.getDefaultGroups();
        }
        if (server.serverClient.Clients.Count == 0)
        {
            server.serverClient.Clients = ServerClientMisc.getDefaultClients();
        }
        server.serverClient.Clients.Sort();
        //Serialize the ServerConfig class to XML
        serializer.Serialize(textWriter, server.serverClient);
        textWriter.Close();
    }
}
