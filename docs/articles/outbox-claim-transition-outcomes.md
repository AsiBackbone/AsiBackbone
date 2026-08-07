# Claimed Outbox Transition Outcomes

Issue: [#590](https://github.com/AsiBackbonecdcavell/AsiBackbone/issues/590)

Status: **Implemented as an additive compatibility-preserving contract.**

## Purpose

A claimed outbox transition has two separate questions:

1. What durable entry exists after the operation?
2. Did this invocation persist the requested transition?

The original claim convenience methods returned only `GovernanceOutboxEntry`. That was safe for recovery because an EF Core concurrency loss reloaded and returned the durable winner, but the returned status alone could not prove that the calling worker wrote it. A losing worker could therefore observe a terminal durable entry and incorrectly attribute that state to its own invocation.

The outcome-aware contract keeps those questions separate.

## Additive API

`IAsiBackboneGovernanceOutboxClaimOutcomeStore` extends the existing claim-capable store and adds:

```csharp
ValueTask<GovernanceOutboxClaimTransitionResult> TryMarkClaimDeliveredAsync(...);
ValueTask<GovernanceOutboxClaimTransitionResult> TryMarkClaimFailedAsync(...);
ValueTask<GovernanceOutboxClaimTransitionResult> TryMarkClaimDeadLetteredAsync(...);
ValueTask<GovernanceOutboxClaimTransitionResult> TrySaveClaimAsync(...);
```

The existing `MarkClaimDeliveredAsync`, `MarkClaimFailedAsync`, `MarkClaimDeadLetteredAsync`, and `SaveClaimAsync` methods remain available and continue returning the observed durable entry. This avoids a breaking change for existing consumers.

New code that needs to attribute a transition to the current worker should use the `Try...` methods and require `IsApplied == true`.

## Outcome meanings

| Outcome | Meaning | Entry returned |
| --- | --- | --- |
| `Applied` | This invocation completed the scoped EF Core save that persisted the requested transition. | The caller-owned updated entry. |
| `StaleClaim` | The row exists, is nonterminal, but the supplied owner/token no longer matches. No write is attempted. | The current durable entry. |
| `Terminal` | The row was already delivered or dead-lettered before this invocation attempted a write. | The current terminal durable entry. |
| `ConcurrencyLost` | The claim was valid when evaluated, but another durable writer won before this invocation completed its update. | The refreshed durable winner, including a terminal winner when applicable. |
| `Missing` | The row did not exist before the attempt or disappeared during the write race. | The caller's last safe claimed snapshot when no durable row remains. |

A terminal entry returned with `ConcurrencyLost` must not be reclassified as `Applied`. The outcome records write ownership; the entry records durable state.

## EF Core registration

`UseEfCoreGovernanceOutbox<TDbContext>()` registers one scoped `EfCoreGovernanceOutboxOutcomeStore` instance through all compatible contracts:

```text
IAsiBackboneGovernanceOutboxStore
IAsiBackboneGovernanceOutboxClaimStore
IAsiBackboneGovernanceOutboxClaimOutcomeStore
```

The outcome-aware store delegates the existing persistence behavior to `EfCoreGovernanceOutboxStore` and observes the scoped `DbContext` save boundary. This keeps the original store behavior intact while making caller-owned persistence explicit.

## Logging and metrics

The outcome-aware store emits a warning whenever a claimed transition is not applied. The message includes:

- worker ID;
- outbox entry ID;
- explicit transition outcome;
- durable status.

It also increments the `asibackbone.outbox.claim_transition_attempts` counter with `outcome` and `durable_status` tags. Hosts can use that counter to distinguish successful caller-owned transitions from stale claims, concurrency losses, terminal no-ops, and missing rows.

The hosted drain continues using the compatibility methods, but when the registered EF Core store is outcome-aware those methods internally use the explicit result path. A losing drain worker therefore produces a non-applied diagnostic and metric rather than silently treating the durable winner as proof of its own write.

## Missing and terminal behavior

Missing rows do not produce a fabricated durable state. The result carries the last safe claimed snapshot only so the caller retains the entry identifier, owner, token, and attempted context. `Outcome == Missing` is authoritative.

Terminal rows observed before an attempted write return `Terminal`. Terminal rows created by a concurrent winner after the caller validated its claim return `ConcurrencyLost`. This distinction identifies whether the no-op was already known before the write boundary or resulted from a race.

## Compatibility guidance

Existing consumers may continue calling the original convenience methods when they only need the final durable entry.

Consumers that log delivery ownership, increment worker-success metrics, acknowledge downstream completion, or make another consequential decision must prefer the outcome-aware interface:

```csharp
if (store is IAsiBackboneGovernanceOutboxClaimOutcomeStore outcomeStore)
{
    GovernanceOutboxClaimTransitionResult transition =
        await outcomeStore.TryMarkClaimDeliveredAsync(claim, emissionResult, cancellationToken);

    if (!transition.IsApplied)
    {
        // Do not attribute the durable status to this worker.
    }
}
```

The new interface and result types are additive. No existing method signature or package ID changes.
