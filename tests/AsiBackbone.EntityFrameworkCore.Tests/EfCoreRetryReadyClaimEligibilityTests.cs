using System.Reflection;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Outbox;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests;

/// <summary>
/// Focused branch and integration coverage for EF Core retry-ready claim eligibility.
/// </summary>
public sealed class EfCoreRetryReadyClaimEligibilityTests
{
    private static readonly MethodInfo RetryReadyEligibilityMethod = typeof(EfCoreGovernanceOutboxStore)
        .GetMethod("IsRetryReadyClaimEligible", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("The EF Core retry-ready claim eligibility method could not be located.");

    /// <summary>
    /// Verifies that every retry-capable status is eligible when retry count, timing, and claim state permit acquisition.
    /// </summary>
    /// <param name="status">The retry-capable status under test.</param>
    [Theory]
    [InlineData(GovernanceEmissionStatus.Deferred)]
    [InlineData(GovernanceEmissionStatus.Failed)]
    [InlineData(GovernanceEmissionStatus.RetryableFailure)]
    public void EligibilityAcceptsRetryCapableStatuses(GovernanceEmissionStatus status)
    {
        DateTimeOffset utcNow = new(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);
        AsiBackboneGovernanceOutboxEntryEntity entity = CreateEligibilityEntity(status: status);

        Assert.True(IsRetryReadyClaimEligible(entity, utcNow));
    }

    /// <summary>
    /// Verifies that pending and terminal statuses cannot enter the retry-ready claim path.
    /// </summary>
    /// <param name="status">The unsupported status under test.</param>
    [Theory]
    [InlineData(GovernanceEmissionStatus.Pending)]
    [InlineData(GovernanceEmissionStatus.Delivered)]
    [InlineData(GovernanceEmissionStatus.DeadLettered)]
    public void EligibilityRejectsNonRetryStatuses(GovernanceEmissionStatus status)
    {
        DateTimeOffset utcNow = new(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);
        AsiBackboneGovernanceOutboxEntryEntity entity = CreateEligibilityEntity(status: status);

        Assert.False(IsRetryReadyClaimEligible(entity, utcNow));
    }

    /// <summary>
    /// Verifies that the legacy persisted retry maximum does not override drain retry policy.
    /// </summary>
    [Fact]
    public void EligibilityIgnoresPersistedMaximumRetryCount()
    {
        DateTimeOffset utcNow = new(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);

        Assert.True(IsRetryReadyClaimEligible(
            CreateEligibilityEntity(retryCount: 4, maxRetryCount: 5),
            utcNow));
        Assert.True(IsRetryReadyClaimEligible(
            CreateEligibilityEntity(retryCount: 5, maxRetryCount: 5),
            utcNow));
        Assert.True(IsRetryReadyClaimEligible(
            CreateEligibilityEntity(retryCount: 6, maxRetryCount: 5),
            utcNow));
    }

    /// <summary>
    /// Verifies null, elapsed, exact-boundary, future, and offset-based retry timestamps.
    /// </summary>
    [Fact]
    public void EligibilityHonorsRetryTimingBoundaryAndUtcNormalization()
    {
        DateTimeOffset utcNow = new(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);
        DateTimeOffset equivalentOffsetNow = utcNow.ToOffset(TimeSpan.FromHours(-5));

        Assert.True(IsRetryReadyClaimEligible(
            CreateEligibilityEntity(nextRetryUtc: null),
            equivalentOffsetNow));
        Assert.True(IsRetryReadyClaimEligible(
            CreateEligibilityEntity(nextRetryUtc: utcNow.AddTicks(-1)),
            equivalentOffsetNow));
        Assert.True(IsRetryReadyClaimEligible(
            CreateEligibilityEntity(nextRetryUtc: utcNow),
            equivalentOffsetNow));
        Assert.False(IsRetryReadyClaimEligible(
            CreateEligibilityEntity(nextRetryUtc: utcNow.AddTicks(1)),
            equivalentOffsetNow));
    }

    /// <summary>
    /// Verifies that absent or elapsed claim fields make retry-ready work available at the exact lease boundary.
    /// </summary>
    [Fact]
    public void EligibilityAcceptsMissingAndExpiredClaims()
    {
        DateTimeOffset utcNow = new(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);

        Assert.True(IsRetryReadyClaimEligible(
            CreateEligibilityEntity(
                claimOwner: "legacy-worker",
                claimToken: null,
                claimedUtc: utcNow.AddMinutes(-1),
                claimExpiresUtc: utcNow.AddMinutes(1)),
            utcNow));
        Assert.True(IsRetryReadyClaimEligible(
            CreateEligibilityEntity(
                claimOwner: "legacy-worker",
                claimToken: "legacy-token",
                claimedUtc: utcNow.AddMinutes(-1),
                claimExpiresUtc: null),
            utcNow));
        Assert.True(IsRetryReadyClaimEligible(
            CreateEligibilityEntity(
                claimOwner: "legacy-worker",
                claimToken: "legacy-token",
                claimedUtc: utcNow.AddMinutes(-2),
                claimExpiresUtc: utcNow.AddTicks(-1)),
            utcNow));
        Assert.True(IsRetryReadyClaimEligible(
            CreateEligibilityEntity(
                claimOwner: "legacy-worker",
                claimToken: "legacy-token",
                claimedUtc: utcNow.AddMinutes(-2),
                claimExpiresUtc: utcNow),
            utcNow));
    }

    /// <summary>
    /// Verifies that an active token and lease block acquisition regardless of the recorded owner identity.
    /// </summary>
    /// <param name="claimOwner">The current owner value under test.</param>
    [Theory]
    [InlineData("requesting-worker")]
    [InlineData("conflicting-worker")]
    [InlineData(null)]
    public void EligibilityRejectsActiveClaimsRegardlessOfOwner(string? claimOwner)
    {
        DateTimeOffset utcNow = new(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);
        AsiBackboneGovernanceOutboxEntryEntity entity = CreateEligibilityEntity(
            claimOwner: claimOwner,
            claimToken: "active-token",
            claimedUtc: utcNow.AddMinutes(-1),
            claimExpiresUtc: utcNow.AddTicks(1));

        Assert.False(IsRetryReadyClaimEligible(entity, utcNow));
    }

    /// <summary>
    /// Verifies the public EF Core claim path accepts all retry statuses at exact UTC boundaries and normalizes the requesting worker.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ClaimRetryReadyClaimsSupportedStatusesAtExactBoundaries()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        DateTimeOffset utcNow = new(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);
        DateTimeOffset offsetNow = utcNow.ToOffset(TimeSpan.FromHours(-5));

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);
        await SaveEntriesAsync(
            store,
            CreateRetryEntry("retry-deferred", GovernanceEmissionStatus.Deferred, utcNow, nextRetryUtc: utcNow),
            CreateRetryEntry("retry-failed", GovernanceEmissionStatus.Failed, utcNow, nextRetryUtc: utcNow),
            CreateRetryEntry("retry-retryable", GovernanceEmissionStatus.RetryableFailure, utcNow, nextRetryUtc: utcNow));
        context.ChangeTracker.Clear();

