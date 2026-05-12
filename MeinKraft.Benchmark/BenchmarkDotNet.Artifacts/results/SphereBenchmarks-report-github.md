```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8246/25H2/2025Update/HudsonValley2)
Intel Core i9-14900KF 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  Job-YFEFPZ : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3

IterationCount=10  WarmupCount=3  

```
| Method                                                    | Segments | Rings | Mean        | Error     | StdDev    | Ratio | Gen0   | Gen1   | Allocated | Alloc Ratio |
|---------------------------------------------------------- |--------- |------ |------------:|----------:|----------:|------:|-------:|-------:|----------:|------------:|
| **&#39;Original — duplicate seam, pole rings, inline trig&#39;**      | **16**       | **8**     |  **2,068.1 ns** |  **33.60 ns** |  **22.22 ns** |  **1.00** | **0.3357** | **0.0076** |   **6.17 KB** |        **1.00** |
| &#39;Rewritten — pole vertices, seam wrap, pre-computed trig&#39; | 16       | 8     |    781.9 ns |  16.10 ns |  10.65 ns |  0.38 | 0.3529 |      - |    6.5 KB |        1.05 |
|                                                           |          |       |             |           |           |       |        |        |           |             |
| **&#39;Original — duplicate seam, pole rings, inline trig&#39;**      | **16**       | **32**    |  **8,094.2 ns** | **100.89 ns** |  **66.73 ns** |  **1.00** | **1.3123** | **0.0458** |  **24.17 KB** |        **1.00** |
| &#39;Rewritten — pole vertices, seam wrap, pre-computed trig&#39; | 16       | 32    |  2,624.6 ns |  60.72 ns |  36.13 ns |  0.32 | 1.3428 | 0.0305 |  24.69 KB |        1.02 |
|                                                           |          |       |             |           |           |       |        |        |           |             |
| **&#39;Original — duplicate seam, pole rings, inline trig&#39;**      | **64**       | **8**     |  **8,507.3 ns** |  **75.06 ns** |  **49.65 ns** |  **1.00** | **1.3123** | **0.0458** |  **24.17 KB** |        **1.00** |
| &#39;Rewritten — pole vertices, seam wrap, pre-computed trig&#39; | 64       | 8     |  2,967.2 ns |  64.78 ns |  38.55 ns |  0.35 | 1.3504 |      - |  24.88 KB |        1.03 |
|                                                           |          |       |             |           |           |       |        |        |           |             |
| **&#39;Original — duplicate seam, pole rings, inline trig&#39;**      | **64**       | **32**    | **33,669.9 ns** | **349.88 ns** | **231.42 ns** |  **1.00** | **5.1880** | **0.6714** |  **96.17 KB** |        **1.00** |
| &#39;Rewritten — pole vertices, seam wrap, pre-computed trig&#39; | 64       | 32    |  9,817.9 ns | 236.37 ns | 140.66 ns |  0.29 | 5.2490 |      - |  97.06 KB |        1.01 |
