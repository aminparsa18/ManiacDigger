public class Node
{
    internal string name;
    internal string parentName;
    internal float posx;
    internal float posy;
    internal float posz;
    internal float rotatex;
    internal float rotatey;
    internal float rotatez;
    internal float sizex;
    internal float sizey;
    internal float sizez;
    internal float u;
    internal float v;
    internal float pivotx;
    internal float pivoty;
    internal float pivotz;
    internal float scalex;
    internal float scaley;
    internal float scalez;
    internal float head;
}

public class KeyframeType
{
    public const int Position = 1;
    public const int Rotation = 2;
    public const int Size = 3;
    public const int Pivot = 4;
    public const int Scale = 5;

    public static string GetName(int p)
    {
        if (p == Position) { return "pos"; }
        if (p == Rotation) { return "rot"; }
        if (p == Size) { return "siz"; }
        if (p == Pivot) { return "piv"; }
        if (p == Scale) { return "sca"; }
        return "";
    }

    public static int GetValue(string p)
    {
        if (p == "pos") { return Position; }
        if (p == "rot") { return Rotation; }
        if (p == "siz") { return Size; }
        if (p == "piv") { return Pivot; }
        if (p == "sca") { return Scale; }
        return 0;
    }
}

public class Keyframe
{
    internal string animationName;
    internal string nodeName;
    internal int frame;
    internal int keyframeType;
    internal float x;
    internal float y;
    internal float z;
}

public class Animation
{
    internal string name;
    internal int length;
}

public class AnimationGlobal
{
    internal int texw;
    internal int texh;
}

public class AnimatedModel
{
    public AnimatedModel()
    {
        global = new AnimationGlobal();
    }
    internal Node[] nodes;
    internal int nodesCount;
    internal Keyframe[] keyframes;
    internal int keyframesCount;
    internal Animation[] animations;
    internal int animationsCount;
    internal AnimationGlobal global;
}

public class AnimatedModelBinding : TableBinding
{
    internal GamePlatform p;
    internal AnimatedModel m;
    public override void Set(string table, int index, string column, string value)
    {
        if (table == "nodes")
        {
            if (index >= m.nodesCount) { m.nodesCount = index + 1; }
            if (m.nodes[index] == null) { m.nodes[index] = new Node(); }
            Node k = m.nodes[index];
            if (column == "name") { k.name = value; }
            if (column == "paren") { k.parentName = value; }
            if (column == "x") { k.posx = FloatParse(value); }
            if (column == "y") { k.posy = FloatParse(value); }
            if (column == "z") { k.posz = FloatParse(value); }
            if (column == "rotx") { k.rotatex = FloatParse(value); }
            if (column == "roty") { k.rotatey = FloatParse(value); }
            if (column == "rotz") { k.rotatez = FloatParse(value); }
            if (column == "sizex") { k.sizex = FloatParse(value); }
            if (column == "sizey") { k.sizey = FloatParse(value); }
            if (column == "sizez") { k.sizez = FloatParse(value); }
            if (column == "u") { k.u = FloatParse(value); }
            if (column == "v") { k.v = FloatParse(value); }
            if (column == "pivx") { k.pivotx = FloatParse(value); }
            if (column == "pivy") { k.pivoty = FloatParse(value); }
            if (column == "pivz") { k.pivotz = FloatParse(value); }
            if (column == "scalx") { k.scalex = FloatParse(value); }
            if (column == "scaly") { k.scaley = FloatParse(value); }
            if (column == "scalz") { k.scalez = FloatParse(value); }
            if (column == "head") { k.head = FloatParse(value); }
        }
        if (table == "keyframes")
        {
            if (index >= m.keyframesCount) { m.keyframesCount = index + 1; }
            if (m.keyframes[index] == null) { m.keyframes[index] = new Keyframe(); }
            Keyframe k = m.keyframes[index];
            if (column == "anim") { k.animationName = value; }
            if (column == "node") { k.nodeName = value; }
            if (column == "frame") { k.frame = IntParse(value); }
            if (column == "type") { k.keyframeType = KeyframeType.GetValue(value); }
            if (column == "x") { k.x = FloatParse(value); }
            if (column == "y") { k.y = FloatParse(value); }
            if (column == "z") { k.z = FloatParse(value); }
        }
        if (table == "animations")
        {
            if (index >= m.animationsCount) { m.animationsCount = index + 1; }
            if (m.animations[index] == null) { m.animations[index] = new Animation(); }
            Animation k = m.animations[index];
            if (column == "name") { k.name = value; }
            if (column == "len") { k.length = IntParse(value); }
        }
        if (table == "global")
        {
            AnimationGlobal global = m.global;
            if (column == "texw") { global.texw = IntParse(value); }
            if (column == "texh") { global.texh = IntParse(value); }
        }
    }

