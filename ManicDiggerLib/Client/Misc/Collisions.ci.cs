using OpenTK.Mathematics;

public class Line3D
{
    internal Vector3 Start;
    internal Vector3 End;
}

public class Box3D
{
    public void Set(float x, float y, float z, float size)
    {
        if (MinEdge == null)
        {
            MinEdge = Vector3.Zero;
            MaxEdge = Vector3.Zero;
        }
        MinEdge[0] = x;
        MinEdge[1] = y;
        MinEdge[2] = z;
        MaxEdge[0] = x + size;
        MaxEdge[1] = y + size;
        MaxEdge[2] = z + size;
    }
    internal new Vector3 MinEdge;
    internal new Vector3 MaxEdge;
    //public Vector3 MaxEdge { get { return new Vector3(MinEdge.X + size, MinEdge.Y + size, MinEdge.Z + size); } }
    //float size;
    public float LengthX() { return MaxEdge[0] - MinEdge[0]; }
    public float LengthY() { return MaxEdge[1] - MinEdge[1]; }
    public float LengthZ() { return MaxEdge[2] - MinEdge[2]; }
    public void AddPoint(float x, float y, float z)
    {
        //if is empty
        if (MinEdge == null || MaxEdge == null ||
            (MinEdge[0] == 0 && MinEdge[1] == 0 && MinEdge[2] == 0
            && MaxEdge[0] == 0 && MaxEdge[1] == 0 && MaxEdge[2] == 0))
        {
            MinEdge = new Vector3(x, y, z);
            MaxEdge = new Vector3(x, y, z);
        }
        MinEdge[0] = MathCi.MinFloat(MinEdge[0], x);
        MinEdge[1] = MathCi.MinFloat(MinEdge[1], y);
        MinEdge[2] = MathCi.MinFloat(MinEdge[2], z);
        MaxEdge[0] = MathCi.MaxFloat(MaxEdge[0], x);
        MaxEdge[1] = MathCi.MaxFloat(MaxEdge[1], y);
        MaxEdge[2] = MathCi.MaxFloat(MaxEdge[2], z);
    }
    public static float[] Center()
    {
        return null;
    }

    internal static Box3D Create(int x, int y, int z, int size)
    {
        Box3D b = new();
        b.Set(x, y, z, size);
        return b;
    }
}

public abstract class PredicateBox3D
{
    public abstract bool Hit(Box3D o);
}
public class ListBox3d
{
    internal Box3D[] arr;
    internal int count;
}

public class TileSide
{
    public const int Top = 0;
    public const int Bottom = 1;
    public const int Front = 2;
    public const int Back = 3;
    public const int Left = 4;
    public const int Right = 5;
}

public class BlockPosSide
{
    public BlockPosSide()
    {
    }
    public static BlockPosSide Create(int x, int y, int z)
    {
        BlockPosSide p = new()
        {
            blockPos = new Vector3(x, y, z)
        };
        return p;
    }
    internal Vector3 blockPos;
    internal Vector3 collisionPos;
    public float[] Translated()
    {
        float[] translated = new float[3];
        translated[0] = blockPos[0];
        translated[1] = blockPos[1];
        translated[2] = blockPos[2];

        if (collisionPos == null)
        {
            return translated;
        }

        if (collisionPos[0] == blockPos[0] ) { translated[0] = translated[0] - 1; }
        if (collisionPos[1] == blockPos[1] ) { translated[1] = translated[1] - 1; }
        if (collisionPos[2] == blockPos[2] ) { translated[2] = translated[2] - 1; }
        if (collisionPos[0] == blockPos[0] + 1) { translated[0] = translated[0] + 1; }
        if (collisionPos[1] == blockPos[1] + 1) { translated[1] = translated[1] + 1; }
        if (collisionPos[2] == blockPos[2] + 1) { translated[2] = translated[2] + 1; }

        return translated;
    }
    public Vector3 Current()
    {
        return blockPos;
    }
}

