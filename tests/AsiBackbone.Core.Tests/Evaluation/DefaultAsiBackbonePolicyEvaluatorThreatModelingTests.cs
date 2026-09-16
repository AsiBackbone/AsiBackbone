using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.ThreatModeling;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Behavior coverage for threat model contributor integration in policy evaluation.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorThreatModelingTests
{
    /// <summary>
    /// Verifies that a threat contributor that returns NoThreat allows the evaluation to proceed and runs the contributor.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that returns NoThreat.
    /// </returns>
    [Fact]
    public async Task EvaluateNoThreatContributorAllowsDecisionAndRunsContributor()
    {
        TestPolicyContext context = CreateContext();
        bool contributorRan = false;
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [new DelegateThreatContributor(
                "no-threat-contributor",
                (_, _) =>
                {
                    contributorRan = true;
                    return ThreatAssessment.NoThreat();
                })], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(contributorRan);
        Assert.True(decision.IsAllowed);
        Assert.True(decision.CanProceed);
        Assert.Empty(decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that a threat contributor that returns null is ignored and does not affect the evaluation outcome.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that returns null.
    /// </returns>
    [Fact]
    public async Task EvaluateNullThreatAssessmentIsIgnored()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [new DelegateThreatContributor("null-threat-contributor", (_, _) => null!)], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Empty(decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that a threat contributor that returns an Allowed outcome throws an InvalidOperationException and skips constraint evaluation.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that returns an Allowed outcome.
    /// </returns>
    [Fact]
    public async Task EvaluateActionableAllowedThreatRecommendationThrowsInvalidOperationExceptionAndSkipsConstraints()
    {
        TestPolicyContext context = CreateContext();
        bool constraintRan = false;
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [new DelegateConstraint(
                (_, _) =>
                {
                    constraintRan = true;
                    return ConstraintEvaluationResult.Allow();
                })],
            [new StaticThreatContributor(
                "invalid-allow-threat-contributor",
                ThreatAssessment.Create(
                    ThreatSeverity.Low,
                    ThreatCategories.InputOversized,
                    "threat.input_oversized",
                    "Oversized input indicator was reported.",
                    GovernanceDecisionOutcome.Allowed))], decisionPolicy: null, options: null, logger: null);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken));

        Assert.False(constraintRan);
        Assert.Contains("Threat model contributors cannot return an Allowed outcome", exception.Message);
        Assert.Contains("ThreatAssessment.NoThreat", exception.Message);
    }

    /// <summary>
    /// Verifies that a threat contributor that returns a Deferred outcome results in a deferred decision and includes the reason code from the threat assessment.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that returns a Deferred outcome.
    /// </returns>
    [Fact]
    public async Task EvaluateDeferredThreatRecommendationReturnsDeferredDecision()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [new StaticThreatContributor(
                "deferred-threat-contributor",
                ThreatAssessment.Create(
                    ThreatSeverity.Medium,
                    ThreatCategories.RegionPolicyMismatch,
                    "threat.region_policy_mismatch",
                    "Region policy mismatch was reported.",
                    GovernanceDecisionOutcome.Deferred))], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDeferred);
        Assert.False(decision.CanProceed);
        Assert.Equal("threat.region_policy_mismatch", Assert.Single(decision.ReasonCodes));
    }

    /// <summary>
    /// Verifies that a threat contributor that returns an AcknowledgmentRequired outcome results in a decision that requires acknowledgment and includes the reason code from the threat assessment.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that returns an AcknowledgmentRequired outcome.
    /// </returns>
    [Fact]
    public async Task EvaluateAcknowledgmentThreatRecommendationReturnsAcknowledgmentRequiredDecision()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [new StaticThreatContributor(
                "ack-threat-contributor",
                ThreatAssessment.Create(
                    ThreatSeverity.Medium,
                    ThreatCategories.AuditIntegrityRisk,
                    "threat.audit_ack_required",
                    "Audit acknowledgment was requested.",
                    GovernanceDecisionOutcome.AcknowledgmentRequired))], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.RequiresAcknowledgment);
        Assert.False(decision.CanProceed);
        Assert.Equal("threat.audit_ack_required", Assert.Single(decision.ReasonCodes));
    }

    /// <summary>
    /// Verifies that multiple threat contributors can aggregate denied reasons with metadata and that the evaluation order is preserved.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with multiple threat contributors that return denied reasons with metadata.
    /// </returns>
    [Fact]
    public async Task EvaluateMultipleThreatContributorsCanAggregateDeniedReasonsWithMetadata()
    {
        TestPolicyContext context = CreateContext();
        var observedOrder = new List<string>();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [
                new DelegateThreatContributor(
                    "input-shape-contributor",
                    (_, _) =>
                    {
                        observedOrder.Add("input");
                        return ThreatAssessment.Create(
                            ThreatSeverity.Low,
                            ThreatCategories.InputMalformed,
                            "threat.input_shape",
                            "Input shape indicator was reported.",
                            GovernanceDecisionOutcome.Warning);
                    }),
                new DelegateThreatContributor(
                    "capability-token-contributor",
                    (_, _) =>
                    {
                        observedOrder.Add("token");
                        return ThreatAssessment.Create(
                            ThreatSeverity.Critical,
                            ThreatCategories.CapabilityTokenMismatch,
                            "threat.capability_token_mismatch",
                            "Capability token mismatch was reported.",
                            GovernanceDecisionOutcome.Denied);
                    })
            ], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.Equal(["input", "token"], observedOrder);
        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Contains("threat.input_shape", decision.ReasonCodes);
        Assert.Contains("threat.capability_token_mismatch", decision.ReasonCodes);

        Assert.Equal(
            ThreatCategories.InputMalformed,
            decision.Reasons.Single(reason => reason.Code == "threat.input_shape").Metadata["threat.category"]);
        Assert.Equal(
            "input-shape-contributor",
            decision.Reasons.Single(reason => reason.Code == "threat.input_shape").Metadata["threat.contributor"]);
        Assert.Equal(
            ThreatSeverity.Critical.ToString(),
            decision.Reasons.Single(reason => reason.Code == "threat.capability_token_mismatch").Metadata["threat.severity"]);
    }

    /// <summary>
    /// Verifies that a threat contributor that throws an exception results in a denied decision with the default reason code for threat contributor exceptions when the evaluator is configured to fail closed.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that throws an exception, resulting in a denied decision with the default reason code for threat contributor exceptions when the evaluator is configured to fail closed.
    /// </returns>
    [Fact]
    public async Task EvaluateThreatContributorExceptionFailsClosedByDefault()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [new ThrowingThreatContributor("throwing-threat-contributor")], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Equal(
            GovernancePolicyOptions.DefaultThreatContributorExceptionReasonCode,
            Assert.Single(decision.ReasonCodes));
        Assert.Equal(
            "throwing-threat-contributor",
            Assert.Single(decision.Reasons).Metadata["threat.contributor"]);
    }

    /// <summary>
    /// Verifies that a threat contributor that throws an exception propagates the exception when the evaluator is configured to not fail closed.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that throws an exception, propagating the exception when the evaluator is configured to not fail closed.
    /// </returns>
    [Fact]
    public async Task EvaluateThreatContributorExceptionPropagatesWhenFailClosedDisabled()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [new ThrowingThreatContributor("throwing-threat-contributor")],
            decisionPolicy: null,
            new GovernancePolicyOptions
            {
                TreatThreatContributorExceptionAsDenial = false
            }, logger: null);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Verifies that a threat contributor that returns a warning outcome cannot be downgraded to an allow decision by the decision policy by default.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that returns a warning outcome, ensuring that it cannot be downgraded to an allow decision by the decision policy by default.
    /// </returns>
    [Fact]
    public async Task EvaluateThreatWarningCannotBeDowngradedToAllowByDecisionPolicyByDefault()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [new StaticThreatContributor(
                "warning-threat-contributor",
                ThreatAssessment.Create(
                    ThreatSeverity.Low,
                    ThreatCategories.InputMalformed,
                    "threat.input_malformed",
                    "Malformed input indicator was reported.",
                    GovernanceDecisionOutcome.Warning))],
            new AlwaysAllowDecisionPolicy(), options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsWarning);
        Assert.True(decision.CanProceed);
        Assert.Equal("threat.input_malformed", Assert.Single(decision.ReasonCodes));
    }

    /// <summary>
    /// Verifies that a threat contributor that returns a warning outcome can be downgraded to an allow decision by the decision policy when the evaluator is configured to allow downgrades.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that returns a warning outcome, ensuring that it can be downgraded to an allow decision by the decision policy when the evaluator is configured to allow downgrades.
    /// </returns>
    [Fact]
    public async Task EvaluateThreatWarningCanBeDowngradedWhenProtectionDisabled()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [new StaticThreatContributor(
                "warning-threat-contributor",
                ThreatAssessment.Create(
                    ThreatSeverity.Low,
                    ThreatCategories.InputMalformed,
                    "threat.input_malformed",
                    "Malformed input indicator was reported.",
                    GovernanceDecisionOutcome.Warning))],
            new AlwaysAllowDecisionPolicy(),
            new GovernancePolicyOptions
            {
                PreventThreatAssessmentAllowDowngrade = false
            }, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Empty(decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that a threat contributor that returns a warning outcome results in a warning decision when there are no constraints and the policy explicitly allows warnings.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that returns a warning outcome, ensuring that it results in a warning decision when there are no constraints and the policy explicitly allows warnings.
    /// </returns>
    [Fact]
    public async Task EvaluateThreatWarningWithoutConstraintsReturnsWarningWhenEmptyPolicyExplicitlyAllows()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [],
            [new StaticThreatContributor(
                "warning-threat-contributor",
                ThreatAssessment.Create(
                    ThreatSeverity.Low,
                    ThreatCategories.AuditIntegrityRisk,
                    "threat.audit_integrity_risk",
                    "Audit integrity risk was reported.",
                    GovernanceDecisionOutcome.Warning))],
            decisionPolicy: null,
            new GovernancePolicyOptions
            {
                DenyWhenNoConstraints = false
            }, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsWarning);
        Assert.Equal("threat.audit_integrity_risk", Assert.Single(decision.ReasonCodes));
    }

    /// <summary>
    /// Verifies that a threat contributor that returns a warning outcome results in a denied decision with the default reason code for no constraints when there are no constraints and the policy denies warnings.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the policy with a threat contributor that returns a warning outcome, ensuring that it results in a denied decision with the default reason code for no constraints when there are no constraints and the policy denies warnings.
    /// </returns>
    [Fact]
    public async Task EvaluateThreatWarningWithoutConstraintsReturnsNoConstraintDenialWhenEmptyPolicyDenies()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultGovernancePolicyEvaluator<TestPolicyContext>(
            [],
            [new StaticThreatContributor(
                "warning-threat-contributor",
                ThreatAssessment.Create(
                    ThreatSeverity.Low,
                    ThreatCategories.AuditIntegrityRisk,
                    "threat.audit_integrity_risk",
                    "Audit integrity risk was reported.",
                    GovernanceDecisionOutcome.Warning))],
            decisionPolicy: null,
            new GovernancePolicyOptions
            {
                DenyWhenNoConstraints = true
            }, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal(
            GovernancePolicyOptions.DefaultNoConstraintsReasonCode,
            Assert.Single(decision.ReasonCodes));
    }

    /// <summary>
    /// Verifies that a threat assessment with an out-of-range confidence value throws an ArgumentOutOfRangeException when created.
    /// </summary>
    [Fact]
    public void ThreatAssessmentRejectsOutOfRangeConfidence()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ThreatAssessment.Create(
            ThreatSeverity.Low,
            ThreatCategories.InputMalformed,
            "threat.invalid_confidence",
            "Invalid confidence should be rejected.",
            GovernanceDecisionOutcome.Warning,
            confidence: 1.1D));
    }

    /// <summary>
    /// Verifies that a threat assessment can merge custom metadata into the operation reason and that the metadata is normalized (trimmed and lowercased).
    /// </summary>
    [Fact]
    public void ThreatAssessmentOperationReasonMergesCustomMetadata()
    {
        var assessment = ThreatAssessment.Create(
            ThreatSeverity.Medium,
            ThreatCategories.ReplayAttempt,
            "threat.replay_attempt",
            "Replay attempt was reported.",
            GovernanceDecisionOutcome.Warning,
            confidence: 0.5D,
            new Dictionary<string, string>
            {
                [" request.id "] = " 123 "
            });

        var reason = assessment.ToOperationReason(" replay-contributor ");

        Assert.Equal("123", reason.Metadata["request.id"]);
        Assert.Equal("replay-contributor", reason.Metadata["threat.contributor"]);
        Assert.Equal("0.5", reason.Metadata["threat.confidence"]);
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-threat-model-123",
            PolicyVersion = "v-threat-model",
            PolicyHash = "hash-threat-model"
        };
    }

    private sealed class TestPolicyContext : IGovernanceEvaluationContext
    {
        public string? CorrelationId { get; init; }

        public string? PolicyVersion { get; init; }

        public string? PolicyHash { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class StaticConstraint(ConstraintEvaluationResult result) : IGovernanceConstraint<TestPolicyContext>
    {
        private readonly ConstraintEvaluationResult result = result;

        public string Name => "static-threat-model-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class DelegateConstraint(
        Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate) : IGovernanceConstraint<TestPolicyContext>
    {
        private readonly Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate = evaluate;

        public string Name => "delegate-threat-model-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(evaluate(context, cancellationToken));
        }
    }

    private sealed class StaticThreatContributor(
        string name,
        ThreatAssessment assessment) : IThreatModelContributor<TestPolicyContext>
    {
        private readonly ThreatAssessment assessment = assessment;

        public string Name { get; } = name;

        public ValueTask<ThreatAssessment> AssessAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(assessment);
        }
    }

    private sealed class DelegateThreatContributor(
        string name,
        Func<TestPolicyContext, CancellationToken, ThreatAssessment> assess) : IThreatModelContributor<TestPolicyContext>
    {
        private readonly Func<TestPolicyContext, CancellationToken, ThreatAssessment> assess = assess;

        public string Name { get; } = name;

        public ValueTask<ThreatAssessment> AssessAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(assess(context, cancellationToken));
        }
    }

    private sealed class ThrowingThreatContributor(string name) : IThreatModelContributor<TestPolicyContext>
    {
        public string Name { get; } = name;

        public ValueTask<ThreatAssessment> AssessAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Threat contributor failed.");
        }
    }

    private sealed class AlwaysAllowDecisionPolicy : IGovernanceDecisionPolicy<TestPolicyContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            TestPolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceDecision.Allow(
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }
    }
}
