using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Storage.InMemory.Outbox;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Unit tests for the clock used by <see cref="GovernanceOutboxDrain" /> when no explicit drain timestamp is supplied.
/// </summary>
public sealed class GovernanceOutboxDrainTimeProviderTests
{
    private static readonly DateTimeOffset NextRetryUtc = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that a drain without an explicit timestamp reads the configured clock when deciding retry readiness.
    /// </summary>
    /// <param name="minutesFromRetry">The clock offset from the entry's next retry time.</param>
    /// <param name="expectedAttempted">Whether the entry is expected to be attempted.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Theory]
    [InlineData(-1, false)]
    [InlineData(1, true)]
    public async Task DrainWithoutTimestampUsesConfiguredTimeProvider(int minutesFromRetry, bool expectedAttempted)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(CreateEnvelope(), cancellationToken);
        _ = await outboxStore.MarkFailedAsync(
            entry.OutboxEntryId,
            GovernanceEmissionError.Create("provider.transient", "Transient failure.", isRetryable: true),
            NextRetryUtc,
            cancellationToken);

        var drain = new GovernanceOutboxDrain(
            outboxStore,
            new DeliveredEmitter(),
            logger: null,
            Options.Create(new GovernanceOutboxOptions { UseClaimLeases = false }),
            new FixedTimeProvider(NextRetryUtc.AddMinutes(minutesFromRetry)));

        IReadOnlyList<GovernanceOutboxEntry> drained = await drain.DrainAsync(cancellationToken: cancellationToken);

        Assert.Equal(expectedAttempted, drained.Count == 1);
    }

    /// <summary>
    /// Verifies that omitting the clock falls back to the system clock rather than failing.
    /// </summary>
    /// <param name="retryInPast">Whether the entry's next retry time is in the past relative to the system clock.</param>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OmittedTimeProviderFallsBackToSystemClock(bool retryInPast)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        var outboxStore = new InMemoryGovernanceOutboxStore();
        GovernanceOutboxEntry entry = await outboxStore.EnqueueAsync(CreateEnvelope(), cancellationToken);
        DateTimeOffset nextRetryUtc = retryInPast
            ? DateTimeOffset.UtcNow.AddYears(-1)
            : DateTimeOffset.UtcNow.AddYears(1);
        _ = await outboxStore.MarkFailedAsync(
            entry.OutboxEntryId,
            GovernanceEmissionError.Create("provider.transient", "Transient failure.", isRetryable: true),
            nextRetryUtc,
            cancellationToken);

        var drain = new GovernanceOutboxDrain(
            outboxStore,
            new DeliveredEmitter(),
            outboxOptions: Options.Create(new GovernanceOutboxOptions { UseClaimLeases = false }),
            timeProvider: null);

        IReadOnlyList<GovernanceOutboxEntry> drained = await drain.DrainAsync(cancellationToken: cancellationToken);

        Assert.Equal(retryInPast, drained.Count == 1);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope()
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditResidue,
            eventId: "event-clock",
            occurredUtc: NextRetryUtc.AddHours(-1),
            envelopeId: "envelope-clock",
            correlationId: "correlation-clock",
            emitterStatus: "pending",
            emitterProvider: "outbox");
    }

    private sealed class DeliveredEmitter : IGovernanceEmitter
    {
        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceEmissionResult.Delivered("clock-provider"));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }
}
