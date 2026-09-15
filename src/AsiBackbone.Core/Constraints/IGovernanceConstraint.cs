namespace AsiBackbone.Core.Constraints;

/// <summary>
/// Identifies the preferred domain-qualified constraint contract for governance policy evaluation.
/// </summary>
/// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
/// <remarks>
/// This interface extends the 5.x product-prefixed contract so semantic 6.0 constraints remain directly consumable by
/// existing evaluator, DI, testing, and ASP.NET Core composition surfaces.
/// </remarks>
public interface IGovernanceConstraint<in TContext> : IAsiBackboneConstraint<TContext>
{
}
