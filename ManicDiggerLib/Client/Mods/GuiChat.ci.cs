using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public class ModGuiChat : ModBase
{
    public ModGuiChat(IGameClient game)
    {
        this.game = game;
        ChatFontSize = 11;
        currentFontSize = ChatFontSize;
        ChatScreenExpireTimeSeconds = 20;
        ChatLinesMaxToDraw = 10;
        font = new Font("Arial", currentFontSize, currentFontStyle);
        chatlines2 = new Chatline[1024];
    }

    private readonly IGameClient game;
    internal float ChatFontSize;
    internal int ChatScreenExpireTimeSeconds;
    internal int ChatLinesMaxToDraw;
    internal int ChatPageScroll;
    private float currentFontSize = 11;
    private FontStyle currentFontStyle = FontStyle.Regular;

    public override void OnNewFrameDraw2d(float deltaTime)
    {
        if (game.GuiState == GuiState.MapLoading)
        {
            return;
        }
        DrawChatLines(game.GuiTyping == TypingState.Typing);
        if (game.GuiTyping == TypingState.Typing)
        {
            DrawTypingBuffer();
        }
    }

    public override void OnMouseDown(Game game_, MouseEventArgs args)
    {
        for (int i = 0; i < chatlines2Count; i++)
        {
            float dx = 20;
            if (!game.Platform.IsMousePointerLocked())
            {
                dx += 100;
            }
            float chatlineStartX = dx * game.Scale();
            float chatlineStartY = (90 + i * 25) * game.Scale();
            float chatlineSizeX = 500 * game.Scale();
            float chatlineSizeY = 20 * game.Scale();
            if (args.GetX() > chatlineStartX && args.GetX() < chatlineStartX + chatlineSizeX)
            {
                if (args.GetY() > chatlineStartY && args.GetY() < chatlineStartY + chatlineSizeY)
                {
                    //Mouse over chatline at position i
                    if (chatlines2[i].clickable)
                    {
                        game.Platform.OpenLinkInBrowser(chatlines2[i].linkTarget);
                    }
                }
            }
        }
    }

    private readonly Chatline[] chatlines2;
    private int chatlines2Count;
    public void DrawChatLines(bool all)
    {
        chatlines2Count = 0;
        int timeNow = game.Platform.TimeMillisecondsFromStart;
        int scroll;
        if (!all)
        {
            scroll = 0;
        }
        else
        {
            scroll = ChatPageScroll;
        }
        int first = game.ChatLinesCount - ChatLinesMaxToDraw * (scroll + 1);
        if (first < 0)
        {
            first = 0;
        }
        int count = game.ChatLinesCount;
        if (count > ChatLinesMaxToDraw)
        {
            count = ChatLinesMaxToDraw;
        }
        for (int i = first; i < first + count; i++)
        {
            Chatline c = game.ChatLines[i];
            if (all || ((1f * (timeNow - c.timeMilliseconds) / 1000) < ChatScreenExpireTimeSeconds))
            {
                chatlines2[chatlines2Count++] = c;
            }
        }
        currentFontSize= ChatFontSize * game.Scale();
        font = new Font("Arial", currentFontSize, currentFontStyle);

        float dx = 20;
        //if (!game.Platform.IsMousePointerLocked())
        //{
        //    dx += 100;
        //}
        for (int i = 0; i < chatlines2Count; i++)
        {
            if (chatlines2[i].clickable)
            {
                //Different display of links in chat
                //2 = italic
                //3 = bold italic
                currentFontStyle = FontStyle.Italic;
                font = new Font("Arial", currentFontSize, currentFontStyle);
            }
            else
            {
                //0 = normal
                //1 = bold
                currentFontStyle = FontStyle.Bold;
                font = new Font("Arial", currentFontSize, currentFontStyle);
            }
            game.Draw2dText(chatlines2[i].text, font, dx * game.Scale(), (90 + i * 25) * game.Scale(), null, false);
        }
        if (ChatPageScroll != 0)
        {
            game.Draw2dText(string.Format("&7Page: {0}", ChatPageScroll.ToString()), font, dx * game.Scale(), (90 + (-1) * 25) * game.Scale(), null, false);
        }
    }
    private Font font;
    public void DrawTypingBuffer()
    {
        currentFontSize = ChatFontSize * game.Scale();
        font = new Font("Arial", currentFontSize, currentFontStyle);
        string s = game.GuiTypingBuffer;
        if (game.IsTeamchat)
        {
            s = string.Format("To team: {0}", s);
        }
        if (game.Platform.IsSmallScreen())
        {
            game.Draw2dText(string.Format("{0}_", s), font, 50 * game.Scale(), (game.Platform.GetCanvasHeight() / 2) - 100 * game.Scale(), null, true);
        }
        else
        {
            game.Draw2dText(string.Format("{0}_", s), font, 50 * game.Scale(), game.Platform.GetCanvasHeight() - 100 * game.Scale(), null, true);
        }
    }

    public override void OnKeyDown(Game game_, KeyEventArgs args)
    {
        if (game.GuiState != GuiState.Normal)
        {
            //Don't open chat when not in normal game
            return;
        }
        int eKey = args.KeyChar;
        if (eKey == game.GetKey(Keys.KeyPad7) && game.IsShiftPressed && game.GuiTyping == TypingState.None) // don't need to hit enter for typing commands starting with slash
        {
            game.GuiTyping = TypingState.Typing;
            game.IsTyping = true;
            game.GuiTypingBuffer = "";
            game.IsTeamchat = false;
            args.Handled=true;
            return;
        }
        if (eKey == game.GetKey(Keys.PageUp) && game.GuiTyping == TypingState.Typing)
        {
            ChatPageScroll++;
            args.Handled=true;
        }
        if (eKey == game.GetKey(Keys.PageDown) && game.GuiTyping == TypingState.Typing)
        {
            ChatPageScroll--;
            args.Handled=true;
        }
        ChatPageScroll = Math.Clamp(ChatPageScroll, 0, game.ChatLinesCount / ChatLinesMaxToDraw);
        if (eKey == game.GetKey(Keys.Enter) || eKey == game.GetKey(Keys.KeyPadEnter))
        {
            if (game.GuiTyping == TypingState.Typing)
            {
                game.TypingLog.Add(game.GuiTypingBuffer);
                game.TypingLogPos = game.TypingLog.Count;
                game.ExecuteChat(game.GuiTypingBuffer);

                game.GuiTypingBuffer = "";
                game.IsTyping = false;

                game.GuiTyping = TypingState.None;
                game.Platform.ShowKeyboard(false);
            }
            else if (game.GuiTyping == TypingState.None)
            {
                game.StartTyping();
            }
            else if (game.GuiTyping == TypingState.Ready)
            {
                Console.WriteLine("Keyboard_KeyDown ready");
            }
            args.Handled=true;
            return;
        }
        if (game.GuiTyping == TypingState.Typing)
        {
            int key = eKey;
            if (key == game.GetKey(Keys.Backspace))
            {
                if (game.GuiTypingBuffer.Length > 0)
                {
                    game.GuiTypingBuffer = game.GuiTypingBuffer[..^1];
                }
                args.Handled=true;
                return;
            }
            if (game.KeyboardStateRaw[game.GetKey(Keys.LeftControl)] || game.KeyboardStateRaw[game.GetKey(Keys.RightControl)])
            {
                if (key == game.GetKey(Keys.V))
                {
                    if (Clipboard.ContainsText())
                    {
                        game.GuiTypingBuffer = string.Concat(game.GuiTypingBuffer, Clipboard.GetText());
                    }
                    args.Handled=true;
                    return;
                }
            }
            if (key == game.GetKey(Keys.Up))
            {
                game.TypingLogPos--;
                if (game.TypingLogPos < 0) { game.TypingLogPos = 0; }
                if (game.TypingLogPos >= 0 && game.TypingLogPos < game.TypingLog.Count)
                {
                    game.GuiTypingBuffer = game.TypingLog[game.TypingLogPos];
                }
                args.Handled=true;
            }
            if (key == game.GetKey(Keys.Down))
            {
                game.TypingLogPos++;
                if (game.TypingLogPos > game.TypingLog.Count) { game.TypingLogPos = game.TypingLog.Count; }
                if (game.TypingLogPos >= 0 && game.TypingLogPos < game.TypingLog.Count)
                {
                    game.GuiTypingBuffer = game.TypingLog[game.TypingLogPos];
                }
                if (game.TypingLogPos == game.TypingLog.Count)
                {
                    game.GuiTypingBuffer = "";
                }
                args.Handled=true;
            }
            //Handles player name autocomplete in chat
            if (eKey == game.GetKey(Keys.Tab) && game.GuiTypingBuffer.Trim() != "")
            {
                string[] parts = game.GuiTypingBuffer.Split(" ");
                string completed = DoAutocomplete(parts[parts.Length - 1]);
                if (completed == "")
                {
                    //No completion available. Abort.
                    args.Handled=true;
                    return;
                }
                else if (parts.Length == 1)
                {
                    //Part is first word. Format as "<name>: "
                    game.GuiTypingBuffer = string.Concat(completed, ": ");
                }
                else
                {
                    //Part is not first. Just complete "<name> "
                    parts[parts.Length - 1] = completed;
                    game.GuiTypingBuffer = string.Concat(string.Join(" ", parts), " ");
                }
                args.Handled=true;
                return;
            }
            args.Handled=true;
            return;
        }
    }

    public override void OnKeyPress(Game game_, KeyPressEventArgs args)
    {
        if (game.GuiState != GuiState.Normal)
        {
            //Don't open chat when not in normal game
            return;
        }
        int eKeyChar = args.KeyChar;
        int chart = 116;
        int charT = 84;
        int chary = 121;
        int charY = 89;
        if ((eKeyChar == chart || eKeyChar == charT) && game.GuiTyping == TypingState.None)
        {
            game.GuiTyping = TypingState.Typing;
            game.GuiTypingBuffer = "";
            game.IsTeamchat = false;
            return;
        }
        if ((eKeyChar == chary || eKeyChar == charY) && game.GuiTyping == TypingState.None)
        {
            game.GuiTyping = TypingState.Typing;
            game.GuiTypingBuffer = "";
            game.IsTeamchat = true;
            return;
        }
        if (game.GuiTyping == TypingState.Typing)
        {
            int c = eKeyChar;
            if (game.Platform.IsValidTypingChar(c))
            {
                game.GuiTypingBuffer = string.Concat(game.GuiTypingBuffer, (char)c);
            }
        }
    }

    public string DoAutocomplete(string text)
    {
        if (!string.IsNullOrEmpty(text))
        {
            for (int i = 0; i < game.Entities.Count; i++)
            {
                Entity entity = game.Entities[i];
                if (entity == null) { continue; }
                if (entity.drawName == null) { continue; }
                if (!entity.drawName.ClientAutoComplete) { continue; }
                DrawName p = entity.drawName;
                //Use substring here because player names are internally in format &xNAME (so we need to cut first 2 characters)
                if (p.Name[2..].StartsWith(text, StringComparison.InvariantCultureIgnoreCase))
                {
                    return p.Name[2..];
                }
            }
        }
        return "";
    }
}

public class Chatline
{
    internal string text;
    internal int timeMilliseconds;
    internal bool clickable;
    internal string linkTarget;

    internal static Chatline Create(string text_, int timeMilliseconds_)
    {
        Chatline c = new()
        {
            text = text_,
            timeMilliseconds = timeMilliseconds_,
            clickable = false
        };
        return c;
    }

    internal static Chatline CreateClickable(string text_, int timeMilliseconds_, string linkTarget_)
    {
        Chatline c = new()
        {
            text = text_,
            timeMilliseconds = timeMilliseconds_,
            clickable = true,
            linkTarget = linkTarget_
        };
        return c;
    }
}
