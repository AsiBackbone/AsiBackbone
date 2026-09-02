using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Audit;
using AsiBackbone.EntityFrameworkCore.Outbox;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests;

/// <summary>
/// Integration tests for durable governance outbox and audit residue lifecycle persistence through host-owned EF Core contexts.
/// </summary>
public sealed class EfCoreGovernanceOutboxPersistenceTests
{
    /// <summary>
    /// Verifies that an outbox entry survives a host-owned context restart when backed by a relational provider.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task OutboxEntrySurvivesRelationalContextRestartAndQueriesPending()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        string outboxEntryId;

        await using (HostOwnedGovernanceDbContext context = new(options))
        {
            var store = new EfCoreGovernanceOutboxStore(context);
            GovernanceEmissionEnvelope envelope = CreateEnvelope("event-pending", "correlation-123", "audit-123");

            GovernanceOutboxEntry entry = await store.EnqueueAsync(envelope, TestContext.Current.CancellationToken);
            outboxEntryId = entry.OutboxEntryId;
        }

        await using (HostOwnedGovernanceDbContext context = new(options))
        {
            var store = new EfCoreGovernanceOutboxStore(context);

            GovernanceOutboxEntry? found = await store.FindByOutboxEntryIdAsync(outboxEntryId, TestContext.Current.CancellationToken);
            IReadOnlyList<GovernanceOutboxEntry> pending = await store.FindPendingAsync(cancellationToken: TestContext.Current.CancellationToken);

            Assert.NotNull(found);
            Assert.Equal(GovernanceEmissionStatus.Pending, found.Status);
            Assert.Equal("event-pending", found.Envelope.EventId);
            Assert.Equal("correlation-123", found.Envelope.CorrelationId);
            Assert.Equal("audit-123", found.Envelope.AuditResidueId);
            Assert.Equal("2026.06", found.Envelope.PolicyVersion);
            Assert.Equal("policy-hash-123", found.Envelope.PolicyHash);
            Assert.Equal("trace-123", found.Envelope.TraceId);
            Assert.Equal(AuditResidueLifecycleStage.ExternalEmissionQueued, found.Envelope.LifecycleStage);
            Assert.NotNull(found.Envelope.Payload);
            Assert.Equal("audit-residue", found.Envelope.Payload.PayloadType);
            Assert.Equal("payload-hash", found.Envelope.Payload.ContentHash);
            Assert.Equal("envelope", found.Envelope.Metadata["source"]);
            Assert.Equal(outboxEntryId, Assert.Single(pending).OutboxEntryId);
        }
    }

    /// <summary>
    /// Verifies pending outbox selection orders and limits before materializing entries from the EF Core store.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task FindPendingAsyncOrdersByCreatedTimestampThenOutboxIdAndAppliesMaxCount()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        DateTimeOffset createdUtc = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);

        GovernanceOutboxEntry delivered = GovernanceOutboxEntry
            .Create(CreateEnvelope("event-delivered-filtered", "correlation-delivered-filtered", "audit-delivered-filtered"), "delivered-filtered", createdUtc.AddMinutes(-5))
            .MarkDelivered(GovernanceEmissionResult.Delivered("test-provider", "provider-record-filtered"), createdUtc.AddMinutes(-4));

        _ = await store.SaveAsync(delivered, TestContext.Current.CancellationToken);
        _ = await store.SaveAsync(CreateOutboxEntry("pending-c", "event-pending-c", createdUtc), TestContext.Current.CancellationToken);
        _ = await store.SaveAsync(CreateOutboxEntry("pending-a", "event-pending-a", createdUtc), TestContext.Current.CancellationToken);
        _ = await store.SaveAsync(CreateOutboxEntry("pending-b", "event-pending-b", createdUtc), TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        IReadOnlyList<GovernanceOutboxEntry> pending = await store.FindPendingAsync(2, TestContext.Current.CancellationToken);

        Assert.Equal(["pending-a", "pending-b"], [.. pending.Select(entry => entry.OutboxEntryId)]);
    }

    /// <summary>
    /// Verifies retry-ready outbox selection filters, orders, and limits before materializing entries from the EF Core store.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task FindRetryReadyAsyncFiltersReadyRowsOrdersDeterministicallyAndAppliesMaxCount()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        DateTimeOffset utcNow = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset readyAt = utcNow.AddMinutes(-5);
        DateTimeOffset createdUtc = utcNow.AddHours(-1);
        DateTimeOffset updatedUtc = utcNow.AddMinutes(-10);
        var retryableError = GovernanceEmissionError.Create(
            "provider.retryable",
            "Provider requested another attempt.",
            isRetryable: true,
            providerName: "test-provider");
        var failedError = GovernanceEmissionError.Create(
            "provider.failed",
            "Provider failed without a retryable marker.",
            providerName: "test-provider");

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);

        _ = await store.SaveAsync(CreateOutboxEntry("retry-ready-b", "event-retry-ready-b", createdUtc)
            .MarkFailed(retryableError, readyAt, updatedUtc), TestContext.Current.CancellationToken);
        _ = await store.SaveAsync(CreateOutboxEntry("retry-ready-a", "event-retry-ready-a", createdUtc)
            .MarkDeferred(retryableError, readyAt, updatedUtc), TestContext.Current.CancellationToken);
        _ = await store.SaveAsync(CreateOutboxEntry("retry-ready-c", "event-retry-ready-c", createdUtc)
            .MarkFailed(failedError, utcNow.AddMinutes(-1), updatedUtc), TestContext.Current.CancellationToken);
        _ = await store.SaveAsync(CreateOutboxEntry("retry-future", "event-retry-future", createdUtc)
            .MarkFailed(retryableError, utcNow.AddMinutes(5), updatedUtc), TestContext.Current.CancellationToken);
        _ = await store.SaveAsync(GovernanceOutboxEntry.Restore(
            CreateEnvelope("event-retry-exhausted", "correlation-retry-exhausted", "audit-retry-exhausted"),
            GovernanceEmissionStatus.Failed,
            "retry-exhausted",
            createdUtc,
            updatedUtc,
            retryCount: 5,
            maxRetryCount: 5,
            nextRetryUtc: readyAt,
            lastError: failedError), TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        IReadOnlyList<GovernanceOutboxEntry> retryReady = await store.FindRetryReadyAsync(
            utcNow,
            10,
            TestContext.Current.CancellationToken);

        Assert.Equal(
            ["retry-exhausted", "retry-ready-a", "retry-ready-b", "retry-ready-c"],
            [.. retryReady.Select(entry => entry.OutboxEntryId)]);
    }

    /// <summary>
    /// Verifies delivered, failed, retry-ready, deferred, and dead-letter state transitions over EF Core persistence.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task OutboxTransitionsPersistAndRetryReadyQueriesUseStoredState()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        DateTimeOffset retryReadyUtc = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);

        GovernanceOutboxEntry delivered = await store.EnqueueAsync(
            CreateEnvelope("event-delivered", "correlation-delivered", "audit-delivered"),
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry failed = await store.EnqueueAsync(
            CreateEnvelope("event-failed", "correlation-failed", "audit-failed"),
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry retryable = await store.EnqueueAsync(
            CreateEnvelope("event-retryable", "correlation-retryable", "audit-retryable"),
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry deferred = await store.EnqueueAsync(
            CreateEnvelope("event-deferred", "correlation-deferred", "audit-deferred"),
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry deadLettered = await store.EnqueueAsync(
            CreateEnvelope("event-dead-letter", "correlation-dead-letter", "audit-dead-letter"),
            TestContext.Current.CancellationToken);

        GovernanceOutboxEntry deliveredResult = await store.MarkDeliveredAsync(
            delivered.OutboxEntryId,
            GovernanceEmissionResult.Delivered(
                "test-provider",
                "provider-record-123",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["delivery"] = "ok"
                }),
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry failedResult = await store.MarkFailedAsync(
            failed.OutboxEntryId,
            GovernanceEmissionError.Create("provider.failed", "Provider rejected the emission.", providerName: "test-provider"),
            cancellationToken: TestContext.Current.CancellationToken);
        GovernanceOutboxEntry retryableResult = await store.MarkFailedAsync(
            retryable.OutboxEntryId,
            GovernanceEmissionError.Create("provider.timeout", "Provider timed out.", isRetryable: true, providerName: "test-provider"),
            retryReadyUtc.AddMinutes(-5),
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry deferredResult = await store.SaveAsync(
            deferred.MarkDeferred(
                GovernanceEmissionError.Create("provider.deferred", "Provider asked the host to retry later.", isRetryable: true, providerName: "test-provider"),
                retryReadyUtc.AddMinutes(-1)),
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry deadLetteredResult = await store.MarkDeadLetteredAsync(
            deadLettered.OutboxEntryId,
            GovernanceEmissionError.Create("provider.dead", "Provider failed permanently.", providerName: "test-provider"),
            "terminal-provider-failure",
            TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        IReadOnlyList<GovernanceOutboxEntry> retryReady = await store.FindRetryReadyAsync(
            retryReadyUtc,
            cancellationToken: TestContext.Current.CancellationToken);
        GovernanceOutboxEntry? persistedDelivered = await store.FindByOutboxEntryIdAsync(delivered.OutboxEntryId, TestContext.Current.CancellationToken);
        GovernanceOutboxEntry? persistedDeadLettered = await store.FindByOutboxEntryIdAsync(deadLettered.OutboxEntryId, TestContext.Current.CancellationToken);

        Assert.Equal(GovernanceEmissionStatus.Delivered, deliveredResult.Status);
        Assert.Equal(GovernanceEmissionStatus.Failed, failedResult.Status);
        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, retryableResult.Status);
        Assert.Equal(GovernanceEmissionStatus.Deferred, deferredResult.Status);
        Assert.Equal(GovernanceEmissionStatus.DeadLettered, deadLetteredResult.Status);
        Assert.NotNull(persistedDelivered);
        Assert.Equal("test-provider", persistedDelivered.ProviderName);
        Assert.Equal("provider-record-123", persistedDelivered.ProviderRecordId);
        Assert.Equal("ok", persistedDelivered.Metadata["delivery"]);
        Assert.NotNull(persistedDeadLettered);
        Assert.Equal("terminal-provider-failure", persistedDeadLettered.DeadLetterReason);

        string[] retryReadyEntryIds = [.. retryReady
            .Select(entry => entry.OutboxEntryId)
            .OrderBy(entryId => entryId, StringComparer.Ordinal)];
        string[] expectedRetryReadyEntryIds = [.. new[]
            {
                failed.OutboxEntryId,
                retryable.OutboxEntryId,
                deferred.OutboxEntryId
            }
            .OrderBy(entryId => entryId, StringComparer.Ordinal)];

        Assert.Equal(expectedRetryReadyEntryIds, retryReadyEntryIds);
    }

    /// <summary>
    /// Verifies append and lookup behavior for EF Core audit residue lifecycle persistence.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task LifecycleStoreAppendsAndFindsByEventCorrelationAndAuditResidue()
    {
        await using SqliteConnection connection = await OpenConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(connection);
        await EnsureCreatedAsync(options);

        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreAuditResidueLifecycleStore(context);
        AuditResidueLifecycleEvent first = CreateLifecycleEvent(
            "lifecycle-1",
            AuditResidueLifecycleStage.ExternalEmissionQueued,
            "correlation-shared",
            "audit-shared",
            new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));
        AuditResidueLifecycleEvent second = CreateLifecycleEvent(
            "lifecycle-2",
            AuditResidueLifecycleStage.ExternalEmissionDelivered,
            "correlation-shared",
            "audit-shared",
            new DateTimeOffset(2026, 6, 15, 10, 1, 0, TimeSpan.Zero));
        AuditResidueLifecycleEvent third = CreateLifecycleEvent(
            "lifecycle-3",
            AuditResidueLifecycleStage.ExternalEmissionFailed,
            "correlation-other",
            "audit-other",
            new DateTimeOffset(2026, 6, 15, 10, 2, 0, TimeSpan.Zero));

        _ = await store.AppendAsync(first, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(second, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(third, TestContext.Current.CancellationToken);

        context.ChangeTracker.Clear();

        AuditResidueLifecycleEvent? found = await store.FindByEventIdAsync("lifecycle-1", TestContext.Current.CancellationToken);
        IReadOnlyList<AuditResidueLifecycleEvent> correlationMatches = await store.FindByCorrelationIdAsync("correlation-shared", TestContext.Current.CancellationToken);
        IReadOnlyList<AuditResidueLifecycleEvent> auditResidueMatches = await store.FindByAuditResidueIdAsync("audit-shared", TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal(AuditResidueLifecycleStage.ExternalEmissionQueued, found.Stage);
        Assert.Equal("trace-lifecycle-1", found.TraceId);
        Assert.Equal("test", found.Metadata["source"]);
        Assert.Equal(["lifecycle-1", "lifecycle-2"], [.. correlationMatches.Select(match => match.EventId)]);
        Assert.Equal(["lifecycle-1", "lifecycle-2"], [.. auditResidueMatches.Select(match => match.EventId)]);
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

    private static GovernanceOutboxEntry CreateOutboxEntry(
        string outboxEntryId,
        string eventId,
        DateTimeOffset createdUtc)
    {
        return GovernanceOutboxEntry.Create(
            CreateEnvelope(eventId, $"correlation-{outboxEntryId}", $"audit-{outboxEntryId}"),
            outboxEntryId,
            createdUtc);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(
        string eventId,
        string correlationId,
        string auditResidueId)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditLifecycle,
            eventId,
            new DateTimeOffset(2026, 6, 15, 9, 0, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{eventId}",
            createdUtc: new DateTimeOffset(2026, 6, 15, 9, 0, 1, TimeSpan.Zero),
            schemaVersion: "1.0.0",
            correlationId: correlationId,
            auditResidueId: auditResidueId,
            lifecycleStage: AuditResidueLifecycleStage.ExternalEmissionQueued,
            policyVersion: "2026.06",
            policyHash: "policy-hash-123",
            traceId: "trace-123",
            spanId: "span-123",
            parentSpanId: "parent-span-123",
            operationName: "governance.emit",
            outcome: "Queued",
            actorId: "actor-123",
            emitterStatus: "queued",
            emitterProvider: "local-outbox",
            outboxSequence: 42,
            gatewayExecutionId: "gateway-123",
            decisionStage: "ExternalEmissionQueued",
            payload: GovernanceEmissionPayload.Create(
                "audit-residue",
                schemaVersion: "1.0.0",
                contentType: "application/json",
                contentHash: "payload-hash",
                sizeBytes: 512,
                metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["payload"] = "metadata"
                }),
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source"] = "envelope"
            });
    }

    private static AuditResidueLifecycleEvent CreateLifecycleEvent(
        string eventId,
        AuditResidueLifecycleStage stage,
        string correlationId,
        string auditResidueId,
        DateTimeOffset occurredUtc)
    {
        return AuditResidueLifecycleEvent.Create(
            stage,
            correlationId,
            auditResidueId,
            eventId,
            occurredUtc,
            traceId: $"trace-{eventId}",
            operationName: "governance.emit",
            outcome: stage.ToString(),
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["source"] = "test"
            });
    }

    private sealed class HostOwnedGovernanceDbContext(DbContextOptions<HostOwnedGovernanceDbContext> options)
        : DbContext(options)
    {
        public DbSet<AsiBackboneGovernanceOutboxEntryEntity> GovernanceOutboxEntries =>
            Set<AsiBackboneGovernanceOutboxEntryEntity>();

        public DbSet<AsiBackboneAuditResidueLifecycleEventEntity> AuditResidueLifecycleEvents =>
            Set<AsiBackboneAuditResidueLifecycleEventEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
