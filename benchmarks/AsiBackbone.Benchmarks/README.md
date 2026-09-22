# AsiBackbone.Benchmarks

This project provides a lightweight manual benchmark entry point for core AsiBackbone policy hot paths.

It is intentionally separate from the unit-test projects. The runner is for local smoke checks and quick trend detection, not for pass/fail CI assertions.

For optimization PR baselines, prefer the sibling BenchmarkDotNet project:

```powershell
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet -- --filter "*"
```

The BenchmarkDotNet project uses `MemoryDiagnoser`, so it reports allocation columns such as Gen0 activity and allocated bytes per operation.

## Run the lightweight manual runner

From the repository root:

```powershell
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks -- --iterations 200000 --warmup 10000
```

The manual runner prints a Markdown table with:

- scenario name;
- scenario description;
- measurement iterations;
- mean nanoseconds per operation;
- allocated bytes per operation;
- total allocated bytes;
- Gen0 collection count;
- elapsed milliseconds.

## Scenario coverage

The current baseline covers:

- zero-constraint policy evaluation;
- all-allow constraint evaluation;
- mixed warning and denial full aggregation;
- first-denial short-circuit evaluation;
- acknowledgment-required decision policy composition;
- escalation-recommended decision policy composition;
- endpoint governance allow, warning, and deny decisions;
- outbox drain batches of 25 and 100 pending entries;
- scoped outbox drain for a 100-entry batch;
- decision receipt creation from a governance decision.

## Interpretation

Benchmark results are useful for trend detection on the same machine, runtime, build configuration, and repository version. They are not absolute performance guarantees and should not be compared across unrelated machines without context.

Use the BenchmarkDotNet runner to decide whether proposed caching, pooling, short-circuit, or allocation-reduction work is justified by measured policy, endpoint governance, decision receipt, or outbox activity.
