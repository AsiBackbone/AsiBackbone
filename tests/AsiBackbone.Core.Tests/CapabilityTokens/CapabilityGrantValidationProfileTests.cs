using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityTokens;

/// <summary>
/// Unit tests for the explicit capability-grant validation profiles.
/// </summary>
public sealed class CapabilityGrantValidationProfileTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 2, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that the execution-boundary profile requires proof and bounded-use validation by default.
    /// </summary>
    [Fact]
    public void CreateExecutionBoundaryRequiresProofAndBoundedUseByDefault()
    {
        var options = CapabilityGrantValidationOptions.CreateExecutionBoundary(
            issuer: " issuer-1 ",
            audience: " gateway-1 ",
            scopes: [" robotics.execute ", "robotics.execute"],
            validationUtc: Now,
            requiredProofProvider: " provider-1 ");

        Assert.Equal("issuer-1", options.Issuer);
        Assert.Equal("gateway-1", options.Audience);
        Assert.Collection(options.Scopes, scope => Assert.Equal("robotics.execute", scope));
        Assert.True(options.RequireProof);
        Assert.True(options.RequireUseCheck);
        Assert.False(options.RequireAcknowledgmentReference);
        Assert.Equal(1, options.MaxUseCount);
        Assert.Equal("provider-1", options.RequiredProofProvider);
    }

    /// <summary>
    /// Verifies that callers can explicitly disable bounded-use validation when another trusted boundary owns that responsibility.
    /// </summary>
    [Fact]
    public void CreateExecutionBoundaryAllowsCallerToMakeBoundedUseExplicitlyOptional()
    {
        var options = CapabilityGrantValidationOptions.CreateExecutionBoundary(
            requireUseCheck: false,
            maxUseCount: 4);

        Assert.True(options.RequireProof);
        Assert.False(options.RequireUseCheck);
        Assert.Equal(4, options.MaxUseCount);
    }

    /// <summary>
    /// Verifies that the metadata-validation profile intentionally disables proof and bounded-use checks.
    /// </summary>
    [Fact]
    public void CreateMetadataValidationDisablesProofAndUseChecks()
    {
        var options = CapabilityGrantValidationOptions.CreateMetadataValidation(
            issuer: "issuer-1",
            audience: "gateway-1",
            requireAcknowledgmentReference: true);

        Assert.False(options.RequireProof);
        Assert.False(options.RequireUseCheck);
        Assert.True(options.RequireAcknowledgmentReference);
        Assert.Equal(1, options.MaxUseCount);
    }

    /// <summary>
    /// Verifies that execution-boundary validation fails closed when proof verification is required but no verifier is available.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecutionBoundaryProfileDeniesWithoutProofVerifier()
    {
        SignedGovernanceArtifact<CapabilityTokenGrant> signedGrant = CreateSignedGrant(CreateGrant());

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            CapabilityGrantValidationOptions.CreateExecutionBoundary(validationUtc: Now),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(result.ShouldAllow);
        Assert.Equal(CapabilityTokenValidationCategory.MissingProof, result.Category);
        Assert.Equal(VerificationPolicyAction.Deny, result.Action);
        Assert.Equal("capability.proof-verifier-missing", result.FailureCode);
    }

    /// <summary>
    /// Verifies that execution-boundary validation defers when proof succeeds but the required bounded-use store is unavailable.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task ExecutionBoundaryProfileDefersWithoutUseStoreAfterProofPasses()
    {
        SignedGovernanceArtifact<CapabilityTokenGrant> signedGrant = CreateSignedGrant(CreateGrant());
        var verifier = new StubVerificationService();

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            CapabilityGrantValidationOptions.CreateExecutionBoundary(validationUtc: Now),
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(verifier.WasCalled);
        Assert.False(result.ShouldAllow);
        Assert.Equal(CapabilityTokenValidationCategory.ReplayStoreUnavailable, result.Category);
        Assert.Equal(VerificationPolicyAction.Defer, result.Action);
        Assert.Equal("capability.use-store-missing", result.FailureCode);
    }

    /// <summary>
    /// Verifies that metadata-only validation can succeed without a proof verifier or bounded-use store.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task MetadataValidationProfileAllowsWithoutProofVerifierOrUseStore()
    {
        SignedGovernanceArtifact<CapabilityTokenGrant> signedGrant = CreateSignedGrant(CreateGrant());

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            CapabilityGrantValidationOptions.CreateMetadataValidation(validationUtc: Now),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.True(result.ShouldAllow);
        Assert.Equal(CapabilityTokenValidationCategory.Valid, result.Category);
    }

    /// <summary>
    /// Verifies that the legacy no-options validator path retains its existing metadata-only behavior for 3.x compatibility.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task LegacyNoOptionsPathRetainsMetadataOnlyBehavior()
    {
        SignedGovernanceArtifact<CapabilityTokenGrant> signedGrant = CreateSignedGrant(CreateGrant());

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.True(result.ShouldAllow);
        Assert.Equal(CapabilityTokenValidationCategory.Valid, result.Category);
    }

    private static CapabilityTokenGrant CreateGrant()
    {
        return CapabilityTokenGrant.Create(
            tokenId: "grant-profile-1",
            issuer: "issuer-1",
            audience: "gateway-1",
            scopes: ["robotics.execute"],
            issuedUtc: Now.AddMinutes(-5),
            expiresUtc: Now.AddMinutes(5));
    }

    private static SignedGovernanceArtifact<CapabilityTokenGrant> CreateSignedGrant(CapabilityTokenGrant grant)
    {
        var payload = CanonicalPayload.Create(
            CanonicalArtifactTypes.CapabilityTokenGrant,
            grant.TokenId,
            grant.SchemaVersion,
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            new Dictionary<string, object?>
            {
                ["audience"] = grant.Audience,
                ["expiresUtc"] = grant.ExpiresUtc.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                ["issuer"] = grant.Issuer,
                ["scopes"] = grant.Scopes.ToArray()
            });
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);
        var signingMetadata = SigningMetadata.Create(
            signingHash: hash.HashValue,
            hashAlgorithm: hash.HashAlgorithm,
            signature: "fake-signature",
            signatureAlgorithm: "FAKE-SIGNATURE-V1",
            keyId: "key-1",
            keyVersion: "v1",
            provider: "fake-provider",
            signedUtc: Now);

        return SignedGovernanceArtifacts.FromSigningMetadata(grant, payload, hash, signingMetadata);
    }

    private sealed class StubVerificationService : IAsiBackboneSignatureVerificationService
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
