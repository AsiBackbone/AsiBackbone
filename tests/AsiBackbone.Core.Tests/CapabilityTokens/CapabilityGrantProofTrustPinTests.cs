using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityTokens;

/// <summary>
/// Tests capability-grant proof trust metadata pinning.
/// </summary>
public sealed class CapabilityGrantProofTrustPinTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 19, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that a cryptographically valid grant is rejected before provider verification when its signing key does not match the configured trust pin.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncRejectsCryptographicallyValidGrantWithUnexpectedProofKey()
    {
        SignedGovernanceArtifact<CapabilityTokenGrant> signedGrant = CreateSignedGrant(
            keyId: "key-2",
            keyVersion: "v1",
            provider: "fake-provider");
        var verifier = new StubVerificationService(SignatureVerificationResult.Verified());

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            CreateOptions(expectedProofKeyId: "key-1"),
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(verifier.WasCalled);
        AssertFailure(
            result,
            CapabilityTokenValidationCategory.InvalidProof,
            VerificationPolicyAction.Deny,
            "signature.key-not-trusted");
    }

    /// <summary>
    /// Verifies that a cryptographically valid grant is rejected before provider verification when its signing provider does not match the configured trust pin.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncRejectsCryptographicallyValidGrantWithUnexpectedProofProvider()
    {
        SignedGovernanceArtifact<CapabilityTokenGrant> signedGrant = CreateSignedGrant(
            keyId: "key-1",
            keyVersion: "v1",
            provider: "other-provider");
        var verifier = new StubVerificationService(SignatureVerificationResult.Verified());

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            CreateOptions(requiredProofProvider: "fake-provider"),
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(verifier.WasCalled);
        AssertFailure(
            result,
            CapabilityTokenValidationCategory.Failed,
            VerificationPolicyAction.Defer,
            "signature.provider-unavailable");
    }

    /// <summary>
    /// Verifies that matching proof trust metadata reaches the verification provider and allows the grant.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncAllowsGrantWithMatchingProofTrustMetadata()
    {
        SignedGovernanceArtifact<CapabilityTokenGrant> signedGrant = CreateSignedGrant(
            keyId: "key-1",
            keyVersion: "v1",
            provider: "fake-provider");
        var verifier = new StubVerificationService(SignatureVerificationResult.Verified());

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            CreateOptions(
                expectedProofKeyId: "key-1",
                expectedProofKeyVersion: "v1",
                requiredProofProvider: "fake-provider",
                requiredProofHashAlgorithm: signedGrant.HashAlgorithm),
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(verifier.WasCalled);
        Assert.True(result.IsValid);
        Assert.True(result.ShouldAllow);
    }

    private static CapabilityGrantValidationOptions CreateOptions(
        string? expectedProofKeyId = null,
        string? expectedProofKeyVersion = null,
        string? requiredProofProvider = null,
        string? requiredProofHashAlgorithm = null)
    {
        return CapabilityGrantValidationOptions.Create(
            issuer: "issuer-1",
            audience: "gateway-1",
            scopes: ["robotics.execute"],
            validationUtc: Now,
            policyVersion: "policy-v1",
            policyHash: "policy-hash",
            requireProof: true,
            expectedProofKeyId: expectedProofKeyId,
            expectedProofKeyVersion: expectedProofKeyVersion,
            requiredProofProvider: requiredProofProvider,
            requiredProofHashAlgorithm: requiredProofHashAlgorithm);
    }

    private static SignedGovernanceArtifact<CapabilityTokenGrant> CreateSignedGrant(
        string keyId,
        string keyVersion,
        string provider)
    {
        var grant = CapabilityTokenGrant.Create(
            tokenId: "grant-proof-pins",
            issuer: "issuer-1",
            audience: "gateway-1",
            scopes: ["robotics.execute"],
            issuedUtc: Now.AddMinutes(-5),
            expiresUtc: Now.AddMinutes(5),
            policyVersion: "policy-v1",
            policyHash: "policy-hash");

        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityTokenGrant(grant);
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);
        var signingMetadata = SigningMetadata.Create(
            signingHash: hash.HashValue,
            hashAlgorithm: hash.HashAlgorithm,
            signature: "fake-signature",
            signatureAlgorithm: "FAKE-SIGNATURE-V1",
            keyId: keyId,
            keyVersion: keyVersion,
            provider: provider,
            signedUtc: Now,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["policy_version"] = "policy-v1",
                ["policy_hash"] = "policy-hash"
            });

        return SignedGovernanceArtifacts.FromSigningMetadata(grant, payload, hash, signingMetadata);
    }

    private static void AssertFailure(
        CapabilityGrantValidationResult result,
        CapabilityTokenValidationCategory category,
        VerificationPolicyAction action,
        string failureCode)
    {
        Assert.False(result.IsValid);
        Assert.False(result.ShouldAllow);
        Assert.Equal(category, result.Category);
        Assert.Equal(action, result.Action);
        Assert.Equal(failureCode, result.FailureCode);
    }

    private sealed class StubVerificationService(SignatureVerificationResult result) : IAsiBackboneSignatureVerificationService
    {
        public bool WasCalled { get; private set; }

        public ValueTask<SignatureVerificationResult> VerifyAsync(
            SignatureVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;
            return ValueTask.FromResult(result);
        }
    }
}
