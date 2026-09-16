using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.ThreatModeling;
using Microsoft.Extensions.Logging;

namespace AsiBackbone.Core.Evaluation;

/// <summary>
/// Fluent builder for <see cref="DefaultGovernancePolicyEvaluator{TContext}" />.
/// </summary>
/// <remarks>
/// Every call to <see cref="Build" /> creates an independent evaluator that snapshots the constraints and threat model
/// contributors added so far. Later builder changes do not affect evaluators that were already built. Options are
/// validated and frozen by the first evaluator built with them.
/// </remarks>
/// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
public sealed class GovernancePolicyEvaluatorBuilder<TContext>
    where TContext : IGovernanceEvaluationContext
{
    private readonly List<IGovernanceConstraint<TContext>> constraints = [];
    private readonly List<IThreatModelContributor<TContext>> threatModelContributors = [];
    private IGovernanceDecisionPolicy<TContext>? decisionPolicy;
    private GovernancePolicyOptions? options;
    private ILogger<DefaultGovernancePolicyEvaluator<TContext>>? logger;

    internal GovernancePolicyEvaluatorBuilder()
    {
    }

    /// <summary>
    /// Adds a constraint to the end of the active policy structure.
    /// </summary>
    /// <param name="constraint">The constraint to add.</param>
    /// <returns>The same builder instance.</returns>
    public GovernancePolicyEvaluatorBuilder<TContext> AddConstraint(IGovernanceConstraint<TContext> constraint)
    {
        ArgumentNullException.ThrowIfNull(constraint);
        constraints.Add(constraint);
        return this;
    }

    /// <summary>
    /// Adds constraints, in order, to the end of the active policy structure.
    /// </summary>
    /// <param name="constraints">The constraints to add.</param>
    /// <returns>The same builder instance.</returns>
    public GovernancePolicyEvaluatorBuilder<TContext> AddConstraints(IEnumerable<IGovernanceConstraint<TContext>> constraints)
    {
        ArgumentNullException.ThrowIfNull(constraints);
        foreach (IGovernanceConstraint<TContext> constraint in constraints)
        {
            AddConstraint(constraint);
        }

        return this;
    }

    /// <summary>
    /// Adds a threat model contributor that inspects the context before constraint composition.
    /// </summary>
    /// <param name="contributor">The contributor to add.</param>
    /// <returns>The same builder instance.</returns>
    public GovernancePolicyEvaluatorBuilder<TContext> AddThreatModelContributor(IThreatModelContributor<TContext> contributor)
    {
        ArgumentNullException.ThrowIfNull(contributor);
        threatModelContributors.Add(contributor);
        return this;
    }

    /// <summary>
    /// Adds threat model contributors, in order, that inspect the context before constraint composition.
    /// </summary>
    /// <param name="contributors">The contributors to add.</param>
    /// <returns>The same builder instance.</returns>
    public GovernancePolicyEvaluatorBuilder<TContext> AddThreatModelContributors(IEnumerable<IThreatModelContributor<TContext>> contributors)
    {
        ArgumentNullException.ThrowIfNull(contributors);
        foreach (IThreatModelContributor<TContext> contributor in contributors)
        {
            AddThreatModelContributor(contributor);
        }

        return this;
    }

    /// <summary>
    /// Sets the decision policy applied after composition, replacing any previously set policy.
    /// </summary>
    /// <param name="decisionPolicy">The decision policy, or <see langword="null" /> for none.</param>
    /// <returns>The same builder instance.</returns>
    public GovernancePolicyEvaluatorBuilder<TContext> WithDecisionPolicy(IGovernanceDecisionPolicy<TContext>? decisionPolicy)
    {
        this.decisionPolicy = decisionPolicy;
        return this;
    }

    /// <summary>
    /// Sets the evaluator options, replacing any previously set options.
    /// </summary>
    /// <param name="options">The options, or <see langword="null" /> for defaults.</param>
    /// <returns>The same builder instance.</returns>
    public GovernancePolicyEvaluatorBuilder<TContext> WithOptions(GovernancePolicyOptions? options)
    {
        this.options = options;
        return this;
    }

    /// <summary>
    /// Sets the logger used to emit operational warning signals, replacing any previously set logger.
    /// </summary>
    /// <param name="logger">The logger, or <see langword="null" /> for none.</param>
    /// <returns>The same builder instance.</returns>
    public GovernancePolicyEvaluatorBuilder<TContext> WithLogger(ILogger<DefaultGovernancePolicyEvaluator<TContext>>? logger)
    {
        this.logger = logger;
        return this;
    }

    /// <summary>
    /// Creates an evaluator from the current builder state.
    /// </summary>
    /// <returns>A new evaluator.</returns>
    public DefaultGovernancePolicyEvaluator<TContext> Build()
    {
        return new DefaultGovernancePolicyEvaluator<TContext>(constraints, threatModelContributors, decisionPolicy, options, logger);
    }
}
