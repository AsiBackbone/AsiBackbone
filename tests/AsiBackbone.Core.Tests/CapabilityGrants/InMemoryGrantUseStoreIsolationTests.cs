using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.Core.Signing;
using AsiBackbone.Storage.InMemory.CapabilityGrants;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityGrants;

/// <summary>
/// Tests issuer isolation, eviction, and concurrent consumption in the in-process capability grant use store.
/// </summary>
public sealed class InMemoryGrantUseStoreIsolationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that two issuers using the same token identifier do not share one use budget.
    /// </summary>
    /// <remarks>
    /// Keying by token identifier alone let one issuer's grant exhaust another issuer's single-use budget.
    /// </remarks>
    [Fact]
    public async Task TryConsumeAsyncIsolatesIssuersSharingATokenIdentifier()
    {
        var store = new InMemoryCapabilityGrantUseStore();

        CapabilityGrantUseResult first = await store.TryConsumeAsync(
            CreateGrant("issuer-1", "shared-id"),
            maxUseCount: 1,
            Now,
            TestContext.Current.CancellationToken);

        CapabilityGrantUseResult second = await store.TryConsumeAsync(
            CreateGrant("issuer-2", "shared-id"),
            maxUseCount: 1,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(GrantUseState.Accepted, first.State);
        Assert.Equal(GrantUseState.Accepted, second.State);
        Assert.Equal(1, store.GetUseCount("issuer-1", "shared-id"));
        Assert.Equal(1, store.GetUseCount("issuer-2", "shared-id"));
        Assert.Equal(2, store.GetUseCount("shared-id"));
    }

    /// <summary>
    /// Verifies that the same issuer's second use of a single-use grant is still refused.
    /// </summary>
    [Fact]
    public async Task TryConsumeAsyncStillRefusesASecondUseForTheSameIssuer()
    {
        var store = new InMemoryCapabilityGrantUseStore();
        CapabilityGrant grant = CreateGrant("issuer-1", "single-use");

        _ = await store.TryConsumeAsync(grant, 1, Now, TestContext.Current.CancellationToken);
        CapabilityGrantUseResult second = await store.TryConsumeAsync(grant, 1, Now, TestContext.Current.CancellationToken);

        Assert.Equal(GrantUseState.UseLimitExceeded, second.State);
    }

    /// <summary>
    /// Verifies that use records for long-expired grants are evicted rather than retained for the process lifetime.
    /// </summary>
    [Fact]
    public async Task TryConsumeAsyncEvictsRecordsForLongExpiredGrants()
    {
        var store = new InMemoryCapabilityGrantUseStore { EvictionGracePeriod = TimeSpan.FromMinutes(1) };

        _ = await store.TryConsumeAsync(
            CreateGrant("issuer-1", "old-grant"),
            maxUseCount: 1,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(1, store.GetUseCount("issuer-1", "old-grant"));

        _ = await store.TryConsumeAsync(
            CreateGrant("issuer-1", "new-grant", expiresUtc: Now.AddHours(2)),
            maxUseCount: 1,
            Now.AddHours(1),
            TestContext.Current.CancellationToken);

        Assert.Equal(0, store.GetUseCount("issuer-1", "old-grant"));
    }

    /// <summary>
    /// Verifies that concurrent consumption of a single-use grant accepts exactly one caller.
    /// </summary>
    [Fact]
    public async Task TryConsumeAsyncAcceptsExactlyOneConcurrentCaller()
    {
        var store = new InMemoryCapabilityGrantUseStore();
        CapabilityGrant grant = CreateGrant("issuer-1", "contended");

        CapabilityGrantUseResult[] results = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(async _ =>
            {
                await Task.Yield();
                return await store.TryConsumeAsync(grant, 1, Now, CancellationToken.None);
            }));

        Assert.Equal(1, results.Count(result => result.State is GrantUseState.Accepted));
        Assert.Equal(31, results.Count(result => result.State is GrantUseState.UseLimitExceeded));
    }

    /// <summary>
    /// Verifies that a grant past the retention horizon is refused rather than given a fresh count after its record was
    /// evicted.
    /// </summary>
    /// <remarks>
    /// Eviction ran against the grace period alone, so once a grant had been expired for longer than the grace period its
    /// record was removed and the next use was accepted as the first use.
    /// </remarks>
    [Fact]
    public async Task TryConsumeAsyncRefusesGrantPastRetentionHorizonInsteadOfResettingCount()
    {
        var store = new InMemoryCapabilityGrantUseStore { EvictionGracePeriod = TimeSpan.FromMinutes(1) };
        CapabilityGrant grant = CreateGrant("issuer-1", "expiring", expiresUtc: Now);

        CapabilityGrantUseResult first = await store.TryConsumeAsync(grant, 1, Now.AddSeconds(30), TestContext.Current.CancellationToken);
        CapabilityGrantUseResult replay = await store.TryConsumeAsync(grant, 1, Now.AddMinutes(2), TestContext.Current.CancellationToken);

        Assert.Equal(GrantUseState.Accepted, first.State);
        Assert.Equal(GrantUseState.UseLimitExceeded, replay.State);
        Assert.Equal("capability.use-retention-elapsed", replay.FailureCode);
    }

    /// <summary>
    /// Verifies that a caller-supplied use time earlier than one already observed cannot resurrect an evicted grant.
    /// </summary>
    [Fact]
    public async Task TryConsumeAsyncMeasuresRetentionFromLatestObservedUseTime()
    {
        var store = new InMemoryCapabilityGrantUseStore { EvictionGracePeriod = TimeSpan.FromMinutes(1) };
        CapabilityGrant grant = CreateGrant("issuer-1", "expiring", expiresUtc: Now);

        _ = await store.TryConsumeAsync(grant, 1, Now, TestContext.Current.CancellationToken);
        _ = await store.TryConsumeAsync(
            CreateGrant("issuer-1", "later", expiresUtc: Now.AddHours(1)),
            1,
            Now.AddMinutes(10),
            TestContext.Current.CancellationToken);
        CapabilityGrantUseResult replay = await store.TryConsumeAsync(grant, 1, Now.AddSeconds(30), TestContext.Current.CancellationToken);

        Assert.Equal(0, store.GetUseCount("issuer-1", "expiring"));
        Assert.Equal(GrantUseState.UseLimitExceeded, replay.State);
        Assert.Equal("capability.use-retention-elapsed", replay.FailureCode);
    }

    /// <summary>
    /// Reproduces the replay end to end: a validator whose clock skew exceeds the store's grace period still accepts an
    /// expired grant, and the second use is now denied instead of accepted as a first use.
    /// </summary>
    [Fact]
    public async Task ValidatorWithSkewAboveGracePeriodDeniesReplayOfExpiredGrant()
    {
        var store = new InMemoryCapabilityGrantUseStore { EvictionGracePeriod = TimeSpan.FromMinutes(1) };
        CapabilityGrant grant = CreateGrant("issuer-1", "skewed", expiresUtc: Now);
        SignedGovernanceArtifact<CapabilityGrant> signedGrant = CreateUnsignedArtifact(grant);

        CapabilityGrantValidationResult first = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            CreateUseCheckOptions(Now.AddSeconds(30)),
            useStore: store,
            cancellationToken: TestContext.Current.CancellationToken);
        CapabilityGrantValidationResult replay = await CapabilityGrantValidator.ValidateAsync(
            signedGrant,
            CreateUseCheckOptions(Now.AddMinutes(2)),
            useStore: store,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(first.ShouldAllow);
        Assert.False(replay.ShouldAllow);
        Assert.Equal(CapabilityGrantValidationCategory.ReuseLimitExceeded, replay.Category);
        Assert.Equal(VerificationPolicyAction.Deny, replay.Action);
        Assert.Equal("capability.use-retention-elapsed", replay.FailureCode);
    }

    /// <summary>
    /// Verifies that stopping and cancelling one issuer's grant leaves another issuer's grant with the same identifier
    /// usable.
    /// </summary>
    [Fact]
    public async Task IssuerScopedStopAndCancelAffectOnlyThatIssuer()
    {
        var store = new InMemoryCapabilityGrantUseStore();
        store.StopGrant("issuer-1", "stopped-id");
        store.CancelGrant("issuer-1", "cancelled-id");

        CapabilityGrantUseResult stopped = await store.TryConsumeAsync(CreateGrant("issuer-1", "stopped-id"), 1, Now, TestContext.Current.CancellationToken);
        CapabilityGrantUseResult otherIssuerStopped = await store.TryConsumeAsync(CreateGrant("issuer-2", "stopped-id"), 1, Now, TestContext.Current.CancellationToken);
        CapabilityGrantUseResult cancelled = await store.TryConsumeAsync(CreateGrant("issuer-1", "cancelled-id"), 1, Now, TestContext.Current.CancellationToken);
        CapabilityGrantUseResult otherIssuerCancelled = await store.TryConsumeAsync(CreateGrant("issuer-2", "cancelled-id"), 1, Now, TestContext.Current.CancellationToken);

        Assert.Equal(GrantUseState.Stopped, stopped.State);
        Assert.Equal(GrantUseState.Accepted, otherIssuerStopped.State);
        Assert.Equal(GrantUseState.Cancelled, cancelled.State);
        Assert.Equal(GrantUseState.Accepted, otherIssuerCancelled.State);
    }

    /// <summary>
    /// Verifies that the identifier-only overloads still apply to every issuer, as documented.
    /// </summary>
    [Fact]
    public async Task IdentifierOnlyStopAppliesToEveryIssuer()
    {
        var store = new InMemoryCapabilityGrantUseStore();
        store.StopGrant("shared-id");

        CapabilityGrantUseResult first = await store.TryConsumeAsync(CreateGrant("issuer-1", "shared-id"), 1, Now, TestContext.Current.CancellationToken);
        CapabilityGrantUseResult second = await store.TryConsumeAsync(CreateGrant("issuer-2", "shared-id"), 1, Now, TestContext.Current.CancellationToken);

        Assert.Equal(GrantUseState.Stopped, first.State);
        Assert.Equal(GrantUseState.Stopped, second.State);
    }

    /// <summary>
    /// Verifies that a later issuer-scoped cancellation replaces an earlier stop for the same issuer.
    /// </summary>
    [Fact]
    public async Task IssuerScopedCancelReplacesStopForTheSameIssuer()
    {
        var store = new InMemoryCapabilityGrantUseStore();
        store.StopGrant("issuer-1", "grant-id");
        store.CancelGrant("issuer-1", "grant-id");

        CapabilityGrantUseResult result = await store.TryConsumeAsync(CreateGrant("issuer-1", "grant-id"), 1, Now, TestContext.Current.CancellationToken);

        Assert.Equal(GrantUseState.Cancelled, result.State);
    }

    /// <summary>
    /// Verifies that a negative grace period is rejected.
    /// </summary>
    [Fact]
    public void EvictionGracePeriodRejectsNegativeValues()
    {
        var store = new InMemoryCapabilityGrantUseStore();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() => store.EvictionGracePeriod = TimeSpan.FromTicks(-1));
        Assert.Equal(TimeSpan.FromMinutes(5), store.EvictionGracePeriod);
    }

    /// <summary>
    /// Verifies that clearing the store also resets the observed use time.
    /// </summary>
    [Fact]
    public async Task ClearResetsObservedUseTime()
    {
        var store = new InMemoryCapabilityGrantUseStore { EvictionGracePeriod = TimeSpan.FromMinutes(1) };
        _ = await store.TryConsumeAsync(
            CreateGrant("issuer-1", "later", expiresUtc: Now.AddHours(1)),
            1,
            Now.AddMinutes(10),
            TestContext.Current.CancellationToken);

        store.Clear();
        CapabilityGrantUseResult result = await store.TryConsumeAsync(
            CreateGrant("issuer-1", "current", expiresUtc: Now.AddMinutes(1)),
            1,
            Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(GrantUseState.Accepted, result.State);
    }

    private static CapabilityGrantValidationOptions CreateUseCheckOptions(DateTimeOffset validationUtc)
    {
        return CapabilityGrantValidationOptions.Create(
            issuer: "issuer-1",
            audience: "gateway-1",
            scopes: ["robotics.execute"],
            validationUtc: validationUtc,
            requireUseCheck: true,
            maxUseCount: 1,
            allowedClockSkew: TimeSpan.FromMinutes(10));
    }

    private static SignedGovernanceArtifact<CapabilityGrant> CreateUnsignedArtifact(CapabilityGrant grant)
    {
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityGrant(grant);

        return SignedGovernanceArtifacts.WithoutSignature(grant, payload, CanonicalPayloadHasher.ComputeHash(payload));
    }

    private static CapabilityGrant CreateGrant(
        string issuer,
        string tokenId,
        DateTimeOffset? expiresUtc = null)
    {
        return CapabilityGrant.Create(
            tokenId: tokenId,
            issuer: issuer,
            audience: "gateway-1",
            scopes: ["robotics.execute"],
            issuedUtc: Now.AddMinutes(-5),
            expiresUtc: expiresUtc ?? Now.AddMinutes(5));
    }
}
