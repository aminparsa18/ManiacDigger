public class ServerConsole
{
    private readonly Server server;
    public GameExit Exit;

    public ServerConsole(Server server, GameExit exit)
    {
        this.server = server;
        this.Exit = exit;

        // run command line reader as seperate thread
        Thread consoleInterpreterThread = new(new ThreadStart(this.CommandLineReader));
        consoleInterpreterThread.Start();
    }

    public void CommandLineReader()
    {
        while (!Exit.exit)
        {
            if (server.IsSinglePlayer)
            {
                Thread.Sleep(1000);
            }
            else
            {
                string input = Console.ReadLine();
                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }
                input = input.Trim();
                server.ReceiveServerConsole(input);
            }
        }
    }

    public static void Receive(string message)
    {
        Console.WriteLine(message);
    }
}
