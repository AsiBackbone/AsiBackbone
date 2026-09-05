using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Storage.InMemory.Outbox;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Verifies provider-neutral maximum retry and poison-message behavior for governance outbox draining.
/// </summary>
public sealed class GovernanceOutboxPoisonMessageTests
{
    /// <summary>
    /// Verifies that the non-claim drain path dead-letters an entry at the configured retry threshold.
    /// </summary>
    [Fact]
    public async Task DrainAsyncDeadLettersAtConfiguredMaxRetryAttempts()
    {
        var store = new InMemoryGovernanceOutboxStore();
        DateTimeOffset firstAttemptUtc = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);
        _ = await store.EnqueueAsync(CreateEnvelope("non-claim"), TestContext.Current.CancellationToken);
        AsiBackboneGovernanceOutboxDrain drain = CreateDrain(store, useClaimLeases: false);

        GovernanceOutboxEntry firstAttempt = Assert.Single(await drain.DrainAsync(
            firstAttemptUtc,
            cancellationToken: TestContext.Current.CancellationToken));
        GovernanceOutboxEntry secondAttempt = Assert.Single(await drain.DrainAsync(
            firstAttemptUtc.AddMinutes(1),
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, firstAttempt.Status);
        Assert.True(secondAttempt.IsDeadLettered);
        Assert.Equal(1, secondAttempt.RetryCount);
        Assert.Equal(AsiBackboneGovernanceOutboxOptions.DefaultDeadLetterReasonCode, secondAttempt.LastError?.Code);
        Assert.Equal(AsiBackboneGovernanceOutboxOptions.DefaultDeadLetterReasonMessage, secondAttempt.DeadLetterReason);
        Assert.Null(secondAttempt.NextRetryUtc);
    }

    /// <summary>
    /// Verifies that the claim-lease drain path applies the same maximum retry policy and clears claim state.
    /// </summary>
    [Fact]
    public async Task DrainAsyncDeadLettersClaimedEntryAtConfiguredMaxRetryAttempts()
    {
        var store = new InMemoryGovernanceOutboxStore();
        DateTimeOffset firstAttemptUtc = new(2026, 7, 9, 13, 0, 0, TimeSpan.Zero);
        _ = await store.EnqueueAsync(CreateEnvelope("claim"), TestContext.Current.CancellationToken);
        AsiBackboneGovernanceOutboxDrain drain = CreateDrain(store, useClaimLeases: true);

        GovernanceOutboxEntry firstAttempt = Assert.Single(await drain.DrainAsync(
            firstAttemptUtc,
            cancellationToken: TestContext.Current.CancellationToken));
        GovernanceOutboxEntry secondAttempt = Assert.Single(await drain.DrainAsync(
            firstAttemptUtc.AddMinutes(1),
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, firstAttempt.Status);
        Assert.True(secondAttempt.IsDeadLettered);
        Assert.Equal(AsiBackboneGovernanceOutboxOptions.DefaultDeadLetterReasonCode, secondAttempt.LastError?.Code);
        Assert.False(secondAttempt.HasClaim);
    }

    /// <summary>
    /// Verifies that a configured threshold above the legacy persisted default remains authoritative.
    /// </summary>
    [Fact]
    public async Task DrainAsyncHonorsConfiguredMaxRetryAttemptsAbovePersistedDefault()
    {
        var store = new InMemoryGovernanceOutboxStore();
        DateTimeOffset firstAttemptUtc = new(2026, 7, 9, 13, 30, 0, TimeSpan.Zero);
        _ = await store.EnqueueAsync(CreateEnvelope("configured-ten"), TestContext.Current.CancellationToken);
        var options = new AsiBackboneGovernanceOutboxOptions { MaxRetryAttempts = 10, UseClaimLeases = false };
        var drain = new AsiBackboneGovernanceOutboxDrain(
            store,
            new RetryableFailureEmitter(),
            outboxOptions: Options.Create(options));

        var attempts = new List<GovernanceOutboxEntry>();
        for (int attempt = 0; attempt < options.MaxRetryAttempts; attempt++)
        {
            attempts.Add(Assert.Single(await drain.DrainAsync(
                firstAttemptUtc.AddMinutes(attempt),
                cancellationToken: TestContext.Current.CancellationToken)));
        }

        Assert.All(attempts.Take(9), attempt => Assert.Equal(GovernanceEmissionStatus.RetryableFailure, attempt.Status));
        GovernanceOutboxEntry terminalAttempt = attempts[9];
        Assert.True(terminalAttempt.IsDeadLettered);
        Assert.Equal(9, terminalAttempt.RetryCount);
        Assert.Equal(AsiBackboneGovernanceOutboxOptions.DefaultDeadLetterReasonCode, terminalAttempt.LastError?.Code);
        Assert.Equal(AsiBackboneGovernanceOutboxOptions.DefaultDeadLetterReasonMessage, terminalAttempt.DeadLetterReason);
    }

    /// <summary>
    /// Verifies that disabling drain-level dead-lettering continues beyond the legacy persisted retry maximum.
    /// </summary>
    [Fact]
    public async Task DrainAsyncContinuesRetryingWhenDrainDeadLetterPolicyIsDisabled()
    {
        var store = new InMemoryGovernanceOutboxStore();
        DateTimeOffset firstAttemptUtc = new(2026, 7, 9, 14, 0, 0, TimeSpan.Zero);
        _ = await store.EnqueueAsync(CreateEnvelope("disabled"), TestContext.Current.CancellationToken);
        var options = new AsiBackboneGovernanceOutboxOptions
        {
            MaxRetryAttempts = 1,
            DeadLetterOnMaxRetryAttempts = false,
            UseClaimLeases = false
        };
        var drain = new AsiBackboneGovernanceOutboxDrain(
            store,
            new RetryableFailureEmitter(),
            outboxOptions: Options.Create(options));

        GovernanceOutboxEntry? attempted = null;
        for (int attempt = 0; attempt < 10; attempt++)
        {
            attempted = Assert.Single(await drain.DrainAsync(
                firstAttemptUtc.AddMinutes(attempt),
                cancellationToken: TestContext.Current.CancellationToken));
        }

        Assert.NotNull(attempted);
        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, attempted.Status);
        Assert.Equal(10, attempted.RetryCount);
        Assert.False(attempted.IsDeadLettered);
    }

