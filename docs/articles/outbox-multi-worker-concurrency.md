# Outbox Multi-Worker Concurrency Guidance

This article records the current concurrency review for the provider-neutral outbox drain, the in-memory outbox store, and the EF Core-backed outbox store.

The goal is to help hosts avoid accidental duplicate emissions when an ASP.NET Core application is horizontally scaled and the hosted drain worker is registered in every replica.

## Summary decision

Claim leasing is enabled by default. `GovernanceOutboxOptions.UseClaimLeases` defaults to `true`, and `ClaimWorkerId` defaults to the machine name and process identifier so replicas of the same deployment do not share a claim owner. The previous default left two replicas free to select and emit the same envelope, which is not a safe default for a durable outbox.

The default claim-lease path requires a store implementing `IGovernanceOutboxClaimStore`. Both shipped stores do. A host supplying its own store that does not implement it must set `UseClaimLeases` to `false`; the drain throws rather than silently falling back, because falling back would restore the duplicate-emission behavior the default exists to prevent.

Hosts that opt out keep the previous behavior: `FindPendingAsync` and `FindRetryReadyAsync` return candidate rows and do not claim, lease, lock, or hide rows from another worker. That path is safe for a single active worker and for local or test validation, and it requires partitioning, a single worker role, or provider-side idempotency when scaled.

