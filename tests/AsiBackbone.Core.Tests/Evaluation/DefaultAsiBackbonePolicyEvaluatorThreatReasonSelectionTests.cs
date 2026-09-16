using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.ThreatModeling;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Verifies that threat decisions retain reasons associated with the selected restrictive outcome.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorThreatReasonSelectionTests
{
    /// <summary>
    /// Verifies that an earlier warning cannot replace the reason associated with a later, more restrictive threat outcome.
    /// </summary>
    /// <param name="selectedOutcome">The restrictive outcome selected by the later contributor.</param>
    /// <param name="selectedReasonCode">The reason code associated with the restrictive outcome.</param>
    /// <param name="selectedReasonMessage">The reason message associated with the restrictive outcome.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Theory]
    [InlineData(
        GovernanceDecisionOutcome.Deferred,
        "threat.selected_deferred",
        "The selected threat requires deferred handling.")]
    [InlineData(
        GovernanceDecisionOutcome.AcknowledgmentRequired,
        "threat.selected_acknowledgment",
        "The selected threat requires acknowledgment.")]
    [InlineData(
        GovernanceDecisionOutcome.EscalationRecommended,
        "threat.selected_escalation",
        "The selected threat requires escalation.")]
    public async Task EvaluateWarningBeforeRestrictiveOutcomeUsesSelectedOutcomeReason(
        GovernanceDecisionOutcome selectedOutcome,
        string selectedReasonCode,
        string selectedReasonMessage)
    {
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [
                new StaticThreatContributor(
                    "warning-contributor",
                    ThreatAssessment.Create(
                        ThreatSeverity.Low,
                        ThreatCategories.InputMalformed,
                        "threat.warning_first",
                        "An earlier warning was reported.",
                        GovernanceDecisionOutcome.Warning)),
                new StaticThreatContributor(
                    "restrictive-contributor",
                    ThreatAssessment.Create(
                        ThreatSeverity.High,
                        ThreatCategories.AuditIntegrityRisk,
                        selectedReasonCode,
                        selectedReasonMessage,
                        selectedOutcome))
            ], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            CreateContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(selectedOutcome, decision.Outcome);
        OperationReason reason = Assert.Single(decision.Reasons);
        Assert.Equal(selectedReasonCode, reason.Code);
        Assert.Equal(selectedReasonMessage, reason.Message);
        Assert.DoesNotContain("threat.warning_first", decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that multiple contributors selecting the same restrictive outcome retain the first matching reason in contributor order.
    /// </summary>
    /// <param name="selectedOutcome">The shared restrictive outcome reported by multiple contributors.</param>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Theory]
    [InlineData(GovernanceDecisionOutcome.Deferred)]
    [InlineData(GovernanceDecisionOutcome.AcknowledgmentRequired)]
    [InlineData(GovernanceDecisionOutcome.EscalationRecommended)]
    public async Task EvaluateMultipleMatchingRestrictiveOutcomesUsesFirstMatchingReason(
        GovernanceDecisionOutcome selectedOutcome)
    {
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [new StaticConstraint(ConstraintEvaluationResult.Allow())],
            [
                new StaticThreatContributor(
                    "warning-contributor",
                    ThreatAssessment.Create(
                        ThreatSeverity.Low,
                        ThreatCategories.InputMalformed,
                        "threat.warning_first",
                        "An earlier warning was reported.",
                        GovernanceDecisionOutcome.Warning)),
                new StaticThreatContributor(
                    "first-matching-contributor",
                    ThreatAssessment.Create(
                        ThreatSeverity.High,
                        ThreatCategories.AuditIntegrityRisk,
                        "threat.first_matching",
                        "The first matching restrictive reason was reported.",
                        selectedOutcome)),
                new StaticThreatContributor(
                    "second-matching-contributor",
                    ThreatAssessment.Create(
                        ThreatSeverity.Critical,
                        ThreatCategories.CapabilityTokenMismatch,
                        "threat.second_matching",
                        "The second matching restrictive reason was reported.",
                        selectedOutcome))
            ], decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            CreateContext(),
            TestContext.Current.CancellationToken);

        Assert.Equal(selectedOutcome, decision.Outcome);
        OperationReason reason = Assert.Single(decision.Reasons);
        Assert.Equal("threat.first_matching", reason.Code);
        Assert.Equal("The first matching restrictive reason was reported.", reason.Message);
        Assert.DoesNotContain("threat.second_matching", decision.ReasonCodes);
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-threat-reason-selection",
            PolicyVersion = "v-threat-reason-selection",
            PolicyHash = "hash-threat-reason-selection"
        };
    }

    private sealed class TestPolicyContext : IAsiBackboneConstraintEvaluationContext
    {
        public string? CorrelationId { get; init; }

        public string? PolicyVersion { get; init; }

        public string? PolicyHash { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class StaticConstraint(ConstraintEvaluationResult result) :
        IAsiBackboneConstraint<TestPolicyContext>
    {
        private readonly ConstraintEvaluationResult result = result;

        public string Name => "threat-reason-selection-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(result);
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
}
