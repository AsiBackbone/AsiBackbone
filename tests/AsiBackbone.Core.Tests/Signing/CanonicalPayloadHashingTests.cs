using System.Text.Json;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// This class contains unit tests for verifying the behavior of canonical payload hashing in the AsiBackbone.Core library. It ensures that equivalent audit ledger records, audit residues, governance emission envelopes, and governance outbox entries produce stable canonical payloads and hashes, while meaningful changes result in different hashes. The tests also validate that allow-listed metadata participates in the hash computation, while other metadata does not. Additionally, it checks that signing metadata correctly reflects the signing hash and algorithm without implying a signature is present.
/// </summary>
public sealed class CanonicalPayloadHashingTests
{
    /// <summary>
    /// This test verifies that two equivalent AuditLedgerRecord instances, which differ only in the order of reason codes and the presence of non-allow-listed metadata, produce the same canonical payload and hash. It ensures that the canonicalization process correctly normalizes the reason codes and filters out non-allow-listed metadata, resulting in a stable hash for equivalent records.
    /// </summary>
    [Fact]
    public void EquivalentAuditLedgerRecordsProduceStableCanonicalPayloadAndHash()
    {
        var options = CanonicalPayloadOptions.Create(["safe"]);
        AuditLedgerRecord first = CreateAuditLedgerRecord(["beta", "alpha"], new Dictionary<string, string>
        {
            ["ignored"] = "one",
            ["safe"] = "included"
        });
        AuditLedgerRecord second = CreateAuditLedgerRecord(["alpha", "beta"], new Dictionary<string, string>
        {
            ["safe"] = "included",
            ["ignored"] = "two"
        });

        CanonicalPayload firstPayload = CanonicalPayloadBuilder.ForAuditLedgerRecord(first, options);
        CanonicalPayload secondPayload = CanonicalPayloadBuilder.ForAuditLedgerRecord(second, options);
        CanonicalPayloadHash firstHash = CanonicalPayloadHasher.ComputeHash(firstPayload);
        CanonicalPayloadHash secondHash = CanonicalPayloadHasher.ComputeHash(secondPayload);

        Assert.Equal(firstPayload.CanonicalJson, secondPayload.CanonicalJson);
        Assert.Equal(firstHash.HashValue, secondHash.HashValue);
        Assert.Equal(CanonicalArtifactTypes.AuditLedgerRecord, firstHash.ArtifactType);
        Assert.Equal("record-1", firstHash.ArtifactId);
    }

