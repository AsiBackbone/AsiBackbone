using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Storage.InMemory.CapabilityTokens;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityTokens;

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
        CapabilityTokenGrant grant = CreateGrant("issuer-1", "single-use");

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
        CapabilityTokenGrant grant = CreateGrant("issuer-1", "contended");

        CapabilityGrantUseResult[] results = await Task.WhenAll(
            Enumerable.Range(0, 32).Select(async _ =>
            {
                await Task.Yield();
                return await store.TryConsumeAsync(grant, 1, Now, CancellationToken.None);
            }));

        Assert.Equal(1, results.Count(result => result.State is GrantUseState.Accepted));
        Assert.Equal(31, results.Count(result => result.State is GrantUseState.UseLimitExceeded));
    }

    private static CapabilityTokenGrant CreateGrant(
        string issuer,
        string tokenId,
        DateTimeOffset? expiresUtc = null)
    {
        return CapabilityTokenGrant.Create(
            tokenId: tokenId,
            issuer: issuer,
            audience: "gateway-1",
            scopes: ["robotics.execute"],
            issuedUtc: Now.AddMinutes(-5),
            expiresUtc: expiresUtc ?? Now.AddMinutes(5));
    }
}
