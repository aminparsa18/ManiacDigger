using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using OpenTK.Mathematics;

/// <summary>
/// Compares HitBoundingBoxWoo (Graphics Gems 1990) against HitBoundingBoxSlab
/// across three ray scenarios that reflect real octree traversal conditions:
///
///   Hit        — ray that definitely intersects the box (most common in traversal)
///   Miss       — ray that definitely misses (early-out path)
///   Mixed      — randomised rays, ~50 % hit rate (realistic pick workload)
///
/// MemoryDiagnoser is included to make the heap allocation difference visible.
/// Run with: dotnet run -c Release
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
//[DisassemblyDiagnoser(printSource: true, maxDepth: 2)]
public class RayBoxBenchmarks
{
    // ── Benchmark config ──────────────────────────────────────────────────────

    private class BenchmarkConfig : ManualConfig
    {
        public BenchmarkConfig()
        {
            AddJob(Job.Default
                .WithWarmupCount(3)
                .WithIterationCount(10));
        }
    }

    // ── Test data ─────────────────────────────────────────────────────────────

    /// <summary>Number of ray-box tests per benchmark invocation.</summary>
    [Params(100, 10_000)]
    public int N { get; set; }

    // Bounding box — unit cube at origin (representative of a voxel chunk node)
    private static readonly Vector3 MinB = new(0f, 0f, 0f);
    private static readonly Vector3 MaxB = new(1f, 1f, 1f);

    // Fixed rays used by the deterministic Hit / Miss benchmarks
    private static readonly Vector3 HitOrigin = new(-1f, 0.5f, 0.5f);
    private static readonly Vector3 HitDir = new(1f, 0f, 0f);   // axis-aligned hit

    private static readonly Vector3 MissOrigin = new(-1f, 2f, 0.5f);
    private static readonly Vector3 MissDir = new(1f, 0f, 0f);    // passes above the box

    // Pre-generated mixed rays so allocation is not counted in the benchmark itself
    private Vector3[] _rayOrigins = null!;
    private Vector3[] _rayDirs = null!;

    [GlobalSetup]
    public void Setup()
    {
        var rng = new Random(42);
        // Allocate for the largest N to avoid re-setup between param runs
        const int maxN = 10_000;
        _rayOrigins = new Vector3[maxN];
        _rayDirs = new Vector3[maxN];

        for (int i = 0; i < maxN; i++)
        {
            // Origins scattered around the box; roughly half will produce hits
            _rayOrigins[i] = new Vector3(
                (float)rng.NextDouble() * 4f - 2f,
                (float)rng.NextDouble() * 4f - 2f,
                (float)rng.NextDouble() * 4f - 2f);

            // Directions aimed loosely toward box centre with some spread
            Vector3 toCenter = new Vector3(0.5f) - _rayOrigins[i];
            _rayDirs[i] = Vector3.Normalize(toCenter + new Vector3(
                (float)(rng.NextDouble() - 0.5) * 0.5f,
                (float)(rng.NextDouble() - 0.5) * 0.5f,
                (float)(rng.NextDouble() - 0.5) * 0.5f));
        }
    }

    // ── Hit benchmarks ────────────────────────────────────────────────────────

    [Benchmark(Description = "Woo original (heap) — guaranteed hit")]
    [BenchmarkCategory("Hit")]
    public bool WooOriginal_Hit()
    {
        bool result = false;
        for (int i = 0; i < N; i++)
            result = RayBox.HitBoundingBoxWooOriginal(MinB, MaxB, HitOrigin, HitDir, out _);
        return result;
    }

    [Benchmark(Description = "Woo stackalloc     — guaranteed hit")]
    [BenchmarkCategory("Hit")]
    public bool Woo_Hit()
    {
        bool result = false;
        for (int i = 0; i < N; i++)
            result = RayBox.HitBoundingBoxWoo(MinB, MaxB, HitOrigin, HitDir, out _);
        return result;
    }

    [Benchmark(Description = "Slab               — guaranteed hit")]
    [BenchmarkCategory("Hit")]
    public bool Slab_Hit()
    {
        bool result = false;
        for (int i = 0; i < N; i++)
            result = RayBox.HitBoundingBoxSlab(MinB, MaxB, HitOrigin, HitDir, out _);
        return result;
    }

