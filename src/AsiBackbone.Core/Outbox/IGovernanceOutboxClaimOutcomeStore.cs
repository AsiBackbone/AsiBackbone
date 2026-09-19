using AsiBackbone.Core.Emissions;

namespace AsiBackbone.Core.Outbox;

/// <summary>
/// Defines an opt-in claim store that reports whether the current invocation applied a claimed transition.
/// </summary>
/// <remarks>
/// This contract is additive to <see cref="IGovernanceOutboxClaimStore" />. Existing convenience methods remain available for compatibility, while these methods distinguish applied transitions from stale claims, terminal no-ops, concurrency losses, and missing rows.
/// </remarks>
public interface IGovernanceOutboxClaimOutcomeStore : IGovernanceOutboxClaimStore
{
    /// <summary>
    /// Attempts to complete a claimed entry as delivered and reports whether this invocation applied the transition.
    /// </summary>
    ValueTask<GovernanceOutboxClaimTransitionResult> TryMarkClaimDeliveredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionResult result,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to complete a claimed entry as failed or retryable failed and reports whether this invocation applied the transition.
    /// </summary>
    ValueTask<GovernanceOutboxClaimTransitionResult> TryMarkClaimFailedAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        DateTimeOffset? nextRetryUtc = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to complete a claimed entry as dead-lettered and reports whether this invocation applied the transition.
    /// </summary>
    ValueTask<GovernanceOutboxClaimTransitionResult> TryMarkClaimDeadLetteredAsync(
        GovernanceOutboxClaim claim,
        GovernanceEmissionError governanceEmissionError,
        string? deadLetterReason = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Attempts to save a caller-produced claimed transition and reports whether this invocation applied the transition.
    /// </summary>
    ValueTask<GovernanceOutboxClaimTransitionResult> TrySaveClaimAsync(
        GovernanceOutboxClaim claim,
        GovernanceOutboxEntry entry,
        CancellationToken cancellationToken = default);
}
