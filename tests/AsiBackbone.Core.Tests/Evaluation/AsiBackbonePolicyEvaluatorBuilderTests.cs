using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.ThreatModeling;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Coverage for <see cref="AsiBackbonePolicyEvaluatorBuilder{TContext}" />.
/// </summary>
public sealed class AsiBackbonePolicyEvaluatorBuilderTests
{
    /// <summary>
    /// Verifies that an empty builder produces an evaluator with default options, which deny an empty policy.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task BuildWithNoConfigurationUsesFailClosedDefaults()
    {
        DefaultAsiBackbonePolicyEvaluator<TestPolicyContext> evaluator =
            DefaultAsiBackbonePolicyEvaluator.CreateBuilder<TestPolicyContext>().Build();

        GovernanceDecision decision = await evaluator.EvaluateAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Contains(AsiBackbonePolicyEvaluatorOptions.DefaultNoConstraintsReasonCode, decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that constraints run in the order they were added across single and range additions.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task AddedConstraintsRunInInsertionOrder()
    {
        var observedOrder = new List<string>();

        DefaultAsiBackbonePolicyEvaluator<TestPolicyContext> evaluator = DefaultAsiBackbonePolicyEvaluator
            .CreateBuilder<TestPolicyContext>()
            .AddConstraint(new RecordingConstraint("first", observedOrder))
            .AddConstraints([new RecordingConstraint("second", observedOrder), new RecordingConstraint("third", observedOrder)])
            .AddConstraint(new RecordingConstraint("fourth", observedOrder))
            .Build();

        _ = await evaluator.EvaluateAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.Equal(["first", "second", "third", "fourth"], observedOrder);
    }

    /// <summary>
    /// Verifies that threat model contributors, the decision policy, and options are all passed to the evaluator.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task BuildAppliesContributorsDecisionPolicyAndOptions()
    {
        DefaultAsiBackbonePolicyEvaluator<TestPolicyContext> permissiveEvaluator = DefaultAsiBackbonePolicyEvaluator
            .CreateBuilder<TestPolicyContext>()
            .WithOptions(new AsiBackbonePolicyEvaluatorOptions { DenyWhenNoConstraints = false })
            .Build();

        DefaultAsiBackbonePolicyEvaluator<TestPolicyContext> threatEvaluator = DefaultAsiBackbonePolicyEvaluator
            .CreateBuilder<TestPolicyContext>()
            .AddConstraint(new RecordingConstraint("allow", []))
            .AddThreatModelContributors([new DeferringThreatContributor()])
            .Build();

        DefaultAsiBackbonePolicyEvaluator<TestPolicyContext> policyEvaluator = DefaultAsiBackbonePolicyEvaluator
            .CreateBuilder<TestPolicyContext>()
            .AddConstraint(new RecordingConstraint("allow", []))
            .WithDecisionPolicy(new DenyingDecisionPolicy())
            .Build();

        TestPolicyContext context = CreateContext();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Assert.True((await permissiveEvaluator.EvaluateAsync(context, cancellationToken)).IsAllowed);
        Assert.True((await threatEvaluator.EvaluateAsync(context, cancellationToken)).IsDeferred);
        Assert.True((await policyEvaluator.EvaluateAsync(context, cancellationToken)).IsDenied);
    }

    /// <summary>
    /// Verifies that later settings replace earlier ones, including resetting to <see langword="null" />.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task WithMethodsReplacePreviousValues()
    {
        DefaultAsiBackbonePolicyEvaluator<TestPolicyContext> evaluator = DefaultAsiBackbonePolicyEvaluator
            .CreateBuilder<TestPolicyContext>()
            .AddConstraint(new RecordingConstraint("allow", []))
            .WithDecisionPolicy(new DenyingDecisionPolicy())
            .WithDecisionPolicy(null)
            .WithLogger(null)
            .Build();

        GovernanceDecision decision = await evaluator.EvaluateAsync(CreateContext(), TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
    }

    /// <summary>
    /// Verifies that evaluators already built are not affected by later builder changes.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task BuiltEvaluatorSnapshotsBuilderState()
    {
        AsiBackbonePolicyEvaluatorBuilder<TestPolicyContext> builder = DefaultAsiBackbonePolicyEvaluator
            .CreateBuilder<TestPolicyContext>()
            .AddConstraint(new RecordingConstraint("allow", []));
        DefaultAsiBackbonePolicyEvaluator<TestPolicyContext> first = builder.Build();

        DefaultAsiBackbonePolicyEvaluator<TestPolicyContext> second = builder
            .AddThreatModelContributor(new DeferringThreatContributor())
            .Build();

        TestPolicyContext context = CreateContext();
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Assert.True((await first.EvaluateAsync(context, cancellationToken)).IsAllowed);
        Assert.True((await second.EvaluateAsync(context, cancellationToken)).IsDeferred);
    }

    /// <summary>
    /// Verifies that null collections and null items are rejected at the point they are added.
    /// </summary>
    [Fact]
    public void AddMethodsRejectNull()
    {
        AsiBackbonePolicyEvaluatorBuilder<TestPolicyContext> builder =
            DefaultAsiBackbonePolicyEvaluator.CreateBuilder<TestPolicyContext>();

        _ = Assert.Throws<ArgumentNullException>(() => builder.AddConstraint(null!));
        _ = Assert.Throws<ArgumentNullException>(() => builder.AddConstraints(null!));
        _ = Assert.Throws<ArgumentNullException>(() => builder.AddConstraints([null!]));
        _ = Assert.Throws<ArgumentNullException>(() => builder.AddThreatModelContributor(null!));
        _ = Assert.Throws<ArgumentNullException>(() => builder.AddThreatModelContributors(null!));
        _ = Assert.Throws<ArgumentNullException>(() => builder.AddThreatModelContributors([null!]));
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-builder-123",
            PolicyVersion = "v-builder",
            PolicyHash = "hash-builder"
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

    private sealed class RecordingConstraint(string name, List<string> observedOrder) : IAsiBackboneConstraint<TestPolicyContext>
    {
        public string Name { get; } = name;

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            observedOrder.Add(Name);
            return ValueTask.FromResult(ConstraintEvaluationResult.Allow());
        }
    }

    private sealed class DeferringThreatContributor : IThreatModelContributor<TestPolicyContext>
    {
        public string Name => "deferring-threat-contributor";

        public ValueTask<ThreatAssessment> AssessAsync(
            TestPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(ThreatAssessment.Create(
                ThreatSeverity.Medium,
                ThreatCategories.RegionPolicyMismatch,
                "threat.region_policy_mismatch",
                "Region policy mismatch was reported.",
                GovernanceDecisionOutcome.Deferred));
        }
    }

    private sealed class DenyingDecisionPolicy : IAsiBackboneDecisionPolicy<TestPolicyContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            TestPolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(GovernanceDecision.Deny(
                "policy.denied",
                "Decision policy denied the operation.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }
    }
}
