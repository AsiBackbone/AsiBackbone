# Regulated Storage and Signing Verification Checklist

Issue: #530.

This article gives regulated and high-assurance hosts a practical checklist for moving from a signed AsiBackbone governance artifact to a defensible verification, storage, retention, and incident-response process.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone provides provider-neutral canonicalization, hashing, signing, verification-policy, and audit-integrity seams. It does not provide immutable storage, WORM configuration, legal-hold administration, evidentiary certification, provider-specific key custody, or compliance approval by itself.

> [!IMPORTANT]
> A signature is one control in an assurance chain. A signed artifact is not automatically verified, append-only, immutable, legally admissible, or tamper-evident. Hosts must deploy and operate the remaining controls described below.

## Signed, verified, chained, and retained are different states

| State | What it establishes | What it does not establish |
| --- | --- | --- |
| Canonically hashed | A deterministic payload produced the recorded hash under the recorded schema and canonicalization versions. | That the payload was signed, verified, or durably retained. |
| Signed | A configured signing provider produced signature metadata for the canonical hash. | That the signature is valid, the key remains trusted, or storage is protected. |
| Verified | A configured verifier confirmed the signature against the expected hash, key reference, and policy context. | That other records were not deleted, reordered, replaced, or rewritten. |
| Hash-chained | Ordered records are linked so later verification can detect many missing, reordered, forked, or modified-record conditions. | That a privileged actor cannot rewrite both records and locally stored chain state. |
| Append-only or WORM retained | The host configured storage controls intended to prevent or constrain rewrite and deletion during a retention period. | That canonicalization, signatures, key status, chain continuity, or legal requirements were correct. |
| Externally anchored | A digest, chain tip, root, or receipt was placed in a separately controlled system. | That every local record is valid or that legal admissibility is guaranteed. |

Use **tamper-evident** only when the deployed design combines the controls appropriate to the host's risk and regulatory boundary and the host has tested the complete verification process.

## End-to-end assurance chain

A regulated deployment should define and test this sequence:

```text
Build minimized governance artifact
  -> canonicalize with recorded schema and canonicalization versions
  -> compute and retain canonical payload hash
  -> sign the hash through a protected provider boundary
  -> retain signature, algorithm, provider, key ID, key version, and signed timestamp
  -> persist artifact and signing metadata durably
  -> verify signature and policy context explicitly
  -> append and verify integrity-chain metadata when chaining is required
  -> apply host-owned append-only, immutable, or WORM controls when required
  -> retain records, verification material, chain state, and legal-hold metadata
  -> monitor failures and execute incident-response procedures
```

No individual step should be described as providing the guarantees of the entire chain.

## Regulated-host implementation checklist

### 1. Canonical payload and hashing

- [ ] Define the exact artifact type, artifact ID, schema version, and canonicalization version.
- [ ] Minimize and classify metadata before it enters the canonical payload.
- [ ] Record deterministic property-order, timestamp, null, collection-order, and normalization rules.
- [ ] Compute the canonical hash with an algorithm approved by the host security program.
- [ ] Persist the hash algorithm and canonicalization version with the artifact.
- [ ] Prove that the payload can be reconstructed during later audit review.
- [ ] Treat schema or canonicalization changes as reviewed compatibility events.

### 2. Signature creation

- [ ] Sign the precomputed canonical hash rather than an ad hoc runtime object representation.
- [ ] Use a host-selected managed-key, HSM, enterprise KMS, certificate, or equivalent protected provider appropriate to the deployment.
- [ ] Restrict signing permissions by host identity, environment, purpose, and least privilege.
- [ ] Retain only provider-neutral references and signature metadata in AsiBackbone records.
- [ ] Do not persist private keys, symmetric secrets, credentials, access tokens, or managed-identity tokens in audit metadata.
- [ ] Define whether signing failure denies, defers, escalates, requires acknowledgment, or routes to a lower-assurance path.

### 3. Signing metadata retention

Retain at least:

- [ ] artifact ID and artifact type;
- [ ] payload schema version and canonicalization version;
- [ ] signing hash and hash algorithm;
- [ ] signature value and signature algorithm;
- [ ] provider descriptor;
- [ ] logical key ID and exact key version;
- [ ] signed timestamp;
- [ ] applicable policy version and policy hash when they form part of the verification context.

Do not overwrite original signing metadata during key rotation, re-verification, archival notarization, or incident review. Store later verification or archival attestations separately.

