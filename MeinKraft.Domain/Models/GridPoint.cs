using MemoryPack;

namespace MeinKraft;

/// <summary>
/// A serialisable 2D integer point used as a dictionary key in inventory grid storage.
/// Implements value equality so it works correctly as a <see cref="Dictionary{TKey,TValue}"/> key.
/// </summary>
[MemoryPackable]
public partial class GridPoint : IEquatable<GridPoint>
{
    /// <summary>Horizontal grid coordinate.</summary>
    public int X { get; set; }

    /// <summary>Vertical grid coordinate.</summary>
    public int Y { get; set; }

    /// <summary>Initialises a <see cref="GridPoint"/> at the origin (0, 0).</summary>
    public GridPoint() { }

    /// <summary>Initialises a <see cref="GridPoint"/> at the given coordinates.</summary>
    [MemoryPackConstructor]
    public GridPoint(int x, int y)
    {
        X = x;
        Y = y;
    }

    /// <inheritdoc/>
    public bool Equals(GridPoint? other)
        => other is not null && X == other.X && Y == other.Y;

    /// <inheritdoc/>
    public override bool Equals(object? obj)
        => obj is GridPoint other && Equals(other);

    /// <summary>
    /// Uses <see cref="HashCode.Combine"/> for a well-distributed hash —
    /// the old <c>X ^ Y</c> XOR produced many collisions for common grid patterns.
    /// </summary>
    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(GridPoint? a, GridPoint? b)
        => a is null ? b is null : a.Equals(b);

    public static bool operator !=(GridPoint? a, GridPoint? b) => !(a == b);
}