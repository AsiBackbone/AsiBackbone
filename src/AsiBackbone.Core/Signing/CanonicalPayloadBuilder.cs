using System.Globalization;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Core.Serialization;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Builds deterministic, provider-neutral signing payloads for AsiBackbone governance artifacts.
/// </summary>
public static class CanonicalPayloadBuilder
{
    /// <summary>
    /// Builds a canonical payload for audit residue.
    /// </summary>
    public static CanonicalPayload ForAuditResidue(IAsiBackboneAuditResidue residue, CanonicalPayloadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(residue);
        CanonicalPayloadOptions effectiveOptions = options ?? CanonicalPayloadOptions.Default;
        string auditResidueId = GetAuditResidueId(residue);

        return CanonicalPayload.Create(
            CanonicalArtifactTypes.AuditResidue,
            auditResidueId,
            residue.SchemaVersion,
            effectiveOptions.CanonicalizationVersion,
            BuildAuditResidueContent(residue, effectiveOptions, auditResidueId));
    }

    /// <summary>
    /// Builds a canonical payload for a persistence-ready audit ledger record.
    /// </summary>
    public static CanonicalPayload ForAuditLedgerRecord(AuditLedgerRecord record, CanonicalPayloadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        CanonicalPayloadOptions effectiveOptions = options ?? CanonicalPayloadOptions.Default;

        SortedDictionary<string, object?> content = BuildAuditResidueContent(record, effectiveOptions, record.AuditResidueId);
        content["acknowledgmentId"] = record.AcknowledgmentId;
        content["capabilityGrantId"] = record.CapabilityTokenId;
        content["handshakeId"] = record.HandshakeId;
        content["previousRecordHash"] = record.PreviousRecordHash;
        content["recordedUtc"] = FormatUtc(record.RecordedUtc);
        content["recordId"] = record.RecordId;

        return CanonicalPayload.Create(
            CanonicalArtifactTypes.AuditLedgerRecord,
            record.RecordId,
            record.SchemaVersion,
            effectiveOptions.CanonicalizationVersion,
            content);
    }

    /// <summary>
    /// Builds a canonical payload for an audit residue lifecycle event.
    /// </summary>
    public static CanonicalPayload ForAuditResidueLifecycleEvent(AuditResidueLifecycleEvent lifecycleEvent, CanonicalPayloadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(lifecycleEvent);
        CanonicalPayloadOptions effectiveOptions = options ?? CanonicalPayloadOptions.Default;

        SortedDictionary<string, object?> content = new(StringComparer.Ordinal)
        {
            ["auditResidueId"] = lifecycleEvent.AuditResidueId,
            ["correlationId"] = lifecycleEvent.CorrelationId,
            ["eventId"] = lifecycleEvent.EventId,
            ["metadata"] = FilterMetadata(lifecycleEvent.Metadata, effectiveOptions),
            ["occurredUtc"] = FormatUtc(lifecycleEvent.OccurredUtc),
            ["operationName"] = lifecycleEvent.OperationName,
            ["outcome"] = lifecycleEvent.Outcome,
            ["stage"] = lifecycleEvent.Stage.ToString(),
            ["stageSequence"] = lifecycleEvent.StageSequence,
            ["traceId"] = lifecycleEvent.TraceId
        };

        return CanonicalPayload.Create(
            CanonicalArtifactTypes.AuditResidueLifecycleEvent,
            lifecycleEvent.EventId,
            AsiBackboneSchemaVersions.StableArtifactsV1,
            effectiveOptions.CanonicalizationVersion,
            content);
    }

    /// <summary>
    /// Builds a canonical payload for a governance emission envelope.
    /// </summary>
    public static CanonicalPayload ForGovernanceEmissionEnvelope(GovernanceEmissionEnvelope envelope, CanonicalPayloadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        CanonicalPayloadOptions effectiveOptions = options ?? CanonicalPayloadOptions.Default;

        return CanonicalPayload.Create(
            CanonicalArtifactTypes.GovernanceEmissionEnvelope,
            envelope.EnvelopeId,
            envelope.SchemaVersion,
            effectiveOptions.CanonicalizationVersion,
            BuildGovernanceEmissionEnvelopeContent(envelope, effectiveOptions));
    }

