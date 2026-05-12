using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using OpenTK.Mathematics;

/// <summary>
/// Compares the original WireframeCube.Create (6 faces × 4 vertices, duplicate edges)
/// against the rewritten version (8 unique vertices, 12 unique edges).
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class WireframeCubeBenchmarks
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

    [Benchmark(Baseline = true, Description = "Original — 24 vertices, 48 indices, duplicate edges")]
    public GeometryModel Original() => WireframeCubeOriginal.Create();

    [Benchmark(Description = "Rewritten — 8 vertices, 24 indices, unique edges")]
    public GeometryModel Rewritten() => WireframeCubeRewritten.Create();
}

// ── Original implementation ───────────────────────────────────────────────────

public static class WireframeCubeOriginal
{
    private const int FaceCount = 6;
    private const int VerticesPerFace = 4;
    private const int IndicesPerFace = 8;

    public static GeometryModel Create()
    {
        GeometryModel m = new()
        {
            Mode = (int)DrawMode.Lines,
            Xyz = new float[VerticesPerFace * FaceCount * 3],
            Uv = new float[VerticesPerFace * FaceCount * 2],
            Rgba = new byte[VerticesPerFace * FaceCount * 4],
            Indices = new int[IndicesPerFace * FaceCount],
        };

        DrawLineLoop(m, new(-1, -1, -1), new(-1, 1, -1), new(1, 1, -1), new(1, -1, -1)); // Back
        DrawLineLoop(m, new(-1, -1, -1), new(1, -1, -1), new(1, -1, 1), new(-1, -1, 1)); // Bottom
        DrawLineLoop(m, new(-1, -1, -1), new(-1, -1, 1), new(-1, 1, 1), new(-1, 1, -1)); // Left
        DrawLineLoop(m, new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1)); // Front
        DrawLineLoop(m, new(-1, 1, -1), new(-1, 1, 1), new(1, 1, 1), new(1, 1, -1)); // Top
        DrawLineLoop(m, new(1, -1, -1), new(1, 1, -1), new(1, 1, 1), new(1, -1, 1)); // Right

        return m;
    }

    private static void DrawLineLoop(GeometryModel m, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
    {
        int start = m.VerticesCount;

        GeometryModel.AddVertex(m, p0.X, p0.Y, p0.Z);
        GeometryModel.AddVertex(m, p1.X, p1.Y, p1.Z);
        GeometryModel.AddVertex(m, p2.X, p2.Y, p2.Z);
        GeometryModel.AddVertex(m, p3.X, p3.Y, p3.Z);

        m.Indices[m.IndicesCount++] = start + 0;
        m.Indices[m.IndicesCount++] = start + 1;
        m.Indices[m.IndicesCount++] = start + 1;
        m.Indices[m.IndicesCount++] = start + 2;
        m.Indices[m.IndicesCount++] = start + 2;
        m.Indices[m.IndicesCount++] = start + 3;
        m.Indices[m.IndicesCount++] = start + 3;
        m.Indices[m.IndicesCount++] = start + 0;
    }
}

// ── Rewritten implementation ──────────────────────────────────────────────────

public static class WireframeCubeRewritten
{
    public static GeometryModel Create()
    {
        float[] xyz =
        [
            -1, -1, -1,  // 0
             1, -1, -1,  // 1
             1,  1, -1,  // 2
            -1,  1, -1,  // 3
            -1, -1,  1,  // 4
             1, -1,  1,  // 5
             1,  1,  1,  // 6
            -1,  1,  1,  // 7
        ];

        int[] indices =
        [
            0, 1,  1, 2,  2, 3,  3, 0,  // back face
            4, 5,  5, 6,  6, 7,  7, 4,  // front face
            0, 4,  1, 5,  2, 6,  3, 7,  // connecting edges
        ];

        byte[] rgba = new byte[8 * 4];
        Array.Fill(rgba, (byte)255);

        return new GeometryModel
        {
            Mode = (int)DrawMode.Lines,
            VerticesCount = 8,
            IndicesCount = 24,
            Xyz = xyz,
            Uv = new float[8 * 2],
            Rgba = rgba,
            Indices = indices,
        };
    }
}