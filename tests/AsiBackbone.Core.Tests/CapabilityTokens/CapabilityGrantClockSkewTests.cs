using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityTokens;

/// <summary>
/// Tests for clock skew validation in capability grants.
/// </summary>
public sealed class CapabilityGrantClockSkewTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 10, 15, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Skew = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Tests that <see cref="CapabilityGrantValidationOptions.Create"/> defaults to zero clock skew.
    /// </summary>
    [Fact]
    public void CreateDefaultsToZeroClockSkew()
    {
        var options = CapabilityGrantValidationOptions.Create();

        Assert.Equal(TimeSpan.Zero, options.AllowedClockSkew);
    }

    /// <summary>
    /// Tests that <see cref="CapabilityGrantValidationOptions.Create"/> rejects negative clock skew.
    /// </summary>
    [Fact]
    public void CreateRejectsNegativeClockSkew()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            CapabilityGrantValidationOptions.Create(allowedClockSkew: TimeSpan.FromTicks(-1)));
    }

    /// <summary>
    /// Tests that <see cref="CapabilityGrantValidationOptions.Create"/> preserves UTC normalization with clock skew.
    /// </summary>
    [Fact]
    public void CreatePreservesUtcNormalizationWithClockSkew()
    {
        DateTimeOffset localValidationUtc = new(2026, 7, 10, 10, 0, 0, TimeSpan.FromHours(-5));

        var options = CapabilityGrantValidationOptions.Create(
            validationUtc: localValidationUtc,
            allowedClockSkew: Skew);

        Assert.Equal(Now, options.ValidationUtc);
        Assert.Equal(Skew, options.AllowedClockSkew);
    }

    /// <summary>
    /// Tests that validation preserves strict zero-skew behavior.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncPreservesStrictZeroSkewBehavior()
    {
        CapabilityGrantValidationResult result = await ValidateAsync(
            notBeforeUtc: Now.AddTicks(1),
            expiresUtc: Now.AddMinutes(1),
            allowedClockSkew: TimeSpan.Zero);

        AssertFailure(result, CapabilityTokenValidationCategory.NotYetValid, "capability.not-yet-valid");
    }

    /// <summary>
    /// Tests that validation allows a grant just inside the not-before tolerance.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncAllowsGrantJustInsideNotBeforeTolerance()
    {
        CapabilityGrantValidationResult result = await ValidateAsync(
            notBeforeUtc: Now.Add(Skew).AddTicks(-1),
            expiresUtc: Now.AddMinutes(1),
            allowedClockSkew: Skew);

        Assert.True(result.ShouldAllow);
    }

    /// <summary>
    /// Tests that validation rejects a grant just outside the not-before tolerance.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncRejectsGrantJustOutsideNotBeforeTolerance()
    {
        CapabilityGrantValidationResult result = await ValidateAsync(
            notBeforeUtc: Now.Add(Skew).AddTicks(1),
            expiresUtc: Now.AddMinutes(1),
            allowedClockSkew: Skew);

        AssertFailure(result, CapabilityTokenValidationCategory.NotYetValid, "capability.not-yet-valid");
    }

    /// <summary>
    /// Tests that validation allows a grant at the exact not-before tolerance boundary.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncAllowsGrantAtExactNotBeforeToleranceBoundary()
    {
        CapabilityGrantValidationResult result = await ValidateAsync(
            notBeforeUtc: Now.Add(Skew),
            expiresUtc: Now.AddMinutes(1),
            allowedClockSkew: Skew);

        Assert.True(result.ShouldAllow);
    }

    /// <summary>
    /// Tests that validation allows a grant just inside the expiration tolerance.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncAllowsGrantJustInsideExpirationTolerance()
    {
        CapabilityGrantValidationResult result = await ValidateAsync(
            notBeforeUtc: null,
            expiresUtc: Now.Subtract(Skew).AddTicks(1),
            allowedClockSkew: Skew);

        Assert.True(result.ShouldAllow);
    }

    /// <summary>
    /// Tests that validation rejects a grant just outside the expiration tolerance.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncRejectsGrantJustOutsideExpirationTolerance()
    {
        CapabilityGrantValidationResult result = await ValidateAsync(
            notBeforeUtc: null,
            expiresUtc: Now.Subtract(Skew).AddTicks(-1),
            allowedClockSkew: Skew);

        AssertFailure(result, CapabilityTokenValidationCategory.Expired, "capability.expired");
    }

    /// <summary>
    /// Tests that validation rejects a grant at the exact expiration tolerance boundary.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncRejectsGrantAtExactExpirationToleranceBoundary()
    {
        CapabilityGrantValidationResult result = await ValidateAsync(
            notBeforeUtc: null,
            expiresUtc: Now.Subtract(Skew),
            allowedClockSkew: Skew);

        AssertFailure(result, CapabilityTokenValidationCategory.Expired, "capability.expired");
    }

    /// <summary>
    /// Tests that validation rejects a grant at expiration with zero skew.
    /// </summary>
    [Fact]
    public async Task ValidateAsyncRejectsGrantAtExpirationWithZeroSkew()
    {
        CapabilityGrantValidationResult result = await ValidateAsync(
            notBeforeUtc: null,
            expiresUtc: Now,
            allowedClockSkew: TimeSpan.Zero);

        AssertFailure(result, CapabilityTokenValidationCategory.Expired, "capability.expired");
    }

    private static async Task<CapabilityGrantValidationResult> ValidateAsync(
        DateTimeOffset? notBeforeUtc,
        DateTimeOffset expiresUtc,
        TimeSpan allowedClockSkew)
    {
        var grant = CapabilityTokenGrant.Create(
            tokenId: "grant-clock-skew",
            issuer: "issuer-1",
            audience: "gateway-1",
            scopes: ["robotics.execute"],
            issuedUtc: Now.AddMinutes(-5),
            expiresUtc: expiresUtc,
            notBeforeUtc: notBeforeUtc);

        return await CapabilityGrantValidator.ValidateAsync(
            CreateSignedGrant(grant),
            CapabilityGrantValidationOptions.Create(
                issuer: "issuer-1",
                audience: "gateway-1",
                scopes: ["robotics.execute"],
                validationUtc: Now,
                allowedClockSkew: allowedClockSkew),
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static SignedGovernanceArtifact<CapabilityTokenGrant> CreateSignedGrant(CapabilityTokenGrant grant)
    {
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityTokenGrant(grant);
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

    private static void AssertFailure(
        CapabilityGrantValidationResult result,
        CapabilityTokenValidationCategory category,
        string failureCode)
    {
        Assert.False(result.ShouldAllow);
        Assert.Equal(category, result.Category);
        Assert.Equal(failureCode, result.FailureCode);
    }
}
