public partial class Game
{
    internal int ChatLinesCount;
    internal string GuiTypingBuffer;
    internal bool IsTyping;

    // -------------------------------------------------------------------------
    // Chat line display
    // -------------------------------------------------------------------------

    public void AddChatline(string s)
    {
        if (string.IsNullOrEmpty(s))
            return;

        string linkTarget = ExtractLink(s);
        bool containsLink = linkTarget != null;
        int now = platform.TimeMillisecondsFromStart;

        if (s.Length > ChatLines.Count && ChatLines.Count > 0)
        {
            for (int i = 0; i <= s.Length / ChatLines.Count; i++)
            {
                int displayLength = Math.Min(ChatLines.Count, s.Length - i * ChatLines.Count);
                string chunk = s.Substring(i * ChatLines.Count, displayLength);
                ChatLinesAdd(containsLink
                    ? Chatline.CreateClickable(chunk, now, linkTarget)
                    : Chatline.Create(chunk, now));
            }
        }
        else
        {
            ChatLinesAdd(containsLink
                ? Chatline.CreateClickable(s, now, linkTarget)
                : Chatline.Create(s, now));
        }
    }

    private static string ExtractLink(string s)
    {
        foreach (string prefix in new[] { "https://", "http://" })
        {
            if (s.Contains(prefix))
            {
                foreach (string word in s.Split(' '))
                {
                    if (word.Contains(prefix, StringComparison.InvariantCultureIgnoreCase))
                        return word;
                }
            }
        }
        return null;
    }

    private void ChatLinesAdd(Chatline chatline)
    {
        if (ChatLinesCount >= ChatLinesMax)
        {
            Chatline[] lines2 = new Chatline[ChatLinesMax * 2];
            for (int i = 0; i < ChatLinesMax; i++)
                lines2[i] = ChatLines[i];
            ChatLines = [.. lines2];
            ChatLinesMax *= 2;
        }
        ChatLines.Add(chatline);
    }

    // -------------------------------------------------------------------------
    // Typing state
    // -------------------------------------------------------------------------

    public void StartTyping()
    {
        GuiTyping = TypingState.Typing;
        IsTyping = true;
        GuiTypingBuffer = "";
        IsTeamchat = false;
    }

    public void StopTyping()
    {
        GuiTyping = TypingState.None;
    }

    // -------------------------------------------------------------------------
    // Chat / command execution
    // -------------------------------------------------------------------------

    internal void ExecuteChat(string s_)
    {
        if (string.IsNullOrEmpty(s_))
            return;

        if (s_.StartsWith("."))
        {
            ExecuteClientCommand(s_);
        }
        else
        {
            // Regular chat message or server command — send to server.
            string chatline = GuiTypingBuffer[..Math.Min(GuiTypingBuffer.Length, 4096)];
            SendChat(chatline);
        }
    }

    public bool BoolCommandArgument(string arguments)
    {
        arguments = arguments.Trim();
        return arguments == "" || arguments == "1" || arguments == "on" || arguments == "yes";
    }

    private void ExecuteClientCommand(string s_)
    {
        string[] ss = s_.Split(' ');
        string cmd = ss[0][1..];
        int spaceIndex = s_.IndexOf(" ", StringComparison.InvariantCultureIgnoreCase);
        string arguments = spaceIndex >= 0 ? s_[spaceIndex..].Trim() : "";
        string strFreemoveNotAllowed = language.FreemoveNotAllowed();

        switch (cmd)
        {
            case "clients":
                Log("Clients:");
                for (int i = 0; i < entities.Count; i++)
                {
                    Entity entity = entities[i];
                    if (entity == null || entity.drawName == null || !entity.drawName.ClientAutoComplete) continue;
                    Log(string.Format("{0} {1}", i.ToString(), entity.drawName.Name));
                }
                break;

            case "reconnect":
                Reconnect();
                break;

            case "m":
                mouseSmoothing = !mouseSmoothing;
                Log(mouseSmoothing ? "Mouse smoothing enabled." : "Mouse smoothing disabled.");
                break;

            case "pos":
                ENABLE_DRAWPOSITION = BoolCommandArgument(arguments);
                break;

            case "noclip":
                controls.noclip = BoolCommandArgument(arguments);
                break;

            case "freemove":
                if (!AllowFreemove) { Log(strFreemoveNotAllowed); return; }
                controls.freemove = BoolCommandArgument(arguments);
                break;

            case "gui":
                ENABLE_DRAW2D = BoolCommandArgument(arguments);
                break;

            default:
                if (arguments != "")
                    ExecuteClientCommandWithArg(cmd, arguments, strFreemoveNotAllowed);
                else
                {
                    // No matching command — send as chat.
                    string chatline = GuiTypingBuffer[..Math.Min(GuiTypingBuffer.Length, 256)];
                    SendChat(chatline);
                }
                break;
        }

        // Always dispatch to client mods regardless of whether a command matched.
        for (int i = 0; i < clientmods.Count; i++)
        {
            ClientCommandArgs args = new() { arguments = arguments, command = cmd };
            clientmods[i].OnClientCommand(this, args);
        }
    }

    private void ExecuteClientCommandWithArg(string cmd, string arguments, string strFreemoveNotAllowed)
    {
        switch (cmd)
        {
            case "fog":
                int foglevel = int.Parse(arguments);
                foglevel = Math.Min(foglevel, 1024);
                if (foglevel % 2 == 0) foglevel--;
                d_Config3d.viewdistance = foglevel;
                OnResize();
                break;

            case "fov":
                int arg = int.Parse(arguments);
                int minfov = issingleplayer ? 1 : 60;
                int maxfov = 179;
                if (arg < minfov || arg > maxfov)
                    Log(string.Format("Valid field of view: {0}-{1}", minfov, maxfov));
                else
                {
                    fov = 2 * MathF.PI * (one * arg / 360);
                    OnResize();
                }
                break;

            case "movespeed":
                if (!AllowFreemove) { Log(strFreemoveNotAllowed); return; }
                float speed = float.Parse(arguments);
                if (speed > 500)
                    AddChatline("Entered movespeed to high! max. 500x");
                else
                {
                    movespeed = basemovespeed * speed;
                    AddChatline(string.Format("Movespeed: {0}x", arguments));
                }
                break;

            case "serverinfo":
                string[] split = arguments.Split(':');
                if (split.Length == 2)
                {
                    var (result, message) = Task.Run(() => new QueryClient(platform).QueryAsync(split[0], int.Parse(split[1]))).GetAwaiter().GetResult();

                    if (result != null)
                    {
                        AddChatline(result.GameMode);
                        AddChatline(result.MapSizeX.ToString());
                        AddChatline(result.MapSizeY.ToString());
                        AddChatline(result.MapSizeZ.ToString());
                        AddChatline(result.MaxPlayers.ToString());
                        AddChatline(result.Motd);
                        AddChatline(result.Name);
                        AddChatline(result.PlayerCount.ToString());
                        AddChatline(result.PlayerList);
                        AddChatline(result.Port.ToString());
                        AddChatline(result.PublicHash);
                        AddChatline(result.ServerVersion);
                    }
                    AddChatline(message);
                }
                break;
        }
    }
}