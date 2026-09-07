using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityTokens;

/// <summary>
/// Tests that capability grant proof validation binds the signature to the grant whose fields are evaluated.
/// </summary>
/// <remarks>
/// The validator evaluates issuer, audience, scopes, expiry, and bindings against the grant object. These tests cover the
/// step that establishes the proof describes that grant, rather than some other artifact whose signature happens to verify.
/// </remarks>
public sealed class CapabilityGrantProofBindingTests
{
    private static readonly DateTimeOffset IssuedUtc = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that an untampered grant passes proof validation.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncAllowsGrantWhoseProofCoversTheGrant()
    {
        CapabilityTokenGrant grant = CreateGrant();

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            CreateSignedGrant(grant),
            CapabilityGrantValidationOptions.Create(audience: "gateway-1", requireProof: true, validationUtc: IssuedUtc.AddMinutes(1)),
            new AlwaysValidVerificationService(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    /// <summary>
    /// Verifies that widening scopes after signing is denied, even though the retained signature still verifies.
    /// </summary>
    /// <remarks>
    /// This is the privilege-escalation shape the binding check exists to stop. Before the check, the validator evaluated
    /// the widened scopes while the proof covered the original, narrower grant.
    /// </remarks>
    [Fact]
    public async Task ValidateAsyncDeniesGrantWhoseScopesWereWidenedAfterSigning()
    {
        SignedGovernanceArtifact<CapabilityTokenGrant> signed = CreateSignedGrant(CreateGrant());
        CapabilityTokenGrant widenedGrant = CreateGrant(scopes: ["robotics.execute", "robotics.admin"]);

        var tampered = SignedGovernanceArtifacts.FromSigningMetadata(
            widenedGrant,
            CanonicalPayloadBuilder.ForCapabilityTokenGrant(widenedGrant),
            signed.CanonicalHash,
            signed.SigningMetadata);

        var verifier = new AlwaysValidVerificationService();

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            tampered,
            CapabilityGrantValidationOptions.Create(audience: "gateway-1", requireProof: true, validationUtc: IssuedUtc.AddMinutes(1)),
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(CapabilityTokenValidationCategory.InvalidProof, result.Category);
        Assert.Equal(VerificationPolicyAction.Deny, result.Action);
        Assert.Equal("capability.proof-content-mismatch", result.FailureCode);
        Assert.False(verifier.WasCalled);
    }

    /// <summary>
    /// Verifies that extending expiry after signing is denied.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncDeniesGrantWhoseExpiryWasExtendedAfterSigning()
    {
        SignedGovernanceArtifact<CapabilityTokenGrant> signed = CreateSignedGrant(CreateGrant());
        CapabilityTokenGrant extendedGrant = CreateGrant(expiresUtc: IssuedUtc.AddDays(30));

        var tampered = SignedGovernanceArtifacts.FromSigningMetadata(
            extendedGrant,
            CanonicalPayloadBuilder.ForCapabilityTokenGrant(extendedGrant),
            signed.CanonicalHash,
            signed.SigningMetadata);

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            tampered,
            CapabilityGrantValidationOptions.Create(audience: "gateway-1", requireProof: true, validationUtc: IssuedUtc.AddMinutes(1)),
            new AlwaysValidVerificationService(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal("capability.proof-content-mismatch", result.FailureCode);
    }

    /// <summary>
    /// Verifies that a proof issued for a different artifact type cannot be presented alongside a grant.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncDeniesProofIssuedForADifferentArtifactType()
    {
        CapabilityTokenGrant grant = CreateGrant();
        CanonicalPayload foreignPayload = CanonicalPayload.Create(
            CanonicalArtifactTypes.AuditResidue,
            grant.TokenId,
            grant.SchemaVersion,
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            new Dictionary<string, object?> { ["tokenId"] = grant.TokenId });

        CanonicalPayloadHash foreignHash = CanonicalPayloadHasher.ComputeHash(foreignPayload);

        var artifact = SignedGovernanceArtifacts.FromSigningMetadata(
            grant,
            foreignPayload,
            foreignHash,
            CreateSigningMetadata(foreignHash));

        var verifier = new AlwaysValidVerificationService();

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            artifact,
            CapabilityGrantValidationOptions.Create(audience: "gateway-1", requireProof: true, validationUtc: IssuedUtc.AddMinutes(1)),
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(VerificationPolicyAction.Deny, result.Action);
        Assert.Equal("capability.proof-artifact-type-mismatch", result.FailureCode);
        Assert.False(verifier.WasCalled);
    }

    /// <summary>
    /// Verifies that a proof bound to a different token identifier cannot be presented alongside this grant.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncDeniesProofBoundToADifferentTokenIdentifier()
    {
        CapabilityTokenGrant otherGrant = CreateGrant(tokenId: "grant-other");
        CapabilityTokenGrant grant = CreateGrant();

        CanonicalPayload otherPayload = CanonicalPayloadBuilder.ForCapabilityTokenGrant(otherGrant);
        CanonicalPayloadHash otherHash = CanonicalPayloadHasher.ComputeHash(otherPayload);

        var artifact = SignedGovernanceArtifacts.FromSigningMetadata(
            grant,
            otherPayload,
            otherHash,
            CreateSigningMetadata(otherHash));

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            artifact,
            CapabilityGrantValidationOptions.Create(audience: "gateway-1", requireProof: true, validationUtc: IssuedUtc.AddMinutes(1)),
            new AlwaysValidVerificationService(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal("capability.proof-artifact-id-mismatch", result.FailureCode);
    }

    private static CapabilityTokenGrant CreateGrant(
        string tokenId = "grant-proof-binding",
        IEnumerable<string>? scopes = null,
        DateTimeOffset? expiresUtc = null)
    {
        return CapabilityTokenGrant.Create(
            tokenId: tokenId,
            issuer: "issuer-1",
            audience: "gateway-1",
            scopes: scopes ?? ["robotics.execute"],
            issuedUtc: IssuedUtc,
            expiresUtc: expiresUtc ?? IssuedUtc.AddMinutes(10),
            policyVersion: "policy-v1",
            policyHash: "policy-hash");
    }

    private static SignedGovernanceArtifact<CapabilityTokenGrant> CreateSignedGrant(CapabilityTokenGrant grant)
    {
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityTokenGrant(grant);
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);

        return SignedGovernanceArtifacts.FromSigningMetadata(grant, payload, hash, CreateSigningMetadata(hash));
    }

    private static SigningMetadata CreateSigningMetadata(CanonicalPayloadHash hash)
    {
        return SigningMetadata.Create(
            signingHash: hash.HashValue,
            hashAlgorithm: hash.HashAlgorithm,
            signature: "fake-signature",
            signatureAlgorithm: "FAKE-SIGNATURE-V1",
            keyId: "key-1",
            keyVersion: "v1",
            provider: "fake-provider",
            signedUtc: IssuedUtc);
    }

    private sealed class AlwaysValidVerificationService : IAsiBackboneSignatureVerificationService
    {
        public bool WasCalled { get; private set; }

        public ValueTask<SignatureVerificationResult> VerifyAsync(
            SignatureVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;

            return ValueTask.FromResult(SignatureVerificationResult.Verified());
        }
    }
}
