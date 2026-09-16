using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Behavior coverage for hot-path policy evaluator composition branches.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorHotPathTests
{
    /// <summary>
    /// Validates that when all constraints allow, the decision is allowed and the constraints are evaluated in order.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateAllAllowConstraintsProducesAllowedDecisionAndRunsInOrder()
    {
        TestPolicyContext context = CreateContext();
        var observedOrder = new List<string>();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new DelegateConstraint(
                    (_, _) =>
                    {
                        observedOrder.Add("first");
                        return ConstraintEvaluationResult.Allow();
                    }),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        observedOrder.Add("second");
                        return ConstraintEvaluationResult.Allow();
                    })
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.True(decision.CanProceed);
        Assert.Empty(decision.ReasonCodes);
        Assert.Equal(["first", "second"], observedOrder);
        Assert.Equal(context.CorrelationId, decision.CorrelationId);
        Assert.Equal(context.PolicyVersion, decision.PolicyVersion);
        Assert.Equal(context.PolicyHash, decision.PolicyHash);
    }


    /// <summary>
    /// Validates that when a constraint produces a warning, the decision is allowed but contains the warning reasons.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateWarningConstraintReturnsWarningReasons()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(ConstraintEvaluationResult.Allow()),
                new StaticConstraint(
                    ConstraintEvaluationResult.Warning(
                        "constraint.warning",
                        "The constraint produced a warning."))
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsWarning);
        Assert.True(decision.CanProceed);
        Assert.Equal("constraint.warning", Assert.Single(decision.ReasonCodes));
        Assert.Equal("The constraint produced a warning.", Assert.Single(decision.Reasons).Message);
    }

    /// <summary>
    /// Validates that when a constraint denies the operation, the decision is denied and contains the denial reasons.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateDenialConstraintReturnsDenialReasons()
    {
        TestPolicyContext context = CreateContext();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new StaticConstraint(
                    ConstraintEvaluationResult.Deny(
                        "constraint.denied",
                        "The constraint denied the operation."))
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Equal("constraint.denied", Assert.Single(decision.ReasonCodes));
        Assert.Equal("The constraint denied the operation.", Assert.Single(decision.Reasons).Message);
    }

    /// <summary>
    /// Validates that when a mix of allow, warning, and deny constraints are evaluated, the order is preserved and warnings are suppressed when the decision is denied.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task EvaluateMixedResultsPreservesOrderAndSuppressesWarningsWhenDenied()
    {
        TestPolicyContext context = CreateContext();
        var observedOrder = new List<string>();
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestPolicyContext>(
            [
                new DelegateConstraint(
                    (_, _) =>
                    {
                        observedOrder.Add("allow");
                        return ConstraintEvaluationResult.Allow();
                    }),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        observedOrder.Add("warning");
                        return ConstraintEvaluationResult.Warning(
                            "constraint.warning",
                            "The constraint produced a warning.");
                    }),
                new DelegateConstraint(
                    (_, _) =>
                    {
                        observedOrder.Add("deny");
                        return ConstraintEvaluationResult.Deny(
                            "constraint.denied",
                            "The constraint denied the operation.");
                    })
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.False(decision.CanProceed);
        Assert.Equal(["allow", "warning", "deny"], observedOrder);
        Assert.Equal("constraint.denied", Assert.Single(decision.ReasonCodes));
        Assert.DoesNotContain("constraint.warning", decision.ReasonCodes);
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-hot-path-123",
            PolicyVersion = "v-hot-path",
            PolicyHash = "hash-hot-path"
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

    private sealed class StaticConstraint(ConstraintEvaluationResult result) : IAsiBackboneConstraint<TestPolicyContext>
    {
        private readonly ConstraintEvaluationResult result = result;

        public string Name => "static-hot-path-constraint";

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

        public string Name => "delegate-hot-path-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(evaluate(context, cancellationToken));
        }
    }
}
