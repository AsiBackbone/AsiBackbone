using System.Collections.ObjectModel;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Serialization;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.Audit;

/// <summary>
/// Represents the framework-neutral decision receipt produced by an AsiBackbone operation.
/// </summary>
public sealed class DecisionReceipt : IDecisionReceipt
{
    private static readonly ReadOnlyCollection<string> EmptyReasonCodes =
        Array.AsReadOnly(Array.Empty<string>());

    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    private DecisionReceipt(
        string eventId,
        string decisionReceiptId,
        string schemaVersion,
        DateTimeOffset occurredUtc,
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
        IReadOnlyDictionary<string, string> metadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(decisionReceiptId);
        ArgumentException.ThrowIfNullOrWhiteSpace(actorId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(outcome);

        EventId = eventId.Trim();
        DecisionReceiptId = decisionReceiptId.Trim();
        SchemaVersion = GovernanceSchemaVersions.Normalize(schemaVersion);
        OccurredUtc = occurredUtc.ToUniversalTime();
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
        Metadata = metadata;
    }

    /// <inheritdoc />
    public string EventId { get; }

    /// <inheritdoc />
    public string DecisionReceiptId { get; }

    /// <inheritdoc />
    public string SchemaVersion { get; }

    /// <inheritdoc />
    public DateTimeOffset OccurredUtc { get; }

    /// <inheritdoc />
    public string ActorId { get; }

    /// <inheritdoc />
    public GovernanceActorType ActorType { get; }

    /// <inheritdoc />
    public string? ActorDisplayName { get; }

    /// <inheritdoc />
    public string OperationName { get; }

    /// <inheritdoc />
    public string Outcome { get; }

    /// <inheritdoc />
    public IReadOnlyList<string> ReasonCodes { get; }

    /// <inheritdoc />
    public string? CorrelationId { get; }

    /// <inheritdoc />
    public string? TraceId { get; }

    /// <inheritdoc />
    public string? SpanId { get; }

    /// <inheritdoc />
    public string? ParentSpanId { get; }

    /// <inheritdoc />
    public long? DecisionLatencyMs { get; }

    /// <inheritdoc />
    public string? ConstraintSetHash { get; }

    /// <inheritdoc />
    public int? ConstraintCount { get; }

    /// <inheritdoc />
    public double? RiskScore { get; }

    /// <inheritdoc />
    public string? PolicyScope { get; }

    /// <inheritdoc />
    public string? TenantHash { get; }

    /// <inheritdoc />
    public string? OrganizationHash { get; }

    /// <inheritdoc />
    public string? EmitterStatus { get; }

    /// <inheritdoc />
    public string? EmitterProvider { get; }

    /// <inheritdoc />
    public long? OutboxSequence { get; }

    /// <inheritdoc />
    public string? GatewayExecutionId { get; }

    /// <inheritdoc />
    public string? DecisionStage { get; }

    /// <inheritdoc />
    public string? PolicyVersion { get; }

    /// <inheritdoc />
    public string? PolicyHash { get; }

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether this decision receipt contains reason codes.
    /// </summary>
    public bool HasReasonCodes => ReasonCodes.Count > 0;

    /// <summary>
    /// Gets a value indicating whether this decision receipt contains metadata.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Creates decision receipt from a host-defined operation outcome.
    /// </summary>
    /// <param name="actor">The actor associated with the operation.</param>
    /// <param name="operationName">The operation name.</param>
    /// <param name="outcome">The governance, constraint, or host-defined outcome.</param>
    /// <param name="reasonCodes">Optional machine-readable reason codes.</param>
    /// <param name="eventId">Optional audit event identifier. When omitted, a new identifier is generated.</param>
    /// <param name="occurredUtc">Optional event timestamp. When omitted, the current UTC timestamp is used.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <param name="policyVersion">Optional policy version.</param>
    /// <param name="policyHash">Optional policy hash.</param>
    /// <param name="metadata">Optional host-provided audit metadata.</param>
    /// <param name="decisionReceiptId">Optional decision receipt identifier. When omitted, the normalized event identifier is used.</param>
    /// <param name="spanId">Optional span identifier.</param>
    /// <param name="parentSpanId">Optional parent span identifier.</param>
    /// <param name="decisionLatencyMs">Optional decision latency in milliseconds.</param>
    /// <param name="constraintSetHash">Optional evaluated constraint-set hash.</param>
    /// <param name="constraintCount">Optional evaluated constraint count.</param>
    /// <param name="riskScore">Optional host-defined risk score.</param>
    /// <param name="policyScope">Optional host-defined policy scope.</param>
    /// <param name="tenantHash">Optional privacy-preserving tenant hash.</param>
    /// <param name="organizationHash">Optional privacy-preserving organization hash.</param>
    /// <param name="emitterStatus">Optional provider-neutral emitter status.</param>
    /// <param name="emitterProvider">Optional provider-neutral emitter provider name.</param>
    /// <param name="outboxSequence">Optional outbox sequence.</param>
    /// <param name="gatewayExecutionId">Optional gateway execution identifier.</param>
    /// <param name="decisionStage">Optional provider-neutral decision stage.</param>
    /// <param name="schemaVersion">Optional schema version. When omitted, the stable artifact schema version is used.</param>
    /// <returns>An decision receipt value.</returns>
    public static DecisionReceipt Create(
        IGovernanceActorContext actor,
        string operationName,
        string outcome,
        IEnumerable<string>? reasonCodes = null,
        string? eventId = null,
        DateTimeOffset? occurredUtc = null,
        string? correlationId = null,
        string? traceId = null,
        string? policyVersion = null,
        string? policyHash = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? decisionReceiptId = null,
        string? spanId = null,
        string? parentSpanId = null,
        long? decisionLatencyMs = null,
        string? constraintSetHash = null,
        int? constraintCount = null,
        double? riskScore = null,
        string? policyScope = null,
        string? tenantHash = null,
        string? organizationHash = null,
        string? emitterStatus = null,
        string? emitterProvider = null,
        long? outboxSequence = null,
        string? gatewayExecutionId = null,
        string? decisionStage = null,
        string? schemaVersion = null)
    {
        return CreateCore(
            actor,
            operationName,
            outcome,
            NormalizeReasonCodes(reasonCodes),
            eventId,
            occurredUtc,
            correlationId,
            traceId,
            policyVersion,
            policyHash,
            metadata,
            decisionReceiptId,
            spanId,
            parentSpanId,
            decisionLatencyMs,
            constraintSetHash,
            constraintCount,
            riskScore,
            policyScope,
            tenantHash,
            organizationHash,
            emitterStatus,
            emitterProvider,
            outboxSequence,
            gatewayExecutionId,
            decisionStage,
            schemaVersion);
    }

    /// <summary>
    /// Creates decision receipt from a governance decision.
    /// </summary>
    /// <param name="actor">The actor associated with the operation.</param>
    /// <param name="operationName">The operation name.</param>
    /// <param name="decision">The governance decision to audit.</param>
    /// <param name="eventId">Optional audit event identifier. When omitted, a new identifier is generated.</param>
    /// <param name="occurredUtc">Optional event timestamp. When omitted, the current UTC timestamp is used.</param>
    /// <param name="metadata">Optional host-provided audit metadata.</param>
    /// <param name="decisionReceiptId">Optional decision receipt identifier. When omitted, the normalized event identifier is used.</param>
    /// <param name="spanId">Optional span identifier.</param>
    /// <param name="parentSpanId">Optional parent span identifier.</param>
    /// <param name="decisionLatencyMs">Optional decision latency in milliseconds.</param>
    /// <param name="constraintSetHash">Optional evaluated constraint-set hash.</param>
    /// <param name="constraintCount">Optional evaluated constraint count.</param>
    /// <param name="riskScore">Optional host-defined risk score.</param>
    /// <param name="policyScope">Optional host-defined policy scope.</param>
    /// <param name="tenantHash">Optional privacy-preserving tenant hash.</param>
    /// <param name="organizationHash">Optional privacy-preserving organization hash.</param>
    /// <param name="emitterStatus">Optional provider-neutral emitter status.</param>
    /// <param name="emitterProvider">Optional provider-neutral emitter provider name.</param>
    /// <param name="outboxSequence">Optional outbox sequence.</param>
    /// <param name="gatewayExecutionId">Optional gateway execution identifier.</param>
    /// <param name="decisionStage">Optional provider-neutral decision stage.</param>
    /// <param name="schemaVersion">Optional schema version. When omitted, the stable artifact schema version is used.</param>
    /// <returns>An decision receipt value.</returns>
    public static DecisionReceipt FromDecision(
        IGovernanceActorContext actor,
        string operationName,
        GovernanceDecision decision,
        string? eventId = null,
        DateTimeOffset? occurredUtc = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? decisionReceiptId = null,
        string? spanId = null,
        string? parentSpanId = null,
        long? decisionLatencyMs = null,
        string? constraintSetHash = null,
        int? constraintCount = null,
        double? riskScore = null,
        string? policyScope = null,
        string? tenantHash = null,
        string? organizationHash = null,
        string? emitterStatus = null,
        string? emitterProvider = null,
        long? outboxSequence = null,
        string? gatewayExecutionId = null,
        string? decisionStage = null,
        string? schemaVersion = null)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return CreateCore(
            actor,
            operationName,
            CanonicalEnumWireNames.ForDecisionOutcome(decision.Outcome),
            UseTrustedReasonCodes(decision.ReasonCodes),
            eventId,
            occurredUtc,
            decision.CorrelationId,
            decision.TraceId,
            decision.PolicyVersion,
            decision.PolicyHash,
            metadata,
            decisionReceiptId,
            spanId,
            parentSpanId,
            decisionLatencyMs,
            constraintSetHash,
            constraintCount,
            riskScore,
            policyScope,
            tenantHash,
            organizationHash,
            emitterStatus,
            emitterProvider,
            outboxSequence,
            gatewayExecutionId,
            decisionStage,
            schemaVersion);
    }