        var request = GovernanceOutboxClaimRequest.Create(
            "  worker-boundary  ",
            offsetNow,
            TimeSpan.FromMinutes(2),
            maxCount: 10);
        IReadOnlyList<GovernanceOutboxClaim> claims = await store.ClaimRetryReadyAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(3, claims.Count);
        Assert.Equal(
            ["retry-deferred", "retry-failed", "retry-retryable"],
            claims.Select(claim => claim.OutboxEntryId).Order(StringComparer.Ordinal));
        Assert.All(claims, claim =>
        {
            Assert.Equal("worker-boundary", claim.WorkerId);
            Assert.Equal(utcNow, claim.ClaimedUtc);
            Assert.Equal(utcNow.AddMinutes(2), claim.ClaimExpiresUtc);
            Assert.Equal(TimeSpan.Zero, claim.ClaimedUtc.Offset);
            Assert.Equal(TimeSpan.Zero, claim.ClaimExpiresUtc.Offset);
        });
    }

    /// <summary>
    /// Verifies that an expired conflicting claim is reassigned while active claims remain protected for both the same and different workers.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ClaimRetryReadyReassignsExpiredClaimsAndPreservesActiveOwnership()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        DateTimeOffset utcNow = new(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);
        await SaveEntriesAsync(
            store,
            CreateRetryEntry(
                "expired-other-worker",
                GovernanceEmissionStatus.RetryableFailure,
                utcNow,
                claimOwner: "worker-b",
                claimToken: "expired-token",
                claimedUtc: utcNow.AddMinutes(-5),
                claimExpiresUtc: utcNow,
                claimAttemptCount: 2),
            CreateRetryEntry(
                "active-same-worker",
                GovernanceEmissionStatus.RetryableFailure,
                utcNow,
                claimOwner: "worker-a",
                claimToken: "same-worker-token",
                claimedUtc: utcNow.AddMinutes(-1),
                claimExpiresUtc: utcNow.AddMinutes(1)),
            CreateRetryEntry(
                "active-other-worker",
                GovernanceEmissionStatus.RetryableFailure,
                utcNow,
                claimOwner: "worker-b",
                claimToken: "other-worker-token",
                claimedUtc: utcNow.AddMinutes(-1),
                claimExpiresUtc: utcNow.AddMinutes(1)));
        context.ChangeTracker.Clear();

        var request = GovernanceOutboxClaimRequest.Create(
            "worker-a",
            utcNow,
            TimeSpan.FromMinutes(3),
            maxCount: 10);
        GovernanceOutboxClaim claim = Assert.Single(await store.ClaimRetryReadyAsync(
            request,
            TestContext.Current.CancellationToken));

        Assert.Equal("expired-other-worker", claim.OutboxEntryId);
        Assert.Equal("worker-a", claim.WorkerId);
        Assert.NotEqual("expired-token", claim.ClaimToken);
        Assert.Equal(3, claim.Entry.ClaimAttemptCount);

        context.ChangeTracker.Clear();
        GovernanceOutboxEntry? activeSame = await store.FindByOutboxEntryIdAsync(
            "active-same-worker",
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry? activeOther = await store.FindByOutboxEntryIdAsync(
            "active-other-worker",
            TestContext.Current.CancellationToken);

        Assert.NotNull(activeSame);
        Assert.Equal("worker-a", activeSame.ClaimOwner);
        Assert.Equal("same-worker-token", activeSame.ClaimToken);
        Assert.NotNull(activeOther);
        Assert.Equal("worker-b", activeOther.ClaimOwner);
        Assert.Equal("other-worker-token", activeOther.ClaimToken);
    }

    /// <summary>
    /// Verifies that incomplete legacy claim metadata is treated as available and replaced by a complete lease for the requesting worker.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ClaimRetryReadyReplacesIncompleteClaimMetadata()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        DateTimeOffset utcNow = new(2026, 7, 10, 18, 0, 0, TimeSpan.Zero);

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);
        await SaveEntriesAsync(
            store,
            CreateRetryEntry(
                "missing-token",
                GovernanceEmissionStatus.Failed,
                utcNow,
                claimOwner: "legacy-worker",
                claimToken: null,
                claimedUtc: utcNow.AddMinutes(-1),
                claimExpiresUtc: utcNow.AddMinutes(5)),
            CreateRetryEntry(
                "missing-expiration",
                GovernanceEmissionStatus.Deferred,
                utcNow,
                claimOwner: "legacy-worker",
                claimToken: "legacy-token",
                claimedUtc: utcNow.AddMinutes(-1),
                claimExpiresUtc: null));
        context.ChangeTracker.Clear();

        var request = GovernanceOutboxClaimRequest.Create(
            "replacement-worker",
            utcNow,
            TimeSpan.FromMinutes(4),
            maxCount: 10);
        IReadOnlyList<GovernanceOutboxClaim> claims = await store.ClaimRetryReadyAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(2, claims.Count);
        Assert.Equal(
            ["missing-expiration", "missing-token"],
            claims.Select(claim => claim.OutboxEntryId).Order(StringComparer.Ordinal));
        Assert.All(claims, claim =>
        {
            Assert.Equal("replacement-worker", claim.WorkerId);
            Assert.Equal("replacement-worker", claim.Entry.ClaimOwner);
            Assert.False(string.IsNullOrWhiteSpace(claim.Entry.ClaimToken));
            Assert.Equal(utcNow, claim.Entry.ClaimedUtc);
            Assert.Equal(utcNow.AddMinutes(4), claim.Entry.ClaimExpiresUtc);
        });
    }

    /// <summary>
    /// Verifies null-request and pre-canceled-operation guards on retry-ready claiming.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ClaimRetryReadyRejectsNullRequestAndCancellation()
    {
        DbContextOptions<HostOwnedGovernanceDbContext> options = new DbContextOptionsBuilder<HostOwnedGovernanceDbContext>()
            .UseInMemoryDatabase($"retry-claim-guards-{Guid.NewGuid():N}")
            .Options;
        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);

        _ = await Assert.ThrowsAsync<ArgumentNullException>(() =>
            store.ClaimRetryReadyAsync(null!, TestContext.Current.CancellationToken).AsTask());

        using var source = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        source.Cancel();
        var request = GovernanceOutboxClaimRequest.Create("worker-cancelled");

        _ = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            store.ClaimRetryReadyAsync(request, source.Token).AsTask());
    }

    private static bool IsRetryReadyClaimEligible(
        AsiBackboneGovernanceOutboxEntryEntity entity,
        DateTimeOffset utcNow)
    {
        object? result = RetryReadyEligibilityMethod.Invoke(null, [entity, utcNow]);
        return Assert.IsType<bool>(result);
    }

    private static AsiBackboneGovernanceOutboxEntryEntity CreateEligibilityEntity(
        GovernanceEmissionStatus status = GovernanceEmissionStatus.RetryableFailure,
        int retryCount = 1,
        int maxRetryCount = 5,
        DateTimeOffset? nextRetryUtc = null,
        string? claimOwner = null,
        string? claimToken = null,
        DateTimeOffset? claimedUtc = null,
        DateTimeOffset? claimExpiresUtc = null)
    {
        return new AsiBackboneGovernanceOutboxEntryEntity
        {
            Status = status,
            RetryCount = retryCount,
            MaxRetryCount = maxRetryCount,
            NextRetryUtc = nextRetryUtc,
            ClaimOwner = claimOwner,
            ClaimToken = claimToken,
            ClaimedUtc = claimedUtc,
            ClaimExpiresUtc = claimExpiresUtc
        };
    }

    private static GovernanceOutboxEntry CreateRetryEntry(
        string outboxEntryId,
        GovernanceEmissionStatus status,
        DateTimeOffset utcNow,
        int retryCount = 1,
        int maxRetryCount = 5,
        DateTimeOffset? nextRetryUtc = null,
        string? claimOwner = null,
        string? claimToken = null,
        DateTimeOffset? claimedUtc = null,
        DateTimeOffset? claimExpiresUtc = null,
        int claimAttemptCount = 0)
    {
        return GovernanceOutboxEntry.Restore(
            CreateEnvelope(outboxEntryId, utcNow),
            status,
            outboxEntryId,
            utcNow.AddHours(-1),
            utcNow.AddMinutes(-1),
            retryCount,
            maxRetryCount,
            nextRetryUtc,
            claimOwner: claimOwner,
            claimToken: claimToken,
            claimedUtc: claimedUtc,
            claimExpiresUtc: claimExpiresUtc,
            claimAttemptCount: claimAttemptCount);
    }

    private static async Task SaveEntriesAsync(
        EfCoreGovernanceOutboxStore store,
        params GovernanceOutboxEntry[] entries)
    {
        foreach (GovernanceOutboxEntry entry in entries)
        {
            _ = await store.SaveAsync(entry, TestContext.Current.CancellationToken);
        }
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static DbContextOptions<HostOwnedGovernanceDbContext> CreateOptions(SqliteConnection connection)
    {
        return new DbContextOptionsBuilder<HostOwnedGovernanceDbContext>()
            .UseSqlite(connection)
            .Options;
    }

    private static async Task EnsureCreatedAsync(DbContextOptions<HostOwnedGovernanceDbContext> options)
    {
        await using HostOwnedGovernanceDbContext context = new(options);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(string outboxEntryId, DateTimeOffset utcNow)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditLifecycle,
            eventId: $"event-{outboxEntryId}",
            occurredUtc: utcNow.AddHours(-1),
            envelopeId: $"envelope-{outboxEntryId}",
            createdUtc: utcNow.AddHours(-1).AddSeconds(1),
            schemaVersion: "1.0.0",
            correlationId: "efcore-retry-claim-eligibility",
            auditResidueId: $"audit-{outboxEntryId}",
            lifecycleStage: AuditResidueLifecycleStage.ExternalEmissionQueued,
            policyVersion: "2026.07",
            policyHash: "policy-hash-retry-claim",
            traceId: $"trace-{outboxEntryId}",
            operationName: "governance.retry.claim",
            outcome: "RetryReady",
            emitterStatus: "retry-ready",
            emitterProvider: "efcore-outbox",
            decisionStage: "ExternalEmissionRetryReady");
    }

    private sealed class HostOwnedGovernanceDbContext(DbContextOptions<HostOwnedGovernanceDbContext> options)
        : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
