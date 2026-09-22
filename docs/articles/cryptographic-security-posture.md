# Cryptographic Security Posture and Production Guidance

This article documents the AsiBackbone cryptographic security posture for signing-ready records, signed artifacts, verification, audit-chain integrity, key management, capability grants, and production wording.

Issue: #216, updated for #253.

AsiBackbone provides governance infrastructure around consequential software decision flow. It is not a key-management system, immutable ledger, blockchain product, legal evidence system, compliance certification service, or tamper-proof storage system by itself.

> [!IMPORTANT]
> `1.1.0` includes stable Core signing-ready metadata, canonical payload hashing, signing seams, verification-policy primitives, a local-development signing provider, and a managed-key adapter boundary. Production cryptographic assurance still requires a host-owned or provider-owned implementation that signs canonical artifacts, protects keys, verifies signatures, stores records durably, monitors failures, manages retention, and defines operational response procedures.

## Security posture summary

AsiBackbone separates cryptographic vocabulary into explicit states so documentation, release notes, and production designs do not overclaim.

| State | Meaning | Safe wording | Do not imply |
| --- | --- | --- | --- |
| Signing-ready | The record contains stable fields, hashes, identifiers, schema versions, policy versions, and metadata that a signer can use. | "Records can carry signing-ready metadata." | The record is signed, immutable, verified, or tamper-evident. |
| Signed | A concrete signer produced signature metadata over a canonical artifact hash. | "This artifact was signed by the configured provider." | The signature has been independently verified or legally proves intent. |
| Verified | A verifier recomputed or received the expected hash and confirmed the signature metadata. | "The signature verified against the expected artifact hash and key reference." | The whole storage system is immutable or the action is legally non-repudiable. |
| Chained | A record includes previous-record hash material or a chain reference. | "Records are hash-linked by the configured chain strategy." | The chain is externally anchored, blockchain-backed, or impossible to rewrite. |
| Externally anchored | A chain root, digest, or receipt is published to a separately controlled timestamp, ledger, object-lock store, transparency log, or other anchoring service. | "The chain is anchored to the configured external service." | All historical data is guaranteed tamper-proof without verifying anchor, storage, key, and retention controls. |

The strongest accurate statement is the narrowest one the deployed system can prove.

## Default package boundary

The stable package family provides governance primitives and metadata surfaces. In `1.1.0`, this includes stable Core signing-ready primitives, the local-development signing provider, and the managed-key signing adapter boundary.

By default the package family does not:

- generate production signing keys;
- store private keys or symmetric signing secrets;
- configure Azure Key Vault, HSM, certificate stores, or cloud KMS resources by itself;
- guarantee key rotation;
- verify every persisted record automatically;
- make database rows immutable;
- anchor audit chains to a blockchain or transparency log;
- certify legal non-repudiation;
- replace legal, compliance, security, privacy, or operational review.

The host application remains responsible for deciding whether a given workflow requires unsigned records, signed records, verified records, chained records, or externally anchored records.

## Released signing package boundaries

| Surface | `1.1.0` status | Production boundary |
| --- | --- | --- |
| `AsiBackbone.Core` signing primitives | Stable. | Signing-ready metadata, canonical hashing, signing seams, verification seams, and verification-policy primitives. No production key custody. |
| `AsiBackbone.Signing.LocalDevelopment` | Stable local-development provider. | Tests, samples, local validation, and wiring proof paths only. Not production signing. |
| `AsiBackbone.Signing.ManagedKey` | Stable managed-key adapter. | Adapter boundary only. Host supplies the actual managed-key client, credentials, verification path, monitoring, and operational policy. |
| Concrete Azure Key Vault, Managed HSM, cloud KMS, HSM, or certificate-store implementation | Host-owned or future package unless separately released. | Do not imply this ships by default. |
| Production tamper-evidence, immutability, external anchoring, legal non-repudiation, or compliance certification | Not provided by default. | Requires a deployed end-to-end operational trust design. |

## Recommended production configuration

A production deployment should make each security control explicit.

