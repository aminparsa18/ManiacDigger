```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8246/25H2/2025Update/HudsonValley2)
Intel Core i9-14900KF 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  Job-QKDGBD : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  Job-YFEFPZ : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3

IterationCount=10  WarmupCount=3  

```
| Method                                     | Job        | InvocationCount | UnrollFactor | Mean     | Error    | StdDev   | Ratio | RatioSD | Allocated | Alloc Ratio |
|------------------------------------------- |----------- |---------------- |------------- |---------:|---------:|---------:|------:|--------:|----------:|------------:|
| &#39;Original  — alloc all chunks (new T[])&#39;   | Job-QKDGBD | 1               | 1            |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
| &#39;Rewritten — alloc all chunks (ArrayPool)&#39; | Job-QKDGBD | 1               | 1            |       NA |       NA |       NA |     ? |       ? |        NA |           ? |
|                                            |            |                 |              |          |          |          |       |         |           |             |
| &#39;Original  — GetBlock hot path&#39;            | Job-YFEFPZ | Default         | 16           | 44.49 μs | 0.480 μs | 0.317 μs |     ? |       ? |         - |           ? |
| &#39;Rewritten — GetBlock hot path&#39;            | Job-YFEFPZ | Default         | 16           | 54.21 μs | 0.906 μs | 0.599 μs |     ? |       ? |         - |           ? |
| &#39;Original  — SetBlock hot path&#39;            | Job-YFEFPZ | Default         | 16           | 45.62 μs | 0.410 μs | 0.244 μs |     ? |       ? |         - |           ? |
| &#39;Rewritten — SetBlock hot path&#39;            | Job-YFEFPZ | Default         | 16           | 59.11 μs | 0.377 μs | 0.225 μs |     ? |       ? |         - |           ? |

Benchmarks with issues:
  ChunkedMap2dBenchmarks.'Original  — alloc all chunks (new T[])': Job-QKDGBD(InvocationCount=1, IterationCount=10, UnrollFactor=1, WarmupCount=3)
  ChunkedMap2dBenchmarks.'Rewritten — alloc all chunks (ArrayPool)': Job-QKDGBD(InvocationCount=1, IterationCount=10, UnrollFactor=1, WarmupCount=3)
