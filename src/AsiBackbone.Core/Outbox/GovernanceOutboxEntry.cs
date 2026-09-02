using System.Collections.ObjectModel;
using AsiBackbone.Core.Emissions;

namespace AsiBackbone.Core.Outbox;

/// <summary>
/// Represents a provider-neutral durable outbox entry for a governance emission envelope.
/// </summary>
/// <remarks>
/// Outbox entries are intended to be persisted before optional downstream provider delivery is attempted.
/// </remarks>
public sealed class GovernanceOutboxEntry
{
    private const int DefaultMaxRetryCount = 5;

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    private GovernanceOutboxEntry(
        string outboxEntryId,
        GovernanceEmissionEnvelope envelope,
        GovernanceEmissionStatus status,
        DateTimeOffset createdUtc,
        DateTimeOffset updatedUtc,
        int retryCount,
        int maxRetryCount,
        DateTimeOffset? nextRetryUtc,
        GovernanceEmissionError? lastError,
        string? providerName,
        string? providerRecordId,
        string? deadLetterReason,
        IReadOnlyDictionary<string, string> metadata,
        string? claimOwner,
        string? claimToken,
        DateTimeOffset? claimedUtc,
        DateTimeOffset? claimExpiresUtc,
        int claimAttemptCount)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
        ArgumentNullException.ThrowIfNull(envelope);

        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status), status, "Outbox status must be defined.");
        }

        if (retryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(retryCount), retryCount, "Retry count must be greater than or equal to zero.");
        }

        if (maxRetryCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxRetryCount), maxRetryCount, "Maximum retry count must be greater than or equal to zero.");
        }

        if (claimAttemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(claimAttemptCount), claimAttemptCount, "Claim attempt count must be greater than or equal to zero.");
        }

        OutboxEntryId = outboxEntryId.Trim();
        Envelope = envelope;
        Status = status;
        CreatedUtc = createdUtc.ToUniversalTime();
        UpdatedUtc = updatedUtc.ToUniversalTime();
        RetryCount = retryCount;
        MaxRetryCount = maxRetryCount;
        NextRetryUtc = nextRetryUtc?.ToUniversalTime();
        LastError = lastError;
        ProviderName = NormalizeOptional(providerName);
        ProviderRecordId = NormalizeOptional(providerRecordId);
        DeadLetterReason = NormalizeOptional(deadLetterReason);
        Metadata = metadata;
        ClaimOwner = NormalizeOptional(claimOwner);
        ClaimToken = NormalizeOptional(claimToken);
        ClaimedUtc = claimedUtc?.ToUniversalTime();
        ClaimExpiresUtc = claimExpiresUtc?.ToUniversalTime();
        ClaimAttemptCount = claimAttemptCount;
    }

    /// <summary>
    /// Gets the stable outbox entry identifier.
    /// </summary>
    public string OutboxEntryId { get; }

    /// <summary>
    /// Gets the provider-neutral governance emission envelope being persisted for delivery.
    /// </summary>
    public GovernanceEmissionEnvelope Envelope { get; }

    /// <summary>
    /// Gets the current provider-neutral outbox status.
    /// </summary>
    public GovernanceEmissionStatus Status { get; }

    /// <summary>
    /// Gets the UTC timestamp when the outbox entry was created.
    /// </summary>
    public DateTimeOffset CreatedUtc { get; }

    /// <summary>
    /// Gets the UTC timestamp when the outbox entry was last updated.
    /// </summary>
    public DateTimeOffset UpdatedUtc { get; }

    /// <summary>
    /// Gets the number of failed or deferred delivery attempts recorded for this entry.
    /// </summary>
    public int RetryCount { get; }

    /// <summary>
    /// Gets the persisted maximum retry count retained for storage compatibility.
    /// </summary>
    /// <remarks>
    /// Retry eligibility and threshold dead-lettering are controlled by <see cref="AsiBackboneGovernanceOutboxOptions" />
    /// when the entry is processed by <see cref="AsiBackboneGovernanceOutboxDrain" />.
    /// </remarks>
    public int MaxRetryCount { get; }

    /// <summary>
    /// Gets the next UTC retry timestamp, when retry scheduling is active.
    /// </summary>
    public DateTimeOffset? NextRetryUtc { get; }

    /// <summary>
    /// Gets the last provider-neutral emission error, when available.
    /// </summary>
    public GovernanceEmissionError? LastError { get; }

    /// <summary>
    /// Gets the provider name associated with the most recent attempt, when available.
    /// </summary>
    public string? ProviderName { get; }

    /// <summary>
    /// Gets the provider-side record identifier, when delivery returned one and it is safe to store.
    /// </summary>
    public string? ProviderRecordId { get; }

    /// <summary>
    /// Gets the dead-letter reason, when the entry has reached a terminal dead-letter state.
    /// </summary>
    public string? DeadLetterReason { get; }

    /// <summary>
    /// Gets minimized provider-neutral outbox metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets the current claim owner, when this row is leased by a worker.
    /// </summary>
    public string? ClaimOwner { get; }

    /// <summary>
    /// Gets the opaque token for the current claim lease.
    /// </summary>
    public string? ClaimToken { get; }

    /// <summary>
    /// Gets the UTC timestamp when the current claim was acquired.
    /// </summary>
    public DateTimeOffset? ClaimedUtc { get; }

    /// <summary>
    /// Gets the UTC timestamp when the current claim lease expires.
    /// </summary>
    public DateTimeOffset? ClaimExpiresUtc { get; }

    /// <summary>
    /// Gets the number of claim or reclaim attempts recorded for this entry.
    /// </summary>
    public int ClaimAttemptCount { get; }

    /// <summary>
    /// Gets a value indicating whether this entry was delivered successfully.
    /// </summary>
    public bool IsDelivered => Status is GovernanceEmissionStatus.Delivered;

    /// <summary>
    /// Gets a value indicating whether this entry has reached a terminal dead-letter state.
    /// </summary>
    public bool IsDeadLettered => Status is GovernanceEmissionStatus.DeadLettered;

    /// <summary>
    /// Gets a value indicating whether metadata is present.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Gets a value indicating whether claim metadata is present.
    /// </summary>
    public bool HasClaim => ClaimOwner is not null && ClaimToken is not null && ClaimedUtc.HasValue && ClaimExpiresUtc.HasValue;

    /// <summary>
    /// Creates a pending durable governance outbox entry.
    /// </summary>
    public static GovernanceOutboxEntry Create(
        GovernanceEmissionEnvelope envelope,
        string? outboxEntryId = null,
        DateTimeOffset? createdUtc = null,
        int maxRetryCount = DefaultMaxRetryCount,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        DateTimeOffset timestamp = createdUtc ?? DateTimeOffset.UtcNow;

        return new GovernanceOutboxEntry(
            NormalizeIdentifier(outboxEntryId),
            envelope,
            GovernanceEmissionStatus.Pending,
            timestamp,
            timestamp,
            retryCount: 0,
            maxRetryCount,
            nextRetryUtc: null,
            lastError: null,
            providerName: null,
            providerRecordId: null,
            deadLetterReason: null,
            NormalizeMetadata(metadata),
            claimOwner: null,
            claimToken: null,
            claimedUtc: null,
            claimExpiresUtc: null,
            claimAttemptCount: 0);
    }

    /// <summary>
    /// Restores a durable outbox entry from provider-neutral storage.
    /// </summary>
    /// <remarks>
    /// This factory exists for storage adapters. It does not perform provider emission and does not add any provider dependency to Core.
    /// </remarks>
    public static GovernanceOutboxEntry Restore(
        GovernanceEmissionEnvelope envelope,
        GovernanceEmissionStatus status,
        string outboxEntryId,
        DateTimeOffset createdUtc,
        DateTimeOffset updatedUtc,
        int retryCount = 0,
        int maxRetryCount = DefaultMaxRetryCount,
        DateTimeOffset? nextRetryUtc = null,
        GovernanceEmissionError? lastError = null,
        string? providerName = null,
        string? providerRecordId = null,
        string? deadLetterReason = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? claimOwner = null,
        string? claimToken = null,
        DateTimeOffset? claimedUtc = null,
        DateTimeOffset? claimExpiresUtc = null,
        int claimAttemptCount = 0)
    {
        return new GovernanceOutboxEntry(
            outboxEntryId,
            envelope,
            status,
            createdUtc,
            updatedUtc,
            retryCount,
            maxRetryCount,
            nextRetryUtc,
            lastError,
            providerName,
            providerRecordId,
            deadLetterReason,
            NormalizeMetadata(metadata),
            claimOwner,
            claimToken,
            claimedUtc,
            claimExpiresUtc,
            claimAttemptCount);
    }

    /// <summary>
    /// Determines whether the entry is ready for retry at the supplied UTC timestamp.
    /// </summary>
    public bool IsRetryReady(DateTimeOffset utcNow)
    {
        return !IsDelivered && !IsDeadLettered && Status is GovernanceEmissionStatus.Deferred or GovernanceEmissionStatus.Failed or GovernanceEmissionStatus.RetryableFailure && (NextRetryUtc is null || NextRetryUtc <= utcNow.ToUniversalTime());
    }

    /// <summary>
    /// Determines whether the entry has an active claim lease at the supplied UTC timestamp.
    /// </summary>
    public bool HasActiveClaim(DateTimeOffset utcNow)
    {
        return HasClaim && ClaimExpiresUtc > utcNow.ToUniversalTime();
    }

    /// <summary>
    /// Determines whether the entry may be claimed by a cooperating worker at the supplied UTC timestamp.
    /// </summary>
    public bool CanBeClaimed(DateTimeOffset utcNow)
    {
        return !IsDelivered && !IsDeadLettered && !HasActiveClaim(utcNow);
    }

    /// <summary>
    /// Determines whether the entry is still owned by the supplied claim token.
    /// </summary>
    public bool IsClaimedBy(GovernanceOutboxClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);

        return string.Equals(OutboxEntryId, claim.OutboxEntryId, StringComparison.Ordinal)
            && string.Equals(ClaimOwner, claim.WorkerId, StringComparison.Ordinal)
            && string.Equals(ClaimToken, claim.ClaimToken, StringComparison.Ordinal);
    }

    /// <summary>
    /// Returns a claimed copy of this entry.
    /// </summary>
    public GovernanceOutboxEntry MarkClaimed(
        string claimOwner,
        string? claimToken = null,
        DateTimeOffset? claimedUtc = null,
        TimeSpan? leaseDuration = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(claimOwner);

        DateTimeOffset normalizedClaimedUtc = (claimedUtc ?? DateTimeOffset.UtcNow).ToUniversalTime();
        TimeSpan normalizedLeaseDuration = leaseDuration ?? GovernanceOutboxClaimRequest.DefaultLeaseDuration;

        return normalizedLeaseDuration <= TimeSpan.Zero
            ? throw new ArgumentOutOfRangeException(nameof(leaseDuration), leaseDuration, "Lease duration must be greater than TimeSpan.Zero.")
            : Copy(
            Status,
            normalizedClaimedUtc,
            RetryCount,
            NextRetryUtc,
            LastError,
            ProviderName,
            ProviderRecordId,
            DeadLetterReason,
            Metadata,
            claimOwner.Trim(),
            string.IsNullOrWhiteSpace(claimToken) ? Guid.NewGuid().ToString("N") : claimToken.Trim(),
            normalizedClaimedUtc,
            normalizedClaimedUtc.Add(normalizedLeaseDuration),
            ClaimAttemptCount + 1);
    }

    /// <summary>
    /// Returns a copy with claim lease fields cleared.
    /// </summary>
    public GovernanceOutboxEntry ReleaseClaim(DateTimeOffset? updatedUtc = null)
    {
        return Copy(
            Status,
            updatedUtc,
            RetryCount,
            NextRetryUtc,
            LastError,
            ProviderName,
            ProviderRecordId,
            DeadLetterReason,
            Metadata,
            claimOwner: null,
            claimToken: null,
            claimedUtc: null,
            claimExpiresUtc: null,
            ClaimAttemptCount);
    }

    /// <summary>
    /// Returns a delivered copy of this entry.
    /// </summary>
    public GovernanceOutboxEntry MarkDelivered(
        GovernanceEmissionResult result,
        DateTimeOffset? updatedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(result);

        return !result.IsSuccess
            ? throw new ArgumentException("Delivered outbox transitions require a successful emission result.", nameof(result))
            : Copy(
            GovernanceEmissionStatus.Delivered,
            updatedUtc,
            retryCount: RetryCount,
            nextRetryUtc: null,
            lastError: null,
            providerName: result.ProviderName,
            providerRecordId: result.ProviderRecordId,
            deadLetterReason: null,
            metadata: MergeMetadata(Metadata, result.Metadata),
            claimOwner: null,
            claimToken: null,
            claimedUtc: null,
            claimExpiresUtc: null,
            ClaimAttemptCount);
    }

    /// <summary>
    /// Returns a failed or retryable-failure copy of this entry.
    /// </summary>
    public GovernanceOutboxEntry MarkFailed(
        GovernanceEmissionError governanceEmissionError,
        DateTimeOffset? nextRetryUtc = null,
        DateTimeOffset? updatedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(governanceEmissionError);

        int nextRetryCount = RetryCount + 1;
        GovernanceEmissionStatus nextStatus = governanceEmissionError.IsRetryable
            ? GovernanceEmissionStatus.RetryableFailure
            : GovernanceEmissionStatus.Failed;

        return Copy(
            nextStatus,
            updatedUtc,
            nextRetryCount,
            nextRetryUtc,
            governanceEmissionError,
            governanceEmissionError.ProviderName,
            providerRecordId: null,
            deadLetterReason: null,
            Metadata,
            claimOwner: null,
            claimToken: null,
            claimedUtc: null,
            claimExpiresUtc: null,
            ClaimAttemptCount);
    }

    /// <summary>
    /// Returns a deferred copy of this entry.
    /// </summary>
    public GovernanceOutboxEntry MarkDeferred(
        GovernanceEmissionError? governanceEmissionError = null,
        DateTimeOffset? nextRetryUtc = null,
        DateTimeOffset? updatedUtc = null)
    {
        return Copy(
            GovernanceEmissionStatus.Deferred,
            updatedUtc,
            retryCount: RetryCount,
            nextRetryUtc,
            governanceEmissionError,
            governanceEmissionError?.ProviderName,
            providerRecordId: null,
            deadLetterReason: null,
            metadata: Metadata,
            claimOwner: null,
            claimToken: null,
            claimedUtc: null,
            claimExpiresUtc: null,
            ClaimAttemptCount);
    }

    /// <summary>
    /// Returns a dead-lettered copy of this entry.
    /// </summary>
    public GovernanceOutboxEntry MarkDeadLettered(
        GovernanceEmissionError governanceEmissionError,
        string? deadLetterReason = null,
        DateTimeOffset? updatedUtc = null)
    {
        ArgumentNullException.ThrowIfNull(governanceEmissionError);

        return Copy(
            GovernanceEmissionStatus.DeadLettered,
            updatedUtc,
            retryCount: RetryCount,
            nextRetryUtc: null,
            lastError: governanceEmissionError,
            providerName: governanceEmissionError.ProviderName,
            providerRecordId: null,
            deadLetterReason: deadLetterReason ?? governanceEmissionError.Message,
            metadata: Metadata,
            claimOwner: null,
            claimToken: null,
            claimedUtc: null,
            claimExpiresUtc: null,
            ClaimAttemptCount);
    }

    private GovernanceOutboxEntry Copy(
        GovernanceEmissionStatus status,
        DateTimeOffset? updatedUtc,
        int retryCount,
        DateTimeOffset? nextRetryUtc,
        GovernanceEmissionError? lastError,
        string? providerName,
        string? providerRecordId,
        string? deadLetterReason,
        IReadOnlyDictionary<string, string> metadata,
        string? claimOwner,
        string? claimToken,
        DateTimeOffset? claimedUtc,
        DateTimeOffset? claimExpiresUtc,
        int claimAttemptCount)
    {
        return new GovernanceOutboxEntry(
            OutboxEntryId,
            Envelope,
            status,
            CreatedUtc,
            updatedUtc ?? DateTimeOffset.UtcNow,
            retryCount,
            MaxRetryCount,
            nextRetryUtc,
            lastError,
            providerName,
            providerRecordId,
            deadLetterReason,
            metadata,
            claimOwner,
            claimToken,
            claimedUtc,
            claimExpiresUtc,
            claimAttemptCount);
    }

    private static string NormalizeIdentifier(string? identifier)
    {
        return string.IsNullOrWhiteSpace(identifier)
            ? Guid.NewGuid().ToString("N")
            : identifier.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static IReadOnlyDictionary<string, string> MergeMetadata(
        IReadOnlyDictionary<string, string> originalMetadata,
        IReadOnlyDictionary<string, string> resultMetadata)
    {
        if (resultMetadata.Count == 0)
        {
            return originalMetadata;
        }

        if (originalMetadata.Count == 0)
        {
            return resultMetadata;
        }

        Dictionary<string, string> mergedMetadata = new(originalMetadata.Count + resultMetadata.Count, StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in originalMetadata)
        {
            mergedMetadata[item.Key] = item.Value;
        }

        foreach (KeyValuePair<string, string> item in resultMetadata)
        {
            mergedMetadata[item.Key] = item.Value;
        }

        return new ReadOnlyDictionary<string, string>(mergedMetadata);
    }

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return EmptyMetadata;
        }

        Dictionary<string, string> normalizedMetadata = new(metadata.Count, StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in metadata)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            normalizedMetadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
        }

        return normalizedMetadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(normalizedMetadata);
    }
}
