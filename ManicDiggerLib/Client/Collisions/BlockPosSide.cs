using OpenTK.Mathematics;
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