    public override void Get(string table, int index, Dictionary<string, string> items)
    {
        if (table == "nodes")
        {
            Node k = m.nodes[index];
            items["name"] = k.name;
            items["paren"] = k.parentName;
            items["x"] = p.FloatToString(k.posx);
            items["y"] = p.FloatToString(k.posy);
            items["z"] = p.FloatToString(k.posz);
            items["rotx"] = p.FloatToString(k.rotatex);
            items["roty"] = p.FloatToString(k.rotatey);
            items["rotz"] = p.FloatToString(k.rotatez);
            items["sizex"] = p.FloatToString(k.sizex);
            items["sizey"] = p.FloatToString(k.sizey);
            items["sizez"] = p.FloatToString(k.sizez);
            items["u"] = p.FloatToString(k.u);
            items["v"] = p.FloatToString(k.v);
            items["pivx"] = p.FloatToString(k.pivotx);
            items["pivy"] = p.FloatToString(k.pivoty);
            items["pivz"] = p.FloatToString(k.pivotz);
            items["scalx"] = p.FloatToString(k.scalex);
            items["scaly"] = p.FloatToString(k.scaley);
            items["scalz"] = p.FloatToString(k.scalez);
            items["head"] = p.FloatToString(k.head);
        }
        if (table == "keyframes")
        {
            Keyframe k = m.keyframes[index];
            items["anim"] = k.animationName;
            items["node"] = k.nodeName;
            items["frame"] = p.FloatToString(k.frame);
            items["type"] = KeyframeType.GetName(k.frame);
            items["x"] = p.FloatToString(k.x);
            items["y"] = p.FloatToString(k.y);
            items["z"] = p.FloatToString(k.z);
        }
        if (table == "animations")
        {
            Animation k = m.animations[index];
            items["name"] = k.name;
            items["len"] = p.FloatToString(k.length);
        }
        if (table == "global")
        {
            AnimationGlobal global = m.global;
            items["texw"] = p.FloatToString(global.texw);
            items["texh"] = p.FloatToString(global.texh);
        }
    }

    public void GetTables(string[] name, int[] count)
    {
        name[0] = "nodes"; count[0] = m.nodesCount;
        name[1] = "keyframes"; count[1] = m.keyframesCount;
        name[2] = "animations"; count[2] = m.animationsCount;
        name[3] = "global"; count[3] = 1;
    }

    private int IntParse(string s)
    {
        return p.FloatToInt(FloatParse(s));
    }

    private float FloatParse(string s)
    {
        p.FloatTryParse(s, out float ret);
        return ret;
    }
}

public abstract class TableBinding
{
    public abstract void Set(string table, int index, string column, string value);
    public abstract void Get(string table, int index, Dictionary<string, string> items);
}