public class BlockOctreeSearcher
{
    internal GamePlatform platform;
    public BlockOctreeSearcher()
    {
        intersection = new Intersection();
        pool = new Box3D[10000];
        for (int i = 0; i < 10000; i++)
        {
            pool[i] = new Box3D();
        }
        listpool = new ListBox3d[50];
        for (int i = 0; i < 50; i++)
        {
            listpool[i] = new ListBox3d
            {
                arr = new Box3D[1000]
            };
        }
        l = new BlockPosSide[1024];
        lCount = 0;
        currentHit = Vector3.Zero;
    }
    internal Box3D StartBox;
    private ListBox3d Search(PredicateBox3D query)
    {
        pool_i = 0;
        listpool_i = 0;
        if (StartBox.LengthX() == 0 && StartBox.LengthY() == 0 && StartBox.LengthZ() == 0)
        {
            return new ListBox3d();
        }
        return SearchPrivate(query, StartBox);
    }
    private ListBox3d SearchPrivate(PredicateBox3D query, Box3D box)
    {
        if (box.LengthX() == 1)
        {
            ListBox3d l1 = newListBox3d();
            l1.count = 1;
            l1.arr[0] = box;
            return l1;
        }
        ListBox3d l = newListBox3d();
        l.count = 0;
        ListBox3d children = Children(box);
        for (int k = 0; k < children.count; k++)
        {
            Box3D child = children.arr[k];
            if (query.Hit(child))
            {
                ListBox3d l2 = SearchPrivate(query, child);
                for (int i = 0; i < l2.count; i++)
                {
                    Box3D n = l2.arr[i];
                    l.arr[l.count++] = n;
                }
                recycleListBox3d(l2);
            }
        }
        recycleListBox3d(children);
        return l;
    }
    private readonly Box3D[] pool;
    private int pool_i;
    private readonly ListBox3d[] listpool;
    private int listpool_i;
    private Box3D newBox3d()
    {
        return pool[pool_i++];
    }
    private void recycleBox3d(Box3D l)
    {
        pool_i--;
        pool[pool_i] = l;
    }
    private ListBox3d newListBox3d()
    {
        ListBox3d l = listpool[listpool_i++];
        l.count = 0;
        return l;
    }
    private void recycleListBox3d(ListBox3d l)
    {
        listpool_i--;
        listpool[listpool_i] = l;
    }
    private ListBox3d Children(Box3D box)
    {
        ListBox3d l = newListBox3d();
        l.count = 8;
        Box3D[] c = l.arr;
        for (int i = 0; i < 8; i++)
        {
            c[i] = newBox3d();
        }
        float x = box.MinEdge[0];
        float y = box.MinEdge[1];
        float z = box.MinEdge[2];
        float size = box.LengthX() / 2;
        c[0].Set(x, y, z, size);
        c[1].Set(x + size, y, z, size);
        c[2].Set(x, y, z + size, size);
        c[3].Set(x + size, y, z + size, size);

        c[4].Set(x, y + size, z, size);
        c[5].Set(x + size, y + size, z, size);
        c[6].Set(x, y + size, z + size, size);
        c[7].Set(x + size, y + size, z + size, size);
        return l;
    }

    public bool BoxHit(Box3D box)
    {
        currentHit = Vector3.Zero;
        return Intersection.CheckLineBox(box, currentLine, out currentHit);
    }

