/// <summary>
/// Provides a factory method for generating UV-sphere <see cref="GeometryModel"/>,
/// parameterised by radius, height, segment and ring counts.
/// </summary>
public class Sphere
{

    /// <summary>
    /// Builds a UV-sphere <see cref="GeometryModel"/> with the given dimensions and tessellation.
    /// All vertices are initialised with full white vertex colour (255, 255, 255, 255).
    /// </summary>
    /// <param name="radius">Horizontal radius of the sphere.</param>
    /// <param name="height">Vertical scale of the sphere (use same as <paramref name="radius"/> for a true sphere).</param>
    /// <param name="segments">Number of subdivisions around the equator (longitude).</param>
    /// <param name="rings">Number of subdivisions from pole to pole (latitude).</param>
    /// <returns>A <see cref="GeometryModel"/> representing the UV-sphere.</returns>
    public static GeometryModel Create(float radius, float height, int segments, int rings)
    {
        int vertexCount = rings * segments;

        float[] xyz = new float[vertexCount * 3];
        float[] uv = new float[vertexCount * 2];
        byte[] rgba = new byte[vertexCount * 4];

        int i = 0;
        for (int y = 0; y < rings; y++)
        {
            float phi = (float)y / (rings - 1) * MathF.PI;

            for (int x = 0; x < segments; x++)
            {
                float theta = (float)x / (segments - 1) * 2f * MathF.PI;

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

        GeometryModel data = new()
        {
            VerticesCount = vertexCount,
            IndicesCount = vertexCount * 6,
            Xyz = xyz,
            Uv = uv,
            Rgba = rgba,
            Indices = CalculateElements(segments, rings)
        };
        return data;
    }

    /// <summary>
    /// Generates the triangle index buffer for a UV-sphere with the given tessellation.
    /// Each quad cell in the ring/segment grid is split into two triangles.
    /// </summary>
    /// <param name="segments">Number of subdivisions around the equator.</param>
    /// <param name="rings">Number of subdivisions from pole to pole.</param>
    /// <returns>An index array suitable for use with a triangle list draw call.</returns>
    private static int[] CalculateElements(int segments, int rings)
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
