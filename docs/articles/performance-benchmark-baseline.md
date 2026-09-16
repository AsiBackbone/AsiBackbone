# Performance Benchmark Baseline

This article documents the repeatable benchmark entry points for core AsiBackbone policy, endpoint governance, audit-residue, decision/result normalization, and outbox drain hot paths.

The benchmark baseline is measurement-first. It exists to help maintainers decide whether caching, pooling, short-circuit behavior, scoped-service changes, metadata handling, or other optimization work is justified by observed hot-path activity. It should not be used as a substitute for unit tests, integration tests, or consumer-specific load testing.

## Benchmark entry points

The repository has two benchmark entry points:

| Project | Purpose |
| --- | --- |
| `benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet` | Primary optimization baseline with BenchmarkDotNet `MemoryDiagnoser` allocation output. |
| `benchmarks/AsiBackbone.Benchmarks` | Lightweight manual runner for quick smoke checks and local trend checks. |

Use the BenchmarkDotNet runner for optimization PR before/after evidence:

```powershell
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet -- --filter "*"
```

Use focused filters when measuring one hot area:

```powershell
# Decision construction with no, one, and multiple reason paths
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet -- --filter "*Decision*"

# OperationResult success/failure reason normalization paths
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet -- --filter "*OperationResult*"

# Outbox drain batch and scoped-drain paths
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet -- --filter "*OutboxDrain*"

# ASP.NET Core endpoint governance allow, warning, and deny paths
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet -- --filter "*EndpointGovernance*"

# Policy evaluation and constraint exception paths
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet -- --filter "*Policy*"
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet -- --filter "*ConstraintException*"

# Decision receipt construction and metadata variants
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks.BenchmarkDotNet -- --filter "*DecisionReceipt*"
```

The manual runner remains useful for quick checks while editing:

```powershell
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks -- --iterations 200000 --warmup 10000
```

Use smaller iteration counts for local smoke checks:

```powershell
dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks -- --iterations 1000 --warmup 100
```

The benchmark projects are included in `AsiBackbone.slnx`, but benchmark runs remain manual. They are intentionally separate from normal CI unit tests so routine builds do not fail because of machine-specific timing variation.

## Current baseline scenarios

The BenchmarkDotNet runner captures latency and allocation measurements for representative Core, ASP.NET Core adapter, audit, decision/result normalization, and outbox scenarios:

| Scenario | Purpose |
| --- | --- |
| `decision.allow_no_reasons` | Direct allowed decision construction with no reason collection. |
| `decision.deny_one_reason` | Denied decision construction with one prebuilt reason. |
| `decision.deny_multiple_reasons` | Denied decision construction and reason-code projection for multiple reasons. |
| `decision.escalate_one_reason` | Escalation-recommended decision construction. |
| `operation_result.success_no_reasons` | Successful `OperationResult` construction with no reasons or warnings. |
| `operation_result.failure_one_reason` | Failed `OperationResult` construction with one prebuilt reason. |
| `operation_result.failure_multiple_reasons` | Failed result normalization and reason-code projection with multiple reasons. |
| `policy.zero_constraints` | Empty policy-evaluation path. |
| `policy.all_allow_8` | Common all-allow path with several constraints. |
| `policy.warning_and_denial_full` | Full aggregation with allow, warning, and denial results. |
| `policy.first_denial_short_circuit` | Optional first-denial fast-abort path. |
| `policy.acknowledgment_required` | Constraint evaluation followed by acknowledgment-required decision-policy composition. |
| `policy.escalation_recommended` | Constraint evaluation followed by escalation-recommended decision-policy composition. |
| `policy.constraint_exception_as_denial` | Fail-closed constraint exception path that emits a synthetic denied decision. |
| `endpoint_governance.policy_allow` | Endpoint governance with a host policy evaluator returning allow. |
| `endpoint_governance.policy_warning` | Endpoint governance with a host policy evaluator returning warning. |
| `endpoint_governance.policy_deny` | Endpoint governance with a host policy evaluator returning deny. |
| `outbox_drain.small_batch_25` | Provider-neutral drain processing for 25 pending entries. |
| `outbox_drain.medium_batch_100` | Provider-neutral drain processing for 100 pending entries. |
| `outbox_drain.scoped_medium_batch_100` | DI scope creation, scoped drain resolution, and medium-batch processing. |
| `audit_residue.from_decision` | Decision receipt creation from a governance decision. |
| `audit_residue.builder_no_metadata` | Fluent decision receipt builder with no metadata. |
| `audit_residue.builder_one_metadata` | Fluent builder with one metadata entry. |
| `audit_residue.builder_many_metadata` | Fluent builder with multiple metadata entries. |

The endpoint-governance scenarios use test doubles for the host policy evaluator so the measured path stays focused on the ASP.NET Core adapter, metadata descriptor, request-correlation resolution, decision mapping, and safe allow/block result handling.

The outbox-drain scenarios use provider-neutral fake storage and a no-op emitter. They measure framework drain behavior and allocations without provider SDK, network, database, or exporter variability.

## Exception-as-denial benchmark interpretation

