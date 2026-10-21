using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.EntityFrameworkCore.CapabilityGrants;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests.CapabilityGrants;

/// <summary>Tests durable SQLite capability-grant replay protection.</summary>
public sealed class EfCoreCapabilityGrantUseStoreTests
{
    /// <summary>Verifies that two contexts accept a single-use grant exactly once.</summary>
    [Fact]
    public async Task ConcurrentSqliteContextsAcceptSingleUseExactlyOnce()
    {
        string databaseName = $"asib_grants_{Guid.NewGuid():N}";
        string connectionString = $"Data Source={databaseName};Mode=Memory;Cache=Shared;Default Timeout=30";
        await using SqliteConnection keeper = new(connectionString);
        await keeper.OpenAsync(TestContext.Current.CancellationToken);
        DbContextOptions<GrantUseDbContext> options = new DbContextOptionsBuilder<GrantUseDbContext>()
            .UseSqlite(connectionString)
            .Options;
        await using (GrantUseDbContext setup = new(options))
        {
            _ = await setup.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        }

        await using GrantUseDbContext firstContext = new(options);
        await using GrantUseDbContext secondContext = new(options);
        EfCoreCapabilityGrantUseStoreOptions storeOptions = new() { ContentionRetryCount = 20 };
        var firstStore = new EfCoreCapabilityGrantUseStore(firstContext, storeOptions);
        var secondStore = new EfCoreCapabilityGrantUseStore(secondContext, storeOptions);
        CapabilityGrant grant = CreateGrant("single-use");

        CapabilityGrantUseResult[] results = await Task.WhenAll(
            firstStore.TryConsumeAsync(grant, 1, Now, TestContext.Current.CancellationToken).AsTask(),
            secondStore.TryConsumeAsync(grant, 1, Now, TestContext.Current.CancellationToken).AsTask());

        Assert.Single(results, result => result.State == GrantUseState.Accepted);
        Assert.Single(results, result => result.State == GrantUseState.UseLimitExceeded);
    }

    /// <summary>Verifies durable administrative state and expiry-plus-skew purging.</summary>
    [Fact]
    public async Task StopCancelRetentionAndPurgeAreDurable()
    {
        DbContextOptions<GrantUseDbContext> options = new DbContextOptionsBuilder<GrantUseDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using GrantUseDbContext context = new(options);
        await context.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var store = new EfCoreCapabilityGrantUseStore(context, new EfCoreCapabilityGrantUseStoreOptions());
        CapabilityGrant stoppedGrant = CreateGrant("stopped");
        CapabilityGrant cancelledGrant = CreateGrant("cancelled");

        await store.SetStoppedAsync(stoppedGrant.Issuer, stoppedGrant.TokenId, Now.AddMinutes(1), TestContext.Current.CancellationToken);
        await store.SetCancelledAsync(cancelledGrant.Issuer, cancelledGrant.TokenId, Now.AddMinutes(1), TestContext.Current.CancellationToken);

        Assert.Equal(GrantUseState.Stopped, (await store.TryConsumeAsync(
            stoppedGrant, 1, Now, TestContext.Current.CancellationToken)).State);
        Assert.Equal(GrantUseState.Cancelled, (await store.TryConsumeAsync(
            cancelledGrant, 1, Now, TestContext.Current.CancellationToken)).State);
        Assert.Equal(2, await store.PurgeExpiredAsync(Now.AddMinutes(2), TestContext.Current.CancellationToken));
    }

    /// <summary>Verifies that an unavailable database fails closed without accepting the grant.</summary>
    [Fact]
    public async Task DatabaseFailureReturnsUnavailable()
    {
        DbContextOptions<GrantUseDbContext> options = new DbContextOptionsBuilder<GrantUseDbContext>()
            .UseSqlite("Data Source=:memory:")
            .Options;
        await using GrantUseDbContext context = new(options);
        var store = new EfCoreCapabilityGrantUseStore(
            context,
            new EfCoreCapabilityGrantUseStoreOptions { ContentionRetryCount = 0 });

        CapabilityGrantUseResult result = await store.TryConsumeAsync(
            CreateGrant("database-unavailable"), 1, Now, TestContext.Current.CancellationToken);

        Assert.Equal(GrantUseState.Unavailable, result.State);
    }

    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static CapabilityGrant CreateGrant(string tokenId)
    {
        return CapabilityGrant.Create(
            tokenId: tokenId,
            issuer: "issuer-1",
            audience: "audience-1",
            scopes: ["sample.execute"],
            issuedUtc: Now.AddMinutes(-1),
            expiresUtc: Now.AddMinutes(10),
            policyVersion: "policy-v1",
            policyHash: "policy-hash");
    }

    private sealed class GrantUseDbContext(DbContextOptions<GrantUseDbContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
