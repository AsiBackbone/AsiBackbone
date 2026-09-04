# EF Core Outbox Non-Claim Concurrency Ownership

This article defines the concurrency contract for non-claim mutation methods on the EF Core governance outbox store.

## Decision

The existing propagation behavior is intentional.

The following non-claim mutation methods do not catch, translate, or automatically retry `DbUpdateConcurrencyException`:

- `SaveAsync(...)`
- `MarkDeliveredAsync(...)`
- `MarkFailedAsync(...)`
- `MarkDeadLetteredAsync(...)`

When one of these methods loses an optimistic-concurrency race, the original EF Core exception is allowed to propagate to the caller. The caller owns conflict handling, retry selection, durable-state reload, and any decision to reapply a transition.

This boundary avoids silently reporting another writer's durable state as though it were applied by the current invocation.

## Terminal entries

The four non-claim mutation methods return an existing `Delivered` or `DeadLettered` entry unchanged when the entry loaded for the mutation is terminal. They do not replace its provider evidence, error details, timestamps, or concurrency stamp. In particular, saving a stale deferred snapshot cannot reopen a terminal entry loaded by `SaveAsync`.

If another writer completes the entry after the mutation loads a nonterminal row, or the host context already tracks a stale nonterminal row, an attempted update still uses EF Core optimistic concurrency and propagates `DbUpdateConcurrencyException`. The host remains responsible for reloading durable state after that conflict. This protection does not prevent duplicate provider calls made before persistence by non-claim workers.

## Why there is no hidden retry

A generic retry inside the store would not know whether the requested transition is still correct or idempotent after another writer wins.

For example:

```text
worker A reads pending row
worker B reads pending row
worker B saves failed
worker A attempts delivered
worker A receives DbUpdateConcurrencyException
```

Automatically reloading and reapplying worker A's delivered transition could overwrite newer durable evidence. Returning worker B's failed row as worker A's success would also misattribute write ownership.

The store therefore preserves the original exception and leaves reconciliation to the host.

## Caller responsibilities

A host using non-claim mutations should:

1. Catch `DbUpdateConcurrencyException` at the application boundary where retry and idempotency policy are known.
2. Reload the durable outbox entry using a fresh context or cleared change tracker.
3. Decide whether the durable winner is acceptable, whether the requested transition remains valid, or whether the operation should be abandoned.
4. Retry only when the transition is still safe and the host can prove the retry is idempotent.
5. Preserve provider-side idempotency keys because a provider call may already have occurred before the final outbox update lost its race.

A basic host-owned pattern is:

```csharp
try
{
    await store.MarkDeliveredAsync(outboxEntryId, result, cancellationToken);
}
catch (DbUpdateConcurrencyException)
{
    GovernanceOutboxEntry? durableEntry = await store.FindByOutboxEntryIdAsync(
        outboxEntryId,
        cancellationToken);

    // Apply host-owned reconciliation and idempotency policy here.
    throw;
}
```

The example deliberately rethrows after inspection. A production host should replace that decision with an explicit policy rather than an unconditional retry.

## Claim-aware path for competing workers

Scaled or competing workers should prefer the claim-capable path:

- enable `AsiBackboneGovernanceOutboxOptions.UseClaimLeases`;
- use an `IAsiBackboneGovernanceOutboxClaimStore`;
- use `IAsiBackboneGovernanceOutboxClaimOutcomeStore` when the caller must know whether its own invocation applied the transition.

The outcome-aware contract distinguishes:

- `Applied`
- `StaleClaim`
- `Terminal`
- `ConcurrencyLost`
- `Missing`

This is the recommended path when write ownership must be observable. Claim leasing reduces duplicate selection among cooperating workers, but downstream delivery remains at-least-once unless the host and provider enforce idempotency.

## Contract boundary

Non-claim methods remain useful for:

- one active worker per durable outbox partition;
- host-owned administrative transitions;
- controlled local or single-process workflows;
- applications that already have an explicit concurrency-resolution policy.

They should not be treated as a multi-worker ownership protocol.

The package does not introduce broad automatic retries because the store cannot infer business idempotency, provider side effects, or whether a newer durable transition should win.

## Verification

Deterministic relational tests verify that:

- `SaveAsync(...)` propagates the original `DbUpdateConcurrencyException`;
- `MarkDeliveredAsync(...)` propagates the original exception without hidden retry;
- `MarkFailedAsync(...)` propagates the original exception without hidden retry;
- the concurrent durable winner remains persisted after the losing call fails.

See:

```text
tests/AsiBackbone.EntityFrameworkCore.Tests/Outbox/EfCoreGovernanceOutboxNonClaimConcurrencyTests.cs
```
