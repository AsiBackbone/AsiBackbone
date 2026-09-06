using System.Diagnostics.Metrics;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace AsiBackbone.EntityFrameworkCore.Outbox;

/// <summary>
/// EF Core governance outbox store that exposes explicit caller-owned claim transition outcomes.
/// </summary>
/// <remarks>
/// This store preserves the existing convenience API while adding an outcome-aware contract that distinguishes applied
/// transitions from stale claims, terminal no-ops, concurrency losses, and missing rows. It delegates persistence to
/// <see cref="EfCoreGovernanceOutboxStore" /> and observes the scoped <see cref="DbContext" /> save boundary so callers do not
/// infer write ownership from the returned durable status alone.
/// </remarks>
public sealed class EfCoreGovernanceOutboxOutcomeStore : IAsiBackboneGovernanceOutboxClaimOutcomeStore
{
    private static readonly Meter Meter = new("AsiBackbone.EntityFrameworkCore.Outbox", "1.0.0");
    private static readonly Counter<long> ClaimTransitionCounter = Meter.CreateCounter<long>(
        "asibackbone.outbox.claim_transition_attempts",
        description: "Counts claimed outbox transition attempts by caller-visible outcome.");

    private static readonly Action<ILogger, string, string, string, string, Exception?> LogClaimTransitionNotApplied =
        LoggerMessage.Define<string, string, string, string>(
            LogLevel.Warning,
            new EventId(19901, nameof(LogClaimTransitionNotApplied)),
            "Claimed governance outbox transition was not applied by worker {WorkerId} for entry {OutboxEntryId}. Outcome: {Outcome}. Durable status: {DurableStatus}.");

