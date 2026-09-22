# Capability Proof Trust Pinning

Capability-grant proof validation can optionally narrow the signing authority accepted for a specific execution path. This is useful when a host verification service trusts multiple signing keys, key versions, or providers, but a particular capability boundary must accept grants from only one approved authority.

Configure trust pins through `CapabilityGrantValidationOptions.Create(...)`:

```csharp
CapabilityGrantValidationOptions options = CapabilityGrantValidationOptions.Create(
    issuer: "policy-engine",
    audience: "robotics-gateway",
    scopes: ["robotics.execute"],
    policyVersion: "policy-v1",
    policyHash: "policy-hash",
    requireProof: true,
    expectedProofKeyId: "capability-signing-key",
    expectedProofKeyVersion: "v3",
    expectedProofPolicyVersion: "policy-v1",
    expectedProofPolicyHash: "policy-hash",
    requiredProofProvider: "azure-key-vault",
    requiredProofHashAlgorithm: "SHA-256");
```

All proof trust-pin settings are optional. Omitting them preserves the previous behavior in which any proof accepted by the configured verification service can satisfy proof validation.

When supplied, the validator creates a provider-neutral `VerificationPolicyContext` and passes it to `GovernanceArtifactVerifier.VerifyAsync(...)`. Key ID, key version, provider, hash algorithm, and proof policy metadata are checked before the verification provider is invoked. A cryptographically valid signature is therefore not sufficient when its signing metadata does not match the configured capability-proof expectations.

## Policy metadata boundary

Grant policy expectations and signing policy expectations are deliberately separate:

- `PolicyVersion` and `PolicyHash` validate the fields carried by `CapabilityGrant`.
- `ExpectedProofPolicyVersion` and `ExpectedProofPolicyHash` validate `policy_version` and `policy_hash` in the signed proof metadata.

A host can validate either boundary independently or pin both when the grant payload and signing envelope are expected to carry the same policy identity. Keeping the settings separate preserves backward compatibility for signed grants whose proof metadata does not contain policy fields.

## Production guidance

For consequential execution paths, pin the narrowest authority appropriate to the deployment. Prefer an explicit key ID and key version during rotations, constrain the expected provider when multiple provider adapters are registered, and pin the hash algorithm according to host policy. Treat mismatches as trust-policy failures rather than retrying with broader authority.

Trust pinning does not replace certificate lifecycle management, key revocation, host authorization, durable audit storage, or provider-specific verification controls. It narrows which otherwise valid signing metadata is acceptable for a capability-grant validation context.
