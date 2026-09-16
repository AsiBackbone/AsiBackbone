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
/// Deterministic relational coverage for explicit EF Core claimed-transition outcomes.
/// </summary>
public sealed class EfCoreGovernanceOutboxClaimOutcomeTests
{
    /// <summary>
    /// Verifies a caller-owned transition reports an applied outcome.
    /// </summary>
    [Fact]
    public async Task TryMarkClaimDeliveredReportsApplied()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection connection = await EfCoreGovernanceOutboxTestHost.OpenConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> options = EfCoreGovernanceOutboxTestHost.CreateOptions(connection);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(options, cancellationToken);

        await using GovernanceOutboxTestDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxOutcomeStore(context);
        GovernanceOutboxClaim claim = await SaveAndClaimAsync(
            context,
            store,
            "claim-outcome-applied",
            cancellationToken);

        GovernanceOutboxClaimTransitionResult transition = await store.TryMarkClaimDeliveredAsync(
            claim,
            GovernanceEmissionResult.Delivered("provider-applied", "record-applied"),
            cancellationToken);

        Assert.Equal(GovernanceOutboxClaimTransitionOutcome.Applied, transition.Outcome);
        Assert.True(transition.IsApplied);
        Assert.True(transition.Entry.IsDelivered);
        Assert.Equal("record-applied", transition.Entry.ProviderRecordId);
        Assert.False(transition.Entry.HasClaim);
    }

    /// <summary>
    /// Verifies a stale claim is distinguished from a database concurrency race.
    /// </summary>
    [Fact]
    public async Task TryMarkClaimDeliveredReportsStaleClaimAfterRelease()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection connection = await EfCoreGovernanceOutboxTestHost.OpenConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> options = EfCoreGovernanceOutboxTestHost.CreateOptions(connection);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(options, cancellationToken);

        await using GovernanceOutboxTestDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxOutcomeStore(context);
        GovernanceOutboxClaim claim = await SaveAndClaimAsync(
            context,
            store,
            "claim-outcome-stale",
            cancellationToken);
        _ = await store.ReleaseClaimAsync(claim, cancellationToken: cancellationToken);
        context.ChangeTracker.Clear();

        GovernanceOutboxClaimTransitionResult transition = await store.TryMarkClaimDeliveredAsync(
            claim,
            GovernanceEmissionResult.Delivered("provider-stale", "record-stale"),
            cancellationToken);

        Assert.Equal(GovernanceOutboxClaimTransitionOutcome.StaleClaim, transition.Outcome);
        Assert.False(transition.IsApplied);
        Assert.Equal(GovernanceEmissionStatus.Pending, transition.Entry.Status);
        Assert.False(transition.Entry.HasClaim);
    }

    /// <summary>
    /// Verifies a durable terminal row observed before the attempt reports a terminal no-op.
    /// </summary>
    [Fact]
    public async Task TryMarkClaimFailedReportsTerminalWhenDurableEntryAlreadyDelivered()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection connection = await EfCoreGovernanceOutboxTestHost.OpenConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> options = EfCoreGovernanceOutboxTestHost.CreateOptions(connection);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(options, cancellationToken);

        await using GovernanceOutboxTestDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxOutcomeStore(context);
        GovernanceOutboxClaim claim = await SaveAndClaimAsync(
            context,
            store,
            "claim-outcome-terminal",
            cancellationToken);
        _ = await store.MarkDeliveredAsync(
            claim.OutboxEntryId,
            GovernanceEmissionResult.Delivered("provider-terminal", "record-terminal"),
            cancellationToken);
        context.ChangeTracker.Clear();

        GovernanceOutboxClaimTransitionResult transition = await store.TryMarkClaimFailedAsync(
            claim,
            GovernanceEmissionError.Create("provider.late-failure", "A stale worker attempted a late failure."),
            cancellationToken: cancellationToken);

        Assert.Equal(GovernanceOutboxClaimTransitionOutcome.Terminal, transition.Outcome);
        Assert.False(transition.IsApplied);
        Assert.True(transition.Entry.IsDelivered);
        Assert.Equal("record-terminal", transition.Entry.ProviderRecordId);
    }

    /// <summary>
    /// Verifies a concurrent nonterminal writer is returned as the durable winner with a concurrency-lost outcome.
    /// </summary>
    [Fact]
    public async Task TryMarkClaimDeliveredReportsConcurrencyLostAndReturnsDurableWinner()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection keeperConnection = await EfCoreGovernanceOutboxTestHost.OpenSharedMemoryKeeperConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> durableOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(keeperConnection.ConnectionString);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(durableOptions, cancellationToken);

        var interceptor = new OneShotSaveChangesInterceptor();
        DbContextOptions<GovernanceOutboxTestDbContext> staleOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(
            keeperConnection.ConnectionString,
            interceptor);
        DateTimeOffset retryUtc = new(2026, 7, 13, 13, 30, 0, TimeSpan.Zero);

        await using GovernanceOutboxTestDbContext staleContext = new(staleOptions);
        var staleStore = new EfCoreGovernanceOutboxOutcomeStore(staleContext);
        GovernanceOutboxClaim claim = await SaveAndClaimAsync(
            staleContext,
            staleStore,
            "claim-outcome-concurrency",
            cancellationToken);
        interceptor.Arm(async callbackCancellationToken =>
        {
            await using GovernanceOutboxTestDbContext winningContext = new(durableOptions);
            var winningStore = new EfCoreGovernanceOutboxStore(winningContext);
            _ = await winningStore.MarkFailedAsync(
                claim.OutboxEntryId,
                GovernanceEmissionError.Create(
                    "provider.concurrent-winner",
                    "The concurrent worker persisted the durable failure.",
                    isRetryable: true,
                    providerName: "provider-winner"),
                retryUtc,
                callbackCancellationToken);
        });

        GovernanceOutboxClaimTransitionResult transition = await staleStore.TryMarkClaimDeliveredAsync(
            claim,
            GovernanceEmissionResult.Delivered("provider-loser", "record-loser"),
            cancellationToken);

        Assert.Equal(GovernanceOutboxClaimTransitionOutcome.ConcurrencyLost, transition.Outcome);
        Assert.False(transition.IsApplied);
        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, transition.Entry.Status);
        Assert.Equal("provider.concurrent-winner", transition.Entry.LastError?.Code);
        Assert.Equal(retryUtc, transition.Entry.NextRetryUtc);
        Assert.Null(transition.Entry.ProviderRecordId);
    }

    /// <summary>
    /// Verifies a concurrent terminal winner remains a concurrency loss rather than being attributed to the losing worker.
    /// </summary>
    [Fact]
    public async Task TryMarkClaimFailedReportsConcurrencyLostWhenConcurrentWinnerIsTerminal()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection keeperConnection = await EfCoreGovernanceOutboxTestHost.OpenSharedMemoryKeeperConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> durableOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(keeperConnection.ConnectionString);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(durableOptions, cancellationToken);

        var interceptor = new OneShotSaveChangesInterceptor();
        DbContextOptions<GovernanceOutboxTestDbContext> staleOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(
            keeperConnection.ConnectionString,
            interceptor);

        await using GovernanceOutboxTestDbContext staleContext = new(staleOptions);
        var staleStore = new EfCoreGovernanceOutboxOutcomeStore(staleContext);
        GovernanceOutboxClaim claim = await SaveAndClaimAsync(
            staleContext,
            staleStore,
            "claim-outcome-terminal-winner",
            cancellationToken);
        interceptor.Arm(async callbackCancellationToken =>
        {
            await using GovernanceOutboxTestDbContext winningContext = new(durableOptions);
            var winningStore = new EfCoreGovernanceOutboxStore(winningContext);
            _ = await winningStore.MarkDeliveredAsync(
                claim.OutboxEntryId,
                GovernanceEmissionResult.Delivered("provider-winning-terminal", "record-winning-terminal"),
                callbackCancellationToken);
        });

        GovernanceOutboxClaimTransitionResult transition = await staleStore.TryMarkClaimFailedAsync(
            claim,
            GovernanceEmissionError.Create("provider.losing-failure", "The losing worker attempted failure."),
            cancellationToken: cancellationToken);

        Assert.Equal(GovernanceOutboxClaimTransitionOutcome.ConcurrencyLost, transition.Outcome);
        Assert.False(transition.IsApplied);
        Assert.True(transition.Entry.IsDelivered);
        Assert.Equal("record-winning-terminal", transition.Entry.ProviderRecordId);
    }

    /// <summary>
    /// Verifies deletion during the write race reports missing and returns the caller's last safe claimed snapshot.
    /// </summary>
    [Fact]
    public async Task TryMarkClaimDeliveredReportsMissingWhenDurableRowIsDeleted()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection keeperConnection = await EfCoreGovernanceOutboxTestHost.OpenSharedMemoryKeeperConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> durableOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(keeperConnection.ConnectionString);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(durableOptions, cancellationToken);

        var interceptor = new OneShotSaveChangesInterceptor();
        DbContextOptions<GovernanceOutboxTestDbContext> staleOptions = EfCoreGovernanceOutboxTestHost.CreateOptions(
            keeperConnection.ConnectionString,
            interceptor);

        await using GovernanceOutboxTestDbContext staleContext = new(staleOptions);
        var staleStore = new EfCoreGovernanceOutboxOutcomeStore(staleContext);
        GovernanceOutboxClaim claim = await SaveAndClaimAsync(
            staleContext,
            staleStore,
            "claim-outcome-missing",
            cancellationToken);
        interceptor.Arm(async callbackCancellationToken =>
        {
            await using GovernanceOutboxTestDbContext deletingContext = new(durableOptions);
            GovernanceOutboxEntryEntity entity = await deletingContext.GovernanceOutboxEntries
                .SingleAsync(item => item.OutboxEntryId == claim.OutboxEntryId, callbackCancellationToken);
            _ = deletingContext.GovernanceOutboxEntries.Remove(entity);
            _ = await deletingContext.SaveChangesAsync(callbackCancellationToken);
        });

        GovernanceOutboxClaimTransitionResult transition = await staleStore.TryMarkClaimDeliveredAsync(
            claim,
            GovernanceEmissionResult.Delivered("provider-missing", "record-missing"),
            cancellationToken);

        Assert.Equal(GovernanceOutboxClaimTransitionOutcome.Missing, transition.Outcome);
        Assert.False(transition.IsApplied);
        Assert.Equal(claim.OutboxEntryId, transition.Entry.OutboxEntryId);
        Assert.Equal(claim.ClaimToken, transition.Entry.ClaimToken);
        Assert.Equal(GovernanceEmissionStatus.Pending, transition.Entry.Status);
    }

    private static async Task<GovernanceOutboxClaim> SaveAndClaimAsync(
        GovernanceOutboxTestDbContext context,
        EfCoreGovernanceOutboxOutcomeStore store,
        string outboxEntryId,
        CancellationToken cancellationToken)
    {
        DateTimeOffset claimUtc = new(2026, 7, 13, 13, 0, 0, TimeSpan.Zero);
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
}
