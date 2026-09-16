# EF Core Outbox Concurrency Validation

Issue: #311.

This page documents the repeatable validation path for EF Core-backed outbox persistence and provider-neutral drain behavior under concurrent writes, retry handling, and drain-worker contention.

The goal is evidence, not a production throughput guarantee. These tests run against SQLite in shared in-memory mode so they remain CI-friendly while still exercising EF Core relational persistence and separate host-owned `DbContext` instances.

## What the validation covers

The validation lives in:

```text
tests/AsiBackbone.EntityFrameworkCore.Tests/EfCoreOutboxConcurrencyValidationTests.cs
```

The test class covers three focused scenarios:

| Scenario | Evidence produced | Boundary confirmed |
| --- | --- | --- |
| Concurrent writers | Multiple host-owned EF Core contexts enqueue outbox entries and append lifecycle records against the same relational store. | Local outbox and lifecycle evidence is preserved under concurrent write pressure. |
| Concurrent drain-worker contention | Two drain instances can reach the same pending entry before either saves final delivery state. | The current provider-neutral drain is selection-based, not claim-based, so hosts still need a single active worker, partitioning, provider idempotency, or host-owned claim/lease behavior for production multi-worker deployments. |
| Retryable transient failure | A simulated downstream failure is persisted as retryable outbox state with retry timing and retry count evidence. | Transient provider failures do not erase local outbox records and can be queried later as retry-ready work. |

## How to run locally

Run the EF Core test project from the repository root:

```bash
dotnet test ./tests/AsiBackbone.EntityFrameworkCore.Tests/AsiBackbone.EntityFrameworkCore.Tests.csproj --configuration Release
```

To run only the concurrency validation tests:

```bash
dotnet test ./tests/AsiBackbone.EntityFrameworkCore.Tests/AsiBackbone.EntityFrameworkCore.Tests.csproj --configuration Release --filter FullyQualifiedName~EfCoreOutboxConcurrencyValidationTests
```

## Expected interpretation

Passing tests support the following bounded claims:

- EF Core-backed outbox entries and lifecycle records can be preserved by concurrent host-owned contexts in the tested relational path.
- Retryable downstream failures can be persisted and later queried as retry-ready outbox records.
- The current provider-neutral drain can expose the same pending entry to more than one worker when workers read before either one saves final state.

Do **not** interpret these tests as proof of exactly-once delivery, universal throughput, production distributed locking, or provider-side idempotency. The current outbox APIs do not claim, lease, lock, or hide rows before emission.

## Production guidance retained by the evidence

For production multi-replica hosts, continue to prefer one of these patterns:

- one active drain worker per durable outbox partition;
- partitioned workers with disjoint selection criteria;
- a host-owned claim/lease design before provider emission;
- provider-side idempotency using stable envelope or outbox identifiers.

The validation deliberately backs the current reliability story without overstating the package boundary.
