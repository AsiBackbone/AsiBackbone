using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using Xunit;

namespace AsiBackbone.Core.Tests.Audit;

/// <summary>
/// Focused branch coverage for decision receipt normalization paths introduced by the hot-path optimization.
/// </summary>
public sealed class DecisionReceiptBranchCoverageTests
{
    /// <summary>
    /// Validates that the <see cref="DecisionReceipt.Create"/> method normalizes reason codes correctly across various input scenarios, including null, empty collections, collections with blank entries, and valid reason codes. This ensures that the resulting <see cref="DecisionReceipt"/> instance has the expected properties for reason codes.
    /// </summary>
    /// <param name="scenario">
    /// The specific input scenario for reason codes, which can be one of the following:
    /// </param>
    /// <param name="expectedHasReasonCodes">The expected value for the <see cref="DecisionReceipt.HasReasonCodes"/> property.</param>
    /// <param name="expectedReasonCodes">The expected array of reason codes.</param>
    [Theory]
    [InlineData("null", false, new string[0])]
    [InlineData("empty_collection", false, new string[0])]
    [InlineData("collection_all_blank", false, new string[0])]
    [InlineData("collection_all_valid", true, new[] { "reason.one", "reason.two" })]
    [InlineData("collection_some_blank", true, new[] { "reason.one", "reason.two" })]
    [InlineData("iterator_all_blank", false, new string[0])]
    [InlineData("iterator_some_valid", true, new[] { "reason.one", "reason.two" })]
    public void CreateNormalizesReasonCodeBranches(
        string scenario,
        bool expectedHasReasonCodes,
        string[] expectedReasonCodes)
    {
        var residue = DecisionReceipt.Create(
            GovernanceActorContext.Service("audit-service"),
            "audit.reason.normalization",
            "Completed",
            CreateReasonCodeScenario(scenario),
            eventId: "event-reason-normalization");

        Assert.Equal(expectedHasReasonCodes, residue.HasReasonCodes);
        Assert.Equal(expectedReasonCodes, residue.ReasonCodes);
    }

    /// <summary>
    /// Validates that the <see cref="DecisionReceipt.FromConstraint"/> method correctly maps constraint evaluation outcomes and reason codes to the resulting <see cref="DecisionReceipt"/> instance. This test covers various constraint scenarios, including not applicable, allowed, warning, and denied outcomes, ensuring that the resulting decision receipt reflects the expected outcome and reason codes.
    /// </summary>
    /// <param name="scenario">The specific input scenario for constraint evaluation.</param>
    /// <param name="expectedOutcome">The expected outcome for the decision receipt.</param>
    /// <param name="expectedReasonCodes">The expected array of reason codes.</param>
    [Theory]
    [InlineData("not_applicable", "NotApplicable", new string[0])]
    [InlineData("allowed", "Allowed", new string[0])]
    [InlineData("warning", "Warning", new[] { "constraint.warning" })]
    [InlineData("denied", "Denied", new[] { "constraint.denied" })]
    public void FromConstraintMapsConstraintOutcomesAndTrustedReasons(
        string scenario,
        string expectedOutcome,
        string[] expectedReasonCodes)
    {
        ConstraintEvaluationResult constraintResult = CreateConstraintResult(scenario);

        var residue = DecisionReceipt.FromConstraint(
            GovernanceActorContext.Service("audit-service"),
            "audit.constraint.outcome",
            constraintResult,
            eventId: "event-constraint-outcome",
            correlationId: "correlation-constraint",
            traceId: "trace-constraint",
            policyVersion: "policy-v1",
            policyHash: "policy-hash");

        Assert.Equal(expectedOutcome, residue.Outcome);
        Assert.Equal(expectedReasonCodes, residue.ReasonCodes);
        Assert.Equal("correlation-constraint", residue.CorrelationId);
        Assert.Equal("trace-constraint", residue.TraceId);
        Assert.Equal("policy-v1", residue.PolicyVersion);
        Assert.Equal("policy-hash", residue.PolicyHash);

        if (expectedReasonCodes.Length == 0)
        {
            Assert.False(residue.HasReasonCodes);
        }
        else
        {
            Assert.Same(constraintResult.ReasonCodes, residue.ReasonCodes);
        }
    }