    /// <summary>
    /// Creates decision receipt from a constraint evaluation result.
    /// </summary>
    /// <param name="actor">The actor associated with the operation.</param>
    /// <param name="operationName">The operation name.</param>
    /// <param name="constraintResult">The constraint evaluation result to audit.</param>
    /// <param name="eventId">Optional audit event identifier. When omitted, a new identifier is generated.</param>
    /// <param name="occurredUtc">Optional event timestamp. When omitted, the current UTC timestamp is used.</param>
    /// <param name="correlationId">Optional correlation identifier.</param>
    /// <param name="traceId">Optional trace identifier.</param>
    /// <param name="policyVersion">Optional policy version.</param>
    /// <param name="policyHash">Optional policy hash.</param>
    /// <param name="metadata">Optional host-provided audit metadata.</param>
    /// <param name="decisionReceiptId">Optional decision receipt identifier. When omitted, the normalized event identifier is used.</param>
    /// <param name="spanId">Optional span identifier.</param>
    /// <param name="parentSpanId">Optional parent span identifier.</param>
    /// <param name="decisionLatencyMs">Optional decision latency in milliseconds.</param>
    /// <param name="constraintSetHash">Optional evaluated constraint-set hash.</param>
    /// <param name="constraintCount">Optional evaluated constraint count.</param>
    /// <param name="riskScore">Optional host-defined risk score.</param>
    /// <param name="policyScope">Optional host-defined policy scope.</param>
    /// <param name="tenantHash">Optional privacy-preserving tenant hash.</param>
    /// <param name="organizationHash">Optional privacy-preserving organization hash.</param>
    /// <param name="emitterStatus">Optional provider-neutral emitter status.</param>
    /// <param name="emitterProvider">Optional provider-neutral emitter provider name.</param>
    /// <param name="outboxSequence">Optional outbox sequence.</param>
    /// <param name="gatewayExecutionId">Optional gateway execution identifier.</param>
    /// <param name="decisionStage">Optional provider-neutral decision stage.</param>
    /// <param name="schemaVersion">Optional schema version. When omitted, the stable artifact schema version is used.</param>
    /// <returns>An decision receipt value.</returns>
    public static DecisionReceipt FromConstraint(
        IGovernanceActorContext actor,
        string operationName,
        ConstraintEvaluationResult constraintResult,
        string? eventId = null,
        DateTimeOffset? occurredUtc = null,
        string? correlationId = null,
        string? traceId = null,
        string? policyVersion = null,
        string? policyHash = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? decisionReceiptId = null,
        string? spanId = null,
        string? parentSpanId = null,
        long? decisionLatencyMs = null,
        string? constraintSetHash = null,
        int? constraintCount = null,
        double? riskScore = null,
        string? policyScope = null,
        string? tenantHash = null,
        string? organizationHash = null,
        string? emitterStatus = null,
        string? emitterProvider = null,
        long? outboxSequence = null,
        string? gatewayExecutionId = null,
        string? decisionStage = null,
        string? schemaVersion = null)
    {
        ArgumentNullException.ThrowIfNull(constraintResult);

        return CreateCore(
            actor,
            operationName,
            CanonicalEnumWireNames.ForConstraintOutcome(constraintResult.Outcome),
            UseTrustedReasonCodes(constraintResult.ReasonCodes),
            eventId,
            occurredUtc,
            correlationId,
            traceId,
            policyVersion,
            policyHash,
            metadata,
            decisionReceiptId,
            spanId,
            parentSpanId,
            decisionLatencyMs,
            constraintSetHash,
            constraintCount,
            riskScore,
            policyScope,
            tenantHash,
            organizationHash,
            emitterStatus,
            emitterProvider,
            outboxSequence,
            gatewayExecutionId,
            decisionStage,
            schemaVersion);
    }

