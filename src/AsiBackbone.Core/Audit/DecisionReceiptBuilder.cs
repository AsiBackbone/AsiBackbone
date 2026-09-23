using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.Audit;

/// <summary>
/// Provides a fluent construction path for complex <see cref="DecisionReceipt" /> values.
/// </summary>
/// <remarks>
/// The builder is intended for ergonomic object creation in host code, samples, and tests. It preserves the immutable
/// <see cref="DecisionReceipt" /> value model by producing a new residue when <see cref="Build" /> is called.
/// </remarks>
public sealed class DecisionReceiptBuilder
{
    private readonly IGovernanceActorContext actor;
    private readonly string operationName;
    private readonly string outcome;
    private readonly List<string> reasonCodes = [];

    private Dictionary<string, string>? metadata;
    private string? eventId;
    private DateTimeOffset? occurredUtc;
    private string? correlationId;
    private string? traceId;
    private string? policyVersion;
    private string? policyHash;
    private string? decisionReceiptId;
    private string? spanId;
    private string? parentSpanId;
    private long? decisionLatencyMs;
    private string? constraintSetHash;
    private int? constraintCount;
    private double? riskScore;
    private string? policyScope;
    private string? tenantHash;
    private string? organizationHash;
    private string? emitterStatus;
    private string? emitterProvider;
    private long? outboxSequence;
    private string? gatewayExecutionId;
    private string? decisionStage;
    private string? schemaVersion;

    private DecisionReceiptBuilder(
        IGovernanceActorContext actor,
        string operationName,
        string outcome,
        IEnumerable<string>? reasonCodes = null)
    {
        ArgumentNullException.ThrowIfNull(actor);

        this.actor = actor;
        this.operationName = operationName;
        this.outcome = outcome;
        _ = WithReasonCodes(reasonCodes);
    }

    /// <summary>
    /// Creates a builder for a host-defined audit outcome.
    /// </summary>
    public static DecisionReceiptBuilder Create(
        IGovernanceActorContext actor,
        string operationName,
        string outcome)
    {
        return new DecisionReceiptBuilder(actor, operationName, outcome);
    }

    /// <summary>
    /// Creates a builder initialized from a governance decision.
    /// </summary>
    public static DecisionReceiptBuilder FromDecision(
        IGovernanceActorContext actor,
        string operationName,
        GovernanceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(decision);

        return new DecisionReceiptBuilder(
            actor,
            operationName,
            CanonicalEnumWireNames.ForDecisionOutcome(decision.Outcome),
            decision.ReasonCodes)
            .WithCorrelationId(decision.CorrelationId)
            .WithTraceId(decision.TraceId)
            .WithPolicyVersion(decision.PolicyVersion)
            .WithPolicyHash(decision.PolicyHash);
    }

    /// <summary>
    /// Creates a builder initialized from a constraint evaluation result.
    /// </summary>
    public static DecisionReceiptBuilder FromConstraint(
        IGovernanceActorContext actor,
        string operationName,
        ConstraintEvaluationResult constraintResult)
    {
        ArgumentNullException.ThrowIfNull(constraintResult);

        return new DecisionReceiptBuilder(
            actor,
            operationName,
            CanonicalEnumWireNames.ForConstraintOutcome(constraintResult.Outcome),
            constraintResult.ReasonCodes);
    }

    /// <summary>
    /// Sets the audit event identifier.
    /// </summary>
    public DecisionReceiptBuilder WithEventId(string? value)
    {
        eventId = value;
        return this;
    }

    /// <summary>
    /// Sets the event occurrence timestamp.
    /// </summary>
    public DecisionReceiptBuilder WithOccurredUtc(DateTimeOffset? value)
    {
        occurredUtc = value;
        return this;
    }

    /// <summary>
    /// Replaces the reason-code collection.
    /// </summary>
    public DecisionReceiptBuilder WithReasonCodes(IEnumerable<string>? values)
    {
        reasonCodes.Clear();

        if (values is not null)
        {
            reasonCodes.AddRange(values);
        }

        return this;
    }

    /// <summary>
    /// Adds one reason code to the builder.
    /// </summary>
    public DecisionReceiptBuilder AddReasonCode(string value)
    {
        reasonCodes.Add(value);
        return this;
    }

    /// <summary>
    /// Sets the correlation identifier.
    /// </summary>
    public DecisionReceiptBuilder WithCorrelationId(string? value)
    {
        correlationId = value;
        return this;
    }

    /// <summary>
    /// Sets the trace identifier.
    /// </summary>
    public DecisionReceiptBuilder WithTraceId(string? value)
    {
        traceId = value;
        return this;
    }

