using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Core.Signing;
using AsiBackbone.DependencyInjection;
using AsiBackbone.Storage.InMemory;
using AsiBackbone.Storage.InMemory.CapabilityTokens;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityTokens;

/// <summary>
/// Tests for the packaged in-memory capability grant use store used by local validation, samples, and smoke tests.
/// </summary>
public sealed class InMemoryCapabilityGrantUseStoreTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that the packaged in-memory store accepts the first bounded use and denies replay when the validator requires use checking.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task ValidateAsyncWithPackagedStoreAcceptsFirstUseAndDeniesReplay()
    {
        SignedGovernanceArtifact<CapabilityTokenGrant> signedGrant = CreateSignedGrant(CreateGrant("grant-replay"));
        var store = new InMemoryCapabilityGrantUseStore();
        CapabilityGrantValidationOptions options = CreateOptions(requireUseCheck: true, maxUseCount: 1);

        CapabilityGrantValidationResult first = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            options,
            useStore: store,
            cancellationToken: TestContext.Current.CancellationToken);
        CapabilityGrantValidationResult second = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            options,
            useStore: store,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(first.IsValid);
        Assert.True(first.ShouldAllow);
        Assert.False(second.IsValid);
        Assert.Equal(CapabilityTokenValidationCategory.ReuseLimitExceeded, second.Category);
        Assert.Equal(VerificationPolicyAction.Deny, second.Action);
        Assert.Equal("capability.use-limit-exceeded", second.FailureCode);
        Assert.Equal(1, store.GetUseCount("grant-replay"));
    }

    /// <summary>
    /// Verifies that the packaged in-memory store tracks grants independently and honors max-use count values greater than one.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task TryConsumeAsyncTracksDistinctGrantsAndHonorsMaxUseCount()
    {
        var store = new InMemoryCapabilityGrantUseStore();
        CapabilityTokenGrant firstGrant = CreateGrant("grant-a");
        CapabilityTokenGrant secondGrant = CreateGrant("grant-b");

        CapabilityGrantUseResult firstUse = await store.TryConsumeAsync(
            firstGrant,
            maxUseCount: 2,
            usedUtc: Now,
            cancellationToken: TestContext.Current.CancellationToken);
        CapabilityGrantUseResult secondUse = await store.TryConsumeAsync(
            firstGrant,
            maxUseCount: 2,
            usedUtc: Now,
            cancellationToken: TestContext.Current.CancellationToken);
        CapabilityGrantUseResult thirdUse = await store.TryConsumeAsync(
            firstGrant,
            maxUseCount: 2,
            usedUtc: Now,
            cancellationToken: TestContext.Current.CancellationToken);
        CapabilityGrantUseResult otherGrantUse = await store.TryConsumeAsync(
            secondGrant,
            maxUseCount: 2,
            usedUtc: Now,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GrantUseState.Accepted, firstUse.State);
        Assert.Equal(1, firstUse.UseCount);
        Assert.Equal(GrantUseState.Accepted, secondUse.State);
        Assert.Equal(2, secondUse.UseCount);
        Assert.Equal(GrantUseState.UseLimitExceeded, thirdUse.State);
        Assert.Equal(2, thirdUse.UseCount);
        Assert.Equal(GrantUseState.Accepted, otherGrantUse.State);
        Assert.Equal(1, otherGrantUse.UseCount);
        Assert.Equal(2, store.GetUseCount("grant-a"));
        Assert.Equal(1, store.GetUseCount("grant-b"));
    }

    /// <summary>
    /// Verifies that stopped and cancelled local-validation states are represented through the public use-result primitives.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task TryConsumeAsyncRepresentsStoppedAndCancelledStates()
    {
        var store = new InMemoryCapabilityGrantUseStore();
        CapabilityTokenGrant stoppedGrant = CreateGrant("grant-stopped");
        CapabilityTokenGrant cancelledGrant = CreateGrant("grant-cancelled");

        store.StopGrant(stoppedGrant.TokenId);
        store.CancelGrant(cancelledGrant.TokenId);

        CapabilityGrantUseResult stopped = await store.TryConsumeAsync(
            stoppedGrant,
            maxUseCount: 1,
            usedUtc: Now,
            cancellationToken: TestContext.Current.CancellationToken);
        CapabilityGrantUseResult cancelled = await store.TryConsumeAsync(
            cancelledGrant,
            maxUseCount: 1,
            usedUtc: Now,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GrantUseState.Stopped, stopped.State);
        Assert.Equal("capability.grant-stopped", stopped.FailureCode);
        Assert.Equal(GrantUseState.Cancelled, cancelled.State);
        Assert.Equal("capability.grant-cancelled", cancelled.FailureCode);
    }

    /// <summary>
    /// Verifies that the builder facade registers the packaged store as both its concrete type and provider-neutral contract.
    /// </summary>
    [Fact]
    public void UseInMemoryCapabilityGrantUseStoreRegistersConcreteAndContractServices()
    {
        var services = new ServiceCollection();

        _ = services.AddAsiBackbone(builder =>
            builder.UseInMemoryCapabilityGrantUseStore());

        using ServiceProvider serviceProvider = services.BuildServiceProvider();
        InMemoryCapabilityGrantUseStore concreteStore = serviceProvider.GetRequiredService<InMemoryCapabilityGrantUseStore>();
        ICapabilityGrantUseStore contractStore = serviceProvider.GetRequiredService<ICapabilityGrantUseStore>();

        Assert.Same(concreteStore, contractStore);
    }

    private static CapabilityGrantValidationOptions CreateOptions(bool requireUseCheck, int maxUseCount)
    {
        return CapabilityGrantValidationOptions.Create(
            issuer: "issuer-1",
            audience: "gateway-1",
            scopes: ["sample.execute"],
            validationUtc: Now,
            policyVersion: "policy-v1",
            policyHash: "policy-hash",
            requireUseCheck: requireUseCheck,
            maxUseCount: maxUseCount);
    }

    private static CapabilityTokenGrant CreateGrant(string tokenId)
    {
        return CapabilityTokenGrant.Create(
            tokenId: tokenId,
            issuer: "issuer-1",
            audience: "gateway-1",
            scopes: ["sample.execute"],
            issuedUtc: Now.AddMinutes(-5),
            expiresUtc: Now.AddMinutes(5),
            policyVersion: "policy-v1",
            policyHash: "policy-hash");
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
}