    private static DecisionReceipt CreateCore(
        IGovernanceActorContext actor,
        string operationName,
        string outcome,
        IReadOnlyList<string> reasonCodes,
        string? eventId,
        DateTimeOffset? occurredUtc,
        string? correlationId,
        string? traceId,
        string? policyVersion,
        string? policyHash,
        IReadOnlyDictionary<string, string>? metadata,
        string? decisionReceiptId,
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
        string? schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(actor);

        string normalizedEventId = NormalizeIdentifier(eventId);

        return new DecisionReceipt(
            normalizedEventId,
            NormalizeDecisionReceiptId(decisionReceiptId, normalizedEventId),
            schemaVersion ?? GovernanceSchemaVersions.StableArtifactsV1,
            occurredUtc ?? DateTimeOffset.UtcNow,
            actor.ActorId,
            actor.ActorType,
            actor.DisplayName,
            operationName,
            outcome,
            reasonCodes,
            correlationId,
            traceId,
            spanId,
            parentSpanId,
            decisionLatencyMs,
            constraintSetHash,
            constraintCount,
            riskScore,
            policyScope,
            tenantHash,
            organizationHash,
            emitterStatus,
            emitterProvider,
            outboxSequence,
            gatewayExecutionId,
            decisionStage,
            policyVersion,
            policyHash,
            NormalizeMetadata(metadata));
    }