    // ── Miss benchmarks ───────────────────────────────────────────────────────

    [Benchmark(Description = "Woo original (heap) — guaranteed miss")]
    [BenchmarkCategory("Miss")]
    public bool WooOriginal_Miss()
    {
        bool result = false;
        for (int i = 0; i < N; i++)
            result = RayBox.HitBoundingBoxWooOriginal(MinB, MaxB, MissOrigin, MissDir, out _);
        return result;
    }

    [Benchmark(Description = "Woo stackalloc     — guaranteed miss")]
    [BenchmarkCategory("Miss")]
    public bool Woo_Miss()
    {
        bool result = false;
        for (int i = 0; i < N; i++)
            result = RayBox.HitBoundingBoxWoo(MinB, MaxB, MissOrigin, MissDir, out _);
        return result;
    }

    [Benchmark(Description = "Slab               — guaranteed miss")]
    [BenchmarkCategory("Miss")]
    public bool Slab_Miss()
    {
        bool result = false;
        for (int i = 0; i < N; i++)
            result = RayBox.HitBoundingBoxSlab(MinB, MaxB, MissOrigin, MissDir, out _);
        return result;
    }

    // ── Mixed benchmarks (realistic pick workload) ────────────────────────────

    [Benchmark(Description = "Woo original (heap) — mixed rays", Baseline = true)]
    [BenchmarkCategory("Mixed")]
    public int WooOriginal_Mixed()
    {
        int hits = 0;
        for (int i = 0; i < N; i++)
            if (RayBox.HitBoundingBoxWooOriginal(MinB, MaxB, _rayOrigins[i], _rayDirs[i], out _))
                hits++;
        return hits;
    }

    [Benchmark(Description = "Woo stackalloc     — mixed rays")]
    [BenchmarkCategory("Mixed")]
    public int Woo_Mixed()
    {
        int hits = 0;
        for (int i = 0; i < N; i++)
            if (RayBox.HitBoundingBoxWoo(MinB, MaxB, _rayOrigins[i], _rayDirs[i], out _))
                hits++;
        return hits;
    }

    [Benchmark(Description = "Slab               — mixed rays")]
    [BenchmarkCategory("Mixed")]
    public int Slab_Mixed()
    {
        int hits = 0;
        for (int i = 0; i < N; i++)
            if (RayBox.HitBoundingBoxSlab(MinB, MaxB, _rayOrigins[i], _rayDirs[i], out _))
                hits++;
        return hits;
    }

    [Benchmark(Description = "Slab precomputed invDir — mixed rays (octree ideal)")]
    [BenchmarkCategory("Mixed")]
    public int Slab_PrecomputedInvDir()
    {
        int hits = 0;
        for (int i = 0; i < N; i++)
        {
            Vector3 invDir = Vector3.One / _rayDirs[i];
            if (RayBox.HitBoundingBoxSlabInvDir(MinB, MaxB, _rayOrigins[i], invDir, out _))
                hits++;
        }
        return hits;
    }
}

/// <summary>
/// The two implementations under test, plus the invDir variant.
/// Isolated here so the benchmark class stays readable.
/// </summary>
public static class RayBox
{
    // Quadrant constants — Woo only
    private const byte Left = 1;
    private const byte Middle = 2;
    private const byte Right = 0;

    // ── Woo original — heap allocations (the code as it existed before) ────────

    public static bool HitBoundingBoxWooOriginal(
        Vector3 minB, Vector3 maxB, Vector3 origin, Vector3 dir, out Vector3 coord)
    {
        bool inside = true;
        byte[] quadrant = new byte[3];   // ← heap allocation
        float[] maxT = new float[3];  // ← heap allocation
        float[] candidatePlane = new float[3];  // ← heap allocation

        coord = Vector3.Zero;

        for (int i = 0; i < 3; i++)
        {
            if (origin[i] < minB[i])
            {
                quadrant[i] = Left;
                candidatePlane[i] = minB[i];
                inside = false;
            }
            else if (origin[i] > maxB[i])
            {
                quadrant[i] = Right;
                candidatePlane[i] = maxB[i];
                inside = false;
            }
            else
            {
                quadrant[i] = Middle;
            }
        }

        if (inside) { coord = origin; return true; }

        for (int i = 0; i < 3; i++)
            maxT[i] = quadrant[i] != Middle && dir[i] != 0
                ? (candidatePlane[i] - origin[i]) / dir[i]
                : -1f;

        int whichPlane = 0;
        for (int i = 1; i < 3; i++)
            if (maxT[whichPlane] < maxT[i]) whichPlane = i;

        if (maxT[whichPlane] < 0) return false;

        for (int i = 0; i < 3; i++)
        {
            if (whichPlane != i)
            {
                coord[i] = origin[i] + maxT[whichPlane] * dir[i];
                if (coord[i] < minB[i] || coord[i] > maxB[i]) return false;
            }
            else
            {
                coord[i] = candidatePlane[i];
            }
        }

        return true;
    }