public class TableSerializer
{
    public static void Deserialize(GamePlatform p, string data, TableBinding b)
    {
        string[] lines = p.ReadAllLines(data, out int linesCount);
        string[] header = null;
        string current = "";
        int currentI = 0;
        for (int i = 0; i < linesCount; i++)
        {
            string s = p.StringTrim(lines[i]);
            if (s == "")
            {
                continue;
            }
            if (p.StringStartsWithIgnoreCase(s, "//")
                || p.StringStartsWithIgnoreCase(s, "#"))
            {
                continue;
            }
            if (p.StringStartsWithIgnoreCase(s, "section="))
            {
                current = p.StringReplace(s, "section=", "");

                string sHeader = p.StringTrim(lines[i + 1]);
                header = p.StringSplit(sHeader, "\t", out _);
                i++; // header
                currentI = 0;
                continue;
            }
            {
                if (header == null)
                {
                    continue;
                }
                string[] ss = p.StringSplit(s, "\t", out int ssLength);
                for (int k = 0; k < ssLength; k++)
                {
                    b.Set(current, currentI, header[k], ss[k]);
                }

                currentI++;
            }
        }
    }
}

public class AnimatedModelSerializer
{
    public static AnimatedModel Deserialize(GamePlatform p, string data)
    {
        AnimatedModel model = new()
        {
            nodes = new Node[256],
            keyframes = new Keyframe[1024],
            animations = new Animation[128]
        };
        AnimatedModelBinding b = new()
        {
            p = p,
            m = model
        };
        TableSerializer s = new();
        TableSerializer.Deserialize(p, data, b);
        return model;
    }

    public static string Serialize(GamePlatform p, AnimatedModel m)
    {
        return null;
    }
}

public class AnimatedModelRenderer
{
    public AnimatedModelRenderer()
    {
        one = 1;
        tempframes = new Keyframe[256];
        tempVec3 = new float[3];
    }
    private readonly float one;
    internal Game game;
    public void Start(Game game_, AnimatedModel model_)
    {
        game = game_;
        m = model_;
    }

    private AnimatedModel m;

    private readonly int anim;
    private const int fps = 60;
    private float frame;
    public void Render(float dt, float headDeg, bool walkAnimation, bool moves, float light)
    {
        if (m == null) { return; }
        if (m.animations == null) { return; }
        if (m.animations[anim] == null) { return; }
        float length = m.animations[anim].length;
        if (moves)
        {
            frame += dt * fps;
            frame = frame % length;
        }
        if (walkAnimation)
        {
            if (!moves)
            {
                if (frame != 0 && frame != length / 2 && frame != length)
                {
                    if (frame < length / 2)
                    {
                        frame += dt * fps;
                        if (frame > length / 2)
                        {
                            frame = length / 2;
                        }
                    }
                    else
                    {
                        frame += dt * fps;
                        if (frame > length)
                        {
                            frame = length;
                        }
                    }
                }
            }
        }
        DrawNode("root", headDeg, light);
    }

    private readonly float[] tempVec3;
    private void DrawNode(string parent, float headDeg, float light)
    {
        for (int i = 0; i < m.nodesCount; i++)
        {
            Node n = m.nodes[i];
            if (n == null)
            {
                continue;
            }
            if (n.parentName != parent)
            {
                continue;
            }
            game.GLPushMatrix();
            RectangleF[] r = new RectangleF[6];
            r = CuboidRenderer.CuboidNet(n.sizex, n.sizey, n.sizez, n.u, n.v);
            CuboidRenderer.CuboidNetNormalize(r, m.global.texw, m.global.texh);
            GetAnimation(n, tempVec3, KeyframeType.Scale);
            if (tempVec3[0] != 0 && tempVec3[1] != 0 && tempVec3[2] != 0)
            {
                game.GLScale(tempVec3[0], tempVec3[1], tempVec3[2]);
            }
            GetAnimation(n, tempVec3, KeyframeType.Position);
            tempVec3[0] /= 16;
            tempVec3[1] /= 16;
            tempVec3[2] /= 16;
            if (!IsZero(tempVec3))
            {
                game.GLTranslate(tempVec3[0], tempVec3[1], tempVec3[2]);
            }
            GetAnimation(n, tempVec3, KeyframeType.Rotation);
            if (tempVec3[0] != 0)
            {
                game.GLRotate(tempVec3[0], 1, 0, 0);
            }
            if (tempVec3[1] != 0)
            {
                game.GLRotate(tempVec3[1], 0, 1, 0);
            }
            if (tempVec3[2] != 0)
            {
                game.GLRotate(tempVec3[2], 0, 0, 1);
            }
            if (n.head == 1)
            {
                game.GLRotate(headDeg, 1, 0, 0);
            }
            GetAnimation(n, tempVec3, KeyframeType.Pivot);
            tempVec3[0] /= 16;
            tempVec3[1] /= 16;
            tempVec3[2] /= 16;
            game.GLTranslate(tempVec3[0], tempVec3[1], tempVec3[2]);
            GetAnimation(n, tempVec3, KeyframeType.Size);
            tempVec3[0] /= 16;
            tempVec3[1] /= 16;
            tempVec3[2] /= 16;
            
            // Use binary minus because unary minus is truncated to integer in cito
            CuboidRenderer.DrawCuboid2(game, 0 - tempVec3[0] / 2, 0 - tempVec3[1] / 2, 0 - tempVec3[2] / 2, tempVec3[0], tempVec3[1], tempVec3[2], r, light);

            DrawNode(n.name, headDeg, light);
            game.GLPopMatrix();
        }
    }