    private static string NormalizeIdentifier(string? identifier)
    {
        return string.IsNullOrWhiteSpace(identifier)
            ? Guid.NewGuid().ToString("N")
            : identifier.Trim();
    }

    private static string NormalizeDecisionReceiptId(string? decisionReceiptId, string eventId)
    {
        return string.IsNullOrWhiteSpace(decisionReceiptId)
            ? eventId
            : decisionReceiptId.Trim();
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
        if (reasonCodes is null)
        {
            return EmptyReasonCodes;
        }

        if (reasonCodes is ICollection<string> collection)
        {
            if (collection.Count == 0)
            {
                return EmptyReasonCodes;
            }

            string[] normalizedReasonCodes = new string[collection.Count];
            int normalizedCount = 0;

            foreach (string? reasonCode in collection)
            {
                AddNormalizedReasonCode(reasonCode, normalizedReasonCodes, ref normalizedCount);
            }

            return CreateReasonCodeCollection(normalizedReasonCodes, normalizedCount);
        }

        List<string>? normalizedList = null;

        foreach (string? reasonCode in reasonCodes)
        {
            if (string.IsNullOrWhiteSpace(reasonCode))
            {
                continue;
            }

            normalizedList ??= [];
            normalizedList.Add(reasonCode.Trim());
        }

        return normalizedList is null || normalizedList.Count == 0
            ? EmptyReasonCodes
            : Array.AsReadOnly([.. normalizedList]);
    }

    private static IReadOnlyList<string> UseTrustedReasonCodes(IReadOnlyList<string> reasonCodes)
    {
        return reasonCodes.Count == 0
            ? EmptyReasonCodes
            : reasonCodes;
    }

    private static void AddNormalizedReasonCode(
        string? reasonCode,
        string[] normalizedReasonCodes,
        ref int normalizedCount)
    {
        if (string.IsNullOrWhiteSpace(reasonCode))
        {
            return;
        }

        normalizedReasonCodes[normalizedCount] = reasonCode.Trim();
        normalizedCount++;
    }

    private static ReadOnlyCollection<string> CreateReasonCodeCollection(
        string[] normalizedReasonCodes,
        int normalizedCount)
    {
        if (normalizedCount == 0)
        {
            return EmptyReasonCodes;
        }

        if (normalizedCount == normalizedReasonCodes.Length)
        {
            return Array.AsReadOnly(normalizedReasonCodes);
        }

        string[] filteredReasonCodes = new string[normalizedCount];
        Array.Copy(normalizedReasonCodes, filteredReasonCodes, normalizedCount);

        return Array.AsReadOnly(filteredReasonCodes);
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

            string normalizedKey = item.Key.Trim();
            if (normalizedMetadata.ContainsKey(normalizedKey))
            {
                throw new ArgumentException(
                    $"Decision receipt metadata contains keys that collide after trimming: '{normalizedKey}'.",
                    nameof(metadata));
            }

            normalizedMetadata[normalizedKey] = item.Value?.Trim() ?? string.Empty;
        }

        return normalizedMetadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(normalizedMetadata);
    }
}
