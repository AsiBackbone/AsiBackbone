using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Tests the multi-instance safety behavior of the claim-lease drain path: paged leasing, bounded reclaim, and the store capability requirement.
/// </summary>
public sealed class GovernanceOutboxDrainClaimSafetyTests
{
    private static readonly DateTimeOffset DrainUtc = new(2026, 9, 4, 18, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that enabling claim leases against a store that cannot claim fails with a message naming the opt-out and its consequence.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DrainAsyncFailsWithActionableErrorWhenStoreIsNotClaimCapable()
    {
        var drain = new AsiBackboneGovernanceOutboxDrain(
            new NonClaimStore(),
            new DeliveringEmitter());

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await drain.DrainAsync(DrainUtc, cancellationToken: TestContext.Current.CancellationToken));

        Assert.Contains(nameof(IAsiBackboneGovernanceOutboxClaimStore), exception.Message, StringComparison.Ordinal);
        Assert.Contains("UseClaimLeases", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a drain larger than the configured page size leases in pages rather than holding one lease across the whole batch.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DrainAsyncClaimsInPagesBoundedByClaimPageSize()
    {
        var store = new RecordingClaimStore(availableEntryCount: 7);
        var options = new AsiBackboneGovernanceOutboxOptions
        {
            ClaimWorkerId = "worker-1",
            ClaimPageSize = 3
        };
        var drain = new AsiBackboneGovernanceOutboxDrain(
            store,
            new DeliveringEmitter(),
            outboxOptions: Options.Create(options));

        IReadOnlyList<GovernanceOutboxEntry> drained = await drain.DrainAsync(
            DrainUtc,
            maxCount: 10,
            TestContext.Current.CancellationToken);

        Assert.Equal(7, drained.Count);

        // Three pages: two full pages of three, then a short page of one that ends the loop.
        Assert.Equal([3, 3, 3], store.PendingClaimMaxCounts);
        Assert.All(store.PendingClaimMaxCounts, maxCount => Assert.True(maxCount <= options.ClaimPageSize));
    }

    /// <summary>
    /// Verifies that a single lease is never requested for the whole batch when the batch exceeds the page size.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DrainAsyncNeverLeasesTheEntireBatchAtOnce()
    {
        var store = new RecordingClaimStore(availableEntryCount: 100);
        var options = new AsiBackboneGovernanceOutboxOptions { ClaimWorkerId = "worker-1" };
        var drain = new AsiBackboneGovernanceOutboxDrain(
            store,
            new DeliveringEmitter(),
            outboxOptions: Options.Create(options));

        _ = await drain.DrainAsync(DrainUtc, maxCount: 100, TestContext.Current.CancellationToken);

        Assert.All(
            store.PendingClaimMaxCounts,
            maxCount => Assert.Equal(AsiBackboneGovernanceOutboxOptions.DefaultClaimPageSize, maxCount));
    }

    /// <summary>
    /// Verifies that an entry reclaimed past the configured threshold is dead-lettered instead of being emitted again.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DrainAsyncDeadLettersEntryPastMaxClaimAttemptsWithoutEmitting()
    {
        var store = new RecordingClaimStore(availableEntryCount: 1, claimAttemptCount: 6);
        var emitter = new DeliveringEmitter();
        var options = new AsiBackboneGovernanceOutboxOptions
        {
            ClaimWorkerId = "worker-1",
            MaxClaimAttempts = 5
        };
        var drain = new AsiBackboneGovernanceOutboxDrain(store, emitter, outboxOptions: Options.Create(options));

        _ = await drain.DrainAsync(DrainUtc, maxCount: 1, TestContext.Current.CancellationToken);

        Assert.Equal(0, emitter.EmissionCount);
        Assert.Equal(1, store.DeadLetteredCount);
        Assert.Equal(
            AsiBackboneGovernanceOutboxOptions.DefaultMaxClaimAttemptsReasonCode,
            store.LastDeadLetterError?.Code);
    }

    /// <summary>
    /// Verifies that an entry at the threshold is still emitted, so the bound applies only once it is exceeded.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DrainAsyncEmitsEntryAtMaxClaimAttempts()
    {
        var store = new RecordingClaimStore(availableEntryCount: 1, claimAttemptCount: 5);
        var emitter = new DeliveringEmitter();
        var options = new AsiBackboneGovernanceOutboxOptions
        {
            ClaimWorkerId = "worker-1",
            MaxClaimAttempts = 5
        };
        var drain = new AsiBackboneGovernanceOutboxDrain(store, emitter, outboxOptions: Options.Create(options));

        _ = await drain.DrainAsync(DrainUtc, maxCount: 1, TestContext.Current.CancellationToken);

        Assert.Equal(1, emitter.EmissionCount);
        Assert.Equal(0, store.DeadLetteredCount);
    }

    /// <summary>
    /// Verifies that disabling the claim-attempt policy restores the unbounded reclaim behavior it exists to bound.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DrainAsyncEmitsPastMaxClaimAttemptsWhenClaimDeadLetteringIsDisabled()
    {
        var store = new RecordingClaimStore(availableEntryCount: 1, claimAttemptCount: 99);
        var emitter = new DeliveringEmitter();
        var options = new AsiBackboneGovernanceOutboxOptions
        {
            ClaimWorkerId = "worker-1",
            MaxClaimAttempts = 5,
            DeadLetterOnMaxClaimAttempts = false
        };
        var drain = new AsiBackboneGovernanceOutboxDrain(store, emitter, outboxOptions: Options.Create(options));

        _ = await drain.DrainAsync(DrainUtc, maxCount: 1, TestContext.Current.CancellationToken);

        Assert.Equal(1, emitter.EmissionCount);
        Assert.Equal(0, store.DeadLetteredCount);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(int index)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditLifecycle,
            eventId: $"event-{index}",
            occurredUtc: DrainUtc,
            envelopeId: $"envelope-{index}",
            correlationId: $"correlation-{index}",
            lifecycleStage: AuditResidueLifecycleStage.ExternalEmissionQueued);
    }

    private sealed class DeliveringEmitter : IAsiBackboneGovernanceEmitter
    {
        public int EmissionCount { get; private set; }

        public ValueTask<GovernanceEmissionResult> EmitAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            EmissionCount++;
            return ValueTask.FromResult(GovernanceEmissionResult.Delivered("test-sink", envelope.EnvelopeId));
        }
    }

