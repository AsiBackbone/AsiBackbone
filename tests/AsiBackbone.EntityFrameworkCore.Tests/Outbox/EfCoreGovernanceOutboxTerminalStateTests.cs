using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Outbox;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests.Outbox;

/// <summary>
/// Verifies that non-claim mutations preserve terminal outbox entries.
/// </summary>
public sealed class EfCoreGovernanceOutboxTerminalStateTests
{
    /// <summary>
    /// Verifies that stale saves and status transitions leave terminal rows unchanged.
    /// </summary>
    [Theory]
    [InlineData(false, "save")]
    [InlineData(false, "deliver")]
    [InlineData(false, "fail")]
    [InlineData(false, "deadletter")]
    [InlineData(true, "save")]
    [InlineData(true, "deliver")]
    [InlineData(true, "fail")]
    [InlineData(true, "deadletter")]
    public async Task NonClaimMutationPreservesTerminalEntry(bool deadLettered, string mutation)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection connection = await EfCoreGovernanceOutboxTestHost.OpenConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> options = EfCoreGovernanceOutboxTestHost.CreateOptions(connection);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(options, cancellationToken);

        GovernanceOutboxEntry snapshot;
        GovernanceOutboxEntry terminal;
        string concurrencyStamp;
        await using (GovernanceOutboxTestDbContext writerContext = new(options))
        {
            var writer = new EfCoreGovernanceOutboxStore(writerContext);
            GovernanceOutboxEntry queued = await writer.EnqueueAsync(
                EfCoreGovernanceOutboxTestHost.CreateEnvelope("terminal-state"), cancellationToken);
            snapshot = Assert.IsType<GovernanceOutboxEntry>(
                await writer.FindByOutboxEntryIdAsync(queued.OutboxEntryId, cancellationToken));
            terminal = deadLettered
                ? await writer.MarkDeadLetteredAsync(snapshot.OutboxEntryId,
                    GovernanceEmissionError.Create("terminal.error", "Permanent failure."), "Terminal reason", cancellationToken)
                : await writer.MarkDeliveredAsync(snapshot.OutboxEntryId,
                    GovernanceEmissionResult.Delivered("original-provider", "original-record"), cancellationToken);
            concurrencyStamp = (await writerContext.GovernanceOutboxEntries.SingleAsync(cancellationToken)).ConcurrencyStamp;
        }

        await using (GovernanceOutboxTestDbContext mutationContext = new(options))
        {
            var store = new EfCoreGovernanceOutboxStore(mutationContext);
            var error = GovernanceEmissionError.Create("late.error", "Late failure.", isRetryable: true);
            GovernanceOutboxEntry result = mutation switch
            {
                "save" => await store.SaveAsync(snapshot.MarkDeferred(nextRetryUtc: DateTimeOffset.UtcNow.AddMinutes(1)), cancellationToken),
                "deliver" => await store.MarkDeliveredAsync(snapshot.OutboxEntryId,
                    GovernanceEmissionResult.Delivered("late-provider", "late-record"), cancellationToken),
                "fail" => await store.MarkFailedAsync(snapshot.OutboxEntryId, error, cancellationToken: cancellationToken),
                "deadletter" => await store.MarkDeadLetteredAsync(snapshot.OutboxEntryId, error, "Late reason", cancellationToken),
                _ => throw new InvalidOperationException("Unknown mutation.")
            };

            Assert.Equivalent(terminal, result);
            Assert.False(mutationContext.ChangeTracker.HasChanges());
        }

        await using GovernanceOutboxTestDbContext verificationContext = new(options);
        var verificationStore = new EfCoreGovernanceOutboxStore(verificationContext);
        GovernanceOutboxEntry persisted = Assert.IsType<GovernanceOutboxEntry>(
            await verificationStore.FindByOutboxEntryIdAsync(snapshot.OutboxEntryId, cancellationToken));
        Assert.Equivalent(terminal, persisted);
        Assert.Equal(concurrencyStamp,
            (await verificationContext.GovernanceOutboxEntries.SingleAsync(cancellationToken)).ConcurrencyStamp);
        Assert.Empty(await verificationStore.FindPendingAsync(cancellationToken: cancellationToken));
        Assert.Empty(await verificationStore.FindRetryReadyAsync(DateTimeOffset.UtcNow.AddHours(1), cancellationToken: cancellationToken));
    }
}
