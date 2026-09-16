using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Results;
using AsiBackbone.EntityFrameworkCore.Audit;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests;

/// <summary>
/// Integration tests for <see cref="EfCoreAuditLedgerStore" /> using a host-owned EF Core context.
/// </summary>
public sealed class EfCoreAuditLedgerStoreTests
{
    /// <summary>
    /// Verifies that the EF Core audit ledger store appends and rehydrates the full audit ledger record shape.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task AppendAsyncPersistsRecordInHostOwnedDbContext()
    {
        await using HostOwnedAuditDbContext context = CreateContext();
        var store = new EfCoreAuditLedgerStore(context);
        AuditLedgerRecord record = CreateRecord(
            "record-123",
            "correlation-123",
            "trace-123",
            "actor-123",
            new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            ["allowed", "policy.current"],
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["tenant"] = "sample",
                ["source"] = "test"
            });

        OperationResult<AuditLedgerRecord> result = await store.AppendAsync(record, TestContext.Current.CancellationToken);
        AuditLedgerRecord? found = await store.FindByRecordIdAsync("record-123", TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded);
        Assert.NotNull(found);
        Assert.Equal(record.RecordId, found.RecordId);
        Assert.Equal(record.EventId, found.EventId);
        Assert.Equal(record.OccurredUtc, found.OccurredUtc);
        Assert.Equal(record.RecordedUtc, found.RecordedUtc);
        Assert.Equal(record.ActorId, found.ActorId);
        Assert.Equal(record.ActorType, found.ActorType);
        Assert.Equal(record.ActorDisplayName, found.ActorDisplayName);
        Assert.Equal(record.OperationName, found.OperationName);
        Assert.Equal(record.Outcome, found.Outcome);
        Assert.Equal(record.ReasonCodes, found.ReasonCodes);
        Assert.Equal(record.CorrelationId, found.CorrelationId);
        Assert.Equal(record.TraceId, found.TraceId);
        Assert.Equal(record.PolicyVersion, found.PolicyVersion);
        Assert.Equal(record.PolicyHash, found.PolicyHash);
        Assert.Equal(record.HandshakeId, found.HandshakeId);
        Assert.Equal(record.AcknowledgmentId, found.AcknowledgmentId);
        Assert.Equal(record.CapabilityTokenId, found.CapabilityTokenId);
        Assert.Equal(record.PreviousRecordHash, found.PreviousRecordHash);
        Assert.Equal(record.RecordHash, found.RecordHash);
        Assert.Equal(record.SigningHash, found.SigningHash);
        Assert.Equal(record.SignatureKeyId, found.SignatureKeyId);
        Assert.Equal(record.SignatureKeyVersion, found.SignatureKeyVersion);
        Assert.Equal(record.SignatureAlgorithm, found.SignatureAlgorithm);
        Assert.Equal(record.SignatureValue, found.SignatureValue);
        Assert.Equal(record.SignatureProvider, found.SignatureProvider);
        Assert.Equal(record.SignedUtc, found.SignedUtc);
        Assert.Equal(record.SigningMetadata.SigningHash, found.SigningMetadata.SigningHash);
        Assert.Equal(record.SigningMetadata.KeyId, found.SigningMetadata.KeyId);
        Assert.Equal(record.SigningMetadata.KeyVersion, found.SigningMetadata.KeyVersion);
        Assert.Equal(record.SigningMetadata.SignatureAlgorithm, found.SigningMetadata.SignatureAlgorithm);
        Assert.Equal(record.SigningMetadata.Signature, found.SigningMetadata.Signature);
        Assert.Equal(record.SigningMetadata.Provider, found.SigningMetadata.Provider);
        Assert.Equal(record.SigningMetadata.SignedUtc, found.SigningMetadata.SignedUtc);
        Assert.Equal(record.Metadata, found.Metadata);

