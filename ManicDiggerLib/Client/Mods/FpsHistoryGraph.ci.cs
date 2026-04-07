using Keys = OpenTK.Windowing.GraphicsLibraryFramework.Keys;

public class ModFpsHistoryGraph : ClientMod
{
    public ModFpsHistoryGraph()
    {
        one = 1;
        drawfpstext = false;
        drawfpsgraph = false;
        dtHistory = new float[MaxCount];
        for (int i = 0; i < MaxCount; i++)
        {
            dtHistory[i] = 0;
        }
        todraw = new Draw2dData[MaxCount];
        for (int i = 0; i < MaxCount; i++)
        {
            todraw[i] = new Draw2dData();
        }
    }

    private readonly float one;
    private int lasttitleupdateMilliseconds;
    private int fpscount;
    private string fpstext;
    private float longestframedt;

    public override void Start(ClientModManager modmanager)
    {
        m = modmanager;
    }
    private readonly float[] dtHistory;
    private const int MaxCount = 300;
    private ClientModManager m;

    public override void OnNewFrame(Game game, NewFrameEventArgs args)
    {
        float dt = args.GetDt();
        UpdateGraph(dt);
        UpdateTitleFps(dt);
        Draw();
    }

    private void UpdateTitleFps(float dt)
    {
        GamePlatform p = m.GetPlatform();
        fpscount++;
        longestframedt = Math.Max(longestframedt, dt);
        float elapsed = one * (p.TimeMillisecondsFromStart() - lasttitleupdateMilliseconds) / 1000;
        if (elapsed >= 1)
        {
            string fpstext1 = "";
            lasttitleupdateMilliseconds = p.TimeMillisecondsFromStart();
            fpstext1 = StringTools.StringAppend(p, fpstext1, string.Format("FPS: {0}", ((int)((one * fpscount) / elapsed)).ToString()));
            fpstext1 = StringTools.StringAppend(p, fpstext1, string.Format(" (min: {0})", ((int)(one / longestframedt)).ToString()));
            longestframedt = 0;
            fpscount = 0;
            m.GetPerformanceInfo()["fps"] = fpstext1;

            string s = "";
            string[] l = new string[64];
            int lCount = 0;
            foreach (string value in m.GetPerformanceInfo().Values)
            {
                l[lCount++] = value;
            }

            int perline = 2;
            for (int i = 0; i < lCount; i++)
            {
                s = StringTools.StringAppend(p, s, l[i]);
                if ((i % perline == 0) && (i != lCount - 1))
                {
                    s = StringTools.StringAppend(p, s, ", ");
                }
                if (i % perline != 0)
                {
                    s = StringTools.StringAppend(p, s, "\n");
                }
            }
            fpstext = s;
        }
    }

    public override void OnKeyDown(Game game, KeyEventArgs args)
    {
        if (args.GetKeyCode() == (int)Keys.F7)
        {
            if (!drawfpsgraph)
            {
                drawfpstext = true;
                drawfpsgraph = true;
            }
            else
            {
                drawfpstext = false;
                drawfpsgraph = false;
            }
        }
    }

    private bool drawfpstext;
    private bool drawfpsgraph;

    public override bool OnClientCommand(Game game, ClientCommandArgs args)
    {
        if (args.command == "fps")
        {
            string[] arguments = m.GetPlatform().StringSplit(args.arguments, " ", out _);
            if (m.GetPlatform().StringTrim(args.arguments) == "")
            {
                drawfpstext = true;
            }
            else if (arguments[0] == "1")
            {
                drawfpstext = true;
                drawfpsgraph = false;
            }
            else if (arguments[0] == "2")
            {
                drawfpstext = true;
                drawfpsgraph = true;
            }
            else
            {
                drawfpstext = false;
                drawfpsgraph = false;
            }
            return true;
        }
        return false;
    }

    private void UpdateGraph(float dt)
    {
        for (int i = 0; i < MaxCount - 1; i++)
        {
            dtHistory[i] = dtHistory[i + 1];
        }
        dtHistory[MaxCount - 1] = dt;
    }

    private void Draw()
    {
        if (drawfpsgraph || drawfpstext)
        {
            m.OrthoMode();
            if (drawfpsgraph)
            {
                DrawGraph();
            }
            if (drawfpstext)
            {
                m.Draw2dText(fpstext, 20, 20, ChatFontSize);
            }
            m.PerspectiveMode();
        }
    }

    private readonly Draw2dData[] todraw;
    private void DrawGraph()
    {
        float maxtime = 0;
        for (int i = 0; i < MaxCount; i++)
        {
            float v = dtHistory[i];
            if (v > maxtime)
            {
                maxtime = v;
            }
        }
        int historyheight = 80;
        int posx = 25;
        int posy = m.GetWindowHeight() - historyheight - 20;
        int[] colors = new int[2];
        colors[0] = Game.ColorFromArgb(255, 0, 0, 0);
        colors[1] = Game.ColorFromArgb(255, 255, 0, 0);
        int linecolor = Game.ColorFromArgb(255, 255, 255, 255);

        for (int i = 0; i < MaxCount; i++)
        {
            float time = dtHistory[i];
            time = (time * 60) * historyheight;
            int c = InterpolationCi.InterpolateColor(m.GetPlatform(), (one * i) / MaxCount, colors, 2);
            todraw[i].x1 = posx + i;
            todraw[i].y1 = posy - time;
            todraw[i].width = 1;
            todraw[i].height = time;
            todraw[i].inAtlasId = -1;
            todraw[i].color = c;
        }
        m.Draw2dTextures(todraw, MaxCount, m.WhiteTexture());

        m.Draw2dTexture(m.WhiteTexture(), posx, posy - historyheight, MaxCount, 1, -1, linecolor);
        m.Draw2dTexture(m.WhiteTexture(), posx, posy - historyheight * (one * 60 / 75), MaxCount, 1, -1, linecolor);
        m.Draw2dTexture(m.WhiteTexture(), posx, posy - historyheight * (one * 60 / 30), MaxCount, 1, -1, linecolor);
        m.Draw2dTexture(m.WhiteTexture(), posx, posy - historyheight * (one * 60 / 150), MaxCount, 1, -1, linecolor);
        m.Draw2dText("60", posx, posy - historyheight * (one * 60 / 60), 6);
        m.Draw2dText("75", posx, posy - historyheight * (one * 60 / 75), 6);
        m.Draw2dText("30", posx, posy - historyheight * (one * 60 / 30), 6);
        m.Draw2dText("150", posx, posy - historyheight * (one * 60 / 150), 6);
    }

    private const int ChatFontSize = 11;
}
