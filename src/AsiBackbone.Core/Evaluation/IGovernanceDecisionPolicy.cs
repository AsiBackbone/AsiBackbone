using AsiBackbone.Core.Constraints;

namespace AsiBackbone.Core.Evaluation;

/// <summary>
/// Identifies the preferred domain-qualified post-composition decision policy contract.
/// </summary>
/// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
public interface IGovernanceDecisionPolicy<in TContext> : IAsiBackboneDecisionPolicy<TContext>
    where TContext : IAsiBackboneConstraintEvaluationContext
{
}
