public static class GameVersion
{
    private static string gameversion;
    public static string Version
    {
        get
        {
            if (gameversion == null)
            {
                gameversion = "unknown";
                if (File.Exists("version.txt"))
                {
                    gameversion = File.ReadAllText("version.txt").Trim();
                }
            }

            return gameversion;
        }
    }
}