    /// <summary>
    /// Validates that the <see cref="DecisionReceipt.Create"/> method correctly normalizes optional fields and uses an explicitly provided decision receipt ID. This test ensures that the resulting <see cref="DecisionReceipt"/> instance has the expected properties, including normalized actor ID, display name, operation name, outcome, reason codes, correlation ID, trace ID, span ID, parent span ID, decision latency, constraint set hash, constraint count, risk score, policy scope, tenant hash, organization hash, emitter status, emitter provider, outbox sequence, gateway execution ID, decision stage, policy version, policy hash, and occurred UTC offset.
    /// </summary>
    [Fact]
    public void CreateNormalizesOptionalFieldsAndUsesExplicitDecisionReceiptId()
    {
        var residue = DecisionReceipt.Create(
            GovernanceActorContext.Service(" audit-service ", " Audit Service "),
            " audit.operation ",
            " Completed ",
            [" reason.one "],
            eventId: " event-custom ",
            occurredUtc: new DateTimeOffset(2026, 7, 3, 12, 0, 0, TimeSpan.FromHours(-5)),
            correlationId: " correlation-custom ",
            traceId: " trace-custom ",
            policyVersion: " policy-v1 ",
            policyHash: " policy-hash ",
            decisionReceiptId: " audit-custom ",
            spanId: " span-custom ",
            parentSpanId: " parent-span-custom ",
            decisionLatencyMs: 15,
            constraintSetHash: " constraint-set ",
            constraintCount: 2,
            riskScore: 0.25,
            policyScope: " policy-scope ",
            tenantHash: " tenant-hash ",
            organizationHash: " org-hash ",
            emitterStatus: " queued ",
            emitterProvider: " local ",
            outboxSequence: 3,
            gatewayExecutionId: " gateway-execution ",
            decisionStage: " decision-stage ",
            schemaVersion: " v1 ");

        Assert.Equal("event-custom", residue.EventId);
        Assert.Equal("audit-custom", residue.DecisionReceiptId);
        Assert.Equal("audit-service", residue.ActorId);
        Assert.Equal("Audit Service", residue.ActorDisplayName);
        Assert.Equal("audit.operation", residue.OperationName);
        Assert.Equal("Completed", residue.Outcome);
        Assert.Equal(["reason.one"], residue.ReasonCodes);
        Assert.Equal("correlation-custom", residue.CorrelationId);
        Assert.Equal("trace-custom", residue.TraceId);
        Assert.Equal("span-custom", residue.SpanId);
        Assert.Equal("parent-span-custom", residue.ParentSpanId);
        Assert.Equal(15, residue.DecisionLatencyMs);
        Assert.Equal("constraint-set", residue.ConstraintSetHash);
        Assert.Equal(2, residue.ConstraintCount);
        Assert.Equal(0.25, residue.RiskScore);
        Assert.Equal("policy-scope", residue.PolicyScope);
        Assert.Equal("tenant-hash", residue.TenantHash);
        Assert.Equal("org-hash", residue.OrganizationHash);
        Assert.Equal("queued", residue.EmitterStatus);
        Assert.Equal("local", residue.EmitterProvider);
        Assert.Equal(3, residue.OutboxSequence);
        Assert.Equal("gateway-execution", residue.GatewayExecutionId);
        Assert.Equal("decision-stage", residue.DecisionStage);
        Assert.Equal("policy-v1", residue.PolicyVersion);
        Assert.Equal("policy-hash", residue.PolicyHash);
        Assert.Equal(TimeSpan.Zero, residue.OccurredUtc.Offset);
    }