    // ── Woo (Graphics Gems 1990) — stackalloc ─────────────────────────────────

    public static bool HitBoundingBoxWoo(
        Vector3 minB, Vector3 maxB, Vector3 origin, Vector3 dir, out Vector3 coord)
    {
        bool inside = true;
        Span<byte> quadrant = stackalloc byte[3];
        Span<float> maxT = stackalloc float[3];
        Span<float> candidatePlane = stackalloc float[3];

        coord = Vector3.Zero;

        for (int i = 0; i < 3; i++)
        {
            if (origin[i] < minB[i])
            {
                quadrant[i] = Left;
                candidatePlane[i] = minB[i];
                inside = false;
            }
            else if (origin[i] > maxB[i])
            {
                quadrant[i] = Right;
                candidatePlane[i] = maxB[i];
                inside = false;
            }
            else
            {
                quadrant[i] = Middle;
            }
        }

        if (inside) { coord = origin; return true; }

        for (int i = 0; i < 3; i++)
            maxT[i] = quadrant[i] != Middle && dir[i] != 0
                ? (candidatePlane[i] - origin[i]) / dir[i]
                : -1f;

        int whichPlane = 0;
        for (int i = 1; i < 3; i++)
            if (maxT[whichPlane] < maxT[i]) whichPlane = i;

        if (maxT[whichPlane] < 0) return false;

        for (int i = 0; i < 3; i++)
        {
            if (whichPlane != i)
            {
                coord[i] = origin[i] + maxT[whichPlane] * dir[i];
                if (coord[i] < minB[i] || coord[i] > maxB[i]) return false;
            }
            else
            {
                coord[i] = candidatePlane[i];
            }
        }

        return true;
    }

    // ── Slab ──────────────────────────────────────────────────────────────────

    public static bool HitBoundingBoxSlab(
        Vector3 minB, Vector3 maxB, Vector3 origin, Vector3 dir, out Vector3 coord)
    {
        Vector3 invDir = Vector3.One / dir;
        return HitBoundingBoxSlabInvDir(minB, maxB, origin, invDir, out coord);
    }

    /// <summary>
    /// Slab variant that accepts a pre-computed <paramref name="invDir"/>.
    /// Use this when the same ray is tested against multiple boxes (octree traversal)
    /// — compute <c>invDir</c> once per ray before entering the traversal loop.
    /// </summary>
    public static bool HitBoundingBoxSlabInvDir(
        Vector3 minB, Vector3 maxB, Vector3 origin, Vector3 invDir, out Vector3 coord)
    {
        float tx1 = (minB.X - origin.X) * invDir.X;
        float tx2 = (maxB.X - origin.X) * invDir.X;
        float ty1 = (minB.Y - origin.Y) * invDir.Y;
        float ty2 = (maxB.Y - origin.Y) * invDir.Y;
        float tz1 = (minB.Z - origin.Z) * invDir.Z;
        float tz2 = (maxB.Z - origin.Z) * invDir.Z;

        float tmin = MathF.Max(MathF.Max(MathF.Min(tx1, tx2), MathF.Min(ty1, ty2)), MathF.Min(tz1, tz2));
        float tmax = MathF.Min(MathF.Min(MathF.Max(tx1, tx2), MathF.Max(ty1, ty2)), MathF.Max(tz1, tz2));

        if (tmax < 0f || tmin > tmax)
        {
            coord = Vector3.Zero;
            return false;
        }

        coord = origin + ((tmin < 0f ? tmax : tmin) * invDir);
        return true;
    }
}