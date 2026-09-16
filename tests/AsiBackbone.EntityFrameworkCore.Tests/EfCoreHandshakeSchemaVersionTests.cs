using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Handshakes;
using AsiBackbone.Core.Serialization;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests;

/// <summary>
/// Integration tests for handshake schema version persistence.
/// </summary>
public sealed class EfCoreHandshakeSchemaVersionTests
{
    /// <summary>
    /// Verifies that EF Core handshake request persistence round-trips the schema version.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task HandshakeRequestRoundTripsSchemaVersion()
    {
        await using HostOwnedHandshakeDbContext context = CreateContext();

        _ = context.HandshakeRequests.Add(new HandshakeRequestEntity
        {
            HandshakeId = "handshake-123",
            SchemaVersion = "1.1-test",
            ActorId = "actor-123",
            ActorType = GovernanceActorType.Human,
            ActorDisplayName = "Test Actor",
            OperationName = "document.approve",
            ReasonCode = "ack.required",
            Message = "Acknowledgment is required.",
            RequiredAcknowledgmentCode = "ACK-001",
            RequiredAcknowledgmentText = "I understand this action is consequential.",
            RiskLevel = LiabilityHandshakeRiskLevel.High,
            RiskCategory = "administrative",
            CorrelationId = "correlation-123",
            TraceId = "trace-456",
            PolicyVersion = "v1",
            PolicyHash = "hash-abc"
        });

        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        HandshakeRequestEntity found = await context.HandshakeRequests
            .AsNoTracking()
            .SingleAsync(request => request.HandshakeId == "handshake-123", TestContext.Current.CancellationToken);

        Assert.Equal("1.1-test", found.SchemaVersion);
    }

    /// <summary>
    /// Verifies that EF Core handshake acknowledgment persistence round-trips the schema version.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task HandshakeAcknowledgmentRoundTripsSchemaVersion()
    {
        await using HostOwnedHandshakeDbContext context = CreateContext();

        _ = context.HandshakeAcknowledgments.Add(new HandshakeAcknowledgmentEntity
        {
            AcknowledgmentId = "ack-123",
            SchemaVersion = "1.1-test",
            HandshakeId = "handshake-123",
            ActorId = "actor-123",
            ActorType = GovernanceActorType.Human,
            ActorDisplayName = "Test Actor",
            AcknowledgmentCode = "ACK-001",
            Acknowledged = true,
            OccurredUtc = new DateTimeOffset(2026, 6, 1, 9, 0, 0, TimeSpan.Zero),
            CorrelationId = "correlation-123",
            TraceId = "trace-456"
        });

        _ = await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        HandshakeAcknowledgmentEntity found = await context.HandshakeAcknowledgments
            .AsNoTracking()
            .SingleAsync(acknowledgment => acknowledgment.AcknowledgmentId == "ack-123", TestContext.Current.CancellationToken);

        Assert.Equal("1.1-test", found.SchemaVersion);
    }

    /// <summary>
    /// Verifies that EF Core handshake entities default to the initial stable schema version.
    /// </summary>
    [Fact]
    public void HandshakeEntitiesDefaultToStableSchemaVersion()
    {
        Assert.Equal(
            GovernanceSchemaVersions.StableArtifactsV1,
            new HandshakeRequestEntity().SchemaVersion);

        Assert.Equal(
            GovernanceSchemaVersions.StableArtifactsV1,
            new HandshakeAcknowledgmentEntity().SchemaVersion);
    }

    private static HostOwnedHandshakeDbContext CreateContext()
    {
        DbContextOptions<HostOwnedHandshakeDbContext> options = new DbContextOptionsBuilder<HostOwnedHandshakeDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;

        return new HostOwnedHandshakeDbContext(options);
    }

    private sealed class HostOwnedHandshakeDbContext(DbContextOptions<HostOwnedHandshakeDbContext> options)
        : DbContext(options)
    {
        public DbSet<HandshakeRequestEntity> HandshakeRequests =>
            Set<HandshakeRequestEntity>();

        public DbSet<HandshakeAcknowledgmentEntity> HandshakeAcknowledgments =>
            Set<HandshakeAcknowledgmentEntity>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            _ = modelBuilder.ApplyAsiBackboneConfigurations();
        }
    }
}
