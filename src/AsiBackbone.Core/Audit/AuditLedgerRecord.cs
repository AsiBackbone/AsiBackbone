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

    /// <summary>
    /// Gets the unique identifier of this ledger record.
    /// </summary>
    /// <remarks>
    /// A new identifier is generated when none is supplied to <see cref="FromDecisionReceipt" />.
    /// </remarks>
    public string RecordId { get; }

    /// <summary>
    /// Gets the normalized serialized schema version for the record shape.
    /// </summary>
    public string SchemaVersion { get; }

    /// <summary>
    /// Gets the stable audit event identifier copied from the source decision receipt.
    /// </summary>
    public string EventId { get; }

    /// <summary>
    /// Gets the decision receipt identifier copied from the source receipt.
    /// </summary>
    /// <remarks>
    /// Falls back to <see cref="EventId" /> when the source receipt does not supply a distinct identifier, so this
    /// value is never <see langword="null" />.
    /// </remarks>
    public string DecisionReceiptId { get; }

    /// <summary>
    /// Gets the UTC timestamp when the audited event occurred.
    /// </summary>
    public DateTimeOffset OccurredUtc { get; }

    /// <summary>
    /// Gets the UTC timestamp when this record was captured for the ledger.
    /// </summary>
    public DateTimeOffset RecordedUtc { get; }

    /// <summary>
    /// Gets the stable actor identifier associated with the event.
    /// </summary>
    public string ActorId { get; }

    /// <summary>
    /// Gets the actor type associated with the event.
    /// </summary>
    public GovernanceActorType ActorType { get; }

    /// <summary>
    /// Gets the optional display name or label associated with the actor.
    /// </summary>
    public string? ActorDisplayName { get; }

    /// <summary>
    /// Gets the operation name associated with the audited event.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets the governance, constraint, or host-defined outcome associated with the event.
    /// </summary>
    public string Outcome { get; }

    /// <summary>
    /// Gets the trimmed, non-empty machine-readable reason codes associated with the event.
    /// </summary>
    public IReadOnlyList<string> ReasonCodes { get; }

    /// <summary>
    /// Gets the correlation identifier associated with the event, when supplied by the host.
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// Gets the trace identifier associated with the event, when supplied by the host.
    /// </summary>
    public string? TraceId { get; }

    /// <summary>
    /// Gets the span identifier associated with the event, when supplied by the host or observability adapter.
    /// </summary>
    public string? SpanId { get; }

    /// <summary>
    /// Gets the parent span identifier associated with the event, when supplied by the host or observability adapter.
    /// </summary>
    public string? ParentSpanId { get; }

    /// <summary>
    /// Gets the non-negative decision latency in milliseconds, when supplied by the host.
    /// </summary>
    public long? DecisionLatencyMs { get; }

    /// <summary>
    /// Gets the hash of the evaluated constraint set, when supplied by the host.
    /// </summary>
    public string? ConstraintSetHash { get; }

    /// <summary>
    /// Gets the non-negative number of constraints evaluated for the decision, when supplied by the host.
    /// </summary>
    public int? ConstraintCount { get; }

    /// <summary>
    /// Gets the finite, non-negative host-defined risk score associated with the decision, when supplied by the host.
    /// </summary>
    public double? RiskScore { get; }

    /// <summary>
    /// Gets the policy scope associated with the decision, when supplied by the host.
    /// </summary>
    public string? PolicyScope { get; }

    /// <summary>
    /// Gets the privacy-preserving tenant hash associated with the decision, when supplied by the host.
    /// </summary>
    public string? TenantHash { get; }

    /// <summary>
    /// Gets the privacy-preserving organization hash associated with the decision, when supplied by the host.
    /// </summary>
    public string? OrganizationHash { get; }

    /// <summary>
    /// Gets the provider-neutral emitter status, when supplied by the host or outbox provider.
    /// </summary>
    public string? EmitterStatus { get; }

    /// <summary>
    /// Gets the provider-neutral emitter provider name, when supplied by the host or outbox provider.
    /// </summary>
    public string? EmitterProvider { get; }

    /// <summary>
    /// Gets the non-negative outbox sequence associated with the event, when supplied by the host or outbox provider.
    /// </summary>
    public long? OutboxSequence { get; }

    /// <summary>
    /// Gets the gateway execution identifier associated with the event, when supplied by the host or gateway provider.
    /// </summary>
    public string? GatewayExecutionId { get; }

    /// <summary>
    /// Gets the provider-neutral decision stage associated with the event, when supplied by the host.
    /// </summary>
    public string? DecisionStage { get; }

    /// <summary>
    /// Gets the policy version associated with the event, when supplied by the host.
    /// </summary>
    public string? PolicyVersion { get; }

    /// <summary>
    /// Gets the policy hash associated with the event, when supplied by the host.
    /// </summary>
    public string? PolicyHash { get; }

    /// <summary>
    /// Gets the liability handshake identifier linked to this record, when supplied.
    /// </summary>
    public string? HandshakeId { get; }

    /// <summary>
    /// Gets the handshake acknowledgment identifier linked to this record, when supplied.
    /// </summary>
    public string? AcknowledgmentId { get; }

    /// <summary>
    /// Gets the capability token or grant identifier linked to this record, when supplied.
    /// </summary>
    public string? CapabilityTokenId { get; }

    /// <summary>
    /// Gets the hash of the preceding ledger record, when the host chains records for tamper evidence.
    /// </summary>
    public string? PreviousRecordHash { get; }

    /// <summary>
    /// Gets the hash of this ledger record, when computed by the host or ledger store.
    /// </summary>
    public string? RecordHash { get; }

    /// <summary>
    /// Gets the hash that was signed or is intended to be signed, when supplied by the host or signing provider.
    /// </summary>
    public string? SigningHash { get; }

    /// <summary>
    /// Gets the identifier of the key used to sign the record, when signed.
    /// </summary>
    public string? SignatureKeyId { get; }

    /// <summary>
    /// Gets the version of the key used to sign the record, when supplied.
    /// </summary>
    public string? SignatureKeyVersion { get; }

    /// <summary>
    /// Gets the signature algorithm used to sign the record, when signed.
    /// </summary>
    public string? SignatureAlgorithm { get; }

    /// <summary>
    /// Gets the encoded signature value for the record, when signed.
    /// </summary>
    public string? SignatureValue { get; }

    /// <summary>
    /// Gets the name of the signing provider that produced the signature, when supplied.
    /// </summary>
    public string? SignatureProvider { get; }

    /// <summary>
    /// Gets the UTC timestamp when the record was signed, when supplied.
    /// </summary>
    public DateTimeOffset? SignedUtc { get; }

    /// <summary>
    /// Gets the provider-neutral signing metadata assembled from the individual signature properties on this record.
    /// </summary>
    public SigningMetadataValue SigningMetadata { get; }

    /// <summary>
    /// Gets the framework-neutral audit metadata, merged from the source receipt and any additional metadata supplied
    /// when the record was created.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether the record carries at least one reason code.
    /// </summary>
    public bool HasReasonCodes => ReasonCodes.Count > 0;

    /// <summary>
    /// Gets a value indicating whether the record carries at least one metadata entry.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Creates a persistence-ready ledger record from a decision receipt, attaching optional handshake, capability,
    /// hash-chain, and signature references.
    /// </summary>
    /// <param name="receipt">The decision receipt to capture.</param>
    /// <param name="recordId">The ledger record identifier; a new identifier is generated when omitted or blank.</param>
    /// <param name="recordedUtc">The capture timestamp; defaults to the current UTC time.</param>
    /// <param name="handshakeId">The optional liability handshake identifier to link.</param>
    /// <param name="acknowledgmentId">The optional handshake acknowledgment identifier to link.</param>
    /// <param name="capabilityTokenId">The optional capability token or grant identifier to link.</param>
    /// <param name="previousRecordHash">The optional hash of the preceding ledger record.</param>
    /// <param name="recordHash">The optional hash of this ledger record.</param>
    /// <param name="signatureKeyId">The optional identifier of the signing key.</param>
    /// <param name="signatureAlgorithm">The optional signature algorithm.</param>
    /// <param name="signatureValue">The optional encoded signature value.</param>
    /// <param name="signingHash">The optional hash that was signed.</param>
    /// <param name="signatureKeyVersion">The optional version of the signing key.</param>
    /// <param name="signatureProvider">The optional name of the signing provider.</param>
    /// <param name="signedUtc">The optional UTC timestamp when the record was signed.</param>
    /// <param name="metadata">Optional metadata merged over the receipt metadata; entries with the same key win.</param>
    /// <param name="schemaVersion">The schema version; defaults to the receipt schema version.</param>
    /// <returns>A normalized ledger record with optional values trimmed and blank values treated as absent.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="receipt" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException">A required receipt value, such as the event, actor, operation, or outcome, is blank.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A receipt latency, constraint count, outbox sequence, or risk score is negative or not finite.</exception>
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