    private static bool IsZero(float[] vec)
    {
        return vec[0] == 0
            && vec[1] == 0
            && vec[2] == 0;
    }

    private void GetAnimation(Node node, float[] ret, int type)
    {
        GetFrames(node.name, type, tempframes, out int tempframesCount);
        int currentI = GetFrameCurrent(tempframes, tempframesCount);
        if (currentI == -1)
        {
            GetDefaultFrame(node, type, ret);
            return;
        }
        int nextI = (currentI + 1) % tempframesCount;

        Keyframe current = tempframes[currentI];
        Keyframe next = tempframes[nextI];
        float t;
        float length = m.animations[anim].length;
        if (next.frame == current.frame)
        {
            t = 0;
        }
        else if (next.frame > current.frame)
        {
            t = (frame - current.frame) / (next.frame - current.frame);
        }
        else
        {
            float end = 0;
            float begin = 0;
            if (frame >= current.frame)
            {
                end = (frame - current.frame);
            }
            else
            {
                end = (length - current.frame);
                begin = frame;
            }
            t = (end + begin) / ((length - current.frame) + next.frame);
        }
        ret[0] = Lerp(current.x, next.x, t);
        ret[1] = Lerp(current.y, next.y, t);
        ret[2] = Lerp(current.z, next.z, t);
    }

    private static void GetDefaultFrame(Node node, int type, float[] ret)
    {
        switch (type)
        {
            case KeyframeType.Position:
                {
                    ret[0] = node.posx;
                    ret[1] = node.posy;
                    ret[2] = node.posz;
                }
                break;
            case KeyframeType.Rotation:
                {
                    ret[0] = node.rotatex;
                    ret[1] = node.rotatey;
                    ret[2] = node.rotatez;
                }
                break;
            case KeyframeType.Size:
                {
                    ret[0] = node.sizex;
                    ret[1] = node.sizey;
                    ret[2] = node.sizez;
                }
                break;
            case KeyframeType.Pivot:
                {
                    ret[0] = node.pivotx;
                    ret[1] = node.pivoty;
                    ret[2] = node.pivotz;
                }
                break;
            case KeyframeType.Scale:
                {
                    ret[0] = node.scalex;
                    ret[1] = node.scaley;
                    ret[2] = node.scalez;
                }
                break;
        }
    }

    private static float Lerp(float v0, float v1, float t)
    {
        return v0 + (v1 - v0) * t;
    }

    private void GetFrames(string nodeName, int type, Keyframe[] frames, out int count)
    {
        count = 0;

        string animName = m.animations[anim].name;

        for (int i = 0; i < m.keyframesCount; i++)
        {
            Keyframe k = m.keyframes[i];
            if (k == null)
            {
                continue;
            }
            if (k.nodeName != nodeName)
            {
                continue;
            }
            if (k.animationName != animName)
            {
                continue;
            }
            if (k.keyframeType != type)
            {
                continue;
            }

            frames[count++] = k;
        }
    }