    /// <summary>
    /// Verifies that pending results remain deferred and do not consume the failed-attempt threshold.
    /// </summary>
    [Fact]
    public async Task DrainAsyncDoesNotDeadLetterPendingResultAtRetryThreshold()
    {
        var store = new InMemoryGovernanceOutboxStore();
        DateTimeOffset drainUtc = new(2026, 7, 9, 15, 0, 0, TimeSpan.Zero);
        _ = await store.EnqueueAsync(CreateEnvelope("pending"), TestContext.Current.CancellationToken);
        var options = new AsiBackboneGovernanceOutboxOptions { MaxRetryAttempts = 1, UseClaimLeases = false };
        var drain = new AsiBackboneGovernanceOutboxDrain(
            store,
            new PendingEmitter(),
            outboxOptions: Options.Create(options));

        GovernanceOutboxEntry attempted = Assert.Single(await drain.DrainAsync(
            drainUtc,
            cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal(GovernanceEmissionStatus.Deferred, attempted.Status);
        Assert.Equal(0, attempted.RetryCount);
        Assert.False(attempted.IsDeadLettered);
    }

    /// <summary>
    /// Verifies validation of provider-neutral poison-message configuration.
    /// </summary>
    [Theory]
    [InlineData(0, "code", "message")]
    [InlineData(1, "", "message")]
    [InlineData(1, "code", "")]
    public void ValidateRejectsInvalidPoisonMessageOptions(int maxRetryAttempts, string reasonCode, string reasonMessage)
    {
        var options = new AsiBackboneGovernanceOutboxOptions
        {
            MaxRetryAttempts = maxRetryAttempts,
            DeadLetterReasonCode = reasonCode,
            DeadLetterReasonMessage = reasonMessage
        };

        _ = Assert.Throws<InvalidOperationException>(options.Validate);
    }

    private static AsiBackboneGovernanceOutboxDrain CreateDrain(
        InMemoryGovernanceOutboxStore store,
        bool useClaimLeases)
    {
        var options = new AsiBackboneGovernanceOutboxOptions
        {
            MaxRetryAttempts = 2,
            UseClaimLeases = useClaimLeases,
            ClaimWorkerId = useClaimLeases ? "poison-test-worker" : null
        };

        return new AsiBackboneGovernanceOutboxDrain(
            store,
            new RetryableFailureEmitter(),
            outboxOptions: Options.Create(options));
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(string suffix)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditResidue,
            eventId: $"event-{suffix}",
            occurredUtc: new DateTimeOffset(2026, 7, 9, 11, 0, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{suffix}",
            correlationId: $"correlation-{suffix}",
            auditResidueId: $"residue-{suffix}",
            traceId: $"trace-{suffix}",
            operationName: "governance.emit",
            emitterStatus: "pending",
            emitterProvider: "poison-test");
    }

    private sealed class RetryableFailureEmitter : IAsiBackboneGovernanceEmitter
    {
        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceEmissionResult.Failed(
                GovernanceEmissionError.Create(
                    "provider.transient",
                    "The downstream provider is temporarily unavailable.",
                    isRetryable: true,
                    providerName: "poison-test")));
        }
    }

    private sealed class PendingEmitter : IAsiBackboneGovernanceEmitter
    {
        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceEmissionResult.Pending("poison-test"));
        }
    }
}