    private Line3D currentLine;
    private Vector3 currentHit;
    private readonly Intersection intersection;
    private readonly BlockPosSide[] l;
    private int lCount;
    public BlockPosSide[] LineIntersection(DelegateIsBlockEmpty isEmpty, DelegateGetBlockHeight getBlockHeight, Line3D line, IntRef retCount)
    {
        lCount = 0;
        currentLine = line;
        currentHit[0] = 0;
        currentHit[1] = 0;
        currentHit[2] = 0;
        ListBox3d l1 = Search(PredicateBox3DHit.Create(this));
        for (int i = 0; i < l1.count; i++)
        {
            Box3D node = l1.arr[i];
            var hit = currentHit;
            float x = node.MinEdge[0];
            float y = node.MinEdge[2];
            float z = node.MinEdge[1];
            if (!isEmpty.IsBlockEmpty(platform.FloatToInt(x),platform.FloatToInt(y),platform.FloatToInt( z)))
            {
                Box3D node2 = new()
                {
                    MinEdge = node.MinEdge,
                    MaxEdge = node.MaxEdge
                };
                node2.MaxEdge[1] = node2.MinEdge[1] + getBlockHeight.GetBlockHeight(platform.FloatToInt(x),platform.FloatToInt(y),platform.FloatToInt(z));

                BlockPosSide b = new();
                Vector3 hit2 = Vector3.Zero;

                float[] dir = [line.End[0] - line.Start[0], line.End[1] - line.Start[1], line.End[2] - line.Start[2]];
                bool ishit = Intersection.HitBoundingBox(node2.MinEdge, node2.MaxEdge, line.Start, dir, out hit2);
                if (ishit)
                {
                    //hit2.pos = Vec3.FromValues(x, z, y);
                    b.blockPos = new Vector3(platform.FloatToInt(x), platform.FloatToInt(z), platform.FloatToInt(y));
                    b.collisionPos = hit2;
                    l[lCount++] = b;
                }
            }
        }
        BlockPosSide[] ll = new BlockPosSide[lCount];
        for (int i = 0; i < lCount; i++)
        {
            ll[i] = l[i];
        }
        retCount.value = lCount;
        return ll;
    }
}

public class PredicateBox3DHit : PredicateBox3D
{
    public static PredicateBox3DHit Create(BlockOctreeSearcher s_)
    {
        PredicateBox3DHit p = new()
        {
            s = s_
        };
        return p;
    }
    private BlockOctreeSearcher s;
    public override bool Hit(Box3D o)
    {
        return s.BoxHit(o);
    }
}
public abstract class DelegateIsBlockEmpty
{
    public abstract bool IsBlockEmpty(int x, int y, int z);
}
public abstract class DelegateGetBlockHeight
{
    public abstract float GetBlockHeight(int x, int y, int z);
}

public class Intersection
{
    public Intersection() { }
    // http://tog.acm.org/resources/GraphicsGems/gems/RayBox.c
    // Fast Ray-Box Intersection
    // by Andrew Woo
    // from "Graphics Gems", Academic Press, 1990
    private const int LEFT = 1;
    private const int MIDDLE = 2;
    private const int RIGHT = 0;
    public static bool HitBoundingBox(Vector3 minB, Vector3 maxB, Vector3 origin, float[] dir, out Vector3 coord)
    {
        bool inside = true;
        byte[] quadrant = new byte[3];
        int i;
        int whichPlane;
        float[] maxT = new float[3];
        float[] candidatePlane = new float[3];

        coord = Vector3.Zero;

        // Find candidate planes; this loop can be avoided if
        // rays cast all from the eye(assume perpsective view)
        for (i = 0; i < 3; i++)
            if (origin[i] < minB[i])
            {
                quadrant[i] = LEFT;
                candidatePlane[i] = minB[i];
                inside = false;
            }
            else if (origin[i] > maxB[i])
            {
                quadrant[i] = RIGHT;
                candidatePlane[i] = maxB[i];
                inside = false;
            }
            else
            {
                quadrant[i] = MIDDLE;
            }

        // Ray origin inside bounding box
        if (inside)
        {
            coord = origin;
            return true;
        }

        // Calculate T distances to candidate planes
        for (i = 0; i < 3; i++)
            if (quadrant[i] != MIDDLE && dir[i] != 0)
                maxT[i] = (candidatePlane[i] - origin[i]) / dir[i];
            else
                maxT[i] = -1;

        // Get largest of the maxT's for final choice of intersection
        whichPlane = 0;
        for (i = 1; i < 3; i++)
            if (maxT[whichPlane] < maxT[i])
                whichPlane = i;

        // Check final candidate actually inside box
        if (maxT[whichPlane] < 0) return false;

        for (i = 0; i < 3; i++)
            if (whichPlane != i)
            {
                coord[i] = origin[i] + maxT[whichPlane] * dir[i];
                if (coord[i] < minB[i] || coord[i] > maxB[i])
                    return false;
            }
            else
            {
                coord[i] = candidatePlane[i];
            }

        return true; // ray hits box
    }

