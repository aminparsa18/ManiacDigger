
/// <summary>
/// Holds the GPU-side rendering state for a single chunk.
/// A chunk is marked <see cref="Dirty"/> on creation and whenever its visual state needs rebuilding.
/// </summary>
public class RenderedChunk
{
    /// <summary>OpenGL buffer object IDs allocated for this chunk's geometry.</summary>
    internal int[] Ids;

    /// <summary>Number of valid entries in <see cref="Ids"/>.</summary>
    internal int IdsCount;

    /// <summary>Whether this chunk's render buffers need to be rebuilt before the next draw.</summary>
    internal bool Dirty = true;

    /// <summary>Per-block light values used when building the chunk's vertex data.</summary>
    internal byte[] Light;
}
