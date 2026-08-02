# Cryptographic Security Hardening Roadmap

This roadmap splits AsiBackbone cryptographic security hardening into implementation-sized work items.

Issue: #207.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a governance spine for consequential software decision flow. It is not an intelligence engine, signing appliance, key-management system, immutable ledger, blockchain product, compliance certification service, or legal evidence system by itself.

> [!IMPORTANT]
> Status note: the stable `3.2.x` package family includes released surfaces for canonical hashing/signing seams, provider-neutral signing and verification abstractions, local-development signing, managed-key adapter boundaries, signed audit/outbox metadata, and verification-policy/result handling. This roadmap is retained for traceability of the hardening sequence. Those released surfaces still do **not** create production tamper-evidence, immutability, legal non-repudiation, or compliance certification by themselves; production claims require concrete host storage, signing, verification, key-management, rotation, retention, and operational controls.

## Roadmap goals

The cryptographic hardening roadmap turns signing-ready abstractions into progressively stronger implementation surfaces without coupling `AsiBackbone.Core` to one provider or platform.

Primary goals:

- define deterministic canonical payloads for hashing and signing;
- add provider-neutral hashing, signing, verification, and integrity concepts;
- keep concrete key providers outside Core;
- sign selected audit and outbox artifacts when configured;
- verify signed artifacts before trust-sensitive use;
- support key rotation and retired-key verification;
- explore append-only hash-chain or Merkle-style audit integrity;
- harden capability-token integrity and reuse controls;
- preserve careful production wording around tamper-evidence, immutability, blockchain anchoring, legal effect, and compliance.

## Vocabulary boundary

Use these terms consistently across issues and documentation.

| Term | Meaning | Safe wording |
| --- | --- | --- |
| Signing-ready | Artifact has stable identifiers, schema/policy metadata, and fields suitable for hashing/signing. | "Records can carry signing-ready metadata." |
| Hashed | A deterministic canonical payload has been hashed. | "The artifact hash was computed using the configured canonicalization rules." |
| Signed | A concrete signing provider signed the artifact hash and returned signing metadata. | "The artifact was signed by the configured provider." |
| Verified | A verifier confirmed the expected hash and signature metadata. | "The signature verified against the expected artifact hash and key reference." |
| Chained | Records are linked through previous-record hash, Merkle root, or equivalent chain metadata. | "Records are hash-linked by the configured chain strategy." |
| Externally anchored | A chain root or digest is published to a separately controlled timestamp, transparency, ledger, object-lock, or anchoring service. | "The chain is anchored to the configured external service." |
| Tamper-evident | A configured system can detect specified forms of modification through signing, verification, durable storage controls, and chain/anchor verification. | Use only after the concrete design exists and is documented. |

Do not use `tamper-proof`, `blockchain-backed`, `immutable`, `non-repudiable`, `legally certified`, or `compliance-approved` unless a specific implemented design supports that claim.

## Provider-neutral Core rule

Core may define artifact metadata, canonicalization contracts, signing and verification request/result contracts, policy-driven verification result handling, audit-integrity metadata shapes, capability-token validation abstractions, and safe failure categories.

Core should not own Azure Key Vault integration, HSM or cloud KMS integration, local key-file production handling, blockchain or external anchoring implementation, immutable storage providers, legal interpretation, compliance certification, host authentication, or host authorization.

Concrete provider work should live in provider packages, host integrations, samples, or documented future packages after boundary review.

## Child issue plan and status

| Order | Issue | Focus | Status in `1.1.x` documentation/package line |
| --- | --- | --- | --- |
| 1 | #219 | Canonical payload hashing and deterministic signing payloads. | Released provider-neutral surface and tests exist; not production tamper-evidence by itself. |
| 2 | #220 | Concrete signing-provider package boundary. | Released package-boundary documentation plus LocalDevelopment and ManagedKey provider surfaces. |
| 3 | #221 | Signing audit receipts and governance outbox records. | Released signing metadata/canonicalization support for selected governance artifacts. |
| 4 | #222 | Verification policy APIs and verification result handling. | Released verification-policy/result handling surface. |
| 5 | #223 | Key rotation and retired-key verification guidance. | Guidance/hardening path; host/provider-owned operational controls remain required. |
| 6 | #224 | Append-only hash-chain or Merkle audit integrity model. | Documented model/guidance; concrete immutable/external anchoring providers remain outside the stable package boundary. |
| 7 | #225 | Capability-token signing, validation, and reuse checks. | Capability-token primitives exist; production replay/custody/issuer validation remains host/provider-owned. |
| Done / parallel | #216 | Production security posture, non-goals, and careful wording. | Released documentation boundary. |

## Implementation phases

### Phase 1: Canonical payload foundation

Child issue: #219. Status: released provider-neutral surface in the stable `1.1.x` line.

Define how selected artifacts become deterministic payloads before hashing. Candidate artifacts include audit receipts, audit ledger records, audit residue lifecycle events, governance outbox entries, governance emission envelopes, and capability-token grants.

Minimum design decisions include canonical property order, UTC timestamp format, null handling, collection ordering, metadata allow-listing, schema version binding, artifact type binding, hash algorithm recording, and canonicalization version recording.

Exit criteria:

