using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Outbox;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests.Outbox.Providers;

/// <summary>
/// Opt-in contention coverage for <see cref="EfCoreGovernanceOutboxStore" /> claim acquisition against real SQL Server
/// and PostgreSQL instances (issue #823).
/// </summary>
/// <remarks>
/// <para>
/// SQLite serializes writers at the database level, so the SQLite claim tests cannot show what happens when two
/// workers' claim statements genuinely overlap on a server that locks rows. These tests run against the real
/// providers and check one invariant: cooperating workers never concurrently hold the same active claim. Delivery is
/// still at-least-once; the invariant covers claim acquisition only.
/// </para>
/// <para>
/// The overlap tests are deterministic rather than timing-based. The holder claims inside an open transaction, so its
/// claimed rows stay locked; the contender then starts its claim, the test waits until the database reports the
/// contender waiting on a lock (or the contender finishes without waiting), and only then does the holder commit.
/// That forces the contender's claim statement to be evaluated before, and to resume after, the holder's claim
/// becomes visible, which is the interleaving a stale candidate read would exploit.
/// </para>
/// <para>
/// Every test is skipped unless its provider connection string is configured. See <see cref="ProviderOutboxDatabase" />.
/// </para>
/// </remarks>
[Trait("Category", "ProviderContention")]
public sealed class EfCoreGovernanceOutboxProviderContentionTests
{
    private const int EligibleRowCount = 6;
    private const int ClaimBatchSize = 4;
    private const int StressRowCount = 60;
    private const int StressWorkerCount = 6;
    private const int StressBatchSize = 3;
    private const int StressMaxAttemptsPerWorker = 500;
    private const int TestTimeoutMilliseconds = 180_000;

    private static readonly DateTimeOffset ClaimUtc = new(2026, 9, 24, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan InterleavingTimeout = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan StressEmptyBatchRetryDelay = TimeSpan.FromMilliseconds(20);

    private delegate ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimOperation(
        EfCoreGovernanceOutboxStore store,
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken);

    private enum Interleaving
    {
        ContenderWaitedOnHolderLocks,
        ContenderFinishedBeforeHolderCommitted,
        NotObserved,
    }

    /// <summary>
    /// Verifies that overlapping pending claims from two independent contexts never share an active claim.
    /// </summary>
    [Theory(Timeout = TestTimeoutMilliseconds)]
    [InlineData(OutboxContentionProvider.SqlServerLockingReadCommitted)]
    [InlineData(OutboxContentionProvider.SqlServerReadCommittedSnapshot)]
    [InlineData(OutboxContentionProvider.PostgreSql)]
    public async Task OverlappingPendingClaimsNeverShareAnActiveClaim(OutboxContentionProvider provider)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using ProviderOutboxDatabase database = await ProviderOutboxDatabase.CreateAsync(provider, cancellationToken);
        string[] eligibleIds = await SeedPendingAsync(database, "pending", EligibleRowCount, cancellationToken);

        ContentionOutcome outcome = await ClaimWithOverlapAsync(
            database,
            (store, request, token) => store.ClaimPendingAsync(request, token),
            cancellationToken);

        await AssertClaimInvariantsAsync(database, outcome, eligibleIds, cancellationToken);
    }

    /// <summary>
    /// Verifies that overlapping retry-ready claims from two independent contexts never share an active claim.
    /// </summary>
    [Theory(Timeout = TestTimeoutMilliseconds)]
    [InlineData(OutboxContentionProvider.SqlServerLockingReadCommitted)]
    [InlineData(OutboxContentionProvider.SqlServerReadCommittedSnapshot)]
    [InlineData(OutboxContentionProvider.PostgreSql)]
    public async Task OverlappingRetryReadyClaimsNeverShareAnActiveClaim(OutboxContentionProvider provider)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using ProviderOutboxDatabase database = await ProviderOutboxDatabase.CreateAsync(provider, cancellationToken);
        string[] eligibleIds = await SeedPendingAsync(database, "retry", EligibleRowCount, cancellationToken);

        await using (GovernanceOutboxTestDbContext context = database.CreateContext())
        {
            var store = new EfCoreGovernanceOutboxStore(context);
            var error = GovernanceEmissionError.Create(
                "provider.transient",
                "Simulated transient provider failure.",
                isRetryable: true);

            foreach (string outboxEntryId in eligibleIds)
            {
                _ = await store.MarkFailedAsync(outboxEntryId, error, ClaimUtc.AddMinutes(-1), cancellationToken);
            }

            IReadOnlyList<GovernanceOutboxEntry> retryReady = await store.FindRetryReadyAsync(ClaimUtc, cancellationToken: cancellationToken);
            IReadOnlyList<GovernanceOutboxEntry> pending = await store.FindPendingAsync(cancellationToken: cancellationToken);
            Assert.Equal(EligibleRowCount, retryReady.Count);
            Assert.Empty(pending);
        }