### 4. Signature verification

- [ ] Reconstruct the canonical payload using the recorded schema and canonicalization versions.
- [ ] Recompute the canonical hash from the retained artifact.
- [ ] Compare the recomputed hash with the signing hash before cryptographic verification.

`GovernanceArtifactVerifier.VerifyAsync` performs that recompute-and-compare step itself and fails closed before calling the
provider, so the three boxes above are satisfied for artifacts verified through it. A host still owns them when it reconstructs
a payload outside that call. Use `SignedGovernanceArtifacts.Rehydrate` rather than `FromSigningMetadata` when rebuilding a
signed artifact from storage or a queue, so a stored payload and stored hash that disagree are rejected at construction.

- [ ] Resolve the exact provider, key ID, and key version recorded at signing time.
- [ ] Check active, retired, revoked, disabled, expired, and unknown key states according to host policy.
- [ ] Verify the signature through `IAsiBackboneSignatureVerificationService` or the host's provider-specific implementation.
- [ ] Apply `VerificationPolicyOptions` and the appropriate `VerificationPolicyContext`.
- [ ] Persist the verification attempt, category, action, timestamp, and safe failure code separately from signing metadata.
- [ ] Reverify samples after key rotation, provider migration, restore testing, or canonicalization changes.

`GovernanceArtifactVerifier` already provides the provider-neutral preflight and policy-mapping helper. It checks missing signing metadata, hash mismatches, hash-algorithm mismatches, canonical artifact descriptors, expected key ID or version, provider expectations, and policy context before calling the configured verifier.

```csharp
VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
    signedArtifact,
    verificationService,
    VerificationPolicyOptions.Default,
    VerificationPolicyContext.Create(
        purpose: signedArtifact.ArtifactType,
        expectedKeyId: "audit-key",
        expectedKeyVersion: "2026-06",
        expectedPolicyHash: "policy-hash",
        requiredHashAlgorithm: "SHA-256"),
    cancellationToken);

if (!outcome.ShouldAllow)
{
    // Apply the host's deny, defer, retry, acknowledge, escalate,
    // dead-letter, quarantine, and incident-response policy.
}
```

The helper does not resolve provider-specific keys, configure revocation services, preserve historical verification material, or create immutable storage.

### 5. Key rotation, retirement, and revocation

- [ ] Preserve the exact key version used for each artifact.
- [ ] Keep retired verification material available for the full applicable retention period.
- [ ] Separate permission to sign new artifacts from permission to verify historical artifacts where supported.
- [ ] Document normal rotation cadence and emergency rotation procedures.
- [ ] Define the approved signing window for each key version.
- [ ] Test verification of artifacts signed by both active and retired versions.
- [ ] Maintain an authoritative key-status source that can report revoked, disabled, expired, and unknown versions.
- [ ] Identify and quarantine artifacts affected by suspected key compromise.
- [ ] Do not silently trust signatures produced by a revoked key version.

### 6. Append-only storage expectations

- [ ] Select a durable system of record owned or explicitly designated by the host.
- [ ] Prevent application identities from updating or deleting finalized records unless the workflow explicitly requires a controlled correction model.
- [ ] Prefer append plus superseding/correction records over in-place mutation.
- [ ] Preserve original event time, record time, signer time, and verification time as separate values when applicable.
- [ ] Enforce least-privilege read, append, administration, retention, and break-glass roles.
- [ ] Record and review privileged storage operations.
- [ ] Test backup, restore, point-in-time recovery, and verification after restore.
- [ ] Ensure the outbox or external projection is not the only retained source of truth unless deliberately designed as authoritative.

"Append-only" is an operational and authorization property of the deployed storage design. An API named `AppendAsync` does not by itself make the backing store immutable.

### 7. Hash-chain validation when used

- [ ] Define chain IDs that match tenant, jurisdiction, provider, date, or retention boundaries.
- [ ] Persist record hash, sequence number, previous-link hash, link hash, and chain metadata.
- [ ] Persist the record and integrity link in one durable unit when possible.
- [ ] Protect chain-tip or expected-root state from silent rewrite.
- [ ] Run `AuditIntegrityVerifier.Verify(...)` against ordered retained links.
- [ ] Detect and route missing, reordered, duplicate, forked, wrong-chain, modified-link, and unsupported-algorithm outcomes.
- [ ] Decide whether every outbox state transition, only initial enqueue, or separate lifecycle events participate in the chain.
- [ ] Preserve verification results and forensic context when continuity fails.

