using OpenTK.Mathematics;

/// <summary>
/// Represents a line segment in 3D space defined by a start and end point.
/// </summary>
public class Line3D
{
    /// <summary>The start point of the line segment.</summary>
    internal Vector3 Start;

    /// <summary>The end point of the line segment.</summary>
    internal Vector3 End;

    /// <summary>
    /// The unnormalized direction vector from <see cref="Start"/> to <see cref="End"/>.
    /// The magnitude equals the length of the line segment.
    /// </summary>
    internal Vector3 Direction => End - Start;
}

/// <summary>
/// Base class for a predicate that tests whether a 3D axis-aligned box
/// satisfies some condition. Used for spatial queries such as raycasting
/// or frustum culling.
/// </summary>
public abstract class PredicateBox3D
{
    /// <summary>
    /// Tests whether the given <paramref name="box"/> satisfies this predicate.
    /// </summary>
    /// <param name="box">The axis-aligned box to test.</param>
    /// <returns><c>true</c> if the box satisfies the predicate; otherwise <c>false</c>.</returns>
    public abstract bool Hit(Box3 box);
}

/// <summary>
/// Identifies one of the 6 faces of a tile (block) in world space.
/// </summary>
public enum TileSide
{
    /// <summary>The face on the positive Y axis.</summary>
    Top = 0,

    /// <summary>The face on the negative Y axis.</summary>
    Bottom = 1,

    /// <summary>The face on the positive Z axis.</summary>
    Front = 2,

    /// <summary>The face on the negative Z axis.</summary>
    Back = 3,

    /// <summary>The face on the negative X axis.</summary>
    Left = 4,

    /// <summary>The face on the positive X axis.</summary>
    Right = 5,
}

/// <summary>
/// Represents the result of a block hit test, storing both the block's
/// grid position and the exact world-space collision point.
/// Used to determine which face of a block was hit and where.
/// </summary>
public class BlockPosSide
{
    /// <summary>
    /// Creates a new <see cref="BlockPosSide"/> at the given block grid coordinates.
    /// </summary>
    /// <param name="x">Block grid X coordinate.</param>
    /// <param name="y">Block grid Y coordinate.</param>
    /// <param name="z">Block grid Z coordinate.</param>
    /// <returns>A new <see cref="BlockPosSide"/> with <see cref="blockPos"/> set.</returns>
    public static BlockPosSide Create(int x, int y, int z)
    {
        return new BlockPosSide { blockPos = new Vector3(x, y, z) };
    }

    /// <summary>The integer grid position of the hit block in world space.</summary>
    internal Vector3 blockPos;

    /// <summary>
    /// The exact world-space position where the collision occurred,
    /// typically on the surface of the block face that was hit.
    /// </summary>
    internal Vector3 collisionPos;

    /// <summary>
    /// Returns the block position translated by one unit in the direction
    /// of the hit face, giving the position of the adjacent block on that side.
    /// Used to determine where a newly placed block should be positioned.
    /// </summary>
    public float[] Translated()
    {
        float[] translated = [blockPos[0], blockPos[1], blockPos[2]];

        if (collisionPos[0] == blockPos[0]) { translated[0] -= 1; }
        if (collisionPos[1] == blockPos[1]) { translated[1] -= 1; }
        if (collisionPos[2] == blockPos[2]) { translated[2] -= 1; }
        if (collisionPos[0] == blockPos[0] + 1) { translated[0] += 1; }
        if (collisionPos[1] == blockPos[1] + 1) { translated[1] += 1; }
        if (collisionPos[2] == blockPos[2] + 1) { translated[2] += 1; }

        return translated;
    }

    /// <summary>
    /// Returns the block's grid position in world space.
    /// </summary>
    public Vector3 Current() => blockPos;
}

/// <summary>
/// Performs octree-based spatial searches over a 3D block world,
/// supporting line intersection tests against non-empty blocks.
/// </summary>
public class BlockOctreeSearcher
{
    /// <summary>Platform utilities for float/int conversion.</summary>
    internal GamePlatform platform;

