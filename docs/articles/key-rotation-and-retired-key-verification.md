# Key Rotation and Retired-Key Verification

Issue: #223.

This article documents key rotation, key versioning, retired-key verification, and compromised-key response guidance for AsiBackbone signing providers and host integrations.

AsiBackbone defines provider-neutral signing, verification, and policy seams. It does not provide managed-key storage, automatic key rotation, emergency key revocation, legal evidence guarantees, compliance certification, immutable storage, or tamper-evidence by itself.

> [!IMPORTANT]
> Key rotation changes which key version should sign new artifacts. It should not make already-signed governance artifacts unverifiable during the configured audit-retention period. Hosts and provider packages must preserve enough key metadata and verification material to verify historical records.

## Required signed-artifact metadata

Every signed governance artifact should preserve the metadata needed to resolve the correct verification material later.

| Metadata | Source | Required behavior |
| --- | --- | --- |
| Artifact ID | Canonical payload hash metadata | Persist with the signed artifact so verification can identify the exact record. |
| Artifact type | Canonical payload hash metadata | Persist with the signed artifact so verification can enforce the expected purpose. |
| Signing hash | Canonical payload hash | Persist with the signature metadata and compare before cryptographic verification. |
| Hash algorithm | Canonical payload hash metadata or provider metadata | Persist and reject unsupported or mismatched algorithms. |
| Key ID | Signing provider metadata | Persist the stable logical key reference. |
| Key version | Signing provider metadata | Persist the exact key version used to sign the artifact. |
| Provider descriptor | Signing provider metadata | Persist enough provider identity to route later verification. |
| Signature algorithm | Signing provider metadata | Persist the algorithm descriptor used by the provider. |
| Signed timestamp | Signing provider metadata | Persist the timestamp returned by the provider or trusted host boundary. |
| Verification status | Verification policy outcome | Persist separately when verification occurs. Do not treat this as signing metadata produced at signing time. |

`SigningMetadata` carries the signing-time fields. `VerificationPolicyOutcome` carries verification-time fields such as verification category, policy action, status, and failure code.

## Key version states

Provider packages and hosts should document how each key version state affects signing and verification.

| Key version state | May sign new artifacts? | May verify historical artifacts? | Recommended verification result | Typical policy response |
| --- | --- | --- | --- | --- |
| Active | Yes | Yes | `Valid` when the signature verifies. | `Allow` |
| Retired | No | Yes, through the retention period. | `Valid` when the signature verifies and signed timestamp/key version are acceptable. | `Allow` for audit review; host-specific for new execution. |
| Revoked | No | Usually no silent trust; may require incident review. | `RevokedKey` when detected. | `Deny` or `DeadLetter`; preserve forensic context. |
| Expired | No | Sometimes, if signed during the valid signing window and retained verification material exists. | `Valid`, `UnknownKeyVersion`, or `RevokedKey` depending on provider evidence and policy. | `Allow` only when policy accepts historical validity; otherwise `Escalate`. |
| Disabled | No | Provider-specific. Some providers may block verification operations while disabled. | `ProviderUnavailable`, `UnknownKeyVersion`, or `RevokedKey` depending on provider signal. | `Defer`, `Retry`, or `Escalate`; do not silently trust. |
| Unknown | No | No, because the verifier cannot resolve the version. | `UnknownKeyVersion`. | `Escalate`, `Retry`, or `DeadLetter` depending on workflow. |

A **retired** key is different from a **revoked** key. Retired means it should no longer be used for new signatures but may still be trusted for historical verification. Revoked means the key version is no longer trusted, usually because compromise, misuse, or invalid issuance is suspected or confirmed.

## Normal rotation model

Use normal rotation when a key reaches the end of its signing window, when scheduled operational rotation occurs, or when a host wants to limit the amount of data signed under one key version.

Recommended sequence:

