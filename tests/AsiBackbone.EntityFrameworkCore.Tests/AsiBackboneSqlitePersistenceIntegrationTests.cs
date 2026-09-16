using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Handshakes;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests;

/// <summary>
/// SQLite-backed integration tests for ASI Backbone Entity Framework Core persistence configuration.
/// </summary>
public sealed class AsiBackboneSqlitePersistenceIntegrationTests
{
    /// <summary>
    /// Verifies that a host-owned <see cref="DbContext" /> can apply ASI Backbone configurations and create a relational schema.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task HostOwnedDbContextBuildsSqliteModelWithoutPackageOwnedMigrations()
    {
        await using SqliteHostFixture fixture = await SqliteHostFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using HostOwnedSqliteDbContext context = fixture.CreateContext();

        Assert.NotNull(context.Model.FindEntityType(typeof(AuditLedgerRecordEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AuditLedgerReasonCodeEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(AuditLedgerMetadataEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(HandshakeRequestEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(HandshakeRequestMetadataEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(HandshakeAcknowledgmentEntity)));
        Assert.NotNull(context.Model.FindEntityType(typeof(HandshakeAcknowledgmentMetadataEntity)));
        Assert.Empty(context.Database.GetMigrations());

        string[] tableNames = await context.Database
            .SqlQueryRaw<string>("SELECT name AS Value FROM sqlite_master WHERE type = 'table' ORDER BY name")
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Contains("AsiBackboneAuditLedgerRecords", tableNames);
        Assert.Contains("AsiBackboneAuditLedgerReasonCodes", tableNames);
        Assert.Contains("AsiBackboneAuditLedgerMetadata", tableNames);
        Assert.Contains("AsiBackboneHandshakeRequests", tableNames);
        Assert.Contains("AsiBackboneHandshakeRequestMetadata", tableNames);
        Assert.Contains("AsiBackboneHandshakeAcknowledgments", tableNames);
        Assert.Contains("AsiBackboneHandshakeAcknowledgmentMetadata", tableNames);
    }

    /// <summary>
    /// Verifies that audit ledger accountability records, reason codes, and metadata persist and read back through SQLite.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task AuditLedgerRecordPersistsReasonCodesMetadataAndIdentifiers()
    {
        await using SqliteHostFixture fixture = await SqliteHostFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using HostOwnedSqliteDbContext context = fixture.CreateContext();
        var record = new AuditLedgerRecordEntity
        {
            RecordId = "record-123",
            EventId = "event-123",
            OccurredUtc = new DateTimeOffset(2026, 6, 1, 9, 30, 0, TimeSpan.Zero),
            RecordedUtc = new DateTimeOffset(2026, 6, 1, 9, 31, 0, TimeSpan.Zero),
            ActorId = "actor-123",
            ActorType = GovernanceActorType.Human,
            ActorDisplayName = "Test Actor",
            OperationName = "document.approve",
            Outcome = "RequireAcknowledgment",
            ReasonCodesJson = "[\"policy.requires_acknowledgment\",\"risk.medium\"]",
            CorrelationId = "correlation-123",
            TraceId = "trace-123",
            PolicyVersion = "2026.06",
            PolicyHash = "policy-hash-123",
            HandshakeId = "handshake-123",
            AcknowledgmentId = "ack-123",
            CapabilityTokenId = "capability-token-123",
            PreviousRecordHash = "previous-record-hash",
            RecordHash = "record-hash",
            SignatureKeyId = "signing-key-1",
            SignatureAlgorithm = "HS256",
            SignatureValue = "signature-value",
            MetadataJson = /*lang=json,strict*/ "{\"tenant\":\"sample\",\"source\":\"integration-test\"}"
        };

        _ = context.AuditLedgerRecords.Add(record);
        context.AuditLedgerReasonCodes.AddRange(
            new AuditLedgerReasonCodeEntity
            {
                AuditLedgerRecordId = record.Id,
                Sequence = 0,
                ReasonCode = "policy.requires_acknowledgment"
            },
            new AuditLedgerReasonCodeEntity
            {
                AuditLedgerRecordId = record.Id,
                Sequence = 1,
                ReasonCode = "risk.medium"
            });
        context.AuditLedgerMetadata.AddRange(
            new AuditLedgerMetadataEntity
            {
                AuditLedgerRecordId = record.Id,
                MetadataKey = "tenant",
                MetadataValue = "sample"
            },
            new AuditLedgerMetadataEntity
            {
                AuditLedgerRecordId = record.Id,
                MetadataKey = "source",
                MetadataValue = "integration-test"
            });

        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        AuditLedgerRecordEntity persistedRecord = await context.AuditLedgerRecords
            .SingleAsync(entity => entity.RecordId == "record-123", TestContext.Current.CancellationToken);
        string[] persistedReasonCodes = await context.AuditLedgerReasonCodes
            .Where(entity => entity.AuditLedgerRecordId == persistedRecord.Id)
            .OrderBy(entity => entity.Sequence)
            .Select(entity => entity.ReasonCode)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Dictionary<string, string> persistedMetadata = await context.AuditLedgerMetadata
            .Where(entity => entity.AuditLedgerRecordId == persistedRecord.Id)
            .ToDictionaryAsync(entity => entity.MetadataKey, entity => entity.MetadataValue, TestContext.Current.CancellationToken);

        Assert.Equal("event-123", persistedRecord.EventId);
        Assert.Equal(GovernanceActorType.Human, persistedRecord.ActorType);
        Assert.Equal("actor-123", persistedRecord.ActorId);
        Assert.Equal("Test Actor", persistedRecord.ActorDisplayName);
        Assert.Equal("document.approve", persistedRecord.OperationName);
        Assert.Equal("RequireAcknowledgment", persistedRecord.Outcome);
        Assert.Equal("correlation-123", persistedRecord.CorrelationId);
        Assert.Equal("trace-123", persistedRecord.TraceId);
        Assert.Equal("2026.06", persistedRecord.PolicyVersion);
        Assert.Equal("policy-hash-123", persistedRecord.PolicyHash);
        Assert.Equal("handshake-123", persistedRecord.HandshakeId);
        Assert.Equal("ack-123", persistedRecord.AcknowledgmentId);
        Assert.Equal("capability-token-123", persistedRecord.CapabilityTokenId);
        Assert.Equal(["policy.requires_acknowledgment", "risk.medium"], persistedReasonCodes);
        Assert.Equal("sample", persistedMetadata["tenant"]);
        Assert.Equal("integration-test", persistedMetadata["source"]);
    }

    /// <summary>
    /// Verifies the expected audit ledger query paths over SQLite-backed persistence.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task AuditLedgerRecordsAreQueryableByCorrelationTraceActorAndDateRange()
    {
        await using SqliteHostFixture fixture = await SqliteHostFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using HostOwnedSqliteDbContext context = fixture.CreateContext();

        context.AuditLedgerRecords.AddRange(
            CreateAuditRecord("record-1", "correlation-shared", "trace-shared", "actor-shared", 1),
            CreateAuditRecord("record-2", "correlation-shared", "trace-shared", "actor-shared", 2),
            CreateAuditRecord("record-3", "correlation-other", "trace-other", "actor-other", 3));
        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        string[] correlationMatches = await context.AuditLedgerRecords
            .Where(entity => entity.CorrelationId == "correlation-shared")
            .OrderBy(entity => entity.RecordedUtc)
            .Select(entity => entity.RecordId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        string[] traceMatches = await context.AuditLedgerRecords
            .Where(entity => entity.TraceId == "trace-shared")
            .OrderBy(entity => entity.RecordedUtc)
            .Select(entity => entity.RecordId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        string[] actorMatches = await context.AuditLedgerRecords
            .Where(entity => entity.ActorId == "actor-shared")
            .OrderBy(entity => entity.RecordedUtc)
            .Select(entity => entity.RecordId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        string[] dateRangeMatches = await context.AuditLedgerRecords
            .Where(entity =>
                entity.RecordedUtc >= new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero) &&
                entity.RecordedUtc <= new DateTimeOffset(2026, 6, 2, 12, 0, 0, TimeSpan.Zero))
            .OrderBy(entity => entity.RecordedUtc)
            .Select(entity => entity.RecordId)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["record-1", "record-2"], correlationMatches);
        Assert.Equal(["record-1", "record-2"], traceMatches);
        Assert.Equal(["record-1", "record-2"], actorMatches);
        Assert.Equal(["record-2"], dateRangeMatches);
    }

    /// <summary>
    /// Verifies that handshake request and acknowledgment accountability records persist with metadata.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task HandshakeRequestAndAcknowledgmentPersistWithMetadata()
    {
        await using SqliteHostFixture fixture = await SqliteHostFixture.CreateAsync(TestContext.Current.CancellationToken);
        await using HostOwnedSqliteDbContext context = fixture.CreateContext();
        var request = new HandshakeRequestEntity
        {
            HandshakeId = "handshake-123",
            ActorId = "actor-123",
            ActorType = GovernanceActorType.Human,
            ActorDisplayName = "Test Actor",
            OperationName = "external-api.execute",
            ReasonCode = "policy.requires_acknowledgment",
            Message = "This action requires acknowledgment before execution.",
            RequiredAcknowledgmentCode = "ACK-REQUIRED",
            RequiredAcknowledgmentText = "I understand the responsibility for this action.",
            RiskLevel = LiabilityHandshakeRiskLevel.Medium,
            RiskCategory = "external-api",
            CorrelationId = "correlation-123",
            TraceId = "trace-123",
            PolicyVersion = "2026.06",
            PolicyHash = "policy-hash-123"
        };
        var acknowledgment = new HandshakeAcknowledgmentEntity
        {
            AcknowledgmentId = "ack-123",
            HandshakeId = "handshake-123",
            ActorId = "actor-123",
            ActorType = GovernanceActorType.Human,
            ActorDisplayName = "Test Actor",
            AcknowledgmentCode = "ACK-REQUIRED",
            Acknowledged = true,
            OccurredUtc = new DateTimeOffset(2026, 6, 1, 10, 0, 0, TimeSpan.Zero),
            CorrelationId = "correlation-123",
            TraceId = "trace-123"
        };

        _ = context.HandshakeRequests.Add(request);
        context.HandshakeRequestMetadata.AddRange(
            new HandshakeRequestMetadataEntity
            {
                HandshakeRequestId = request.Id,
                MetadataKey = "tenant",
                MetadataValue = "sample"
            },
            new HandshakeRequestMetadataEntity
            {
                HandshakeRequestId = request.Id,
                MetadataKey = "channel",
                MetadataValue = "web"
            });
        _ = context.HandshakeAcknowledgments.Add(acknowledgment);
        context.HandshakeAcknowledgmentMetadata.AddRange(
            new HandshakeAcknowledgmentMetadataEntity
            {
                HandshakeAcknowledgmentId = acknowledgment.Id,
                MetadataKey = "ip",
                MetadataValue = "127.0.0.1"
            },
            new HandshakeAcknowledgmentMetadataEntity
            {
                HandshakeAcknowledgmentId = acknowledgment.Id,
                MetadataKey = "agent",
                MetadataValue = "integration-test"
            });

        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.ChangeTracker.Clear();

        HandshakeRequestEntity persistedRequest = await context.HandshakeRequests
            .SingleAsync(entity => entity.HandshakeId == "handshake-123", TestContext.Current.CancellationToken);
        HandshakeAcknowledgmentEntity persistedAcknowledgment = await context.HandshakeAcknowledgments
            .SingleAsync(entity => entity.AcknowledgmentId == "ack-123", TestContext.Current.CancellationToken);
        Dictionary<string, string> requestMetadata = await context.HandshakeRequestMetadata
            .Where(entity => entity.HandshakeRequestId == persistedRequest.Id)
            .ToDictionaryAsync(entity => entity.MetadataKey, entity => entity.MetadataValue, TestContext.Current.CancellationToken);
        Dictionary<string, string> acknowledgmentMetadata = await context.HandshakeAcknowledgmentMetadata
            .Where(entity => entity.HandshakeAcknowledgmentId == persistedAcknowledgment.Id)
            .ToDictionaryAsync(entity => entity.MetadataKey, entity => entity.MetadataValue, TestContext.Current.CancellationToken);

        Assert.Equal(GovernanceActorType.Human, persistedRequest.ActorType);
        Assert.Equal(LiabilityHandshakeRiskLevel.Medium, persistedRequest.RiskLevel);
        Assert.Equal("correlation-123", persistedRequest.CorrelationId);
        Assert.Equal("trace-123", persistedRequest.TraceId);
        Assert.Equal("2026.06", persistedRequest.PolicyVersion);
        Assert.Equal("policy-hash-123", persistedRequest.PolicyHash);
        Assert.Equal("sample", requestMetadata["tenant"]);
        Assert.Equal("web", requestMetadata["channel"]);
        Assert.Equal("handshake-123", persistedAcknowledgment.HandshakeId);
        Assert.Equal(GovernanceActorType.Human, persistedAcknowledgment.ActorType);
        Assert.True(persistedAcknowledgment.Acknowledged);
        Assert.Equal("correlation-123", persistedAcknowledgment.CorrelationId);
        Assert.Equal("trace-123", persistedAcknowledgment.TraceId);
        Assert.Equal("127.0.0.1", acknowledgmentMetadata["ip"]);
        Assert.Equal("integration-test", acknowledgmentMetadata["agent"]);
    }

    private static AuditLedgerRecordEntity CreateAuditRecord(
        string recordId,
        string correlationId,
        string traceId,
        string actorId,
        int day)
    {
        return new AuditLedgerRecordEntity
        {
            RecordId = recordId,
            EventId = $"event-{recordId}",
            OccurredUtc = new DateTimeOffset(2026, 6, day, 9, 0, 0, TimeSpan.Zero),
            RecordedUtc = new DateTimeOffset(2026, 6, day, 10, 0, 0, TimeSpan.Zero),
            ActorId = actorId,
            ActorType = GovernanceActorType.Human,
            OperationName = "document.approve",
            Outcome = "Allowed",
            ReasonCodesJson = "[]",
            CorrelationId = correlationId,
            TraceId = traceId,
            PolicyVersion = "2026.06",
            PolicyHash = "policy-hash",
            MetadataJson = "{}"
        };
    }

    private sealed class SqliteHostFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions<HostOwnedSqliteDbContext> options;

        private SqliteHostFixture(SqliteConnection connection, DbContextOptions<HostOwnedSqliteDbContext> options)
        {
            this.connection = connection;
            this.options = options;
        }

        public static async Task<SqliteHostFixture> CreateAsync(CancellationToken cancellationToken)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(cancellationToken);

            DbContextOptions<HostOwnedSqliteDbContext> options = new DbContextOptionsBuilder<HostOwnedSqliteDbContext>()
                .UseSqlite(connection)
                .Options;

            var fixture = new SqliteHostFixture(connection, options);

            await using HostOwnedSqliteDbContext context = fixture.CreateContext();
            _ = await context.Database.EnsureCreatedAsync(cancellationToken);

            return fixture;
        }

        public HostOwnedSqliteDbContext CreateContext()
        {
            return new HostOwnedSqliteDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
        }
    }

    private sealed class HostOwnedSqliteDbContext(DbContextOptions<HostOwnedSqliteDbContext> options)
        : DbContext(options)
    {
        private static readonly ValueConverter<DateTimeOffset, long> DateTimeOffsetToTicksConverter =
            new(
                value => value.UtcDateTime.Ticks,
                value => new DateTimeOffset(new DateTime(value, DateTimeKind.Utc)));

        public DbSet<AuditLedgerRecordEntity> AuditLedgerRecords =>
            Set<AuditLedgerRecordEntity>();

        public DbSet<AuditLedgerReasonCodeEntity> AuditLedgerReasonCodes =>
            Set<AuditLedgerReasonCodeEntity>();

        public DbSet<AuditLedgerMetadataEntity> AuditLedgerMetadata =>
            Set<AuditLedgerMetadataEntity>();

        public DbSet<HandshakeRequestEntity> HandshakeRequests =>
            Set<HandshakeRequestEntity>();

        public DbSet<HandshakeRequestMetadataEntity> HandshakeRequestMetadata =>
            Set<HandshakeRequestMetadataEntity>();

        public DbSet<HandshakeAcknowledgmentEntity> HandshakeAcknowledgments =>
            Set<HandshakeAcknowledgmentEntity>();

        public DbSet<HandshakeAcknowledgmentMetadataEntity> HandshakeAcknowledgmentMetadata =>
            Set<HandshakeAcknowledgmentMetadataEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.ApplyAsiBackboneConfigurations();
            ApplySqliteDateTimeOffsetConversions(modelBuilder);
        }

        private static void ApplySqliteDateTimeOffsetConversions(ModelBuilder modelBuilder)
        {
            _ = modelBuilder.Entity<AuditLedgerRecordEntity>()
                .Property(entity => entity.OccurredUtc)
                .HasConversion(DateTimeOffsetToTicksConverter);

            _ = modelBuilder.Entity<AuditLedgerRecordEntity>()
                .Property(entity => entity.RecordedUtc)
                .HasConversion(DateTimeOffsetToTicksConverter);

            _ = modelBuilder.Entity<HandshakeAcknowledgmentEntity>()
                .Property(entity => entity.OccurredUtc)
                .HasConversion(DateTimeOffsetToTicksConverter);
        }
    }
}