`policy.constraint_exception_as_denial` measures fail-closed evaluator behavior when a constraint unexpectedly throws and `TreatConstraintExceptionAsDenial` converts that fault into a synthetic denied governance decision.

Do not interpret this benchmark as a recommended denial-authoring pattern. Expected policy blocks should return `ConstraintEvaluationResult.Deny(...)`. Throwing an exception only to deny a request forces the evaluator through an abnormal failure path and can add avoidable latency and allocation pressure.

Use the benchmark to verify that the safety boundary remains bounded and observable. When this path becomes hot in a consumer workload, first fix the constraint so expected denials are returned explicitly. Optimize the framework path only after profiling demonstrates that genuine unexpected faults are frequent enough to matter.

## Output fields

BenchmarkDotNet output includes timing statistics and memory columns. With `MemoryDiagnoser`, the important columns are:

- `Mean`: average time per operation;
- `Median`: midpoint time value when present;
- `Gen0`: generation 0 collection activity normalized by BenchmarkDotNet;
- `Allocated`: allocated bytes per operation.

The lightweight manual runner prints scenario name, description, iterations, mean nanoseconds, allocated bytes, total allocated bytes, Gen0 count, and elapsed milliseconds. Both runners include runtime, operating-system, architecture, warmup, and measurement context.

## Interpretation guidance

Benchmark results are for trend detection, not absolute guarantees. Compare results only across:

- the same machine or comparable CI runner type;
- the same build configuration, preferably Release;
- the same .NET runtime family;
- comparable repository revisions;
- comparable filters, iteration settings, and warmup settings.

Prefer at least three repeated BenchmarkDotNet runs before treating a small delta as meaningful. Use median or repeated-run direction when results are noisy.

Do not use these numbers to promise consumer latency. Host applications should benchmark their actual constraints, decision policies, persistence, middleware, logging, telemetry emitters, durable stores, network providers, and deployment topology.

Outbox drain benchmarks are especially sensitive to host-owned infrastructure. Real durable stores, provider SDKs, exporters, retry policies, batch sizes, row claiming, and network conditions can dominate runtime.

## Threat-contributor exception metadata allocation

The metadata dictionary allocated when a threat-model contributor throws remains a profiling-gated failure path, not a routine evaluator hot path.

No optimization is warranted without evidence that contributor exceptions dominate incident traffic. The allocation is intentionally local to the denied decision so the evaluator avoids shared mutable metadata, pooling complexity, and public API churn for an exceptional path.

Preserve these boundaries when revisiting the area:

- keep the public reason code and message configured by evaluator options;
- include only bounded diagnostic metadata such as contributor identity and exception type;
- do not copy exception messages, stack traces, secrets, raw payloads, prompts, protected content, or user input into decisions;
- preserve logger behavior for operational diagnostics;
- prefer fixing, disabling, circuit-breaking, or isolating unstable host-owned contributors before optimizing framework metadata construction.

Revisit optimization only after BenchmarkDotNet output, `dotnet-trace`, `dotnet-counters`, `dotnet-gcdump`, or production-equivalent profiling shows the path is materially hot.

## Allocation profiling plan

Start with BenchmarkDotNet `MemoryDiagnoser` output. When `Allocated`, `Gen0`, or run-to-run variance identifies a hot scenario, move to process-level profiling.

### dotnet-counters

```powershell
dotnet-counters monitor --process-id <PID> System.Runtime
```

Watch `alloc-rate`, `gen-0-gc-count`, `gc-heap-size`, and `% time in GC` while running focused benchmark filters.

### dotnet-trace

```powershell
dotnet-trace collect --process-id <PID> --providers Microsoft-Windows-DotNETRuntime:0x1C000080018:5 --output artifacts/perf/asi-backbone-hotpath.nettrace
```

Inspect the trace in Visual Studio, PerfView, or another trace viewer.

### dotnet-gcdump

```powershell
dotnet-gcdump collect --process-id <PID> --output artifacts/perf/asi-backbone-hotpath.gcdump
```

Use a GC dump to identify object types dominating the managed heap during a focused run.

### PerfView

```powershell
PerfView.exe /AcceptEula /NoGui /Collect:GCCollectOnly /MaxCollectSec:120 /DataFile:artifacts/perf/asi-backbone-hotpath.etl.zip collect
```

Use PerfView when BenchmarkDotNet identifies a scenario but lighter tools do not provide enough allocation-stack detail.

## Optimization decision rule

Before adding caching, pooling, shared mutable state, or specialized fast paths, capture benchmark output and document:

1. the scenario under pressure;
2. the before/after latency and allocation deltas;
3. whether the change improves common paths without making auditability or host-owned boundaries harder to reason about;
4. whether an existing option, such as `ShortCircuitOnFirstDenial`, is sufficient;
5. whether the bottleneck is framework orchestration or host-owned I/O, storage, telemetry, or provider behavior.

This keeps optimization work evidence-driven and avoids adding complexity before the hot path is measured.

## Related documentation

- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Policy Evaluator Allocation Review](policy-evaluator-allocation-review.md)
- [Constraint Exception Policy](constraint-exception-policy.md)
- [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)