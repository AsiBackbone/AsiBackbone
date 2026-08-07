# Signing-Ready Receipts and Key Handling

This article documents the stable Core-neutral signing and verification primitives released in the `3.0.0 - Observability, Outbox, Signing, and Governance Emission Providers` package family.

Issues: #147, #219, #253.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a governance spine for consequential software decision flow. It is not a signing product, key-management system, immutable ledger, legal certification system, or compliance guarantee by itself.

> [!IMPORTANT]
> `AsiBackbone.Core` includes stable signing-ready metadata, canonical payload hashing, signing seams, and verification-policy primitives in `3.2.1`. Those Core primitives make artifacts ready for provider signing and later verification workflows; they do not create production tamper-evidence by themselves. Production tamper evidence requires a concrete signing provider, protected key management, verification policy, durable storage guarantees, retention policy, monitoring, and operational procedures supplied by the host or provider environment.
>
> These primitives were introduced in the historical `3.0.0` release.

For released provider boundaries, see [Signing Provider Package Boundary](signing-provider-package-boundary.md), [Managed-Key Signing Provider](managed-key-signing-provider.md), and [Signed Audit and Outbox Records](signed-audit-and-outbox-records.md). For production posture, setup guidance, verification-failure behavior, audit-chain wording, capability-token validation, and security non-goals, see [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md).

## Purpose

AsiBackbone audit residue, audit ledger records, capability-token references, outbox entries, and downstream governance emission envelopes need a stable way to construct deterministic payloads, compute provider-neutral hashes, and carry signing metadata without forcing Core to depend on one key provider.

The signing-ready model gives host applications and provider packages a neutral contract for building canonical payloads, hashing deterministic artifact bytes, signing precomputed hashes, recording signature metadata on audit receipts, carrying key identifier and key version references, and verifying signatures downstream.

## Released signing boundaries

| Boundary | `3.0.0` status | Wording limit |
| --- | --- | --- |
| Core signing-ready primitives | Stable in `AsiBackbone.Core`. | Current behavior. Do not describe it as future-only. |
| Local-development signer | Stable package: `AsiBackbone.Signing.LocalDevelopment`. | Local/test/sample/wiring proof path only, not production key custody. |
| Managed-key signing adapter | Stable package: `AsiBackbone.Signing.ManagedKey`. | Adapter boundary only. The host supplies the actual managed-key client and operational policy. |
| Concrete Azure Key Vault, Managed HSM, cloud KMS, HSM, or certificate-store clients | Host-owned or future provider-specific work unless separately released. | Do not imply these concrete integrations ship by default. |
| Tamper-evidence, immutability, external anchoring, legal non-repudiation, or compliance certification | Not provided by default. | Requires deployed signing, verification, protected key management, durable storage controls, retention, monitoring, and operational process. |

## Added Core abstractions

| Type | Role |
| --- | --- |
| `CanonicalArtifactTypes` | Stable artifact type identifiers bound into canonical payloads before hashing. |
| `CanonicalPayloadOptions` | Canonicalization version, hash algorithm, and metadata allow-list configuration. |
| `CanonicalPayload` | Deterministic JSON payload envelope containing artifact type, artifact ID, payload schema version, canonicalization version, and artifact content. |
| `CanonicalPayloadBuilder` | Provider-neutral builders for audit residue, audit ledger records, lifecycle events, governance emission envelopes, and governance outbox entries. |
| `CanonicalPayloadHash` | Provider-neutral hash result metadata containing hash value, hash algorithm, canonicalization version, artifact type, artifact ID, and payload schema version. |
| `CanonicalPayloadHasher` | Built-in SHA-256 hasher for canonical payload bytes. |
| `SigningMetadata` | Provider-neutral signing metadata containing signing hash, hash algorithm, signature, signature algorithm, key ID, key version, provider descriptor, signed timestamp, and safe metadata. |
| `SigningRequest` | Provider-neutral request to sign a precomputed artifact hash. |
| `SigningResult` | Provider-neutral result containing signing metadata. |
| `SignatureVerificationRequest` | Provider-neutral request to verify signing metadata against a precomputed artifact hash. |
| `SignatureVerificationResult` | Provider-neutral verification result. |
| `IAsiBackboneSigningService` | Async signing boundary implemented by host applications or provider packages. |
| `IAsiBackboneSignatureVerificationService` | Async verification boundary implemented by host applications or provider packages. |

