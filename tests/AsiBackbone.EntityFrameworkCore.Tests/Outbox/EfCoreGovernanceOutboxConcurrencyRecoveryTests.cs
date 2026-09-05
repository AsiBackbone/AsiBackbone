using System.Data.Common;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Outbox;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests.Outbox;

/// <summary>
/// Deterministic relational coverage for EF Core outbox claim races and optimistic-concurrency recovery.
/// </summary>
public sealed class EfCoreGovernanceOutboxConcurrencyRecoveryTests
{
    /// <summary>
    /// Verifies a competing set-based claim observes the winner atomically and leaves the context usable for a later retry.
    /// </summary>
    [Fact]
    public async Task SetBasedClaimRaceReturnsNoClaimAndAllowsSubsequentRetry()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection keeperConnection = await EfCoreGovernanceOutboxTestHost.OpenSharedMemoryKeeperConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> durableOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(keeperConnection.ConnectionString);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(durableOptions, cancellationToken);

        const string outboxEntryId = "claim-race-entry";
        await SeedPendingEntryAsync(durableOptions, outboxEntryId, cancellationToken);

        var interceptor = new OneShotCommandInterceptor();
        DbContextOptions<GovernanceOutboxTestDbContext> losingOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(
            keeperConnection.ConnectionString,
            interceptor);
        DateTimeOffset claimUtc = new(2026, 7, 12, 17, 0, 0, TimeSpan.Zero);
        GovernanceOutboxClaim? winningClaim = null;
        interceptor.Arm(async callbackCancellationToken =>
        {
            await using GovernanceOutboxTestDbContext winningContext = new(durableOptions);
            var winningStore = new EfCoreGovernanceOutboxStore(winningContext);
            winningClaim = Assert.Single(await winningStore.ClaimPendingAsync(
                GovernanceOutboxClaimRequest.Create(
                    "worker-winning",
                    claimUtc,
                    TimeSpan.FromMinutes(5),
                    maxCount: 1),
                callbackCancellationToken));
        });

