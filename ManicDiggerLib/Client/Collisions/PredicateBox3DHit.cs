using OpenTK.Mathematics;
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
