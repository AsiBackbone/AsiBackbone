using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace AsiBackbone.EntityFrameworkCore.Tests.Outbox;

internal static class EfCoreGovernanceOutboxTestHost
{
    public static async Task<SqliteConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    public static async Task<SqliteConnection> OpenSharedMemoryKeeperConnectionAsync(CancellationToken cancellationToken)
    {
        string databaseName = $"AsiBackboneIssue578_{Guid.NewGuid():N}";
        var connection = new SqliteConnection($"Data Source={databaseName};Mode=Memory;Cache=Shared;Default Timeout=30");
        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    public static DbContextOptions<GovernanceOutboxTestDbContext> CreateOptions(
        SqliteConnection connection,
        IInterceptor? interceptor = null)
    {
        ArgumentNullException.ThrowIfNull(connection);

        DbContextOptionsBuilder<GovernanceOutboxTestDbContext> builder = new DbContextOptionsBuilder<GovernanceOutboxTestDbContext>()
            .UseSqlite(connection);

        if (interceptor is not null)
        {
            _ = builder.AddInterceptors(interceptor);
        }

        return builder.Options;
    }

    public static DbContextOptions<GovernanceOutboxTestDbContext> CreateOptions(
        string connectionString,
        IInterceptor? interceptor = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        DbContextOptionsBuilder<GovernanceOutboxTestDbContext> builder = new DbContextOptionsBuilder<GovernanceOutboxTestDbContext>()
            .UseSqlite(connectionString);

        if (interceptor is not null)
        {
            _ = builder.AddInterceptors(interceptor);
        }

        return builder.Options;
    }

    public static async Task EnsureCreatedAsync(
        DbContextOptions<GovernanceOutboxTestDbContext> options,
        CancellationToken cancellationToken)
    {
        await using GovernanceOutboxTestDbContext context = new(options);
        _ = await context.Database.EnsureCreatedAsync(cancellationToken);
    }

    public static GovernanceEmissionEnvelope CreateEnvelope(
        string eventId,
        GovernanceEmissionPayload? payload = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);

        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.Outbox,
            eventId,
            new DateTimeOffset(2026, 7, 12, 11, 0, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{eventId}",
            createdUtc: new DateTimeOffset(2026, 7, 12, 11, 0, 1, TimeSpan.Zero),
            schemaVersion: "1.0.0",
            correlationId: $"correlation-{eventId}",
            auditResidueId: $"audit-{eventId}",
            lifecycleStage: DecisionReceiptLifecycleStage.ExternalEmissionQueued,
            policyVersion: "2026.07",
            policyHash: "policy-hash-issue-578",
            traceId: $"trace-{eventId}",
            spanId: $"span-{eventId}",
            parentSpanId: "parent-span-issue-578",
            operationName: "governance.emit",
            outcome: "Queued",
            actorId: "actor-issue-578",
            emitterStatus: "queued",
            emitterProvider: "efcore-outbox",
            outboxSequence: 77,
            gatewayExecutionId: "gateway-issue-578",
            decisionStage: "ExternalEmissionQueued",
            payload: payload,
            metadata: metadata);
    }
}

internal sealed class GovernanceOutboxTestDbContext(DbContextOptions<GovernanceOutboxTestDbContext> options)
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
