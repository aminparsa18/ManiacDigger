//Sends ping to all clients and disconnects timed-out players
public class ServerSystemNotifyPing : ServerSystem
{
    private readonly Timer pingtimer = new() { INTERVAL = 1, MaxDeltaTime = 5 };

    public override void Update(Server server, float dt)
    {
        pingtimer.Update(
        delegate
        {
            if (server.exit.GetExit())
            {
                //Instantly return if server wants to exit
                return;
            }
            List<int> keysToDelete = new();
            foreach (var k in server.clients)
            {
                // Check if client is alive. Detect half-dropped connections.
                if (!k.Value.Ping.Send(server.platform)/*&& k.Value.state == ClientStateOnServer.Playing*/)
                {
                    if (k.Value.Ping.Timeout(server.platform))
                    {
                        Console.WriteLine(k.Key + ": ping timeout. Disconnecting...");
                        keysToDelete.Add(k.Key);
                    }
                }
                else
                {
                    server.SendPacket(k.Key, ServerPackets.Ping());
                }
            }

            foreach (int key in keysToDelete)
            {
                server.KillPlayer(key);
            }
        });
    }
}
