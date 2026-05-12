using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

/// <summary>
/// Compares the original Sphere.Create (duplicate seam, pole rings, redundant trig)
/// against the rewritten version (pole vertices, seam wrapping, pre-computed trig).
///
/// Parameterised by segments/rings to show how the gap widens with tessellation.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class SphereBenchmarks
{
    private class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(10));
        }
    }

    /// <summary>
    /// Low = typical debug/placeholder sphere.
    /// High = typical in-game quality sphere.
    /// </summary>
    [Params(16, 64)]
    public int Segments { get; set; }

    [Params(8, 32)]
    public int Rings { get; set; }

    [Benchmark(Baseline = true, Description = "Original — duplicate seam, pole rings, inline trig")]
    public GeometryModel Original() => SphereOriginal.Create(1f, 1f, Segments, Rings);

    [Benchmark(Description = "Rewritten — pole vertices, seam wrap, pre-computed trig")]
    public GeometryModel Rewritten() => SphereRewritten.Create(1f, 1f, Segments, Rings);
}

// ── Original implementation ───────────────────────────────────────────────────

public static class SphereOriginal
{
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

                xyz[(i * 3) + 0] = vx;
                xyz[(i * 3) + 1] = vy;
                xyz[(i * 3) + 2] = vz;

                uv[(i * 2) + 0] = (float)x / (segments - 1);
                uv[(i * 2) + 1] = (float)y / (rings - 1);

                rgba[(i * 4) + 0] = 255;
                rgba[(i * 4) + 1] = 255;
                rgba[(i * 4) + 2] = 255;
                rgba[(i * 4) + 3] = 255;

                i++;
            }
        }

        return new GeometryModel
        {
            VerticesCount = vertexCount,
            IndicesCount = vertexCount * 6,
            Xyz = xyz,
            Uv = uv,
            Rgba = rgba,
            Indices = CalculateElements(segments, rings),
        };
    }

    private static int[] CalculateElements(int segments, int rings)
    {
        int[] indices = new int[segments * rings * 6];
        int i = 0;

        for (int y = 0; y < rings - 1; y++)
        {
            for (int x = 0; x < segments - 1; x++)
            {
                int bottomLeft = ((y + 0) * segments) + x;
                int topLeft = ((y + 1) * segments) + x;
                int topRight = ((y + 1) * segments) + x + 1;
                int bottomRight = ((y + 0) * segments) + x + 1;

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

// ── Rewritten implementation ──────────────────────────────────────────────────

public static class SphereRewritten
{
    public static GeometryModel Create(float radius, float height, int segments, int rings)
    {
        int bodyVertices = rings * segments;
        int vertexCount = bodyVertices + 2;
        int northPoleIdx = 0;
        int southPoleIdx = vertexCount - 1;

        int indexCount = (segments * 3) + ((rings - 1) * segments * 6) + (segments * 3);

        float[] xyz = new float[vertexCount * 3];
        float[] uv = new float[vertexCount * 2];
        byte[] rgba = new byte[vertexCount * 4];
        int[] indices = new int[indexCount];

        // Pre-computed trig — one sin/cos per segment and per ring
        float[] sinTheta = new float[segments];
        float[] cosTheta = new float[segments];
        for (int x = 0; x < segments; x++)
        {
            float theta = x / (float)segments * 2f * MathF.PI;
            sinTheta[x] = MathF.Sin(theta);
            cosTheta[x] = MathF.Cos(theta);
        }

        float[] sinPhi = new float[rings];
        float[] cosPhi = new float[rings];
        for (int y = 0; y < rings; y++)
        {
            float phi = (y + 1) / (float)(rings + 1) * MathF.PI;
            sinPhi[y] = MathF.Sin(phi);
            cosPhi[y] = MathF.Cos(phi);
        }

        WriteVertex(xyz, uv, rgba, northPoleIdx, 0f, height, 0f, 0.5f, 0f);

        for (int y = 0; y < rings; y++)
        {
            float sp = sinPhi[y];
            float cp = cosPhi[y];
            float vv = (y + 1) / (float)(rings + 1);

            for (int x = 0; x < segments; x++)
            {
                int vi = 1 + y * segments + x;
                WriteVertex(xyz, uv, rgba, vi,
                    radius * sp * cosTheta[x],
                    height * cp,
                    radius * sp * sinTheta[x],
                    x / (float)segments,
                    vv);
            }
        }

        WriteVertex(xyz, uv, rgba, southPoleIdx, 0f, -height, 0f, 0.5f, 1f);

        int ii = 0;

        for (int x = 0; x < segments; x++)
        {
            indices[ii++] = northPoleIdx;
            indices[ii++] = 1 + x;
            indices[ii++] = 1 + (x + 1) % segments;
        }

        for (int y = 0; y < rings - 1; y++)
        {
            int ringBase = 1 + y * segments;
            int nextRingBase = 1 + (y + 1) * segments;
            for (int x = 0; x < segments; x++)
            {
                int nextX = (x + 1) % segments;
                int bl = ringBase + x;
                int br = ringBase + nextX;
                int tl = nextRingBase + x;
                int tr = nextRingBase + nextX;
                indices[ii++] = bl; indices[ii++] = tl; indices[ii++] = tr;
                indices[ii++] = tr; indices[ii++] = br; indices[ii++] = bl;
            }
        }

        int lastRingBase = 1 + (rings - 1) * segments;
        for (int x = 0; x < segments; x++)
        {
            indices[ii++] = southPoleIdx;
            indices[ii++] = lastRingBase + (x + 1) % segments;
            indices[ii++] = lastRingBase + x;
        }

        return new GeometryModel
        {
            VerticesCount = vertexCount,
            IndicesCount = indexCount,
            Xyz = xyz,
            Uv = uv,
            Rgba = rgba,
            Indices = indices,
        };
    }

    private static void WriteVertex(
        float[] xyz, float[] uv, byte[] rgba,
        int vi, float x, float y, float z, float u, float v)
    {
        xyz[(vi * 3) + 0] = x;
        xyz[(vi * 3) + 1] = y;
        xyz[(vi * 3) + 2] = z;
        uv[(vi * 2) + 0] = u;
        uv[(vi * 2) + 1] = v;
        rgba[(vi * 4) + 0] = 255;
        rgba[(vi * 4) + 1] = 255;
        rgba[(vi * 4) + 2] = 255;
        rgba[(vi * 4) + 3] = 255;
    }
}