
/// <summary>
/// Holds the GPU-side rendering state for a single chunk.
/// A chunk is marked <see cref="Dirty"/> on creation and whenever its visual state needs rebuilding.
/// </summary>
public class RenderedChunk
{
    internal bool Dirty;
    internal int[] Ids;
    internal int IdsCount;
    internal byte[] Light;

    /// <summary>
    /// True when <see cref="Light"/> was rented from <see cref="System.Buffers.ArrayPool{T}.Shared"/>
    /// by <c>ModDrawTerrain.CalculateShadows</c> and must be returned to the pool
    /// rather than simply nulled when this chunk is unloaded.
    /// </summary>
    internal bool LightRented;
}