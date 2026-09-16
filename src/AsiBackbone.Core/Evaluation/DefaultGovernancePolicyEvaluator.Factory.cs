using AsiBackbone.Core.Constraints;

namespace AsiBackbone.Core.Evaluation;

/// <summary>
/// Factory methods for <see cref="DefaultGovernancePolicyEvaluator{TContext}" />.
/// </summary>
public static class DefaultGovernancePolicyEvaluator
{
    /// <summary>
    /// Creates a fluent builder for configuring a <see cref="DefaultGovernancePolicyEvaluator{TContext}" />.
    /// </summary>
    /// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
    /// <returns>A new, empty builder.</returns>
    public static GovernancePolicyEvaluatorBuilder<TContext> CreateBuilder<TContext>()
        where TContext : IGovernanceEvaluationContext
    {
        return new GovernancePolicyEvaluatorBuilder<TContext>();
    }
}
