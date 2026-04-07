using OpenTK.Mathematics;
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
