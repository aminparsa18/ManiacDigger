using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public class ModGuiTextEditor : GameScreen
{
    public ModGuiTextEditor()
    {
        buffer = new int[maxLines][];
        for (int i = 0; i < maxLines; i++)
        {
            buffer[i] = new int[maxColumns];
        }
        startX = 100;
        startY = 100;
        charSize = 12;
        font = new FontCi
        {
            family = "Courier New",
            size = 12
        };
    }
    private bool visible;
    private const int maxLines = 128;
    private const int maxColumns = 80;
    private readonly FontCi font;
    private readonly int startX;
    private readonly int startY;
    private readonly int charSize;
    public override void OnNewFrameDraw2d(Game game, float deltaTime)
    {
        float dt = deltaTime;
        if (!visible)
        {
            return;
        }
        game.Draw2dTexture(game.WhiteTexture(), startX, startY, maxColumns * charSize, maxLines * charSize, null, 0, Game.ColorFromArgb(255, 100, 100, 100), false);
        for (int i = 0; i < maxLines; i++)
        {
            game.Draw2dText(LineToString(buffer[i]), font, startX, startY + charSize * i, null, false);
        }
        int[] spaces = new int[maxColumns];
        for (int i = 0; i < maxColumns; i++)
        {
            spaces[i] = 32;
        }
        spaces[cursorColumn] = 95; //_
        string spacesString = StringUtils.CharArrayToString(spaces, cursorColumn + 1);
        game.Draw2dText(spacesString, font, startX, startY + cursorLine * charSize, null, false);
    }
    private readonly int[][] buffer;
    private int cursorColumn;
    private int cursorLine;
    public override void OnKeyDown(Game game_, KeyEventArgs e)
    {
        if (e.GetKeyCode() == game.GetKey(Keys.F9))
        {
            visible = !visible;
        }
        if (!visible)
        {
            return;
        }
        if (e.GetKeyCode() == (int)Keys.Escape)
        {
            visible = false;
        }
        if (e.GetKeyCode() == (int)Keys.Left)
        {
            cursorColumn--;
        }
        if (e.GetKeyCode() == (int)Keys.Right)
        {
            cursorColumn++;
        }
        if (e.GetKeyCode() == (int)Keys.Up)
        {
            cursorLine--;
        }
        if (e.GetKeyCode() == (int)Keys.Down)
        {
            cursorLine++;
        }
        if (e.GetKeyCode() == (int)Keys.Backspace)
        {
            cursorColumn--;
            e.SetKeyCode((int)Keys.Delete);
        }
        if (cursorColumn < 0) { cursorColumn = 0; }
        if (cursorLine < 0) { cursorLine = 0; }
        if (cursorColumn >= maxColumns) { cursorColumn = maxColumns; }
        if (cursorLine > maxLines) { cursorLine = maxLines; }
        if (cursorColumn > LineLength(buffer[cursorLine])) { cursorColumn = LineLength(buffer[cursorLine]); }
        if (e.GetKeyCode() == (int)Keys.Delete)
        {
            for (int i = cursorColumn; i < maxColumns - 1; i++)
            {
                buffer[cursorLine][i] = buffer[cursorLine][i + 1];
            }
        }
        e.SetHandled(true);
    }
    public override void OnKeyPress(Game game_, KeyPressEventArgs e)
    {
        if (!visible)
        {
            return;
        }
        if (e.GetKeyChar() == 8) // backspace
        {
            return;
        }
        for (int i = maxColumns - 1; i > cursorColumn; i--)
        {
            buffer[cursorLine][i] = buffer[cursorLine][i - 1];
        }
        buffer[cursorLine][cursorColumn] = e.GetKeyChar();
        cursorColumn++;
        e.SetHandled(true);
    }

    private string LineToString(int[] line)
    {
        if (line == null)
        {
            return "";
        }
        return StringUtils.CharArrayToString(line, LineLength(line));
    }
    private static int LineLength(int[] line)
    {
        for (int i = 0; i < maxColumns; i++)
        {
            if (line[i] == 0)
            {
                return i;
            }
        }
        return maxColumns;
    }
}
