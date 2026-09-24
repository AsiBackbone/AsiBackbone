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

## Real-provider claim contention (issue #823)

SQLite serializes writers at the database level, so the SQLite tests above cannot show what SQL Server or PostgreSQL do when two workers' claim statements genuinely overlap. An opt-in suite runs the claim path against real servers:

```text
tests/AsiBackbone.EntityFrameworkCore.Tests/Outbox/Providers/EfCoreGovernanceOutboxProviderContentionTests.cs
```

It covers three provider configurations: SQL Server with `READ_COMMITTED_SNAPSHOT OFF`, SQL Server with `READ_COMMITTED_SNAPSHOT ON`, and PostgreSQL at its default read committed isolation. SQL Server is covered twice because the snapshot setting changes whether a statement reads locked rows or their last committed versions.

| Scenario | How the overlap is established | Invariant checked |
| --- | --- | --- |
| Pending claims | The holder claims inside an open transaction. The contender starts its claim, and the holder commits only after the database reports the contender waiting on a lock (or the contender finishes first). | The two claim sets are disjoint, their total does not exceed the eligible rows, and every returned claim is still the active claim on its row. |
| Retry-ready claims | The same overlap, over entries made retry-ready with a retryable failure. | The same invariants. |
| Reclaim after lease expiry | Entries are claimed by a worker whose lease then expires; two workers reclaim them with the same overlap. | The same invariants, and no returned claim carries the expired token. |
| Unscripted autocommit contention | Several workers start together and claim small batches until the backlog is empty. | No entry is claimed by more than one worker, and every entry is claimed exactly once. |

The "still the active claim" check matters on its own. A claim statement that overwrote another worker's owner and token after that worker had read its claims back would leave both workers holding the entry even though the two returned sets never overlap.

Deadlock or serialization victims in the unscripted test retry on the worker's next attempt and are reported in the failure message. They are not an overlap.

### How to run locally

Each provider is enabled by a server-level connection string. The login needs permission to create and drop databases, and on SQL Server `VIEW SERVER STATE`, which the test uses to detect a blocked claim. Each test creates and drops its own database.

```bash
docker run -d --name asib-mssql -e ACCEPT_EULA=Y -e 'MSSQL_SA_PASSWORD=AsiBackbone-local-823!' -p 1433:1433 mcr.microsoft.com/mssql/server:2022-latest
docker run -d --name asib-postgres -e POSTGRES_PASSWORD=asibackbone-local-823 -p 5432:5432 postgres:17

# docker run -d returns before either server accepts connections; wait until both are ready.
until docker exec asib-mssql /opt/mssql-tools18/bin/sqlcmd -C -S localhost -U sa -P 'AsiBackbone-local-823!' -Q 'SELECT 1' -b > /dev/null 2>&1; do sleep 2; done
until docker exec asib-postgres pg_isready -U postgres > /dev/null 2>&1; do sleep 1; done

export ASIBACKBONE_TEST_SQLSERVER_CONNECTION='Server=localhost,1433;User Id=sa;Password=AsiBackbone-local-823!;TrustServerCertificate=True;Encrypt=False'
export ASIBACKBONE_TEST_POSTGRES_CONNECTION='Host=localhost;Port=5432;Username=postgres;Password=asibackbone-local-823'

dotnet test --project ./tests/AsiBackbone.EntityFrameworkCore.Tests/AsiBackbone.EntityFrameworkCore.Tests.csproj --configuration Release
```

The CI job gets the same readiness guarantee from the service containers' health checks. Without the environment variables the provider tests are skipped, so the default local and CI test runs are unaffected. Setting `ASIBACKBONE_TEST_PROVIDERS_REQUIRED=true` turns a missing connection string into a failure. The `EF Core provider contention` job in `.github/workflows/ci.yml` sets it, runs both servers as service containers, and runs the EF Core test project against them.

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
