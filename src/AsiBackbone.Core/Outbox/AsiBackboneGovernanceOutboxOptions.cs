namespace AsiBackbone.Core.Outbox;

/// <summary>
/// Provides provider-neutral retry timing, poison-message, and optional claim/lease options for governance outbox drain processing.
/// </summary>
/// <remarks>
/// These options control default retry timestamps when a downstream emitter does not provide its own retry-after value. Claim leasing is enabled by default and requires a claim-capable outbox store; hosts supplying a store that does not implement <see cref="IAsiBackboneGovernanceOutboxClaimStore" /> must set <see cref="UseClaimLeases" /> to <see langword="false" /> and accept the duplicate-emission behavior that follows from it.
/// </remarks>
public sealed class AsiBackboneGovernanceOutboxOptions
{
    /// <summary>
    /// Gets the stable default reason code used when the drain dead-letters an entry after the configured retry threshold is reached.
    /// </summary>
    public const string DefaultDeadLetterReasonCode = "outbox.max_retry_attempts_exceeded";

    /// <summary>
    /// Gets the stable default reason message used when the drain dead-letters an entry after the configured retry threshold is reached.
    /// </summary>
    public const string DefaultDeadLetterReasonMessage = "Governance outbox entry exceeded the configured maximum retry attempts.";

    /// <summary>
    /// Gets the stable default reason code used when the drain dead-letters an entry after the configured claim threshold is reached.
    /// </summary>
    public const string DefaultMaxClaimAttemptsReasonCode = "outbox.max_claim_attempts_exceeded";

    /// <summary>
    /// Gets the stable default reason message used when the drain dead-letters an entry after the configured claim threshold is reached.
    /// </summary>
    public const string DefaultMaxClaimAttemptsReasonMessage = "Governance outbox entry exceeded the configured maximum claim attempts without reaching a terminal state.";

    /// <summary>
    /// Gets the default number of claimed entries drained under a single lease before the drain claims again.
    /// </summary>
    public const int DefaultClaimPageSize = 10;

    /// <summary>
    /// Gets the default number of claims permitted before the drain dead-letters an entry that never reaches a terminal state.
    /// </summary>
    public const int DefaultMaxClaimAttempts = 5;

    /// <summary>
    /// Gets the default worker identifier used when claim leases are enabled and the host does not supply one.
    /// </summary>
    /// <remarks>
    /// The value combines the machine name and the current process identifier so that replicas of the same deployment do not share a claim owner by default. Hosts that run several drain workers in one process, or that need a stable identifier across restarts, should set <see cref="ClaimWorkerId" /> explicitly.
    /// </remarks>
    public static string DefaultClaimWorkerId { get; } = $"{Environment.MachineName}:{Environment.ProcessId}";

    /// <summary>
    /// Gets or sets the default delay applied after a transient emission failure or unexpected emitter exception.
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the default delay applied when an emitter returns a pending or deferred result without a retry-after timestamp.
    /// </summary>
    public TimeSpan DeferredDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Gets or sets the maximum number of failed emission attempts permitted before the drain applies its poison-message policy.
    /// </summary>
    /// <remarks>
    /// The threshold counts the failure currently being processed. A value of <c>1</c> dead-letters the first failed attempt when <see cref="DeadLetterOnMaxRetryAttempts" /> is enabled.
    /// </remarks>
    public int MaxRetryAttempts { get; set; } = 5;

    /// <summary>
    /// Gets or sets a value indicating whether entries are dead-lettered when <see cref="MaxRetryAttempts" /> is reached.
    /// </summary>
    public bool DeadLetterOnMaxRetryAttempts { get; set; } = true;

    /// <summary>
    /// Gets or sets the provider-neutral reason code recorded when the configured retry threshold is reached.
    /// </summary>
    public string DeadLetterReasonCode { get; set; } = DefaultDeadLetterReasonCode;

    /// <summary>
    /// Gets or sets the provider-neutral reason message recorded when the configured retry threshold is reached.
    /// </summary>
    public string DeadLetterReasonMessage { get; set; } = DefaultDeadLetterReasonMessage;

    /// <summary>
    /// Gets or sets a value indicating whether the drain should claim outbox entries before provider emission when the store supports claim leases.
    /// </summary>
    /// <remarks>
    /// This is enabled by default because the alternative path allows two hosts draining the same durable outbox to emit the same envelope twice. The drain throws when this is enabled and the configured store does not implement <see cref="IAsiBackboneGovernanceOutboxClaimStore" />; a host supplying such a store must opt out explicitly. Claiming coordinates workers before emission and does not by itself create an exactly-once delivery guarantee.
    /// </remarks>
    public bool UseClaimLeases { get; set; } = true;

