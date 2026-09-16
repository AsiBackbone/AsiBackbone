# Optional NCAT Audit-Completion Adapter

The `samples/NcatAuditCompletionAdapter` project is a reference integration that maps an NCAT mutation-audit completion into AsiBackbone governed execution lifecycle evidence.

The adapter is intentionally outside every required AsiBackbone package. It references `AsiBackbone.Core`, but `AsiBackbone.Core` does not reference NCAT. NCAT likewise remains independently installable and does not require AsiBackbone assemblies, configuration, migrations, or services.

## Authority boundary

| Component | Authoritative responsibility |
| --- | --- |
| NCAT | Application mutation details, transaction outcome, mutation batch, privacy-safe canonical manifest, and completion-outbox delivery state |
| AsiBackbone | Policy decision evidence, governed execution receipt, lifecycle evidence, optional signing, and outbox delivery |
| Optional adapter | Translation, correlation validation, idempotent lifecycle append, and normalized handoff result |

The adapter does not create a distributed transaction and does not claim exactly-once delivery. It uses an at-least-once handoff with a deterministic lifecycle event identifier so duplicate attempts can be detected safely.

## Independent installation

An application may use either product without the other:

```text
NCAT host only
    -> NCAT mutation records and completion receipt/outbox

AsiBackbone host only
    -> policy decisions and governed lifecycle records

Combined host
    -> optional translation at the application composition boundary
```

The reference sample uses a source-neutral `NcatAuditCompletionHandoff` instead of a compile-time NCAT type. A combined host translates its current NCAT completion receipt or completion-outbox entry into this minimized contract.

## Required handoff fields

Every handoff requires:

- a stable NCAT completion entry identifier used as the idempotency key;
- the logical operation execution identifier;
- the persisted AsiBackbone decision audit record identifier;
- the NCAT persistence outcome;
- the completion timestamp.

Correlation ID, trace ID, and execution attempt ID should be supplied whenever available. The adapter rejects a supplied correlation or trace identifier that conflicts with the resolved decision receipt.

A committed outcome additionally requires:

- mutation batch identifier;
- audit record count greater than zero;
- canonical privacy-safe mutation manifest hash;
- manifest hash algorithm.

Failed, rolled-back, and completed-without-mutation outcomes cannot claim committed mutation evidence.

## Outcome mapping

| NCAT handoff outcome | AsiBackbone outcome |
| --- | --- |
| `committed` | `Committed` |
| `failed` | `Failed` |
| `rolled-back` | `RolledBack` |
| `no-mutation` or `completed-without-mutation` | `CompletedWithoutMutation` |

Outcome matching ignores case and separators. Unknown outcomes are terminal validation failures rather than guessed mappings.

## Combined host registration

The host supplies a decision-receipt resolver and a durable lifecycle store:

```csharp
var adapter = new NcatAuditCompletionAdapter(
    lifecycleStore,
    decisionReceiptResolver,
    new NcatAuditCompletionAdapterOptions
    {
        PersistenceProvider = "NCAT",
        DeadLetterAfterAttempts = 10
    });
```

`INcatDecisionReceiptResolver` is host-owned because the decision receipt may be stored through EF Core, an in-memory development provider, or another repository.

A completion dispatcher can translate the source entry and invoke the adapter:

```csharp
var handoff = new NcatAuditCompletionHandoff(
    CompletionEntryId: completionEntry.Id,
    PersistenceOutcome: completionEntry.Receipt.PersistenceOutcome,
    CompletedUtc: completionEntry.Receipt.CompletedUtc,
    OperationExecutionId: completionEntry.Receipt.OperationExecutionId,
    ExecutionAttemptId: completionEntry.Receipt.ExecutionAttemptId,
    DecisionAuditRecordId: completionEntry.Receipt.DecisionAuditRecordId,
    CorrelationId: completionEntry.Receipt.CorrelationId,
    TraceId: completionEntry.Receipt.TraceId,
    MutationBatchId: completionEntry.Receipt.MutationBatchId,
    AuditRecordCount: completionEntry.Receipt.AuditRecordCount,
    MutationManifestHash: completionEntry.Receipt.MutationManifestHash,
    MutationManifestAlgorithm: completionEntry.Receipt.MutationManifestAlgorithm,
    DeliveryAttempt: completionEntry.AttemptCount);

NcatAuditCompletionDeliveryResult result =
    await adapter.DeliverAsync(handoff, cancellationToken);

if (result.ShouldAcknowledgeSource)
{
    await completionOutbox.MarkDeliveredAsync(completionEntry.Id, cancellationToken);
}
else
{
    await completionOutbox.RecordAttemptAsync(
        completionEntry.Id,
        result.Disposition.ToString(),
        result.ReasonCode,
        cancellationToken);
}
```

The source entry is acknowledged only after the lifecycle event has been durably appended, or when an equivalent lifecycle event already exists.

## Delivery dispositions

| Disposition | Meaning | Acknowledge NCAT source entry? |
| --- | --- | --- |
| `Delivered` | Lifecycle event appended | Yes |
| `Duplicate` | Equivalent event already exists | Yes |
| `Retryable` | Append failed and should be retried | No |
| `Deferred` | Required decision receipt is not available yet | No |
| `Terminal` | Invalid handoff or idempotency conflict | No |
| `DeadLetter` | Configured retry threshold was exhausted | No |

Dead-letter classification does not delete local evidence or claim successful delivery. The host remains responsible for retaining and reconciling the NCAT completion entry.

## Idempotency and conflict handling

The lifecycle event identifier is deterministically derived from the NCAT completion entry identifier. On a retry:

1. the adapter looks up the event before appending;
2. an equivalent event returns `Duplicate`;
3. a different event under the same idempotency key returns `Terminal` with `idempotency-conflict`;
4. append failure remains retryable or dead-lettered according to host configuration.

This protects against duplicate publication while avoiding an exactly-once claim.

## Metadata minimization

The adapter carries only opaque identifiers, counts, hashes, outcome, provider label, and the source completion entry identifier. It does not accept or copy:

- entity keys;
- original or current values;
- request bodies;
- secrets or credentials;
- unrestricted exception messages.

Failure results expose only the exception type name. Detailed diagnostics should remain in the host's protected local logs.

## Related work

- AsiBackbone issue #634 introduced the framework-neutral governed execution receipt and lifecycle helpers.
- NCAT issue #367 owns the privacy-safe canonical mutation manifest and hash.
- NCAT issues #368 through #370 own transaction coordination, completion outbox/dispatch, reconciliation, health checks, and metrics.
