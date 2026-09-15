using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Xunit;

namespace AsiBackbone.Core.Tests.Naming;

/// <summary>
/// Verifies that the 6.0 domain-qualified API names compose with the existing product implementation surfaces.
/// </summary>
public sealed class GovernanceSemanticNamingTests
{
    /// <summary>
    /// Verifies that the preferred context, constraint, options, and evaluator contracts compose end to end.
    /// </summary>
    [Fact]
    public async Task DomainQualifiedPolicyTypesComposeEndToEnd()
    {
        var context = new GovernanceEvaluationContext(
            correlationId: " correlation-782 ",
            policyVersion: " v6 ",
            policyHash: " hash-782 ");
        var options = new GovernancePolicyOptions();
        IGovernanceConstraint<GovernanceEvaluationContext> constraint = new AllowConstraint();
        DefaultAsiBackbonePolicyEvaluator<GovernanceEvaluationContext> evaluator =
            new DefaultAsiBackbonePolicyEvaluator<GovernanceEvaluationContext>(
                [constraint],
                options: options);

        GovernanceDecision decision = await evaluator.EvaluateAsync(
            context,
            Xunit.TestContext.Current.CancellationToken);

        Assert.True(decision.IsAllowed);
        Assert.Equal("correlation-782", decision.CorrelationId);
        Assert.Equal("v6", decision.PolicyVersion);
        Assert.Equal("hash-782", decision.PolicyHash);
    }

    /// <summary>
    /// Verifies that the preferred actor context remains consumable through the existing actor contract.
    /// </summary>
    [Fact]
    public void DomainQualifiedActorContextPreservesActorContract()
    {
        GovernanceActorContext actor = GovernanceActorContext.Human(
            " actor-782 ",
            " Governance User ");

        Assert.Equal("actor-782", actor.ActorId);
        Assert.Equal("Governance User", actor.DisplayName);
        Assert.Equal(AsiBackboneActorType.Human, actor.ActorType);
        Assert.True(actor.IsKnown);
        Assert.True(actor.IsAuthenticated);
    }

    private sealed class AllowConstraint : IGovernanceConstraint<GovernanceEvaluationContext>
    {
        public string Name => "governance.naming.allow";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(context);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(ConstraintEvaluationResult.Allow());
        }
    }
}
