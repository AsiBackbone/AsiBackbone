using AsiBackbone.Core.Constraints;

namespace AsiBackbone.Core.Evaluation;

/// <summary>
/// Factory methods for <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}" />.
/// </summary>
public static class DefaultAsiBackbonePolicyEvaluator
{
    /// <summary>
    /// Creates a fluent builder for configuring a <see cref="DefaultAsiBackbonePolicyEvaluator{TContext}" />.
    /// </summary>
    /// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
    /// <returns>A new, empty builder.</returns>
    public static AsiBackbonePolicyEvaluatorBuilder<TContext> CreateBuilder<TContext>()
        where TContext : IAsiBackboneConstraintEvaluationContext
    {
        return new AsiBackbonePolicyEvaluatorBuilder<TContext>();
    }
}
