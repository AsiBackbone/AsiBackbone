# Audit Integrity Chain Model

This article documents the selected provider-neutral append-only audit integrity model for AsiBackbone audit and outbox records.

AsiBackbone provides canonical hashing, signing, verification, and audit-integrity seams. It does not provide immutable storage, external anchoring, blockchain, transparency logs, legal evidence guarantees, compliance certification, or tamper-evidence by itself.

> [!IMPORTANT]
> A local hash chain can detect changed, missing, reordered, or forked records when verification is run against retained chain metadata. It is not automatically tamper-evident unless the deployed system also includes durable append-only storage controls, verification, monitoring, retention, and any required external anchoring.

## Selected model

The first Core model uses a **per-chain append-only hash chain**.

The model is intentionally simpler than a Merkle tree or external anchoring provider:

```text
record canonical payload
  -> record hash
  -> integrity link payload
  -> link hash
  -> next link stores previous link hash
```

Each link binds:

- chain ID;
- one-based sequence number;
- record ID;
- record type;
- record hash;
- previous link hash;
- link hash;
- hash algorithm;
- canonicalization version;
- schema version;
- created timestamp.

This gives Core a provider-neutral way to express sequence continuity while leaving storage, anchoring, and operational assurance to host applications.

## Why not Merkle first?

| Model | Strength | Tradeoff | Selected now? |
| --- | --- | --- | --- |
| Previous-record hash chain | Simple, ordered, easy to verify sequentially. | Verification is linear and chain tips must be preserved. | Yes. |
| Per-stream hash chain | Supports independent tenant/provider/day chains. | Requires careful chain ID strategy. | Yes, via `ChainId`. |
| Merkle root per batch | Efficient batch proofs and external anchoring. | More structure and proof APIs are needed. | Deferred. |
| Rolling batch root | Good for periodic anchoring. | Requires batch lifecycle and root persistence. | Deferred. |
| Blockchain or transparency log | Stronger independent anchoring when properly operated. | Provider-specific, operationally heavy, not Core-neutral. | Non-goal by default. |

The selected model does not block Merkle or batch-root support later. A future provider package or extension can build batch roots over `AuditIntegrityLink.LinkHash` values and anchor those roots externally.

## Core API shape

`AuditIntegrityLink` represents one chain link. Use it with hashes produced from canonical audit or outbox artifacts.

```csharp
CanonicalPayloadHash recordHash = CanonicalPayloadHasher.ComputeHash(recordPayload);

AuditIntegrityLink first = AuditIntegrityLink.CreateGenesis(
    "audit-ledger:tenant-a:2026-06-16",
    recordHash,
    DateTimeOffset.UtcNow);

AuditIntegrityLink next = AuditIntegrityLink.Append(
    first,
    nextRecordHash,
    DateTimeOffset.UtcNow);
```

Persist the link metadata next to the durable audit ledger row, outbox row, lifecycle row, or separate integrity table. Core does not prescribe the storage schema.

## Verification behavior

`AuditIntegrityVerifier.Verify(...)` verifies a supplied ordered chain.

It detects:

| Condition | Category | Failure code |
| --- | --- | --- |
| No links supplied | `EmptyChain` | `integrity.chain-empty` |
| Sequence jump | `MissingRecord` | `integrity.sequence-missing` |
| Sequence moves backward | `ReorderedRecord` | `integrity.sequence-reordered` |
| Duplicate sequence | `ForkedChain` | `integrity.sequence-duplicate` |
| Wrong chain ID | `WrongChain` | `integrity.chain-id-mismatch` |
| Previous hash does not match prior link | `HashMismatch` | `integrity.previous-link-hash-mismatch` |
| Genesis link has previous hash | `HashMismatch` | `integrity.genesis-previous-hash-present` |
| Link hash no longer matches canonical link fields | `ModifiedRecord` | `integrity.link-hash-mismatch` |
| Unsupported hash algorithm | `UnsupportedAlgorithm` | `integrity.hash-algorithm-unsupported` |

Example:

```csharp
AuditIntegrityVerificationResult result = AuditIntegrityVerifier.Verify(
    linksInSequence,
    expectedChainId: "audit-ledger:tenant-a:2026-06-16");

if (!result.IsValid)
{
    // Route result.Category, result.FailureCode, and result.SafeMetadata
    // through host audit review, alerting, or dead-letter policy.
}
```

## Persistence interaction

Recommended write order when chaining audit ledger records:

```text
Build audit ledger record
  -> canonicalize record
  -> hash record
  -> load previous chain tip for chain ID
  -> create genesis or append integrity link
  -> persist record and integrity link in one durable unit when possible
  -> update chain tip only after append succeeds
```

Recommended write order when chaining outbox entries:

```text
Build governance outbox entry
  -> canonicalize outbox entry
  -> hash outbox entry
  -> load previous chain tip for outbox chain ID
  -> append integrity link
  -> persist outbox row and integrity link
  -> drain outbox through provider after local append succeeds
```

Outbox entries are stateful. Status changes may require new canonical outbox hashes and new integrity links. Hosts should decide whether to chain only initial enqueue, every state transition, or separate lifecycle records.

## Chain ID strategy

Use chain IDs that match review and retention boundaries.

Examples:

- `audit-ledger:global`;
- `audit-ledger:tenant:{tenantHash}`;
- `audit-ledger:tenant:{tenantHash}:date:2026-06-16`;
- `outbox:{providerName}:date:2026-06-16`;
- `lifecycle:{auditResidueId}`.

Shorter chains are easier to verify and repair operationally. Longer chains provide stronger continuity but require careful retention and tip management.

## External anchoring remains optional

External anchoring can strengthen evidence that a chain tip existed at a point in time, but it is not part of Core by default.

Possible future anchoring options:

- write chain tips to immutable object storage;
- publish batch roots to an internal transparency log;
- timestamp daily roots with a trusted timestamp authority;
- anchor Merkle batch roots through a provider package;
- export signed root summaries to a separate governance system.

When anchoring is used, store anchoring records separately from the local chain. Do not overwrite the original link metadata.

## Safe wording

Safe wording:

- "The audit record participates in a local append-only hash chain."
- "Verification detected a missing sequence."
- "The chain verified for the supplied records and expected chain ID."
- "The chain can support later external anchoring."
- "Tamper-evidence requires verification plus storage and retention controls."

Avoid wording such as:

- "The audit trail is tamper-proof."
- "A local hash chain is immutable."
- "Chaining alone proves legal non-repudiation."
- "This is blockchain-backed."
- "External anchoring is enabled by default."

Use **tamper-evident** only when the deployed system includes signed/chained records, verification, durable append-only storage controls, monitoring, retention, and any required external anchoring.

## Non-goals

The current implementation does not include:

- blockchain storage;
- external timestamp authorities;
- transparency-log providers;
- Merkle proof APIs;
- automatic immutable storage configuration;
- legal evidence certification;
- provider-specific database schemas or migrations.
