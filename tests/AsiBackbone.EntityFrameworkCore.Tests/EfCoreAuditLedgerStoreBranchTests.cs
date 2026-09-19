using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Results;
using AsiBackbone.EntityFrameworkCore.Audit;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests;

/// <summary>
/// Tests for the <see cref="EfCoreAuditLedgerStore"/> class using a branch of the audit ledger.
/// </summary>
public sealed class EfCoreAuditLedgerStoreBranchTests
{
    /// <summary>
    /// Tests that the <see cref="EfCoreAuditLedgerStore.FindByRecordIdAsync(string, CancellationToken)"/> method returns null when the specified record ID does not exist in the audit ledger.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is null when the record ID does not exist.
    /// </returns>
    [Fact]
    public async Task FindByRecordIdAsyncReturnsNullWhenRecordDoesNotExist()
    {
        await using HostOwnedAuditDbContext context = CreateInMemoryContext();
        var store = new EfCoreAuditLedgerStore(context);

        AuditLedgerRecord? found = await store.FindByRecordIdAsync(
            "missing-record",
            TestContext.Current.CancellationToken);

        Assert.Null(found);
    }

    /// <summary>
    /// Tests that the <see cref="EfCoreAuditLedgerStore.FindByRecordedUtcRangeAsync(DateTimeOffset, DateTimeOffset, CancellationToken)"/> method throws an <see cref="ArgumentException"/> when the specified recorded UTC range is inverted (i.e., the start time is after the end time).
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is an <see cref="ArgumentException"/> when the recorded UTC range is inverted.
    /// </returns>
    [Fact]
    public async Task FindByRecordedUtcRangeAsyncRejectsInvertedRange()
    {
        await using HostOwnedAuditDbContext context = CreateInMemoryContext();
        var store = new EfCoreAuditLedgerStore(context);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(() =>
            store.FindByRecordedUtcRangeAsync(
                new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero),
                new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
                TestContext.Current.CancellationToken).AsTask());

        Assert.Contains("recorded UTC range", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests that the <see cref="EfCoreAuditLedgerStore.AppendAsync(AuditLedgerRecord, CancellationToken)"/> method returns a failure result when attempting to append a record with a duplicate record ID, which is rejected by the underlying database provider.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is a failure result when the provider rejects the duplicate record ID.
    /// </returns>
    [Fact]
    public async Task AppendAsyncReturnsFailureWhenProviderRejectsDuplicateRecordId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        DbContextOptions<HostOwnedAuditDbContext> options = new DbContextOptionsBuilder<HostOwnedAuditDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var context = new HostOwnedAuditDbContext(options);
        _ = await context.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var store = new EfCoreAuditLedgerStore(context);
        AuditLedgerRecord firstRecord = CreateRecord("duplicate-record", "event-1");
        AuditLedgerRecord secondRecord = CreateRecord("duplicate-record", "event-2");

        OperationResult<AuditLedgerRecord> firstResult = await store.AppendAsync(
            firstRecord,
            TestContext.Current.CancellationToken);
        OperationResult<AuditLedgerRecord> secondResult = await store.AppendAsync(
            secondRecord,
            TestContext.Current.CancellationToken);

        Assert.True(firstResult.Succeeded);
        Assert.False(secondResult.Succeeded);
        Assert.Contains("asi_backbone.audit_ledger.append_failed", secondResult.ReasonCodes);
    }

    /// <summary>
    /// Tests that the <see cref="EfCoreAuditLedgerStore.FindByRecordIdAsync(string, CancellationToken)"/> method correctly handles empty JSON payloads for reason codes and metadata, treating them as empty collections instead of null or invalid data.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation. The task result is an <see cref="AuditLedgerRecord"/> with empty collections for reason codes and metadata when the JSON payloads are empty.
    /// </returns>
    [Fact]
    public async Task FindByRecordIdAsyncHandlesEmptyJsonPayloadsAsEmptyCollections()
    {
        await using HostOwnedAuditDbContext context = CreateInMemoryContext();
        var store = new EfCoreAuditLedgerStore(context);

        _ = context.AuditLedgerRecords.Add(new AuditLedgerRecordEntity
        {
            RecordId = "empty-json-record",
            EventId = "empty-json-event",
            OccurredUtc = new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
            RecordedUtc = new DateTimeOffset(2026, 6, 1, 1, 0, 0, TimeSpan.Zero),
            ActorId = "empty-json-actor",
            ActorType = GovernanceActorType.Service,
            OperationName = "empty-json.operation",
            Outcome = "Allowed",
            ReasonCodesJson = " ",
            MetadataJson = " ",
        });
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        AuditLedgerRecord? found = await store.FindByRecordIdAsync(
            "empty-json-record",
            TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Empty(found.ReasonCodes);
        Assert.Empty(found.Metadata);
    }

    private static HostOwnedAuditDbContext CreateInMemoryContext()
    {
        DbContextOptions<HostOwnedAuditDbContext> options = new DbContextOptionsBuilder<HostOwnedAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HostOwnedAuditDbContext(options);
    }

    private static AuditLedgerRecord CreateRecord(string recordId, string eventId)
    {
        var actor = GovernanceActorContext.Human("actor-branch", "Branch Actor");
        var residue = DecisionReceipt.Create(
            actor,
            "branch.operation",
            "Allowed",
            ["branch.reason"],
            eventId: eventId,
            occurredUtc: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            correlationId: "branch-correlation",
            traceId: "branch-trace",
            policyVersion: "branch-version",
            policyHash: "branch-hash",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["branch"] = "true",
            });

        return AuditLedgerRecord.FromDecisionReceipt(
            residue,
            recordId: recordId,
            recordedUtc: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero));
    }

    private sealed class HostOwnedAuditDbContext(DbContextOptions<HostOwnedAuditDbContext> options)
        : DbContext(options)
    {
        public DbSet<AuditLedgerRecordEntity> AuditLedgerRecords =>
            Set<AuditLedgerRecordEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
