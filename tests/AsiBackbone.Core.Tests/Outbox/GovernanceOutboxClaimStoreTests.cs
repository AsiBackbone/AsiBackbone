using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Storage.InMemory.Outbox;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Tests for the <see cref="InMemoryGovernanceOutboxStore"/> class, focusing on the behavior of claiming pending outbox entries and ensuring that active leases are respected.
/// </summary>
public sealed class GovernanceOutboxClaimStoreTests
{
    /// <summary>
    /// Tests that the <see cref="InMemoryGovernanceOutboxStore.ClaimPendingAsync"/> method correctly skips entries that are already claimed by another worker, ensuring that active leases are respected and not overwritten.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ClaimPendingAsyncSkipsActiveLease()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope("event-1", "correlation-1"),
            TestContext.Current.CancellationToken);
        DateTimeOffset claimUtc = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

        IReadOnlyList<GovernanceOutboxClaim> firstClaims = await outboxStore.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create("worker-a", claimUtc, TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxClaim> secondClaims = await outboxStore.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create("worker-b", claimUtc.AddMinutes(1), TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken);
        GovernanceOutboxEntry? storedEntry = await outboxStore.FindByOutboxEntryIdAsync(
            entry.OutboxEntryId,
            TestContext.Current.CancellationToken);

        GovernanceOutboxClaim claim = Assert.Single(firstClaims);
        Assert.Equal(entry.OutboxEntryId, claim.OutboxEntryId);
        Assert.Empty(secondClaims);
        Assert.NotNull(storedEntry);
        Assert.True(storedEntry.HasClaim);
        Assert.Equal(1, storedEntry.ClaimAttemptCount);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(string eventId, string correlationId)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.DecisionReceipt,
            eventId: eventId,
            occurredUtc: new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{eventId}",
            correlationId: correlationId,
            decisionReceiptId: $"residue-{eventId}",
            traceId: $"trace-{eventId}",
            operationName: "governance.emit",
            emitterStatus: "pending",
            emitterProvider: "outbox");
    }
}
