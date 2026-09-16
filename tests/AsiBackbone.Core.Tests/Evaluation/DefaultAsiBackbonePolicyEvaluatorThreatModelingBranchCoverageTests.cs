using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.ThreatModeling;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Targeted branch coverage for threat model contributor decision composition.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorThreatModelingBranchCoverageTests
{
    /// <summary>
    /// Test that when a threat warning and a constraint denial are both present, the threat warning is suppressed by default in the final governance decision.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateThreatWarningAndConstraintDenialSuppressesThreatWarningByDefault()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Deny("constraint.denied", "Constraint denied."))],
            [CreateWarningContributor()], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal("constraint.denied", Assert.Single(decision.ReasonCodes));
        Assert.DoesNotContain("threat.warning", decision.ReasonCodes);
    }

    /// <summary>
    /// Test that when a threat warning and a constraint denial are both present, the threat warning is preserved in the final governance decision when short-circuiting is enabled.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateThreatWarningAndConstraintDenialPreservesThreatWarningWhenShortCircuitEnabled()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Deny("constraint.denied", "Constraint denied."))],
            [CreateWarningContributor()],
            null,
            new AsiBackbonePolicyEvaluatorOptions
            {
                ShortCircuitOnFirstDenial = true
            }, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Contains("threat.warning", decision.ReasonCodes);
        Assert.Contains("constraint.denied", decision.ReasonCodes);
    }

    /// <summary>
    /// Test that when a constraint denial is followed by a constraint warning, the warning is ignored in the final governance decision when short-circuiting is not enabled.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateConstraintWarningAfterDenialIsIgnoredWhenNotShortCircuiting()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(ConstraintEvaluationResult.Deny("constraint.denied", "Constraint denied.")),
                new StaticConstraint(ConstraintEvaluationResult.Warning("constraint.warning", "Constraint warned."))
            ],
            [], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Contains("constraint.denied", decision.ReasonCodes);
        Assert.DoesNotContain("constraint.warning", decision.ReasonCodes);
    }

    /// <summary>
    /// Test that when a constraint denial is present and short-circuiting is enabled, the evaluation stops after the first denial and only that denial reason is included in the final governance decision.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateShortCircuitDenialWithoutWarningsReturnsSingleDenialReason()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Deny("constraint.denied", "Constraint denied."))],
            [],
            null,
            new AsiBackbonePolicyEvaluatorOptions
            {
                ShortCircuitOnFirstDenial = true
            }, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal("constraint.denied", Assert.Single(decision.ReasonCodes));
    }

    /// <summary>
    /// Test that when a constraint denial is present and short-circuiting is enabled, the evaluation stops after the first denial and any subsequent constraints are not evaluated, ensuring that only the first denial reason is included in the final governance decision.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateShortCircuitDenialSkipsLaterConstraint()
    {
        TestPolicyContext context = CreateContext();
        bool secondConstraintRan = false;
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(ConstraintEvaluationResult.Deny("constraint.denied", "Constraint denied.")),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        secondConstraintRan = true;
                        return ConstraintEvaluationResult.Allow();
                    })
            ],
            [],
            null,
            new AsiBackbonePolicyEvaluatorOptions
            {
                ShortCircuitOnFirstDenial = true
            }, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(secondConstraintRan);
    }

    /// <summary>
    /// Test that when multiple warnings are produced by constraints and threat contributors, all warning reason codes are included in the final governance decision.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateMultipleWarningsProducesMultipleWarningReasons()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Warning("constraint.warning", "Constraint warned."))],
            [CreateWarningContributor()], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsWarning);
        Assert.Contains("threat.warning", decision.ReasonCodes);
        Assert.Contains("constraint.warning", decision.ReasonCodes);
    }

    /// <summary>
    /// Test that when multiple denials are produced by constraints, all denial reason codes are included in the final governance decision.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateMultipleDenialsProducesMultipleDenialReasons()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(ConstraintEvaluationResult.Deny("constraint.denied.one", "Constraint one denied.")),
                new StaticConstraint(ConstraintEvaluationResult.Deny("constraint.denied.two", "Constraint two denied."))
            ],
            [], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Contains("constraint.denied.one", decision.ReasonCodes);
        Assert.Contains("constraint.denied.two", decision.ReasonCodes);
    }

    /// <summary>
    /// Test that when a decision policy denies the operation, it overrides any threat warnings produced by contributors, resulting in a final governance decision that is denied with the reason code from the decision policy.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateDecisionPolicyDenialIsNotOverriddenByThreatWarningProtection()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [CreateWarningContributor()],
            new AlwaysDenyDecisionPolicy(), options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal("decision.policy_denied", Assert.Single(decision.ReasonCodes));
    }

    /// <summary>
    /// Test that when a threat warning is produced by a contributor and the evaluator is configured to allow downgrades, the final governance decision is allowed with no reason codes, effectively downgrading the warning to an allowed outcome.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateNoConstraintThreatWarningCanBeDowngradedWhenProtectionDisabled()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [],
            [CreateWarningContributor()],
            new AlwaysAllowDecisionPolicy(),
            new AsiBackbonePolicyEvaluatorOptions
            {
                PreventThreatAssessmentAllowDowngrade = false
            }, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Empty(decision.ReasonCodes);
    }

    /// <summary>
    /// Test that when the cancellation token is canceled before evaluation starts, the evaluator throws an OperationCanceledException and does not run any threat contributors.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateCanceledTokenThrowsBeforeContributorRuns()
    {
        TestPolicyContext context = CreateContext();
        bool contributorRan = false;
        using var cancellationTokenSource = new CancellationTokenSource();
        await cancellationTokenSource.CancelAsync();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [new DelegateThreatContributor(
                "cancel-observer",
                (_, _) =>
                {
                    contributorRan = true;
                    return ThreatAssessment.NoThreat();
                })], decisionPolicy: null, options: null, logger: null);

        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await evaluator.EvaluateAsync(context, cancellationTokenSource.Token));
        Assert.False(contributorRan);
    }

    /// <summary>
    /// Test that when a threat contributor throws an OperationCanceledException during evaluation, the exception propagates and the evaluation is canceled.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    /// <exception cref="OperationCanceledException">
    /// Thrown when the evaluation is canceled due to a contributor throwing an OperationCanceledException.
    /// </exception>
    [Fact]
    public async Task EvaluateContributorOperationCanceledExceptionPropagates()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [new DelegateThreatContributor(
                "canceling-threat-contributor",
                (_, _) => throw new OperationCanceledException())], decisionPolicy: null, options: null, logger: null);

        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Test that when a constraint throws an OperationCanceledException during evaluation, the exception propagates and the evaluation is canceled.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateConstraintOperationCanceledExceptionPropagates()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new ThrowingConstraint(new OperationCanceledException())],
            [], decisionPolicy: null, options: null, logger: null);

        _ = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Test that when a more restrictive threat contributor (denial) is evaluated before a less restrictive threat contributor (warning), the final governance decision reflects the more restrictive outcome, and both reason codes are included in the decision.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task EvaluateLessRestrictiveSecondThreatKeepsMoreRestrictiveFirstOutcome()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [
                new StaticThreatContributor(
                    "denial-threat-contributor",
                    ThreatAssessment.Create(
                        ThreatSeverity.Critical,
                        ThreatCategories.CapabilityTokenMismatch,
                        "threat.denied",
                        "Denied threat indicator was reported.",
                        GovernanceDecisionOutcome.Denied)),
                CreateWarningContributor()
            ], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Contains("threat.denied", decision.ReasonCodes);
        Assert.Contains("threat.warning", decision.ReasonCodes);
    }

    /// <summary>
    /// Test that when a threat assessment is created with no threat, it results in a non-actionable assessment with severity None, category None, and recommended outcome Allowed.
    /// </summary>
    [Fact]
    public void ThreatAssessmentNoThreatCreatesNonActionableAllowedAssessment()
    {
        var assessment = ThreatAssessment.NoThreat();

        Assert.False(assessment.IsActionable);
        Assert.Equal(ThreatSeverity.None, assessment.Severity);
        Assert.Equal(ThreatCategories.None, assessment.Category);
        Assert.Equal(GovernanceDecisionOutcome.Allowed, assessment.RecommendedOutcome);
    }

    /// <summary>
    /// Test that when a threat assessment is created with severity None but a warning outcome, it is still considered actionable, demonstrating that even low-severity threats can require attention.
    /// </summary>
    [Fact]
    public void ThreatAssessmentNoneSeverityWarningOutcomeIsActionable()
    {
        var assessment = ThreatAssessment.Create(
            ThreatSeverity.None,
            ThreatCategories.InputMalformed,
            "threat.none_warning",
            "None severity warning should still be actionable.",
            GovernanceDecisionOutcome.Warning);

        Assert.True(assessment.IsActionable);
    }

    /// <summary>
    /// Test that when a threat assessment is created with empty metadata, it results in an empty metadata collection, ensuring that the assessment does not contain any unintended metadata entries.
    /// </summary>
    [Fact]
    public void ThreatAssessmentEmptyMetadataUsesEmptyMetadataCollection()
    {
        var assessment = ThreatAssessment.Create(
            ThreatSeverity.Low,
            ThreatCategories.InputMalformed,
            "threat.empty_metadata",
            "Empty metadata should stay empty.",
            GovernanceDecisionOutcome.Warning,
            metadata: new Dictionary<string, string>());

        Assert.Empty(assessment.Metadata);
    }

    /// <summary>
    /// Test that when a threat assessment is created with blank metadata keys, those keys are ignored and not included in the final metadata collection, ensuring that only valid metadata entries are preserved.
    /// </summary>
    [Fact]
    public void ThreatAssessmentBlankMetadataKeysAreIgnored()
    {
        var assessment = ThreatAssessment.Create(
            ThreatSeverity.Low,
            ThreatCategories.InputMalformed,
            "threat.blank_metadata_key",
            "Blank metadata keys should be ignored.",
            GovernanceDecisionOutcome.Warning,
            metadata: new Dictionary<string, string>
            {
                [" "] = "ignored"
            });

        Assert.Empty(assessment.Metadata);
    }

    /// <summary>
    /// Test that when a threat assessment is created with blank category, it throws an ArgumentException, ensuring that the category must be a non-empty string.
    /// </summary>
    [Fact]
    public void ThreatAssessmentRejectsBlankCategory()
    {
        _ = Assert.Throws<ArgumentException>(() => ThreatAssessment.Create(
            ThreatSeverity.Low,
            " ",
            "threat.blank_category",
            "Blank category should be rejected.",
            GovernanceDecisionOutcome.Warning));
    }

    /// <summary>
    /// Test that when a threat assessment is created with blank reason code, it throws an ArgumentException, ensuring that the reason code must be a non-empty string.
    /// </summary>
    [Fact]
    public void ThreatAssessmentRejectsBlankReasonCode()
    {
        _ = Assert.Throws<ArgumentException>(() => ThreatAssessment.Create(
            ThreatSeverity.Low,
            ThreatCategories.InputMalformed,
            " ",
            "Blank reason code should be rejected.",
            GovernanceDecisionOutcome.Warning));
    }

    /// <summary>
    /// Test that when a threat assessment is created with blank description, it throws an ArgumentException, ensuring that the description must be a non-empty string.
    /// </summary>
    [Fact]
    public void ThreatAssessmentRejectsBlankDescription()
    {
        _ = Assert.Throws<ArgumentException>(() => ThreatAssessment.Create(
            ThreatSeverity.Low,
            ThreatCategories.InputMalformed,
            "threat.blank_description",
            " ",
            GovernanceDecisionOutcome.Warning));
    }

    /// <summary>
    /// Test that when a threat assessment is created with a negative confidence value, it throws an ArgumentOutOfRangeException, ensuring that the confidence must be within the range of 0 to 1.
    /// </summary>
    [Fact]
    public void ThreatAssessmentRejectsNegativeConfidence()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => ThreatAssessment.Create(
            ThreatSeverity.Low,
            ThreatCategories.InputMalformed,
            "threat.negative_confidence",
            "Negative confidence should be rejected.",
            GovernanceDecisionOutcome.Warning,
            confidence: -0.1D));
    }

    /// <summary>
    /// Test that when a threat assessment is created with a blank contributor name, the contributor metadata is omitted from the operation reason.
    /// </summary>
    [Fact]
    public void ThreatAssessmentOperationReasonOmitsContributorMetadataWhenNameIsBlank()
    {
        var assessment = ThreatAssessment.Create(
            ThreatSeverity.Low,
            ThreatCategories.InputMalformed,
            "threat.input_malformed",
            "Malformed input indicator was reported.",
            GovernanceDecisionOutcome.Warning);

        var reason = assessment.ToOperationReason(" ");

        Assert.False(reason.Metadata.ContainsKey("threat.contributor"));
        Assert.Equal(ThreatCategories.InputMalformed, reason.Metadata["threat.category"]);
    }

    /// <summary>
    /// Test that when a threat assessment is created with a null metadata value, the value is normalized to an empty string in the operation reason, ensuring that null values do not propagate into the metadata collection.
    /// </summary>
    [Fact]
    public void ThreatAssessmentOperationReasonNormalizesNullMetadataValue()
    {
        var assessment = ThreatAssessment.Create(
            ThreatSeverity.Low,
            ThreatCategories.InputMalformed,
            "threat.null_metadata_value",
            "Null metadata value should be normalized.",
            GovernanceDecisionOutcome.Warning,
            metadata: new Dictionary<string, string>
            {
                ["nullable.value"] = null!
            });

        var reason = assessment.ToOperationReason();

        Assert.Equal(string.Empty, reason.Metadata["nullable.value"]);
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-threat-branch-123",
            PolicyVersion = "v-threat-branch",
            PolicyHash = "hash-threat-branch"
        };
    }

    private static StaticThreatContributor CreateWarningContributor()
    {
        return new StaticThreatContributor(
            "warning-threat-contributor",
            ThreatAssessment.Create(
                ThreatSeverity.Low,
                ThreatCategories.InputMalformed,
                "threat.warning",
                "Warning threat indicator was reported.",
                GovernanceDecisionOutcome.Warning));
    }

    private sealed class TestPolicyContext : IAsiBackboneConstraintEvaluationContext
    {
        public string? CorrelationId { get; init; }

        public string? PolicyVersion { get; init; }

        public string? PolicyHash { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class StaticConstraint(ConstraintEvaluationResult result) : IAsiBackboneConstraint<TestPolicyContext>
    {
        private readonly ConstraintEvaluationResult result = result;

        public string Name => "static-threat-branch-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
        }
    }

    private sealed class DelegateConstraint(
        Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate) : IAsiBackboneConstraint<TestPolicyContext>
    {
        private readonly Func<TestPolicyContext, CancellationToken, ConstraintEvaluationResult> evaluate = evaluate;

        public string Name => "delegate-threat-branch-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(evaluate(context, cancellationToken));
        }
    }

    private sealed class ThrowingConstraint(Exception exception) : IAsiBackboneConstraint<TestPolicyContext>
    {
        private readonly Exception exception = exception;

        public string Name => "throwing-threat-branch-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            throw exception;
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

    private sealed class AlwaysAllowDecisionPolicy : IAsiBackboneDecisionPolicy<TestPolicyContext>
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

    private sealed class AlwaysDenyDecisionPolicy : IAsiBackboneDecisionPolicy<TestPolicyContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            TestPolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceDecision.Deny(
                "decision.policy_denied",
                "Decision policy denied the operation.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }
    }
}
