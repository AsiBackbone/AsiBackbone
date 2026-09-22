using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Tests for the CanonicalPayloadBuilder class, focusing on branch coverage for different artifact types and scenarios.
/// </summary>
public sealed class CanonicalPayloadBuilderBranchTests
{
    private static readonly DateTimeOffset OccurredLocal = new(2026, 6, 18, 7, 0, 0, TimeSpan.FromHours(-5));
    private static readonly DateTimeOffset CreatedUtc = new(2026, 6, 18, 12, 0, 1, TimeSpan.Zero);
    private static readonly DateTimeOffset RetryUtc = new(2026, 6, 18, 12, 10, 0, TimeSpan.Zero);

    /// <summary>
    /// Tests that the ForDecisionReceipt method uses the event identifier as the artifact ID when the decision receipt ID is missing, and filters metadata based on the provided options.
    /// </summary>
    [Fact]
    public void ForDecisionReceiptUsesEventIdentifierWhenResidueIdIsMissingAndFiltersMetadata()
    {
        var options = CanonicalPayloadOptions.Create(["safe"]);
        var residue = DecisionReceipt.Create(
            GovernanceActorContext.Service(" system-1 ", " System "),
            " gateway.execute ",
            " Allowed ",
            reasonCodes: [" reason.beta ", "", "reason.alpha", "reason.beta"],
            eventId: " event-1 ",
            occurredUtc: OccurredLocal,
            correlationId: " correlation-1 ",
            traceId: " trace-1 ",
            metadata: new Dictionary<string, string>
            {
                ["safe"] = " included ",
                ["ignored"] = " excluded "
            },
            decisionReceiptId: " ");

        CanonicalPayload payload = CanonicalPayloadBuilder.ForDecisionReceipt(residue, options);

        Assert.Equal(CanonicalArtifactTypes.DecisionReceipt, payload.ArtifactType);
        Assert.Equal("event-1", payload.ArtifactId);
        Assert.Contains("\"auditResidueId\":\"event-1\"", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"metadata\":{\"safe\":\"included\"}", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"reasonCodes\":[\"reason.alpha\",\"reason.beta\"]", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ignored", payload.CanonicalJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that the ForDecisionReceiptLifecycleEvent method includes filtered metadata and the correct stage sequence in the canonical payload.
    /// </summary>
    [Fact]
    public void ForDecisionReceiptLifecycleEventIncludesFilteredMetadataAndStageSequence()
    {
        var options = CanonicalPayloadOptions.Create(["safe"]);
        var lifecycleEvent = DecisionReceiptLifecycleEvent.Create(
            DecisionReceiptLifecycleStage.ExternalEmissionQueued,
            " correlation-1 ",
            decisionReceiptId: " residue-1 ",
            eventId: " lifecycle-1 ",
            occurredUtc: OccurredLocal,
            traceId: " trace-1 ",
            operationName: " gateway.execute ",
            outcome: " Queued ",
            metadata: new Dictionary<string, string>
            {
                ["safe"] = " included ",
                ["ignored"] = " excluded "
            });

        CanonicalPayload payload = CanonicalPayloadBuilder.ForDecisionReceiptLifecycleEvent(lifecycleEvent, options);

        Assert.Equal(CanonicalArtifactTypes.DecisionReceiptLifecycleEvent, payload.ArtifactType);
        Assert.Equal("lifecycle-1", payload.ArtifactId);
        Assert.Contains("\"metadata\":{\"safe\":\"included\"}", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains($"\"stageSequence\":{(int)DecisionReceiptLifecycleStage.ExternalEmissionQueued}", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("ignored", payload.CanonicalJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that the ForGovernanceEmissionEnvelope method writes a null payload in the canonical JSON when the envelope has no payload, and includes empty metadata.
    /// </summary>
    [Fact]
    public void ForGovernanceEmissionEnvelopeWritesNullPayloadWhenEnvelopeHasNoPayload()
    {
        GovernanceEmissionEnvelope envelope = CreateEnvelope(payload: null, metadata: null);

        CanonicalPayload payload = CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(envelope);

        Assert.Equal(CanonicalArtifactTypes.GovernanceEmissionEnvelope, payload.ArtifactType);
        Assert.Equal("envelope-event-1", payload.ArtifactId);
        Assert.Contains("\"metadata\":{}", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"payload\":null", payload.CanonicalJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that the ForGovernanceEmissionEnvelope method includes both the payload and filtered metadata in the canonical JSON when they are present, and excludes any disallowed metadata keys.
    /// </summary>
    [Fact]
    public void ForGovernanceEmissionEnvelopeIncludesPayloadAndFilteredMetadataWhenPresent()
    {
        var options = CanonicalPayloadOptions.Create(["safe"]);
        var emissionPayload = GovernanceEmissionPayload.Create(
            " audit-summary ",
            schemaVersion: " v1 ",
            contentType: " application/json ",
            contentHash: " payload-hash ",
            sizeBytes: 128,
            metadata: new Dictionary<string, string>
            {
                ["safe"] = " payload-included ",
                ["ignored"] = " payload-excluded "
            });
        GovernanceEmissionEnvelope envelope = CreateEnvelope(
            emissionPayload,
            new Dictionary<string, string>
            {
                ["safe"] = " envelope-included ",
                ["ignored"] = " envelope-excluded "
            });

        CanonicalPayload payload = CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(envelope, options);

        Assert.Contains("\"metadata\":{\"safe\":\"envelope-included\"}", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"payload\":{", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"metadata\":{\"safe\":\"payload-included\"}", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"sizeBytes\":128", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("excluded", payload.CanonicalJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that the ForGovernanceOutboxEntry method includes error content and the next retry timestamp in the canonical JSON when the outbox entry has failed with a retryable error.
    /// </summary>
    [Fact]
    public void ForGovernanceOutboxEntryIncludesErrorContentAndRetryTimestamp()
    {
        var error = GovernanceEmissionError.Create(
            " provider.timeout ",
            " Provider timed out. ",
            isRetryable: true,
            providerName: " sink ",
            providerErrorCode: " timeout ");
        GovernanceOutboxEntry entry = GovernanceOutboxEntry.Create(
            CreateEnvelope(payload: null, metadata: null),
            outboxEntryId: " outbox-1 ",
            createdUtc: CreatedUtc)
            .MarkFailed(error, RetryUtc, RetryUtc.AddMinutes(-1));

        CanonicalPayload payload = CanonicalPayloadBuilder.ForGovernanceOutboxEntry(entry);

        Assert.Equal(CanonicalArtifactTypes.GovernanceOutboxEntry, payload.ArtifactType);
        Assert.Equal("outbox-1", payload.ArtifactId);
        Assert.Contains("\"lastError\":{", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"code\":\"provider.timeout\"", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"isRetryable\":true", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"nextRetryUtc\":\"2026-06-18T12:10:00.0000000Z\"", payload.CanonicalJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Tests that the ForGovernanceOutboxEntry method writes null values for optional fields in the canonical JSON when the outbox entry has no error and no retry information.
    /// </summary>
    [Fact]
    public void ForGovernanceOutboxEntryWritesNullOptionalErrorAndRetryFields()
    {
        var entry = GovernanceOutboxEntry.Create(
            CreateEnvelope(payload: null, metadata: null),
            outboxEntryId: " outbox-1 ",
            createdUtc: CreatedUtc);

        CanonicalPayload payload = CanonicalPayloadBuilder.ForGovernanceOutboxEntry(entry);

        Assert.Contains("\"deadLetterReason\":null", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"lastError\":null", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"nextRetryUtc\":null", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"providerName\":null", payload.CanonicalJson, StringComparison.Ordinal);
        Assert.Contains("\"providerRecordId\":null", payload.CanonicalJson, StringComparison.Ordinal);
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(
        GovernanceEmissionPayload? payload,
        IReadOnlyDictionary<string, string>? metadata)
    {
        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditLifecycle,
            eventId: " event-1 ",
            occurredUtc: OccurredLocal,
            envelopeId: " envelope-event-1 ",
            createdUtc: CreatedUtc,
            correlationId: " correlation-1 ",
            decisionReceiptId: " residue-1 ",
            lifecycleStage: DecisionReceiptLifecycleStage.ExternalEmissionQueued,
            policyVersion: " policy-v1 ",
            policyHash: " policy-hash ",
            traceId: " trace-1 ",
            spanId: " span-1 ",
            parentSpanId: " parent-span-1 ",
            operationName: " gateway.execute ",
            outcome: " Queued ",
            payload: payload,
            metadata: metadata);
    }
}