        await using GovernanceOutboxTestDbContext losingContext = new(losingOptions);
        var losingStore = new EfCoreGovernanceOutboxStore(losingContext);
        IReadOnlyList<GovernanceOutboxClaim> losingClaims = await losingStore.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create(
                "worker-losing",
                claimUtc,
                TimeSpan.FromMinutes(5),
                maxCount: 1),
            cancellationToken);

        Assert.Empty(losingClaims);
        Assert.NotNull(winningClaim);
        Assert.Empty(losingContext.ChangeTracker.Entries<AsiBackboneGovernanceOutboxEntryEntity>());

        await using (GovernanceOutboxTestDbContext verificationContext = new(durableOptions))
        {
            var verificationStore = new EfCoreGovernanceOutboxStore(verificationContext);
            GovernanceOutboxEntry? persisted = await verificationStore.FindByOutboxEntryIdAsync(outboxEntryId, cancellationToken);

            Assert.NotNull(persisted);
            Assert.Equal(winningClaim.WorkerId, persisted.ClaimOwner);
            Assert.Equal(winningClaim.ClaimToken, persisted.ClaimToken);
            Assert.Equal(1, persisted.ClaimAttemptCount);
        }

        await using (GovernanceOutboxTestDbContext releaseContext = new(durableOptions))
        {
            var releaseStore = new EfCoreGovernanceOutboxStore(releaseContext);
            GovernanceOutboxEntry? released = await releaseStore.ReleaseClaimAsync(winningClaim, cancellationToken: cancellationToken);

            Assert.NotNull(released);
            Assert.False(released.HasClaim);
        }

        GovernanceOutboxClaim retryClaim = Assert.Single(await losingStore.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create(
                "worker-retry",
                claimUtc.AddMinutes(1),
                TimeSpan.FromMinutes(5),
                maxCount: 1),
            cancellationToken));

        Assert.Equal(outboxEntryId, retryClaim.OutboxEntryId);
        Assert.Equal("worker-retry", retryClaim.WorkerId);
        Assert.Equal(2, retryClaim.Entry.ClaimAttemptCount);
        Assert.Empty(losingContext.ChangeTracker.Entries<AsiBackboneGovernanceOutboxEntryEntity>());
    }

    /// <summary>
    /// Verifies a claimed-state update conflict reloads and returns the current durable winner rather than the losing transition.
    /// </summary>
    [Fact]
    public async Task ClaimedUpdateConcurrencyConflictReloadsCurrentDurableEntry()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection keeperConnection = await EfCoreGovernanceOutboxTestHost.OpenSharedMemoryKeeperConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> durableOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(keeperConnection.ConnectionString);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(durableOptions, cancellationToken);

        var interceptor = new OneShotSaveChangesInterceptor();
        DbContextOptions<GovernanceOutboxTestDbContext> staleOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(
            keeperConnection.ConnectionString,
            interceptor);
        DateTimeOffset claimUtc = new(2026, 7, 12, 17, 15, 0, TimeSpan.Zero);
        var winningError = GovernanceEmissionError.Create(
            "provider.concurrent-winner",
            "A concurrent worker persisted the durable failure.",
            isRetryable: false,
            providerName: "provider-winner",
            providerErrorCode: "winner-409");
        DateTimeOffset winningRetryUtc = claimUtc.AddMinutes(10);

        await using GovernanceOutboxTestDbContext staleContext = new(staleOptions);
        var staleStore = new EfCoreGovernanceOutboxStore(staleContext);
        GovernanceOutboxClaim claim = await SaveAndClaimAsync(
            staleContext,
            staleStore,
            "claimed-update-conflict",
            claimUtc,
            cancellationToken);
        interceptor.Arm(async callbackCancellationToken =>
        {
            await using GovernanceOutboxTestDbContext winningContext = new(durableOptions);
            var winningStore = new EfCoreGovernanceOutboxStore(winningContext);
            _ = await winningStore.MarkFailedAsync(
                claim.OutboxEntryId,
                winningError,
                winningRetryUtc,
                callbackCancellationToken);
        });

        GovernanceOutboxEntry result = await staleStore.MarkClaimDeliveredAsync(
            claim,
            GovernanceEmissionResult.Delivered(
                "provider-losing",
                "record-losing",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["concurrency.result"] = "losing"
                }),
            cancellationToken);

        Assert.Equal(GovernanceEmissionStatus.Failed, result.Status);
        Assert.Equal(1, result.RetryCount);
        Assert.Equal(winningRetryUtc, result.NextRetryUtc);
        Assert.Equal(winningError.Code, result.LastError?.Code);
        Assert.Equal(winningError.ProviderErrorCode, result.LastError?.ProviderErrorCode);
        Assert.Equal("provider-winner", result.ProviderName);
        Assert.Null(result.ProviderRecordId);
        Assert.False(result.Metadata.ContainsKey("concurrency.result"));
        Assert.False(result.HasClaim);
        Assert.Empty(staleContext.ChangeTracker.Entries<AsiBackboneGovernanceOutboxEntryEntity>());

        await using GovernanceOutboxTestDbContext verificationContext = new(durableOptions);
        var verificationStore = new EfCoreGovernanceOutboxStore(verificationContext);
        GovernanceOutboxEntry? persisted = await verificationStore.FindByOutboxEntryIdAsync(claim.OutboxEntryId, cancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal(result.Status, persisted.Status);
        Assert.Equal(result.LastError?.Code, persisted.LastError?.Code);
        Assert.Equal(result.NextRetryUtc, persisted.NextRetryUtc);
        Assert.Null(persisted.ProviderRecordId);
    }

    /// <summary>
    /// Verifies conflict recovery returns the pre-update claimed snapshot when the durable row disappears during the race.
    /// </summary>
    [Fact]
    public async Task ClaimedUpdateConcurrencyConflictUsesSafeFallbackWhenDurableRowDisappears()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection keeperConnection = await EfCoreGovernanceOutboxTestHost.OpenSharedMemoryKeeperConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> durableOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(keeperConnection.ConnectionString);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(durableOptions, cancellationToken);

        var interceptor = new OneShotSaveChangesInterceptor();
        DbContextOptions<GovernanceOutboxTestDbContext> staleOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(
            keeperConnection.ConnectionString,
            interceptor);
        DateTimeOffset claimUtc = new(2026, 7, 12, 17, 30, 0, TimeSpan.Zero);

        await using GovernanceOutboxTestDbContext staleContext = new(staleOptions);
        var staleStore = new EfCoreGovernanceOutboxStore(staleContext);
        GovernanceOutboxClaim claim = await SaveAndClaimAsync(
            staleContext,
            staleStore,
            "claimed-update-deleted",
            claimUtc,
            cancellationToken);
        interceptor.Arm(async callbackCancellationToken =>
        {
            await using GovernanceOutboxTestDbContext deletingContext = new(durableOptions);
            AsiBackboneGovernanceOutboxEntryEntity entity = await deletingContext.GovernanceOutboxEntries
                .SingleAsync(item => item.OutboxEntryId == claim.OutboxEntryId, callbackCancellationToken);
            _ = deletingContext.GovernanceOutboxEntries.Remove(entity);
            _ = await deletingContext.SaveChangesAsync(callbackCancellationToken);
        });

        GovernanceOutboxEntry result = await staleStore.MarkClaimDeliveredAsync(
            claim,
            GovernanceEmissionResult.Delivered("provider-removed-row", "record-removed-row"),
            cancellationToken);

        Assert.Equal(GovernanceEmissionStatus.Pending, result.Status);
        Assert.Equal(claim.WorkerId, result.ClaimOwner);
        Assert.Equal(claim.ClaimToken, result.ClaimToken);
        Assert.Equal(claim.ClaimedUtc, result.ClaimedUtc);
        Assert.Equal(claim.ClaimExpiresUtc, result.ClaimExpiresUtc);
        Assert.Equal(1, result.ClaimAttemptCount);
        Assert.Null(result.ProviderName);
        Assert.Null(result.ProviderRecordId);
        Assert.Empty(staleContext.ChangeTracker.Entries<AsiBackboneGovernanceOutboxEntryEntity>());

        await using GovernanceOutboxTestDbContext verificationContext = new(durableOptions);
        var verificationStore = new EfCoreGovernanceOutboxStore(verificationContext);
        GovernanceOutboxEntry? persisted = await verificationStore.FindByOutboxEntryIdAsync(claim.OutboxEntryId, cancellationToken);

        Assert.Null(persisted);
    }

    private static async Task SeedPendingEntryAsync(
        DbContextOptions<GovernanceOutboxTestDbContext> options,
        string outboxEntryId,
        CancellationToken cancellationToken)
    {
        await using GovernanceOutboxTestDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);
        var entry = GovernanceOutboxEntry.Create(
            EfCoreGovernanceOutboxTestHost.CreateEnvelope($"event-{outboxEntryId}"),
            outboxEntryId,
            new DateTimeOffset(2026, 7, 12, 16, 30, 0, TimeSpan.Zero));
        _ = await store.SaveAsync(entry, cancellationToken);
    }

    private static async Task<GovernanceOutboxClaim> SaveAndClaimAsync(
        GovernanceOutboxTestDbContext context,
        EfCoreGovernanceOutboxStore store,
        string outboxEntryId,
        DateTimeOffset claimUtc,
        CancellationToken cancellationToken)
    {
        var entry = GovernanceOutboxEntry.Create(
            EfCoreGovernanceOutboxTestHost.CreateEnvelope($"event-{outboxEntryId}"),
            outboxEntryId,
            claimUtc.AddMinutes(-5));
        _ = await store.SaveAsync(entry, cancellationToken);
        context.ChangeTracker.Clear();

        return Assert.Single(await store.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create(
                "worker-original",
                claimUtc,
                TimeSpan.FromMinutes(5),
                maxCount: 1),
            cancellationToken));
    }

    private sealed class OneShotSaveChangesInterceptor : SaveChangesInterceptor
    {
        private Func<CancellationToken, Task>? beforeSaveAsync;

        public void Arm(Func<CancellationToken, Task> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);

            if (Interlocked.CompareExchange(ref beforeSaveAsync, callback, null) is not null)
            {
                throw new InvalidOperationException("The save interceptor is already armed.");
            }
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _ = eventData;
            Func<CancellationToken, Task>? callback = Interlocked.Exchange(ref beforeSaveAsync, null);
            if (callback is not null)
            {
                await callback(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
    }

    private sealed class OneShotCommandInterceptor : DbCommandInterceptor
    {
        private Func<CancellationToken, Task>? beforeCommandAsync;

        public void Arm(Func<CancellationToken, Task> callback)
        {
            ArgumentNullException.ThrowIfNull(callback);

            if (Interlocked.CompareExchange(ref beforeCommandAsync, callback, null) is not null)
            {
                throw new InvalidOperationException("The command interceptor is already armed.");
            }
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _ = command;
            _ = eventData;
            Func<CancellationToken, Task>? callback = Interlocked.Exchange(ref beforeCommandAsync, null);
            if (callback is not null)
            {
                await callback(cancellationToken).ConfigureAwait(false);
            }

            return result;
        }
    }
}
