
/// <summary>
/// Provides factory methods for building <see cref="GeometryModel"/> instances
/// representing screen-aligned quads (two triangles forming a rectangle).
/// </summary>
public class Quad
{
    /// <summary>Number of vertices in a quad.</summary>
    private const int VertexCount = 4;

    /// <summary>Number of position components per vertex (X, Y, Z).</summary>
    private const int PositionComponents = 3;

    /// <summary>Number of UV components per vertex (U, V).</summary>
    private const int UvComponents = 2;

    /// <summary>Number of colour components per vertex (R, G, B, A).</summary>
    private const int ColorComponents = 4;

    /// <summary>Number of indices forming the two triangles of a quad.</summary>
    private const int IndexCount = 6;

    /// <summary>
    /// Flat XYZ positions for a unit quad centred at the origin on the Z=0 plane.
    /// Laid out as 4 vertices × 3 components.
    /// </summary>
    private static readonly float[] QuadVertices =
    {
        // X      Y     Z
        -1f,  -1f,   0f,  // Bottom-left
         1f,  -1f,   0f,  // Bottom-right
         1f,   1f,   0f,  // Top-right
        -1f,   1f,   0f,  // Top-left
    };

    /// <summary>
    /// Flat UV texture coordinates for a quad covering the full [0,1] range.
    /// Laid out as 4 vertices × 2 components.
    /// </summary>
    private static readonly float[] QuadTextureCoords =
    {
        // U   V
        0f,  0f,  // Bottom-left
        1f,  0f,  // Bottom-right
        1f,  1f,  // Top-right
        0f,  1f,  // Top-left
    };

    /// <summary>
    /// Triangle indices for a quad, forming two clockwise triangles.
    /// </summary>
    private static readonly int[] QuadVertexIndices =
    {
        0, 1, 2,
        0, 2, 3,
    };

    /// <summary>
    /// Builds a unit quad <see cref="GeometryModel"/> centred at the origin
    /// with full [0,1] UV coverage and no vertex colours.
    /// </summary>
    /// <returns>A <see cref="GeometryModel"/> representing the unit quad.</returns>
    public static GeometryModel Create()
    {
        GeometryModel m = new();

        float[] xyz = new float[PositionComponents * VertexCount];
        Array.Copy(QuadVertices, xyz, xyz.Length);
        m.Xyz = xyz;

        float[] uv = new float[UvComponents * VertexCount];
        Array.Copy(QuadTextureCoords, uv, uv.Length);
        m.Uv = uv;
        // white, fully opaque for all vertices
        byte[] rgba = new byte[4 * VertexCount];
        for (int i = 0; i < VertexCount; i++)
        {
            rgba[i * 4 + 0] = 255; // R
            rgba[i * 4 + 1] = 255; // G
            rgba[i * 4 + 2] = 255; // B
            rgba[i * 4 + 3] = 255; // A
        }
        m.Rgba = rgba;

        m.VerticesCount = VertexCount;
        m.Indices = QuadVertexIndices;
        m.IndicesCount = IndexCount;

        return m;
    }


    /// <summary>
    /// Builds a quad <see cref="GeometryModel"/> with explicit screen-space destination
    /// rectangle, source UV rectangle, and a flat vertex colour applied to all 4 vertices.
    /// </summary>
    /// <param name="sx">Source UV left edge.</param>
    /// <param name="sy">Source UV bottom edge.</param>
    /// <param name="sw">Source UV width.</param>
    /// <param name="sh">Source UV height.</param>
    /// <param name="dx">Destination rectangle left edge (screen space).</param>
    /// <param name="dy">Destination rectangle bottom edge (screen space).</param>
    /// <param name="dw">Destination rectangle width.</param>
    /// <param name="dh">Destination rectangle height.</param>
    /// <param name="r">Vertex colour red component.</param>
    /// <param name="g">Vertex colour green component.</param>
    /// <param name="b">Vertex colour blue component.</param>
    /// <param name="a">Vertex colour alpha component.</param>
    /// <returns>A <see cref="GeometryModel"/> representing the positioned, coloured quad.</returns>
    public static GeometryModel CreateColored(
        float sx, float sy, float sw, float sh,
        float dx, float dy, float dw, float dh,
        byte r, byte g, byte b, byte a)
    {
        GeometryModel m = new();

        float[] xyz =
        [
            dx,      dy,      0f,
            dx + dw, dy,      0f,
            dx + dw, dy + dh, 0f,
            dx,      dy + dh, 0f,
        ];
        m.Xyz = xyz;

        float[] uv =
        [
            sx,      sy,
            sx + sw, sy,
            sx + sw, sy + sh,
            sx,      sy + sh,
        ];
        m.Uv =uv;

        // Apply the same flat colour to all 4 vertices.
        byte[] rgba = new byte[ColorComponents * VertexCount];
        for (int i = 0; i < VertexCount; i++)
        {
            rgba[i * ColorComponents + 0] = r;
            rgba[i * ColorComponents + 1] = g;
            rgba[i * ColorComponents + 2] = b;
            rgba[i * ColorComponents + 3] = a;
        }
        m.Rgba = rgba;

        m.VerticesCount = VertexCount;
        m.Indices = QuadVertexIndices;
        m.IndicesCount = IndexCount;
        return m;
    }
}
