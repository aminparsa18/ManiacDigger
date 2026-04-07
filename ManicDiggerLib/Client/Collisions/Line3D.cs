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
