using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Storage.InMemory.Outbox;
using Xunit;

namespace AsiBackbone.Core.Tests.Storage;

/// <summary>
/// Branch coverage tests for <see cref="InMemoryGovernanceOutboxStore"/> guard, missing-entry, and no-change paths.
/// </summary>
public sealed class InMemoryGovernanceOutboxStoreBranchTests
{
    private static readonly DateTimeOffset ClaimUtc = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that saving an entry the store has not seen adds it as a new entry.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SaveAsyncAddsEntryThatIsNotYetStored()
    {
        var store = new InMemoryGovernanceOutboxStore();
        var entry = GovernanceOutboxEntry.Create(CreateEnvelope("event-1"), outboxEntryId: "outbox-1");

        GovernanceOutboxEntry saved = await store.SaveAsync(entry, TestContext.Current.CancellationToken);
        GovernanceOutboxEntry? stored = await store.FindByOutboxEntryIdAsync("outbox-1", TestContext.Current.CancellationToken);

        Assert.Same(entry, saved);
        Assert.Same(entry, stored);
    }

    /// <summary>
    /// Verifies that a claim cannot be saved against an entry with a different outbox entry identifier.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task SaveClaimAsyncRejectsEntryForDifferentClaim()
    {
        var store = new InMemoryGovernanceOutboxStore();
        _ = await store.EnqueueAsync(CreateEnvelope("event-1"), TestContext.Current.CancellationToken);
        GovernanceOutboxClaim claim = Assert.Single(await store.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create("worker-a", ClaimUtc, TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken));
        var mismatched = GovernanceOutboxEntry.Create(CreateEnvelope("event-2"), outboxEntryId: "other-entry");

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () => await store.SaveClaimAsync(
            claim,
            mismatched,
            TestContext.Current.CancellationToken));

        Assert.Equal("entry", exception.ParamName);
    }

    /// <summary>
    /// Verifies that marking a missing entry delivered reports the missing outbox entry.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task MarkDeliveredAsyncRejectsMissingEntry()
    {
        var store = new InMemoryGovernanceOutboxStore();

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.MarkDeliveredAsync(
            " missing-entry ",
            GovernanceEmissionResult.Delivered("test-provider"),
            TestContext.Current.CancellationToken));

        Assert.Contains("missing-entry", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that marking an already delivered entry delivered again returns the stored terminal entry unchanged.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task MarkDeliveredAsyncReturnsTerminalEntryUnchanged()
    {
        var store = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await store.EnqueueAsync(CreateEnvelope("event-1"), TestContext.Current.CancellationToken);
        GovernanceOutboxEntry delivered = await store.MarkDeliveredAsync(
            entry.OutboxEntryId,
            GovernanceEmissionResult.Delivered("test-provider"),
            TestContext.Current.CancellationToken);

        GovernanceOutboxEntry repeated = await store.MarkDeliveredAsync(
            entry.OutboxEntryId,
            GovernanceEmissionResult.Delivered("other-provider"),
            TestContext.Current.CancellationToken);

        Assert.True(delivered.IsDelivered);
        Assert.Same(delivered, repeated);
    }

    /// <summary>
    /// Verifies that claim completion reports a claim whose entry is not present in this store.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task MarkClaimDeliveredAsyncRejectsClaimForMissingEntry()
    {
        var claimingStore = new InMemoryGovernanceOutboxStore();
        _ = await claimingStore.EnqueueAsync(CreateEnvelope("event-1"), TestContext.Current.CancellationToken);
        GovernanceOutboxClaim claim = Assert.Single(await claimingStore.ClaimPendingAsync(
            GovernanceOutboxClaimRequest.Create("worker-a", ClaimUtc, TimeSpan.FromMinutes(5)),
            TestContext.Current.CancellationToken));
        var otherStore = new InMemoryGovernanceOutboxStore();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () => await otherStore.MarkClaimDeliveredAsync(
            claim,
            GovernanceEmissionResult.Delivered("test-provider"),
            TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that pending and retry-ready queries reject a non-positive maximum count.
    /// </summary>
    /// <param name="maxCount">The invalid maximum count.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task QueriesRejectNonPositiveMaxCount(int maxCount)
    {
        var store = new InMemoryGovernanceOutboxStore();

        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await store.FindPendingAsync(
            maxCount,
            TestContext.Current.CancellationToken));
        _ = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () => await store.FindRetryReadyAsync(
            ClaimUtc,
            maxCount,
            TestContext.Current.CancellationToken));
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(string eventId)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.DecisionReceipt,
            eventId: eventId,
            occurredUtc: new DateTimeOffset(2026, 6, 15, 14, 0, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{eventId}",
            correlationId: "correlation-1",
            decisionReceiptId: $"residue-{eventId}",
            traceId: $"trace-{eventId}",
            operationName: "governance.emit",
            emitterStatus: "pending",
            emitterProvider: "outbox");
    }
}
