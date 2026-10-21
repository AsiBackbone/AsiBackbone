using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.EntityFrameworkCore.CapabilityGrants;
using AsiBackbone.EntityFrameworkCore.Tests.Outbox;
using AsiBackbone.EntityFrameworkCore.Tests.Outbox.Providers;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests.CapabilityGrants;

/// <summary>Tests replay-store contention against opt-in real database providers.</summary>
public sealed class EfCoreCapabilityGrantUseStoreProviderTests
{
    /// <summary>Verifies atomic single-use consumption on SQL Server isolation configurations.</summary>
    [Theory]
    [InlineData(OutboxContentionProvider.SqlServerLockingReadCommitted)]
    [InlineData(OutboxContentionProvider.SqlServerReadCommittedSnapshot)]
    public async Task ConcurrentSqlServerContextsAcceptSingleUseExactlyOnce(OutboxContentionProvider provider)
    {
        await using ProviderOutboxDatabase database = await ProviderOutboxDatabase.CreateAsync(
            provider, TestContext.Current.CancellationToken);
        await using GovernanceOutboxTestDbContext firstContext = database.CreateContext();
        await using GovernanceOutboxTestDbContext secondContext = database.CreateContext();
        EfCoreCapabilityGrantUseStoreOptions options = new() { ContentionRetryCount = 20 };
        var firstStore = new EfCoreCapabilityGrantUseStore(firstContext, options);
        var secondStore = new EfCoreCapabilityGrantUseStore(secondContext, options);
        DateTimeOffset now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);
        var grant = CapabilityGrant.Create(
            tokenId: "sqlserver-single-use",
            issuer: "issuer-1",
            audience: "audience-1",
            scopes: ["sample.execute"],
            issuedUtc: now.AddMinutes(-1),
            expiresUtc: now.AddMinutes(10),
            policyVersion: "policy-v1",
            policyHash: "policy-hash");

        CapabilityGrantUseResult[] results = await Task.WhenAll(
            firstStore.TryConsumeAsync(grant, 1, now, TestContext.Current.CancellationToken).AsTask(),
            secondStore.TryConsumeAsync(grant, 1, now, TestContext.Current.CancellationToken).AsTask());

        Assert.Single(results, result => result.State == GrantUseState.Accepted);
        Assert.Single(results, result => result.State == GrantUseState.UseLimitExceeded);
    }
}
