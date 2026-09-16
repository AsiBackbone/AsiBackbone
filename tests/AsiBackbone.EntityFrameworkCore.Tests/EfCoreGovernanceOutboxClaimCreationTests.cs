using System.Reflection;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Outbox;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests;

/// <summary>
/// Focused coverage for EF Core outbox claim creation and claim metadata validation.
/// </summary>
public sealed class EfCoreGovernanceOutboxClaimCreationTests
{
    private static readonly MethodInfo CreateClaimMethod = typeof(EfCoreGovernanceOutboxStore)
        .GetMethod("CreateClaim", BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("The EF Core claim creation method could not be located.");

    /// <summary>
    /// Verifies the public claim path creates a stable claim snapshot with normalized ownership and UTC timestamps.
    /// </summary>
    [Fact]
    public async Task ClaimPendingCreatesClaimWithStableIdentityOwnershipAndTimestamps()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);
        DateTimeOffset claimedUtc = new(2026, 7, 11, 7, 0, 0, TimeSpan.Zero);
        DateTimeOffset offsetClaimedUtc = claimedUtc.ToOffset(TimeSpan.FromHours(-5));
        var entry = GovernanceOutboxEntry.Create(
            CreateEnvelope("claim-create-success"),
            "claim-create-entry",
            claimedUtc.AddMinutes(-1));
        _ = await store.SaveAsync(entry, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        GovernanceOutboxClaim claim = Assert.Single(await store.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create(
                "  ef-worker  ",
                offsetClaimedUtc,
                TimeSpan.FromMinutes(3),
                maxCount: 1),
            TestContext.Current.CancellationToken));

        Assert.Equal("claim-create-entry", claim.OutboxEntryId);
        Assert.Equal("ef-worker", claim.WorkerId);
        Assert.False(string.IsNullOrWhiteSpace(claim.ClaimToken));
        Assert.Equal(claim.ClaimToken, claim.Entry.ClaimToken);
        Assert.Equal(claim.WorkerId, claim.Entry.ClaimOwner);
        Assert.Equal(claimedUtc, claim.ClaimedUtc);
        Assert.Equal(claimedUtc.AddMinutes(3), claim.ClaimExpiresUtc);
        Assert.Equal(TimeSpan.Zero, claim.ClaimedUtc.Offset);
        Assert.Equal(TimeSpan.Zero, claim.ClaimExpiresUtc.Offset);
        Assert.Equal(1, claim.Entry.ClaimAttemptCount);
    }

    /// <summary>
    /// Verifies claim creation rejects a claimed entry that has no owner.
    /// </summary>
    [Fact]
    public void CreateClaimRejectsMissingOwner()
    {
        GovernanceOutboxEntry entry = CreateClaimEntry(claimOwner: null);

        AssertCreateClaimFails(entry, "claim owner");
    }

    /// <summary>
    /// Verifies claim creation rejects a claimed entry that has no token.
    /// </summary>
    [Fact]
    public void CreateClaimRejectsMissingToken()
    {
        GovernanceOutboxEntry entry = CreateClaimEntry(claimToken: null);

        AssertCreateClaimFails(entry, "claim token");
    }

    /// <summary>
    /// Verifies claim creation rejects a claimed entry that has no acquisition timestamp.
    /// </summary>
    [Fact]
    public void CreateClaimRejectsMissingClaimedTimestamp()
    {
        GovernanceOutboxEntry entry = CreateClaimEntry(includeClaimedUtc: false);

        AssertCreateClaimFails(entry, "claimed timestamp");
    }

    /// <summary>
    /// Verifies claim creation rejects a claimed entry that has no expiration timestamp.
    /// </summary>
    [Fact]
    public void CreateClaimRejectsMissingExpirationTimestamp()
    {
        GovernanceOutboxEntry entry = CreateClaimEntry(includeClaimExpiresUtc: false);

        AssertCreateClaimFails(entry, "claim expiration timestamp");
    }

    private static void AssertCreateClaimFails(GovernanceOutboxEntry entry, string expectedMessage)
    {
        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(() => CreateClaimMethod.Invoke(null, [entry]));
        InvalidOperationException innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains(expectedMessage, innerException.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static GovernanceOutboxEntry CreateClaimEntry(
        string? claimOwner = "worker-a",
        string? claimToken = "claim-token",
        bool includeClaimedUtc = true,
        bool includeClaimExpiresUtc = true)
    {
        DateTimeOffset timestamp = new(2026, 7, 11, 8, 0, 0, TimeSpan.Zero);

        return GovernanceOutboxEntry.Restore(
            CreateEnvelope("claim-create-invalid"),
            GovernanceEmissionStatus.Pending,
            "claim-create-invalid-entry",
            timestamp.AddMinutes(-1),
            timestamp,
            claimOwner: claimOwner,
            claimToken: claimToken,
            claimedUtc: includeClaimedUtc ? timestamp : null,
            claimExpiresUtc: includeClaimExpiresUtc ? timestamp.AddMinutes(5) : null,
            claimAttemptCount: 1);
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

    private static GovernanceEmissionEnvelope CreateEnvelope(string eventId)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.Outbox,
            eventId,
            new DateTimeOffset(2026, 7, 11, 6, 0, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{eventId}",
            correlationId: "efcore-claim-creation-coverage",
            emitterStatus: GovernanceEmissionStatus.Pending.ToString(),
            emitterProvider: "efcore-outbox");
    }

    private sealed class HostOwnedGovernanceDbContext(DbContextOptions<HostOwnedGovernanceDbContext> options)
        : DbContext(options)
    {
        public DbSet<GovernanceOutboxEntryEntity> GovernanceOutboxEntries =>
            Set<GovernanceOutboxEntryEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
