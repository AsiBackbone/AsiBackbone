# Signed Audit and Outbox Records

Issue: #221, updated for #253.

This article documents the implemented signing flow for audit receipts, audit ledger records, audit residue lifecycle events, governance emission envelopes, and governance outbox entries.

AsiBackbone provides provider-neutral governance primitives and signing seams. It does not provide immutable storage, external anchoring, legal non-repudiation, or tamper-evidence by itself.

> [!IMPORTANT]
> A signed record is not automatically a verified record. A signed record is also not automatically tamper-evident. Verification, hash chaining, durable write controls, immutable/object-lock storage, external anchoring, retention, monitoring, and incident response remain host or provider responsibilities.

## Released signing context

`1.1.0` includes stable Core signing helpers and stable signing-provider package boundaries:

| Surface | Status | Limit |
| --- | --- | --- |
| Core canonical payload and signed-artifact helpers | Stable in `AsiBackbone.Core`. | Produces signing-ready, unsigned, or signed artifact wrappers; does not own key custody or storage controls. |
| Local-development signing provider | Stable package: `AsiBackbone.Signing.LocalDevelopment`. | Test/sample/local validation only. Not production signing. |
| Managed-key signing adapter | Stable package: `AsiBackbone.Signing.ManagedKey`. | Host supplies the concrete managed-key client, credentials, verification path, and operational controls. |
| Future concrete cloud/HSM/KMS packages | Future or host-owned unless separately released. | Not implied by this article. |

## Selected signable artifacts

The Core signing helpers support the following canonical governance artifacts:

| Artifact | Canonical type | Typical signing point |
| --- | --- | --- |
| Audit residue | `asibackbone.audit-residue` | After audit residue creation and canonical hashing, before the host treats the residue as a signed receipt. |
| Audit ledger record | `asibackbone.audit-ledger-record` | After ledger record construction and canonical hashing, before durable append when the persisted row must carry signing metadata. |
| Audit residue lifecycle event | `asibackbone.audit-residue-lifecycle-event` | After lifecycle event creation and canonical hashing, before lifecycle-store append when lifecycle events require signatures. |
| Governance emission envelope | `asibackbone.governance-emission-envelope` | After envelope construction and canonical hashing, before outbox enqueue or provider emission when the envelope itself is the signed artifact. |
| Governance outbox entry | `asibackbone.governance-outbox-entry` | After outbox entry construction and canonical hashing, before provider emission when the durable outbox entry is the signed artifact. |

For issue #221, an **audit receipt** is represented by either a persistence-ready `AuditLedgerRecord` or the provider-neutral `SignedGovernanceArtifact<TArtifact>` wrapper around a canonicalized audit artifact. Hosts may persist the wrapper metadata directly or project it into their own storage model.

## State model

`SignedGovernanceArtifact<TArtifact>` preserves three distinct states:

| State | Meaning | What is present |
| --- | --- | --- |
| Unsigned | The artifact has a canonical payload and canonical hash, but no signing metadata is attached. | Artifact ID, artifact type, canonicalization version, payload schema version, hash algorithm, hash value. |
| Signing-ready | The artifact hash has been projected into `SigningMetadata`, but no signature value is present. | Signing hash, hash algorithm, artifact ID/type metadata, canonicalization metadata, optional host metadata. |
| Signed | A configured `IAsiBackboneSigningService` returned provider-neutral signature metadata for the canonical hash. | Signing hash, hash algorithm, key ID, key version, signature algorithm, signature value, signature provider, signed timestamp, artifact ID/type metadata. |

Unsigned and signing-ready flows intentionally remain supported. This allows hosts to run in local development, low-assurance, phased rollout, or provider-deferral modes without pretending that signatures exist.

## Implemented helper surface

`GovernanceArtifactSigner` provides helper methods for each selected artifact type:

```csharp
SignedGovernanceArtifact<AuditLedgerRecord> signingReady =
    GovernanceArtifactSigner.CreateSigningReadyAuditLedgerRecord(record);

SignedGovernanceArtifact<AuditLedgerRecord> signed =
    await GovernanceArtifactSigner.SignAuditLedgerRecordAsync(
        record,
        signingService,
        keyId: "audit-key",
        keyVersion: "2026-06",
        cancellationToken: cancellationToken);
```

Governance outbox entries use the same pattern:

```csharp
SignedGovernanceArtifact<GovernanceOutboxEntry> signedOutboxEntry =
    await GovernanceArtifactSigner.SignGovernanceOutboxEntryAsync(
        outboxEntry,
        signingService,
        keyId: "outbox-key",
        keyVersion: "2026-06",
        metadata: new Dictionary<string, string>
        {
            ["workflow"] = "governance-outbox"
        },
        cancellationToken: cancellationToken);
```

The helper builds a deterministic canonical payload, computes a canonical payload hash, creates a `SigningRequest` using the artifact type as the signing purpose, invokes the configured signing service, and then merges canonical artifact descriptors back into the returned signing metadata.

