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
/// Integration tests for string-backed JSON metadata persistence in the EF Core governance outbox store.
/// </summary>
public sealed class EfCoreGovernanceOutboxJsonMetadataTests
{
    /// <summary>
    /// Verifies entry, envelope, and payload metadata round-trip through the string-backed JSON columns.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task OutboxMetadataJsonColumnsRoundTripEntryEnvelopeAndPayloadMetadata()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);

        var payload = GovernanceEmissionPayload.Create(
            "audit-residue",
            schemaVersion: "1.0.0",
            contentType: "application/json",
            contentHash: "payload-hash-json",
            sizeBytes: 256,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["payload.key"] = "payload-value",
                ["payload.future"] = "payload-future-value"
            });

        GovernanceEmissionEnvelope envelope = CreateEnvelope(
            "event-json-metadata",
            payload,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["envelope.key"] = "envelope-value",
                ["envelope.future"] = "envelope-future-value"
            });

        var entry = GovernanceOutboxEntry.Create(
            envelope,
            "outbox-json-metadata",
            new DateTimeOffset(2026, 7, 7, 10, 0, 0, TimeSpan.Zero),
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["entry.key"] = "entry-value",
                ["entry.future"] = "entry-future-value"
            });

        _ = await store.SaveAsync(entry, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        GovernanceOutboxEntry? persisted = await store.FindByOutboxEntryIdAsync(
            entry.OutboxEntryId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal("entry-value", persisted.Metadata["entry.key"]);
        Assert.Equal("entry-future-value", persisted.Metadata["entry.future"]);
        Assert.Equal("envelope-value", persisted.Envelope.Metadata["envelope.key"]);
        Assert.Equal("envelope-future-value", persisted.Envelope.Metadata["envelope.future"]);
        Assert.NotNull(persisted.Envelope.Payload);
        Assert.Equal("payload-value", persisted.Envelope.Payload.Metadata["payload.key"]);
        Assert.Equal("payload-future-value", persisted.Envelope.Payload.Metadata["payload.future"]);
    }

    /// <summary>
    /// Verifies missing metadata inputs round-trip as empty dictionaries for entries, envelopes, and payloads.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task OutboxMetadataJsonColumnsRoundTripMissingMetadataAsEmptyDictionaries()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);

        var payload = GovernanceEmissionPayload.Create("audit-residue");
        GovernanceEmissionEnvelope envelope = CreateEnvelope("event-empty-metadata", payload, metadata: null);
        var entry = GovernanceOutboxEntry.Create(
            envelope,
            "outbox-empty-metadata",
            new DateTimeOffset(2026, 7, 7, 10, 5, 0, TimeSpan.Zero),
            metadata: null);

        _ = await store.SaveAsync(entry, TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        GovernanceOutboxEntry? persisted = await store.FindByOutboxEntryIdAsync(
            entry.OutboxEntryId,
            TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);
        Assert.Empty(persisted.Metadata);
        Assert.Empty(persisted.Envelope.Metadata);
        Assert.NotNull(persisted.Envelope.Payload);
        Assert.Empty(persisted.Envelope.Payload.Metadata);
    }

    /// <summary>
    /// Verifies manually persisted string JSON metadata keeps unknown future keys as dictionary entries.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task OutboxManualStringJsonMetadataPreservesUnknownFutureKeys()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);
        var timestamp = new DateTimeOffset(2026, 7, 7, 10, 10, 0, TimeSpan.Zero);

        _ = context.GovernanceOutboxEntries.Add(new GovernanceOutboxEntryEntity
        {
            OutboxEntryId = "outbox-future-metadata",
            Status = GovernanceEmissionStatus.Pending,
            CreatedUtc = timestamp,
            UpdatedUtc = timestamp,
            RetryCount = 0,
            MaxRetryCount = 5,
            MetadataJson = /*lang=json,strict*/ "{\"entry.future.key\":\"entry-future-value\",\"entry.source\":\"manual-json\"}",
            EnvelopeId = "envelope-future-metadata",
            EnvelopeSchemaVersion = "1.0.0",
            EnvelopeEventType = GovernanceEmissionEventType.AuditLifecycle,
            EnvelopeEventId = "event-future-metadata",
            EnvelopeOccurredUtc = timestamp,
            EnvelopeCreatedUtc = timestamp,
            EnvelopeCorrelationId = "correlation-future-metadata",
            EnvelopeDecisionReceiptId = "audit-future-metadata",
            EnvelopeLifecycleStage = DecisionReceiptLifecycleStage.ExternalEmissionQueued,
            EnvelopeLifecycleStageSequence = (int)DecisionReceiptLifecycleStage.ExternalEmissionQueued,
            EnvelopeMetadataJson = /*lang=json,strict*/ "{\"envelope.future.key\":\"envelope-future-value\",\"envelope.source\":\"manual-json\"}",
            EnvelopePayloadType = "audit-residue",
            EnvelopePayloadSchemaVersion = "1.0.0",
            EnvelopePayloadContentType = "application/json",
            EnvelopePayloadContentHash = "future-payload-hash",
            EnvelopePayloadSizeBytes = 128,
            EnvelopePayloadMetadataJson = /*lang=json,strict*/ "{\"payload.future.key\":\"payload-future-value\",\"payload.source\":\"manual-json\"}"
        });
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        GovernanceOutboxEntry? persisted = await store.FindByOutboxEntryIdAsync(
            "outbox-future-metadata",
            TestContext.Current.CancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal("entry-future-value", persisted.Metadata["entry.future.key"]);
        Assert.Equal("manual-json", persisted.Metadata["entry.source"]);
        Assert.Equal("envelope-future-value", persisted.Envelope.Metadata["envelope.future.key"]);
        Assert.Equal("manual-json", persisted.Envelope.Metadata["envelope.source"]);
        Assert.NotNull(persisted.Envelope.Payload);
        Assert.Equal("payload-future-value", persisted.Envelope.Payload.Metadata["payload.future.key"]);
        Assert.Equal("manual-json", persisted.Envelope.Payload.Metadata["payload.source"]);
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

    private static GovernanceEmissionEnvelope CreateEnvelope(
        string eventId,
        GovernanceEmissionPayload payload,
        IReadOnlyDictionary<string, string>? metadata)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditLifecycle,
            eventId,
            new DateTimeOffset(2026, 7, 7, 9, 0, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{eventId}",
            createdUtc: new DateTimeOffset(2026, 7, 7, 9, 0, 1, TimeSpan.Zero),
            schemaVersion: "1.0.0",
            correlationId: $"correlation-{eventId}",
            decisionReceiptId: $"audit-{eventId}",
            lifecycleStage: DecisionReceiptLifecycleStage.ExternalEmissionQueued,
            policyVersion: "2026.07",
            policyHash: "policy-hash-json-metadata",
            traceId: "trace-json-metadata",
            operationName: "governance.emit",
            outcome: "Queued",
            emitterStatus: "queued",
            emitterProvider: "efcore-outbox",
            decisionStage: "ExternalEmissionQueued",
            payload: payload,
            metadata: metadata);
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
