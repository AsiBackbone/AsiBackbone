# Outbox Claim and Lease Design Record

This design record captures the selected direction for multi-worker governance outbox claim and lease support.

Issue: [#407](https://github.com/AsiBackbone/AsiBackbone/issues/407), implemented baseline for [#464](https://github.com/AsiBackbone/AsiBackbone/issues/464)

Status: **Accepted design direction; initial provider-neutral claim contracts and opt-in drain/store support implemented.** Provider-specific stronger atomic claim patterns remain host/provider-owned.

## Context

The provider-neutral governance outbox drain can read pending or retry-ready entries, emit each envelope to a provider, and then save the resulting delivered, deferred, failed, retryable-failure, or dead-lettered state.

That default behavior remains appropriate for a single active worker or a host that partitions workers so each worker reads a disjoint durable outbox slice. It is not sufficient by itself for multiple workers reading the same durable outbox rows.

The risk is duplicate provider emission:

```text
worker A selects row 123
worker B selects row 123
worker A emits to provider
worker B emits to provider
worker A saves delivered
worker B may fail on concurrency or stale-state checks
```

Optimistic concurrency can protect the persisted row from some conflicting final-state writes, but it cannot undo a provider call that already occurred. A claim-before-emit strategy is therefore a separate reliability concern from final-state concurrency protection.

## Decision

Claim and lease support is treated as an explicit durable outbox capability, not as a silent behavior change to the existing `FindPendingAsync`, `FindRetryReadyAsync`, or default drain APIs.

The implemented baseline direction is:

1. Keep current single-worker selection APIs supported.
2. Add opt-in Core claim/lease contracts so the concept remains provider-neutral.
3. Add an opt-in drain path that uses claim leases only when `AsiBackboneGovernanceOutboxOptions.UseClaimLeases` is enabled.
4. Implement baseline claim/lease behavior in the in-memory and EF Core stores.
5. Continue requiring provider-side idempotency guidance because claim/lease reduces duplicate selection risk but does not create universal exactly-once delivery.

This preserves existing package behavior while creating a path for scaled durable drains.

## Implemented contract shape

The claim-capable contract is additive. It does not replace `IAsiBackboneGovernanceOutboxStore` for hosts that only need local tests, samples, or one active worker.

```csharp
public interface IAsiBackboneGovernanceOutboxClaimStore : IAsiBackboneGovernanceOutboxStore
{
    ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimPendingAsync(
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimRetryReadyAsync(
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken = default);

    ValueTask<GovernanceOutboxEntry> MarkClaimDeliveredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionResult result,
        CancellationToken cancellationToken = default);

    ValueTask<GovernanceOutboxEntry> MarkClaimFailedAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        DateTimeOffset? nextRetryUtc = null,
        CancellationToken cancellationToken = default);

    ValueTask<GovernanceOutboxEntry> MarkClaimDeadLetteredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        string? deadLetterReason = null,
        CancellationToken cancellationToken = default);

    ValueTask<GovernanceOutboxEntry> SaveClaimAsync(
        GovernanceOutboxClaim claim,
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken = default);

    ValueTask<GovernanceOutboxEntry?> ReleaseClaimAsync(
        GovernanceOutboxClaim claim,
        string? reason = null,
        CancellationToken cancellationToken = default);
}
```

The important design boundary remains that claim operations are explicit. A host should know when it has opted into a claim-capable durable path.

## Claim model fields

Claim support uses separate claim fields rather than a new provider-neutral emission status:

| Field | Purpose |
| --- | --- |
| `ClaimOwner` | Stable worker, process, node, or partition owner identifier. |
| `ClaimedUtc` | UTC timestamp when the claim was acquired. |
| `ClaimExpiresUtc` | UTC timestamp when the lease expires and another worker may reclaim. |
| `ClaimToken` | Opaque value used to verify the same claim owner during final state transition. |
| `ClaimAttemptCount` | Operational counter for repeated claims/reclaims. |

`ClaimToken` is preferred over relying only on `ClaimOwner` because worker names can be reused across process restarts. A token allows finalization to prove that the completing worker still holds the same lease instance.

## Status model decision

Do **not** add a provider-neutral `Claimed` or `InProgress` status as the first implementation step.

The current `GovernanceEmissionStatus` values describe provider-neutral emission state: pending, delivered, deferred, failed, retryable failure, and dead-lettered. A claim is a lease over a row, not necessarily an emission state. Keeping claim as separate metadata avoids broadening status semantics and reduces status migration/API churn.

## Claim lifecycle

The expected claim lifecycle is:

```text
select eligible pending/retry-ready row
  -> atomically write claim owner, claim token, claimed time, and expiration
  -> emit to provider only after claim succeeds
  -> complete final transition only when claim token still matches
  -> clear claim fields on delivered/deferred/failed/retryable/dead-letter transition
```

A worker that cannot obtain a claim must not emit that entry.

If a worker crashes after claiming but before completing, the row becomes eligible again after `ClaimExpiresUtc`. The reclaiming worker must still treat downstream provider emission as at-least-once unless the provider accepts and enforces an idempotency key.

## EF Core implementation direction

The EF Core store implements a baseline provider-neutral claim path using optimistic concurrency:

1. Query eligible unclaimed or expired rows.
2. Attempt to update claim fields with the current tracked row and concurrency token.
3. Return only rows whose claim update succeeded.
4. Complete final state transitions only when the claim owner/token still matches.

This reduces duplicate provider calls when workers respect the claim result and emit only after claim success. It is intentionally not advertised as a universal exactly-once guarantee.

Provider-specific implementations may be better for high-throughput drains:

| Provider family | Possible host/provider-specific pattern |
| --- | --- |
| SQL Server | `UPDATE TOP (...) ... OUTPUT` with `UPDLOCK`, `READPAST`, and appropriate transaction isolation. |
| PostgreSQL | `SELECT ... FOR UPDATE SKIP LOCKED` or `UPDATE ... RETURNING` patterns. |
| Cloud queues | Visibility timeout or native lease semantics. |

These stronger patterns are intentionally provider-specific. They should live in provider-specific packages or host-owned adapters unless a portable EF Core implementation is proven sufficient for the host's workload.

## Completion and ownership verification

Claim completion verifies the claim owner/token before writing delivered, failed, retryable, deferred, or dead-lettered state.

A final transition should fail or no-op when:

* the claim token does not match;
* the claim has expired and another worker has reclaimed the row;
* the row is already terminal;
* the row is not in a status that can be completed by the claim path.

This prevents an old worker from overwriting a newer worker's outcome after a lease expiration/reclaim race.

## Delivery guarantees vocabulary

Documentation and API naming should keep the following terms distinct:

| Term | Meaning |
| --- | --- |
| At-least-once delivery | The same provider emission may be attempted more than once. Hosts and providers must tolerate duplicates. |
| Idempotent delivery | Duplicate attempts use stable identifiers so the provider or host can collapse duplicates into one effective outcome. |
| Claim/lease | Workers coordinate selection so only a claim holder should emit a given row during a lease window. |
| Exactly-once delivery | A full end-to-end guarantee that a provider observes one and only one effective delivery. AsiBackbone should not claim this generically. |

Claim/lease support can reduce duplicate selection risk. It does not by itself prove exactly-once delivery because provider behavior, network failure, transaction scope, and replay/recovery policy remain outside the package boundary.

## Compatibility and migration boundaries

Claim support is introduced as a backward-compatible additive feature where possible:

* Existing single-worker drains continue to use the current APIs by default.
* New claim-capable APIs are opt-in.
* EF Core claim fields require an explicit host migration.
* Hosts that do not add claim columns should continue to use the existing durable outbox behavior until they opt into claim leases.
* Release notes should identify claim support as a minor release feature unless a breaking schema/API decision is made.

The package does not silently change an existing host's deployed database without a host-owned migration.

## Documentation outcome

With the initial claim/lease support available, the recommended production guidance is:

* use one active worker per durable outbox partition when simple operations are preferred;
* use disjoint partitions when running multiple workers without claim leases;
* enable the claim-capable drain path only when the configured store implements `IAsiBackboneGovernanceOutboxClaimStore` and the host has applied any required schema migration;
* use provider-side idempotency keys wherever the downstream provider supports them.

This design record records the implemented baseline without overstating runtime guarantees.
