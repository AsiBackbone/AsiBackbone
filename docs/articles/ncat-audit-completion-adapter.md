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

A host that receives NCAT's `ApplicationAuditCompletionMessage` through an `IApplicationAuditCompletionPublisher` can instead model it as `NcatAuditCompletionMessage` and validate it before translation:

```csharp
if (!NcatAuditCompletionContract.TryCreateHandoff(
        message,
        deliveryAttempt,
        out NcatAuditCompletionHandoff? handoff,
        out string? reasonCode))
{
    // Contract violation: report a terminal publish failure to NCAT with reasonCode.
}
```

`TryCreateHandoff` applies version 1 of NCAT's audit-completion contract and uses the message idempotency key as the completion entry identifier. Hosts that retain the canonical manifest text can also call `TryVerifyCanonicalManifest` to confirm that the manifest is a single JSON value and to confirm the batch, record count, schema version, and SHA-256 digest. The contract rules are described in [NCAT contract vectors](#ncat-contract-vectors).

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

## NCAT contract vectors

The adapter models NCAT independently, so both repositories could drift while their own tests stay green. To prevent that, the sample tests replay NCAT's published, machine-readable [audit-completion contract vectors](https://github.com/AsiBackbone/NetCoreApplicationTemplate/blob/main/contracts/audit-completion/README.md). Neither product takes a compile-time dependency on the other.

The vectors are vendored from a pinned NCAT commit:

| File | Purpose |
| --- | --- |
| `tests/AsiBackbone.Samples.NcatAuditCompletionAdapter.Tests/ContractVectors/ncat/v1/audit-completion-vectors.json` | Exact bytes of NCAT's `contracts/audit-completion/v1/audit-completion-vectors.json` at the pinned revision |
| `tests/AsiBackbone.Samples.NcatAuditCompletionAdapter.Tests/ContractVectors/ncat/ncat-contract-pin.json` | NCAT repository, pinned commit SHA, source path, and SHA-256 of the vendored file |

For the AsiBackbone 7.0.0 release candidate, the reviewed commit pin
`8aec7d32438907e73b09f283d4f61221a6b0cedc` remains the immutable release
evidence. The vendored bytes match both NCAT `main` and the NCAT 2.11.0
release-candidate commit `2f16c2aedf0689fce78f756e614d0278fb0c8597`.
Keeping the contract-producing commit, rather than replacing it with a tag that
does not yet exist during release preparation, preserves exact provenance. A
later metadata-only repin to `v2.11.0` is unnecessary unless the published tag
contains different vector bytes, which the drift check would reject.

`NcatContractVectorTests` checks that:

- the vendored file matches the pinned SHA-256;
- the contract version, schema versions, digest algorithm, idempotency prefix, and supported outcomes match `NcatAuditCompletionContract`;
- each valid vector's canonical manifest bytes reproduce its expected digest and its idempotency key is recomputed from the destination and the trimmed mutation batch ID;
- each valid vector's receipt and message agree, pass `TryCreateHandoff`, and deliver with the operation, attempt, decision, correlation, trace, batch, count, algorithm, digest, and timestamp preserved. Vectors without an operation or decision identifier are rejected with the adapter's documented reason codes;
- each published invalid message is rejected with a specific reason code;
- a lowercase manifest digest is rejected by both `TryCreateHandoff` and `TryVerifyCanonicalManifest` with `invalid-manifest-hash`, because the contract encodes digests as uppercase hex;
- the no-mutation, failed, and rolled-back scenarios cannot carry committed batch evidence.

Valid vectors, invalid messages, and no-message scenarios are replayed from the reviewed names in `NcatContractVectorTests`, not discovered from the fixture. A test fails with review guidance when NCAT publishes or removes any of these names, or publishes an outcome or contract major version the adapter does not recognize.

### Drift reporting

The `NCAT Contract Vectors` workflow runs weekly, on demand, and on pull requests that change the vendored vectors. `scripts/Test-NcatContractVectorPin.ps1` confirms that the vendored bytes still match the pinned NCAT revision. It then compares them with NCAT `main` and fails with a summary when NCAT publishes different vectors, removes them, or adds a newer contract major version. If it cannot list NCAT's contract versions, it reports that as a finding rather than passing on an incomplete check. The weekly failure is the signal that the pin needs review; it does not block unrelated pull requests. Pull request runs execute the contributor's copy of the script, so they call the public NCAT endpoints without a GitHub token; only scheduled and manual runs, which maintainers trigger, pass one, to raise the API rate limit.

### Updating the pinned vectors

1. Read the NCAT change and its compatibility classification in NCAT's contract README.
2. Copy `contracts/audit-completion/v1/audit-completion-vectors.json` from the new NCAT commit or release tag without modifying it. `.gitattributes` keeps the vendored JSON as LF, so the bytes match upstream.
3. Update `revision` and `sha256` in `ncat-contract-pin.json`. Use the full commit SHA.
4. Run the sample tests. Extend `NcatAuditCompletionContract` and `NcatContractVectorTests` for any new vector, reason, outcome, or schema version. Add a reviewed vector name to the supported-vector allowlist only after the adapter handles it.
5. For a new contract major version, vendor the new `vN/` directory alongside the existing one until the adapter supports it.
6. Run `./scripts/Test-NcatContractVectorPin.ps1` and confirm it reports no drift.

## Related work

- AsiBackbone issue #634 introduced the framework-neutral governed execution receipt and lifecycle helpers.
- NCAT issue #367 owns the privacy-safe canonical mutation manifest and hash.
- NCAT issues #368 through #370 own transaction coordination, completion outbox/dispatch, reconciliation, health checks, and metrics.
- NCAT issue #580 publishes the versioned audit-completion contract vectors, and AsiBackbone issue #826 replays them against this adapter.
