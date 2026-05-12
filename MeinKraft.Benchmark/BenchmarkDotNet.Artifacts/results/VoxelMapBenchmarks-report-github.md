```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8246/25H2/2025Update/HudsonValley2)
Intel Core i9-14900KF 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.203
  [Host] : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3

Toolchain=InProcessEmitToolchain  IterationCount=10  WarmupCount=3  

```
| Method                                                         | Mean         | Error      | StdDev     | Ratio | RatioSD | Allocated | Alloc Ratio |
|--------------------------------------------------------------- |-------------:|-----------:|-----------:|------:|--------:|----------:|------------:|
| &#39;Original  — SetMapPortion dirty (repeated per-block marking)&#39; |           NA |         NA |         NA |     ? |       ? |        NA |           ? |
| &#39;Rewritten — SetMapPortion dirty (HashSet deduplication)&#39;      |           NA |         NA |         NA |     ? |       ? |        NA |           ? |
| &#39;Original  — GetBlock (recomputed dims, VectorIndexUtil)&#39;      | 3,060.591 μs | 43.8985 μs | 29.0362 μs | 1.000 |    0.01 |         - |          NA |
| &#39;Rewritten — GetBlock (cached dims, inlined math)&#39;             | 2,625.618 μs | 43.9668 μs | 26.1639 μs | 0.858 |    0.01 |         - |          NA |
| &#39;Original  — GetMapPortion (per-voxel index recomputation)&#39;    |     3.661 μs |  0.0144 μs |  0.0085 μs | 0.001 |    0.00 |         - |          NA |
| &#39;Rewritten — GetMapPortion (hoisted row bases)&#39;                |     2.977 μs |  0.0125 μs |  0.0065 μs | 0.001 |    0.00 |         - |          NA |

Benchmarks with issues:
  VoxelMapBenchmarks.'Original  — SetMapPortion dirty (repeated per-block marking)': Job-SHSVTU(Toolchain=InProcessEmitToolchain, IterationCount=10, WarmupCount=3)
  VoxelMapBenchmarks.'Rewritten — SetMapPortion dirty (HashSet deduplication)': Job-SHSVTU(Toolchain=InProcessEmitToolchain, IterationCount=10, WarmupCount=3)
