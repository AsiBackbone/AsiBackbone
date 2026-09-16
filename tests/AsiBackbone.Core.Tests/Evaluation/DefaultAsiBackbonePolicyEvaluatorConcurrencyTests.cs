using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.ThreatModeling;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Regression coverage for the documented evaluator concurrency and construction-snapshot contract.
/// These tests do not prove thread safety for arbitrary host extensions.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorConcurrencyTests
{
    /// <summary>
    /// Tests that the constructor freezes options and preserves configured behavior.
    /// </summary>
    [Fact]
    public async Task ConstructorFreezesOptionsAndPreservesConfiguredBehavior()
    {
        var options = new AsiBackbonePolicyEvaluatorOptions
        {
            DenyWhenNoConstraints = false
        };

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestContext>(
            constraints: [],
            decisionPolicy: null,
            options: options, threatModelContributors: null, logger: null);

        _ = Assert.Throws<InvalidOperationException>(() => options.DenyWhenNoConstraints = true);

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            CreateContext(),
            Xunit.TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
    }

    /// <summary>
    /// Tests that the constructor snapshots the constraint collection and ordering.
    /// </summary>
    [Fact]
    public async Task ConstructorSnapshotsConstraintCollectionAndOrdering()
    {
        var constraints = new List<IAsiBackboneConstraint<TestContext>>
        {
            new FixedConstraint("first", ConstraintEvaluationResult.Warning("warning.first", "First warning.")),
            new FixedConstraint("second", ConstraintEvaluationResult.Deny("denial.second", "Second denial."))
        };

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestContext>(
            constraints,
            decisionPolicy: null,
            options: new AsiBackbonePolicyEvaluatorOptions
            {
                ShortCircuitOnFirstDenial = true
            }, threatModelContributors: null, logger: null);

        constraints.Clear();
        constraints.Add(new FixedConstraint("replacement", ConstraintEvaluationResult.Allow()));

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            CreateContext(),
            Xunit.TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal(["warning.first", "denial.second"], decision.ReasonCodes);
    }

    /// <summary>
    /// Tests that the constructor snapshots the threat contributor collection and ordering.
    /// </summary>
    [Fact]
    public async Task ConstructorSnapshotsThreatContributorCollectionAndOrdering()
    {
        var contributors = new List<IThreatModelContributor<TestContext>>
        {
            new FixedThreatContributor("first", CreateWarningAssessment("threat.first")),
            new FixedThreatContributor("second", CreateWarningAssessment("threat.second"))
        };

        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestContext>(
            constraints: [new FixedConstraint("allow", ConstraintEvaluationResult.Allow())],
            threatModelContributors: contributors,
            decisionPolicy: null,
            options: null, logger: null);

        contributors.Clear();
        contributors.Add(new FixedThreatContributor("replacement", ThreatAssessment.NoThreat()));

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            CreateContext(),
            Xunit.TestContext.Current.CancellationToken);

        Assert.True(decision.IsWarning);
        Assert.Equal(["threat.first", "threat.second"], decision.ReasonCodes);
    }

    /// <summary>
    /// Tests that concurrent evaluation with stateless extensions produces deterministic results.
    /// </summary>
    [Fact]
    public async Task ConcurrentEvaluationWithStatelessExtensionsProducesDeterministicResults()
    {
        var evaluator = new DefaultAsiBackbonePolicyEvaluator<TestContext>(
            constraints:
            [
                new FixedConstraint("warning", ConstraintEvaluationResult.Warning("policy.warning", "Warning.")),
                new FixedConstraint("allow", ConstraintEvaluationResult.Allow())
            ], threatModelContributors: null, decisionPolicy: null, options: null, logger: null);

        Task<GovernanceDecision>[] evaluations = [.. Enumerable.Range(0, 64)
            .Select(index => evaluator
                .EvaluateAsync(
                    CreateContext($"correlation-{index}"),
                    Xunit.TestContext.Current.CancellationToken)
                .AsTask())];

        GovernanceDecision[] decisions = await Task.WhenAll(evaluations);

        Assert.All(decisions, decision =>
        {
            Assert.True(decision.IsWarning);
            Assert.Equal("policy.warning", Assert.Single(decision.ReasonCodes));
        });

        Assert.Equal(
            Enumerable.Range(0, 64).Select(index => $"correlation-{index}"),
            decisions.Select(decision => decision.CorrelationId));
    }

    private static TestContext CreateContext(string correlationId = "correlation-1")
    {
        return new TestContext(correlationId);
    }

    private static ThreatAssessment CreateWarningAssessment(string reasonCode)
    {
        return ThreatAssessment.Create(
            ThreatSeverity.Low,
            ThreatCategories.InputMalformed,
            reasonCode,
            "Threat warning.",
            GovernanceDecisionOutcome.Warning);
    }

    private sealed class TestContext(string correlationId) : IAsiBackboneConstraintEvaluationContext
    {
        /// <summary>
        /// Gets the correlation ID for the test context.
        /// </summary>
        public string? CorrelationId { get; } = correlationId;

        /// <summary>
        /// Gets the policy version for the test context.
        /// </summary>
        public string? PolicyVersion { get; } = "policy-v1";

        /// <summary>
        /// Gets the policy hash for the test context.
        /// </summary>
        public string? PolicyHash { get; } = "policy-hash";

        /// <summary>
        /// Gets the metadata for the test context.
        /// </summary>
        public IReadOnlyDictionary<string, string> Metadata { get; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class FixedConstraint(
        string name,
        ConstraintEvaluationResult result) : IAsiBackboneConstraint<TestContext>
    {
        /// <summary>
        /// Gets the name of the constraint.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Evaluates the constraint asynchronously.
        /// </summary>
        /// <param name="context">The evaluation context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The constraint evaluation result.</returns>
        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FixedThreatContributor(
        string name,
        ThreatAssessment assessment) : IThreatModelContributor<TestContext>
    {
        /// <summary>
        /// Gets the name of the threat contributor.
        /// </summary>
        public string Name { get; } = name;

        /// <summary>
        /// Assesses the threat asynchronously.
        /// </summary>
        /// <param name="context">The evaluation context.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The threat assessment.</returns>
        public ValueTask<ThreatAssessment> AssessAsync(
            TestContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(assessment);
        }
    }
}
