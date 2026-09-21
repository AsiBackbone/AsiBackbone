using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Outbox;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests.Outbox;

/// <summary>
/// Relational coverage for claim transitions performed through the same host-owned <see cref="DbContext" /> that
/// enqueued the entries.
/// </summary>
/// <remarks>
/// The claim path updates rows with a set-based statement that bypasses the change tracker. An entity already
/// tracked by the same context would otherwise keep its pre-claim values, and a later tracking query returns that
/// tracked instance instead of the claimed row, so the claim appears not to be held.
/// </remarks>
public sealed class EfCoreGovernanceOutboxSharedContextTrackingTests
{
    private static readonly DateTimeOffset CreatedUtc = new(2026, 9, 21, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that an entry enqueued and claimed through one context can be marked delivered through that context.
    /// </summary>
    [Fact]
    public async Task ClaimedEntryEnqueuedThroughSameContextCanBeMarkedDelivered()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using SharedContextDbContext context = await CreateContextAsync(connection, cancellationToken);
        var store = new EfCoreGovernanceOutboxStore(context);

        _ = await store.EnqueueAsync(CreateEnvelope("shared-context-delivered"), cancellationToken);

        GovernanceOutboxClaim claim = Assert.Single(await store.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create("shared-context-worker", CreatedUtc.AddMinutes(1), TimeSpan.FromMinutes(5)),
            cancellationToken));

        GovernanceOutboxEntry delivered = await store.MarkClaimDeliveredAsync(
            claim,
            GovernanceEmissionResult.Delivered("shared-context-provider"),
            cancellationToken);

        Assert.Equal(GovernanceEmissionStatus.Delivered, delivered.Status);

