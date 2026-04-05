namespace ManicDigger.Mods;

/// <summary>
/// This class contains all command logic and event handling
/// </summary>
public class CoreEvents : IMod
{
    private IModManager m;

    public void PreStart(IModManager m)
    {
        m.RequireMod("CoreBlocks");
    }
    public void Start(IModManager manager)
    {
        m = manager;

        //Register Commands
        m.RegisterOnCommand(OnCommandSetModel);

        //Register special events
        m.RegisterOnSpecialKey(OnRespawnKey);
        m.RegisterOnSpecialKey(OnSetSpawnKey);
        m.RegisterOnPlayerDeath(OnPlayerDeath);
    }

    //Dictionary to store temporary spawn positions
    private readonly Dictionary<string, float[]> spawnPositions = [];

    private void OnSetSpawnKey(int player, SpecialKey key)
    {
        if (key != SpecialKey.SetSpawn)
        {
            return;
        }
        float[] pos = [m.GetPlayerPositionX(player), m.GetPlayerPositionY(player), m.GetPlayerPositionZ(player)];
        spawnPositions[m.GetPlayerName(player)] = pos;
        m.SendMessage(player, "&7Spawn position set");
    }

    private void OnRespawnKey(int player, SpecialKey key)
    {
        if (key != SpecialKey.Respawn)
        {
            return;
        }
        Respawn(player);
        m.SendMessage(player, "&7Respawn");
    }

    private string ColoredPlayername(int player)
    {
        //Returns the player name in group color
        return string.Format("{0}{1}", m.GetGroupColor(player), m.GetPlayerName(player));
    }

    private void OnPlayerDeath(int player, DeathReason reason, int sourceID)
    {
        //Respawn the player and send a death message to all players
        Respawn(player);

        string deathMessage;
        //Different death message depending on reason for death
        switch (reason)
        {
            case DeathReason.FallDamage:
                deathMessage = string.Format("{0} &7was doomed to fall.", ColoredPlayername(player));
                break;
            case DeathReason.BlockDamage:
                if (sourceID == m.GetBlockId("Lava"))
                {
                    deathMessage = string.Format("{0} &7thought they could swim in Lava.", ColoredPlayername(player));
                }
                else if (sourceID == m.GetBlockId("Fire"))
                {
                    deathMessage = string.Format("{0} &7was burned alive.", ColoredPlayername(player));
                }
                else
                {
                    deathMessage = string.Format("{0} &7was killed by {1}.", ColoredPlayername(player), m.GetBlockName(sourceID));
                }
                break;
            case DeathReason.Drowning:
                deathMessage = string.Format("{0} &7tried to breathe under water.", ColoredPlayername(player));
                break;
            case DeathReason.Explosion:
                deathMessage = string.Format("{0} &7was blown into pieces by {1}.", ColoredPlayername(player), ColoredPlayername(sourceID));
                break;
            default:
                deathMessage = string.Format("{0} &7died.", ColoredPlayername(player));
                break;
        }
        m.SendMessageToAll(deathMessage);
    }

    private void Respawn(int player)
    {
        if (!spawnPositions.ContainsKey(m.GetPlayerName(player)))
        {
            float[] pos = m.GetDefaultSpawnPosition(player);
            m.SetPlayerPosition(player, pos[0], pos[1], pos[2]);
        }
        else
        {
            float[] pos = spawnPositions[m.GetPlayerName(player)];
            m.SetPlayerPosition(player, pos[0], pos[1], pos[2]);
        }
    }

    private bool OnCommandSetModel(int player, string command, string argument)
    {
        if (command.Equals("setmodel", StringComparison.InvariantCultureIgnoreCase))
        {
            if (!(m.PlayerHasPrivilege(player, "setmodel") || m.IsSinglePlayer()))
            {
                m.SendMessage(player, m.ColorError + "No setmodel privilege");
                return true;
            }
            string[] ss = argument.Split(' ');
            string targetplayername = ss[0];
            string modelname = ss[1];
            string texturename = null;
            if (ss.Length >= 3)
            {
                texturename = ss[2];
            }
            bool found = false;
            foreach (int p in m.AllPlayers())
            {
                if (m.GetPlayerName(p).Equals(targetplayername, StringComparison.InvariantCultureIgnoreCase))
                {
                    m.SetPlayerModel(p, modelname, texturename);
                    found = true;
                }
            }
            if (!found)
            {
                m.SendMessage(player, m.ColorError + string.Format("Player {0} not found", targetplayername));
            }
            return true;
        }
        return false;
    }
}
