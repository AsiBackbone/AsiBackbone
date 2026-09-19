using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.EntityFrameworkCore.Outbox;
using AsiBackbone.EntityFrameworkCore.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AsiBackbone.EntityFrameworkCore.Tests.Outbox;

/// <summary>
/// Focused persistence coverage for complete and sparse EF Core governance outbox records.
/// </summary>
public sealed class EfCoreGovernanceOutboxRoundTripTests
{
    /// <summary>
    /// Verifies representative payload, error, provider, retry, claim, sequence, timestamp, and ordinal metadata fields survive a full round trip.
    /// </summary>
    [Fact]
    public async Task FullClaimedEntryRoundTripsAllRepresentativeState()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection connection = await EfCoreGovernanceOutboxTestHost.OpenConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> options = EfCoreGovernanceOutboxTestHost.CreateOptions(connection);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(options, cancellationToken);

        var payload = GovernanceEmissionPayload.Create(
            "audit-residue",
            schemaVersion: "2.0.0",
            contentType: "application/vnd.asibackbone.audit+json",
            contentHash: "payload-hash-round-trip",
            sizeBytes: 2048,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["PayloadKey"] = "upper-payload",
                ["payloadkey"] = "lower-payload"
            });
        GovernanceEmissionEnvelope envelope = EfCoreGovernanceOutboxTestHost.CreateEnvelope(
            "full-round-trip",
            payload,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["EnvelopeKey"] = "upper-envelope",
                ["envelopekey"] = "lower-envelope"
            });
        var error = GovernanceEmissionError.Create(
            "provider.rate-limited",
            "Provider rate limit was reached.",
            isRetryable: true,
            providerName: "provider-round-trip",
            providerErrorCode: "429");
        DateTimeOffset createdUtc = new(2026, 7, 12, 15, 0, 0, TimeSpan.Zero);
        DateTimeOffset updatedUtc = createdUtc.AddMinutes(5);
        DateTimeOffset nextRetryUtc = createdUtc.AddMinutes(20);
        DateTimeOffset claimedUtc = createdUtc.AddMinutes(4);
        DateTimeOffset claimExpiresUtc = createdUtc.AddMinutes(14);
        var entry = GovernanceOutboxEntry.Restore(
            envelope,
            GovernanceEmissionStatus.RetryableFailure,
            "outbox-full-round-trip",
            createdUtc,
            updatedUtc,
            retryCount: 2,
            maxRetryCount: 7,
            nextRetryUtc: nextRetryUtc,
            lastError: error,
            providerName: "provider-round-trip",
            providerRecordId: "provider-record-round-trip",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["EntryKey"] = "upper-entry",
                ["entrykey"] = "lower-entry"
            },
            claimOwner: "worker-round-trip",
            claimToken: "claim-token-round-trip",
            claimedUtc: claimedUtc,
            claimExpiresUtc: claimExpiresUtc,
            claimAttemptCount: 3);

        await using GovernanceOutboxTestDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);
        _ = await store.SaveAsync(entry, cancellationToken);
        context.ChangeTracker.Clear();

        GovernanceOutboxEntry? persisted = await store.FindByOutboxEntryIdAsync(entry.OutboxEntryId, cancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal(entry.OutboxEntryId, persisted.OutboxEntryId);
        Assert.Equal(GovernanceEmissionStatus.RetryableFailure, persisted.Status);
        Assert.Equal(createdUtc, persisted.CreatedUtc);
        Assert.Equal(updatedUtc, persisted.UpdatedUtc);
        Assert.Equal(2, persisted.RetryCount);
        Assert.Equal(7, persisted.MaxRetryCount);
        Assert.Equal(nextRetryUtc, persisted.NextRetryUtc);
        Assert.Equal("provider-round-trip", persisted.ProviderName);
        Assert.Equal("provider-record-round-trip", persisted.ProviderRecordId);
        Assert.Null(persisted.DeadLetterReason);
        Assert.NotNull(persisted.LastError);
        Assert.Equal(error.Code, persisted.LastError.Code);
        Assert.Equal(error.Message, persisted.LastError.Message);
        Assert.True(persisted.LastError.IsRetryable);
        Assert.Equal(error.ProviderName, persisted.LastError.ProviderName);
        Assert.Equal(error.ProviderErrorCode, persisted.LastError.ProviderErrorCode);
        Assert.Equal("worker-round-trip", persisted.ClaimOwner);
        Assert.Equal("claim-token-round-trip", persisted.ClaimToken);
        Assert.Equal(claimedUtc, persisted.ClaimedUtc);
        Assert.Equal(claimExpiresUtc, persisted.ClaimExpiresUtc);
        Assert.Equal(3, persisted.ClaimAttemptCount);
        Assert.True(persisted.HasClaim);
        Assert.Equal(2, persisted.Metadata.Count);
        Assert.Equal("upper-entry", persisted.Metadata["EntryKey"]);
        Assert.Equal("lower-entry", persisted.Metadata["entrykey"]);

        GovernanceEmissionEnvelope persistedEnvelope = persisted.Envelope;
        Assert.Equal("envelope-full-round-trip", persistedEnvelope.EnvelopeId);
        Assert.Equal("1.0.0", persistedEnvelope.SchemaVersion);
        Assert.Equal(GovernanceEmissionEventType.Outbox, persistedEnvelope.EventType);
        Assert.Equal("full-round-trip", persistedEnvelope.EventId);
        Assert.Equal(new DateTimeOffset(2026, 7, 12, 11, 0, 0, TimeSpan.Zero), persistedEnvelope.OccurredUtc);
        Assert.Equal(new DateTimeOffset(2026, 7, 12, 11, 0, 1, TimeSpan.Zero), persistedEnvelope.CreatedUtc);
        Assert.Equal("correlation-full-round-trip", persistedEnvelope.CorrelationId);
        Assert.Equal("audit-full-round-trip", persistedEnvelope.AuditResidueId);
        Assert.Equal(DecisionReceiptLifecycleStage.ExternalEmissionQueued, persistedEnvelope.LifecycleStage);
        Assert.Equal((int)DecisionReceiptLifecycleStage.ExternalEmissionQueued, persistedEnvelope.LifecycleStageSequence);
        Assert.Equal("2026.07", persistedEnvelope.PolicyVersion);
        Assert.Equal("policy-hash-issue-578", persistedEnvelope.PolicyHash);
        Assert.Equal("trace-full-round-trip", persistedEnvelope.TraceId);
        Assert.Equal("span-full-round-trip", persistedEnvelope.SpanId);
        Assert.Equal("parent-span-issue-578", persistedEnvelope.ParentSpanId);
        Assert.Equal("governance.emit", persistedEnvelope.OperationName);
        Assert.Equal("Queued", persistedEnvelope.Outcome);
        Assert.Equal("actor-issue-578", persistedEnvelope.ActorId);
        Assert.Equal("queued", persistedEnvelope.EmitterStatus);
        Assert.Equal("efcore-outbox", persistedEnvelope.EmitterProvider);
        Assert.Equal(77L, persistedEnvelope.OutboxSequence);
        Assert.Equal("gateway-issue-578", persistedEnvelope.GatewayExecutionId);
        Assert.Equal("ExternalEmissionQueued", persistedEnvelope.DecisionStage);
        Assert.Equal(2, persistedEnvelope.Metadata.Count);
        Assert.Equal("upper-envelope", persistedEnvelope.Metadata["EnvelopeKey"]);
        Assert.Equal("lower-envelope", persistedEnvelope.Metadata["envelopekey"]);

        GovernanceEmissionPayload? persistedPayload = persistedEnvelope.Payload;
        Assert.NotNull(persistedPayload);
        Assert.Equal("audit-residue", persistedPayload.PayloadType);
        Assert.Equal("2.0.0", persistedPayload.SchemaVersion);
        Assert.Equal("application/vnd.asibackbone.audit+json", persistedPayload.ContentType);
        Assert.Equal("payload-hash-round-trip", persistedPayload.ContentHash);
        Assert.Equal(2048L, persistedPayload.SizeBytes);
        Assert.Equal(2, persistedPayload.Metadata.Count);
        Assert.Equal("upper-payload", persistedPayload.Metadata["PayloadKey"]);
        Assert.Equal("lower-payload", persistedPayload.Metadata["payloadkey"]);
    }

    /// <summary>
    /// Verifies absent payload and error branches restore correctly while null and empty JSON metadata become empty ordinal dictionaries.
    /// </summary>
    [Fact]
    public async Task SparseEntryRestoresAbsentPayloadErrorAndNullOrEmptyMetadataJson()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection connection = await EfCoreGovernanceOutboxTestHost.OpenConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> options = EfCoreGovernanceOutboxTestHost.CreateOptions(connection);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(options, cancellationToken);

        await using GovernanceOutboxTestDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);
        var entry = GovernanceOutboxEntry.Create(
            EfCoreGovernanceOutboxTestHost.CreateEnvelope("sparse-round-trip"),
            "outbox-sparse-round-trip",
            new DateTimeOffset(2026, 7, 12, 15, 30, 0, TimeSpan.Zero));
        _ = await store.SaveAsync(entry, cancellationToken);
        context.ChangeTracker.Clear();

        GovernanceOutboxEntryEntity entity = await context.GovernanceOutboxEntries
            .SingleAsync(item => item.OutboxEntryId == entry.OutboxEntryId, cancellationToken);
        entity.MetadataJson = "null";
        entity.EnvelopeMetadataJson = string.Empty;
        entity.EnvelopePayloadMetadataJson = "{}";
        _ = await context.SaveChangesAsync(cancellationToken);
        context.ChangeTracker.Clear();

        GovernanceOutboxEntry? persisted = await store.FindByOutboxEntryIdAsync(entry.OutboxEntryId, cancellationToken);

        Assert.NotNull(persisted);
        Assert.Equal(GovernanceEmissionStatus.Pending, persisted.Status);
        Assert.Null(persisted.Envelope.Payload);
        Assert.Null(persisted.LastError);
        Assert.Null(persisted.ProviderName);
        Assert.Null(persisted.ProviderRecordId);
        Assert.Null(persisted.DeadLetterReason);
        Assert.Empty(persisted.Metadata);
        Assert.Empty(persisted.Envelope.Metadata);
        Assert.False(persisted.HasClaim);
        Assert.Equal(0, persisted.RetryCount);
        Assert.Equal(5, persisted.MaxRetryCount);
        Assert.Null(persisted.NextRetryUtc);
    }

    /// <summary>
    /// Verifies pending and retry-ready query paths reject non-positive maximum counts before materialization.
    /// </summary>
    [Fact]
    public async Task QueryPathsRejectInvalidMaxCountArguments()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        await using SqliteConnection connection = await EfCoreGovernanceOutboxTestHost.OpenConnectionAsync(cancellationToken);
        DbContextOptions<GovernanceOutboxTestDbContext> options = EfCoreGovernanceOutboxTestHost.CreateOptions(connection);
        await EfCoreGovernanceOutboxTestHost.EnsureCreatedAsync(options, cancellationToken);

        await using GovernanceOutboxTestDbContext context = new(options);
        var store = new EfCoreGovernanceOutboxStore(context);

        ArgumentOutOfRangeException pendingException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await store.FindPendingAsync(0, cancellationToken));
        ArgumentOutOfRangeException retryException = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await store.FindRetryReadyAsync(
                new DateTimeOffset(2026, 7, 12, 16, 0, 0, TimeSpan.Zero),
                -1,
                cancellationToken));

        Assert.Equal("maxCount", pendingException.ParamName);
        Assert.Equal(0, pendingException.ActualValue);
        Assert.Equal("maxCount", retryException.ParamName);
        Assert.Equal(-1, retryException.ActualValue);
    }
}
