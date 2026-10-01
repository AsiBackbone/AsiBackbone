# ASIB902: Legacy Signature Input Compatibility

`ASIB902` is reported when source code calls `GovernanceSignatureInput.CreateLegacy(string)`, the public factory for the pre-6.0 hash-only signature input.

The method remains callable in the `7.x` line. `ASIB902` is emitted as a compiler warning, but projects that enable `TreatWarningsAsErrors` (or otherwise promote warnings to errors) will fail to build until the affected call sites are migrated or the diagnostic is narrowly suppressed. AsiBackbone itself enables `TreatWarningsAsErrors`, so consumers using the same policy should treat this warning as an upgrade-blocking migration item.

Verifying historical artifacts is **not** deprecated. `VerificationPolicyContext.WithLegacySignatureInputAllowed()` remains a supported opt-in with no planned removal, because governance evidence signed before 6.0 must stay verifiable for its audit-retention period.

## Why `CreateLegacy` is deprecated

Since 6.0, AsiBackbone signs a versioned signature input created by `GovernanceSignatureInput.CreateV1(...)`. The version 1 input binds the canonical artifact descriptors, payload hash, and signing policy version/hash into the bytes that are signed. The legacy input authenticates only the payload hash.

Producing hash-only input for new artifacts would therefore preserve a weaker trust boundary: policy metadata can be carried beside the signature without being authenticated by it. Code that verifies historical artifacts does not need `CreateLegacy`; `GovernanceArtifactVerifier` builds the legacy input itself when the verification context opts in.

## Migrate new signing code

Do not create hash-only signature input for new artifacts.

```csharp
CanonicalPayloadHash canonicalHash = CanonicalPayloadHasher.ComputeHash(payload);
ReadOnlyMemory<byte> signatureInput = GovernanceSignatureInput.CreateV1(
    canonicalHash,
    signingMetadata);
```

In normal package use, prefer `GovernanceArtifactSigner`; it supplies the version 1 `SigningRequest.SignatureInput` automatically. When you construct a `SigningRequest` or `SignatureVerificationRequest` yourself, set `SignatureInput`. A request without it falls back to the hash-only input.

## Verify historical artifacts

Current artifacts should use the ordinary verification context without enabling the legacy retry path:

```csharp
VerificationPolicyContext context = VerificationPolicyContext.Default;
```

or create the required key/provider/policy pins with `VerificationPolicyContext.Create(...)` and leave legacy fallback disabled.

To review or migrate artifacts signed before 6.0, opt in explicitly on the context you pass to `GovernanceArtifactVerifier`. No suppression is needed:

```csharp
VerificationPolicyContext historicalReview =
    VerificationPolicyContext.Default.WithLegacySignatureInputAllowed();
```

A legacy signature accepted through that path authenticates the canonical payload hash only. It cannot satisfy `ExpectedPolicyVersion` or `ExpectedPolicyHash`, because those labels were not part of the pre-6.0 signature input. Use the opt-in for historical review paths, not for new execution decisions.

## Removal target

`CreateLegacy` is planned for removal in `8.0`, subject to the repository's deprecation and major-release policy. It remains available throughout the required `7.x` deprecation window. Removing the public factory does not remove legacy verification: `GovernanceArtifactVerifier` keeps building the hash-only input internally for contexts that opt in.

Do not suppress `ASIB902` globally. Keep any suppression scoped to the smallest compatibility path that still needs to produce hash-only input, and remove it once that path is migrated.