```text
Prepare new key version
  -> configure provider to sign new artifacts with the new version
  -> keep old version resolvable for verification
  -> persist key ID and exact key version on every new signature
  -> verify samples from both old and new versions
  -> monitor verification outcomes for UnknownKeyVersion, UnsupportedAlgorithm, and ProviderUnavailable
```

Normal rotation should preserve these invariants:

- the active signing version changes for new artifacts;
- historical signed artifacts keep their original key ID and key version;
- retired versions remain discoverable by the verifier for the audit-retention period;
- verification policy decides what to do when old versions cannot be resolved;
- key version history is backed up and recoverable according to host retention requirements;
- release notes and documentation do not imply that rotation is automatic.

## How old records remain verifiable

Old records remain verifiable only when the host and provider can reconstruct or resolve the verification context that existed when the record was signed.

Minimum requirements:

1. Preserve the signed artifact, canonical hash metadata, and signing metadata.
2. Preserve the key ID and exact key version used to sign the artifact.
3. Preserve or resolve the public verification material, certificate chain, managed-key version reference, or provider-specific verification capability for the retention period.
4. Preserve algorithm descriptors and canonicalization version so the verifier knows how to recompute and compare the expected hash.
5. Preserve verification outcomes as review metadata, not as a replacement for future verification.

For asymmetric signatures, hosts may be able to verify with public material. For managed-key providers, the provider may own the verification operation or key-version lookup. Core does not assume either model.

## Retired-key verification

A retired key version should normally be treated as valid for historical verification when all of the following are true:

- the artifact signing timestamp falls within the version's approved signing window;
- the artifact carries the exact key ID and key version;
- the verifier can resolve the historical version or public verification material;
- the signature verifies against the expected signing hash;
- the key version was retired normally and was not revoked for compromise;
- host policy accepts the artifact's policy context and retention context.

Recommended retired-key verification flow:

```text
Load signed artifact
  -> recompute canonical hash
  -> read key ID and key version from signing metadata
  -> resolve key version status
  -> if retired, verify using retained historical verification material
  -> apply verification policy outcome
  -> persist verification attempt status separately from signing metadata
```

Retired-key verification should not re-sign old artifacts under the new key by default. Re-signing changes the evidence model and may obscure which key version was authoritative at the time of the original event. If a host needs archival re-signing or notarization, it should store it as a separate archival attestation rather than overwrite original signing metadata.

## Expired and disabled keys

Expired and disabled states require careful provider-specific documentation.

An expired key version may still be acceptable for historical verification if the signature was created during the key version's valid signing window and the provider can verify the historical signature. If the provider cannot establish that historical validity, map the result to `UnknownKeyVersion` or `Failed` and escalate.

A disabled key version may be temporarily unavailable for verification. If the host intentionally disabled a key for maintenance or access-control repair, map provider failure to `ProviderUnavailable`, then `Defer` or `Retry`. If the key was disabled due to suspected compromise, treat the workflow as a compromised-key response and prefer `RevokedKey` or `Escalate`.

## Revoked or compromised key response

A revoked key version should not be silently trusted, even for old records. Revocation usually means the host no longer trusts signatures under that version without further investigation.

Recommended emergency sequence:

```text
Detect suspected compromise
  -> stop signing with affected key ID/version
  -> activate emergency replacement version
  -> mark affected version revoked or quarantined in provider/host key registry
  -> identify artifacts signed by affected version
  -> verify or quarantine affected artifacts according to incident policy
  -> preserve forensic context and verification outcomes
  -> document scope, remediation, and retained trust assumptions
```

Recommended policy mapping:

| Condition | Verification category | Typical action |
| --- | --- | --- |
| Provider confirms key version revoked | `RevokedKey` | `Deny` or `DeadLetter` for emission; escalate for audit review. |
| Provider cannot resolve historical key version | `UnknownKeyVersion` | `Escalate`, `Retry`, or `DeadLetter`. |
| Provider is temporarily unavailable during incident response | `ProviderUnavailable` | `Defer` or `Retry` for lower-risk review; fail closed for high-risk execution. |
| Signature fails under affected key version | `InvalidSignature` or `HashMismatch` | `Deny`, `DeadLetter`, and alert. |
| Algorithm is no longer accepted | `UnsupportedAlgorithm` | `Deny` for new trust decisions; escalate historical review. |