| Area | Recommendation |
| --- | --- |
| Key provider | Use a managed key provider, HSM-backed provider, enterprise KMS, or equivalent protected signing service. Prefer key-based APIs that sign hashes without exposing private key material to the application process. |
| Key access | Grant least-privilege signing and verification permissions to the host identity. Separate sign permissions from verify permissions where the provider supports it. |
| Key rotation | Preserve `SignatureKeyId` and `SignatureKeyVersion` on each signed artifact. Keep old verifying keys available for the full retention period. Define rotation cadence and emergency revocation procedure. |
| Canonicalization | Define the exact canonical payload shape before hashing. Include schema version, artifact type, record ID, policy version, policy hash, timestamps, correlation ID, decision outcome, acknowledgment/capability references, and safe metadata. |
| Hashing | Use a strong algorithm approved by the host security program. Record the hash algorithm and canonicalization version with the artifact. |
| Signing | Sign the precomputed canonical artifact hash, not an ad hoc object instance whose serialization may drift. Return only provider descriptor, key reference, signature algorithm, signature value, and signed timestamp. |
| Verification | Verify before trusting a signed receipt, before emitting high-assurance governance records, and during audit review. Decide whether verification failure causes deny, defer, require acknowledgment, escalate, or dead-letter. |
| Storage | Store signed records in durable host-owned storage with access controls, backup, restore, retention, and operational monitoring. Use immutable or object-lock storage only when explicitly configured and tested. |
| Outbox | Preserve local audit/outbox records before external emission. Do not treat provider delivery as the source of truth unless the host has explicitly designed that provider as authoritative. |
| Monitoring | Alert on signing failures, verification failures, key-access failures, clock skew, unexpected key versions, hash mismatches, outbox dead letters, and repeated provider emission failures. |
| Incident response | Define how to freeze signing, rotate keys, revoke compromised keys, reverify affected records, quarantine outbox entries, and communicate audit limitations. |

## Canonical artifact guidance

Signing should use a canonical artifact that is stable across processes and package versions.

Recommended canonical payload fields:

```text
schema_version
artifact_type
artifact_id
record_id / event_id
occurred_utc
recorded_utc
correlation_id
trace_id
actor_id or actor hash
decision_outcome
reason_codes
policy_scope
policy_version
policy_hash
constraint_set_hash
acknowledgment_id when present
handshake_id when present
capability_token_id when present
outbox_sequence when present
previous_record_hash when chaining
safe metadata
```

Canonicalization rules should define:

- deterministic property order;
- UTC timestamp formatting;
- string casing and trimming rules;
- null and missing value behavior;
- collection ordering;
- metadata allow-listing;
- schema-version upgrade behavior.

Do not sign raw free-form metadata until the host has classified and minimized it.

## Signing audit receipts

The signing-ready seam expects the host or provider package to precompute the artifact hash and pass that hash into the signing boundary.

Conceptual flow:

```text
Build audit receipt
  -> classify and minimize metadata
  -> canonicalize receipt payload
  -> compute artifact hash
  -> build the version 1 signature input (hash + descriptors + policy context)
  -> sign the signature input through IGovernanceSigningService
  -> attach SigningMetadata to the receipt or ledger record
  -> persist receipt and signing metadata durably
```

Illustrative pseudo-code:

```csharp
var payload = CanonicalReceiptPayload.FromDecision(
    decision,
    acknowledgment,
    capabilityToken,
    safeMetadata);

var hash = canonicalHasher.Hash(payload);

var signingResult = await signingService.SignAsync(
    SigningRequest.Create(
        artifactId: payload.ArtifactId,
        artifactType: "audit-residue",
        signingHash: hash.Value,
        hashAlgorithm: hash.Algorithm,
        metadata: payload.SafeMetadata),
    cancellationToken);

var signedRecord = AuditLedgerRecord.FromDecisionReceipt(
    receipt,
    signingHash: signingResult.Metadata.SigningHash,
    signatureKeyId: signingResult.Metadata.KeyId,
    signatureKeyVersion: signingResult.Metadata.KeyVersion,
    signatureAlgorithm: signingResult.Metadata.SignatureAlgorithm,
    signatureValue: signingResult.Metadata.SignatureValue,
    signatureProvider: signingResult.Metadata.Provider,
    signedUtc: signingResult.Metadata.SignedUtc);
```

This example describes the intended production shape. The exact constructor names and helper methods should follow the implemented package APIs in the consuming version.

## Verifying audit receipts

Verification should compare the expected canonical hash to the signature metadata.

Conceptual flow:

```text
Load receipt
  -> rebuild canonical payload using the recorded schema version
  -> recompute artifact hash
  -> rebuild the version 1 signature input from the hash and signing metadata
  -> verify the signature input through IGovernanceSignatureVerificationService
  -> apply host verification policy
```