    private readonly Keyframe[] tempframes;
    private int GetFrameCurrent(Keyframe[] frames, int framesCount)
    {
        string animName = m.animations[anim].name;
        int current = -1;
        for (int i = 0; i < framesCount; i++)
        {
            Keyframe k = frames[i];
            if (k.frame <= frame)
            {
                //any previous frame
                if (current == -1)
                {
                    current = i;
                }
                else
                {
                    //closest previous frame
                    if (k.frame > frames[current].frame)
                    {
                        current = i;
                    }
                }
            }

        }
        if (current == -1)
        {
            //not found. use last frame
            for (int i = 0; i < framesCount; i++)
            {
                Keyframe k = frames[i];
                if (current == -1 || k.frame > frames[current].frame)
                {
                    current = i;
                }
            }
        }
        return current;
    }
}

public class CuboidRenderer
{
    //Maps description of position of 6 faces
    //of a single cuboid in texture file to UV coordinates (in pixels)
    //(one RectangleF in texture file for each 3d face of cuboid).
    //Arguments:
    // Size (in pixels) in 2d cuboid net.
    // Start position of 2d cuboid net in texture file.
    public static RectangleF[] CuboidNet(float tsizex, float tsizey, float tsizez, float tstartx, float tstarty)
    {
        RectangleF[] coords = new RectangleF[6];
        {
            coords[0] = new RectangleF(tsizez + tstartx, tsizez + tstarty, tsizex, tsizey);//front
            coords[1] = new RectangleF(2 * tsizez + tsizex + tstartx, tsizez + tstarty, tsizex, tsizey);//back
            coords[2] = new RectangleF(tstartx, tsizez + tstarty, tsizez, tsizey);//right
            coords[3] = new RectangleF(tsizez + tsizex + tstartx, tsizez + tstarty, tsizez, tsizey);//left
            coords[4] = new RectangleF(tsizez + tstartx, tstarty, tsizex, tsizez);//top
            coords[5] = new RectangleF(tsizez + tsizex + tstartx, tstarty, tsizex, tsizez);//bottom
        }
        return coords;
    }

    //Divides CuboidNet() result by texture size, to get relative coordinates. (0-1, not 0-32 pixels).
    public static void CuboidNetNormalize(RectangleF[] coords, float texturewidth, float textureheight)
    {
        float AtiArtifactFix = 0.15f;
        for (int i = 0; i < 6; i++)
        {
            float x = ((coords[i].X + AtiArtifactFix) / texturewidth);
            float y = ((coords[i].Y + AtiArtifactFix) / textureheight);
            float w = ((coords[i].X + coords[i].Width - AtiArtifactFix) / texturewidth) - x;
            float h = ((coords[i].Y + coords[i].Height - AtiArtifactFix) / textureheight) - y;
            coords[i] = new RectangleF(x, y, w, h);
        }
    }
    public static void DrawCuboid(Game game, float posX, float posY, float posZ,
        float sizeX, float sizeY, float sizeZ,
        RectangleF[] texturecoords, float light)
    {
        ModelData data = new()
        {
            xyz = new float[4 * 6 * 3],
            uv = new float[4 * 6 * 2],
            rgba = new byte[4 * 6 * 4]
        };
        int light255 = game.platform.FloatToInt(light * 255);
        int color = Game.ColorFromArgb(255, light255, light255, light255);

        RectangleF rect;

        //front
        rect = texturecoords[0];
        AddVertex(data, posX, posY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //back
        rect = texturecoords[1];
        AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //left
        rect = texturecoords[2];
        AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //right
        rect = texturecoords[3];
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);

        //top
        rect = texturecoords[4];
        AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //bottom
        rect = texturecoords[5];
        AddVertex(data, posX, posY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Y, color);

        data.indices = new int[6 * 6];
        for (int i = 0; i < 6; i++)
        {
            data.indices[i * 6 + 0] = i * 4 + 3;
            data.indices[i * 6 + 1] = i * 4 + 2;
            data.indices[i * 6 + 2] = i * 4 + 0;
            data.indices[i * 6 + 3] = i * 4 + 2;
            data.indices[i * 6 + 4] = i * 4 + 1;
            data.indices[i * 6 + 5] = i * 4 + 0;
        }
        data.indicesCount = 36;

        game.platform.GlDisableCullFace();
        game.DrawModelData(data);
        game.platform.GlEnableCullFace();
    }