        Assert.Equal(1, await context.AuditLedgerRecords.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await context.AuditLedgerReasonCodes.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await context.AuditLedgerMetadata.CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that EF Core persistence failures return a stable sanitized operation result instead of provider exception text.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task AppendAsyncReturnsSanitizedFailureWhenPersistenceFails()
    {
        await using ThrowingAuditDbContext context = CreateThrowingContext();
        var store = new EfCoreAuditLedgerStore(context);
        AuditLedgerRecord record = CreateRecord(
            "record-sensitive-failure",
            "correlation-sensitive-failure",
            "trace-sensitive-failure",
            "actor-sensitive-failure");

        OperationResult<AuditLedgerRecord> result = await store.AppendAsync(
            record,
            TestContext.Current.CancellationToken);

        OperationReason reason = Assert.Single(result.Reasons);

        Assert.True(result.Failed);
        Assert.Equal("asi_backbone.audit_ledger.append_failed", reason.Code);
        Assert.Equal(
            "The audit ledger record could not be persisted by the configured EF Core store.",
            reason.Message);
        Assert.DoesNotContain("Sensitive provider detail", reason.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", reason.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("prod-db", reason.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Verifies that audit ledger records can be queried by correlation ID, trace ID, actor ID, and recorded UTC range.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task QueryMethodsReturnExpectedRecords()
    {
        await using HostOwnedAuditDbContext context = CreateContext();
        var store = new EfCoreAuditLedgerStore(context);
        AuditLedgerRecord firstRecord = CreateRecord(
            "record-1",
            "correlation-shared",
            "trace-shared",
            "actor-shared",
            new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero));
        AuditLedgerRecord secondRecord = CreateRecord(
            "record-2",
            "correlation-shared",
            "trace-shared",
            "actor-shared",
            new DateTimeOffset(2026, 6, 2, 0, 0, 0, TimeSpan.Zero));
        AuditLedgerRecord thirdRecord = CreateRecord(
            "record-3",
            "correlation-other",
            "trace-other",
            "actor-other",
            new DateTimeOffset(2026, 6, 3, 0, 0, 0, TimeSpan.Zero));

        _ = await store.AppendAsync(firstRecord, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(secondRecord, TestContext.Current.CancellationToken);
        _ = await store.AppendAsync(thirdRecord, TestContext.Current.CancellationToken);

        IReadOnlyList<AuditLedgerRecord> correlationMatches = await store.FindByCorrelationIdAsync(
            "correlation-shared",
            TestContext.Current.CancellationToken);
        IReadOnlyList<AuditLedgerRecord> traceMatches = await store.FindByTraceIdAsync(
            "trace-shared",
            TestContext.Current.CancellationToken);
        IReadOnlyList<AuditLedgerRecord> actorMatches = await store.FindByActorIdAsync(
            "actor-shared",
            TestContext.Current.CancellationToken);
        IReadOnlyList<AuditLedgerRecord> dateRangeMatches = await store.FindByRecordedUtcRangeAsync(
            new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero),
            TestContext.Current.CancellationToken);

        string[] expectedSharedRecordIds = ["record-1", "record-2"];
        string[] expectedDateRangeRecordIds = ["record-2"];

        Assert.Equal(expectedSharedRecordIds, correlationMatches.Select(record => record.RecordId).ToArray());
        Assert.Equal(expectedSharedRecordIds, traceMatches.Select(record => record.RecordId).ToArray());
        Assert.Equal(expectedSharedRecordIds, actorMatches.Select(record => record.RecordId).ToArray());
        Assert.Equal(expectedDateRangeRecordIds, dateRangeMatches.Select(record => record.RecordId).ToArray());
    }

    private static HostOwnedAuditDbContext CreateContext()
    {
        DbContextOptions<HostOwnedAuditDbContext> options = new DbContextOptionsBuilder<HostOwnedAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HostOwnedAuditDbContext(options);
    }

    private static ThrowingAuditDbContext CreateThrowingContext()
    {
        DbContextOptions<ThrowingAuditDbContext> options = new DbContextOptionsBuilder<ThrowingAuditDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new ThrowingAuditDbContext(options);
    }

    private static AuditLedgerRecord CreateRecord(
        string recordId,
        string correlationId,
        string traceId,
        string actorId,
        DateTimeOffset? recordedUtc = null,
        IEnumerable<string>? reasonCodes = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        var actor = GovernanceActorContext.Human(actorId, "Test Actor");
        var residue = DecisionReceipt.Create(
            actor,
            "system.sync",
            "Allowed",
            reasonCodes,
            eventId: $"event-{recordId}",
            occurredUtc: new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            correlationId: correlationId,
            traceId: traceId,
            policyVersion: "2026.06",
            policyHash: "policy-hash",
            metadata: metadata);

        return AuditLedgerRecord.FromResidue(
            residue,
            recordId: recordId,
            recordedUtc: recordedUtc,
            handshakeId: "handshake-123",
            acknowledgmentId: "ack-123",
            capabilityTokenId: "token-123",
            previousRecordHash: "previous-hash",
            recordHash: "record-hash",
            signatureKeyId: "key-123",
            signatureAlgorithm: "HS256",
            signatureValue: "signature-value",
            signingHash: "signing-hash",
            signatureKeyVersion: "key-version-2026-06",
            signatureProvider: "local-validation",
            signedUtc: new DateTimeOffset(2026, 6, 1, 10, 1, 0, TimeSpan.Zero));
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

    private sealed class ThrowingAuditDbContext(DbContextOptions<ThrowingAuditDbContext> options)
        : DbContext(options)
    {
        public DbSet<AuditLedgerRecordEntity> AuditLedgerRecords =>
            Set<AuditLedgerRecordEntity>();

        public DbSet<AuditLedgerReasonCodeEntity> AuditLedgerReasonCodes =>
            Set<AuditLedgerReasonCodeEntity>();

        public DbSet<AuditLedgerMetadataEntity> AuditLedgerMetadata =>
            Set<AuditLedgerMetadataEntity>();

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            throw new DbUpdateException("Sensitive provider detail: password=secret; server=prod-db; schema=internal.");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