    /// <summary>
    /// Builds a canonical payload for a durable governance outbox entry.
    /// </summary>
    public static CanonicalPayload ForGovernanceOutboxEntry(GovernanceOutboxEntry entry, CanonicalPayloadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(entry);
        CanonicalPayloadOptions effectiveOptions = options ?? CanonicalPayloadOptions.Default;

        SortedDictionary<string, object?> content = new(StringComparer.Ordinal)
        {
            ["createdUtc"] = FormatUtc(entry.CreatedUtc),
            ["deadLetterReason"] = entry.DeadLetterReason,
            ["envelope"] = BuildGovernanceEmissionEnvelopeContent(entry.Envelope, effectiveOptions),
            ["lastError"] = BuildGovernanceEmissionErrorContent(entry.LastError),
            ["maxRetryCount"] = entry.MaxRetryCount,
            ["metadata"] = FilterMetadata(entry.Metadata, effectiveOptions),
            ["nextRetryUtc"] = FormatUtc(entry.NextRetryUtc),
            ["outboxEntryId"] = entry.OutboxEntryId,
            ["providerName"] = entry.ProviderName,
            ["providerRecordId"] = entry.ProviderRecordId,
            ["retryCount"] = entry.RetryCount,
            ["status"] = entry.Status.ToString(),
            ["updatedUtc"] = FormatUtc(entry.UpdatedUtc)
        };

        return CanonicalPayload.Create(
            CanonicalArtifactTypes.GovernanceOutboxEntry,
            entry.OutboxEntryId,
            entry.Envelope.SchemaVersion,
            effectiveOptions.CanonicalizationVersion,
            content);
    }

    /// <summary>
    /// Builds a canonical payload for a capability token grant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every field the grant carries is included, so the resulting hash binds the whole grant rather than a subset of
    /// it. A payload that omits a field leaves that field outside the proof while
    /// <see cref="CapabilityGrantValidator" /> still enforces it, which lets a modified value pass
    /// validation against a signature computed before the change. Scopes are normalized to a sorted, de-duplicated,
    /// ordinal set, so two grants that differ only in scope ordering produce the same hash.
    /// </para>
    /// <para>
    /// Metadata is the one exception, and deliberately so: it is filtered through
    /// <see cref="CanonicalPayloadOptions.AllowsMetadataKey" />, whose allow-list is empty by default. With default
    /// options no grant metadata reaches the proof at all, which keeps unbounded and potentially sensitive host data
    /// out of hashed payloads. A host that puts security-relevant data in grant metadata has no binding for it until
    /// that key is added to the allow-list.
    /// </para>
    /// </remarks>
    /// <param name="grant">The capability token grant to canonicalize.</param>
    /// <param name="options">Canonicalization options, including the metadata allow-list.</param>
    /// <returns>A deterministic canonical payload for the grant.</returns>
    public static CanonicalPayload ForCapabilityTokenGrant(CapabilityTokenGrant grant, CanonicalPayloadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(grant);
        CanonicalPayloadOptions effectiveOptions = options ?? CanonicalPayloadOptions.Default;

        SortedDictionary<string, object?> content = new(StringComparer.Ordinal)
        {
            ["acknowledgmentId"] = grant.AcknowledgmentId,
            ["audience"] = grant.Audience,
            ["expiresUtc"] = FormatUtc(grant.ExpiresUtc),
            ["gatewayBinding"] = grant.GatewayBinding,
            ["handshakeId"] = grant.HandshakeId,
            ["issuedUtc"] = FormatUtc(grant.IssuedUtc),
            ["issuer"] = grant.Issuer,
            ["metadata"] = FilterMetadata(grant.Metadata, effectiveOptions),
            ["notBeforeUtc"] = FormatUtc(grant.NotBeforeUtc),
            ["operationName"] = grant.OperationName,
            ["policyHash"] = grant.PolicyHash,
            ["policyVersion"] = grant.PolicyVersion,
            ["resourceBinding"] = grant.ResourceBinding,
            ["scopes"] = NormalizeStringSet(grant.Scopes),
            ["subjectId"] = grant.SubjectId,
            ["tokenId"] = grant.TokenId
        };

        return CanonicalPayload.Create(
            CanonicalArtifactTypes.CapabilityTokenGrant,
            grant.TokenId,
            grant.SchemaVersion,
            effectiveOptions.CanonicalizationVersion,
            content);
    }