        GovernanceOutboxEntry? persisted = await store.FindByOutboxEntryIdAsync(claim.OutboxEntryId, cancellationToken);
        Assert.NotNull(persisted);
        Assert.Equal(GovernanceEmissionStatus.Delivered, persisted.Status);
    }

    /// <summary>
    /// Verifies that a claim taken through the same context that enqueued the entry can be released.
    /// </summary>
    [Fact]
    public async Task ClaimedEntryEnqueuedThroughSameContextCanBeReleased()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using SharedContextDbContext context = await CreateContextAsync(connection, cancellationToken);
        var store = new EfCoreGovernanceOutboxStore(context);

        _ = await store.EnqueueAsync(CreateEnvelope("shared-context-released"), cancellationToken);

        GovernanceOutboxClaim claim = Assert.Single(await store.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create("shared-context-worker", CreatedUtc.AddMinutes(1), TimeSpan.FromMinutes(5)),
            cancellationToken));

        GovernanceOutboxEntry? released = await store.ReleaseClaimAsync(claim, cancellationToken: cancellationToken);

        Assert.NotNull(released);
        Assert.False(released.IsClaimedBy(claim));

        GovernanceOutboxEntry? persisted = await store.FindByOutboxEntryIdAsync(claim.OutboxEntryId, cancellationToken);
        Assert.NotNull(persisted);
        Assert.False(persisted.IsClaimedBy(claim));
    }

    /// <summary>
    /// Verifies that entities tracked by the context reflect the claim written by the set-based update.
    /// </summary>
    [Fact]
    public async Task TrackedEntityDoesNotRetainPreClaimValuesAfterClaim()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using SharedContextDbContext context = await CreateContextAsync(connection, cancellationToken);
        var store = new EfCoreGovernanceOutboxStore(context);

        _ = await store.EnqueueAsync(CreateEnvelope("shared-context-tracked"), cancellationToken);

        GovernanceOutboxClaim claim = Assert.Single(await store.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create("shared-context-worker", CreatedUtc.AddMinutes(1), TimeSpan.FromMinutes(5)),
            cancellationToken));

        GovernanceOutboxEntryEntity tracked = await context.GovernanceOutboxEntries
            .SingleAsync(entity => entity.OutboxEntryId == claim.OutboxEntryId, cancellationToken);

        Assert.Equal(claim.ClaimToken, tracked.ClaimToken);
        Assert.Equal("shared-context-worker", tracked.ClaimOwner);
    }

    /// <summary>
    /// Verifies that unsaved host changes on a tracked row survive a claim, that the tracked row reflects the claim,
    /// and that a later host save persists both.
    /// </summary>
    [Fact]
    public async Task ClaimPreservesUnsavedHostChangesOnTrackedRow()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using SharedContextDbContext context = await CreateContextAsync(connection, cancellationToken);
        var store = new EfCoreGovernanceOutboxStore(context);

        GovernanceOutboxEntry enqueued = await store.EnqueueAsync(CreateEnvelope("shared-context-dirty"), cancellationToken);
        GovernanceOutboxEntryEntity tracked = context.GovernanceOutboxEntries.Local
            .Single(entity => entity.OutboxEntryId == enqueued.OutboxEntryId);
        tracked.EnvelopeOperationName = "host-unsaved-change";

        GovernanceOutboxClaim claim = Assert.Single(await store.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create("shared-context-worker", CreatedUtc.AddMinutes(1), TimeSpan.FromMinutes(5)),
            cancellationToken));

        Assert.Equal(EntityState.Modified, context.Entry(tracked).State);
        Assert.Equal("host-unsaved-change", tracked.EnvelopeOperationName);
        Assert.Equal(claim.ClaimToken, tracked.ClaimToken);

        _ = await context.SaveChangesAsync(cancellationToken);

        GovernanceOutboxEntryEntity persisted = await context.GovernanceOutboxEntries.AsNoTracking()
            .SingleAsync(entity => entity.OutboxEntryId == claim.OutboxEntryId, cancellationToken);
        Assert.Equal("host-unsaved-change", persisted.EnvelopeOperationName);
        Assert.Equal(claim.ClaimToken, persisted.ClaimToken);
        Assert.Equal("shared-context-worker", persisted.ClaimOwner);
    }

    /// <summary>
    /// Verifies that a tracked row the host has marked for deletion stays marked after a claim and can still be deleted.
    /// </summary>
    [Fact]
    public async Task ClaimPreservesPendingHostDeletionOnTrackedRow()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using SharedContextDbContext context = await CreateContextAsync(connection, cancellationToken);
        var store = new EfCoreGovernanceOutboxStore(context);

        GovernanceOutboxEntry enqueued = await store.EnqueueAsync(CreateEnvelope("shared-context-deleted"), cancellationToken);
        GovernanceOutboxEntryEntity tracked = context.GovernanceOutboxEntries.Local
            .Single(entity => entity.OutboxEntryId == enqueued.OutboxEntryId);
        _ = context.GovernanceOutboxEntries.Remove(tracked);

        _ = Assert.Single(await store.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create("shared-context-worker", CreatedUtc.AddMinutes(1), TimeSpan.FromMinutes(5)),
            cancellationToken));

        Assert.Equal(EntityState.Deleted, context.Entry(tracked).State);

        _ = await context.SaveChangesAsync(cancellationToken);

        Assert.False(await context.GovernanceOutboxEntries.AsNoTracking()
            .AnyAsync(entity => entity.OutboxEntryId == enqueued.OutboxEntryId, cancellationToken));
    }

    /// <summary>
    /// Verifies that an unsaved terminal status on a tracked row does not short-circuit the claim transition. The claim
    /// was taken against the persisted status, so the persisted status governs the claimed row.
    /// </summary>
    /// <param name="hostStatus">The terminal status the host set without saving.</param>
    [Theory]
    [InlineData(GovernanceEmissionStatus.Delivered)]
    [InlineData(GovernanceEmissionStatus.DeadLettered)]
    public async Task UnsavedTerminalStatusDoesNotBypassClaimTransition(GovernanceEmissionStatus hostStatus)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync(cancellationToken);
        await using SharedContextDbContext context = await CreateContextAsync(connection, cancellationToken);
        var store = new EfCoreGovernanceOutboxStore(context);

        GovernanceOutboxEntry enqueued = await store.EnqueueAsync(CreateEnvelope($"shared-context-{hostStatus}"), cancellationToken);
        GovernanceOutboxEntryEntity tracked = context.GovernanceOutboxEntries.Local
            .Single(entity => entity.OutboxEntryId == enqueued.OutboxEntryId);
        tracked.Status = hostStatus;

        GovernanceOutboxClaim claim = Assert.Single(await store.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create("shared-context-worker", CreatedUtc.AddMinutes(1), TimeSpan.FromMinutes(5)),
            cancellationToken));

        GovernanceOutboxEntry delivered = await store.MarkClaimDeliveredAsync(
            claim,
            GovernanceEmissionResult.Delivered("shared-context-provider"),
            cancellationToken);

        Assert.Equal(GovernanceEmissionStatus.Delivered, delivered.Status);

        GovernanceOutboxEntryEntity persisted = await context.GovernanceOutboxEntries.AsNoTracking()
            .SingleAsync(entity => entity.OutboxEntryId == claim.OutboxEntryId, cancellationToken);
        Assert.Equal(GovernanceEmissionStatus.Delivered, persisted.Status);
        Assert.Equal("shared-context-provider", persisted.ProviderName);
    }


    private static async Task<SharedContextDbContext> CreateContextAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        DbContextOptions<SharedContextDbContext> options = new DbContextOptionsBuilder<SharedContextDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new SharedContextDbContext(options);
        _ = await context.Database.EnsureCreatedAsync(cancellationToken);
        return context;
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(string suffix)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.Outbox,
            $"event-{suffix}",
            CreatedUtc,
            envelopeId: $"envelope-{suffix}",
            createdUtc: CreatedUtc,
            correlationId: "efcore-outbox-shared-context",
            emitterStatus: GovernanceEmissionStatus.Pending.ToString(),
            emitterProvider: "efcore-outbox");
    }

    private sealed class SharedContextDbContext(DbContextOptions<SharedContextDbContext> options)
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