Compromised-key response is host-owned. Core can carry the result categories and policy actions, but Core cannot decide whether a historical record remains legally or operationally acceptable.

## Provider package responsibilities

A production-capable signing provider should document:

- how key ID and key version are chosen for signing;
- whether key version is required or optional;
- how a verifier resolves historical key versions;
- whether signing permission and verification permission can be separated;
- how active, retired, revoked, expired, disabled, and unknown versions are surfaced;
- whether disabled or expired versions can still verify historical signatures;
- how provider timeouts and unavailable key stores map to `ProviderUnavailable`;
- how unsupported algorithms map to `UnsupportedAlgorithm`;
- whether revocation status is checked during verification;
- how host applications should configure retention and backups for historical verification material.

Provider packages should not return private keys, symmetric keys, credentials, access tokens, managed identity tokens, or raw secret material to Core. Provider packages should return only provider-neutral signing and verification metadata that is safe for persistence.

## Host responsibilities

Host applications remain responsible for operational key management:

- define the audit-retention period for signed governance artifacts;
- define how long retired verification material must remain available;
- configure the active signing key version;
- ensure new deployments use the intended key version;
- monitor verification outcomes after rotation;
- preserve provider diagnostics that are safe to retain;
- define emergency rotation and incident response procedures;
- decide whether high-risk workflows fail open, fail closed, defer, retry, escalate, or dead-letter.

Hosts should treat key rotation as an operational change that needs deployment validation. A successful build does not prove that old audit records remain verifiable.

## Verification policy connection

Issue #222 introduced verification result handling. Key lifecycle states should feed that policy model rather than bypass it.

Recommended mapping:

| Key lifecycle signal | Verification category | Suggested default |
| --- | --- | --- |
| Active key verifies successfully | `Valid` | `Allow` |
| Retired key verifies successfully within retention policy | `Valid` | `Allow` for audit review; host-specific for execution. |
| Unknown key ID or version | `UnknownKeyVersion` | `Escalate` or `Retry`. |
| Revoked or compromised version | `RevokedKey` | `Deny`, `DeadLetter`, or incident escalation. |
| Provider cannot resolve key registry | `ProviderUnavailable` | `Defer` or `Retry`; fail closed for high-risk execution. |
| Signature algorithm no longer supported | `UnsupportedAlgorithm` | `Deny` or escalate historical review. |
| Signing hash no longer matches artifact | `HashMismatch` | `Deny`, `DeadLetter`, and preserve forensic context. |

## Safe release wording

Safe wording:

- "Signed artifacts preserve key ID and key version metadata for later verification."
- "Hosts should keep retired key versions or public verification material available for the audit-retention period."
- "Retired keys may remain valid for historical verification when host policy accepts the signing window and verification succeeds."
- "Revoked or compromised keys map to explicit verification failure behavior."
- "Core remains provider-neutral and does not rotate or store keys."

Avoid wording such as:

- "AsiBackbone automatically rotates keys."
- "Retained keys guarantee legal non-repudiation."
- "Old records are always verifiable after rotation."
- "Revoked keys can still be trusted for audit evidence."
- "Key rotation makes the audit trail tamper-evident."

Use **verified** only when verification was explicitly performed. Use **tamper-evident** only when the deployed system also includes signing, verification, durable storage controls, hash chaining or equivalent integrity strategy, external anchoring if required, retention, monitoring, and incident response.

## Non-goals

This guidance does not implement:

- managed-key provider integration;
- Azure Key Vault, HSM, cloud KMS, local certificate-store, or key-file support;
- automatic key rotation;
- automatic key revocation or compromise detection;
- legal or compliance validation of retention practices;
- immutable storage or external anchoring;
- audit-chain or Merkle integrity verification.
