# Verification Policy and Result Handling

Issue: #222.

This article documents the provider-neutral verification policy APIs and result handling for signed AsiBackbone governance artifacts.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone provides governance primitives and verification seams. It does not provide provider-specific key lookup, immutable storage, external anchoring, automatic compliance guarantees, legal evidence guarantees, or tamper-evidence by itself.

> [!IMPORTANT]
> A signed artifact is not verified until a verifier confirms the expected hash, signature metadata, key reference, and policy context. Verification must be explicit and policy-driven.

## Signed is not verified

Signing and verification are separate states.

| State | Meaning | Safe statement |
| --- | --- | --- |
| Signed | A signing provider attached signature metadata to a canonical artifact hash. | "The artifact carries provider-neutral signing metadata." |
| Verification attempted | A verifier checked the expected artifact hash and signing metadata. | "Verification was attempted and produced an explicit result." |
| Verified | The verifier confirmed the signature against the expected artifact hash and metadata. | "The artifact verified against the configured verifier and policy context." |
| Policy accepted | Host verification policy mapped the result to an allow action. | "The host policy allowed this verified artifact to proceed." |

Do not treat `IsSigned` as permission to execute, emit, or trust a high-assurance audit record. Use verification policy to decide the next action.

## Verification categories

`SignatureVerificationCategory` gives hosts stable categories that are safe to log and route.

| Category | Meaning | Typical default action |
| --- | --- | --- |
| `Valid` | The verifier confirmed the signature. | `Allow` |
| `InvalidSignature` | Signature value was present but did not verify. | `Deny` |
| `HashMismatch` | Expected hash does not match signing metadata or verifier expectation. | `Deny` |
| `MissingSignature` | Required signature metadata is missing. | `RequireAcknowledgment` |
| `UnknownKeyVersion` | Key ID or key version cannot be resolved or does not match policy expectation. | `Escalate` |
| `RevokedKey` | Key is revoked, disabled, or no longer trusted. | `Deny` |
| `ProviderUnavailable` | Verification provider could not complete verification. | `Defer` |
| `CanonicalizationMismatch` | Artifact descriptors or policy context do not match the artifact being verified. | `Escalate` |
| `UnsupportedAlgorithm` | Hash or signature algorithm is unsupported by verifier or policy. | `Deny` |
| `Failed` | Verification failed without a more specific category. | `Escalate` |

These categories do not require Core to know how a provider resolves keys. Provider-specific lookup remains outside Core.

Verification providers should attach a category when creating failures so policy does not depend on free-form failure-code text:

```csharp
return SignatureVerificationResult.Failed(
    "provider.key-unavailable",
    SignatureVerificationCategory.RevokedKey,
    "The configured verification key was deleted.");
```

`VerificationPolicyEvaluator` gives the explicit category precedence over the failure code. The overload of `Failed` without a category remains available for compatibility and uses the established case-insensitive failure-code rules before falling back to `Failed`. New providers should use the categorized overload.

## Host policy actions

`VerificationPolicyAction` maps verification categories to host decisions:

| Action | Recommended use |
| --- | --- |
| `Allow` | Continue execution, emission, or review because verification succeeded and policy accepts it. |
| `Deny` | Stop execution or emission because verification failed in a high-risk way. |
| `Defer` | Wait for later verification, usually when a provider is temporarily unavailable. |
| `RequireAcknowledgment` | Continue only after an explicit human or workflow acknowledgment. |
| `Escalate` | Route to an operator, reviewer, or host-defined governance process. |
| `Retry` | Retry verification or downstream handling according to host retry policy. |
| `DeadLetter` | Move the item to dead-letter handling for later review or repair. |

Hosts can override defaults with `VerificationPolicyOptions.Create(...)`.

```csharp
VerificationPolicyOptions options = VerificationPolicyOptions.Create(
    new Dictionary<SignatureVerificationCategory, VerificationPolicyAction>
    {
        [SignatureVerificationCategory.MissingSignature] = VerificationPolicyAction.DeadLetter,
        [SignatureVerificationCategory.ProviderUnavailable] = VerificationPolicyAction.Retry
    });
```

## Verifying signed artifacts

