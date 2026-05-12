```

BenchmarkDotNet v0.15.8, Windows 11 (10.0.26200.8246/25H2/2025Update/HudsonValley2)
Intel Core i9-14900KF 3.20GHz, 1 CPU, 32 logical and 24 physical cores
.NET SDK 10.0.203
  [Host]     : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3
  Job-YFEFPZ : .NET 10.0.7 (10.0.7, 10.0.726.21808), X64 RyuJIT x86-64-v3

IterationCount=10  WarmupCount=3  

```
| Method                                                | Mean      | Error    | StdDev   | Ratio | RatioSD | Gen0   | Allocated | Alloc Ratio |
|------------------------------------------------------ |----------:|---------:|---------:|------:|--------:|-------:|----------:|------------:|
| &#39;Original — 24 vertices, 48 indices, duplicate edges&#39; | 129.01 ns | 3.859 ns | 2.553 ns |  1.00 |    0.03 | 0.0501 |     944 B |        1.00 |
| &#39;Rewritten — 8 vertices, 24 indices, unique edges&#39;    |  23.46 ns | 0.514 ns | 0.340 ns |  0.18 |    0.00 | 0.0246 |     464 B |        0.49 |