    /// <summary>
    /// Gets or sets the worker, process, node, or partition identifier used when <see cref="UseClaimLeases" /> is enabled.
    /// </summary>
    /// <remarks>
    /// Defaults to <see cref="DefaultClaimWorkerId" />. Setting this to <see langword="null" /> or whitespace while <see cref="UseClaimLeases" /> is enabled fails validation.
    /// </remarks>
    public string? ClaimWorkerId { get; set; } = DefaultClaimWorkerId;

    /// <summary>
    /// Gets or sets the lease duration used when <see cref="UseClaimLeases" /> is enabled.
    /// </summary>
    public TimeSpan ClaimLeaseDuration { get; set; } = GovernanceOutboxClaimRequest.DefaultLeaseDuration;

    /// <summary>
    /// Gets or sets the maximum number of claimed entries drained under a single lease before the drain claims again.
    /// </summary>
    /// <remarks>
    /// A drain pass claims entries in pages of this size rather than leasing an entire batch at once, so a slow emitter cannot exhaust one lease across the whole batch and leave later entries reclaimable by a peer while they are still in flight. Each page is leased from the clock reading taken when that page is claimed.
    /// </remarks>
    public int ClaimPageSize { get; set; } = DefaultClaimPageSize;

    /// <summary>
    /// Gets or sets the number of claims permitted before the drain dead-letters an entry that has never reached a terminal state.
    /// </summary>
    /// <remarks>
    /// An emitter that hangs or is killed mid-emission leaves the entry claimed but not failed, so its retry count never advances and the retry-based poison-message policy never applies. This threshold bounds that loop. The check runs before emission, using the claim count recorded by the store, and dead-lettering is gated by <see cref="DeadLetterOnMaxClaimAttempts" />.
    /// </remarks>
    public int MaxClaimAttempts { get; set; } = DefaultMaxClaimAttempts;

    /// <summary>
    /// Gets or sets a value indicating whether entries are dead-lettered when <see cref="MaxClaimAttempts" /> is reached.
    /// </summary>
    /// <remarks>
    /// When disabled, an entry that exceeds the claim threshold is still drained normally, which restores the unbounded reclaim behavior this threshold exists to bound.
    /// </remarks>
    public bool DeadLetterOnMaxClaimAttempts { get; set; } = true;

    /// <summary>
    /// Gets or sets the provider-neutral reason code recorded when the configured claim threshold is reached.
    /// </summary>
    public string MaxClaimAttemptsReasonCode { get; set; } = DefaultMaxClaimAttemptsReasonCode;

    /// <summary>
    /// Gets or sets the provider-neutral reason message recorded when the configured claim threshold is reached.
    /// </summary>
    public string MaxClaimAttemptsReasonMessage { get; set; } = DefaultMaxClaimAttemptsReasonMessage;

    /// <summary>
    /// Validates the configured outbox retry, poison-message, timing, and claim options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a retry threshold, reason, delay, or claim lease option is invalid.</exception>
    public void Validate()
    {
        ValidateDelay(RetryDelay, nameof(RetryDelay));
        ValidateDelay(DeferredDelay, nameof(DeferredDelay));

        if (MaxRetryAttempts <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxRetryAttempts)} must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(DeadLetterReasonCode))
        {
            throw new InvalidOperationException($"{nameof(DeadLetterReasonCode)} is required.");
        }

        if (string.IsNullOrWhiteSpace(DeadLetterReasonMessage))
        {
            throw new InvalidOperationException($"{nameof(DeadLetterReasonMessage)} is required.");
        }

        if (ClaimLeaseDuration <= TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{nameof(ClaimLeaseDuration)} must be greater than TimeSpan.Zero.");
        }

        if (ClaimPageSize <= 0)
        {
            throw new InvalidOperationException($"{nameof(ClaimPageSize)} must be greater than zero.");
        }

        if (MaxClaimAttempts <= 0)
        {
            throw new InvalidOperationException($"{nameof(MaxClaimAttempts)} must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(MaxClaimAttemptsReasonCode))
        {
            throw new InvalidOperationException($"{nameof(MaxClaimAttemptsReasonCode)} is required.");
        }

        if (string.IsNullOrWhiteSpace(MaxClaimAttemptsReasonMessage))
        {
            throw new InvalidOperationException($"{nameof(MaxClaimAttemptsReasonMessage)} is required.");
        }

        if (UseClaimLeases && string.IsNullOrWhiteSpace(ClaimWorkerId))
        {
            throw new InvalidOperationException($"{nameof(ClaimWorkerId)} is required when {nameof(UseClaimLeases)} is enabled.");
        }
    }

    private static void ValidateDelay(TimeSpan delay, string propertyName)
    {
        if (delay < TimeSpan.Zero)
        {
            throw new InvalidOperationException($"{propertyName} must be greater than or equal to TimeSpan.Zero.");
        }
    }
}
