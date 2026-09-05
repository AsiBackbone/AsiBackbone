using System.Globalization;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Audit;
using AsiBackbone.EntityFrameworkCore.Outbox;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests;

/// <summary>
/// CI-friendly relational validation for EF Core outbox concurrency and drain-worker contention behavior.
/// </summary>
public sealed class EfCoreOutboxConcurrencyValidationTests
{
    private const int ConcurrentWriteCount = 16;

    /// <summary>
    /// Verifies that concurrent host-owned EF Core contexts can preserve outbox entries and lifecycle evidence.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ConcurrentWritersPersistOutboxAndLifecycleRecordsWithoutLosingLocalEvidence()
    {
        await using SqliteConnection keeperConnection = await OpenSharedMemoryConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(keeperConnection.ConnectionString);
        await EnsureCreatedAsync(options);

        Task<WriteEvidence>[] writeTasks = [.. Enumerable
            .Range(0, ConcurrentWriteCount)
            .Select(index => WriteOutboxAndLifecycleEvidenceAsync(options, index))];

        WriteEvidence[] evidence = await Task.WhenAll(writeTasks);

        await using HostOwnedGovernanceDbContext verificationContext = new(options);
        var outboxStore = new EfCoreGovernanceOutboxStore(verificationContext);
        var lifecycleStore = new EfCoreAuditResidueLifecycleStore(verificationContext);

        IReadOnlyList<GovernanceOutboxEntry> pendingEntries = await outboxStore.FindPendingAsync(
            ConcurrentWriteCount + 1,
            TestContext.Current.CancellationToken);
        IReadOnlyList<AuditResidueLifecycleEvent> lifecycleEvents = await lifecycleStore.FindByCorrelationIdAsync(
            "efcore-concurrency-validation",
            TestContext.Current.CancellationToken);

        Assert.Equal(ConcurrentWriteCount, evidence.Select(item => item.OutboxEntryId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ConcurrentWriteCount, evidence.Select(item => item.LifecycleEventId).Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(ConcurrentWriteCount, pendingEntries.Count);
        Assert.Equal(ConcurrentWriteCount, lifecycleEvents.Count);
        Assert.All(pendingEntries, entry =>
        {
            Assert.Equal("efcore-concurrency-validation", entry.Envelope.CorrelationId);
            Assert.Equal(GovernanceEmissionStatus.Pending, entry.Status);
            Assert.Equal(AuditResidueLifecycleStage.ExternalEmissionQueued, entry.Envelope.LifecycleStage);
        });
        Assert.All(lifecycleEvents, lifecycleEvent =>
        {
            Assert.Equal("efcore-concurrency-validation", lifecycleEvent.CorrelationId);
            Assert.Equal(AuditResidueLifecycleStage.ExternalEmissionQueued, lifecycleEvent.Stage);
        });
    }

    /// <summary>
    /// Verifies that under the default claim-lease configuration two concurrent workers emit a pending entry exactly once.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ConcurrentDrainWorkersEmitPendingEntryOnceUnderDefaultClaimLeases()
    {
        await using SqliteConnection keeperConnection = await OpenSharedMemoryConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(keeperConnection.ConnectionString);
        await EnsureCreatedAsync(options);

        string outboxEntryId;
        await using (HostOwnedGovernanceDbContext seedContext = new(options))
        {
            var store = new EfCoreGovernanceOutboxStore(seedContext);
            GovernanceOutboxEntry entry = await store.EnqueueAsync(
                CreateEnvelope(1),
                TestContext.Current.CancellationToken);
            outboxEntryId = entry.OutboxEntryId;
        }

        var emitter = new CoordinatedDeliveredEmitter(expectedEmissionCount: 1);

        // Distinct worker identifiers model two replicas, which is what the machine-name plus process-id default produces.
        Task<IReadOnlyList<GovernanceOutboxEntry>> firstDrain = DrainWithNewContextAsync(options, emitter, "worker-a");
        Task<IReadOnlyList<GovernanceOutboxEntry>> secondDrain = DrainWithNewContextAsync(options, emitter, "worker-b");

        Exception?[] drainExceptions = await Task.WhenAll(
            CaptureExceptionAsync(firstDrain),
            CaptureExceptionAsync(secondDrain));

        await using HostOwnedGovernanceDbContext verificationContext = new(options);
        var verificationStore = new EfCoreGovernanceOutboxStore(verificationContext);
        GovernanceOutboxEntry? persistedEntry = await verificationStore.FindByOutboxEntryIdAsync(
            outboxEntryId,
            TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> pendingEntries = await verificationStore.FindPendingAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(1, emitter.EmissionCount);
        Assert.All(drainExceptions, Assert.Null);
        Assert.NotNull(persistedEntry);
        Assert.Equal(GovernanceEmissionStatus.Delivered, persistedEntry.Status);
        Assert.DoesNotContain(pendingEntries, entry => entry.OutboxEntryId == outboxEntryId);
    }

    /// <summary>
    /// Verifies the documented consequence of opting out of claim leases: two workers can select the same pending entry before either worker saves final state.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task ConcurrentDrainWorkersCanReachSamePendingEntryWhenClaimLeasesAreDisabled()
    {
        await using SqliteConnection keeperConnection = await OpenSharedMemoryConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(keeperConnection.ConnectionString);
        await EnsureCreatedAsync(options);

        string outboxEntryId;
        await using (HostOwnedGovernanceDbContext seedContext = new(options))
        {
            var store = new EfCoreGovernanceOutboxStore(seedContext);
            GovernanceOutboxEntry entry = await store.EnqueueAsync(
                CreateEnvelope(1),
                TestContext.Current.CancellationToken);
            outboxEntryId = entry.OutboxEntryId;
        }

        var emitter = new CoordinatedDeliveredEmitter(expectedEmissionCount: 2);
        Task<IReadOnlyList<GovernanceOutboxEntry>> firstDrain = DrainWithNewContextAsync(options, emitter, workerId: null);
        Task<IReadOnlyList<GovernanceOutboxEntry>> secondDrain = DrainWithNewContextAsync(options, emitter, workerId: null);

        Exception?[] drainExceptions = await Task.WhenAll(
            CaptureExceptionAsync(firstDrain),
            CaptureExceptionAsync(secondDrain));

        int successfulDrainCount = new[] { firstDrain, secondDrain }
            .Count(task => task.Status == TaskStatus.RanToCompletion);

        await using HostOwnedGovernanceDbContext verificationContext = new(options);
        var verificationStore = new EfCoreGovernanceOutboxStore(verificationContext);
        GovernanceOutboxEntry? persistedEntry = await verificationStore.FindByOutboxEntryIdAsync(
            outboxEntryId,
            TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> pendingEntries = await verificationStore.FindPendingAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, emitter.EmissionCount);
        Assert.InRange(successfulDrainCount, 1, 2);
        Assert.All(drainExceptions.Where(exception => exception is not null), exception =>
            Assert.IsAssignableFrom<DbUpdateConcurrencyException>(exception));
        Assert.NotNull(persistedEntry);
        Assert.Equal(GovernanceEmissionStatus.Delivered, persistedEntry.Status);
        Assert.DoesNotContain(pendingEntries, entry => entry.OutboxEntryId == outboxEntryId);
    }

    /// <summary>
    /// Verifies that a transient downstream failure is persisted as retryable EF Core outbox state.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DrainPersistsRetryableFailuresAndRetryReadyEvidence()
    {
        await using SqliteConnection keeperConnection = await OpenSharedMemoryConnectionAsync();
        DbContextOptions<HostOwnedGovernanceDbContext> options = CreateOptions(keeperConnection.ConnectionString);
        await EnsureCreatedAsync(options);

        const int retryEntryCount = 4;
        DateTimeOffset drainUtc = new(2026, 6, 19, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset retryReadyUtc = drainUtc.AddSeconds(30);

        await using (HostOwnedGovernanceDbContext seedContext = new(options))
        {
            var seedStore = new EfCoreGovernanceOutboxStore(seedContext);

            for (int index = 0; index < retryEntryCount; index++)
            {
                _ = await seedStore.EnqueueAsync(
                    CreateEnvelope(index + 100),
                    TestContext.Current.CancellationToken);
            }
        }

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries;
        await using (HostOwnedGovernanceDbContext drainContext = new(options))
        {
            var drainStore = new EfCoreGovernanceOutboxStore(drainContext);
            var drain = new AsiBackboneGovernanceOutboxDrain(
                drainStore,
                new RetryableFailureEmitter(retryReadyUtc));

            drainedEntries = await drain.DrainAsync(
                drainUtc,
                retryEntryCount,
                TestContext.Current.CancellationToken);
        }

        await using HostOwnedGovernanceDbContext verificationContext = new(options);
        var verificationStore = new EfCoreGovernanceOutboxStore(verificationContext);
        IReadOnlyList<GovernanceOutboxEntry> retryReadyBefore = await verificationStore.FindRetryReadyAsync(
            retryReadyUtc.AddTicks(-1),
            cancellationToken: TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> retryReadyAfter = await verificationStore.FindRetryReadyAsync(
            retryReadyUtc,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(retryEntryCount, drainedEntries.Count);
        Assert.All(drainedEntries, entry =>
        {
            Assert.Equal(GovernanceEmissionStatus.RetryableFailure, entry.Status);
            Assert.Equal(1, entry.RetryCount);
            Assert.Equal(retryReadyUtc, entry.NextRetryUtc);
            Assert.Equal("efcore.validation.transient", entry.LastError?.Code);
        });
        Assert.Empty(retryReadyBefore);
        Assert.Equal(retryEntryCount, retryReadyAfter.Count);
    }

    private static async Task<WriteEvidence> WriteOutboxAndLifecycleEvidenceAsync(
        DbContextOptions<HostOwnedGovernanceDbContext> options,
        int index)
    {
        await using HostOwnedGovernanceDbContext context = new(options);
        var outboxStore = new EfCoreGovernanceOutboxStore(context);
        var lifecycleStore = new EfCoreAuditResidueLifecycleStore(context);

        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope(index),
            TestContext.Current.CancellationToken);
        AuditResidueLifecycleEvent lifecycleEvent = await lifecycleStore.AppendAsync(
            CreateLifecycleEvent(index),
            TestContext.Current.CancellationToken);

        return new WriteEvidence(entry.OutboxEntryId, lifecycleEvent.EventId);
    }

    /// <summary>
    /// Drains a single entry on its own context, claiming as <paramref name="workerId" /> or opting out of claim leases when it is null.
    /// </summary>
    /// <param name="options">The database context options shared by the competing drains.</param>
    /// <param name="emitter">The emitter observing emission counts across both drains.</param>
    /// <param name="workerId">The claim owner, or null to disable claim leases and exercise the opt-out path.</param>
    /// <returns>The entries attempted by this drain.</returns>
    private static async Task<IReadOnlyList<GovernanceOutboxEntry>> DrainWithNewContextAsync(
        DbContextOptions<HostOwnedGovernanceDbContext> options,
        IAsiBackboneGovernanceEmitter emitter,
        string? workerId)
    {
        await using HostOwnedGovernanceDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);
        var outboxOptions = new AsiBackboneGovernanceOutboxOptions
        {
            UseClaimLeases = workerId is not null,
            ClaimWorkerId = workerId ?? AsiBackboneGovernanceOutboxOptions.DefaultClaimWorkerId
        };
        var drain = new AsiBackboneGovernanceOutboxDrain(
            store,
            emitter,
            outboxOptions: Options.Create(outboxOptions));

        return await drain.DrainAsync(
            new DateTimeOffset(2026, 6, 19, 12, 0, 0, TimeSpan.Zero),
            maxCount: 1,
            TestContext.Current.CancellationToken);
    }

    private static async Task<Exception?> CaptureExceptionAsync(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    private static async Task<SqliteConnection> OpenSharedMemoryConnectionAsync()
    {
        string databaseName = $"AsiBackboneConcurrency_{Guid.NewGuid():N}";
        var connection = new SqliteConnection($"Data Source={databaseName};Mode=Memory;Cache=Shared;Default Timeout=30");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return connection;
    }

    private static DbContextOptions<HostOwnedGovernanceDbContext> CreateOptions(string connectionString)
    {
        return new DbContextOptionsBuilder<HostOwnedGovernanceDbContext>()
            .UseSqlite(connectionString)
            .Options;
    }

    private static async Task EnsureCreatedAsync(DbContextOptions<HostOwnedGovernanceDbContext> options)
    {
        await using HostOwnedGovernanceDbContext context = new(options);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(int index)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditLifecycle,
            eventId: $"event-concurrency-{index:D3}",
            occurredUtc: new DateTimeOffset(2026, 6, 19, 11, 0, 0, TimeSpan.Zero).AddSeconds(index),
            envelopeId: $"envelope-concurrency-{index:D3}",
            createdUtc: new DateTimeOffset(2026, 6, 19, 11, 0, 1, TimeSpan.Zero).AddSeconds(index),
            schemaVersion: "1.0.0",
            correlationId: "efcore-concurrency-validation",
            auditResidueId: $"audit-concurrency-{index:D3}",
            lifecycleStage: AuditResidueLifecycleStage.ExternalEmissionQueued,
            policyVersion: "2026.06",
            policyHash: "policy-hash-concurrency",
            traceId: $"trace-concurrency-{index:D3}",
            spanId: $"span-concurrency-{index:D3}",
            parentSpanId: "parent-span-concurrency",
            operationName: "governance.emit",
            outcome: "Queued",
            actorId: "actor-concurrency",
            emitterStatus: "queued",
            emitterProvider: "efcore-outbox",
            outboxSequence: index,
            decisionStage: "ExternalEmissionQueued",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["validation"] = "efcore-outbox-concurrency",
                ["index"] = index.ToString("D3", CultureInfo.InvariantCulture)
            });
    }

    private static AuditResidueLifecycleEvent CreateLifecycleEvent(int index)
    {
        return AuditResidueLifecycleEvent.Create(
            AuditResidueLifecycleStage.ExternalEmissionQueued,
            "efcore-concurrency-validation",
            $"audit-concurrency-{index:D3}",
            $"lifecycle-concurrency-{index:D3}",
            new DateTimeOffset(2026, 6, 19, 11, 1, 0, TimeSpan.Zero).AddSeconds(index),
            traceId: $"trace-concurrency-{index:D3}",
            operationName: "governance.emit",
            outcome: "Queued",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["validation"] = "efcore-outbox-concurrency",
                ["index"] = index.ToString("D3", CultureInfo.InvariantCulture)
            });
    }

    private readonly record struct WriteEvidence(string OutboxEntryId, string LifecycleEventId);

    private sealed class CoordinatedDeliveredEmitter(int expectedEmissionCount) : IAsiBackboneGovernanceEmitter
    {
        private readonly TaskCompletionSource allExpectedEmissionsArrived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int emissionCount;

        public int EmissionCount => Volatile.Read(ref emissionCount);

        public async ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);
            cancellationToken.ThrowIfCancellationRequested();

            int observedEmissionCount = Interlocked.Increment(ref emissionCount);
            if (observedEmissionCount >= expectedEmissionCount)
            {
                _ = allExpectedEmissionsArrived.TrySetResult();
            }

            await allExpectedEmissionsArrived.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);

            return GovernanceEmissionResult.Delivered(
                "efcore-concurrency-test",
                envelope.EnvelopeId,
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["validation"] = "coordinated-drain-contention",
                    ["emission.count"] = observedEmissionCount.ToString("D3", CultureInfo.InvariantCulture)
                });
        }
    }

    private sealed class RetryableFailureEmitter(DateTimeOffset retryReadyUtc) : IAsiBackboneGovernanceEmitter
    {
        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(GovernanceEmissionResult.RetryableFailure(
                GovernanceEmissionError.Create(
                    "efcore.validation.transient",
                    "The validation emitter simulated a transient provider failure.",
                    isRetryable: true,
                    providerName: "efcore-concurrency-test"),
                retryReadyUtc));
        }
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