- repeated canonicalization produces the same hash for equivalent payloads;
- meaningful changes produce different hashes;
- hash metadata can be passed to the signing seam;
- documentation states that hashing is not signing and not tamper-evidence by itself.

### Phase 2: Signing provider boundary

Child issue: #220. Status: released provider-boundary documentation and provider packages in the stable `1.1.x` line.

Define where concrete signing implementations live. Keep Core provider-neutral, treat managed-key providers as separate package work, require key ID and key version to flow with signatures, and never expose private keys, symmetric keys, credentials, connection strings, or managed identity tokens to Core.

Exit criteria:

- package boundary and provider responsibilities are documented;
- concrete provider work does not leak into Core;
- local-development signing is explicitly marked non-production.

### Phase 3: Signed audit and outbox artifacts

Child issue: #221. Status: released signing metadata/canonicalization surfaces for selected governance artifacts in the stable `1.1.x` line.

Decide which artifacts can be signed and where signing occurs in the flow.

Recommended sequence:

```text
Decision / acknowledgment / capability event
  -> build audit residue or lifecycle event
  -> classify and minimize metadata
  -> canonicalize artifact
  -> compute artifact hash
  -> sign hash through configured provider
  -> attach signing metadata
  -> persist durable audit/outbox record
  -> optionally emit provider envelope
```

Exit criteria:

- selected audit and outbox artifacts can carry signature metadata;
- unsigned and signing-ready flows remain supported;
- documentation states that signed does not mean verified unless verification is performed.

### Phase 4: Verification policy

Child issue: #222. Status: released verification-policy and result-handling surfaces in the stable `1.1.x` line.

Verification should produce explicit outcomes that can be routed through policy.

Recommended result categories include valid, invalid signature, hash mismatch, missing signature where required, unknown key version, revoked or disabled key, unsupported algorithm, canonicalization mismatch, and verification provider unavailable.

Recommended policy behavior:

| Verification result | Typical policy response |
| --- | --- |
| Valid | Continue. |
| Invalid signature | Deny trust, quarantine, alert, and preserve forensic context. |
| Missing required signature | Defer, escalate, or dead-letter. |
| Unknown key version | Retry configured key resolution or escalate. |
| Revoked key | Follow incident policy and do not silently trust. |
| Provider unavailable | Retry, defer, fail closed, or escalate based on risk. |

Exit criteria:

- verification failure behavior is policy-driven;
- high-risk workflows do not silently accept unverifiable artifacts;
- documentation distinguishes signed from verified.

### Phase 5: Key rotation and retired-key verification

Child issue: #223. Status: guidance/hardening path; production behavior remains host/provider-owned.

Production signatures often outlive active signing keys. Retired keys or historical key material must remain resolvable for verification during the retention period.

Minimum guidance:

- preserve key ID and key version on every signed artifact;
- separate signing permission from verification permission where supported;
- document active, retired, revoked, disabled, expired, and unknown key states;
- keep historical verification material available for the retention period;
- define emergency rotation and compromised-key response.

Exit criteria:

- old records remain verifiable after normal rotation;
- revoked or compromised keys trigger explicit policy behavior;
- documentation avoids implying automatic key management.

### Phase 6: Audit-chain integrity

Child issue: #224. Status: documented model/guidance; concrete immutable storage and external anchoring providers remain outside the stable package boundary.

Evaluate append-only hash chains, per-stream chains, Merkle roots, or batch-root integrity models.

Candidate metadata includes chain ID, chain sequence, previous record hash, current record hash, root hash, canonicalization version, and chain verification status.

Design boundary:

- a local hash chain can detect modification only when verified against trusted chain state;
- a signed chain root strengthens integrity but still depends on key security;
- immutable storage and external anchoring are separate provider/host choices;
- blockchain-backed guarantees are not included unless a concrete provider is implemented.

Exit criteria:

- selected model can detect at least modified or missing records under documented assumptions;
- chain verification behavior is documented;
- documentation distinguishes chained records from externally anchored tamper-evidence.

### Phase 7: Capability-token hardening

Child issue: #225. Status: capability-token primitives exist; production issuer/custody/replay validation remains host/provider-owned.

Capability tokens should remain short-lived, scoped, and checked at the execution boundary.

Recommended checks include issuer, audience, expiration, not-before, scope, policy version/hash, acknowledgment or handshake reference, token ID or nonce, cancellation/revocation state, and reuse state for single-use or bounded-use workflows.

Exit criteria:

- token validation is explicit before governed execution;
- token failure maps to deny, defer, require acknowledgment, or escalate;
- token handling does not replace normal host authentication/authorization;
- Core remains provider-neutral.

## Relationship to existing work

Issue #147 and PR #206 introduced signing-ready abstractions and metadata boundaries. This roadmap treats that work as the seam, not the completed security model.

Issue #216 covers production security posture documentation and non-goals. That work should remain documentation-focused and should not be treated as a complete implementation of production signing, verification, key management, tamper-evidence, or immutable storage.

The roadmap continues the existing governance-spine sequence:

```text
Policy pipeline
  -> acknowledgment workflow
  -> audit residue
  -> capability boundary
  -> durable local/outbox persistence
  -> optional provider emission
  -> host-owned execution
```

Cryptographic hardening strengthens this flow but does not replace it.
