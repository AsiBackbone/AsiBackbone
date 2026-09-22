using System.Collections.ObjectModel;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Serialization;
using SigningMetadataValue = AsiBackbone.Core.Signing.SigningMetadata;

namespace AsiBackbone.Core.Audit;

/// <summary>
/// Represents a persistence-ready audit ledger record captured from AsiBackbone decision receipt.
/// </summary>
public sealed class AuditLedgerRecord : IDecisionReceipt
{
    private static readonly ReadOnlyCollection<string> EmptyReasonCodes =
        Array.AsReadOnly(Array.Empty<string>());

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    private AuditLedgerRecord(
        string recordId,
        string? schemaVersion,
        string eventId,
        string? decisionReceiptId,
        DateTimeOffset occurredUtc,
        DateTimeOffset recordedUtc,
        string actorId,
        GovernanceActorType actorType,
        string? actorDisplayName,
        string operationName,
        string outcome,
        IReadOnlyList<string> reasonCodes,
        string? correlationId,
        string? traceId,
        string? spanId,
        string? parentSpanId,
        long? decisionLatencyMs,
        string? constraintSetHash,
        int? constraintCount,
        double? riskScore,
        string? policyScope,
        string? tenantHash,
        string? organizationHash,
        string? emitterStatus,
        string? emitterProvider,
        long? outboxSequence,
        string? gatewayExecutionId,
        string? decisionStage,
        string? policyVersion,
        string? policyHash,
        string? handshakeId,
        string? acknowledgmentId,
        string? capabilityTokenId,
        string? previousRecordHash,
        string? recordHash,
        string? signatureKeyId,
        string? signatureAlgorithm,
        string? signatureValue,
        string? signingHash,
        string? signatureKeyVersion,
        string? signatureProvider,
        DateTimeOffset? signedUtc,
        IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(recordId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        RecordId = recordId.Trim();
        SchemaVersion = GovernanceSchemaVersions.Normalize(schemaVersion);
        EventId = eventId.Trim();
        DecisionReceiptId = NormalizeOptional(decisionReceiptId) ?? EventId;
        OccurredUtc = occurredUtc.ToUniversalTime();
        RecordedUtc = recordedUtc.ToUniversalTime();
        ActorId = actorId.Trim();
        ActorType = actorType;
        ActorDisplayName = NormalizeOptional(actorDisplayName);
        OperationName = operationName.Trim();
        Outcome = outcome.Trim();
        ReasonCodes = reasonCodes;
        CorrelationId = NormalizeOptional(correlationId);
        TraceId = NormalizeOptional(traceId);
        SpanId = NormalizeOptional(spanId);
        ParentSpanId = NormalizeOptional(parentSpanId);
        DecisionLatencyMs = NormalizeNonNegative(decisionLatencyMs, nameof(decisionLatencyMs));
        ConstraintSetHash = NormalizeOptional(constraintSetHash);
        ConstraintCount = NormalizeNonNegative(constraintCount, nameof(constraintCount));
        RiskScore = NormalizeRiskScore(riskScore);
        PolicyScope = NormalizeOptional(policyScope);
        TenantHash = NormalizeOptional(tenantHash);
        OrganizationHash = NormalizeOptional(organizationHash);
        EmitterStatus = NormalizeOptional(emitterStatus);
        EmitterProvider = NormalizeOptional(emitterProvider);
        OutboxSequence = NormalizeNonNegative(outboxSequence, nameof(outboxSequence));
        GatewayExecutionId = NormalizeOptional(gatewayExecutionId);
        DecisionStage = NormalizeOptional(decisionStage);
        PolicyVersion = NormalizeOptional(policyVersion);
        PolicyHash = NormalizeOptional(policyHash);
        HandshakeId = NormalizeOptional(handshakeId);
        AcknowledgmentId = NormalizeOptional(acknowledgmentId);
        CapabilityTokenId = NormalizeOptional(capabilityTokenId);
        PreviousRecordHash = NormalizeOptional(previousRecordHash);
        RecordHash = NormalizeOptional(recordHash);
        SignatureKeyId = NormalizeOptional(signatureKeyId);
        SignatureAlgorithm = NormalizeOptional(signatureAlgorithm);
        SignatureValue = NormalizeOptional(signatureValue);
        SigningHash = NormalizeOptional(signingHash);
        SignatureKeyVersion = NormalizeOptional(signatureKeyVersion);
        SignatureProvider = NormalizeOptional(signatureProvider);
        SignedUtc = signedUtc?.ToUniversalTime();
        SigningMetadata = SigningMetadataValue.Create(
            SigningHash,
            null,
            SignatureValue,
            SignatureAlgorithm,
            SignatureKeyId,
            SignatureKeyVersion,
            SignatureProvider,
            SignedUtc);
        Metadata = metadata;
    }

    public string RecordId { get; }

    public string SchemaVersion { get; }

    public string EventId { get; }

    public string DecisionReceiptId { get; }

    public DateTimeOffset OccurredUtc { get; }

    public DateTimeOffset RecordedUtc { get; }

    public string ActorId { get; }

    public GovernanceActorType ActorType { get; }

    public string? ActorDisplayName { get; }

    public string OperationName { get; }

    public string Outcome { get; }

    public IReadOnlyList<string> ReasonCodes { get; }

    public string? CorrelationId { get; }

    public string? TraceId { get; }

    public string? SpanId { get; }

    public string? ParentSpanId { get; }

    public long? DecisionLatencyMs { get; }

    public string? ConstraintSetHash { get; }

    public int? ConstraintCount { get; }

    public double? RiskScore { get; }

    public string? PolicyScope { get; }

    public string? TenantHash { get; }

    public string? OrganizationHash { get; }

    public string? EmitterStatus { get; }

    public string? EmitterProvider { get; }

    public long? OutboxSequence { get; }

    public string? GatewayExecutionId { get; }

    public string? DecisionStage { get; }

    public string? PolicyVersion { get; }

    public string? PolicyHash { get; }

    public string? HandshakeId { get; }

    public string? AcknowledgmentId { get; }

    public string? CapabilityTokenId { get; }

    public string? PreviousRecordHash { get; }

    public string? RecordHash { get; }

    public string? SigningHash { get; }

    public string? SignatureKeyId { get; }

    public string? SignatureKeyVersion { get; }

    public string? SignatureAlgorithm { get; }

    public string? SignatureValue { get; }

    public string? SignatureProvider { get; }

    public DateTimeOffset? SignedUtc { get; }

    public SigningMetadataValue SigningMetadata { get; }

    public IReadOnlyDictionary<string, string> Metadata { get; }

    public bool HasReasonCodes => ReasonCodes.Count > 0;

    public bool HasMetadata => Metadata.Count > 0;

    public static AuditLedgerRecord FromDecisionReceipt(
        IDecisionReceipt receipt,
        string? recordId = null,
        DateTimeOffset? recordedUtc = null,
        string? handshakeId = null,
        string? acknowledgmentId = null,
        string? capabilityTokenId = null,
        string? previousRecordHash = null,
        string? recordHash = null,
        string? signatureKeyId = null,
        string? signatureAlgorithm = null,
        string? signatureValue = null,
        string? signingHash = null,
        string? signatureKeyVersion = null,
        string? signatureProvider = null,
        DateTimeOffset? signedUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? schemaVersion = null)
    {
        ArgumentNullException.ThrowIfNull(receipt);

        return new AuditLedgerRecord(
            NormalizeIdentifier(recordId),
            schemaVersion ?? receipt.SchemaVersion,
            receipt.EventId,
            receipt.DecisionReceiptId,
            receipt.OccurredUtc,
            recordedUtc ?? DateTimeOffset.UtcNow,
            receipt.ActorId,
            receipt.ActorType,
            receipt.ActorDisplayName,
            receipt.OperationName,
            receipt.Outcome,
            NormalizeReasonCodes(receipt.ReasonCodes),
            receipt.CorrelationId,
            receipt.TraceId,
            receipt.SpanId,
            receipt.ParentSpanId,
            receipt.DecisionLatencyMs,
            receipt.ConstraintSetHash,
            receipt.ConstraintCount,
            receipt.RiskScore,
            receipt.PolicyScope,
            receipt.TenantHash,
            receipt.OrganizationHash,
            receipt.EmitterStatus,
            receipt.EmitterProvider,
            receipt.OutboxSequence,
            receipt.GatewayExecutionId,
            receipt.DecisionStage,
            receipt.PolicyVersion,
            receipt.PolicyHash,
            handshakeId,
            acknowledgmentId,
            capabilityTokenId,
            previousRecordHash,
            recordHash,
            signatureKeyId,
            signatureAlgorithm,
            signatureValue,
            signingHash,
            signatureKeyVersion,
            signatureProvider,
            signedUtc,
            NormalizeMetadata(receipt.Metadata, metadata));
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

    private static long? NormalizeNonNegative(long? value, string parameterName)
    {
        return value < 0
            ? throw new ArgumentOutOfRangeException(parameterName, value, "Value must be greater than or equal to zero.")
            : value;
    }

    private static int? NormalizeNonNegative(int? value, string parameterName)
    {
        return value < 0
            ? throw new ArgumentOutOfRangeException(parameterName, value, "Value must be greater than or equal to zero.")
            : value;
    }

    private static double? NormalizeRiskScore(double? riskScore)
    {
        return riskScore is null
            ? null
            : double.IsNaN(riskScore.Value) || double.IsInfinity(riskScore.Value) || riskScore.Value < 0
            ? throw new ArgumentOutOfRangeException(nameof(riskScore), riskScore, "Risk score must be a finite value greater than or equal to zero.")
            : riskScore;
    }

    private static ReadOnlyCollection<string> NormalizeReasonCodes(IEnumerable<string>? reasonCodes)
    {
        string[] normalizedReasonCodes = reasonCodes?
            .Where(reasonCode => !string.IsNullOrWhiteSpace(reasonCode))
            .Select(reasonCode => reasonCode.Trim())
            .ToArray() ?? [];

        return normalizedReasonCodes.Length == 0
            ? EmptyReasonCodes
            : Array.AsReadOnly(normalizedReasonCodes);
    }

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(
        params IReadOnlyDictionary<string, string>?[] metadataSets)
    {
        Dictionary<string, string> normalizedMetadata = new(StringComparer.Ordinal);

        foreach (IReadOnlyDictionary<string, string>? metadata in metadataSets)
        {
            if (metadata is null || metadata.Count == 0)
            {
                continue;
            }

            foreach (KeyValuePair<string, string> item in metadata)
            {
                if (string.IsNullOrWhiteSpace(item.Key))
                {
                    continue;
                }

                normalizedMetadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
            }
        }

        return normalizedMetadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(normalizedMetadata);
    }
}
