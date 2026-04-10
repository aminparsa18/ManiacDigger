/// <summary>
/// Displays an FPS counter and frame-time history graph. Toggle with F7 or the "fps" command.
/// </summary>
public class ModFpsHistoryGraph : ModBase
{
    private const int MaxCount = 300;
    private const int ChatFontSize = 11;
    private const int GraphHeight = 80;
    private const int GraphPosX = 25;
    private const int PerLine = 2;

    private ClientModManager m;
    private readonly float[] dtHistory = new float[MaxCount];
    private readonly Draw2dData[] todraw = new Draw2dData[MaxCount];

    private int lasttitleUpdateMilliseconds;
    private int fpsCount;
    private string fpsText;
    private float longestFrameDt;
    private bool drawFpsText;
    private bool drawFpsGraph;

    public override void Start(ClientModManager modmanager)
    {
        m = modmanager;
        for (int i = 0; i < MaxCount; i++)
            todraw[i] = new Draw2dData();
    }

    public override void OnNewFrame(Game game, float args)
    {
        float dt = args;
        UpdateGraph(dt);
        UpdateTitleFps(dt);
        Draw();
    }

    public override void OnKeyDown(Game game, KeyEventArgs args)
    {
        if (args.KeyChar == (int)Keys.F7)
        {
            drawFpsText = !drawFpsGraph;
            drawFpsGraph = !drawFpsGraph;
        }
    }

    public override bool OnClientCommand(Game game, ClientCommandArgs args)
    {
        if (args.command != "fps") return false;

        string arg = args.arguments.Trim();
        (drawFpsText, drawFpsGraph) = arg switch
        {
            "" => (true, false),
            "1" => (true, false),
            "2" => (true, true),
            _ => (false, false)
        };
        return true;
    }

    /// <summary>Shifts frame-time history left and appends the latest dt.</summary>
    private void UpdateGraph(float dt)
    {
        Array.Copy(dtHistory, 1, dtHistory, 0, MaxCount - 1);
        dtHistory[MaxCount - 1] = dt;
    }

    /// <summary>Updates the FPS counter and performance info string once per second.</summary>
    private void UpdateTitleFps(float dt)
    {
        IGamePlatform p = m.GetPlatform();
        fpsCount++;
        longestFrameDt = Math.Max(longestFrameDt, dt);

        int now = p.TimeMillisecondsFromStart;
        float elapsed = (now - lasttitleUpdateMilliseconds) / 1000f;
        if (elapsed < 1f) return;

        lasttitleUpdateMilliseconds = now;

        int fps = (int)(fpsCount / elapsed);
        int minFps = (int)(1f / longestFrameDt);
        longestFrameDt = 0;
        fpsCount = 0;

        m.GetPerformanceInfo()["fps"] = $"FPS: {fps} (min: {minFps})";

        var sb = new System.Text.StringBuilder();
        int idx = 0;
        foreach (string value in m.GetPerformanceInfo().Values)
        {
            sb.Append(value);
            if (idx % PerLine == 0 && idx != m.GetPerformanceInfo().Count - 1)
                sb.Append(", ");
            else if (idx % PerLine != 0)
                sb.Append('\n');
            idx++;
        }
        fpsText = sb.ToString();
    }

    private void Draw()
    {
        if (!drawFpsGraph && !drawFpsText) return;

        m.OrthoMode();
        if (drawFpsGraph) DrawGraph();
        if (drawFpsText) m.Draw2dText(fpsText, 20, 20, ChatFontSize);
        m.PerspectiveMode();
    }

    private void DrawGraph()
    {
        int posx = GraphPosX;
        int posy = m.GetWindowHeight() - GraphHeight - 20;

        int[] colors =
        [
            ColorUtils.ColorFromArgb(255, 0,   0, 0),
            ColorUtils.ColorFromArgb(255, 255, 0, 0)
        ];
        int lineColor = ColorUtils.ColorFromArgb(255, 255, 255, 255);

        for (int i = 0; i < MaxCount; i++)
        {
            float barHeight = dtHistory[i] * 60 * GraphHeight;
            int color = ColorUtils.InterpolateColor((float)i / MaxCount, colors, 2);
            todraw[i].x1 = posx + i;
            todraw[i].y1 = posy - barHeight;
            todraw[i].width = 1;
            todraw[i].height = barHeight;
            todraw[i].inAtlasId = -1;
            todraw[i].color = color;
        }
        m.Draw2dTextures(todraw, MaxCount, m.WhiteTexture());

        // Reference FPS lines
        DrawFpsLine(posy, 60, lineColor);
        DrawFpsLine(posy, 75, lineColor);
        DrawFpsLine(posy, 30, lineColor);
        DrawFpsLine(posy, 150, lineColor);
    }

    private void DrawFpsLine(int posy, int fps, int color)
    {
        float y = posy - GraphHeight * (60f / fps);
        m.Draw2dTexture(m.WhiteTexture(), GraphPosX, y, MaxCount, 1, -1, color);
        m.Draw2dText(fps.ToString(), GraphPosX, y, 6);
    }
}