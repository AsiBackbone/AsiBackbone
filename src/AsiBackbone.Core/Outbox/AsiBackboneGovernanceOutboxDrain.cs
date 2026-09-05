using AsiBackbone.Core.Emissions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace AsiBackbone.Core.Outbox;

/// <summary>
/// Drains provider-neutral governance outbox entries through a configured governance emitter.
/// </summary>
/// <remarks>
/// This drain path is provider-neutral. It is suitable for tests, samples, local validation, and host-owned workers that need to hand persisted outbox entries to an optional downstream emitter without coupling Core to a provider SDK.
/// </remarks>
/// <remarks>
/// Initializes a new instance of the <see cref="AsiBackboneGovernanceOutboxDrain" /> class.
/// </remarks>
/// <param name="outboxStore">The provider-neutral outbox store.</param>
/// <param name="emitter">The provider-neutral governance emitter.</param>
/// <param name="logger">The logger used to record local operational diagnostics for drain failures.</param>
/// <param name="outboxOptions">The provider-neutral retry, poison-message, and claim options used by the drain.</param>
public sealed class AsiBackboneGovernanceOutboxDrain(
    IAsiBackboneGovernanceOutboxStore outboxStore,
    IAsiBackboneGovernanceEmitter emitter,
    ILogger<AsiBackboneGovernanceOutboxDrain>? logger = null,
    IOptions<AsiBackboneGovernanceOutboxOptions>? outboxOptions = null)
{
    private static readonly Action<ILogger, string, int, string, DateTimeOffset, string?, string?, Exception?> LogGovernanceEmissionException = LoggerMessage.Define<string, int, string, DateTimeOffset, string?, string?>(
        LogLevel.Warning,
        new EventId(19701, nameof(LogGovernanceEmissionException)),
        "Governance outbox emission threw an exception for outbox entry {OutboxEntryId} on attempt {AttemptCount}. Emitter provider: {EmitterProvider}. Next retry UTC: {NextRetryUtc}. Correlation ID: {CorrelationId}. Audit residue ID: {AuditResidueId}.");

    private static readonly Action<ILogger, string, int, int, string?, Exception?> LogGovernanceClaimAttemptsExceeded = LoggerMessage.Define<string, int, int, string?>(
        LogLevel.Warning,
        new EventId(19702, nameof(LogGovernanceClaimAttemptsExceeded)),
        "Governance outbox entry {OutboxEntryId} was claimed {ClaimAttemptCount} times without reaching a terminal state, exceeding the configured maximum of {MaxClaimAttempts}. The entry is being dead-lettered without a further emission attempt. Correlation ID: {CorrelationId}.");

    private readonly IAsiBackboneGovernanceOutboxStore outboxStore = outboxStore ?? throw new ArgumentNullException(nameof(outboxStore));
    private readonly IAsiBackboneGovernanceEmitter emitter = emitter ?? throw new ArgumentNullException(nameof(emitter));
    private readonly ILogger<AsiBackboneGovernanceOutboxDrain> logger = logger ?? NullLogger<AsiBackboneGovernanceOutboxDrain>.Instance;
    private readonly AsiBackboneGovernanceOutboxOptions retryOptions = ResolveOptions(outboxOptions);

    /// <summary>
    /// Drains pending and retry-ready outbox entries through the configured emitter.
    /// </summary>
    /// <param name="utcNow">The UTC timestamp used for retry-ready checks.</param>
    /// <param name="maxCount">The maximum number of entries to drain.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The updated outbox entries that were attempted by the drain.</returns>
    public async ValueTask<IReadOnlyList<GovernanceOutboxEntry>> DrainAsync(
        DateTimeOffset? utcNow = null,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        if (maxCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxCount), maxCount, "Maximum count must be greater than zero.");
        }

        cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset drainUtc = (utcNow ?? DateTimeOffset.UtcNow).ToUniversalTime();

        if (retryOptions.UseClaimLeases)
        {
            // A caller-supplied timestamp is honored for every page so deterministic tests stay deterministic;
            // otherwise each page is leased from a fresh reading taken when that page is claimed.
            Func<DateTimeOffset> claimClock = utcNow.HasValue
                ? () => drainUtc
                : static () => DateTimeOffset.UtcNow;

            return await DrainClaimedAsync(claimClock, maxCount, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<GovernanceOutboxEntry> pendingEntries = await outboxStore
            .FindPendingAsync(maxCount, cancellationToken)
            .ConfigureAwait(false);

        if (pendingEntries.Count >= maxCount)
        {
            return await DrainEntriesAsync(pendingEntries, drainUtc, cancellationToken).ConfigureAwait(false);
        }

        IReadOnlyList<GovernanceOutboxEntry> retryReadyEntries = await outboxStore
            .FindRetryReadyAsync(drainUtc, maxCount - pendingEntries.Count, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<GovernanceOutboxEntry> entriesToDrain = MergeEntries(pendingEntries, retryReadyEntries, maxCount);
        return await DrainEntriesAsync(entriesToDrain, drainUtc, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<IReadOnlyList<GovernanceOutboxEntry>> DrainClaimedAsync(
        Func<DateTimeOffset> claimClock,
        int maxCount,
        CancellationToken cancellationToken)
    {
        if (outboxStore is not IAsiBackboneGovernanceOutboxClaimStore claimStore)
        {
            throw new InvalidOperationException("Claim leases are enabled, but the configured outbox store does not implement IAsiBackboneGovernanceOutboxClaimStore. Supply a claim-capable store or set AsiBackboneGovernanceOutboxOptions.UseClaimLeases to false, which allows concurrent hosts to emit the same envelope more than once.");
        }

        string workerId = retryOptions.ClaimWorkerId ?? throw new InvalidOperationException("ClaimWorkerId is required when claim leases are enabled.");

        List<GovernanceOutboxEntry> drainedEntries = [];
        int remainingCount = maxCount;

        // Entries are claimed a page at a time rather than leasing the whole batch at once, so a slow emitter
        // cannot exhaust a single lease across the batch and leave later entries reclaimable while in flight.
        while (remainingCount > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int pageSize = Math.Min(retryOptions.ClaimPageSize, remainingCount);
            DateTimeOffset pageUtc = claimClock().ToUniversalTime();

            IReadOnlyList<GovernanceOutboxClaim> pageClaims = await ClaimPageAsync(
                claimStore,
                workerId,
                pageUtc,
                pageSize,
                cancellationToken)
                .ConfigureAwait(false);

            if (pageClaims.Count == 0)
            {
                break;
            }

            IReadOnlyList<GovernanceOutboxEntry> pageEntries = await DrainClaimsAsync(
                claimStore,
                pageClaims,
                pageUtc,
                cancellationToken)
                .ConfigureAwait(false);

            drainedEntries.AddRange(pageEntries);
            remainingCount -= pageClaims.Count;

            if (pageClaims.Count < pageSize)
            {
                break;
            }
        }

        return drainedEntries;
    }

    private async ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimPageAsync(
        IAsiBackboneGovernanceOutboxClaimStore claimStore,
        string workerId,
        DateTimeOffset pageUtc,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var pendingRequest = GovernanceOutboxClaimRequest.Create(
            workerId,
            pageUtc,
            retryOptions.ClaimLeaseDuration,
            pageSize);
        IReadOnlyList<GovernanceOutboxClaim> pendingClaims = await claimStore
            .ClaimPendingAsync(pendingRequest, cancellationToken)
            .ConfigureAwait(false);

        if (pendingClaims.Count >= pageSize)
        {
            return pendingClaims;
        }

        var retryRequest = GovernanceOutboxClaimRequest.Create(
            workerId,
            pageUtc,
            retryOptions.ClaimLeaseDuration,
            pageSize - pendingClaims.Count);
        IReadOnlyList<GovernanceOutboxClaim> retryReadyClaims = await claimStore
            .ClaimRetryReadyAsync(retryRequest, cancellationToken)
            .ConfigureAwait(false);

        return MergeClaims(pendingClaims, retryReadyClaims, pageSize);
    }

    private async ValueTask<IReadOnlyList<GovernanceOutboxEntry>> DrainEntriesAsync(
        IReadOnlyList<GovernanceOutboxEntry> entriesToDrain,
        DateTimeOffset drainUtc,
        CancellationToken cancellationToken)
    {
        if (entriesToDrain.Count == 0)
        {
            return Array.Empty<GovernanceOutboxEntry>();
        }

        List<GovernanceOutboxEntry> updatedEntries = new(entriesToDrain.Count);

        foreach (GovernanceOutboxEntry entry in entriesToDrain)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GovernanceOutboxEntry updatedEntry = await DrainEntryAsync(entry, drainUtc, cancellationToken).ConfigureAwait(false);
            updatedEntries.Add(updatedEntry);
        }

        return updatedEntries;
    }

    private async ValueTask<IReadOnlyList<GovernanceOutboxEntry>> DrainClaimsAsync(
        IAsiBackboneGovernanceOutboxClaimStore claimStore,
        IReadOnlyList<GovernanceOutboxClaim> claimsToDrain,
        DateTimeOffset drainUtc,
        CancellationToken cancellationToken)
    {
        if (claimsToDrain.Count == 0)
        {
            return Array.Empty<GovernanceOutboxEntry>();
        }

        List<GovernanceOutboxEntry> updatedEntries = new(claimsToDrain.Count);

        foreach (GovernanceOutboxClaim claim in claimsToDrain)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GovernanceOutboxEntry updatedEntry = await DrainClaimAsync(claimStore, claim, drainUtc, cancellationToken).ConfigureAwait(false);
            updatedEntries.Add(updatedEntry);
        }

        return updatedEntries;
    }

    private static IReadOnlyList<GovernanceOutboxEntry> MergeEntries(
        IReadOnlyList<GovernanceOutboxEntry> pendingEntries,
        IReadOnlyList<GovernanceOutboxEntry> retryReadyEntries,
        int maxCount)
    {
        if (pendingEntries.Count == 0)
        {
            return retryReadyEntries;
        }

        if (retryReadyEntries.Count == 0)
        {
            return pendingEntries;
        }

        var entriesToDrain = new List<GovernanceOutboxEntry>(Math.Min(maxCount, pendingEntries.Count + retryReadyEntries.Count));
        var existingEntryIds = new HashSet<string>(pendingEntries.Count + retryReadyEntries.Count, StringComparer.Ordinal);

        foreach (GovernanceOutboxEntry pendingEntry in pendingEntries)
        {
            _ = existingEntryIds.Add(pendingEntry.OutboxEntryId);
            entriesToDrain.Add(pendingEntry);
        }

        foreach (GovernanceOutboxEntry retryReadyEntry in retryReadyEntries)
        {
            if (entriesToDrain.Count >= maxCount)
            {
                break;
            }

            if (existingEntryIds.Add(retryReadyEntry.OutboxEntryId))
            {
                entriesToDrain.Add(retryReadyEntry);
            }
        }

        return entriesToDrain;
    }

    private static IReadOnlyList<GovernanceOutboxClaim> MergeClaims(
        IReadOnlyList<GovernanceOutboxClaim> pendingClaims,
        IReadOnlyList<GovernanceOutboxClaim> retryReadyClaims,
        int maxCount)
    {
        if (pendingClaims.Count == 0)
        {
            return retryReadyClaims;
        }

        if (retryReadyClaims.Count == 0)
        {
            return pendingClaims;
        }

        var claimsToDrain = new List<GovernanceOutboxClaim>(Math.Min(maxCount, pendingClaims.Count + retryReadyClaims.Count));
        var existingEntryIds = new HashSet<string>(pendingClaims.Count + retryReadyClaims.Count, StringComparer.Ordinal);

        foreach (GovernanceOutboxClaim pendingClaim in pendingClaims)
        {
            _ = existingEntryIds.Add(pendingClaim.OutboxEntryId);
            claimsToDrain.Add(pendingClaim);
        }

        foreach (GovernanceOutboxClaim retryReadyClaim in retryReadyClaims)
        {
            if (claimsToDrain.Count >= maxCount)
            {
                break;
            }

            if (existingEntryIds.Add(retryReadyClaim.OutboxEntryId))
            {
                claimsToDrain.Add(retryReadyClaim);
            }
        }

        return claimsToDrain;
    }

    private async ValueTask<GovernanceOutboxEntry> DrainEntryAsync(
        GovernanceOutboxEntry entry,
        DateTimeOffset drainUtc,
        CancellationToken cancellationToken)
    {
        GovernanceEmissionResult result;

        try
        {
            result = await emitter.EmitAsync(entry.Envelope, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            DateTimeOffset nextRetryUtc = GetRetryUtc(drainUtc);
            LogEmissionException(entry, nextRetryUtc, ex);
            GovernanceEmissionError governanceEmissionError = CreateExceptionError(ex);

            return await ApplyFailureAsync(
                entry,
                governanceEmissionError,
                nextRetryUtc,
                cancellationToken)
                .ConfigureAwait(false);
        }

        return await ApplyEmissionResultAsync(entry, result, drainUtc, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<GovernanceOutboxEntry> DrainClaimAsync(
        IAsiBackboneGovernanceOutboxClaimStore claimStore,
        GovernanceOutboxClaim claim,
        DateTimeOffset drainUtc,
        CancellationToken cancellationToken)
    {
        // An emitter that hangs or is killed mid-emission leaves the entry claimed but never failed, so its retry
        // count does not advance and the retry-based poison-message policy never fires. The claim count does
        // advance on every reclaim, so it is the only signal that bounds that loop. Checked before emission so a
        // repeatedly reclaimed entry is not handed to the emitter again.
        if (ShouldDeadLetterForClaimAttempts(claim.Entry))
        {
            var claimExhaustedError = GovernanceEmissionError.Create(
                retryOptions.MaxClaimAttemptsReasonCode,
                retryOptions.MaxClaimAttemptsReasonMessage);

            LogClaimAttemptsExceeded(claim.Entry);

            return await claimStore.MarkClaimDeadLetteredAsync(
                claim,
                claimExhaustedError,
                retryOptions.MaxClaimAttemptsReasonMessage,
                cancellationToken)
                .ConfigureAwait(false);
        }

        GovernanceEmissionResult result;

        try
        {
            result = await emitter.EmitAsync(claim.Entry.Envelope, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            DateTimeOffset nextRetryUtc = GetRetryUtc(drainUtc);
            LogEmissionException(claim.Entry, nextRetryUtc, ex);
            GovernanceEmissionError governanceEmissionError = CreateExceptionError(ex);

            return await ApplyClaimFailureAsync(
                claimStore,
                claim,
                governanceEmissionError,
                nextRetryUtc,
                cancellationToken)
                .ConfigureAwait(false);
        }

        return await ApplyEmissionResultAsync(claimStore, claim, result, drainUtc, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<GovernanceOutboxEntry> ApplyEmissionResultAsync(
        GovernanceOutboxEntry entry,
        GovernanceEmissionResult result,
        DateTimeOffset drainUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return await outboxStore.MarkDeliveredAsync(entry.OutboxEntryId, result, cancellationToken).ConfigureAwait(false);
        }

        if (result.Status is GovernanceEmissionStatus.DeadLettered)
        {
            GovernanceEmissionError governanceEmissionError = result.Error ?? GovernanceEmissionError.Create(
                "emission.deadlettered",
                "Governance emission returned a dead-lettered result.",
                providerName: result.ProviderName);

            return await outboxStore.MarkDeadLetteredAsync(
                entry.OutboxEntryId,
                governanceEmissionError,
                governanceEmissionError.Message,
                cancellationToken)
                .ConfigureAwait(false);
        }

        if (result.Status is GovernanceEmissionStatus.Deferred or GovernanceEmissionStatus.Pending)
        {
            GovernanceEmissionError? governanceEmissionError = result.Error ?? (result.Status is GovernanceEmissionStatus.Pending
                ? GovernanceEmissionError.Create(
                    "emission.pending",
                    "Governance emission remained pending after the outbox drain attempt.",
                    isRetryable: true,
                    providerName: result.ProviderName)
                : null);

            GovernanceOutboxEntry deferredEntry = entry.MarkDeferred(
                governanceEmissionError,
                result.RetryAfterUtc ?? GetDeferredUtc(drainUtc),
                drainUtc);

            return await outboxStore.SaveAsync(deferredEntry, cancellationToken).ConfigureAwait(false);
        }

        GovernanceEmissionError failure = result.Error ?? GovernanceEmissionError.Create(
            "emission.failed",
            "Governance emission returned a failed result without provider-neutral error details.",
            isRetryable: result.ShouldRetry,
            providerName: result.ProviderName);

        return await ApplyFailureAsync(
            entry,
            failure,
            result.RetryAfterUtc,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<GovernanceOutboxEntry> ApplyEmissionResultAsync(
        IAsiBackboneGovernanceOutboxClaimStore claimStore,
        GovernanceOutboxClaim claim,
        GovernanceEmissionResult result,
        DateTimeOffset drainUtc,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (result.IsSuccess)
        {
            return await claimStore.MarkClaimDeliveredAsync(claim, result, cancellationToken).ConfigureAwait(false);
        }

        if (result.Status is GovernanceEmissionStatus.DeadLettered)
        {
            GovernanceEmissionError governanceEmissionError = result.Error ?? GovernanceEmissionError.Create(
                "emission.deadlettered",
                "Governance emission returned a dead-lettered result.",
                providerName: result.ProviderName);

            return await claimStore.MarkClaimDeadLetteredAsync(
                claim,
                governanceEmissionError,
                governanceEmissionError.Message,
                cancellationToken)
                .ConfigureAwait(false);
        }

        if (result.Status is GovernanceEmissionStatus.Deferred or GovernanceEmissionStatus.Pending)
        {
            GovernanceEmissionError? governanceEmissionError = result.Error ?? (result.Status is GovernanceEmissionStatus.Pending
                ? GovernanceEmissionError.Create(
                    "emission.pending",
                    "Governance emission remained pending after the outbox drain attempt.",
                    isRetryable: true,
                    providerName: result.ProviderName)
                : null);

            GovernanceOutboxEntry deferredEntry = claim.Entry.MarkDeferred(
                governanceEmissionError,
                result.RetryAfterUtc ?? GetDeferredUtc(drainUtc),
                drainUtc);

            return await claimStore.SaveClaimAsync(claim, deferredEntry, cancellationToken).ConfigureAwait(false);
        }

        GovernanceEmissionError failure = result.Error ?? GovernanceEmissionError.Create(
            "emission.failed",
            "Governance emission returned a failed result without provider-neutral error details.",
            isRetryable: result.ShouldRetry,
            providerName: result.ProviderName);

        return await ApplyClaimFailureAsync(
            claimStore,
            claim,
            failure,
            result.RetryAfterUtc,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<GovernanceOutboxEntry> ApplyFailureAsync(
        GovernanceOutboxEntry entry,
        GovernanceEmissionError failure,
        DateTimeOffset? nextRetryUtc,
        CancellationToken cancellationToken)
    {
        if (ShouldDeadLetter(entry))
        {
            GovernanceEmissionError deadLetterError = CreateMaxRetryError(failure);
            return await outboxStore.MarkDeadLetteredAsync(
                entry.OutboxEntryId,
                deadLetterError,
                retryOptions.DeadLetterReasonMessage,
                cancellationToken)
                .ConfigureAwait(false);
        }

        return await outboxStore.MarkFailedAsync(
            entry.OutboxEntryId,
            failure,
            nextRetryUtc,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private async ValueTask<GovernanceOutboxEntry> ApplyClaimFailureAsync(
        IAsiBackboneGovernanceOutboxClaimStore claimStore,
        GovernanceOutboxClaim claim,
        GovernanceEmissionError failure,
        DateTimeOffset? nextRetryUtc,
        CancellationToken cancellationToken)
    {
        if (ShouldDeadLetter(claim.Entry))
        {
            GovernanceEmissionError deadLetterError = CreateMaxRetryError(failure);
            return await claimStore.MarkClaimDeadLetteredAsync(
                claim,
                deadLetterError,
                retryOptions.DeadLetterReasonMessage,
                cancellationToken)
                .ConfigureAwait(false);
        }

        return await claimStore.MarkClaimFailedAsync(
            claim,
            failure,
            nextRetryUtc,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private bool ShouldDeadLetter(GovernanceOutboxEntry entry)
    {
        return retryOptions.DeadLetterOnMaxRetryAttempts
            && entry.RetryCount + 1 >= retryOptions.MaxRetryAttempts;
    }

    private bool ShouldDeadLetterForClaimAttempts(GovernanceOutboxEntry entry)
    {
        // The store has already stamped this claim, so ClaimAttemptCount includes the current attempt.
        return retryOptions.DeadLetterOnMaxClaimAttempts
            && entry.ClaimAttemptCount > retryOptions.MaxClaimAttempts;
    }

    private GovernanceEmissionError CreateMaxRetryError(GovernanceEmissionError failure)
    {
        return GovernanceEmissionError.Create(
            retryOptions.DeadLetterReasonCode,
            retryOptions.DeadLetterReasonMessage,
            providerName: failure.ProviderName,
            providerErrorCode: failure.Code);
    }

    private void LogEmissionException(GovernanceOutboxEntry entry, DateTimeOffset nextRetryUtc, Exception exception)
    {
        LogGovernanceEmissionException(
            logger,
            entry.OutboxEntryId,
            entry.RetryCount + 1,
            ResolveEmitterProvider(entry),
            nextRetryUtc,
            entry.Envelope.CorrelationId,
            entry.Envelope.AuditResidueId,
            exception);
    }

    private void LogClaimAttemptsExceeded(GovernanceOutboxEntry entry)
    {
        LogGovernanceClaimAttemptsExceeded(
            logger,
            entry.OutboxEntryId,
            entry.ClaimAttemptCount,
            retryOptions.MaxClaimAttempts,
            entry.Envelope.CorrelationId,
            null);
    }

    private static GovernanceEmissionError CreateExceptionError(Exception exception)
    {
        return GovernanceEmissionError.Create(
            "emission.exception",
            $"Governance emission threw {exception.GetType().Name} during outbox drain.",
            isRetryable: true,
            providerErrorCode: exception.GetType().FullName);
    }

    private DateTimeOffset GetRetryUtc(DateTimeOffset drainUtc)
    {
        return drainUtc.Add(retryOptions.RetryDelay);
    }

    private DateTimeOffset GetDeferredUtc(DateTimeOffset drainUtc)
    {
        return drainUtc.Add(retryOptions.DeferredDelay);
    }

    private static AsiBackboneGovernanceOutboxOptions ResolveOptions(IOptions<AsiBackboneGovernanceOutboxOptions>? options)
    {
        AsiBackboneGovernanceOutboxOptions resolved = options?.Value ?? new AsiBackboneGovernanceOutboxOptions();
        resolved.Validate();

        return new AsiBackboneGovernanceOutboxOptions
        {
            RetryDelay = resolved.RetryDelay,
            DeferredDelay = resolved.DeferredDelay,
            MaxRetryAttempts = resolved.MaxRetryAttempts,
            DeadLetterOnMaxRetryAttempts = resolved.DeadLetterOnMaxRetryAttempts,
            DeadLetterReasonCode = resolved.DeadLetterReasonCode.Trim(),
            DeadLetterReasonMessage = resolved.DeadLetterReasonMessage.Trim(),
            UseClaimLeases = resolved.UseClaimLeases,
            ClaimWorkerId = string.IsNullOrWhiteSpace(resolved.ClaimWorkerId) ? null : resolved.ClaimWorkerId.Trim(),
            ClaimLeaseDuration = resolved.ClaimLeaseDuration,
            ClaimPageSize = resolved.ClaimPageSize,
            MaxClaimAttempts = resolved.MaxClaimAttempts,
            DeadLetterOnMaxClaimAttempts = resolved.DeadLetterOnMaxClaimAttempts,
            MaxClaimAttemptsReasonCode = resolved.MaxClaimAttemptsReasonCode.Trim(),
            MaxClaimAttemptsReasonMessage = resolved.MaxClaimAttemptsReasonMessage.Trim()
        };
    }

    private static string ResolveEmitterProvider(GovernanceOutboxEntry entry)
    {
        return entry.Envelope.EmitterProvider ?? entry.ProviderName ?? "unspecified";
    }
}