A valid local hash chain demonstrates continuity for the supplied records and expected chain state. It does not prove that an administrator never replaced both the records and the locally stored chain tip.

### 8. Immutable or WORM storage options

Where policy requires immutability or Write Once Read Many retention, the host should evaluate provider-specific controls such as:

- object-lock or immutable-blob retention modes;
- database ledger or append-only features;
- storage-account, bucket, or vault retention policies;
- separately controlled archive tiers;
- trusted timestamp or external anchoring services;
- administrative separation and multi-party approval for retention changes;
- monitored break-glass access.

For any selected option:

- [ ] Document whether retention is governance mode, compliance mode, soft lock, or equivalent provider-specific behavior.
- [ ] Test the actual service configuration rather than relying on design documentation alone.
- [ ] Record who can shorten retention, delete archives, disable immutability, or use break-glass procedures.
- [ ] Validate region, residency, encryption, backup, replication, and recovery behavior.
- [ ] Preserve configuration evidence and change history for the review period.

AsiBackbone does not select, configure, or certify these provider-specific controls.

### 9. Retention and legal hold

- [ ] Map artifact categories to retention schedules approved by the host organization.
- [ ] Preserve signed artifacts, signing metadata, verification results, key-version history, chain metadata, and provider configuration evidence for compatible periods.
- [ ] Ensure historical verification material remains available for at least as long as the records that depend on it.
- [ ] Define legal-hold initiation, scope, release, authorization, and audit procedures.
- [ ] Prevent normal purge jobs from deleting held records or required verification material.
- [ ] Define how privacy, minimization, deletion, records-management, and legal-hold obligations interact.
- [ ] Test retention expiration and legal-hold exceptions in a non-production environment.

Retention and legal-hold requirements are jurisdictional and organizational matters. This checklist does not provide legal advice or determine the correct retention period.

### 10. Monitoring and periodic verification

- [ ] Alert on signing failures and missing signatures where signatures are required.
- [ ] Alert on hash mismatch, invalid signature, revoked key, unknown key version, unsupported algorithm, and provider unavailability.
- [ ] Alert on chain discontinuity, unexpected sequence reuse, missing chain tips, and verification backlog growth.
- [ ] Monitor storage retention-policy changes and privileged deletion or mutation attempts.
- [ ] Schedule periodic sample verification and full-chain verification according to risk.
- [ ] Reverify after restore, migration, provider change, key rotation, or incident response.
- [ ] Retain safe operational evidence without copying signature secrets or sensitive payload fields into logs.

## Failure handling matrix

| Condition | AsiBackbone category or signal | Regulated-host response |
| --- | --- | --- |
| Signature and policy context verify | `Valid` | Mark verified for the specific review context; continue only if all non-signature policy checks also pass. |
| Canonical or signing hash mismatch | `HashMismatch` | Deny trust, quarantine the artifact, preserve original bytes and metadata, alert, and investigate serialization drift or modification. |
| Required signature is missing | `MissingSignature` | Fail closed, dead-letter, or require governed acknowledgment according to the documented assurance tier. Never relabel the artifact as verified. |
| Key ID or version cannot be resolved | `UnknownKeyVersion` | Escalate or retry key resolution; preserve the artifact and do not treat it as verified. |
| Key version is revoked or compromised | `RevokedKey` | Deny or dead-letter new trust decisions, identify affected records, preserve forensic context, and execute compromised-key response. |
| Verification provider is unavailable | `ProviderUnavailable` | Defer or retry lower-risk review; fail closed for high-risk execution according to host policy. |
| Canonicalization or policy descriptors do not match | `CanonicalizationMismatch` | Escalate as schema, policy, or serialization drift before relying on the record. |
| Signature algorithm is unsupported | `UnsupportedAlgorithm` | Deny new trust decisions and route historical review through the approved exception process. |

## Incident response checklist

When an invalid signature, hash mismatch, missing metadata, unexpected key version, revoked key, broken chain, or storage-control failure is detected:

