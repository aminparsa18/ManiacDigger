using OpenTK.Mathematics;

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