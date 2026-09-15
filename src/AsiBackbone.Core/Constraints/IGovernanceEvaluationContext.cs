namespace AsiBackbone.Core.Constraints;

/// <summary>
/// Identifies the preferred domain-qualified evaluation context contract for governance policy evaluation.
/// </summary>
/// <remarks>
/// The contract intentionally extends the 5.x product-prefixed interface so implementations can be consumed by
/// existing AsiBackbone policy evaluator and integration surfaces without adapters.
/// </remarks>
public interface IGovernanceEvaluationContext : IAsiBackboneConstraintEvaluationContext
{
}
