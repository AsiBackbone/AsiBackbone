# Host Mutation Accountability

AsiBackbone records **why** a protected operation was allowed, denied, deferred, escalated, or required acknowledgment. The consuming host remains responsible for **what** business state changed and whether its transaction committed.

An `Allowed` decision is therefore not proof that execution started, completed, or persisted anything.

## Accountability chain

```text
AuditResidue / AuditLedgerRecord
        -> operation execution identity
        -> host-owned mutation audit batch
        -> AuditResidueLifecycleEvent completion record
```

Each record remains authoritative for a separate question:

| Record | Authoritative question |
| --- | --- |
| AsiBackbone decision record | Why was the operation allowed or denied? |
| Host mutation audit batch | What persisted application state changed? |
| AsiBackbone lifecycle event | Did execution complete, and which host batch resulted? |

## Framework-neutral receipt

`GovernedOperationExecutionReceipt` represents one completed execution attempt. It supports:

- `Committed`, with an opaque mutation batch ID, record count, canonical manifest hash, and hash algorithm;
- `Failed`, with no claim that a mutation committed;
- `RolledBack`, with no committed mutation binding;
- `CompletedWithoutMutation`, for successful work that changed no persisted state.

The logical `OperationExecutionId` remains stable across retries. `ExecutionAttemptId` distinguishes individual attempts.

```csharp
GovernedOperationExecutionReceipt receipt =
    GovernedOperationExecutionReceipt.Create(
        operationExecutionId: operationExecutionId,
        persistenceOutcome: GovernedOperationPersistenceOutcome.Committed,
        executionAttemptId: executionAttemptId,
        mutationBatchId: hostResult.MutationBatchId,
        mutationRecordCount: hostResult.RecordCount,
        mutationManifestHash: hostResult.ManifestHash,
        mutationManifestAlgorithm: "SHA-256",
        persistenceProvider: "relational",
        decisionAuditRecordId: decisionRecord.RecordId);

AuditResidueLifecycleEvent completed =
    HostAccountabilityLifecycleEvent.FromExecutionReceipt(
        residue,
        receipt);

await lifecycleStore.AppendAsync(completed, cancellationToken);
```

The helper uses the existing `GatewayExecutionCompleted` lifecycle stage. Outcome distinctions remain in the typed receipt and stable metadata rather than expanding the lifecycle enum.

## Recommended host sequence

1. Evaluate policy and persist the decision `AuditResidue` or `AuditLedgerRecord`.
2. Create a stable operation execution ID and an attempt ID.
3. Append `HostAccountabilityLifecycleEvent.ExecutionStarted(...)`.
4. Begin the host-owned transaction and mutation-audit scope.
5. Apply the host's redaction, minimization, and audit-value protection policy.
6. Commit or roll back the host transaction.
7. Create a `GovernedOperationExecutionReceipt` from the host result.
8. Append the completion lifecycle event locally.
9. Optionally place the lifecycle artifact into the configured governance outbox or signing/integrity process.

## Mutation-manifest binding

AsiBackbone does not ingest entity snapshots or raw original/current values. The host should:

1. reduce mutation records according to its privacy and retention policy;
2. deterministically order a privacy-safe manifest;
3. canonicalize and hash that manifest;
4. store the complete mutation records and manifest in the host-owned audit store;
5. place only the opaque batch ID, record count, algorithm, and hash in the AsiBackbone receipt;
6. sign or chain the receipt or lifecycle event only when the configured signing/integrity process actually includes that artifact.

A hash proves correspondence only to the canonical manifest the host retained. It does not by itself prove transaction atomicity, database durability, or legal compliance.

## Failure semantics

| Outcome | Decision record | Host mutation records | Lifecycle evidence |
| --- | --- | --- | --- |
| Denied | Yes | None | Decision denial; execution does not start |
| Allowed, never executed | Yes | None | No start event, or host-defined abandonment evidence |
| Allowed, completed without mutation | Yes | None | `CompletedWithoutMutation` receipt |
| Allowed, persistence committed | Yes | Yes | `Committed` receipt with batch binding |
| Allowed, persistence failed | Yes | None committed | `Failed` receipt |
| Allowed, persistence rolled back | Yes | None committed | `RolledBack` receipt |
| Downstream emission failed | Local records remain | Local records remain | Existing outbox retry/failure lifecycle |

## Metadata and privacy boundary

`HostAccountabilityMetadataKeys` defines stable keys for local audit and downstream governance emission. Keep values minimized. Do not place raw entity values, request bodies, secrets, credentials, personal data, or unrestricted exception details into receipt or lifecycle metadata.

Canonical receipt payloads are available through `GovernedOperationExecutionReceiptCanonicalPayload.Create(...)` and can be hashed with the existing `CanonicalPayloadHasher`. Metadata remains excluded unless explicitly allow-listed through `CanonicalPayloadOptions`.

## NCAT companion integration

NetCoreApplicationTemplate issue [#365](https://github.com/AsiBackbone/NetCoreApplicationTemplate/issues/365) owns the NCAT-specific mutation-audit context, mutation batch identifier, value-protection policy, transaction atomicity, and mutation-batch completion result. AsiBackbone remains independent of NCAT, EF Core, ASP.NET Core, and any persistence provider.