    /// <summary>
    /// A claim store that hands out a fixed number of entries and records how each page was requested.
    /// </summary>
    private sealed class RecordingClaimStore(int availableEntryCount, int claimAttemptCount = 1)
        : IAsiBackboneGovernanceOutboxClaimStore
    {
        private int issuedEntryCount;

        public List<int> PendingClaimMaxCounts { get; } = [];

        public int DeadLetteredCount { get; private set; }

        public GovernanceEmissionError? LastDeadLetterError { get; private set; }

        public ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimPendingAsync(
            GovernanceOutboxClaimRequest request,
            CancellationToken cancellationToken = default)
        {
            PendingClaimMaxCounts.Add(request.MaxCount);

            int issueCount = Math.Min(request.MaxCount, availableEntryCount - issuedEntryCount);
            List<GovernanceOutboxClaim> claims = [];

            for (int index = 0; index < issueCount; index++)
            {
                issuedEntryCount++;
                claims.Add(GovernanceOutboxClaim.Create(
                    CreateClaimedEntry(issuedEntryCount),
                    request.WorkerId,
                    $"token-{issuedEntryCount}",
                    request.UtcNow,
                    request.UtcNow.Add(request.LeaseDuration)));
            }

            return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxClaim>>(claims);
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimRetryReadyAsync(
            GovernanceOutboxClaimRequest request,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxClaim>>(Array.Empty<GovernanceOutboxClaim>());
        }

        public ValueTask<GovernanceOutboxEntry> MarkClaimDeliveredAsync(
            GovernanceOutboxClaim claim,
            GovernanceEmissionResult result,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(claim.Entry);
        }

        public ValueTask<GovernanceOutboxEntry> MarkClaimFailedAsync(
            GovernanceOutboxClaim claim,
            GovernanceEmissionError governanceEmissionError,
            DateTimeOffset? nextRetryUtc = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(claim.Entry);
        }

        public ValueTask<GovernanceOutboxEntry> MarkClaimDeadLetteredAsync(
            GovernanceOutboxClaim claim,
            GovernanceEmissionError governanceEmissionError,
            string? deadLetterReason = null,
            CancellationToken cancellationToken = default)
        {
            DeadLetteredCount++;
            LastDeadLetterError = governanceEmissionError;
            return ValueTask.FromResult(claim.Entry);
        }

        public ValueTask<GovernanceOutboxEntry> SaveClaimAsync(
            GovernanceOutboxClaim claim,
            GovernanceOutboxEntry entry,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(entry);
        }

        public ValueTask<GovernanceOutboxEntry?> ReleaseClaimAsync(
            GovernanceOutboxClaim claim,
            string? reason = null,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<GovernanceOutboxEntry?>(claim.Entry);
        }

        public ValueTask<GovernanceOutboxEntry> EnqueueAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> SaveAsync(
            GovernanceOutboxEntry entry,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(entry);
        }

        public ValueTask<GovernanceOutboxEntry?> FindByOutboxEntryIdAsync(
            string outboxEntryId,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<GovernanceOutboxEntry?>(null);
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindPendingAsync(
            int maxCount = 100,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxEntry>>(Array.Empty<GovernanceOutboxEntry>());
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindRetryReadyAsync(
            DateTimeOffset utcNow,
            int maxCount = 100,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult<IReadOnlyList<GovernanceOutboxEntry>>(Array.Empty<GovernanceOutboxEntry>());
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeliveredAsync(
            string outboxEntryId,
            GovernanceEmissionResult result,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> MarkFailedAsync(
            string outboxEntryId,
            GovernanceEmissionError governanceEmissionError,
            DateTimeOffset? nextRetryUtc = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeadLetteredAsync(
            string outboxEntryId,
            GovernanceEmissionError governanceEmissionError,
            string? deadLetterReason = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        private GovernanceOutboxEntry CreateClaimedEntry(int index)
        {
            return GovernanceOutboxEntry.Restore(
                CreateEnvelope(index),
                GovernanceEmissionStatus.Pending,
                $"outbox-{index}",
                DrainUtc,
                DrainUtc,
                claimOwner: "worker-1",
                claimToken: $"token-{index}",
                claimedUtc: DrainUtc,
                claimExpiresUtc: DrainUtc.AddMinutes(5),
                claimAttemptCount: claimAttemptCount);
        }
    }

    private sealed class NonClaimStore : IAsiBackboneGovernanceOutboxStore
    {
        public ValueTask<GovernanceOutboxEntry> EnqueueAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> SaveAsync(
            GovernanceOutboxEntry entry,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry?> FindByOutboxEntryIdAsync(
            string outboxEntryId,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindPendingAsync(
            int maxCount = 100,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindRetryReadyAsync(
            DateTimeOffset utcNow,
            int maxCount = 100,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeliveredAsync(
            string outboxEntryId,
            GovernanceEmissionResult result,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> MarkFailedAsync(
            string outboxEntryId,
            GovernanceEmissionError governanceEmissionError,
            DateTimeOffset? nextRetryUtc = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeadLetteredAsync(
            string outboxEntryId,
            GovernanceEmissionError governanceEmissionError,
            string? deadLetterReason = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }
}