## Metadata preserved for later verification

Signed artifacts preserve enough provider-neutral metadata for later verification workflows:

| Metadata | Source |
| --- | --- |
| Artifact ID | Canonical payload hash metadata. |
| Artifact type | Canonical payload hash metadata. |
| Payload schema version | Canonical payload hash metadata. |
| Canonicalization version | Canonical payload hash metadata. |
| Hash algorithm | Canonical payload hash metadata or provider metadata. |
| Signing hash | Canonical payload hash value. |
| Key ID | Signing provider metadata. |
| Key version | Signing provider metadata. |
| Signature algorithm | Signing provider metadata. |
| Signature value | Signing provider metadata. |
| Signature provider | Signing provider metadata. |
| Signed timestamp | Signing provider metadata. |

The helper rejects mismatched signing hashes. If provider metadata contains a signing hash, it must match the canonical hash produced for the artifact being signed.

## Recommended flow order

### Audit ledger record signing

Use this order when the durable audit ledger row must carry signature metadata:

```text
Build audit residue
  -> build audit ledger record
  -> canonicalize audit ledger record
  -> compute canonical hash
  -> sign canonical hash through IAsiBackboneSigningService
  -> attach or project signing metadata
  -> persist durable audit ledger record
```

If the host signs after persistence, the host should define a second durable update/projection step and should document which persisted shape is authoritative.

### Lifecycle event signing

Use this order when lifecycle events require signatures:

```text
Build lifecycle event
  -> canonicalize lifecycle event
  -> compute canonical hash
  -> sign canonical hash
  -> append lifecycle event and signing metadata
```

### Governance envelope signing

Use this order when the envelope is the signed artifact:

```text
Build governance emission envelope
  -> classify and minimize metadata
  -> canonicalize envelope
  -> compute canonical hash
  -> sign canonical hash
  -> enqueue or emit signed envelope projection
```

### Governance outbox entry signing

Use this order when the durable outbox entry is the signed artifact:

```text
Build governance emission envelope
  -> create governance outbox entry
  -> canonicalize outbox entry
  -> compute canonical hash
  -> sign canonical hash
  -> persist signed outbox entry projection before provider emission
  -> drain through configured provider
```

Outbox entries are stateful. Status changes such as delivered, failed, retryable failure, deferred, or dead-lettered change the canonical outbox entry payload. If a host needs each state transition signed, it should sign each new outbox-entry state as a separate signed artifact or lifecycle record.

## Signing-ready and failure behavior

Signing providers may return unsigned failure metadata instead of throwing when configured to do so. The helper preserves that metadata and canonical descriptors while leaving `IsSigned` false.

This is useful for:

- local development;
- provider outage simulation;
- fail-open/continue-audit workflows;
- dead-lettering signing attempts;
- phased rollout where signing is desired but not mandatory.

For high-assurance workflows, hosts should define policy that treats missing signatures or signing failures as deny, defer, require acknowledgment, escalate, or dead-letter.

## Safe wording boundary

Safe wording:

- "This artifact was canonicalized and signed by the configured provider."
- "The signed artifact carries metadata needed for later verification."
- "Unsigned, signing-ready, and signed states are represented separately."
- "Verification must be explicitly performed before a signature is trusted."
- "Local-development signing is a released test/sample proof path, not production key custody."
- "Managed-key signing uses a released adapter boundary with a host-owned managed-key client."

Avoid wording such as:

- "The audit trail is tamper-proof."
- "Signing makes this record legally non-repudiable."
- "The outbox is immutable by default."
- "Signed means verified."
- "A signature alone makes the full audit trail tamper-evident."
- "The managed-key adapter ships Azure Key Vault, Managed HSM, or cloud KMS support by default."

Use the phrase **tamper-evident** only when the deployed system also includes verification, durable storage controls, hash chaining or equivalent integrity strategy, and any required external anchoring.

## Relationship to provider packages

Core remains provider-neutral. It does not choose Azure Key Vault, HSM, certificate store, local-development keys, cloud KMS, blockchain, transparency log, or object-lock storage.

Provider packages implement `IAsiBackboneSigningService`. Host applications decide:

- which artifacts must be signed;
- which key provider is authoritative;
- which key ID and key version apply;
- whether verification is required before persistence, emission, or review;
- how signing failures affect decision flow;
- whether signed metadata is stored in audit ledger rows, outbox rows, lifecycle rows, external records, or all of them.

## Related documentation

- [Production Wording and Stable Signing Boundaries](production-wording-and-alpha-limitations.md)
- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Signing Provider Package Boundary](signing-provider-package-boundary.md)
- [Managed-Key Signing Provider](managed-key-signing-provider.md)
- [Verification Policy and Result Handling](verification-policy-and-result-handling.md)
- [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md)