Source-of-truth check: the runtime default is defined by [`GovernanceOutboxOptions.UseClaimLeases`](https://github.com/AsiBackbone/AsiBackbone/blob/main/src/AsiBackbone.Core/Outbox/GovernanceOutboxOptions.cs), and [`ValidateAcceptsDefaultOptions`](https://github.com/AsiBackbone/AsiBackbone/blob/main/tests/AsiBackbone.Core.Tests/Outbox/AsiBackboneGovernanceOutboxOptionsTests.cs) asserts that a newly constructed options instance has claim leases enabled. Keep this article aligned with those sources if the default changes.

Claim leasing reduces duplicate selection between cooperating workers. Downstream provider delivery remains at-least-once unless the provider and host enforce idempotency.

The selected design direction is recorded in [Outbox Claim and Lease Design Record](outbox-claim-lease-design.md).

## Lease scope and bounded reclaim

Two properties bound what a lease covers and how long an entry can be recycled:

- `ClaimPageSize` (default `10`) limits how many entries are claimed under one lease before the drain claims again. A drain pass claims a page, drains it, then claims the next page from a fresh clock reading. Leasing a whole batch at once let a slow emitter exhaust the lease partway through, leaving later entries reclaimable by a peer while they were still in flight.
- `MaxClaimAttempts` (default `5`) bounds reclaim. An emitter that hangs or is killed mid-emission leaves an entry claimed but never failed, so its retry count never advances and the retry-based poison-message policy never applies. The claim count does advance on every reclaim, so it is the only signal that can end that loop. The check runs before emission, so a repeatedly reclaimed entry is not handed to the emitter again. Setting `DeadLetterOnMaxClaimAttempts` to `false` restores the unbounded behavior.
## Repeatable validation path

Issue #311 adds a CI-friendly EF Core validation path for concurrent outbox/lifecycle writes, retryable drain failures, and drain-worker contention:

```text
tests/AsiBackbone.EntityFrameworkCore.Tests/EfCoreOutboxConcurrencyValidationTests.cs
```

The validation deliberately confirms both sides of the reliability story:

- EF Core-backed local outbox and lifecycle records can be preserved under concurrent host-owned context writes in the tested SQLite relational path.
- Retryable provider failures remain represented as local outbox state and can be queried as retry-ready work.
- Workers using the default claim-lease path emit a contended pending entry exactly once (`ConcurrentDrainWorkersEmitPendingEntryOnceUnderDefaultClaimLeases`).
- Workers that opt out of claim leases can still reach the same pending entry before final state is saved (`ConcurrentDrainWorkersCanReachSamePendingEntryWhenClaimLeasesAreDisabled`), so that path should not be described as exactly-once or duplicate-proof.

See [EF Core Outbox Concurrency Validation](../quality/ef-core-outbox-concurrency-validation.md) for the local command and interpretation guidance.

## Current behavior

| Area | Current behavior | Multi-worker implication |
| --- | --- | --- |
| `IGovernanceOutboxStore.FindPendingAsync` | Returns pending entries ordered for delivery. | Selection only. It does not claim, lease, lock, or hide rows from another worker. |
| `IGovernanceOutboxStore.FindRetryReadyAsync` | Returns retry-ready entries ordered for delivery. | Selection only. It does not prevent another worker from selecting the same entry. |
| `IGovernanceOutboxClaimStore` | Adds explicit `ClaimPendingAsync`, `ClaimRetryReadyAsync`, claim completion, save, and release operations. | Cooperating workers emit only after acquiring a claim lease. Completion verifies claim owner/token before final state transition. |
| `GovernanceOutboxDrain` | Uses the claim-capable path by default because `UseClaimLeases = true`. It requires a claim-capable store and emits only after claim acquisition. The existing candidate path is used only when a host explicitly sets `UseClaimLeases = false`. | Explicitly opting out restores a path where multiple workers can select and emit the same entry before final state is saved. Claim leasing reduces duplicate selection risk but does not create exactly-once provider delivery. |
| `EfCoreGovernanceOutboxStore` | Uses EF Core persistence and configured concurrency tokens for state updates. It also implements the claim-capable store contract with claim owner, token, claimed time, expiration, and attempt count fields, and claims a batch in one set-based update that rechecks eligibility on every row it writes. | Hosts must apply the claim schema/migration changes before running the default claim-lease path against a durable EF Core outbox in production. A host that explicitly disables claim leases accepts the non-claiming duplicate-emission risk. Claim acquisition is provider-neutral LINQ; see [EF Core claim guarantees by provider](#ef-core-claim-guarantees-by-provider) for what is verified on SQL Server, PostgreSQL, and SQLite. |
| `InMemoryGovernanceOutboxStore` | Intended for tests, samples, and local validation. Same-entry status transitions and claim updates use single-process compare-and-swap updates. | Useful for local validation and tests only. It is not durable and does not model cross-replica infrastructure behavior. |
| Hosted drain worker | Runs wherever it is registered and enabled. | In scaled deployments, each replica may run a worker unless the host disables, partitions, or claim-coordinates it. |

## What optimistic concurrency does and does not solve

The EF Core persistence configuration marks `ConcurrencyStamp` as a concurrency token. That is useful for detecting stale updates when two contexts try to save incompatible state transitions for the same row.

However, the non-claiming drain flow performs provider emission before the final delivered/failed state is saved. Optimistic concurrency at save time cannot undo a provider call that already happened. In other words:

```text
worker A reads pending row
worker B reads same pending row
worker A emits to provider
worker B emits to provider
worker A saves delivered
worker B may hit a concurrency conflict or overwrite attempt
```

Even if the second save fails, the second provider emission may already have occurred. Treat optimistic concurrency as state-protection, not as duplicate-emission protection.

Claim leasing moves the coordination point before provider emission for cooperating workers:

```text
worker A claims row 123
worker B cannot claim row 123 while the lease is active
worker A emits to provider
worker A completes the row only if the claim token still matches
```

A crashed worker's claim becomes eligible for reclaim after `ClaimExpiresUtc`. That reclaim path is necessary for recovery, but it means provider delivery should still be treated as at-least-once.

## Recommended host patterns

### 1. Single active worker per durable outbox partition

For most hosts, the safest default is one active drain worker per shared durable outbox partition.

Common deployment patterns include:

- run the web/API replicas with `GovernanceOutboxDrainWorkerOptions.Enabled = false`;
- run one dedicated worker process, job, container, or app service instance with the worker enabled;
- use platform leader election or a singleton scheduler if the hosting platform provides it;
- ensure only one replica has permission or configuration to drain a given outbox partition.

This remains the recommended pattern unless the host has designed and tested a multi-worker claim or partition strategy.

### 2. Partitioned workers

Multiple workers can be safe when each worker owns a disjoint outbox partition. Partitions can be based on tenant, region, workload, provider path, shard, or another host-owned routing key.

Partitioning must be enforced in the durable selection query or storage adapter. Merely giving workers different names is not enough if they still read the same pending rows.

### 3. Default package claim leases before provider emission

The drain claims work before calling the provider by default when the configured store implements `IGovernanceOutboxClaimStore`. Both shipped stores implement that contract. Setting `UseClaimLeases = false` is an explicit compatibility opt-out and should be used only when the host has another coordination or idempotency strategy appropriate to its deployment.

The default already sets `UseClaimLeases` to `true`; the following configuration makes that posture explicit while assigning a host-controlled worker identifier and lease duration:

```csharp
builder.Services.Configure<GovernanceOutboxOptions>(options =>
{
    options.UseClaimLeases = true;
    options.ClaimWorkerId = "worker-1";
    options.ClaimLeaseDuration = TimeSpan.FromMinutes(5);
});
```

When claim leases are enabled, the drain:

- claims pending work before provider emission;
- claims retry-ready work before provider emission;
- emits only claimed entries;
- completes delivered, deferred, failed, retryable, or dead-letter transitions only when the claim token still matches;
- allows expired claims to be reclaimed by another worker.

Hosts using EF Core must add the claim columns and indexes to their host-owned migration before running the default claim-lease path in production. A host that cannot deploy that schema must explicitly set `UseClaimLeases = false` and accept the duplicate-emission risk of the non-claiming path.

### 4. Provider-side idempotency

Even with a claim strategy, downstream providers should be treated as at-least-once targets unless the provider and host have a verified exactly-once model.

Use stable identifiers where available:

- `GovernanceEmissionEnvelope.EnvelopeId`;
- `GovernanceOutboxEntry.OutboxEntryId`;
- source event or decision receipt identifiers;
- provider idempotency keys, when the provider supports them;
- provider record IDs returned after delivery.

Provider idempotency is especially important for retry, recovery, replay, lease expiration, and manual re-drain operations.

## EF Core claim guarantees by provider

`EfCoreGovernanceOutboxStore` claims a batch with one set-based `UPDATE`. A subquery chooses and orders the candidate rows, and the update restates the eligibility conditions (status, retry time, and no active lease) against each row it writes.

The restated conditions are what make overlapping claims safe. A claim statement chooses its candidates before it can lock them, so two workers starting together can choose the same rows. The second worker waits on the first worker's row locks and resumes after the first commits. At that point the database checks the update's own conditions against the row as it now stands, sees an active lease, and skips it. Without the restated conditions, the second worker could resume with candidates it read before the first claim was visible, overwrite the first worker's claim token, and leave both workers holding the same entries. Issue #823 records this hazard and its verification.

| Provider | Claim acquisition | Evidence |
| --- | --- | --- |
| SQL Server, `READ_COMMITTED_SNAPSHOT OFF` | Cooperating workers do not receive the same active claim. A worker that loses rows while waiting receives a smaller or empty batch. | Opt-in provider contention tests, run in CI by the `EF Core provider contention` job. |
| SQL Server, `READ_COMMITTED_SNAPSHOT ON` (the Azure SQL Database default) | Same as above. | Same tests, run against a database with the snapshot setting enabled. |
| PostgreSQL, read committed | Same as above. | Same tests. |
| SQLite | The database serializes writers, so claim statements cannot overlap. | The SQLite claim tests in the EF Core test project. |
| Other EF Core relational providers | Not verified. The claim relies on the database rechecking an update's own conditions after waiting on a row lock, which a provider may not do. | None. Run a single active worker or partition workers until the host has verified the provider with the same kind of contention test. |

These guarantees cover claim acquisition only. Workers do not skip locked rows, so under heavy contention a worker can wait for another worker's claim statement to finish and then find fewer rows than it asked for. Delivery remains at-least-once: a lease can expire while its worker is still emitting, and a reclaimed entry can be emitted again.

On SQL Server, two claim statements that lock rows in different orders can occasionally deadlock. SQL Server rolls back one of them, and that claim call fails with a deadlock error and claims nothing. Neither worker receives an overlapping claim, and the entries stay eligible for the next pass.

See [EF Core Outbox Concurrency Validation](../quality/ef-core-outbox-concurrency-validation.md#real-provider-claim-contention-issue-823) for the test scenarios and how to run them locally.

## Provider-specific SQL patterns

Provider-specific locking and skip-locked semantics may still suit high-throughput deployments better, because they let competing workers take different rows instead of waiting for each other.

Examples that hosts may evaluate in their own infrastructure include:

- PostgreSQL-style `SELECT ... FOR UPDATE SKIP LOCKED`;
- SQL Server patterns using `UPDLOCK`, `READPAST`, and appropriate transaction isolation;
- database-specific atomic `UPDATE ... OUTPUT` / `RETURNING` claim statements;
- cloud queue visibility timeouts or lease-based message claims.

These patterns are useful, but they are not provider-neutral. They also require testing with the host's actual database provider, isolation level, indexes, retry policy, and migration process.

## Worker configuration guidance

`AddAsiBackboneGovernanceOutboxDrainWorker` should be treated as an operational registration. In multi-replica applications, do not assume it becomes singleton across replicas.

Recommended single-worker configuration posture:

```csharp
builder.Services.Configure<GovernanceOutboxDrainWorkerOptions>(options =>
{
    options.Enabled = builder.Configuration.GetValue<bool>("AsiBackbone:OutboxDrain:Enabled");
    options.BatchSize = 100;
    options.PollingInterval = TimeSpan.FromSeconds(30);
});
```

Then set `AsiBackbone:OutboxDrain:Enabled` to `true` only for the selected worker role or selected partition owner.

Recommended claim-capable configuration posture:

```csharp
builder.Services.Configure<GovernanceOutboxOptions>(options =>
{
    options.UseClaimLeases = true;
    options.ClaimWorkerId = builder.Configuration["AsiBackbone:OutboxDrain:WorkerId"];
    options.ClaimLeaseDuration = TimeSpan.FromMinutes(5);
});
```

Ensure the configured worker ID is stable enough to diagnose ownership but unique enough to distinguish concurrent workers.

## Operational checks

Hosts should monitor for signals that may indicate accidental duplicate workers or unsafe claim behavior:

- more worker heartbeats than expected for a partition;
- duplicate provider records with the same envelope or outbox entry identifier;
- repeated EF Core concurrency exceptions during outbox state transitions;
- provider throttling caused by duplicate drain attempts;
- delivered records without the expected provider record IDs;
- rising retry/dead-letter counts after a scale-out event;
- claims that remain active until expiration without completion;
- frequent claim reclaims that may indicate worker crashes, too-short leases, or slow provider calls.

If duplicate workers are discovered, disable extra workers first, then inspect provider-side duplicates and outbox state before replaying records.

## Wording boundary

Do not describe the provider-neutral outbox drain as exactly-once delivery.

A safer description is:

> AsiBackbone provides durable local outbox records, provider-neutral drain primitives, and claim/lease coordination enabled by default for cooperating workers. Hosts that explicitly opt out of claim leases must provide another coordination or idempotency strategy appropriate to their deployment. Delivery remains at-least-once, so provider-side idempotency is still required where duplicates are consequential.

This keeps the package boundary clear and avoids overstating delivery guarantees.

## Continuing design considerations

Claim/lease support is now an implemented baseline rather than only a future design item, but some questions remain host- or provider-specific:

- whether the provider-neutral EF Core claim path, which waits rather than skipping locked rows, is sufficient for a given production workload;
- how to avoid breaking existing host-owned migrations and deployed schemas;
- how provider idempotency keys should flow into downstream emitters;
- how tests should model real database concurrency beyond in-memory concurrency;
- whether later operational evidence justifies provider-specific claim packages or a new provider-neutral `Claimed` or `InProgress` status.

Production multi-replica hosts should use the default claim/lease behavior with provider-side idempotency unless they deliberately choose one active worker, partitioned workers, or another coordination strategy and explicitly opt out of claim leases.
