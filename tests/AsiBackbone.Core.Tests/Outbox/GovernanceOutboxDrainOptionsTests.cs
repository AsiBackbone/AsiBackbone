using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Storage.InMemory.Outbox;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Tests for <see cref="GovernanceOutboxDrain"/> that verify the behavior of the drain operation with respect to configured retry and deferred delays.
/// </summary>
public sealed class GovernanceOutboxDrainOptionsTests
{
    private static readonly DateTimeOffset DrainUtc = new(2026, 6, 29, 14, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that when the emitter throws an exception during the drain operation, the next retry time for the outbox entry is set according to the configured retry delay in <see cref="GovernanceOutboxOptions"/>.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous test operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncUsesConfiguredRetryDelayForEmitterExceptions()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry queuedEntry = await outboxStore.EnqueueAsync(
            CreateEnvelope("exception"),
            TestContext.Current.CancellationToken);
        IOptions<GovernanceOutboxOptions> options = Options.Create(new GovernanceOutboxOptions
        {
            RetryDelay = TimeSpan.FromMinutes(7),
            DeferredDelay = TimeSpan.FromMinutes(13)
        });
        var drain = new GovernanceOutboxDrain(
            outboxStore,
            new ThrowingEmitter(new InvalidOperationException("provider unavailable")),
            outboxOptions: options);

        GovernanceOutboxEntry drained = Assert.Single(await drain.DrainAsync(
            DrainUtc,
            cancellationToken: TestContext.Current.CancellationToken));
        GovernanceOutboxEntry? storedEntry = await outboxStore.FindByOutboxEntryIdAsync(
            queuedEntry.OutboxEntryId,
            TestContext.Current.CancellationToken);

        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, drained.Status);
        Assert.NotNull(storedEntry);
        Assert.Equal(DrainUtc.AddMinutes(7), drained.NextRetryUtc);
        Assert.Equal(DrainUtc.AddMinutes(7), storedEntry.NextRetryUtc);
    }

    /// <summary>
    /// Verifies that when the emitter returns a deferred result without a specified retry-after time, the next retry time for the outbox entry is set according to the configured deferred delay in <see cref="GovernanceOutboxOptions"/>.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous test operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncUsesConfiguredDeferredDelayWhenDeferredResultHasNoRetryAfter()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        _ = await outboxStore.EnqueueAsync(
            CreateEnvelope("deferred"),
            TestContext.Current.CancellationToken);
        IOptions<GovernanceOutboxOptions> options = Options.Create(new GovernanceOutboxOptions
        {
            RetryDelay = TimeSpan.FromMinutes(7),
            DeferredDelay = TimeSpan.FromMinutes(13)
        });
        var drain = new GovernanceOutboxDrain(
            outboxStore,
            new ResultEmitter(GovernanceEmissionResult.Deferred()),
            outboxOptions: options);

        GovernanceOutboxEntry drained = Assert.Single(await drain.DrainAsync(
            DrainUtc,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(GovernanceEmissionStatus.Deferred, drained.Status);
        Assert.Equal(DrainUtc.AddMinutes(13), drained.NextRetryUtc);
    }

    /// <summary>
    /// Verifies that when the emitter returns a deferred result with a specified retry-after time, the next retry time for the outbox entry is set to that retry-after time, even if it exceeds the configured deferred delay in <see cref="GovernanceOutboxOptions"/>.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous test operation.
    /// </returns>
    [Fact]
    public async Task DrainAsyncPreservesEmitterRetryAfterOverConfiguredDeferredDelay()
    {
        var outboxStore = new InMemoryGovernanceOutboxStore();
        _ = await outboxStore.EnqueueAsync(
            CreateEnvelope("retry-after"),
            TestContext.Current.CancellationToken);
        DateTimeOffset retryAfterUtc = DrainUtc.AddMinutes(3);
        IOptions<GovernanceOutboxOptions> options = Options.Create(new GovernanceOutboxOptions
        {
            RetryDelay = TimeSpan.FromMinutes(7),
            DeferredDelay = TimeSpan.FromMinutes(13)
        });
        var drain = new GovernanceOutboxDrain(
            outboxStore,
            new ResultEmitter(GovernanceEmissionResult.Deferred(retryAfterUtc: retryAfterUtc)),
            outboxOptions: options);

        GovernanceOutboxEntry drained = Assert.Single(await drain.DrainAsync(
            DrainUtc,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(GovernanceEmissionStatus.Deferred, drained.Status);
        Assert.Equal(retryAfterUtc, drained.NextRetryUtc);
    }

    /// <summary>
    /// Verifies that the constructor of <see cref="GovernanceOutboxDrain"/> throws an <see cref="InvalidOperationException"/> when provided with options that have a negative retry delay.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNegativeRetryDelayOptions()
    {
        IOptions<GovernanceOutboxOptions> options = Options.Create(new GovernanceOutboxOptions
        {
            RetryDelay = TimeSpan.FromTicks(-1)
        });

        _ = Assert.Throws<InvalidOperationException>(() => new GovernanceOutboxDrain(
            new InMemoryGovernanceOutboxStore(),
            NoOpGovernanceEmitter.Instance,
            outboxOptions: options));
    }

    /// <summary>
    /// Verifies that the constructor of <see cref="GovernanceOutboxDrain"/> throws an <see cref="InvalidOperationException"/> when provided with options that have a negative deferred delay.
    /// </summary>  
    [Fact]
    public void ConstructorRejectsNegativeDeferredDelayOptions()
    {
        IOptions<GovernanceOutboxOptions> options = Options.Create(new GovernanceOutboxOptions
        {
            DeferredDelay = TimeSpan.FromTicks(-1)
        });

        _ = Assert.Throws<InvalidOperationException>(() => new GovernanceOutboxDrain(
            new InMemoryGovernanceOutboxStore(),
            NoOpGovernanceEmitter.Instance,
            outboxOptions: options));
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(string suffix)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditLifecycle,
            eventId: $"event-{suffix}",
            occurredUtc: DrainUtc.AddMinutes(-1),
            envelopeId: $"envelope-{suffix}",
            correlationId: $"correlation-{suffix}",
            auditResidueId: $"residue-{suffix}",
            lifecycleStage: DecisionReceiptLifecycleStage.ExternalEmissionQueued,
            policyVersion: "v1",
            policyHash: "hash",
            traceId: $"trace-{suffix}",
            operationName: "governance.emit",
            outcome: "Queued",
            emitterProvider: "test-sink");
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

    private sealed class ThrowingEmitter(Exception exception) : IGovernanceEmitter
    {
        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);
            cancellationToken.ThrowIfCancellationRequested();
            throw exception;
        }
    }
}
