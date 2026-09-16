using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Storage.InMemory.Outbox;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Tests for the <see cref="GovernanceOutboxDrain"/> class when claim leases are enabled and a claim store is used.
/// </summary>
public sealed class GovernanceOutboxClaimDrainTests
{
    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxDrain.DrainAsync"/> method throws an <see cref="InvalidOperationException"/> when claim leases are enabled but no claim store is provided.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncThrowsWhenClaimLeasesEnabledWithoutClaimStore()
    {
        var drain = new GovernanceOutboxDrain(
            new SelectionOnlyOutboxStore(),
            new DeliveredEmitter(),
            outboxOptions: Options.Create(new GovernanceOutboxOptions
            {
                UseClaimLeases = true,
                ClaimWorkerId = "worker-1"
            }));

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await drain.DrainAsync(cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxDrain.DrainAsync"/> method drains a pending entry and marks it as delivered when claim leases are enabled and a claim store is used.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncClaimsPendingEntryBeforeDeliveryWhenClaimLeasesEnabled()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope("event-1"),
            TestContext.Current.CancellationToken);
        GovernanceOutboxDrain drain = CreateClaimDrain(outboxStore, new DeliveredEmitter());

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
            maxCount: 10,
            cancellationToken: TestContext.Current.CancellationToken);
        GovernanceOutboxEntry? storedEntry = await outboxStore.FindByOutboxEntryIdAsync(
            entry.OutboxEntryId,
            TestContext.Current.CancellationToken);