        ContentionOutcome outcome = await ClaimWithOverlapAsync(
            database,
            (store, request, token) => store.ClaimRetryReadyAsync(request, token),
            cancellationToken);

        await AssertClaimInvariantsAsync(database, outcome, eligibleIds, cancellationToken);
    }

    /// <summary>
    /// Verifies that two workers reclaiming the same expired leases never both take over the same entry.
    /// </summary>
    [Theory(Timeout = TestTimeoutMilliseconds)]
    [InlineData(OutboxContentionProvider.SqlServerLockingReadCommitted)]
    [InlineData(OutboxContentionProvider.SqlServerReadCommittedSnapshot)]
    [InlineData(OutboxContentionProvider.PostgreSql)]
    public async Task OverlappingReclaimsOfExpiredLeasesNeverShareAnActiveClaim(OutboxContentionProvider provider)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using ProviderOutboxDatabase database = await ProviderOutboxDatabase.CreateAsync(provider, cancellationToken);
        string[] eligibleIds = await SeedPendingAsync(database, "reclaim", EligibleRowCount, cancellationToken);

        IReadOnlyList<GovernanceOutboxClaim> expiredClaims;
        await using (GovernanceOutboxTestDbContext context = database.CreateContext())
        {
            var store = new EfCoreGovernanceOutboxStore(context);
            expiredClaims = await store.ClaimPendingAsync(
                GovernanceOutboxClaimRequest.Create("worker-crashed", ClaimUtc.AddMinutes(-10), TimeSpan.FromMinutes(1), EligibleRowCount),
                cancellationToken);
        }

        Assert.Equal(EligibleRowCount, expiredClaims.Count);
        Assert.All(expiredClaims, claim => Assert.True(claim.IsExpired(ClaimUtc)));

        ContentionOutcome outcome = await ClaimWithOverlapAsync(
            database,
            (store, request, token) => store.ClaimPendingAsync(request, token),
            cancellationToken);

        await AssertClaimInvariantsAsync(database, outcome, eligibleIds, cancellationToken);

        HashSet<string> expiredTokens = [.. expiredClaims.Select(claim => claim.ClaimToken)];
        Assert.All(outcome.AllClaims, claim => Assert.DoesNotContain(claim.ClaimToken, expiredTokens));
    }

    /// <summary>
    /// Verifies that several workers claiming from the same pending backlog at once each receive disjoint entries and
    /// that, between them, they claim every entry exactly once.
    /// </summary>
    /// <remarks>
    /// This complements the deterministic overlap tests with unscripted autocommit contention. The lease never
    /// expires at the fixed claim time and nothing completes, so any entry claimed twice was claimed concurrently. A
    /// worker that the database chooses as a deadlock or serialization victim retries on its next attempt; those
    /// retries are counted and reported, not treated as success.
    /// <para>
    /// An empty batch does not end a worker. A worker that waited on rows another worker claimed can legitimately
    /// receive an empty batch while other entries are still eligible, so workers stop only once the shared claimed
    /// count reaches the seeded backlog. A worker that exhausts its attempts first reports itself incomplete, which
    /// keeps a claim path that stops making progress from hanging the test.
    /// </para>
    /// </remarks>
    [Theory(Timeout = TestTimeoutMilliseconds)]
    [InlineData(OutboxContentionProvider.SqlServerLockingReadCommitted)]
    [InlineData(OutboxContentionProvider.SqlServerReadCommittedSnapshot)]
    [InlineData(OutboxContentionProvider.PostgreSql)]
    public async Task ConcurrentWorkersClaimEachPendingEntryExactlyOnce(OutboxContentionProvider provider)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using ProviderOutboxDatabase database = await ProviderOutboxDatabase.CreateAsync(provider, cancellationToken);
        string[] eligibleIds = await SeedPendingAsync(database, "stress", StressRowCount, cancellationToken);

        var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new StressProgress();
        Task<StressWorkerResult>[] workers = [.. Enumerable
            .Range(0, StressWorkerCount)
            .Select(index => RunStressWorkerAsync(database, $"worker-{index:D2}", start.Task, progress, cancellationToken))];

        start.SetResult();
        StressWorkerResult[] results = await Task.WhenAll(workers);

        GovernanceOutboxClaim[] allClaims = [.. results.SelectMany(result => result.Claims)];
        string[] duplicateIds = [.. allClaims
            .GroupBy(claim => claim.OutboxEntryId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)];
        int transientVictims = results.Sum(result => result.TransientContentionCount);

        Assert.True(
            duplicateIds.Length == 0,
            $"{provider}: entries claimed by more than one worker: {string.Join(", ", duplicateIds)} " +
            $"({transientVictims} transient contention retries).");
        Assert.Equal(
            eligibleIds.Order(StringComparer.Ordinal),
            allClaims.Select(claim => claim.OutboxEntryId).Order(StringComparer.Ordinal));
        Assert.All(results, result => Assert.True(result.Completed, $"{result.WorkerId} did not drain the backlog."));

        await AssertClaimsStillHeldAsync(database, allClaims, cancellationToken);
    }

    private static async Task<ContentionOutcome> ClaimWithOverlapAsync(
        ProviderOutboxDatabase database,
        ClaimOperation claim,
        CancellationToken cancellationToken)
    {
        await using GovernanceOutboxTestDbContext holderContext = database.CreateContext();
        await using GovernanceOutboxTestDbContext contenderContext = database.CreateContext();
        var holderStore = new EfCoreGovernanceOutboxStore(holderContext);
        var contenderStore = new EfCoreGovernanceOutboxStore(contenderContext);

        // Open the contender's connection first so connection setup cannot be mistaken for the claim waiting on locks.
        await contenderContext.Database.OpenConnectionAsync(cancellationToken);

        await using IDbContextTransaction holderTransaction = await holderContext.Database.BeginTransactionAsync(cancellationToken);
        IReadOnlyList<GovernanceOutboxClaim> holderClaims = await claim(holderStore, CreateRequest("worker-holder"), cancellationToken);
        Assert.NotEmpty(holderClaims);

        Task<IReadOnlyList<GovernanceOutboxClaim>> contenderClaim = claim(
            contenderStore,
            CreateRequest("worker-contender"),
            cancellationToken).AsTask();

        Interleaving interleaving = await WaitForInterleavingAsync(database, contenderClaim, cancellationToken);

        await holderTransaction.CommitAsync(cancellationToken);
        IReadOnlyList<GovernanceOutboxClaim> contenderClaims = await contenderClaim;

        Assert.True(
            interleaving is not Interleaving.NotObserved,
            $"{database.Provider}: the contender neither waited on the holder's locks nor finished within {InterleavingTimeout}, " +
            "so the overlap this test depends on was not established.");

        return new ContentionOutcome(database.Provider, interleaving, holderClaims, contenderClaims);
    }

    private static async Task<Interleaving> WaitForInterleavingAsync(
        ProviderOutboxDatabase database,
        Task contenderClaim,
        CancellationToken cancellationToken)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.Add(InterleavingTimeout);

        while (DateTimeOffset.UtcNow < deadline)
        {
            if (contenderClaim.IsCompleted)
            {
                return Interleaving.ContenderFinishedBeforeHolderCommitted;
            }

            if (await database.CountLockWaitingSessionsAsync(cancellationToken) > 0)
            {
                return Interleaving.ContenderWaitedOnHolderLocks;
            }

            await Task.Delay(PollInterval, cancellationToken);
        }

        return contenderClaim.IsCompleted
            ? Interleaving.ContenderFinishedBeforeHolderCommitted
            : Interleaving.NotObserved;
    }

    private static async Task AssertClaimInvariantsAsync(
        ProviderOutboxDatabase database,
        ContentionOutcome outcome,
        string[] eligibleIds,
        CancellationToken cancellationToken)
    {
        string context = $"{outcome.Provider} ({outcome.Interleaving})";
        string[] holderIds = [.. outcome.HolderClaims.Select(claim => claim.OutboxEntryId)];
        string[] contenderIds = [.. outcome.ContenderClaims.Select(claim => claim.OutboxEntryId)];
        string[] sharedIds = [.. holderIds.Intersect(contenderIds, StringComparer.Ordinal)];

        Assert.True(
            sharedIds.Length == 0,
            $"{context}: both workers received claims for {string.Join(", ", sharedIds)}.");
        Assert.True(
            holderIds.Length + contenderIds.Length <= eligibleIds.Length,
            $"{context}: {holderIds.Length} + {contenderIds.Length} claims exceed the {eligibleIds.Length} eligible entries.");
        Assert.InRange(holderIds.Length, 1, ClaimBatchSize);
        Assert.InRange(contenderIds.Length, 0, ClaimBatchSize);
        Assert.All(holderIds.Concat(contenderIds), outboxEntryId => Assert.Contains(outboxEntryId, eligibleIds));

        await AssertClaimsStillHeldAsync(database, outcome.AllClaims, cancellationToken);
    }

    // A claim returned to a worker must still be the active claim on its row. If a later claim statement silently
    // replaced the owner and token after the first worker read them back, both workers would believe they hold the
    // entry even though the returned claim sets do not overlap.
    private static async Task AssertClaimsStillHeldAsync(
        ProviderOutboxDatabase database,
        IEnumerable<GovernanceOutboxClaim> claims,
        CancellationToken cancellationToken)
    {
        await using GovernanceOutboxTestDbContext context = database.CreateContext();
        var store = new EfCoreGovernanceOutboxStore(context);

        foreach (GovernanceOutboxClaim claim in claims)
        {
            GovernanceOutboxEntry? persisted = await store.FindByOutboxEntryIdAsync(claim.OutboxEntryId, cancellationToken);

            Assert.NotNull(persisted);
            Assert.True(
                persisted.IsClaimedBy(claim),
                $"{database.Provider}: {claim.WorkerId} was returned a claim on {claim.OutboxEntryId}, but the row is now " +
                $"claimed by {persisted.ClaimOwner ?? "nobody"}.");
        }
    }

    private static async Task<StressWorkerResult> RunStressWorkerAsync(
        ProviderOutboxDatabase database,
        string workerId,
        Task start,
        StressProgress progress,
        CancellationToken cancellationToken)
    {
        await using GovernanceOutboxTestDbContext context = database.CreateContext();
        var store = new EfCoreGovernanceOutboxStore(context);
        await context.Database.OpenConnectionAsync(cancellationToken);

        List<GovernanceOutboxClaim> claims = [];
        int transientContentionCount = 0;
        await start;

        for (int attempt = 0; attempt < StressMaxAttemptsPerWorker; attempt++)
        {
            IReadOnlyList<GovernanceOutboxClaim> batch;
            try
            {
                batch = await store.ClaimPendingAsync(
                    GovernanceOutboxClaimRequest.Create(workerId, ClaimUtc, LeaseDuration, StressBatchSize),
                    cancellationToken);
            }
            catch (Exception exception) when (IsTransientContention(exception))
            {
                transientContentionCount++;
                context.ChangeTracker.Clear();
                continue;
            }

            if (batch.Count > 0)
            {
                claims.AddRange(batch);
                progress.Record(batch.Count);
                continue;
            }

            // An empty batch can mean this worker lost its candidates to another worker while other entries remain
            // eligible, so only the shared claimed count decides that the backlog is drained.
            if (progress.Claimed >= StressRowCount)
            {
                return new StressWorkerResult(workerId, claims, transientContentionCount, Completed: true);
            }

            await Task.Delay(StressEmptyBatchRetryDelay, cancellationToken);
        }

        return new StressWorkerResult(workerId, claims, transientContentionCount, Completed: false);
    }

    private static bool IsTransientContention(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            // SQL Server 1205: chosen as deadlock victim. PostgreSQL 40P01: deadlock detected; 40001: serialization failure.
            if (current is SqlException { Number: 1205 } or PostgresException { SqlState: "40P01" or "40001" })
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<string[]> SeedPendingAsync(
        ProviderOutboxDatabase database,
        string prefix,
        int count,
        CancellationToken cancellationToken)
    {
        await using GovernanceOutboxTestDbContext context = database.CreateContext();
        var store = new EfCoreGovernanceOutboxStore(context);
        string[] outboxEntryIds = new string[count];

        for (int index = 0; index < count; index++)
        {
            GovernanceOutboxEntry entry = await store.EnqueueAsync(
                EfCoreGovernanceOutboxTestHost.CreateEnvelope($"issue-823-{prefix}-{index:D3}"),
                cancellationToken);
            outboxEntryIds[index] = entry.OutboxEntryId;
        }

        return outboxEntryIds;
    }

    private static GovernanceOutboxClaimRequest CreateRequest(string workerId)
    {
        return GovernanceOutboxClaimRequest.Create(workerId, ClaimUtc, LeaseDuration, ClaimBatchSize);
    }

    private sealed record ContentionOutcome(
        OutboxContentionProvider Provider,
        Interleaving Interleaving,
        IReadOnlyList<GovernanceOutboxClaim> HolderClaims,
        IReadOnlyList<GovernanceOutboxClaim> ContenderClaims)
    {
        public IEnumerable<GovernanceOutboxClaim> AllClaims => HolderClaims.Concat(ContenderClaims);
    }

    private sealed class StressProgress
    {
        private int claimed;

        public int Claimed => Volatile.Read(ref claimed);

        public void Record(int count)
        {
            _ = Interlocked.Add(ref claimed, count);
        }
    }

    private sealed record StressWorkerResult(
        string WorkerId,
        IReadOnlyList<GovernanceOutboxClaim> Claims,
        int TransientContentionCount,
        bool Completed);
}