    /// <summary>
    /// The root bounding box of the octree search space.
    /// Must have equal power-of-two dimensions for the octree subdivision to work correctly.
    /// </summary>
    internal Box3 StartBox;

    /// <summary>The line currently being tested, set at the start of <see cref="LineIntersection"/>.</summary>
    private Line3D currentLine;

    /// <summary>The most recent intersection hit point, populated by <see cref="BoxHit"/>.</summary>
    private Vector3 currentHit;

    /// <summary>
    /// Reusable result buffer for <see cref="LineIntersection"/> to avoid
    /// per-call heap allocation.
    /// </summary>
    private readonly List<BlockPosSide> hits;

    public BlockOctreeSearcher()
    {
        hits = new List<BlockPosSide>();
        currentHit = Vector3.Zero;
    }

    /// <summary>
    /// Recursively searches the octree for all unit-sized leaf boxes
    /// that satisfy <paramref name="query"/>, starting from <see cref="StartBox"/>.
    /// Returns an empty list if <see cref="StartBox"/> has zero size.
    /// </summary>
    /// <param name="query">The predicate to test each box against.</param>
    /// <returns>All matching leaf boxes.</returns>
    private List<Box3> Search(PredicateBox3D query)
    {
        if (StartBox.Size.X == 0 && StartBox.Size.Y == 0 && StartBox.Size.Z == 0)
        {
            return [];
        }
        return SearchRecursive(query, StartBox);
    }

    /// <summary>
    /// Recursively subdivides <paramref name="box"/> into 8 children,
    /// collecting all unit-sized leaves that satisfy <paramref name="query"/>.
    /// </summary>
    private static List<Box3> SearchRecursive(PredicateBox3D query, Box3 box)
    {
        if (box.Size.X == 1)
        {
            return [box];
        }

        var result = new List<Box3>();
        foreach (Box3 child in GetChildren(box))
        {
            if (query.Hit(child))
            {
                result.AddRange(SearchRecursive(query, child));
            }
        }
        return result;
    }

    /// <summary>
    /// Returns the 8 equal child boxes produced by subdividing <paramref name="box"/> in half
    /// along each axis.
    /// </summary>
    private static Box3[] GetChildren(Box3 box)
    {
        float x = box.Min.X;
        float y = box.Min.Y;
        float z = box.Min.Z;
        float half = box.Size.X / 2;
        Vector3 s = new(half, half, half);

        return
        [
            new Box3(new Vector3(x,        y,        z       ), new Vector3(x,        y,        z       ) + s),
            new Box3(new Vector3(x + half, y,        z       ), new Vector3(x + half, y,        z       ) + s),
            new Box3(new Vector3(x,        y,        z + half), new Vector3(x,        y,        z + half) + s),
            new Box3(new Vector3(x + half, y,        z + half), new Vector3(x + half, y,        z + half) + s),
            new Box3(new Vector3(x,        y + half, z       ), new Vector3(x,        y + half, z       ) + s),
            new Box3(new Vector3(x + half, y + half, z       ), new Vector3(x + half, y + half, z       ) + s),
            new Box3(new Vector3(x,        y + half, z + half), new Vector3(x,        y + half, z + half) + s),
            new Box3(new Vector3(x + half, y + half, z + half), new Vector3(x + half, y + half, z + half) + s),
        ];
    }

    /// <summary>
    /// Tests whether <paramref name="box"/> intersects the current line,
    /// populating <see cref="currentHit"/> with the intersection point if so.
    /// Called by <see cref="PredicateBox3DHit"/> during the octree search.
    /// </summary>
    /// <param name="box">The box to test.</param>
    /// <returns><c>true</c> if the line intersects <paramref name="box"/>.</returns>
    public bool BoxHit(Box3 box)
    {
        return Intersection.CheckLineBox(box, currentLine, out currentHit);
    }

