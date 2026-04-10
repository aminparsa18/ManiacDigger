using OpenTK.Mathematics;

/// <summary>
/// Provides a factory method for generating a wireframe unit cube <see cref="GeometryModel"/>,
/// rendered as line loops around each of the 6 faces.
/// </summary>
public class WireframeCube
{
    /// <summary>Number of faces on a cube.</summary>
    private const int FaceCount = 6;

    /// <summary>Number of vertices per face (one quad = 4 corners).</summary>
    private const int VerticesPerFace = 4;

    /// <summary>Number of indices per face (4 edges × 2 endpoints = 8).</summary>
    private const int IndicesPerFace = 8;

    /// <summary>Full white, fully opaque vertex colour.</summary>
    private static readonly int White = ColorUtils.ColorFromArgb(255, 255, 255, 255);

    /// <summary>
    /// Builds a wireframe unit cube <see cref="GeometryModel"/> centred at the origin,
    /// with extents from -1 to +1 on each axis.
    /// Rendered using <see cref="DrawModeEnum.Lines"/>.
    /// </summary>
    /// <returns>A <see cref="GeometryModel"/> representing the wireframe cube.</returns>
    public static GeometryModel Create()
    {
        GeometryModel m = new()
        {
            Mode = (int)DrawMode.Lines,
            Xyz = new float[VerticesPerFace * FaceCount * 3],
            Uv = new float[VerticesPerFace * FaceCount * 2],
            Rgba = new byte[VerticesPerFace * FaceCount * 4],
            Indices = new int[IndicesPerFace * FaceCount]
        };
        DrawLineLoop(m, new Vector3(-1, -1, -1), new Vector3(-1, 1, -1), new Vector3(1, 1, -1), new Vector3(1, -1, -1)); // Back face
        DrawLineLoop(m, new Vector3(-1, -1, -1), new Vector3(1, -1, -1), new Vector3(1, -1, 1), new Vector3(-1, -1, 1)); // Bottom face
        DrawLineLoop(m, new Vector3(-1, -1, -1), new Vector3(-1, -1, 1), new Vector3(-1, 1, 1), new Vector3(-1, 1, -1)); // Left face
        DrawLineLoop(m, new Vector3(-1, -1, 1), new Vector3(1, -1, 1), new Vector3(1, 1, 1), new Vector3(-1, 1, 1)); // Front face
        DrawLineLoop(m, new Vector3(-1, 1, -1), new Vector3(-1, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, -1)); // Top face
        DrawLineLoop(m, new Vector3(1, -1, -1), new Vector3(1, 1, -1), new Vector3(1, 1, 1), new Vector3(1, -1, 1)); // Right face

        return m;
    }

    /// <summary>
    /// Adds 4 vertices and 8 line indices forming a closed quad loop (4 edges)
    /// for one face of the wireframe cube.
    /// </summary>
    /// <param name="m">The model data being built.</param>
    /// <param name="p0">First corner of the face.</param>
    /// <param name="p1">Second corner of the face.</param>
    /// <param name="p2">Third corner of the face.</param>
    /// <param name="p3">Fourth corner of the face.</param>
    private static void DrawLineLoop(GeometryModel m, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        int start = m.VerticesCount;

        AddVertex(m, p0.X, p0.Y, p0.Z);
        AddVertex(m, p1.X, p1.Y, p1.Z);
        AddVertex(m, p2.X, p2.Y, p2.Z);
        AddVertex(m, p3.X, p3.Y, p3.Z);

        // Each edge is two indices — connect corners in a loop: 0→1→2→3→0.
        m.Indices[m.IndicesCount++] = start + 0;
        m.Indices[m.IndicesCount++] = start + 1;
        m.Indices[m.IndicesCount++] = start + 1;
        m.Indices[m.IndicesCount++] = start + 2;
        m.Indices[m.IndicesCount++] = start + 2;
        m.Indices[m.IndicesCount++] = start + 3;
        m.Indices[m.IndicesCount++] = start + 3;
        m.Indices[m.IndicesCount++] = start + 0;
    }

    /// <summary>
    /// Appends a single vertex with the given position and full white colour
    /// to the model's XYZ, UV, and RGBA buffers.
    /// </summary>
    /// <param name="m">The model data being built.</param>
    /// <param name="x">Vertex X position.</param>
    /// <param name="y">Vertex Y position.</param>
    /// <param name="z">Vertex Z position.</param>
    private static void AddVertex(GeometryModel m, float x, float y, float z)
    {
        int xyzOffset = m.XyzCount;
        int uvOffset = m.UvCount;
        int rgbaOffset = m.RgbaCount;

        m.Xyz[xyzOffset] = x;
        m.Xyz[xyzOffset + 1] = y;
        m.Xyz[xyzOffset + 2] = z;
        // UV is always (0,0) for wireframe — no texture sampling needed.
        m.Uv[uvOffset] = 0f;
        m.Uv[uvOffset + 1] = 0f;

        m.Rgba[rgbaOffset] = (byte)ColorUtils.ColorR(White);
        m.Rgba[rgbaOffset + 1] = (byte)ColorUtils.ColorG(White);
        m.Rgba[rgbaOffset + 2] = (byte)ColorUtils.ColorB(White);
        m.Rgba[rgbaOffset + 3] = (byte)ColorUtils.ColorA(White);

        m.VerticesCount++;
    }
}