    private static SortedDictionary<string, object?> BuildAuditResidueContent(
        IAsiBackboneAuditResidue residue,
        CanonicalPayloadOptions options,
        string auditResidueId)
    {
        return new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["actorDisplayName"] = residue.ActorDisplayName,
            ["actorId"] = residue.ActorId,
            ["actorType"] = residue.ActorType.ToString(),
            ["auditResidueId"] = auditResidueId,
            ["constraintCount"] = residue.ConstraintCount,
            ["constraintSetHash"] = residue.ConstraintSetHash,
            ["correlationId"] = residue.CorrelationId,
            ["decisionLatencyMs"] = residue.DecisionLatencyMs,
            ["decisionStage"] = residue.DecisionStage,
            ["emitterProvider"] = residue.EmitterProvider,
            ["emitterStatus"] = residue.EmitterStatus,
            ["eventId"] = residue.EventId,
            ["gatewayExecutionId"] = residue.GatewayExecutionId,
            ["metadata"] = FilterMetadata(residue.Metadata, options),
            ["occurredUtc"] = FormatUtc(residue.OccurredUtc),
            ["operationName"] = residue.OperationName,
            ["organizationHash"] = residue.OrganizationHash,
            ["outboxSequence"] = residue.OutboxSequence,
            ["outcome"] = residue.Outcome,
            ["parentSpanId"] = residue.ParentSpanId,
            ["policyHash"] = residue.PolicyHash,
            ["policyScope"] = residue.PolicyScope,
            ["policyVersion"] = residue.PolicyVersion,
            ["reasonCodes"] = NormalizeStringSet(residue.ReasonCodes),
            ["riskScore"] = residue.RiskScore,
            ["schemaVersion"] = residue.SchemaVersion,
            ["spanId"] = residue.SpanId,
            ["tenantHash"] = residue.TenantHash,
            ["traceId"] = residue.TraceId
        };
    }

    private static SortedDictionary<string, object?> BuildGovernanceEmissionEnvelopeContent(GovernanceEmissionEnvelope envelope, CanonicalPayloadOptions options)
    {
        return new SortedDictionary<string, object?>(StringComparer.Ordinal)
        {
            ["actorId"] = envelope.ActorId,
            ["auditResidueId"] = envelope.AuditResidueId,
            ["correlationId"] = envelope.CorrelationId,
            ["createdUtc"] = FormatUtc(envelope.CreatedUtc),
            ["decisionStage"] = envelope.DecisionStage,
            ["emitterProvider"] = envelope.EmitterProvider,
            ["emitterStatus"] = envelope.EmitterStatus,
            ["envelopeId"] = envelope.EnvelopeId,
            ["eventId"] = envelope.EventId,
            ["eventType"] = envelope.EventType.ToString(),
            ["gatewayExecutionId"] = envelope.GatewayExecutionId,
            ["lifecycleStage"] = envelope.LifecycleStage?.ToString(),
            ["lifecycleStageSequence"] = envelope.LifecycleStageSequence,
            ["metadata"] = FilterMetadata(envelope.Metadata, options),
            ["occurredUtc"] = FormatUtc(envelope.OccurredUtc),
            ["operationName"] = envelope.OperationName,
            ["outboxSequence"] = envelope.OutboxSequence,
            ["outcome"] = envelope.Outcome,
            ["parentSpanId"] = envelope.ParentSpanId,
            ["payload"] = BuildGovernanceEmissionPayloadContent(envelope.Payload, options),
            ["policyHash"] = envelope.PolicyHash,
            ["policyVersion"] = envelope.PolicyVersion,
            ["schemaVersion"] = envelope.SchemaVersion,
            ["spanId"] = envelope.SpanId,
            ["traceId"] = envelope.TraceId
        };
    }

    private static SortedDictionary<string, object?>? BuildGovernanceEmissionPayloadContent(GovernanceEmissionPayload? payload, CanonicalPayloadOptions options)
    {
        return payload is null
            ? null
            : new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["contentHash"] = payload.ContentHash,
                ["contentType"] = payload.ContentType,
                ["metadata"] = FilterMetadata(payload.Metadata, options),
                ["payloadType"] = payload.PayloadType,
                ["schemaVersion"] = payload.SchemaVersion,
                ["sizeBytes"] = payload.SizeBytes
            };
    }

    private static SortedDictionary<string, object?>? BuildGovernanceEmissionErrorContent(GovernanceEmissionError? error)
    {
        return error is null
            ? null
            : new SortedDictionary<string, object?>(StringComparer.Ordinal)
            {
                ["code"] = error.Code,
                ["isRetryable"] = error.IsRetryable,
                ["message"] = error.Message,
                ["providerErrorCode"] = error.ProviderErrorCode,
                ["providerName"] = error.ProviderName
            };
    }

    private static SortedDictionary<string, object?> FilterMetadata(IReadOnlyDictionary<string, string>? metadata, CanonicalPayloadOptions options)
    {
        SortedDictionary<string, object?> filteredMetadata = new(StringComparer.Ordinal);

        if (metadata is null || metadata.Count == 0)
        {
            return filteredMetadata;
        }

        foreach (KeyValuePair<string, string> item in metadata)
        {
            if (!options.AllowsMetadataKey(item.Key))
            {
                continue;
            }

            filteredMetadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
        }

        return filteredMetadata;
    }

    private static string[] NormalizeStringSet(IEnumerable<string> values)
    {
        return [.. values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)];
    }

    private static string? FormatUtc(DateTimeOffset? timestamp)
    {
        return timestamp.HasValue ? FormatUtc(timestamp.Value) : null;
    }

    private static string FormatUtc(DateTimeOffset timestamp)
    {
        return timestamp.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'", CultureInfo.InvariantCulture);
    }

    private static string GetAuditResidueId(IAsiBackboneAuditResidue residue)
    {
        return string.IsNullOrWhiteSpace(residue.AuditResidueId)
            ? residue.EventId
            : residue.AuditResidueId;
    }
}