    public static void AddVertex(ModelData model, float x, float y, float z, float u, float v, int color)
    {
        model.xyz[model.GetXyzCount() + 0] = x;
        model.xyz[model.GetXyzCount() + 1] = y;
        model.xyz[model.GetXyzCount() + 2] = z;
        model.uv[model.GetUvCount() + 0] = u;
        model.uv[model.GetUvCount() + 1] = v;
        model.rgba[model.GetRgbaCount() + 0] = Game.IntToByte(Game.ColorR(color));
        model.rgba[model.GetRgbaCount() + 1] = Game.IntToByte(Game.ColorG(color));
        model.rgba[model.GetRgbaCount() + 2] = Game.IntToByte(Game.ColorB(color));
        model.rgba[model.GetRgbaCount() + 3] = Game.IntToByte(Game.ColorA(color));
        model.verticesCount++;
    }

    public static void DrawCuboid2(Game game, float posX, float posY, float posZ,
        float sizeX, float sizeY, float sizeZ,
        RectangleF[] texturecoords, float light)
    {
        ModelData data = new()
        {
            xyz = new float[4 * 6 * 3],
            uv = new float[4 * 6 * 2],
            rgba = new byte[4 * 6 * 4]
        };
        int light255 = game.platform.FloatToInt(light * 255);
        int color = Game.ColorFromArgb(255, light255, light255, light255);

        RectangleF rect;

        //right
        rect = texturecoords[2];
        AddVertex(data, posX, posY, posZ, rect.X, rect.Bottom , color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //left
        rect = texturecoords[3];
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY, posZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X, rect.Y, color);

        //back
        rect = texturecoords[1];
        AddVertex(data, posX + sizeX, posY, posZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X, rect.Y, color);

        //front
        rect = texturecoords[0];
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X, rect.Y, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Y, color);

        //top
        rect = texturecoords[4];
        AddVertex(data, posX, posY + sizeY, posZ, rect.X, rect.Y, color);
        AddVertex(data, posX, posY + sizeY, posZ + sizeZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY + sizeY, posZ, rect.X + rect.Width, rect.Y, color);

        //bottom
        rect = texturecoords[5];
        AddVertex(data, posX, posY, posZ, rect.X, rect.Y, color);
        AddVertex(data, posX, posY, posZ + sizeZ, rect.X, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY, posZ + sizeZ, rect.X + rect.Width, rect.Bottom, color);
        AddVertex(data, posX + sizeX, posY, posZ, rect.X + rect.Width, rect.Y, color);

        data.indices = new int[6 * 6];
        for (int i = 0; i < 6; i++)
        {
            data.indices[i * 6 + 0] = i * 4 + 3;
            data.indices[i * 6 + 1] = i * 4 + 2;
            data.indices[i * 6 + 2] = i * 4 + 0;
            data.indices[i * 6 + 3] = i * 4 + 2;
            data.indices[i * 6 + 4] = i * 4 + 1;
            data.indices[i * 6 + 5] = i * 4 + 0;
        }
        data.indicesCount = 36;



        game.platform.GlDisableCullFace();
        game.DrawModelData(data);
        game.platform.GlEnableCullFace();
    }
}

public class AnimationState
{
    public AnimationState()
    {
        bodyrotation = -1;
        light = 1;
    }
    internal float interp;
    internal float headbodydelta;
    internal bool fullbodyrotate;
    internal float lastheading;
    internal float bodyrotation;
    internal float speed;
    internal float light;

    public float GetInterp()
    {
        return interp;
    }
}

public class AnimationHint
{
    internal bool InVehicle;
    internal float DrawFixX;
    internal float DrawFixY;
    internal float DrawFixZ;
    internal bool leanleft;
    internal bool leanright;
}