    private readonly DbContext dbContext;
    private readonly EfCoreGovernanceOutboxStore innerStore;
    private readonly ILogger<EfCoreGovernanceOutboxOutcomeStore> logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EfCoreGovernanceOutboxOutcomeStore" /> class.
    /// </summary>
    /// <param name="dbContext">The host-owned scoped database context.</param>
    /// <param name="logger">The logger used for non-applied claimed-transition diagnostics.</param>
    public EfCoreGovernanceOutboxOutcomeStore(
        DbContext dbContext,
        ILogger<EfCoreGovernanceOutboxOutcomeStore>? logger = null)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        this.dbContext = dbContext;
        innerStore = new EfCoreGovernanceOutboxStore(dbContext);
        this.logger = logger ?? NullLogger<EfCoreGovernanceOutboxOutcomeStore>.Instance;
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> EnqueueAsync(
        GovernanceEmissionEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        return innerStore.EnqueueAsync(envelope, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> SaveAsync(
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken = default)
    {
        return innerStore.SaveAsync(entry, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry?> FindByOutboxEntryIdAsync(
        string outboxEntryId,
        CancellationToken cancellationToken = default)
    {
        return innerStore.FindByOutboxEntryIdAsync(outboxEntryId, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindPendingAsync(
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        return innerStore.FindPendingAsync(maxCount, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindRetryReadyAsync(
        DateTimeOffset utcNow,
        int maxCount = 100,
        CancellationToken cancellationToken = default)
    {
        return innerStore.FindRetryReadyAsync(utcNow, maxCount, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimPendingAsync(
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        return innerStore.ClaimPendingAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<GovernanceOutboxClaim>> ClaimRetryReadyAsync(
        GovernanceOutboxClaimRequest request,
        CancellationToken cancellationToken = default)
    {
        return innerStore.ClaimRetryReadyAsync(request, cancellationToken);
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> MarkClaimDeliveredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionResult result,
        CancellationToken cancellationToken = default)
    {
        return (await TryMarkClaimDeliveredAsync(claim, result, cancellationToken).ConfigureAwait(false)).Entry;
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> MarkClaimFailedAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        DateTimeOffset? nextRetryUtc = null,
        CancellationToken cancellationToken = default)
    {
        return (await TryMarkClaimFailedAsync(claim, governanceEmissionError, nextRetryUtc, cancellationToken).ConfigureAwait(false)).Entry;
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> MarkClaimDeadLetteredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        string? deadLetterReason = null,
        CancellationToken cancellationToken = default)
    {
        return (await TryMarkClaimDeadLetteredAsync(
            claim,
            governanceEmissionError,
            deadLetterReason,
            cancellationToken).ConfigureAwait(false)).Entry;
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceOutboxEntry> SaveClaimAsync(
        GovernanceOutboxClaim claim,
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken = default)
    {
        return (await TrySaveClaimAsync(claim, entry, cancellationToken).ConfigureAwait(false)).Entry;
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry?> ReleaseClaimAsync(
        GovernanceOutboxClaim claim,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        return innerStore.ReleaseClaimAsync(claim, reason, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> MarkDeliveredAsync(
        string outboxEntryId,
        GovernanceEmissionResult result,
        CancellationToken cancellationToken = default)
    {
        return innerStore.MarkDeliveredAsync(outboxEntryId, result, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> MarkFailedAsync(
        string outboxEntryId,
        GovernanceEmissionError governanceEmissionError,
        DateTimeOffset? nextRetryUtc = null,
        CancellationToken cancellationToken = default)
    {
        return innerStore.MarkFailedAsync(outboxEntryId, governanceEmissionError, nextRetryUtc, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxEntry> MarkDeadLetteredAsync(
        string outboxEntryId,
        GovernanceEmissionError governanceEmissionError,
        string? deadLetterReason = null,
        CancellationToken cancellationToken = default)
    {
        return innerStore.MarkDeadLetteredAsync(outboxEntryId, governanceEmissionError, deadLetterReason, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxClaimTransitionResult> TryMarkClaimDeliveredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionResult result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(result);
        return TryUpdateClaimAsync(
            claim,
            () => innerStore.MarkClaimDeliveredAsync(claim, result, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxClaimTransitionResult> TryMarkClaimFailedAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        DateTimeOffset? nextRetryUtc = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(governanceEmissionError);
        return TryUpdateClaimAsync(
            claim,
            () => innerStore.MarkClaimFailedAsync(claim, governanceEmissionError, nextRetryUtc, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxClaimTransitionResult> TryMarkClaimDeadLetteredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        string? deadLetterReason = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(governanceEmissionError);
        return TryUpdateClaimAsync(
            claim,
            () => innerStore.MarkClaimDeadLetteredAsync(claim, governanceEmissionError, deadLetterReason, cancellationToken),
            cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<GovernanceOutboxClaimTransitionResult> TrySaveClaimAsync(
        GovernanceOutboxClaim claim,
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return !string.Equals(claim.OutboxEntryId, entry.OutboxEntryId, StringComparison.Ordinal)
            ? throw new ArgumentException("Claim and entry must reference the same outbox entry ID.", nameof(entry))
            : TryUpdateClaimAsync(
            claim,
            () => innerStore.SaveClaimAsync(claim, entry, cancellationToken),
            cancellationToken);
    }

    private async ValueTask<GovernanceOutboxClaimTransitionResult> TryUpdateClaimAsync(
        GovernanceOutboxClaim claim,
        Func<ValueTask<GovernanceOutboxEntry>> update,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(claim);
        cancellationToken.ThrowIfCancellationRequested();

        GovernanceOutboxEntry? currentEntry = await innerStore
            .FindByOutboxEntryIdAsync(claim.OutboxEntryId, cancellationToken)
            .ConfigureAwait(false);

        if (currentEntry is null)
        {
            return Complete(claim, claim.Entry, GovernanceOutboxClaimTransitionOutcome.Missing);
        }

        if (currentEntry.IsDelivered || currentEntry.IsDeadLettered)
        {
            return Complete(claim, currentEntry, GovernanceOutboxClaimTransitionOutcome.Terminal);
        }

        if (!currentEntry.IsClaimedBy(claim))
        {
            return Complete(claim, currentEntry, GovernanceOutboxClaimTransitionOutcome.StaleClaim);
        }

        bool saveCompleted = false;
        void savedChangesHandler(object? sender, SavedChangesEventArgs eventArgs)
        {
            _ = sender;
            _ = eventArgs;
            saveCompleted = true;
        }

        dbContext.SavedChanges += savedChangesHandler;

        GovernanceOutboxEntry returnedEntry;
        try
        {
            returnedEntry = await update().ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            GovernanceOutboxEntry? missingEntry = await innerStore
                .FindByOutboxEntryIdAsync(claim.OutboxEntryId, cancellationToken)
                .ConfigureAwait(false);

            if (missingEntry is null)
            {
                return Complete(claim, currentEntry, GovernanceOutboxClaimTransitionOutcome.Missing);
            }

            throw;
        }
        finally
        {
            dbContext.SavedChanges -= savedChangesHandler;
        }

        if (saveCompleted)
        {
            return Complete(claim, returnedEntry, GovernanceOutboxClaimTransitionOutcome.Applied);
        }

        GovernanceOutboxEntry? refreshedEntry = await innerStore
            .FindByOutboxEntryIdAsync(claim.OutboxEntryId, cancellationToken)
            .ConfigureAwait(false);

        return refreshedEntry is null
            ? Complete(claim, currentEntry, GovernanceOutboxClaimTransitionOutcome.Missing)
            : Complete(claim, refreshedEntry, GovernanceOutboxClaimTransitionOutcome.ConcurrencyLost);
    }

    private GovernanceOutboxClaimTransitionResult Complete(
        GovernanceOutboxClaim claim,
        GovernanceOutboxEntry entry,
        GovernanceOutboxClaimTransitionOutcome outcome)
    {
        ClaimTransitionCounter.Add(
            1,
            new KeyValuePair<string, object?>("outcome", outcome.ToString()),
            new KeyValuePair<string, object?>("durable_status", entry.Status.ToString()));

        if (outcome is not GovernanceOutboxClaimTransitionOutcome.Applied)
        {
            LogClaimTransitionNotApplied(
                logger,
                claim.WorkerId,
                claim.OutboxEntryId,
                outcome.ToString(),
                entry.Status.ToString(),
                null);
        }

        return GovernanceOutboxClaimTransitionResult.Create(entry, outcome);
    }
}
