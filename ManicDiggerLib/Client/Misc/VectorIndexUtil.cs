using OpenTK.Mathematics;

public class VectorIndexUtil
{
    public static int Index3d(int x, int y, int h, int sizex, int sizey)
    {
        return (h * sizey + y) * sizex + x;
    }

    public static int Index2d(int x, int y, int sizex)
    {
        return x + y * sizex;
    }

    public static Vector3 Pos(int index, int sizex, int sizey)
    {
        int x = index % sizex;
        int y = index / sizex % sizey;
        int h = index / (sizex * sizey);
        return new Vector3(x, y, h);
    }

    internal static void PosInt(int index, int sizex, int sizey, ref Vector3i ret)
    {
        int x = index % sizex;
        int y = index / sizex % sizey;
        int h = index / (sizex * sizey);
        ret.X = x;
        ret.Y = y;
        ret.Z = h;
    }

    public static int PosX(int index, int sizex, int sizey)
    {
        return index % sizex;
    }

    public static int PosY(int index, int sizex, int sizey)
    {
        return index / sizex % sizey;
    }

    public static int PosZ(int index, int sizex, int sizey)
    {
        return index / (sizex * sizey);
    }
}