Recommended verification outcomes:

| Verification result | Recommended response |
| --- | --- |
| Valid | Continue normal review or emission. |
| Invalid signature | Treat as a high-severity integrity failure; deny trust, quarantine record, alert operators, and preserve forensic context. |
| Missing signature where required | Defer, escalate, or dead-letter according to policy. |
| Unknown key version | Attempt configured key-resolution path; if unresolved, escalate and do not mark verified. |
| Key revoked or disabled | Apply incident policy; do not silently accept the record. |
| Canonicalization mismatch | Treat as schema or serialization drift; escalate before relying on the record. |
| Verification provider unavailable | Follow policy: defer, retry, require acknowledgment, escalate, or fail closed for high-risk workflows. |

## What the signature covers

Since 6.0, signing providers sign and verification providers verify a versioned **signature input**, not the hash text alone. `GovernanceSignatureInput.CreateV1` builds it as canonical JSON (the same serializer and ordinal key order as canonical payload JSON v1) with these properties:

| Property | Source |
| --- | --- |
| `artifactId`, `artifactType`, `canonicalizationVersion`, `payloadSchemaVersion` | Canonical payload descriptors. |
| `format` | Always `asibackbone.signature-input.v1`. |
| `hashAlgorithm`, `hashValue` | The canonical payload hash. |
| `policyVersion`, `policyHash` | The `policy_version` and `policy_hash` signing metadata values, trimmed. A missing or blank value is bound as JSON `null`. |

Consequences:

- Relabeling `policy_version` or `policy_hash` after signing, or adding either label to an artifact signed without it, invalidates the signature. Policy pins in `VerificationPolicyContext` therefore check authenticated values.
- `KeyId`, `KeyVersion`, `Provider`, and `SignedUtc` are **not** in the input, because managed-key services commonly resolve the key version and timestamp during signing. They are authenticated only to the extent that the verification service resolves its verification key from `KeyId` and `KeyVersion` and rejects a `Provider` label it does not own. The local-development provider does both. Host verification services must do the same, or key and provider pins check labels the signature does not cover.
- `SignedUtc` is provider-asserted. Hosts that need an authenticated signing time must obtain it from a trusted timestamping or anchoring service.
- Other signing metadata keys are diagnostic labels and are not covered by the signature.

`GovernanceArtifactSigner` supplies the version 1 input on `SigningRequest.SignatureInput` and, after signing, restores the signed `policy_version` and `policy_hash` values in the stored metadata. `GovernanceArtifactVerifier` rebuilds the same input from the artifact and its signing metadata and passes it on `SignatureVerificationRequest.SignatureInput`. Hosts that persist and rehydrate signed artifacts must persist the `policy_version` and `policy_hash` signing metadata values they signed; otherwise the rebuilt input differs and verification fails closed.

### Artifacts signed before 6.0

Artifacts signed by 5.x providers carry signatures over the hash text only. Version 1 verification reports them as `InvalidSignature` and denies. To review or migrate such artifacts, opt in explicitly:

```csharp
VerificationPolicyContext historicalReview = VerificationPolicyContext.Default.WithLegacySignatureInputAllowed();
```

With the opt-in, an invalid-signature result is retried once against the hash-only input. A signature accepted that way authenticates the canonical payload hash only, so it cannot satisfy `ExpectedPolicyVersion` or `ExpectedPolicyHash`; such pins deny with `UntrustedSigningContext` and `signature.policy-context-not-authenticated`. Use the opt-in for historical review paths, not for new execution decisions.

## Audit chains and anchoring

Hash-linked audit chains can help detect record deletion, insertion, or modification when verification is performed against the full chain.

A chained record may carry:

- `PreviousRecordHash`;
- `RecordHash`;
- chain ID;
- chain sequence;
- chain root;
- chain canonicalization version;
- signature metadata for the record or chain root.

Chaining is not the same as external anchoring.

| Pattern | What it provides | Remaining risk |
| --- | --- | --- |
| Local hash chain | Detects changes when the verifier has a trusted chain head or expected root. | A privileged actor may rewrite both records and local chain heads if storage is not protected. |
| Signed local chain | Binds records or roots to a key reference. | A compromised signing key or permissive signing service may still sign bad data. |
| Immutable/object-lock storage | Reduces rewrite/delete risk in configured storage. | Misconfiguration, retention gaps, privileged break-glass access, or unprotected upstream records remain risks. |
| External timestamp/anchor | Places a digest in a separately controlled system. | The anchor must be verified and must correspond to the retained local records. |
| Blockchain-backed anchor | Can provide public or consortium anchoring when designed correctly. | AsiBackbone does not provide this by default; chain selection, cost, privacy, key custody, and legal interpretation remain host responsibilities. |

