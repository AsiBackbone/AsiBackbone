using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.EntityFrameworkCore.Audit;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests;

/// <summary>
/// Integration tests for audit ledger schema version persistence.
/// </summary>
public sealed class EfCoreAuditLedgerSchemaVersionTests
{
    /// <summary>
    /// Verifies that the EF Core audit ledger store persists and rehydrates the audit record schema version.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task AppendAsyncRoundTripsExplicitSchemaVersion()
    {
        await using HostOwnedAuditDbContext context = CreateContext();
        var store = new EfCoreAuditLedgerStore(context);
        AuditLedgerRecord record = CreateRecord("record-123", schemaVersion: "1.1-test");

        _ = await store.AppendAsync(record, TestContext.Current.CancellationToken);

        AuditLedgerRecord? found = await store.FindByRecordIdAsync("record-123", TestContext.Current.CancellationToken);

        Assert.NotNull(found);
        Assert.Equal("1.1-test", found.SchemaVersion);
    }

    private static HostOwnedAuditDbContext CreateContext()
    {
        DbContextOptions<HostOwnedAuditDbContext> options = new DbContextOptionsBuilder<HostOwnedAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HostOwnedAuditDbContext(options);
    }

    private static AuditLedgerRecord CreateRecord(string recordId, string schemaVersion)
    {
        var actor = GovernanceActorContext.Human("actor-123", "Test Actor");
        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Allowed",
            ["policy.allowed"],
            eventId: $"event-{recordId}",
            occurredUtc: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            correlationId: "correlation-123",
            traceId: "trace-123",
            policyVersion: "2026.06",
            policyHash: "policy-hash");

        return AuditLedgerRecord.FromDecisionReceipt(
            residue,
            recordId: recordId,
            recordedUtc: new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            schemaVersion: schemaVersion);
    }

    private sealed class HostOwnedAuditDbContext(DbContextOptions<HostOwnedAuditDbContext> options)
        : DbContext(options)
    {
        public DbSet<AuditLedgerRecordEntity> AuditLedgerRecords =>
            Set<AuditLedgerRecordEntity>();

        public DbSet<AuditLedgerReasonCodeEntity> AuditLedgerReasonCodes =>
            Set<AuditLedgerReasonCodeEntity>();

        public DbSet<AuditLedgerMetadataEntity> AuditLedgerMetadata =>
            Set<AuditLedgerMetadataEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