1. Stop automated trust or emission for the affected artifact class when risk requires it.
2. Preserve the original artifact, canonical payload, signing metadata, verification result, provider response, chain context, and relevant storage logs.
3. Avoid rewriting the affected record as part of investigation.
4. Determine whether the event is isolated, caused by canonicalization drift, caused by provider or key configuration, or indicates modification or deletion.
5. Identify all records that share the key version, policy version, canonicalization version, storage boundary, or chain segment.
6. Rotate, disable, or revoke signing material according to the host's key-compromise procedure.
7. Reverify retained records using an approved and documented method.
8. Quarantine or dead-letter affected downstream emissions and prevent silent replay.
9. Document the scope, decisions, limitations, remediation, and remaining trust assumptions.
10. Notify security, records-management, legal, compliance, privacy, and affected service owners according to organizational policy.

## Consumer verification: NuGet packages versus runtime artifacts

Package-consumption verification and runtime governance-artifact verification are separate processes.

### NuGet package intake

For AsiBackbone packages, consumers should:

- obtain packages from NuGet.org or the expected GitHub Actions release artifact set;
- confirm the expected `AsiBackbone.*` package ID, version, and target framework assets;
- review license, project URL, repository URL, repository type, and repository commit metadata;
- validate Source Link repository commit metadata after publication;
- review package SBOMs, `sbom-manifest.json`, recorded hashes, and provenance attestations when produced;
- ensure package, SBOM, manifest, and provenance evidence belong to the same release workflow or event;
- perform organization-specific dependency, vulnerability, source, cache, and approval checks;
- understand that NuGet package signing is currently deferred unless a later release explicitly changes that posture.

These checks establish package-source and release evidence. They do not verify signatures on runtime audit records.

### Runtime governance artifacts

For audit records, outbox records, lifecycle events, or governance emission artifacts, consumers should:

- load the retained artifact and its recorded signing metadata;
- rebuild the canonical payload under the recorded schema and canonicalization versions;
- recompute and compare the canonical hash;
- resolve the recorded provider, key ID, and exact key version;
- check key lifecycle and revocation status;
- verify the signature through the configured verification provider;
- apply host verification policy;
- validate the relevant integrity chain when chaining is used;
- confirm the record remains inside the approved append-only, immutable, WORM, retention, and legal-hold boundary;
- preserve the verification result as separate review evidence.

A package may pass package-intake verification while a runtime artifact fails signature or chain verification. A runtime artifact may verify cryptographically while its storage or retention controls are inadequate. Treat each control independently.

## Evidence package for audit review

A host's review package may include:

- architecture and data-flow diagrams;
- canonicalization and schema specifications;
- approved hash and signature algorithms;
- key inventory, version history, rotation records, and revocation status;
- role and permission evidence for signing, verification, storage, retention, and break-glass access;
- sample successful and failed verification results;
- chain-validation reports where chaining is used;
- immutable/WORM configuration and retention-policy evidence where configured;
- backup, restore, migration, and post-restore verification results;
- monitoring, alert, dead-letter, and incident-response procedures;
- retention schedules and legal-hold procedures;
- package intake, SBOM, provenance, and Source Link verification evidence for deployed dependencies.

The existence of this evidence does not by itself create certification. It gives the host a traceable basis for its own security, compliance, records-management, and legal review.

## Safe wording

Safe wording:

- "The artifact was canonicalized and signed by the configured provider."
- "The signature verified against the recorded hash, key version, and policy context."
- "The supplied audit chain verified for the expected chain ID and retained links."
- "The host stores records under its configured append-only or WORM retention controls."
- "The deployment combines signing, verification, chain validation, storage, retention, monitoring, and incident response."

Avoid wording such as:

- "Signing makes the ledger immutable."
- "The database is tamper-proof because records carry signatures."
- "A valid signature proves the complete event history."
- "The package is signed" when NuGet package signing remains deferred.
- "AsiBackbone provides legal non-repudiation or regulatory certification."
- "WORM storage guarantees admissibility in every jurisdiction."

## Related documentation

- [3.0.0 Consumer Verification Guide](consumer-verification-300.md)
- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Signed Audit and Outbox Records](signed-audit-and-outbox-records.md)
- [Verification Policy and Result Handling](verification-policy-and-result-handling.md)
- [Key Rotation and Retired-Key Verification](key-rotation-and-retired-key-verification.md)
- [Audit Integrity Chain Model](audit-integrity-chain-model.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Production Managed-Key Integration Guide](production-managed-key-integration.md)
- [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md)
- [Supply-Chain Provenance and Package SBOMs](supply-chain-provenance.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
