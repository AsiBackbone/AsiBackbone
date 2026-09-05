using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Evaluation;
using Microsoft.AspNetCore.Builder;

namespace AsiBackbone.AspNetCore.Endpoints;

/// <summary>
/// Provides Minimal API / endpoint route-builder helpers for adding AsiBackbone governance metadata.
/// </summary>
public static class AsiBackboneEndpointGovernanceRouteBuilderExtensions
{
    /// <summary>
    /// Marks a Minimal API route handler endpoint with the host-defined decision policy that governs it.
    /// </summary>
    /// <remarks>
    /// This records a marker, not an enforcement rule. The framework does not resolve <typeparamref name="TPolicy" />
    /// or select constraints from it: the registered <c>IAsiBackbonePolicyEvaluator</c> evaluates every registered
    /// constraint on every governed endpoint regardless of which policy an endpoint is marked with. The marker reaches
    /// evaluation as the <c>endpoint.policy_types</c> metadata entry, where a host-supplied
    /// <see cref="IAsiBackboneDecisionPolicy{TContext}" /> can read it and vary its own outcome. Marking two endpoints
    /// with different policy types does not by itself make them evaluate differently.
    /// </remarks>
    /// <typeparam name="TPolicy">The host-registered decision policy that governs the endpoint.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    public static RouteHandlerBuilder MarkGovernancePolicy<TPolicy>(this RouteHandlerBuilder builder)
        where TPolicy : IAsiBackboneDecisionPolicy<AsiBackboneConstraintEvaluationContext>
    {
        return builder.MarkGovernancePolicy(typeof(TPolicy));
    }

    /// <summary>
    /// Marks an endpoint with the host-defined policy type that governs it.
    /// </summary>
    /// <remarks>
    /// This records a marker, not an enforcement rule. See
    /// <see cref="MarkGovernancePolicy{TPolicy}(RouteHandlerBuilder)" /> for what the framework does and does not do
    /// with the recorded type. This overload accepts any type so hosts can mark endpoints with a plain marker type
    /// rather than a registered decision policy.
    /// </remarks>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="policyType">The host-defined policy marker or decision policy type.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    public static TBuilder MarkGovernancePolicy<TBuilder>(this TBuilder builder, Type policyType)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(policyType);
        return AddEndpointMetadata(builder, new RequireGovernancePolicyAttribute(policyType));
    }

    /// <summary>
    /// Adds a host-defined governance policy marker to a Minimal API route handler endpoint.
    /// </summary>
    /// <typeparam name="TPolicy">The host-defined policy marker or resolver type.</typeparam>
    /// <param name="builder">The route handler builder.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    [Obsolete("The framework never resolved the policy type or selected constraints from it, so the 'Require' name overstated what this does. Use MarkGovernancePolicy, which records the same metadata under an accurate name.")]
    public static RouteHandlerBuilder RequireGovernancePolicy<TPolicy>(this RouteHandlerBuilder builder)
    {
        return builder.MarkGovernancePolicy(typeof(TPolicy));
    }

    /// <summary>
    /// Adds a host-defined governance policy marker to an endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="policyType">The host-defined policy marker or resolver type.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    [Obsolete("The framework never resolved the policy type or selected constraints from it, so the 'Require' name overstated what this does. Use MarkGovernancePolicy, which records the same metadata under an accurate name.")]
    public static TBuilder RequireGovernancePolicy<TBuilder>(this TBuilder builder, Type policyType)
        where TBuilder : IEndpointConventionBuilder
    {
        return builder.MarkGovernancePolicy(policyType);
    }

    /// <summary>
    /// Adds endpoint-scoped metadata requesting latency-optimized fast-abort policy evaluation after the first denied constraint result.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="enabled">Whether first-denial short-circuit metadata is enabled for the endpoint.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    public static TBuilder ShortCircuitOnFirstDenial<TBuilder>(this TBuilder builder, bool enabled = true)
        where TBuilder : IEndpointConventionBuilder
    {
        return AddEndpointMetadata(builder, new ShortCircuitOnFirstDenialAttribute(enabled));
    }

    /// <summary>
    /// Adds liability-handshake metadata to an endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    public static TBuilder RequireLiabilityHandshake<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return AddEndpointMetadata(builder, new RequireLiabilityHandshakeAttribute());
    }

    /// <summary>
    /// Adds a required capability-grant scope to an endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <param name="scope">The required capability-grant scope.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    public static TBuilder RequireCapabilityGrant<TBuilder>(this TBuilder builder, string scope)
        where TBuilder : IEndpointConventionBuilder
    {
        return AddEndpointMetadata(builder, new RequireCapabilityGrantAttribute(scope));
    }

    /// <summary>
    /// Adds governance-audit emission metadata to an endpoint.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    public static TBuilder EmitGovernanceAudit<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return AddEndpointMetadata(builder, new EmitGovernanceAuditAttribute());
    }

    /// <summary>
    /// Allows an endpoint to pass through endpoint governance middleware when strict governance metadata enforcement is enabled.
    /// </summary>
    /// <typeparam name="TBuilder">The endpoint convention builder type.</typeparam>
    /// <param name="builder">The endpoint convention builder.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    public static TBuilder AllowMissingGovernanceMetadata<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        return AddEndpointMetadata(builder, new AllowMissingGovernanceMetadataAttribute());
    }

    private static TBuilder AddEndpointMetadata<TBuilder>(TBuilder builder, object metadata)
        where TBuilder : IEndpointConventionBuilder
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(metadata);

        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(metadata));
        return builder;
    }
}