    private static bool GetIntersection(float fDst1, float fDst2, Vector3 p1, Vector3 p2, out Vector3 hit)
    {
        hit = Vector3.Zero;
        if ((fDst1 * fDst2) >= 0) return false;
        if (fDst1 == fDst2) return false;
        hit = p1 + (p2 - p1) * (-fDst1 / (fDst2 - fDst1));
        return true;
    }

    private static bool InBox(Vector3 hit, Vector3 b1, Vector3 b2, int axis)
    {
        if (axis == 1 && hit.Z > b1.Z && hit.Z < b2.Z && hit.Y > b1.Y && hit.Y < b2.Y) return true;
        if (axis == 2 && hit.Z > b1.Z && hit.Z < b2.Z && hit.X > b1.X && hit.X < b2.X) return true;
        if (axis == 3 && hit.X > b1.X && hit.X < b2.X && hit.Y > b1.Y && hit.Y < b2.Y) return true;
        return false;
    }

    // returns true if line (L1, L2) intersects with the box (B1, B2)
    // returns intersection point in Hit
    public static bool CheckLineBox1(Vector3 b1, Vector3 b2, Vector3 l1, Vector3 l2, out Vector3 hit)
    {
        hit = Vector3.Zero;
        if (l2.X < b1.X && l1.X < b1.X) return false;
        if (l2.X > b2.X && l1.X > b2.X) return false;
        if (l2.Y < b1.Y && l1.Y < b1.Y) return false;
        if (l2.Y > b2.Y && l1.Y > b2.Y) return false;
        if (l2.Z < b1.Z && l1.Z < b1.Z) return false;
        if (l2.Z > b2.Z && l1.Z > b2.Z) return false;
        if (l1.X > b1.X && l1.X < b2.X &&
            l1.Y > b1.Y && l1.Y < b2.Y &&
            l1.Z > b1.Z && l1.Z < b2.Z)
        {
            hit = l1;
            return true;
        }
        if ((GetIntersection(l1.X - b1.X, l2.X - b1.X, l1, l2, out hit) && InBox(hit, b1, b2, 1))
          || (GetIntersection(l1.Y - b1.Y, l2.Y - b1.Y, l1, l2, out hit) && InBox(hit, b1, b2, 2))
          || (GetIntersection(l1.Z - b1.Z, l2.Z - b1.Z, l1, l2, out hit) && InBox(hit, b1, b2, 3))
          || (GetIntersection(l1.X - b2.X, l2.X - b2.X, l1, l2, out hit) && InBox(hit, b1, b2, 1))
          || (GetIntersection(l1.Y - b2.Y, l2.Y - b2.Y, l1, l2, out hit) && InBox(hit, b1, b2, 2))
          || (GetIntersection(l1.Z - b2.Z, l2.Z - b2.Z, l1, l2, out hit) && InBox(hit, b1, b2, 3)))
            return true;
        return false;
    }

    /// <summary>
    /// Warning: randomly returns incorrect hit position (back side of box).
    /// </summary>
    /// <param name="box"></param>
    /// <param name="line"></param>
    /// <param name="hit"></param>
    /// <returns></returns>
    public static bool CheckLineBox(Box3D box, Line3D line, out Vector3 hit)
    {
        return CheckLineBox1(box.MinEdge, box.MaxEdge, line.Start, line.End, out hit);
    }


    public static Vector3? CheckLineBoxExact(Line3D line, Box3D box)
    {
        float[] dir_ = [line.End[0] - line.Start[0], line.End[1] - line.Start[1], line.End[2] - line.Start[2]];
        if (!HitBoundingBox(box.MinEdge, box.MaxEdge, line.Start, dir_, out Vector3 hit))
        {
            return null;
        }
        return hit;
    }
}