    /// <summary>
    /// Sets the policy version.
    /// </summary>
    public DecisionReceiptBuilder WithPolicyVersion(string? value)
    {
        policyVersion = value;
        return this;
    }

    /// <summary>
    /// Sets the policy hash.
    /// </summary>
    public DecisionReceiptBuilder WithPolicyHash(string? value)
    {
        policyHash = value;
        return this;
    }

    /// <summary>
    /// Replaces the metadata collection.
    /// </summary>
    public DecisionReceiptBuilder WithMetadata(IReadOnlyDictionary<string, string>? values)
    {
        metadata = null;

        if (values is not null)
        {
            foreach (KeyValuePair<string, string> item in values)
            {
                (metadata ??= new Dictionary<string, string>(StringComparer.Ordinal))[item.Key] = item.Value;
            }
        }

        return this;
    }

    /// <summary>
    /// Adds or replaces one metadata value.
    /// </summary>
    public DecisionReceiptBuilder AddMetadata(string key, string value)
    {
        (metadata ??= new Dictionary<string, string>(StringComparer.Ordinal))[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the stable decision receipt identifier.
    /// </summary>
    public DecisionReceiptBuilder WithDecisionReceiptId(string? value)
    {
        decisionReceiptId = value;
        return this;
    }

    /// <summary>
    /// Sets the span identifier.
    /// </summary>
    public DecisionReceiptBuilder WithSpanId(string? value)
    {
        spanId = value;
        return this;
    }

    /// <summary>
    /// Sets the parent span identifier.
    /// </summary>
    public DecisionReceiptBuilder WithParentSpanId(string? value)
    {
        parentSpanId = value;
        return this;
    }

    /// <summary>
    /// Sets the decision latency in milliseconds.
    /// </summary>
    public DecisionReceiptBuilder WithDecisionLatencyMs(long? value)
    {
        decisionLatencyMs = value;
        return this;
    }

    /// <summary>
    /// Sets the evaluated constraint-set hash.
    /// </summary>
    public DecisionReceiptBuilder WithConstraintSetHash(string? value)
    {
        constraintSetHash = value;
        return this;
    }

    /// <summary>
    /// Sets the evaluated constraint count.
    /// </summary>
    public DecisionReceiptBuilder WithConstraintCount(int? value)
    {
        constraintCount = value;
        return this;
    }

    /// <summary>
    /// Sets the host-defined risk score.
    /// </summary>
    public DecisionReceiptBuilder WithRiskScore(double? value)
    {
        riskScore = value;
        return this;
    }

    /// <summary>
    /// Sets the host-defined policy scope.
    /// </summary>
    public DecisionReceiptBuilder WithPolicyScope(string? value)
    {
        policyScope = value;
        return this;
    }

    /// <summary>
    /// Sets the privacy-preserving tenant hash.
    /// </summary>
    public DecisionReceiptBuilder WithTenantHash(string? value)
    {
        tenantHash = value;
        return this;
    }

    /// <summary>
    /// Sets the privacy-preserving organization hash.
    /// </summary>
    public DecisionReceiptBuilder WithOrganizationHash(string? value)
    {
        organizationHash = value;
        return this;
    }

    /// <summary>
    /// Sets the provider-neutral emitter status.
    /// </summary>
    public DecisionReceiptBuilder WithEmitterStatus(string? value)
    {
        emitterStatus = value;
        return this;
    }

    /// <summary>
    /// Sets the provider-neutral emitter provider name.
    /// </summary>
    public DecisionReceiptBuilder WithEmitterProvider(string? value)
    {
        emitterProvider = value;
        return this;
    }

    /// <summary>
    /// Sets the outbox sequence.
    /// </summary>
    public DecisionReceiptBuilder WithOutboxSequence(long? value)
    {
        outboxSequence = value;
        return this;
    }

    /// <summary>
    /// Sets the gateway execution identifier.
    /// </summary>
    public DecisionReceiptBuilder WithGatewayExecutionId(string? value)
    {
        gatewayExecutionId = value;
        return this;
    }

    /// <summary>
    /// Sets the provider-neutral decision stage.
    /// </summary>
    public DecisionReceiptBuilder WithDecisionStage(string? value)
    {
        decisionStage = value;
        return this;
    }

    /// <summary>
    /// Sets the decision receipt schema version.
    /// </summary>
    public DecisionReceiptBuilder WithSchemaVersion(string? value)
    {
        schemaVersion = value;
        return this;
    }

    /// <summary>
    /// Builds an immutable decision receipt value.
    /// </summary>
    public DecisionReceipt Build()
    {
        return DecisionReceipt.Create(
            actor,
            operationName,
            outcome,
            reasonCodes,
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
}
