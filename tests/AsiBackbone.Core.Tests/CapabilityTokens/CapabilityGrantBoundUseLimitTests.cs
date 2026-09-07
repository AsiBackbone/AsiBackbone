using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Core.Serialization;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityTokens;

/// <summary>
/// Tests that a use limit bound by the issuer is part of the signed payload and cannot be widened by a relying party.
/// </summary>
public sealed class CapabilityGrantBoundUseLimitTests
{
    private static readonly DateTimeOffset IssuedUtc = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that a grant recording the second schema version binds its use limit into the canonical payload.
    /// </summary>
    [Fact]
    public void CanonicalPayloadBindsTheUseLimitForTheCurrentSchemaVersion()
    {
        CanonicalPayload withLimit = CanonicalPayloadBuilder.ForCapabilityTokenGrant(
            CreateGrant(maxUseCount: 1, schemaVersion: AsiBackboneSchemaVersions.StableArtifactsV2));

        CanonicalPayload withWiderLimit = CanonicalPayloadBuilder.ForCapabilityTokenGrant(
            CreateGrant(maxUseCount: 5, schemaVersion: AsiBackboneSchemaVersions.StableArtifactsV2));

        Assert.Contains("maxUseCount", withLimit.CanonicalJson, StringComparison.Ordinal);
        Assert.NotEqual(
            CanonicalPayloadHasher.ComputeHash(withLimit).HashValue,
            CanonicalPayloadHasher.ComputeHash(withWiderLimit).HashValue);
    }

    /// <summary>
    /// Verifies that a grant recording the first schema version canonicalizes exactly as it did before the field existed.
    /// </summary>
    /// <remarks>
    /// Grants signed under the earlier schema version must keep verifying, so the field is absent from their payload.
    /// </remarks>
    [Fact]
    public void CanonicalPayloadOmitsTheUseLimitForTheEarlierSchemaVersion()
    {
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityTokenGrant(
            CreateGrant(maxUseCount: 3, schemaVersion: AsiBackboneSchemaVersions.StableArtifactsV1));

        Assert.DoesNotContain("maxUseCount", payload.CanonicalJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a caller cannot widen the issuer's bound limit.
    /// </summary>
    [Fact]
    public async Task ValidationUsesTheIssuerLimitWhenTheCallerAsksForMore()
    {
        var useStore = new RecordingUseStore();
        CapabilityTokenGrant grant = CreateGrant(maxUseCount: 1, schemaVersion: AsiBackboneSchemaVersions.StableArtifactsV2);

        _ = await CapabilityGrantValidator.ValidateAsync(
            CreateSignedGrant(grant),
            CapabilityGrantValidationOptions.Create(
                audience: "gateway-1",
                requireUseCheck: true,
                maxUseCount: 10,
                validationUtc: IssuedUtc.AddMinutes(1)),
            useStore: useStore,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, useStore.ObservedMaxUseCount);
    }

    /// <summary>
    /// Verifies that a caller may still tighten the limit below what the issuer bound.
    /// </summary>
    [Fact]
    public async Task ValidationUsesTheCallerLimitWhenItIsNarrower()
    {
        var useStore = new RecordingUseStore();
        CapabilityTokenGrant grant = CreateGrant(maxUseCount: 10, schemaVersion: AsiBackboneSchemaVersions.StableArtifactsV2);

        _ = await CapabilityGrantValidator.ValidateAsync(
            CreateSignedGrant(grant),
            CapabilityGrantValidationOptions.Create(
                audience: "gateway-1",
                requireUseCheck: true,
                maxUseCount: 2,
                validationUtc: IssuedUtc.AddMinutes(1)),
            useStore: useStore,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, useStore.ObservedMaxUseCount);
    }

    /// <summary>
    /// Verifies that a grant binding no limit leaves the caller's limit in force.
    /// </summary>
    [Fact]
    public async Task ValidationUsesTheCallerLimitWhenTheGrantBindsNone()
    {
        var useStore = new RecordingUseStore();

        _ = await CapabilityGrantValidator.ValidateAsync(
            CreateSignedGrant(CreateGrant()),
            CapabilityGrantValidationOptions.Create(
                audience: "gateway-1",
                requireUseCheck: true,
                maxUseCount: 4,
                validationUtc: IssuedUtc.AddMinutes(1)),
            useStore: useStore,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(4, useStore.ObservedMaxUseCount);
    }

    /// <summary>
    /// Verifies that a non-positive bound limit is rejected at construction.
    /// </summary>
    [Fact]
    public void CreateRejectsANonPositiveBoundUseLimit()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => CreateGrant(maxUseCount: 0));

        Assert.Equal("maxUseCount", exception.ParamName);
    }

    private static CapabilityTokenGrant CreateGrant(int? maxUseCount = null, string? schemaVersion = null)
    {
        return CapabilityTokenGrant.Create(
            tokenId: "grant-bound-use",
            issuer: "issuer-1",
            audience: "gateway-1",
            scopes: ["robotics.execute"],
            issuedUtc: IssuedUtc,
            expiresUtc: IssuedUtc.AddMinutes(10),
            maxUseCount: maxUseCount,
            schemaVersion: schemaVersion);
    }

    private static SignedGovernanceArtifact<CapabilityTokenGrant> CreateSignedGrant(CapabilityTokenGrant grant)
    {
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityTokenGrant(grant);
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);

        return SignedGovernanceArtifacts.FromSigningMetadata(
            grant,
            payload,
            hash,
            SigningMetadata.Create(signingHash: hash.HashValue, hashAlgorithm: hash.HashAlgorithm));
    }

    private sealed class RecordingUseStore : ICapabilityGrantUseStore
    {
        public int ObservedMaxUseCount { get; private set; }

        public ValueTask<CapabilityGrantUseResult> TryConsumeAsync(
            CapabilityTokenGrant grant,
            int maxUseCount,
            DateTimeOffset usedUtc,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(grant);
            cancellationToken.ThrowIfCancellationRequested();
            ObservedMaxUseCount = maxUseCount;

            return ValueTask.FromResult(CapabilityGrantUseResult.Accepted(1));
        }
    }
}
