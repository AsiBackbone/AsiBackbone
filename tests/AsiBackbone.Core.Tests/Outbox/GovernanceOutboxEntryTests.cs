using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using Xunit;

namespace AsiBackbone.Core.Tests.Outbox;

/// <summary>
/// Focused branch tests for provider-neutral governance outbox entries.
/// </summary>
public sealed class GovernanceOutboxEntryTests
{
    private static readonly DateTimeOffset CreatedLocal = new(2026, 6, 17, 8, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset UpdatedLocal = new(2026, 6, 17, 8, 5, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset RetryLocal = new(2026, 6, 17, 8, 10, 0, TimeSpan.FromHours(-5));

    /// <summary>
    /// Tests that creating a governance outbox entry with a blank outbox entry ID generates a new identifier and normalizes the created timestamp to UTC.
    /// </summary>
    [Fact]
    public void CreateGeneratesIdentifierForBlankOutboxIdAndNormalizesCreatedTimestamp()
    {
        var entry = GovernanceOutboxEntry.Create(
            CreateEnvelope(),
            outboxEntryId: " ",
            createdUtc: CreatedLocal);

        Assert.False(string.IsNullOrWhiteSpace(entry.OutboxEntryId));
        Assert.NotEqual(" ", entry.OutboxEntryId);
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 13, 0, 0, TimeSpan.Zero), entry.CreatedUtc);
        Assert.Equal(entry.CreatedUtc, entry.UpdatedUtc);
        Assert.Equal(GovernanceEmissionStatus.Pending, entry.Status);
        Assert.Equal(0, entry.RetryCount);
        Assert.Equal(5, entry.MaxRetryCount);
    }

