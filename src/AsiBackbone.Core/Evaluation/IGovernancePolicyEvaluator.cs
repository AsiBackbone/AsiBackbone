using AsiBackbone.Core.Constraints;

namespace AsiBackbone.Core.Evaluation;

/// <summary>
/// Identifies the preferred domain-qualified governance policy evaluator contract.
/// </summary>
/// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
public interface IGovernancePolicyEvaluator<in TContext> : IAsiBackbonePolicyEvaluator<TContext>
    where TContext : IAsiBackboneConstraintEvaluationContext
{
}