## Canonical payload hashing

Canonical payload hashing prepares governance artifacts for downstream signing and verification seams. It does **not** sign the artifact, verify a signature, create an append-only chain, or make the record tamper-evident by itself.

The Core canonicalization contract uses a deterministic JSON envelope with these top-level fields:

| Field | Purpose |
| --- | --- |
| `artifactType` | Binds the payload to a stable artifact category such as `asibackbone.audit-ledger-record`. |
| `artifactId` | Binds the payload to the concrete artifact identifier. |
| `payloadSchemaVersion` | Binds the payload to the stable artifact schema version. |
| `canonicalizationVersion` | Binds the payload to the deterministic serialization rules. |
| `content` | Contains the artifact-specific, minimized governance content. |

Canonicalization rules:

* JSON object properties are emitted in ordinal key order.
* Timestamps are converted to UTC and formatted as `yyyy-MM-ddTHH:mm:ss.fffffffZ`.
* Null properties are retained so absence and presence remain explicit.
* Unordered string collections such as reason-code sets are trimmed, de-duplicated, and sorted ordinally.
* Metadata is excluded unless the host supplies an explicit `CanonicalPayloadOptions` allow-list. This prevents diagnostic or provider-specific metadata from silently changing signable payloads.
* The built-in hasher computes SHA-256 over the UTF-8 bytes of the canonical JSON payload.
* Additional hash algorithms should be implemented by host or provider packages rather than forcing Core to depend on a concrete key-management or crypto-provider stack.

`CanonicalPayloadHash.ToSigningMetadata()` can copy the hash into `SigningMetadata.SigningHash` and add descriptor metadata such as artifact type, artifact ID, canonicalization version, and payload schema version. This metadata is still unsigned until a host or provider uses `IAsiBackboneSigningService` and records a signature value, signature algorithm, key ID, and key version.

## Audit ledger metadata

`AuditLedgerRecord` can carry signing-ready metadata alongside existing audit and capability-token references:

* `SigningHash`
* `SignatureKeyId`
* `SignatureKeyVersion`
* `SignatureAlgorithm`
* `SignatureValue`
* `SignatureProvider`
* `SignedUtc`
* `SigningMetadata`
* `CapabilityTokenId`

`SignatureKeyId` and `SignatureKeyVersion` are references, not secrets. They should identify the key used for verification without exposing raw signing material.

## Key-handling rules

Signing providers should:

* prefer key-based APIs over retrieving raw signing secrets;
* avoid returning private keys, symmetric keys, connection strings, credentials, or managed identity tokens to Core;
* return only key references, provider descriptors, and signature metadata;
* preserve key version metadata whenever available;
* treat key rotation as provider or host policy;
* avoid claims of tamper evidence unless signing, verification, durable storage, and operational controls are all implemented.

## Verification strategy

`IAsiBackboneSignatureVerificationService` verifies a `SignatureVerificationRequest` containing the expected signing hash and signing metadata.

Core does not require a particular signing algorithm such as RSA, ECDSA, HMAC, EdDSA, or a provider-specific managed key operation.

A signed artifact should not be treated as trusted merely because a signature value is present. Hosts that require trust should explicitly run verification and apply verification policy before execution, emission, or audit review.

## Test seams

The Core tests use deterministic canonical payload builders, stable hash assertions, and fake signer/verifier implementations to prove the seams without live key-management resources. Live Azure Key Vault, HSM, or cloud signing tests should be optional, explicitly configured, and excluded from default CI.

## Related documentation

- [Production Wording and Stable Signing Boundaries](production-wording-and-alpha-limitations.md)
- [Signing Provider Package Boundary](signing-provider-package-boundary.md)
- [Managed-Key Signing Provider](managed-key-signing-provider.md)
- [Signed Audit and Outbox Records](signed-audit-and-outbox-records.md)
- [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Audit Residue Observability Schema](audit-residue-observability-schema.md)
- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
