using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Storage.InMemory.Outbox;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Smoke tests for the provider-neutral outbox drain path before real provider implementation.
/// </summary>
public sealed class GovernanceOutboxDrainSmokeTests
{
    /// <summary>
    /// Smoke test for the no-op governance emitter that returns a delivered result without an external provider.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task NoOpGovernanceEmitterReturnsDeliveredResultWithoutExternalProvider()
    {
        GovernanceEmissionEnvelope envelope = CreateEnvelope();

        GovernanceEmissionResult result = await NoOpGovernanceEmitter.Instance.EmitAsync(
            envelope,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(GovernanceEmissionStatus.Delivered, result.Status);
        Assert.Equal(NoOpGovernanceEmitter.ProviderName, result.ProviderName);
        Assert.Equal(envelope.EnvelopeId, result.ProviderRecordId);
        Assert.Equal("noop", result.Metadata["emitter.kind"]);
        Assert.Equal("outbox-drain-validation", result.Metadata["emitter.purpose"]);
    }

    /// <summary>
    /// Smoke test for the outbox drain that enqueues a pending entry, drains it to the no-op sink, and marks it as delivered.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncHandsPendingEntryToNoOpSinkAndMarksDelivered()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        var drain = new GovernanceOutboxDrain(outboxStore, NoOpGovernanceEmitter.Instance);
        GovernanceEmissionEnvelope envelope = CreateEnvelope();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            envelope,
            TestContext.Current.CancellationToken);

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            utcNow: new DateTimeOffset(2026, 6, 15, 16, 0, 0, TimeSpan.Zero),
            cancellationToken: TestContext.Current.CancellationToken);
        GovernanceOutboxEntry? storedEntry = await outboxStore.FindByOutboxEntryIdAsync(
            entry.OutboxEntryId,
            TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> pendingEntries = await outboxStore.FindPendingAsync(
            cancellationToken: TestContext.Current.CancellationToken);

        GovernanceOutboxEntry deliveredEntry = Assert.Single(drainedEntries);
        Assert.NotNull(storedEntry);
        Assert.True(deliveredEntry.IsDelivered);
        Assert.Equal(GovernanceEmissionStatus.Delivered, storedEntry.Status);
        Assert.Equal(NoOpGovernanceEmitter.ProviderName, deliveredEntry.ProviderName);
        Assert.Equal(envelope.EnvelopeId, deliveredEntry.ProviderRecordId);
        Assert.Equal("correlation-193", deliveredEntry.Envelope.CorrelationId);
        Assert.Equal("residue-193", deliveredEntry.Envelope.AuditResidueId);
        Assert.Equal(DecisionReceiptLifecycleStage.ExternalEmissionQueued, deliveredEntry.Envelope.LifecycleStage);
        Assert.Equal(envelope.SchemaVersion, deliveredEntry.Envelope.SchemaVersion);
        Assert.Equal("noop", deliveredEntry.Metadata["emitter.kind"]);
        Assert.DoesNotContain(pendingEntries, pendingEntry => pendingEntry.OutboxEntryId == entry.OutboxEntryId);
    }

    /// <summary>
    /// Smoke test for the outbox drain that enqueues a pending entry, drains it to a retryable failure result, and ensures the local outbox record is not lost and is retried after the specified retry time.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncRetryableFailureDoesNotLoseLocalOutboxRecord()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        DateTimeOffset retryUtc = new(2026, 6, 15, 16, 5, 0, TimeSpan.Zero);
        var drain = new GovernanceOutboxDrain(
            outboxStore,
            new ResultEmitter(GovernanceEmissionResult.RetryableFailure(
                GovernanceEmissionError.Create(
                    "provider.unavailable",
                    "Provider was unavailable.",
                    isRetryable: true,
                    providerName: "test-sink"),
                retryUtc)));
        GovernanceEmissionEnvelope envelope = CreateEnvelope();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(
            envelope,
            TestContext.Current.CancellationToken);

        IReadOnlyList<GovernanceOutboxEntry> drainedEntries = await drain.DrainAsync(
            utcNow: new DateTimeOffset(2026, 6, 15, 16, 0, 0, TimeSpan.Zero),
            cancellationToken: TestContext.Current.CancellationToken);
        GovernanceOutboxEntry? storedEntry = await outboxStore.FindByOutboxEntryIdAsync(
            entry.OutboxEntryId,
            TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> retryReadyBefore = await outboxStore.FindRetryReadyAsync(
            retryUtc.AddTicks(-1),
            cancellationToken: TestContext.Current.CancellationToken);
        IReadOnlyList<GovernanceOutboxEntry> retryReadyAfter = await outboxStore.FindRetryReadyAsync(
            retryUtc,
            cancellationToken: TestContext.Current.CancellationToken);

        GovernanceOutboxEntry failedEntry = Assert.Single(drainedEntries);
        Assert.NotNull(storedEntry);
        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, failedEntry.Status);
        Assert.Equal(1, failedEntry.RetryCount);
        Assert.Equal("test-sink", failedEntry.ProviderName);
        Assert.Equal("provider.unavailable", failedEntry.LastError?.Code);
        Assert.Equal("correlation-193", storedEntry.Envelope.CorrelationId);
        Assert.Equal("residue-193", storedEntry.Envelope.AuditResidueId);
        Assert.DoesNotContain(retryReadyBefore, retryReadyEntry => retryReadyEntry.OutboxEntryId == entry.OutboxEntryId);
        Assert.Contains(retryReadyAfter, retryReadyEntry => retryReadyEntry.OutboxEntryId == entry.OutboxEntryId);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope()
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditLifecycle,
            eventId: "event-193",
            occurredUtc: new DateTimeOffset(2026, 6, 15, 15, 59, 0, TimeSpan.Zero),
            envelopeId: "envelope-193",
            correlationId: "correlation-193",
            auditResidueId: "residue-193",
            lifecycleStage: DecisionReceiptLifecycleStage.ExternalEmissionQueued,
            policyVersion: "v1",
            policyHash: "hash-193",
            traceId: "trace-193",
            spanId: "span-193",
            parentSpanId: "parent-span-193",
            operationName: "governance.emit",
            outcome: "Queued",
            emitterStatus: "pending",
            emitterProvider: "outbox",
            outboxSequence: 193,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["purpose"] = "outbox-drain-smoke"
            });
    }

    private sealed class ResultEmitter(GovernanceEmissionResult result) : IGovernanceEmitter
    {
        private readonly GovernanceEmissionResult result = result;

        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(result);
        }
    }
}