    /// <summary>
    /// Validates that the <see cref="DecisionReceipt.Create"/> method rejects negative values for fields that are expected to be non-negative, such as decision latency, constraint count, and outbox sequence. This test ensures that an <see cref="ArgumentOutOfRangeException"/> is thrown with an appropriate message when negative values are provided for these fields.
    /// </summary>
    /// <param name="scenario">
    /// The specific field scenario to test for negative value rejection, which can be one of the following: "decision_latency", "constraint_count", or "outbox_sequence".
    /// </param>
    [Theory]
    [InlineData("decision_latency")]
    [InlineData("constraint_count")]
    [InlineData("outbox_sequence")]
    public void CreateRejectsNegativeNonNegativeFields(string scenario)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() => CreateWithNegativeValue(scenario));

        Assert.Contains("greater than or equal to zero", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates that the <see cref="DecisionReceipt.Create"/> method rejects invalid risk score values, such as NaN, positive infinity, and negative values. This test ensures that an <see cref="ArgumentOutOfRangeException"/> is thrown with an appropriate message when invalid risk scores are provided.
    /// </summary>
    /// <param name="scenario">
    /// The specific risk score scenario to test for invalid value rejection, which can be one of the following: "nan", "infinity", or "negative".
    /// </param>
    [Theory]
    [InlineData("nan")]
    [InlineData("infinity")]
    [InlineData("negative")]
    public void CreateRejectsInvalidRiskScore(string scenario)
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            DecisionReceipt.Create(
                GovernanceActorContext.Service("audit-service"),
                "audit.invalid_risk",
                "Completed",
                eventId: "event-invalid-risk",
                riskScore: CreateInvalidRiskScore(scenario)));

        Assert.Contains("finite value", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Validates that the <see cref="DecisionReceipt.Create"/> method correctly handles metadata dictionaries that contain only blank keys. This test ensures that the resulting <see cref="DecisionReceipt"/> instance has no metadata entries and that the shared empty metadata shape is returned, confirming that blank keys are ignored during normalization.
    /// </summary>
    [Fact]
    public void CreateWithOnlyBlankMetadataKeysReturnsSharedEmptyMetadataShape()
    {
        var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" "] = "ignored",
            ["\t"] = "also ignored"
        };

        var residue = DecisionReceipt.Create(
            GovernanceActorContext.Service("audit-service"),
            "audit.blank_metadata",
            "Completed",
            metadata: metadata,
            eventId: "event-blank-metadata");

        Assert.False(residue.HasMetadata);
        Assert.Empty(residue.Metadata);
    }

    private static IEnumerable<string>? CreateReasonCodeScenario(string scenario)
    {
        return scenario switch
        {
            "null" => null,
            "empty_collection" => Array.Empty<string>(),
            "collection_all_blank" => new[] { " ", "", "\t" },
            "collection_all_valid" => new[] { " reason.one ", "reason.two" },
            "collection_some_blank" => new[] { " reason.one ", " ", "reason.two" },
            "iterator_all_blank" => EnumerateReasonCodes(" ", "", "\t"),
            "iterator_some_valid" => EnumerateReasonCodes(" reason.one ", " ", "reason.two"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported reason-code scenario.")
        };
    }

    private static IEnumerable<string> EnumerateReasonCodes(params string[] reasonCodes)
    {
        foreach (string reasonCode in reasonCodes)
        {
            yield return reasonCode;
        }
    }

    private static ConstraintEvaluationResult CreateConstraintResult(string scenario)
    {
        return scenario switch
        {
            "not_applicable" => ConstraintEvaluationResult.NotApplicable(),
            "allowed" => ConstraintEvaluationResult.Allow(),
            "warning" => ConstraintEvaluationResult.Warning(
                "constraint.warning",
                "Constraint produced a warning."),
            "denied" => ConstraintEvaluationResult.Deny(
                "constraint.denied",
                "Constraint denied the operation."),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported constraint scenario.")
        };
    }

    private static void CreateWithNegativeValue(string scenario)
    {
        _ = scenario switch
        {
            "decision_latency" => DecisionReceipt.Create(
                GovernanceActorContext.Service("audit-service"),
                "audit.negative_latency",
                "Completed",
                eventId: "event-negative-latency",
                decisionLatencyMs: -1),
            "constraint_count" => DecisionReceipt.Create(
                GovernanceActorContext.Service("audit-service"),
                "audit.negative_constraint_count",
                "Completed",
                eventId: "event-negative-constraint-count",
                constraintCount: -1),
            "outbox_sequence" => DecisionReceipt.Create(
                GovernanceActorContext.Service("audit-service"),
                "audit.negative_outbox_sequence",
                "Completed",
                eventId: "event-negative-outbox-sequence",
                outboxSequence: -1),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported negative-value scenario.")
        };
    }

    private static double CreateInvalidRiskScore(string scenario)
    {
        return scenario switch
        {
            "nan" => double.NaN,
            "infinity" => double.PositiveInfinity,
            "negative" => -0.01,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unsupported risk-score scenario.")
        };
    }
}