`GovernanceArtifactVerifier` wraps the existing verification service with preflight checks and policy mapping.

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

switch (outcome.Action)
{
    case VerificationPolicyAction.Allow:
        // Continue execution, emission, or review.
        break;
    case VerificationPolicyAction.Defer:
    case VerificationPolicyAction.Retry:
        // Queue retry or defer the workflow.
        break;
    case VerificationPolicyAction.RequireAcknowledgment:
    case VerificationPolicyAction.Escalate:
        // Route through host governance workflow.
        break;
    case VerificationPolicyAction.Deny:
    case VerificationPolicyAction.DeadLetter:
        // Stop or dead-letter the request.
        break;
}
```

The wrapper performs provider-neutral preflight checks before calling the verifier:

- missing signature metadata;
- signing hash mismatch;
- hash algorithm mismatch;
- canonical artifact metadata mismatch;
- expected key ID or key version mismatch;
- required provider mismatch;
- expected policy version or policy hash mismatch.

Provider-specific cryptographic verification still happens through `IAsiBackboneSignatureVerificationService`.

## Recommended verification points

### Before execution

Verify before consequential execution when a capability token, acknowledgment, or signed audit artifact is required to authorize the action.

Recommended default behavior:

| Result category | Execution response |
| --- | --- |
| `Valid` | Allow if the rest of policy also allows. |
| `MissingSignature` | Require acknowledgment or deny for high-risk workflows. |
| `InvalidSignature`, `HashMismatch`, `RevokedKey`, `UnsupportedAlgorithm` | Deny and alert. |
| `ProviderUnavailable` | Defer or fail closed depending on risk. |
| `UnknownKeyVersion`, `CanonicalizationMismatch`, `Failed` | Escalate. |

### Before high-assurance emission

Verify before emitting records to high-assurance governance channels when downstream systems may treat the artifact as trusted.

Recommended default behavior:

| Result category | Emission response |
| --- | --- |
| `Valid` | Emit and preserve verification outcome metadata. |
| `ProviderUnavailable` | Retry or defer emission. |
| `MissingSignature` | Dead-letter or route to lower-assurance channel, depending on policy. |
| `InvalidSignature`, `HashMismatch`, `RevokedKey`, `UnsupportedAlgorithm` | Dead-letter and alert. |
| `UnknownKeyVersion`, `CanonicalizationMismatch`, `Failed` | Escalate before emission. |

### During audit review

Verify during audit review before treating signed artifacts as reliable evidence of system behavior.

Recommended default behavior:

| Result category | Audit review response |
| --- | --- |
| `Valid` | Mark the artifact verified for the review context. |
| `MissingSignature` | Mark as unsigned or signing-ready only. |
| `ProviderUnavailable` | Mark verification pending and retry later. |
| `UnknownKeyVersion` | Resolve historical key material or escalate. |
| `InvalidSignature`, `HashMismatch`, `RevokedKey`, `CanonicalizationMismatch`, `UnsupportedAlgorithm`, `Failed` | Treat as integrity concern and preserve forensic context. |

## Safe-to-log outcomes

`VerificationPolicyOutcome.SafeMetadata` is intended for structured logs and audit routing. It includes provider-neutral fields such as:

- artifact ID and type;
- verification category;
- selected policy action;
- status and failure code;
- signing hash and hash algorithm;
- key ID and key version;
- signature algorithm descriptor;
- provider descriptor;
- signed timestamp.

It excludes signature values and filters metadata keys that appear to contain signatures, secrets, tokens, credentials, or private key material.

## Core boundary and non-goals

Core remains provider-neutral. It does not:

- resolve provider-specific keys;
- query Azure Key Vault, HSM, KMS, certificate stores, transparency logs, blockchains, or object-lock storage;
- require every host to verify every artifact synchronously;
- guarantee compliance, legal evidence, or non-repudiation;
- make a signed audit trail tamper-evident by itself.

Use the phrase **verified** only when verification was explicitly performed and the policy outcome supports that statement. Use **tamper-evident** only when the deployed system also includes durable storage controls, hash chaining or equivalent integrity strategy, verification, and any required external anchoring.