    /// <summary>
    /// Finds all non-empty blocks intersected by <paramref name="line"/>,
    /// returning their positions and exact collision points.
    /// </summary>
    /// <param name="isEmpty">Delegate that returns <c>true</c> if a block at (x, y, z) is empty.</param>
    /// <param name="getBlockHeight">Delegate that returns the height of a block at (x, y, z).</param>
    /// <param name="line">The line segment to test against the block world.</param>
    /// <param name="retCount">The number of hits written to the returned segment.</param>
    /// <returns>
    /// A segment of the internal hit buffer containing all intersected blocks.
    /// </returns>
    private int lCount;
    private readonly BlockPosSide[] l;
    public ArraySegment<BlockPosSide> LineIntersection(IsBlockEmptyDelegate isEmpty, GetBlockHeightDelegate getBlockHeight, Line3D line, out int retCount)
    {
        hits.Clear();
        currentLine = line;
        currentHit = Vector3.Zero;

        List<Box3> candidates = Search(PredicateBox3DHit.Create(this));

        for (int i = 0; i < candidates.Count; i++)
        {
            Box3 node = candidates[i];
            int bx = platform.FloatToInt(node.Min.X);
            int by = platform.FloatToInt(node.Min.Z); // note: Y/Z are swapped in world space
            int bz = platform.FloatToInt(node.Min.Y);

            if (isEmpty(bx, by, bz)) { continue; }

            Box3 adjustedBox = new(node.Min, new Vector3(
                node.Max.X,
                node.Min.Y + getBlockHeight(bx, by, bz),
                node.Max.Z
            ));

            if (Intersection.HitBoundingBox(adjustedBox.Min, adjustedBox.Max, line.Start, line.Direction, out Vector3 hit))
            {
                hits.Add(new BlockPosSide
                {
                    blockPos = new Vector3(bx, bz, by), // note: Y/Z are swapped in world space
                    collisionPos = hit
                });
            }
        }

        retCount = hits.Count;
        return new ArraySegment<BlockPosSide>([.. hits], 0, hits.Count);
    }
}


/// <summary>
/// A <see cref="PredicateBox3D"/> that tests boxes against the current line
/// in a <see cref="BlockOctreeSearcher"/> using <see cref="BlockOctreeSearcher.BoxHit"/>.
/// Created per search via <see cref="Create"/> to bind a searcher instance.
/// </summary>
public class PredicateBox3DHit : PredicateBox3D
{
    /// <summary>The searcher whose current line is tested against each box.</summary>
    private BlockOctreeSearcher s;

    /// <summary>
    /// Creates a new <see cref="PredicateBox3DHit"/> bound to the given <paramref name="searcher"/>.
    /// </summary>
    /// <param name="searcher">The octree searcher providing the line to test against.</param>
    public static PredicateBox3DHit Create(BlockOctreeSearcher searcher)
    {
        return new PredicateBox3DHit { s = searcher };
    }

    /// <inheritdoc/>
    public override bool Hit(Box3 box) => s.BoxHit(box);
}

/// <summary>
/// Tests whether the block at the given world-space grid coordinates is empty (air).
/// </summary>
/// <param name="x">Block grid X coordinate.</param>
/// <param name="y">Block grid Y coordinate.</param>
/// <param name="z">Block grid Z coordinate.</param>
/// <returns><c>true</c> if the block is empty; <c>false</c> if it is solid.</returns>
public delegate bool IsBlockEmptyDelegate(int x, int y, int z);

/// <summary>
/// Returns the height of the block at the given world-space grid coordinates,
/// in model units. Used to support non-full-height blocks such as slabs or stairs.
/// </summary>
/// <param name="x">Block grid X coordinate.</param>
/// <param name="y">Block grid Y coordinate.</param>
/// <param name="z">Block grid Z coordinate.</param>
/// <returns>The block height in model units.</returns>
public delegate float GetBlockHeightDelegate(int x, int y, int z);

