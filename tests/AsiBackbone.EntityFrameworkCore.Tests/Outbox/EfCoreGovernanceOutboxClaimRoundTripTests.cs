using System.Data.Common;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Outbox;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests.Outbox;

/// <summary>
/// Relational integration coverage for the database-command shape of the portable EF Core outbox claim path.
/// </summary>
public sealed class EfCoreGovernanceOutboxClaimRoundTripTests
{
    /// <summary>
    /// Verifies an uncontended batch performs one set-based update plus one token-based read regardless of batch size.
    /// </summary>
    /// <param name="batchSize">The requested number of pending claims.</param>
    /// <param name="expectedCommandCount">The expected number of relational database commands.</param>
    [Theory]
    [InlineData(1, 2)]
    [InlineData(10, 2)]
    [InlineData(50, 2)]
    [InlineData(100, 2)]
    public async Task ClaimPendingUsesExpectedPortableRoundTripShape(int batchSize, int expectedCommandCount)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using var connection = new SqliteConnection("Filename=:memory:");
        await connection.OpenAsync(cancellationToken);

        var interceptor = new CommandCountingInterceptor();
        DbContextOptions<RoundTripDbContext> options = new DbContextOptionsBuilder<RoundTripDbContext>()
            .UseSqlite(connection)
            .AddInterceptors(interceptor)
            .Options;

        await using (var initializationContext = new RoundTripDbContext(options))
        {
            _ = await initializationContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        DateTimeOffset createdUtc = new(2026, 7, 13, 10, 0, 0, TimeSpan.Zero);
        string[] expectedIds = new string[batchSize];

        await using (var seedContext = new RoundTripDbContext(options))
        {
            var seedStore = new EfCoreGovernanceOutboxStore(seedContext);

            for (int index = 0; index < batchSize; index++)
            {
                string suffix = $"{index:D4}";
                string outboxEntryId = $"round-trip-entry-{suffix}";
                expectedIds[index] = outboxEntryId;
                var envelope = GovernanceEmissionEnvelope.Create(
                    GovernanceEmissionEventType.Outbox,
                    $"round-trip-event-{suffix}",
                    createdUtc.AddTicks(index),
                    envelopeId: $"round-trip-envelope-{suffix}",
                    correlationId: "efcore-outbox-claim-round-trip",
                    emitterStatus: GovernanceEmissionStatus.Pending.ToString(),
                    emitterProvider: "efcore-outbox");
                var entry = GovernanceOutboxEntry.Create(
                    envelope,
                    outboxEntryId,
                    createdUtc.AddTicks(index));

                _ = await seedStore.SaveAsync(entry, cancellationToken);
            }
        }

        interceptor.Reset();
        DateTimeOffset claimUtc = createdUtc.AddHours(1);

        await using var claimContext = new RoundTripDbContext(options);
        var claimStore = new EfCoreGovernanceOutboxStore(claimContext);
        IReadOnlyList<GovernanceOutboxClaim> claims = await claimStore.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create(
                "round-trip-worker",
                claimUtc,
                TimeSpan.FromMinutes(5),
                maxCount: batchSize),
            cancellationToken);

        Assert.Equal(batchSize, claims.Count);
        Assert.Equal(expectedCommandCount, interceptor.CommandCount);
        Assert.Equal(expectedIds, claims.Select(claim => claim.OutboxEntryId));
        _ = Assert.Single(claims.Select(claim => claim.ClaimToken).Distinct(StringComparer.Ordinal));
        Assert.All(claims, claim =>
        {
            Assert.Equal("round-trip-worker", claim.WorkerId);
            Assert.Equal(1, claim.Entry.ClaimAttemptCount);
            Assert.Equal(claimUtc, claim.ClaimedUtc);
            Assert.Equal(claimUtc.AddMinutes(5), claim.ClaimExpiresUtc);
        });
    }

    private sealed class RoundTripDbContext(DbContextOptions<RoundTripDbContext> options)
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

    private sealed class CommandCountingInterceptor : DbCommandInterceptor
    {
        private int commandCount;

        public int CommandCount => Volatile.Read(ref commandCount);

        public void Reset()
        {
            _ = Interlocked.Exchange(ref commandCount, 0);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            _ = command;
            _ = eventData;
            _ = cancellationToken;
            _ = Interlocked.Increment(ref commandCount);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            _ = command;
            _ = eventData;
            _ = cancellationToken;
            _ = Interlocked.Increment(ref commandCount);
            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<object> result,
            CancellationToken cancellationToken = default)
        {
            _ = command;
            _ = eventData;
            _ = cancellationToken;
            _ = Interlocked.Increment(ref commandCount);
            return ValueTask.FromResult(result);
        }
    }
}