Use the phrase "tamper-evident" only when the deployed design includes signing, verification, durable storage controls, and a tested audit-chain or anchoring process.

## Outbox emission

Governance emission should preserve local accountability before downstream projection.

Recommended secure sequence:

```text
Decision / acknowledgment / capability event
  -> build decision receipt or lifecycle event
  -> persist durable local audit record
  -> optionally sign the local artifact or outbox envelope
  -> enqueue outbox entry
  -> drain through configured provider
  -> verify delivery result
  -> preserve delivery status and failure reason
```

Provider-specific emissions should be treated as projections unless the host explicitly designates them as authoritative.

Security guidance:

- Do not emit unclassified or sensitive metadata to external systems by default.
- Apply DLP/classification policy before provider emission.
- Do not include signing secrets, raw credentials, bearer tokens, private keys, or managed identity tokens in outbox payloads.
- Preserve enough correlation data to connect emission records back to durable local audit records.
- Alert on repeated emission failures, dead-letter growth, and provider authorization failures.

## Capability-token validation

Capability grants should be short-lived, scoped, and validated at the execution boundary. Signing a token is useful only when the validator checks it every time it matters.

Recommended validation checks:

| Check | Purpose |
| --- | --- |
| Signature / MAC | Confirms the token was issued by the configured authority. |
| Issuer | Confirms the token came from an expected issuer. |
| Audience | Confirms the token is intended for this gateway or host boundary. |
| Expiration | Prevents broad long-lived authority. |
| Not-before | Prevents early use before issuance or approval. |
| Scope | Limits which operation, resource, region, tenant, or gateway may use the token. |
| Replay identifier | Supports single-use or bounded-use behavior where required. |
| Policy version/hash | Binds the grant to the policy context that produced it. |
| Acknowledgment/handshake reference | Binds consequential execution to the required acknowledgment workflow. |
| Revocation state | Allows emergency revocation or workflow cancellation. |

Illustrative pseudo-code:

```csharp
var validation = await capabilityTokenValidator.ValidateAsync(
    token,
    new CapabilityTokenValidationContext(
        requiredScope: "document.approve",
        audience: "governed-workflow-gateway",
        expectedPolicyHash: decision.PolicyHash,
        requiredAcknowledgmentId: acknowledgment.Id),
    cancellationToken);

if (!validation.IsValid)
{
    return BackboneDecision.Deny(
        reasonCode: "capability-token-invalid",
        metadata: validation.SafeMetadata);
}
```

For high-risk workflows, token validation failure should usually deny or escalate rather than silently fall back to broad host authorization.

## Local-development signer limitations

`AsiBackbone.Signing.LocalDevelopment` is a released local-development provider package. It can be useful for samples, smoke tests, and development workflows, but it should be described narrowly.

Local-development signers are appropriate for:

- proving the signing seam;
- exercising metadata flow;
- testing canonicalization;
- validating verification logic;
- demonstrating how hosts wire the abstraction.

Local-development signers should not be described as:

- production key management;
- HSM-backed security;
- tamper-evidence;
- non-repudiation;
- compliance evidence;
- protection against privileged host compromise.

Local signer guidance:

- Keep development keys out of source control.
- Use environment-specific key identifiers.
- Never reuse sample keys in production.
- Mark local signatures with a provider descriptor such as `local-development` or `sample-only`.
- Exclude live cloud signing tests from default CI unless the workflow is explicitly configured with protected credentials and clear opt-in behavior.

## Managed-key adapter limitations

`AsiBackbone.Signing.ManagedKey` is a released adapter boundary, not a complete key-management implementation.

Hosts using it in production should:

- supply the concrete managed-key client;
- keep private keys inside the managed-key/HSM/KMS boundary;
- control credentials and managed identity configuration;
- define signing and verification permissions;
- document failure policy and monitoring;
- provide the verification path required to trust signed records later.

## Host responsibilities

The host application owns the production security envelope.

Before using cryptographic features in production, the host should define:

