namespace ManicDigger;

public class ServerSystemLoadFirst : ServerSystem
{
    private bool loaded;

    public override void Update(Server server, float dt)
    {
        if (!loaded)
        {
            loaded = true;
            LoadFirstEvent(server);
        }
    }

    private static void LoadFirstEvent(Server server)
    {
        // Add things that need to be done prior to all other systems here.
    }
}