    /// <summary>
    /// Tests that creating a governance outbox entry with a negative maximum retry count throws an ArgumentOutOfRangeException.
    /// </summary>
    [Fact]
    public void CreateRejectsNegativeMaxRetryCount()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            GovernanceOutboxEntry.Create(
                CreateEnvelope(),
                maxRetryCount: -1));
    }

    /// <summary>
    /// Tests that restoring a governance outbox entry with an invalid status, negative counts, or a blank identifier throws the appropriate exceptions.
    /// </summary>  
    [Fact]
    public void RestoreRejectsInvalidStatusNegativeCountsAndBlankIdentifier()
    {
        GovernanceEmissionEnvelope envelope = CreateEnvelope();

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            GovernanceOutboxEntry.Restore(
                envelope,
                (GovernanceEmissionStatus)999,
                "outbox-123",
                CreatedLocal,
                UpdatedLocal));

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            GovernanceOutboxEntry.Restore(
                envelope,
                GovernanceEmissionStatus.Pending,
                "outbox-123",
                CreatedLocal,
                UpdatedLocal,
                retryCount: -1));

        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            GovernanceOutboxEntry.Restore(
                envelope,
                GovernanceEmissionStatus.Pending,
                "outbox-123",
                CreatedLocal,
                UpdatedLocal,
                maxRetryCount: -1));

        _ = Assert.Throws<ArgumentException>(() =>
            GovernanceOutboxEntry.Restore(
                envelope,
                GovernanceEmissionStatus.Pending,
                " ",
                CreatedLocal,
                UpdatedLocal));
    }

    /// <summary>
    /// Tests that restoring a governance outbox entry normalizes timestamps to UTC and trims optional string properties.
    /// </summary>
    [Fact]
    public void RestoreNormalizesTimestampsAndOptionalStrings()
    {
        var entry = GovernanceOutboxEntry.Restore(
            CreateEnvelope(),
            GovernanceEmissionStatus.DeadLettered,
            " outbox-123 ",
            CreatedLocal,
            UpdatedLocal,
            providerName: " provider ",
            providerRecordId: " record-123 ",
            deadLetterReason: " reason ");

        Assert.Equal("outbox-123", entry.OutboxEntryId);
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 13, 0, 0, TimeSpan.Zero), entry.CreatedUtc);
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 13, 5, 0, TimeSpan.Zero), entry.UpdatedUtc);
        Assert.Equal("provider", entry.ProviderName);
        Assert.Equal("record-123", entry.ProviderRecordId);
        Assert.Equal("reason", entry.DeadLetterReason);
    }

    /// <summary>
    /// Tests that creating a governance outbox entry with metadata normalizes keys and values, ignores blank keys, and allows delivered results to override entry metadata.
    /// </summary>
    [Fact]
    public void MetadataNormalizationIgnoresBlankKeysTrimsValuesAndAllowsDeliveredResultOverrides()
    {
        var entry = GovernanceOutboxEntry.Create(
            CreateEnvelope(),
            metadata: new Dictionary<string, string>
            {
                [" shared "] = " entry ",
                [" entry-only "] = " retained ",
                [" "] = "ignored"
            });

        GovernanceOutboxEntry delivered = entry.MarkDelivered(
            GovernanceEmissionResult.Delivered(
                providerName: " provider ",
                providerRecordId: " record-123 ",
                metadata: new Dictionary<string, string>
                {
                    ["shared"] = " result ",
                    [" result-only "] = " added "
                }),
            updatedUtc: UpdatedLocal);

        Assert.True(entry.HasMetadata);
        Assert.Equal("entry", entry.Metadata["shared"]);
        Assert.DoesNotContain(entry.Metadata, item => string.IsNullOrWhiteSpace(item.Key));
        Assert.True(delivered.HasMetadata);
        Assert.Equal("result", delivered.Metadata["shared"]);
        Assert.Equal("retained", delivered.Metadata["entry-only"]);
        Assert.Equal("added", delivered.Metadata["result-only"]);
        Assert.Equal("provider", delivered.ProviderName);
        Assert.Equal("record-123", delivered.ProviderRecordId);
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 13, 5, 0, TimeSpan.Zero), delivered.UpdatedUtc);
    }

    /// <summary>
    /// Tests that creating a governance outbox entry with null, empty, or blank metadata results in an empty metadata view and indicates that no metadata is present.
    /// </summary>
    [Fact]
    public void CreateReturnsEmptyMetadataViewForNullEmptyAndBlankMetadata()
    {
        var nullMetadataEntry = GovernanceOutboxEntry.Create(CreateEnvelope(), metadata: null);
        var emptyMetadataEntry = GovernanceOutboxEntry.Create(CreateEnvelope(), metadata: new Dictionary<string, string>());
        var blankMetadataEntry = GovernanceOutboxEntry.Create(
            CreateEnvelope(),
            metadata: new Dictionary<string, string>
            {
                [" "] = "ignored"
            });

        Assert.False(nullMetadataEntry.HasMetadata);
        Assert.Empty(nullMetadataEntry.Metadata);
        Assert.False(emptyMetadataEntry.HasMetadata);
        Assert.Empty(emptyMetadataEntry.Metadata);
        Assert.False(blankMetadataEntry.HasMetadata);
        Assert.Empty(blankMetadataEntry.Metadata);
    }

    /// <summary>
    /// Tests that the IsRetryReady method correctly evaluates retry readiness from status and retry timing without applying the legacy persisted maximum.
    /// </summary>
    [Fact]
    public void IsRetryReadyCoversTerminalPendingLegacyMaxRetryAndRetryTimingBranches()
    {
        DateTimeOffset now = new(2026, 6, 17, 13, 10, 0, TimeSpan.Zero);

        Assert.False(RestoreEntry(GovernanceEmissionStatus.Delivered).IsRetryReady(now));
        Assert.False(RestoreEntry(GovernanceEmissionStatus.DeadLettered).IsRetryReady(now));
        Assert.False(RestoreEntry(GovernanceEmissionStatus.Pending).IsRetryReady(now));
        Assert.True(RestoreEntry(
            GovernanceEmissionStatus.Failed,
            retryCount: 5,
            maxRetryCount: 5).IsRetryReady(now));
        Assert.True(RestoreEntry(GovernanceEmissionStatus.Deferred).IsRetryReady(now));
        Assert.True(RestoreEntry(GovernanceEmissionStatus.Failed).IsRetryReady(now));
        Assert.True(RestoreEntry(GovernanceEmissionStatus.RetryableFailure).IsRetryReady(now));
        Assert.False(RestoreEntry(
            GovernanceEmissionStatus.RetryableFailure,
            nextRetryUtc: now.AddTicks(1)).IsRetryReady(now));
        Assert.True(RestoreEntry(
            GovernanceEmissionStatus.RetryableFailure,
            nextRetryUtc: now).IsRetryReady(now));
    }

    /// <summary>
    /// Tests that marking a governance outbox entry as delivered with a non-success result throws an ArgumentException, ensuring that only successful results can be used to mark delivery.
    /// </summary>
    [Fact]
    public void MarkDeliveredRejectsNonSuccessResults()
    {
        var entry = GovernanceOutboxEntry.Create(CreateEnvelope());

        _ = Assert.Throws<ArgumentException>(() =>
            entry.MarkDelivered(GovernanceEmissionResult.Pending()));
    }

    /// <summary>
    /// Tests that marking a governance outbox entry as failed records failure state without applying the legacy persisted maximum retry count.
    /// </summary>
    [Fact]
    public void MarkFailedCoversNonRetryableRetryableAndLegacyMaxRetryBranches()
    {
        DateTimeOffset retryUtc = RetryLocal;
        DateTimeOffset updatedUtc = UpdatedLocal;
        var nonRetryableError = GovernanceEmissionError.Create(
            "provider.rejected",
            "Provider rejected the minimized envelope.",
            providerName: "provider");
        var retryableError = GovernanceEmissionError.Create(
            "provider.timeout",
            "Provider timed out.",
            isRetryable: true,
            providerName: "provider");

        GovernanceOutboxEntry failed = GovernanceOutboxEntry.Create(CreateEnvelope())
            .MarkFailed(nonRetryableError, retryUtc, updatedUtc);
        GovernanceOutboxEntry retryableFailure = GovernanceOutboxEntry.Create(CreateEnvelope())
            .MarkFailed(retryableError, retryUtc, updatedUtc);
        GovernanceOutboxEntry legacyMaxRetryFailure = RestoreEntry(
            GovernanceEmissionStatus.RetryableFailure,
            retryCount: 1,
            maxRetryCount: 2,
            nextRetryUtc: retryUtc)
            .MarkFailed(retryableError, retryUtc, updatedUtc);

        Assert.Equal(GovernanceEmissionStatus.Failed, failed.Status);
        Assert.Equal(1, failed.RetryCount);
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 13, 10, 0, TimeSpan.Zero), failed.NextRetryUtc);
        Assert.Equal("provider.rejected", failed.LastError?.Code);
        Assert.Equal("provider", failed.ProviderName);
        Assert.Null(failed.DeadLetterReason);

        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, retryableFailure.Status);
        Assert.Equal(1, retryableFailure.RetryCount);
        Assert.Equal("provider.timeout", retryableFailure.LastError?.Code);
        Assert.Equal("provider", retryableFailure.ProviderName);
        Assert.Null(retryableFailure.DeadLetterReason);

        Assert.False(legacyMaxRetryFailure.IsDeadLettered);
        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, legacyMaxRetryFailure.Status);
        Assert.Equal(2, legacyMaxRetryFailure.RetryCount);
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 13, 10, 0, TimeSpan.Zero), legacyMaxRetryFailure.NextRetryUtc);
        Assert.Equal("provider.timeout", legacyMaxRetryFailure.LastError?.Code);
        Assert.Null(legacyMaxRetryFailure.DeadLetterReason);
    }

    /// <summary>
    /// Tests that marking a governance outbox entry as deferred correctly updates the status, next retry timestamp, last error, provider name, and retry count for both cases where an error is present and where no error is provided.
    /// </summary>
    [Fact]
    public void MarkDeferredCoversNullErrorAndErrorPresentPaths()
    {
        var entry = GovernanceOutboxEntry.Create(CreateEnvelope());
        var error = GovernanceEmissionError.Create(
            "provider.throttled",
            "Provider throttled the request.",
            isRetryable: true,
            providerName: "provider");

        GovernanceOutboxEntry deferredWithoutError = entry.MarkDeferred(
            nextRetryUtc: RetryLocal,
            updatedUtc: UpdatedLocal);
        GovernanceOutboxEntry deferredWithError = entry.MarkDeferred(
            error,
            RetryLocal,
            UpdatedLocal);

        Assert.Equal(GovernanceEmissionStatus.Deferred, deferredWithoutError.Status);
        Assert.Null(deferredWithoutError.LastError);
        Assert.Null(deferredWithoutError.ProviderName);
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 13, 10, 0, TimeSpan.Zero), deferredWithoutError.NextRetryUtc);
        Assert.Equal(0, deferredWithoutError.RetryCount);

        Assert.Equal(GovernanceEmissionStatus.Deferred, deferredWithError.Status);
        Assert.Equal("provider.throttled", deferredWithError.LastError?.Code);
        Assert.Equal("provider", deferredWithError.ProviderName);
        Assert.Equal(new DateTimeOffset(2026, 6, 17, 13, 10, 0, TimeSpan.Zero), deferredWithError.NextRetryUtc);
        Assert.Equal(0, deferredWithError.RetryCount);
    }

    /// <summary>
    /// Tests that marking a governance outbox entry as dead-lettered correctly updates the status, dead letter reason, last error, provider name, and next retry timestamp for both cases where an explicit dead letter reason is provided and where it falls back to the error message.
    /// </summary>
    [Fact]
    public void MarkDeadLetteredCoversExplicitReasonAndFallbackErrorMessageReason()
    {
        var error = GovernanceEmissionError.Create(
            "provider.rejected",
            "Provider rejected the minimized envelope.",
            providerName: "provider");
        var entry = GovernanceOutboxEntry.Create(CreateEnvelope());

        GovernanceOutboxEntry explicitReason = entry.MarkDeadLettered(
            error,
            deadLetterReason: " policy quarantine ",
            updatedUtc: UpdatedLocal);
        GovernanceOutboxEntry fallbackReason = entry.MarkDeadLettered(
            error,
            updatedUtc: UpdatedLocal);

        Assert.True(explicitReason.IsDeadLettered);
        Assert.Equal(GovernanceEmissionStatus.DeadLettered, explicitReason.Status);
        Assert.Equal("policy quarantine", explicitReason.DeadLetterReason);
        Assert.Equal("provider.rejected", explicitReason.LastError?.Code);
        Assert.Equal("provider", explicitReason.ProviderName);
        Assert.Null(explicitReason.NextRetryUtc);

        Assert.True(fallbackReason.IsDeadLettered);
        Assert.Equal("Provider rejected the minimized envelope.", fallbackReason.DeadLetterReason);
        Assert.Equal("provider.rejected", fallbackReason.LastError?.Code);
        Assert.Null(fallbackReason.NextRetryUtc);
    }

    private static GovernanceOutboxEntry RestoreEntry(
        GovernanceEmissionStatus status,
        int retryCount = 0,
        int maxRetryCount = 5,
        DateTimeOffset? nextRetryUtc = null,
        GovernanceEmissionError? lastError = null)
    {
        return GovernanceOutboxEntry.Restore(
            CreateEnvelope(),
            status,
            "outbox-123",
            CreatedLocal,
            UpdatedLocal,
            retryCount,
            maxRetryCount,
            nextRetryUtc,
            lastError);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope()
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditLifecycle,
            eventId: "event-249",
            occurredUtc: new DateTimeOffset(2026, 6, 17, 12, 55, 0, TimeSpan.Zero),
            envelopeId: "envelope-249",
            correlationId: "correlation-249",
            auditResidueId: "residue-249",
            lifecycleStage: AuditResidueLifecycleStage.ExternalEmissionQueued,
            policyVersion: "v1",
            policyHash: "hash-249",
            traceId: "trace-249",
            operationName: "governance.emit",
            outcome: "Queued");
    }
}
