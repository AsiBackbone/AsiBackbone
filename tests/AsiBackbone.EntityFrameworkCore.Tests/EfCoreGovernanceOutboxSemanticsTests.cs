using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Outbox;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests;

/// <summary>
/// Integration tests that pin the documented EF Core governance outbox identity and idempotency semantics.
/// </summary>
public sealed class EfCoreGovernanceOutboxSemanticsTests
{
    /// <summary>
    /// Verifies that the EF Core model treats the stable outbox entry identifier as a unique idempotency boundary.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task EfCoreModelUsesUniqueOutboxEntryIdForIdempotencyBoundary()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        await using HostOwnedGovernanceDbContext context = new(options);
        IEntityType? entityType = context.Model.FindEntityType(typeof(AsiBackboneGovernanceOutboxEntryEntity));

        Assert.NotNull(entityType);

        IIndex outboxEntryIdIndex = Assert.Single(entityType.GetIndexes(), index =>
            index.Properties.Count == 1 &&
            index.Properties[0].Name == nameof(AsiBackboneGovernanceOutboxEntryEntity.OutboxEntryId));

        Assert.True(outboxEntryIdIndex.IsUnique);
        Assert.Equal(6, entityType.GetIndexes().Count());
        AssertHasIndex(entityType, nameof(AsiBackboneGovernanceOutboxEntryEntity.ClaimToken));
        AssertHasIndex(
            entityType,
            nameof(AsiBackboneGovernanceOutboxEntryEntity.Status),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.CreatedUtc),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.OutboxEntryId));
        AssertHasIndex(
            entityType,
            nameof(AsiBackboneGovernanceOutboxEntryEntity.Status),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.NextRetryUtc),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.UpdatedUtc),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.OutboxEntryId));
        AssertHasIndex(
            entityType,
            nameof(AsiBackboneGovernanceOutboxEntryEntity.Status),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.ClaimExpiresUtc),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.CreatedUtc),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.OutboxEntryId));
        AssertHasIndex(
            entityType,
            nameof(AsiBackboneGovernanceOutboxEntryEntity.Status),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.ClaimExpiresUtc),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.NextRetryUtc),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.UpdatedUtc),
            nameof(AsiBackboneGovernanceOutboxEntryEntity.OutboxEntryId));

        Assert.Equal(4096, entityType.FindProperty(nameof(AsiBackboneGovernanceOutboxEntryEntity.DeadLetterReason))?.GetMaxLength());
        Assert.Equal(4096, entityType.FindProperty(nameof(AsiBackboneGovernanceOutboxEntryEntity.LastErrorMessage))?.GetMaxLength());
        Assert.Equal(65536, entityType.FindProperty(nameof(AsiBackboneGovernanceOutboxEntryEntity.MetadataJson))?.GetMaxLength());
        Assert.Equal(65536, entityType.FindProperty(nameof(AsiBackboneGovernanceOutboxEntryEntity.EnvelopeMetadataJson))?.GetMaxLength());
        Assert.Equal(65536, entityType.FindProperty(nameof(AsiBackboneGovernanceOutboxEntryEntity.EnvelopePayloadMetadataJson))?.GetMaxLength());
    }

    /// <summary>
    /// Verifies that saving a second state for the same outbox entry identifier updates the row instead of appending a duplicate.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task SaveAsyncUpdatesExistingOutboxEntryByStableIdentifier()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        const string outboxEntryId = "outbox-idempotency-001";
        DateTimeOffset createdUtc = new(2026, 7, 7, 9, 0, 0, TimeSpan.Zero);

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);

        var pending = GovernanceOutboxEntry.Create(
            CreateEnvelope("event-idempotency-pending"),
            outboxEntryId,
            createdUtc);

        _ = await store.SaveAsync(pending, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        GovernanceOutboxEntry delivered = pending.MarkDelivered(
            GovernanceEmissionResult.Delivered("test-provider", "provider-record-001"),
            createdUtc.AddMinutes(1));

        _ = await store.SaveAsync(delivered, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        int persistedRowCount = await context.GovernanceOutboxEntries.CountAsync(TestContext.Current.CancellationToken);
        GovernanceOutboxEntry? persisted = await store.FindByOutboxEntryIdAsync(outboxEntryId, TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> pendingEntries = await store.FindPendingAsync(cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, persistedRowCount);
        Assert.NotNull(persisted);
        Assert.Equal(outboxEntryId, persisted.OutboxEntryId);
        Assert.Equal(GovernanceEmissionStatus.Delivered, persisted.Status);
        Assert.Equal("test-provider", persisted.ProviderName);
        Assert.Equal("provider-record-001", persisted.ProviderRecordId);
        Assert.DoesNotContain(pendingEntries, entry => entry.OutboxEntryId == outboxEntryId);
    }

    private static async Task<SqliteConnection> OpenConnectionAsync()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return connection;
    }

    private static void AssertHasIndex(IEntityType entityType, params string[] propertyNames)
    {
        Assert.Contains(
            entityType.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(propertyNames));
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
            GovernanceEmissionEventType.AuditLifecycle,
            eventId,
            new DateTimeOffset(2026, 7, 7, 8, 0, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{eventId}",
            createdUtc: new DateTimeOffset(2026, 7, 7, 8, 0, 1, TimeSpan.Zero),
            schemaVersion: "1.0.0",
            correlationId: "outbox-semantics-validation",
            auditResidueId: "audit-outbox-semantics",
            lifecycleStage: AuditResidueLifecycleStage.ExternalEmissionQueued,
            policyVersion: "2026.07",
            policyHash: "policy-hash-semantics",
            traceId: "trace-outbox-semantics",
            operationName: "governance.emit",
            outcome: "Queued",
            emitterStatus: "queued",
            emitterProvider: "efcore-outbox",
            decisionStage: "ExternalEmissionQueued",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["validation"] = "efcore-outbox-semantics"
            });
    }

    private sealed class HostOwnedGovernanceDbContext(DbContextOptions<HostOwnedGovernanceDbContext> options)
        : DbContext(options)
    {
        public DbSet<AsiBackboneGovernanceOutboxEntryEntity> GovernanceOutboxEntries =>
            Set<AsiBackboneGovernanceOutboxEntryEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
