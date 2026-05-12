public class ServerConsole
{
    private readonly Server server;
    public IGameExitService Exit;

    public ServerConsole(Server server, IGameExitService exit)
    {
        this.server = server;
        Exit = exit;

        // run command line reader as seperate thread
        Thread consoleInterpreterThread = new(new ThreadStart(CommandLineReader));
        consoleInterpreterThread.Start();
    }

    public void CommandLineReader()
    {
        while (!Exit.Exit)
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

    public static void Receive(string message) => Console.WriteLine(message);
}
