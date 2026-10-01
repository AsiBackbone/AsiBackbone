# ASIB902: Legacy Signature Input Compatibility

`ASIB902` is reported when source code calls either of the two public methods retained for pre-6.0 hash-only signature compatibility:

- `GovernanceSignatureInput.CreateLegacy(string)`
- `VerificationPolicyContext.WithLegacySignatureInputAllowed()`

Both methods remain callable in the `7.x` line. The warning is not an error. Their purpose is limited to reviewing or migrating artifacts produced before 6.0, when providers signed only the UTF-8 text of the canonical payload hash.

## Why these methods are deprecated

Since 6.0, AsiBackbone signs a versioned signature input created by `GovernanceSignatureInput.CreateV1(...)`. The version 1 input binds the canonical artifact descriptors, payload hash, and signing policy version/hash into the bytes that are signed. The legacy input authenticates only the payload hash.

Allowing the legacy path for current artifacts would therefore preserve a weaker trust boundary: policy metadata can be carried beside the signature without being authenticated by it.

## Migrate new signing code

Do not create hash-only signature input for new artifacts.

```csharp
CanonicalPayloadHash canonicalHash = CanonicalPayloadHasher.ComputeHash(payload);
ReadOnlyMemory<byte> signatureInput = GovernanceSignatureInput.CreateV1(
    canonicalHash,
    signingMetadata);
```

In normal package use, prefer `GovernanceArtifactSigner`; it supplies the version 1 `SigningRequest.SignatureInput` automatically.

## Migrate verification code

Current artifacts should use the ordinary verification context without enabling the legacy retry path:

```csharp
VerificationPolicyContext context = VerificationPolicyContext.Default;
```

or create the required key/provider/policy pins with `VerificationPolicyContext.Create(...)` and leave legacy fallback disabled.

Only a deliberate historical-review or migration workflow should temporarily suppress `ASIB902` and call `WithLegacySignatureInputAllowed()`:

```csharp
#pragma warning disable ASIB902 // Required only while reviewing pre-6.0 artifacts.
VerificationPolicyContext historicalReview =
    VerificationPolicyContext.Default.WithLegacySignatureInputAllowed();
#pragma warning restore ASIB902
```

A legacy signature accepted through that path authenticates the canonical payload hash only. It cannot satisfy `ExpectedPolicyVersion` or `ExpectedPolicyHash`, because those labels were not part of the pre-6.0 signature input.

## Removal target

The methods are planned for removal in `8.0`, subject to the repository's deprecation and major-release policy. They remain available throughout the required `7.x` deprecation window so consumers can migrate historical-artifact workflows without a source break in the current major line.

Do not suppress `ASIB902` globally. Keep any suppression scoped to the smallest historical compatibility path and remove it once pre-6.0 artifacts no longer need to be accepted.