/// <summary>
/// Provides static methods for ray and line intersection tests against
/// axis-aligned bounding boxes (AABB).
/// </summary>
public class Intersection
{
    // Quadrant classification constants used by HitBoundingBox.
    private const int Left = 1;
    private const int Middle = 2;
    private const int Right = 0;

    /// <summary>
    /// Tests whether a ray intersects an axis-aligned bounding box,
    /// returning the intersection point if so.
    /// Fast Ray-Box Intersection by Andrew Woo,
    /// from "Graphics Gems", Academic Press, 1990.
    /// Original source: http://tog.acm.org/resources/GraphicsGems/gems/RayBox.c
    /// </summary>
    /// <param name="minB">Minimum corner of the bounding box.</param>
    /// <param name="maxB">Maximum corner of the bounding box.</param>
    /// <param name="origin">Ray origin.</param>
    /// <param name="dir">Ray direction (does not need to be normalized).</param>
    /// <param name="coord">
    /// The intersection point if the ray hits the box;
    /// <see cref="Vector3.Zero"/> otherwise.
    /// </param>
    /// <returns><c>true</c> if the ray intersects the box.</returns>
    public static bool HitBoundingBox(Vector3 minB, Vector3 maxB, Vector3 origin, Vector3 dir, out Vector3 coord)
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
                quadrant[i] = Left;
                candidatePlane[i] = minB[i];
                inside = false;
            }
            else if (origin[i] > maxB[i])
            {
                quadrant[i] = Right;
                candidatePlane[i] = maxB[i];
                inside = false;
            }
            else
            {
                quadrant[i] = Middle;
            }

        // Ray origin inside bounding box
        if (inside)
        {
            coord = origin;
            return true;
        }

        // Calculate T distances to candidate planes
        for (i = 0; i < 3; i++)
            if (quadrant[i] != Middle && dir[i] != 0)
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

    /// <summary>
    /// Returns the point where the line segment from <paramref name="p1"/> to
    /// <paramref name="p2"/> crosses the plane defined by the signed distances
    /// <paramref name="fDst1"/> and <paramref name="fDst2"/>.
    /// Returns <c>false</c> if the segment does not cross the plane.
    /// </summary>
    private static bool GetIntersection(float fDst1, float fDst2, Vector3 p1, Vector3 p2, out Vector3 hit)
    {
        hit = Vector3.Zero;
        if ((fDst1 * fDst2) >= 0) return false;
        if (fDst1 == fDst2) return false;
        hit = p1 + (p2 - p1) * (-fDst1 / (fDst2 - fDst1));
        return true;
    }

    /// <summary>
    /// Tests whether <paramref name="hit"/> lies within the face of the box
    /// perpendicular to the given <paramref name="axis"/>.
    /// </summary>
    /// <param name="hit">The candidate intersection point.</param>
    /// <param name="b1">Minimum corner of the box.</param>
    /// <param name="b2">Maximum corner of the box.</param>
    /// <param name="axis">1 = X face, 2 = Y face, 3 = Z face.</param>
    private static bool InBox(Vector3 hit, Vector3 b1, Vector3 b2, int axis)
    {
        if (axis == 1 && hit.Z > b1.Z && hit.Z < b2.Z && hit.Y > b1.Y && hit.Y < b2.Y) return true;
        if (axis == 2 && hit.Z > b1.Z && hit.Z < b2.Z && hit.X > b1.X && hit.X < b2.X) return true;
        if (axis == 3 && hit.X > b1.X && hit.X < b2.X && hit.Y > b1.Y && hit.Y < b2.Y) return true;
        return false;
    }

    /// <summary>
    /// Tests whether the line segment from <paramref name="l1"/> to <paramref name="l2"/>
    /// intersects the axis-aligned box defined by <paramref name="b1"/> and <paramref name="b2"/>,
    /// returning the intersection point in <paramref name="hit"/>.
    /// </summary>
    /// <param name="b1">Minimum corner of the box.</param>
    /// <param name="b2">Maximum corner of the box.</param>
    /// <param name="l1">Start point of the line segment.</param>
    /// <param name="l2">End point of the line segment.</param>
    /// <param name="hit">The intersection point, or <see cref="Vector3.Zero"/> if no hit.</param>
    /// <returns><c>true</c> if the line segment intersects the box.</returns>
    public static bool CheckLineBox(Vector3 b1, Vector3 b2, Vector3 l1, Vector3 l2, out Vector3 hit)
    {
        hit = Vector3.Zero;

        // Broad-phase rejection: if both endpoints are outside the same face, no intersection.
        if (l2.X < b1.X && l1.X < b1.X) { return false; }
        if (l2.X > b2.X && l1.X > b2.X) { return false; }
        if (l2.Y < b1.Y && l1.Y < b1.Y) { return false; }
        if (l2.Y > b2.Y && l1.Y > b2.Y) { return false; }
        if (l2.Z < b1.Z && l1.Z < b1.Z) { return false; }
        if (l2.Z > b2.Z && l1.Z > b2.Z) { return false; }

        // Start point is inside the box.
        if (l1.X > b1.X && l1.X < b2.X &&
            l1.Y > b1.Y && l1.Y < b2.Y &&
            l1.Z > b1.Z && l1.Z < b2.Z)
        {
            hit = l1;
            return true;
        }

        // Test intersection against each of the 6 faces.
        return (GetIntersection(l1.X - b1.X, l2.X - b1.X, l1, l2, out hit) && InBox(hit, b1, b2, 1))
            || (GetIntersection(l1.Y - b1.Y, l2.Y - b1.Y, l1, l2, out hit) && InBox(hit, b1, b2, 2))
            || (GetIntersection(l1.Z - b1.Z, l2.Z - b1.Z, l1, l2, out hit) && InBox(hit, b1, b2, 3))
            || (GetIntersection(l1.X - b2.X, l2.X - b2.X, l1, l2, out hit) && InBox(hit, b1, b2, 1))
            || (GetIntersection(l1.Y - b2.Y, l2.Y - b2.Y, l1, l2, out hit) && InBox(hit, b1, b2, 2))
            || (GetIntersection(l1.Z - b2.Z, l2.Z - b2.Z, l1, l2, out hit) && InBox(hit, b1, b2, 3));
    }

    /// <summary>
    /// Tests whether <paramref name="line"/> intersects <paramref name="box"/>,
    /// returning the intersection point in <paramref name="hit"/>.
    /// </summary>
    /// <remarks>
    /// Warning: may return an incorrect hit position on the far (back) side of the box
    /// in some edge cases. Use <see cref="CheckLineBoxExact"/> for precise results.
    /// </remarks>
    /// <param name="box">The axis-aligned box to test against.</param>
    /// <param name="line">The line segment to test.</param>
    /// <param name="hit">The intersection point if hit; otherwise <see cref="Vector3.Zero"/>.</param>
    /// <returns><c>true</c> if the line intersects the box.</returns>
    public static bool CheckLineBox(Box3 box, Line3D line, out Vector3 hit)
    {
        return CheckLineBox(box.Min, box.Max, line.Start, line.End, out hit);
    }

    /// <summary>
    /// Tests whether <paramref name="line"/> intersects <paramref name="box"/>
    /// using the more accurate <see cref="HitBoundingBox"/> method.
    /// Unlike <see cref="CheckLineBox"/>, this always returns the near (front) hit point.
    /// </summary>
    /// <param name="line">The line segment to test.</param>
    /// <param name="box">The axis-aligned box to test against.</param>
    /// <returns>The intersection point, or <c>null</c> if there is no intersection.</returns>
    public static Vector3? CheckLineBoxExact(Line3D line, Box3 box)
    {
        if (!HitBoundingBox(box.Min, box.Max, line.Start, line.Direction, out Vector3 hit))
        {
            return null;
        }
        return hit;
    }
}