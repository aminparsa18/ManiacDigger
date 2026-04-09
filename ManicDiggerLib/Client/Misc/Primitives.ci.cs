using OpenTK.Mathematics;

/// <summary>
/// Provides factory methods for building <see cref="ModelData"/> instances
/// representing screen-aligned quads (two triangles forming a rectangle).
/// </summary>
public class QuadModelData
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
    /// Builds a unit quad <see cref="ModelData"/> centred at the origin
    /// with full [0,1] UV coverage and no vertex colours.
    /// </summary>
    /// <returns>A <see cref="ModelData"/> representing the unit quad.</returns>
    public static ModelData GetQuadModelData()
    {
        ModelData m = new();

        float[] xyz = new float[PositionComponents * VertexCount];
        Array.Copy(QuadVertices, xyz, xyz.Length);
        m.setXyz(xyz);

        float[] uv = new float[UvComponents * VertexCount];
        Array.Copy(QuadTextureCoords, uv, uv.Length);
        m.setUv(uv);

        // white, fully opaque for all vertices
        byte[] rgba = new byte[4 * VertexCount];
        for (int i = 0; i < VertexCount; i++)
        {
            rgba[i * 4 + 0] = 255; // R
            rgba[i * 4 + 1] = 255; // G
            rgba[i * 4 + 2] = 255; // B
            rgba[i * 4 + 3] = 255; // A
        }
        m.setRgba(rgba);

        m.SetVerticesCount(VertexCount);
        m.setIndices(QuadVertexIndices);
        m.SetIndicesCount(IndexCount);

        return m;
    }


    /// <summary>
    /// Builds a quad <see cref="ModelData"/> with explicit screen-space destination
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
    /// <returns>A <see cref="ModelData"/> representing the positioned, coloured quad.</returns>
    public static ModelData GetColoredQuadModelData(
        float sx, float sy, float sw, float sh,
        float dx, float dy, float dw, float dh,
        byte r, byte g, byte b, byte a)
    {
        ModelData m = new();

        float[] xyz =
        [
            dx,      dy,      0f,
            dx + dw, dy,      0f,
            dx + dw, dy + dh, 0f,
            dx,      dy + dh, 0f,
        ];
        m.setXyz(xyz);

        float[] uv =
        [
            sx,      sy,
            sx + sw, sy,
            sx + sw, sy + sh,
            sx,      sy + sh,
        ];
        m.setUv(uv);

        // Apply the same flat colour to all 4 vertices.
        byte[] rgba = new byte[ColorComponents * VertexCount];
        for (int i = 0; i < VertexCount; i++)
        {
            rgba[i * ColorComponents + 0] = r;
            rgba[i * ColorComponents + 1] = g;
            rgba[i * ColorComponents + 2] = b;
            rgba[i * ColorComponents + 3] = a;
        }
        m.setRgba(rgba);

        m.SetVerticesCount(VertexCount);
        m.setIndices(QuadVertexIndices);
        m.SetIndicesCount(IndexCount);

        return m;
    }
}

/// <summary>
/// Provides a factory method for generating UV-sphere <see cref="ModelData"/>,
/// parameterised by radius, height, segment and ring counts.
/// </summary>
public class SphereModelData
{

    /// <summary>
    /// Builds a UV-sphere <see cref="ModelData"/> with the given dimensions and tessellation.
    /// All vertices are initialised with full white vertex colour (255, 255, 255, 255).
    /// </summary>
    /// <param name="radius">Horizontal radius of the sphere.</param>
    /// <param name="height">Vertical scale of the sphere (use same as <paramref name="radius"/> for a true sphere).</param>
    /// <param name="segments">Number of subdivisions around the equator (longitude).</param>
    /// <param name="rings">Number of subdivisions from pole to pole (latitude).</param>
    /// <returns>A <see cref="ModelData"/> representing the UV-sphere.</returns>
    public static ModelData GetSphereModelData(float radius, float height, int segments, int rings)
    {
        int vertexCount = rings * segments;

        float[] xyz = new float[vertexCount * 3];
        float[] uv = new float[vertexCount * 2];
        byte[] rgba = new byte[vertexCount * 4];

        int i = 0;
        for (int y = 0; y < rings; y++)
        {
            float phi = ((float)y / (rings - 1)) * MathF.PI;

            for (int x = 0; x < segments; x++)
            {
                float theta = ((float)x / (segments - 1)) * 2f * MathF.PI;

                float vx = radius * MathF.Sin(phi) * MathF.Cos(theta);
                float vy = height * MathF.Cos(phi);
                float vz = radius * MathF.Sin(phi) * MathF.Sin(theta);

                xyz[i * 3 + 0] = vx;
                xyz[i * 3 + 1] = vy;
                xyz[i * 3 + 2] = vz;

                uv[i * 2 + 0] = (float)x / (segments - 1);
                uv[i * 2 + 1] = (float)y / (rings - 1);

                // Default full-white vertex colour — tint at draw time if needed.
                rgba[i * 4 + 0] = 255;
                rgba[i * 4 + 1] = 255;
                rgba[i * 4 + 2] = 255;
                rgba[i * 4 + 3] = 255;

                i++;
            }
        }

        ModelData data = new();
        data.SetVerticesCount(vertexCount);
        data.SetIndicesCount(vertexCount * 6);
        data.setXyz(xyz);
        data.setUv(uv);
        data.setRgba(rgba);
        data.setIndices(CalculateElements(segments, rings));
        return data;
    }