- which workflows require signatures;
- which workflows require verification before execution, emission, or review;
- which records require hash chaining;
- whether any chain root is externally anchored;
- which key provider is authoritative;
- which identities may sign;
- which identities may verify;
- how keys rotate;
- how compromised keys are revoked;
- how old signatures remain verifiable during retention;
- how clocks are synchronized;
- how verification failures are handled;
- how signed records are backed up and restored;
- how immutable storage or legal hold is configured when needed;
- how audit records are retained, purged, or exported;
- how DLP/classification policy applies before signing and emission;
- how operators are alerted on failures.

### Clock and timestamp responsibilities

Timestamps are meaningful only when the host controls clock behavior.

Production hosts should:

- use reliable NTP or cloud time synchronization;
- monitor clock skew;
- record timestamps in UTC;
- distinguish event occurrence time from record persistence time and signing time;
- consider trusted timestamp authority integration when legal or regulatory review requires it.

A signed timestamp from an application server is not the same as an independent trusted timestamp.

## Threat-model notes

Cryptographic metadata can improve integrity checks, but it does not remove the need for a threat model.

| Threat | Mitigation direction |
| --- | --- |
| Record modification after persistence | Verify signatures and hash chains; restrict database writes; monitor unexpected changes. |
| Record deletion | Use retention policy, backups, append-only stores, object lock, or external anchoring where required. |
| Key compromise | Use managed keys, least privilege, rotation, revocation, monitoring, and incident response. |
| Signing bad data | Require policy evaluation, acknowledgment workflow, canonicalization review, DLP checks, and approval gates before signing. |
| Replay of capability grant | Use short expiration, nonce or token ID, audience/scope checks, single-use stores, and revocation. |
| Clock manipulation | Monitor clock skew and use trusted timestamping when required. |
| Provider outage | Persist local audit/outbox records and apply fail-closed, defer, retry, or escalate policy based on risk. |
| Privileged operator misuse | Separate duties, restrict sign permissions, review logs, use external anchors, and require break-glass auditing. |
| Serialization drift | Version canonicalization and schema rules; verify old records with their original schema version. |

## Security non-goals

AsiBackbone documentation and package surfaces should not claim that AsiBackbone alone provides:

- legal immunity;
- legal non-repudiation;
- proof of consent in every jurisdiction;
- regulatory certification;
- tamper-proof records;
- blockchain-backed audit storage;
- immutable database rows;
- automatic key rotation;
- automatic privacy classification;
- automatic incident response;
- replacement for identity, authentication, authorization, or access-control systems.

## Relationship to core governance flow

Cryptographic posture should reinforce the existing AsiBackbone governance flow rather than replace it.

| Governance area | Cryptographic relationship |
| --- | --- |
| Policy pipeline | Policy version and policy hash should be part of the canonical signed artifact when signatures are required. |
| Acknowledgment Workflow / acknowledgment workflow | Acknowledgment and handshake identifiers should be included in signed receipts for consequential actions. |
| Audit receipts | Audit records can carry signing metadata, hashes, key references, and chain references. |
| Capability grants | Tokens should be signed or otherwise protected and validated at the execution boundary. |
| Durable outbox | Outbox records should be preserved locally before external emission, and high-assurance deployments may sign the local artifact or the emission envelope. |
| Gateway execution | Gateways should validate token scope, expiration, policy binding, and acknowledgment binding before execution. |

## Release-note wording

Safe release wording:

> AsiBackbone `1.1.0` provides stable Core signing-ready metadata, canonical hashing, provider-neutral signing and verification seams, a local-development signing provider, a managed-key signing adapter boundary, and production guidance for hosts that need signed or verified governance artifacts. Production tamper-evidence, immutability, blockchain anchoring, and non-repudiation are not provided by default and require concrete host or provider configuration.

Avoid wording such as:

- "AsiBackbone makes audit records tamper-proof."
- "AsiBackbone provides blockchain-backed audit trails."
- "AsiBackbone guarantees legal non-repudiation."
- "AsiBackbone proves user consent."
- "AsiBackbone handles key management automatically."
- "The managed-key adapter provides Azure Key Vault, Managed HSM, or cloud KMS support by default."

## Related documentation

- [Production Wording and Stable Signing Boundaries](production-wording-and-alpha-limitations.md)
- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Signing Provider Package Boundary](signing-provider-package-boundary.md)
- [Managed-Key Signing Provider](managed-key-signing-provider.md)
- [Signed Audit and Outbox Records](signed-audit-and-outbox-records.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md)
- [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
- [OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md)
- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Core Domain Language](core-domain-language.md)
