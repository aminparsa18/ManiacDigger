using OpenTK.Mathematics;
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