        GovernanceOutboxEntry drainedEntry = Assert.Single(drainedEntries);
        Assert.True(drainedEntry.IsDelivered);
        Assert.False(drainedEntry.HasClaim);
        Assert.NotNull(storedEntry);
        Assert.True(storedEntry.IsDelivered);
        Assert.False(storedEntry.HasClaim);
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxDrain.DrainAsync"/> method returns an empty list when the claim store has no eligible entries to drain.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncReturnsEmptyWhenClaimStoreHasNoEligibleEntries()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxDrain drain = CreateClaimDrain(outboxStore, new DeliveredEmitter());

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(drainedEntries);
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxDrain.DrainAsync"/> method drains a retry-ready entry and marks it as delivered when claim leases are enabled and a claim store is used, even if there are no pending entries to claim.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncUsesRetryReadyClaimsWhenNoPendingEntriesAreClaimed()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope("event-1"),
            TestContext.Current.CancellationToken);
        DateTimeOffset drainUtc = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        _ = await outboxStore.MarkFailedAsync(
            entry.OutboxEntryId,
            GovernanceEmissionError.Create("provider.transient", "Transient provider failure.", isRetryable: true),
            drainUtc.AddMinutes(-1),
            TestContext.Current.CancellationToken);
        GovernanceOutboxDrain drain = CreateClaimDrain(outboxStore, new DeliveredEmitter());

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            drainUtc,
            maxCount: 10,
            cancellationToken: TestContext.Current.CancellationToken);

        GovernanceOutboxEntry drainedEntry = Assert.Single(drainedEntries);
        Assert.True(drainedEntry.IsDelivered);
        Assert.Equal("record-event-1", drainedEntry.ProviderRecordId);
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxDrain.DrainAsync"/> method stops draining after reaching the specified maximum count of pending claims, even if there are retry-ready entries available.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncStopsAfterPendingClaimLimitBeforeRetryReadyLookup()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        _ = await outboxStore.EnqueueAsync(CreateEnvelope("event-1"), TestContext.Current.CancellationToken);
        _ = await outboxStore.EnqueueAsync(CreateEnvelope("event-2"), TestContext.Current.CancellationToken);
        GovernanceOutboxDrain drain = CreateClaimDrain(outboxStore, new DeliveredEmitter());

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
            maxCount: 1,
            cancellationToken: TestContext.Current.CancellationToken);

        GovernanceOutboxEntry drainedEntry = Assert.Single(drainedEntries);
        Assert.True(drainedEntry.IsDelivered);
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxDrain.DrainAsync"/> method saves a deferred result and clears the claim when claim leases are enabled and a claim store is used.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncSavesDeferredResultAndClearsClaim()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            CreateEnvelope("event-1"),
            TestContext.Current.CancellationToken);
        DateTimeOffset retryAfterUtc = new(2026, 7, 8, 12, 10, 0, TimeSpan.Zero);
        GovernanceOutboxDrain drain = CreateClaimDrain(outboxStore, new ResultEmitter(GovernanceEmissionResult.Deferred(
            GovernanceEmissionError.Create("provider.deferred", "Provider deferred the emission.", isRetryable: true),
            retryAfterUtc,
            "test-provider")));

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
            cancellationToken: TestContext.Current.CancellationToken);
        GovernanceOutboxEntry? storedEntry = await outboxStore.FindByOutboxEntryIdAsync(
            entry.OutboxEntryId,
            TestContext.Current.CancellationToken);

        GovernanceOutboxEntry drainedEntry = Assert.Single(drainedEntries);
        Assert.Equal(GovernanceEmissionStatus.Deferred, drainedEntry.Status);
        Assert.Equal(retryAfterUtc, drainedEntry.NextRetryUtc);
        Assert.False(drainedEntry.HasClaim);
        Assert.NotNull(storedEntry);
        Assert.False(storedEntry.HasClaim);
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxDrain.DrainAsync"/> method saves a pending result as deferred with the default retry interval when claim leases are enabled and a claim store is used.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncSavesPendingResultAsDeferredWithDefaultRetry()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        _ = await outboxStore.EnqueueAsync(CreateEnvelope("event-1"), TestContext.Current.CancellationToken);
        DateTimeOffset drainUtc = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        GovernanceOutboxDrain drain = CreateClaimDrain(outboxStore, new ResultEmitter(GovernanceEmissionResult.Pending("test-provider")));

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            drainUtc,
            cancellationToken: TestContext.Current.CancellationToken);

        GovernanceOutboxEntry drainedEntry = Assert.Single(drainedEntries);
        Assert.Equal(GovernanceEmissionStatus.Deferred, drainedEntry.Status);
        Assert.Equal(drainUtc.AddMinutes(1), drainedEntry.NextRetryUtc);
        Assert.NotNull(drainedEntry.LastError);
        Assert.Equal("emission.pending", drainedEntry.LastError.Code);
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxDrain.DrainAsync"/> method marks a claim as dead-lettered when the emitter returns a dead-letter result, and clears the claim from the entry.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncMarksClaimDeadLetteredForDeadLetterResult()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        _ = await outboxStore.EnqueueAsync(CreateEnvelope("event-1"), TestContext.Current.CancellationToken);
        var error = GovernanceEmissionError.Create("provider.deadletter", "Provider rejected the emission.", providerName: "test-provider");
        GovernanceOutboxDrain drain = CreateClaimDrain(outboxStore, new ResultEmitter(GovernanceEmissionResult.DeadLettered(error)));

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
            cancellationToken: TestContext.Current.CancellationToken);

        GovernanceOutboxEntry drainedEntry = Assert.Single(drainedEntries);
        Assert.True(drainedEntry.IsDeadLettered);
        Assert.Equal("Provider rejected the emission.", drainedEntry.DeadLetterReason);
        Assert.False(drainedEntry.HasClaim);
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxDrain.DrainAsync"/> method marks a claim as failed when the emitter returns a failed result, and clears the claim from the entry.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncMarksClaimFailedForFailedResult()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        _ = await outboxStore.EnqueueAsync(CreateEnvelope("event-1"), TestContext.Current.CancellationToken);
        var error = GovernanceEmissionError.Create("provider.failed", "Provider failed the emission.", isRetryable: false, providerName: "test-provider");
        GovernanceOutboxDrain drain = CreateClaimDrain(outboxStore, new ResultEmitter(GovernanceEmissionResult.Failed(error)));

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
            cancellationToken: TestContext.Current.CancellationToken);

        GovernanceOutboxEntry drainedEntry = Assert.Single(drainedEntries);
        Assert.Equal(GovernanceEmissionStatus.Failed, drainedEntry.Status);
        Assert.Equal("provider.failed", drainedEntry.LastError?.Code);
        Assert.False(drainedEntry.HasClaim);
    }

    /// <summary>
    /// Tests that the <see cref="GovernanceOutboxDrain.DrainAsync"/> method marks a claim as failed when the emitter throws an exception, and clears the claim from the entry.
    /// </summary>
    /// <returns>
    /// A <see cref="Task"/> representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncMarksClaimFailedWhenEmitterThrows()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        _ = await outboxStore.EnqueueAsync(CreateEnvelope("event-1"), TestContext.Current.CancellationToken);
        DateTimeOffset drainUtc = new(2026, 7, 8, 12, 0, 0, TimeSpan.Zero);
        GovernanceOutboxDrain drain = CreateClaimDrain(outboxStore, new ThrowingEmitter());

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            drainUtc,
            cancellationToken: TestContext.Current.CancellationToken);

        GovernanceOutboxEntry drainedEntry = Assert.Single(drainedEntries);
        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, drainedEntry.Status);
        Assert.Equal("emission.exception", drainedEntry.LastError?.Code);
        Assert.Equal(drainUtc.AddMinutes(1), drainedEntry.NextRetryUtc);
        Assert.False(drainedEntry.HasClaim);
    }

    private static GovernanceOutboxDrain CreateClaimDrain(
        InMemoryGovernanceOutboxStore outboxStore,
        IGovernanceEmitter emitter)
    {
        return new GovernanceOutboxDrain(
            outboxStore,
            emitter,
            outboxOptions: Options.Create(new GovernanceOutboxOptions
            {
                UseClaimLeases = true,
                ClaimWorkerId = "worker-1",
                ClaimLeaseDuration = TimeSpan.FromMinutes(5)
            }));
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(string eventId)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditResidue,
            eventId: eventId,
            occurredUtc: new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{eventId}");
    }

    private sealed class DeliveredEmitter : IGovernanceEmitter
    {
        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceEmissionResult.Delivered("test-provider", $"record-{envelope.EventId}"));
        }
    }

    private sealed class ResultEmitter(GovernanceEmissionResult result) : IGovernanceEmitter
    {
        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class ThrowingEmitter : IGovernanceEmitter
    {
        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Emitter failed.");
        }
    }

    private sealed class SelectionOnlyOutboxStore : IGovernanceOutboxStore
    {
        public ValueTask<GovernanceOutboxEntry> EnqueueAsync(GovernanceEmissionEnvelope envelope, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceOutboxEntry.Create(envelope));
        }

        public ValueTask<GovernanceOutboxEntry> SaveAsync(GovernanceOutboxEntry entry, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(entry);
        }

        public ValueTask<GovernanceOutboxEntry?> FindByOutboxEntryIdAsync(string outboxEntryId, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<GovernanceOutboxEntry?>(null);
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindPendingAsync(int maxCount = 100, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxEntry>>(Array.Empty<GovernanceOutboxEntry>());
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindRetryReadyAsync(DateTimeOffset utcNow, int maxCount = 100, CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxEntry>>(Array.Empty<GovernanceOutboxEntry>());
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeliveredAsync(string outboxEntryId, GovernanceEmissionResult result, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> MarkFailedAsync(string outboxEntryId, GovernanceEmissionError governanceEmissionError, DateTimeOffset? nextRetryUtc = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeadLetteredAsync(string outboxEntryId, GovernanceEmissionError governanceEmissionError, string? deadLetterReason = null, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