    /// <summary>
    /// This test verifies that a meaningful change in an AuditLedgerRecord, such as adding an additional reason code, results in a different canonical payload and hash. It ensures that the canonicalization process correctly reflects changes in the record's content, leading to a distinct hash for records with different reason codes.
    /// </summary>
    [Fact]
    public void MeaningfulAuditLedgerChangeChangesCanonicalHash()
    {
        var options = CanonicalPayloadOptions.Create(["safe"]);
        AuditLedgerRecord first = CreateAuditLedgerRecord(["alpha"], new Dictionary<string, string>
        {
            ["safe"] = "included"
        });
        AuditLedgerRecord second = CreateAuditLedgerRecord(["alpha", "policy.additional-review"], new Dictionary<string, string>
        {
            ["safe"] = "included"
        });

        CanonicalPayloadHash firstHash = CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForAuditLedgerRecord(first, options));
        CanonicalPayloadHash secondHash = CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForAuditLedgerRecord(second, options));

        Assert.NotEqual(firstHash.HashValue, secondHash.HashValue);
    }

    /// <summary>
    /// This test verifies that allow-listed metadata keys participate in the canonical hash computation, while other metadata keys do not. It ensures that changes to non-allow-listed metadata do not affect the hash, while changes to allow-listed metadata result in a different hash. This behavior is important for maintaining stable hashes for records with irrelevant metadata changes, while still capturing meaningful changes in the allow-listed metadata.
    /// </summary>
    [Fact]
    public void AllowListedMetadataParticipatesInHashButOtherMetadataDoesNot()
    {
        var options = CanonicalPayloadOptions.Create(["safe"]);
        AuditLedgerRecord first = CreateAuditLedgerRecord(["alpha"], new Dictionary<string, string>
        {
            ["ignored"] = "one",
            ["safe"] = "included"
        });
        AuditLedgerRecord ignoredChange = CreateAuditLedgerRecord(["alpha"], new Dictionary<string, string>
        {
            ["ignored"] = "two",
            ["safe"] = "included"
        });
        AuditLedgerRecord allowedChange = CreateAuditLedgerRecord(["alpha"], new Dictionary<string, string>
        {
            ["ignored"] = "one",
            ["safe"] = "changed"
        });

        string firstHash = CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForAuditLedgerRecord(first, options)).HashValue;
        string ignoredChangeHash = CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForAuditLedgerRecord(ignoredChange, options)).HashValue;
        string allowedChangeHash = CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForAuditLedgerRecord(allowedChange, options)).HashValue;

        Assert.Equal(firstHash, ignoredChangeHash);
        Assert.NotEqual(firstHash, allowedChangeHash);
    }

    private static readonly string[] expected = ["alpha", "beta"];

    /// <summary>
    /// This test verifies that when an AuditResidue has a blank AuditResidueId, the canonical payload uses the EventId as the artifact identifier. It also checks that reason codes are normalized (trimmed and deduplicated) and that metadata is filtered according to the allow-list specified in the CanonicalPayloadOptions. The test ensures that the canonical payload correctly reflects these transformations and produces the expected artifact type and identifier.
    /// </summary>
    [Fact]
    public void AuditResiduePayloadUsesEventIdWhenResidueIdIsBlankAndNormalizesReasonCodesAndMetadata()
    {
        var options = CanonicalPayloadOptions.Create(["allowed", "safe"]);
        var residue = new TestAuditResidue
        {
            EventId = "event-fallback",
            AuditResidueId = "   ",
            OccurredUtc = new DateTimeOffset(2026, 6, 16, 8, 0, 0, TimeSpan.FromHours(-5)),
            ReasonCodes = [" beta ", "", "alpha", "beta", " alpha "],
            Metadata = new Dictionary<string, string>
            {
                [" unsafe "] = " ignored ",
                [" safe "] = " included ",
                ["allowed"] = " second "
            }
        };

        CanonicalPayload payload = CanonicalPayloadBuilder.ForAuditResidue(residue, options);

        Assert.Equal(CanonicalArtifactTypes.AuditResidue, payload.ArtifactType);
        Assert.Equal("event-fallback", payload.ArtifactId);

        using JsonDocument document = Parse(payload);
        JsonElement content = document.RootElement.GetProperty("content");
        JsonElement metadata = content.GetProperty("metadata");

        Assert.Equal("event-fallback", document.RootElement.GetProperty("artifactId").GetString());
        Assert.Equal("event-fallback", content.GetProperty("auditResidueId").GetString());
        Assert.Equal("2026-06-16T13:00:00.0000000Z", content.GetProperty("occurredUtc").GetString());
        Assert.Equal(expected, ReadStringArray(content.GetProperty("reasonCodes")));
        Assert.Equal("included", metadata.GetProperty("safe").GetString());
        Assert.Equal("second", metadata.GetProperty("allowed").GetString());
        Assert.False(metadata.TryGetProperty("unsafe", out _));
    }

    /// <summary>
    /// This test verifies that the canonical payload for an AuditResidueLifecycleEvent correctly preserves the stage, correlation ID, and occurred UTC timestamp, while also filtering metadata according to the allow-list specified in the CanonicalPayloadOptions. It ensures that the canonical payload reflects the expected artifact type and identifier, and that only allow-listed metadata keys are included in the final payload.
    /// </summary>
    [Fact]
    public void AuditResidueLifecyclePayloadPreservesStageCorrelationUtcAndFilteredMetadata()
    {
        var options = CanonicalPayloadOptions.Create(["safe"]);
        var lifecycleEvent = AuditResidueLifecycleEvent.Create(
            AuditResidueLifecycleStage.ExternalEmissionQueued,
            correlationId: " correlation-1 ",
            auditResidueId: " residue-1 ",
            eventId: " lifecycle-event-1 ",
            occurredUtc: new DateTimeOffset(2026, 6, 16, 8, 15, 16, 123, TimeSpan.FromHours(-5)),
            traceId: " trace-1 ",
            operationName: " gateway.execute ",
            outcome: " Queued ",
            metadata: new Dictionary<string, string>
            {
                [" safe "] = " lifecycle metadata ",
                ["unsafe"] = "ignored"
            });

        CanonicalPayload payload = CanonicalPayloadBuilder.ForAuditResidueLifecycleEvent(lifecycleEvent, options);

        Assert.Equal(CanonicalArtifactTypes.AuditResidueLifecycleEvent, payload.ArtifactType);
        Assert.Equal("lifecycle-event-1", payload.ArtifactId);

        using JsonDocument document = Parse(payload);
        JsonElement content = document.RootElement.GetProperty("content");
        JsonElement metadata = content.GetProperty("metadata");

        Assert.Equal("residue-1", content.GetProperty("auditResidueId").GetString());
        Assert.Equal("correlation-1", content.GetProperty("correlationId").GetString());
        Assert.Equal("lifecycle-event-1", content.GetProperty("eventId").GetString());
        Assert.Equal("2026-06-16T13:15:16.1230000Z", content.GetProperty("occurredUtc").GetString());
        Assert.Equal("gateway.execute", content.GetProperty("operationName").GetString());
        Assert.Equal("Queued", content.GetProperty("outcome").GetString());
        Assert.Equal("ExternalEmissionQueued", content.GetProperty("stage").GetString());
        Assert.Equal(500, content.GetProperty("stageSequence").GetInt32());
        Assert.Equal("trace-1", content.GetProperty("traceId").GetString());
        Assert.Equal("lifecycle metadata", metadata.GetProperty("safe").GetString());
        Assert.False(metadata.TryGetProperty("unsafe", out _));
    }

    /// <summary>
    /// This test verifies that the canonical payload for a GovernanceEmissionEnvelope correctly handles the presence of a payload, lifecycle fields, and metadata filtering. It ensures that the canonical payload reflects the expected artifact type and identifier, and that only allow-listed metadata keys are included in both the envelope and payload metadata. The test also checks that lifecycle fields such as event type, lifecycle stage, and timestamps are preserved in the canonical payload.
    /// </summary>
    [Fact]
    public void GovernanceEmissionEnvelopePayloadHandlesLifecycleFieldsPayloadPresenceAndMetadataFiltering()
    {
        var options = CanonicalPayloadOptions.Create(["safe"]);
        var emissionPayload = GovernanceEmissionPayload.Create(
            " audit-summary ",
            schemaVersion: " payload-v1 ",
            contentType: " application/json ",
            contentHash: " payload-hash ",
            sizeBytes: 128,
            metadata: new Dictionary<string, string>
            {
                [" safe "] = " payload metadata ",
                ["unsafe"] = "ignored"
            });
        var envelope = GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditLifecycle,
            eventId: " lifecycle-event-1 ",
            occurredUtc: new DateTimeOffset(2026, 6, 16, 8, 0, 0, TimeSpan.FromHours(-5)),
            envelopeId: " envelope-1 ",
            createdUtc: new DateTimeOffset(2026, 6, 16, 13, 0, 1, TimeSpan.Zero),
            correlationId: " correlation-1 ",
            auditResidueId: " residue-1 ",
            lifecycleStage: AuditResidueLifecycleStage.ExternalEmissionQueued,
            policyVersion: " policy-v1 ",
            policyHash: " policy-hash ",
            traceId: " trace-1 ",
            spanId: " span-1 ",
            parentSpanId: " parent-span-1 ",
            operationName: " gateway.execute ",
            outcome: " Queued ",
            actorId: " actor-1 ",
            emitterStatus: " queued ",
            emitterProvider: " provider-neutral ",
            outboxSequence: 7,
            gatewayExecutionId: " gateway-1 ",
            decisionStage: " outbox-queued ",
            payload: emissionPayload,
            metadata: new Dictionary<string, string>
            {
                [" safe "] = " envelope metadata ",
                ["unsafe"] = "ignored"
            });

        CanonicalPayload canonicalPayload = CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(envelope, options);

        Assert.Equal(CanonicalArtifactTypes.GovernanceEmissionEnvelope, canonicalPayload.ArtifactType);
        Assert.Equal("envelope-1", canonicalPayload.ArtifactId);

        using JsonDocument document = Parse(canonicalPayload);
        JsonElement content = document.RootElement.GetProperty("content");
        JsonElement payloadContent = content.GetProperty("payload");
        JsonElement metadata = content.GetProperty("metadata");
        JsonElement payloadMetadata = payloadContent.GetProperty("metadata");

        Assert.Equal("AuditLifecycle", content.GetProperty("eventType").GetString());
        Assert.Equal("ExternalEmissionQueued", content.GetProperty("lifecycleStage").GetString());
        Assert.Equal(500, content.GetProperty("lifecycleStageSequence").GetInt32());
        Assert.Equal("2026-06-16T13:00:00.0000000Z", content.GetProperty("occurredUtc").GetString());
        Assert.Equal("2026-06-16T13:00:01.0000000Z", content.GetProperty("createdUtc").GetString());
        Assert.Equal("payload metadata", payloadMetadata.GetProperty("safe").GetString());
        Assert.False(payloadMetadata.TryGetProperty("unsafe", out _));
        Assert.Equal("envelope metadata", metadata.GetProperty("safe").GetString());
        Assert.False(metadata.TryGetProperty("unsafe", out _));
        Assert.Equal("audit-summary", payloadContent.GetProperty("payloadType").GetString());
        Assert.Equal("payload-v1", payloadContent.GetProperty("schemaVersion").GetString());
        Assert.Equal("application/json", payloadContent.GetProperty("contentType").GetString());
        Assert.Equal("payload-hash", payloadContent.GetProperty("contentHash").GetString());
        Assert.Equal(128, payloadContent.GetProperty("sizeBytes").GetInt64());
    }

    /// <summary>
    /// This test verifies that when a GovernanceEmissionEnvelope is created without a payload and lifecycle fields, the canonical payload correctly represents these fields as null. It ensures that the canonicalization process handles omitted optional fields gracefully, resulting in a stable canonical payload that reflects the absence of these fields. The test checks that the payload and lifecycle fields are represented as null in the final canonical JSON output.
    /// </summary>
    [Fact]
    public void GovernanceEmissionEnvelopePayloadWritesNullPayloadAndLifecycleFieldsWhenOmitted()
    {
        var envelope = GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditResidue,
            eventId: "event-1",
            occurredUtc: new DateTimeOffset(2026, 6, 16, 8, 0, 0, TimeSpan.FromHours(-5)),
            envelopeId: "envelope-without-payload",
            createdUtc: new DateTimeOffset(2026, 6, 16, 13, 0, 1, TimeSpan.Zero));

        CanonicalPayload payload = CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(envelope);

        using JsonDocument document = Parse(payload);
        JsonElement content = document.RootElement.GetProperty("content");

        Assert.Equal(JsonValueKind.Null, content.GetProperty("payload").ValueKind);
        Assert.Equal(JsonValueKind.Null, content.GetProperty("lifecycleStage").ValueKind);
        Assert.Equal(JsonValueKind.Null, content.GetProperty("lifecycleStageSequence").ValueKind);
    }

    /// <summary>
    /// This test verifies that the canonical payload for a GovernanceOutboxEntry correctly handles error, retry, provider, dead letter, and metadata fields. It ensures that the canonical payload reflects the expected artifact type and identifier, and that only allow-listed metadata keys are included in the final payload.
    /// </summary>
    [Fact]
    public void GovernanceOutboxEntryPayloadHandlesErrorRetryProviderDeadLetterAndMetadataFields()
    {
        var options = CanonicalPayloadOptions.Create(["safe"]);
        GovernanceEmissionEnvelope envelope = CreateEmissionEnvelope("envelope-outbox", "payload-hash");
        var error = GovernanceEmissionError.Create(
            " provider.failure ",
            " Provider failed ",
            isRetryable: true,
            providerName: " provider-neutral ",
            providerErrorCode: " timeout ");
        var entry = GovernanceOutboxEntry.Restore(
            envelope,
            GovernanceEmissionStatus.RetryableFailure,
            outboxEntryId: " outbox-1 ",
            createdUtc: new DateTimeOffset(2026, 6, 16, 8, 0, 0, TimeSpan.FromHours(-5)),
            updatedUtc: new DateTimeOffset(2026, 6, 16, 8, 30, 0, TimeSpan.FromHours(-5)),
            retryCount: 2,
            maxRetryCount: 5,
            nextRetryUtc: new DateTimeOffset(2026, 6, 16, 9, 0, 0, TimeSpan.FromHours(-5)),
            lastError: error,
            providerName: " provider-neutral ",
            providerRecordId: " provider-record-1 ",
            deadLetterReason: " dead letter ",
            metadata: new Dictionary<string, string>
            {
                [" safe "] = " outbox metadata ",
                ["unsafe"] = "ignored"
            });

        CanonicalPayload payload = CanonicalPayloadBuilder.ForGovernanceOutboxEntry(entry, options);

        Assert.Equal(CanonicalArtifactTypes.GovernanceOutboxEntry, payload.ArtifactType);
        Assert.Equal("outbox-1", payload.ArtifactId);

        using JsonDocument document = Parse(payload);
        JsonElement content = document.RootElement.GetProperty("content");
        JsonElement lastError = content.GetProperty("lastError");
        JsonElement metadata = content.GetProperty("metadata");

        Assert.Equal("2026-06-16T13:00:00.0000000Z", content.GetProperty("createdUtc").GetString());
        Assert.Equal("2026-06-16T13:30:00.0000000Z", content.GetProperty("updatedUtc").GetString());
        Assert.Equal("2026-06-16T14:00:00.0000000Z", content.GetProperty("nextRetryUtc").GetString());
        Assert.Equal("dead letter", content.GetProperty("deadLetterReason").GetString());
        Assert.Equal("provider-neutral", content.GetProperty("providerName").GetString());
        Assert.Equal("provider-record-1", content.GetProperty("providerRecordId").GetString());
        Assert.Equal(2, content.GetProperty("retryCount").GetInt32());
        Assert.Equal(5, content.GetProperty("maxRetryCount").GetInt32());
        Assert.Equal("RetryableFailure", content.GetProperty("status").GetString());
        Assert.Equal("provider.failure", lastError.GetProperty("code").GetString());
        Assert.True(lastError.GetProperty("isRetryable").GetBoolean());
        Assert.Equal("Provider failed", lastError.GetProperty("message").GetString());
        Assert.Equal("provider-neutral", lastError.GetProperty("providerName").GetString());
        Assert.Equal("timeout", lastError.GetProperty("providerErrorCode").GetString());
        Assert.Equal("outbox metadata", metadata.GetProperty("safe").GetString());
        Assert.False(metadata.TryGetProperty("unsafe", out _));
    }

    /// <summary>
    /// This test verifies that when a GovernanceOutboxEntry is created without optional fields such as dead letter reason, last error, next retry timestamp, provider name, and provider record ID, the canonical payload correctly represents these fields as null. It ensures that the canonicalization process handles omitted optional fields gracefully, resulting in a stable canonical payload that reflects the absence of these fields. The test checks that the optional fields are represented as null in the final canonical JSON output.
    /// </summary>
    [Fact]
    public void GovernanceOutboxEntryPayloadWritesNullOptionalFieldsWhenAbsent()
    {
        GovernanceEmissionEnvelope envelope = CreateEmissionEnvelope("envelope-with-null-outbox-fields", "payload-hash");
        var entry = GovernanceOutboxEntry.Create(
            envelope,
            outboxEntryId: "outbox-with-null-fields",
            createdUtc: new DateTimeOffset(2026, 6, 16, 13, 0, 0, TimeSpan.Zero));

        CanonicalPayload payload = CanonicalPayloadBuilder.ForGovernanceOutboxEntry(entry);

        using JsonDocument document = Parse(payload);
        JsonElement content = document.RootElement.GetProperty("content");

        Assert.Equal(JsonValueKind.Null, content.GetProperty("deadLetterReason").ValueKind);
        Assert.Equal(JsonValueKind.Null, content.GetProperty("lastError").ValueKind);
        Assert.Equal(JsonValueKind.Null, content.GetProperty("nextRetryUtc").ValueKind);
        Assert.Equal(JsonValueKind.Null, content.GetProperty("providerName").ValueKind);
        Assert.Equal(JsonValueKind.Null, content.GetProperty("providerRecordId").ValueKind);
    }

    /// <summary>
    /// This test verifies that the canonical hash of a GovernanceEmissionEnvelope is stable when only non-allow-listed metadata changes, and that a meaningful change in the payload content results in a different hash.
    /// </summary>
    [Fact]
    public void EquivalentGovernanceEmissionPayloadsProduceStableHashAndMeaningfulPayloadChangeChangesHash()
    {
        var options = CanonicalPayloadOptions.Create(["safe"]);
        GovernanceEmissionEnvelope first = CreateEmissionEnvelope(
            "envelope-hash-stability",
            "payload-hash",
            payloadMetadata: new Dictionary<string, string>
            {
                ["safe"] = "included",
                ["ignored"] = "one"
            });
        GovernanceEmissionEnvelope ignoredChange = CreateEmissionEnvelope(
            "envelope-hash-stability",
            "payload-hash",
            payloadMetadata: new Dictionary<string, string>
            {
                ["ignored"] = "two",
                ["safe"] = "included"
            });
        GovernanceEmissionEnvelope meaningfulChange = CreateEmissionEnvelope(
            "envelope-hash-stability",
            "payload-hash-changed",
            payloadMetadata: new Dictionary<string, string>
            {
                ["safe"] = "included",
                ["ignored"] = "one"
            });

        string firstHash = CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(first, options)).HashValue;
        string ignoredChangeHash = CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(ignoredChange, options)).HashValue;
        string meaningfulChangeHash = CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(meaningfulChange, options)).HashValue;

        Assert.Equal(firstHash, ignoredChangeHash);
        Assert.NotEqual(firstHash, meaningfulChangeHash);
    }

    /// <summary>
    /// This test verifies that the canonical hash of a GovernanceOutboxEntry correctly binds the artifact type and identifier, ensuring that the hash reflects the unique identity of the outbox entry.
    /// </summary>
    [Fact]
    public void OutboxEntryHashBindsArtifactTypeAndIdentifier()
    {
        var options = CanonicalPayloadOptions.Create(["safe"]);
        var payload = GovernanceEmissionPayload.Create(
            "audit-summary",
            schemaVersion: "v1",
            contentType: "application/json",
            contentHash: "payload-hash",
            sizeBytes: 128,
            metadata: new Dictionary<string, string>
            {
                ["safe"] = "payload-metadata"
            });
        var envelope = GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditResidue,
            eventId: "event-1",
            occurredUtc: new DateTimeOffset(2026, 6, 16, 8, 0, 0, TimeSpan.FromHours(-5)),
            envelopeId: "envelope-1",
            createdUtc: new DateTimeOffset(2026, 6, 16, 13, 0, 1, TimeSpan.Zero),
            correlationId: "correlation-1",
            auditResidueId: "residue-1",
            policyVersion: "policy-v1",
            policyHash: "policy-hash",
            payload: payload,
            metadata: new Dictionary<string, string>
            {
                ["safe"] = "envelope-metadata"
            });
        var entry = GovernanceOutboxEntry.Create(
            envelope,
            outboxEntryId: "outbox-1",
            createdUtc: new DateTimeOffset(2026, 6, 16, 13, 0, 2, TimeSpan.Zero),
            metadata: new Dictionary<string, string>
            {
                ["safe"] = "outbox-metadata"
            });

        CanonicalPayload payloadToHash = CanonicalPayloadBuilder.ForGovernanceOutboxEntry(entry, options);
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payloadToHash);

        Assert.Equal(CanonicalArtifactTypes.GovernanceOutboxEntry, payloadToHash.ArtifactType);
        Assert.Equal("outbox-1", payloadToHash.ArtifactId);
        Assert.Equal(CanonicalPayloadOptions.DefaultHashAlgorithm, hash.HashAlgorithm);
        Assert.Equal(CanonicalPayloadOptions.DefaultCanonicalizationVersion, hash.CanonicalizationVersion);
        Assert.Contains("\"artifactType\":\"asibackbone.governance-outbox-entry\"", payloadToHash.CanonicalJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// This test verifies that the SigningMetadata generated from a CanonicalPayloadHash correctly carries the signing hash and hash algorithm, without implying that a signature is present. It ensures that the SigningMetadata reflects the correct artifact type and identifier, and that it indicates the absence of a signature.
    /// </summary>
    [Fact]
    public void HashMetadataCarriesSigningHashWithoutImplyingSignature()
    {
        AuditLedgerRecord record = CreateAuditLedgerRecord(["alpha"], new Dictionary<string, string>());
        CanonicalPayload payload = CanonicalPayloadBuilder.ForAuditLedgerRecord(record);
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);

        var signingMetadata = hash.ToSigningMetadata();

        Assert.Equal(hash.HashValue, signingMetadata.SigningHash);
        Assert.Equal(CanonicalPayloadOptions.DefaultHashAlgorithm, signingMetadata.HashAlgorithm);
        Assert.False(signingMetadata.HasSignature);
        Assert.False(signingMetadata.IsSigned);
        Assert.Equal(CanonicalArtifactTypes.AuditLedgerRecord, signingMetadata.Metadata["artifact_type"]);
        Assert.Equal("record-1", signingMetadata.Metadata["artifact_id"]);
    }

    /// <summary>
    /// Verifies that the algorithm selected during payload construction is retained and honored by the hasher.
    /// </summary>
    [Fact]
    public void ConfiguredSha512AlgorithmIsUsedByBuiltInHasher()
    {
        var options = CanonicalPayloadOptions.Create(hashAlgorithm: "sha512");
        AuditLedgerRecord record = CreateAuditLedgerRecord(["alpha"], new Dictionary<string, string>());

        CanonicalPayload payload = CanonicalPayloadBuilder.ForAuditLedgerRecord(record, options);
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);

        Assert.Equal("SHA-512", options.HashAlgorithm);
        Assert.Equal("SHA-512", payload.HashAlgorithm);
        Assert.Equal("SHA-512", hash.HashAlgorithm);
        Assert.Equal(128, hash.HashValue.Length);
    }

    /// <summary>
    /// Verifies that unsupported configured algorithms fail explicitly instead of falling back to SHA-256.
    /// </summary>
    [Fact]
    public void UnsupportedConfiguredHashAlgorithmFailsExplicitly()
    {
        var options = CanonicalPayloadOptions.Create(hashAlgorithm: "SHA-384");
        AuditLedgerRecord record = CreateAuditLedgerRecord(["alpha"], new Dictionary<string, string>());
        CanonicalPayload payload = CanonicalPayloadBuilder.ForAuditLedgerRecord(record, options);

        NotSupportedException exception = Assert.Throws<NotSupportedException>(() => CanonicalPayloadHasher.ComputeHash(payload));

        Assert.Contains("SHA-256 and SHA-512", exception.Message, StringComparison.Ordinal);
    }

    private static AuditLedgerRecord CreateAuditLedgerRecord(IEnumerable<string> reasonCodes, IReadOnlyDictionary<string, string> metadata)
    {
        var actor = AsiBackboneActorContext.Service("system-1", "System");
        var residue = AuditResidue.Create(
            actor,
            "gateway.execute",
            "Allowed",
            reasonCodes,
            eventId: "event-1",
            occurredUtc: new DateTimeOffset(2026, 6, 16, 8, 0, 0, TimeSpan.FromHours(-5)),
            correlationId: "correlation-1",
            traceId: "trace-1",
            policyVersion: "policy-v1",
            policyHash: "policy-hash",
            metadata: metadata,
            auditResidueId: "residue-1",
            spanId: "span-1",
            parentSpanId: "parent-span-1",
            decisionLatencyMs: 42,
            constraintSetHash: "constraint-hash",
            constraintCount: 2,
            riskScore: 0.25,
            policyScope: "regional",
            tenantHash: "tenant-hash",
            organizationHash: "organization-hash",
            emitterStatus: "queued",
            emitterProvider: "provider-neutral",
            outboxSequence: 7,
            gatewayExecutionId: "gateway-1",
            decisionStage: "policy-evaluated");

        return AuditLedgerRecord.FromResidue(
            residue,
            recordId: "record-1",
            recordedUtc: new DateTimeOffset(2026, 6, 16, 13, 0, 1, TimeSpan.Zero),
            handshakeId: "handshake-1",
            acknowledgmentId: "ack-1",
            capabilityTokenId: "capability-grant-1",
            previousRecordHash: "previous-hash");
    }

    private static GovernanceEmissionEnvelope CreateEmissionEnvelope(
        string envelopeId,
        string contentHash,
        IReadOnlyDictionary<string, string>? payloadMetadata = null)
    {
        var payload = GovernanceEmissionPayload.Create(
            "audit-summary",
            schemaVersion: "v1",
            contentType: "application/json",
            contentHash: contentHash,
            sizeBytes: 128,
            metadata: payloadMetadata ?? new Dictionary<string, string>());

        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditResidue,
            eventId: "event-1",
            occurredUtc: new DateTimeOffset(2026, 6, 16, 8, 0, 0, TimeSpan.FromHours(-5)),
            envelopeId: envelopeId,
            createdUtc: new DateTimeOffset(2026, 6, 16, 13, 0, 1, TimeSpan.Zero),
            correlationId: "correlation-1",
            auditResidueId: "residue-1",
            policyVersion: "policy-v1",
            policyHash: "policy-hash",
            payload: payload);
    }

    private static JsonDocument Parse(CanonicalPayload payload)
    {
        return JsonDocument.Parse(payload.CanonicalJson);
    }

    private static string[] ReadStringArray(JsonElement arrayElement)
    {
        return [.. arrayElement.EnumerateArray().Select(item => item.GetString() ?? string.Empty)];
    }

    private sealed class TestAuditResidue : IAsiBackboneAuditResidue
    {
        public string EventId { get; init; } = "event-1";

        public string? AuditResidueId { get; init; } = "residue-1";

        public string SchemaVersion { get; init; } = "asibackbone.stable-artifacts.v1";

        public DateTimeOffset OccurredUtc { get; init; } = new(2026, 6, 16, 13, 0, 0, TimeSpan.Zero);

        public string ActorId { get; init; } = "actor-1";

        public AsiBackboneActorType ActorType { get; init; } = AsiBackboneActorType.Service;

        public string? ActorDisplayName { get; init; } = "System";

        public string OperationName { get; init; } = "gateway.execute";

        public string Outcome { get; init; } = "Allowed";

        public IReadOnlyList<string> ReasonCodes { get; init; } = [];

        public string? CorrelationId { get; init; } = "correlation-1";

        public string? TraceId { get; init; } = "trace-1";

        public string? SpanId { get; init; } = "span-1";

        public string? ParentSpanId { get; init; } = "parent-span-1";

        public long? DecisionLatencyMs { get; init; } = 42;

        public string? ConstraintSetHash { get; init; } = "constraint-hash";

        public int? ConstraintCount { get; init; } = 2;

        public double? RiskScore { get; init; } = 0.25;

        public string? PolicyScope { get; init; } = "regional";

        public string? TenantHash { get; init; } = "tenant-hash";

        public string? OrganizationHash { get; init; } = "organization-hash";

        public string? EmitterStatus { get; init; } = "queued";

        public string? EmitterProvider { get; init; } = "provider-neutral";

        public long? OutboxSequence { get; init; } = 7;

        public string? GatewayExecutionId { get; init; } = "gateway-1";

        public string? DecisionStage { get; init; } = "policy-evaluated";

        public string? PolicyVersion { get; init; } = "policy-v1";

        public string? PolicyHash { get; init; } = "policy-hash";

        public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>();
    }
}