    /// <summary>
    /// Generates the triangle index buffer for a UV-sphere with the given tessellation.
    /// Each quad cell in the ring/segment grid is split into two triangles.
    /// </summary>
    /// <param name="segments">Number of subdivisions around the equator.</param>
    /// <param name="rings">Number of subdivisions from pole to pole.</param>
    /// <returns>An index array suitable for use with a triangle list draw call.</returns>
    public static int[] CalculateElements(int segments, int rings)
    {
        int[] indices = new int[segments * rings * 6];
        int i = 0;

        for (int y = 0; y < rings - 1; y++)
        {
            for (int x = 0; x < segments - 1; x++)
            {
                int bottomLeft  = (y + 0) * segments + x;
                int topLeft     = (y + 1) * segments + x;
                int topRight    = (y + 1) * segments + x + 1;
                int bottomRight = (y + 0) * segments + x + 1;

                indices[i++] = bottomLeft;
                indices[i++] = topLeft;
                indices[i++] = topRight;

                indices[i++] = topRight;
                indices[i++] = bottomRight;
                indices[i++] = bottomLeft;
            }
        }

        return indices;
    }
}

/// <summary>
/// Provides a factory method for generating a wireframe unit cube <see cref="ModelData"/>,
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
    private static readonly int White = Game.ColorFromArgb(255, 255, 255, 255);

    /// <summary>
    /// Builds a wireframe unit cube <see cref="ModelData"/> centred at the origin,
    /// with extents from -1 to +1 on each axis.
    /// Rendered using <see cref="DrawModeEnum.Lines"/>.
    /// </summary>
    /// <returns>A <see cref="ModelData"/> representing the wireframe cube.</returns>
    public static ModelData GetWireframeCubeModelData()
    {
        ModelData m = new();
        m.setMode(DrawModeEnum.Lines);
        m.xyz = new float[VerticesPerFace * FaceCount * 3];
        m.uv = new float[VerticesPerFace * FaceCount * 2];
        m.rgba = new byte[VerticesPerFace * FaceCount * 4];
        m.indices = new int[IndicesPerFace * FaceCount];

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
    private static void DrawLineLoop(ModelData m, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        int start = m.GetVerticesCount();

        AddVertex(m, p0.X, p0.Y, p0.Z);
        AddVertex(m, p1.X, p1.Y, p1.Z);
        AddVertex(m, p2.X, p2.Y, p2.Z);
        AddVertex(m, p3.X, p3.Y, p3.Z);

        // Each edge is two indices — connect corners in a loop: 0→1→2→3→0.
        m.indices[m.indicesCount++] = start + 0;
        m.indices[m.indicesCount++] = start + 1;
        m.indices[m.indicesCount++] = start + 1;
        m.indices[m.indicesCount++] = start + 2;
        m.indices[m.indicesCount++] = start + 2;
        m.indices[m.indicesCount++] = start + 3;
        m.indices[m.indicesCount++] = start + 3;
        m.indices[m.indicesCount++] = start + 0;
    }

    /// <summary>
    /// Appends a single vertex with the given position and full white colour
    /// to the model's XYZ, UV, and RGBA buffers.
    /// </summary>
    /// <param name="m">The model data being built.</param>
    /// <param name="x">Vertex X position.</param>
    /// <param name="y">Vertex Y position.</param>
    /// <param name="z">Vertex Z position.</param>
    private static void AddVertex(ModelData m, float x, float y, float z)
    {
        int xyzOffset = m.GetXyzCount();
        int uvOffset = m.GetUvCount();
        int rgbaOffset = m.GetRgbaCount();

        m.xyz[xyzOffset] = x;
        m.xyz[xyzOffset + 1] = y;
        m.xyz[xyzOffset + 2] = z;

        // UV is always (0,0) for wireframe — no texture sampling needed.
        m.uv[uvOffset] = 0f;
        m.uv[uvOffset + 1] = 0f;

        m.rgba[rgbaOffset] = (byte)Game.ColorR(White);
        m.rgba[rgbaOffset + 1] = (byte)Game.ColorG(White);
        m.rgba[rgbaOffset + 2] = (byte)Game.ColorB(White);
        m.rgba[rgbaOffset + 3] = (byte)Game.ColorA(White);

        m.verticesCount++;
    }
}
