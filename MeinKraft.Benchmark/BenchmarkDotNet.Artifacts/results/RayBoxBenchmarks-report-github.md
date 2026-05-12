```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8246/25H2/2025Update/HudsonValley2)
Intel Core i9-14900KF 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  Job-YFEFPZ : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3

IterationCount=10  WarmupCount=3  

```
| Method                                                | N     | Mean         | Error       | StdDev      | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------------------ |------ |-------------:|------------:|------------:|------:|--------:|----------:|------------:|
| **&#39;Woo original (heap) — guaranteed hit&#39;**                | **100**   |     **752.3 ns** |    **10.97 ns** |     **7.25 ns** |  **0.88** |    **0.01** |         **-** |          **NA** |
| &#39;Woo stackalloc     — guaranteed hit&#39;                 | 100   |   4,021.3 ns |    62.48 ns |    41.32 ns |  4.71 |    0.05 |         - |          NA |
| &#39;Slab               — guaranteed hit&#39;                 | 100   |   1,026.5 ns |     5.16 ns |     3.07 ns |  1.20 |    0.00 |         - |          NA |
| &#39;Woo original (heap) — guaranteed miss&#39;               | 100   |     735.9 ns |    12.87 ns |     8.51 ns |  0.86 |    0.01 |         - |          NA |
| &#39;Woo stackalloc     — guaranteed miss&#39;                | 100   |   3,344.0 ns |    34.42 ns |    22.77 ns |  3.91 |    0.03 |         - |          NA |
| &#39;Slab               — guaranteed miss&#39;                | 100   |     952.7 ns |     3.16 ns |     1.88 ns |  1.11 |    0.00 |         - |          NA |
| &#39;Woo original (heap) — mixed rays&#39;                    | 100   |     854.5 ns |     3.91 ns |     2.59 ns |  1.00 |    0.00 |         - |          NA |
| &#39;Woo stackalloc     — mixed rays&#39;                     | 100   |   3,173.5 ns |   149.05 ns |    98.59 ns |  3.71 |    0.11 |         - |          NA |
| &#39;Slab               — mixed rays&#39;                     | 100   |   1,055.2 ns |     2.15 ns |     1.13 ns |  1.23 |    0.00 |         - |          NA |
| &#39;Slab precomputed invDir — mixed rays (octree ideal)&#39; | 100   |   1,005.0 ns |     6.68 ns |     4.42 ns |  1.18 |    0.01 |         - |          NA |
|                                                       |       |              |             |             |       |         |           |             |
| **&#39;Woo original (heap) — guaranteed hit&#39;**                | **10000** |  **74,440.9 ns** | **1,293.47 ns** |   **855.55 ns** |  **0.31** |    **0.00** |         **-** |          **NA** |
| &#39;Woo stackalloc     — guaranteed hit&#39;                 | 10000 | 293,451.0 ns | 9,297.62 ns | 6,149.80 ns |  1.23 |    0.03 |         - |          NA |
| &#39;Slab               — guaranteed hit&#39;                 | 10000 | 102,037.7 ns |   277.44 ns |   165.10 ns |  0.43 |    0.00 |         - |          NA |
| &#39;Woo original (heap) — guaranteed miss&#39;               | 10000 |  63,769.4 ns |   836.79 ns |   553.49 ns |  0.27 |    0.00 |         - |          NA |
| &#39;Woo stackalloc     — guaranteed miss&#39;                | 10000 | 241,981.9 ns | 6,973.53 ns | 4,612.56 ns |  1.02 |    0.02 |         - |          NA |
| &#39;Slab               — guaranteed miss&#39;                | 10000 |  95,139.5 ns |   435.57 ns |   288.10 ns |  0.40 |    0.00 |         - |          NA |
| &#39;Woo original (heap) — mixed rays&#39;                    | 10000 | 237,850.1 ns | 1,313.94 ns |   869.09 ns |  1.00 |    0.00 |         - |          NA |
| &#39;Woo stackalloc     — mixed rays&#39;                     | 10000 | 483,299.8 ns | 3,135.25 ns | 2,073.78 ns |  2.03 |    0.01 |         - |          NA |
| &#39;Slab               — mixed rays&#39;                     | 10000 | 106,997.2 ns |   330.17 ns |   218.38 ns |  0.45 |    0.00 |         - |          NA |
| &#39;Slab precomputed invDir — mixed rays (octree ideal)&#39; | 10000 | 100,918.2 ns |   232.09 ns |   138.11 ns |  0.42 |    0.00 |         - |          NA |
