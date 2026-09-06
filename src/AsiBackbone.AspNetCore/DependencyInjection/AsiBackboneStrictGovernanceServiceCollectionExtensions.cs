using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AsiBackbone.AspNetCore.DependencyInjection;

/// <summary>
/// Provides strict governance registration helpers for hosts that prefer fail-closed defaults.
/// </summary>
public static class AsiBackboneStrictGovernanceServiceCollectionExtensions
{
    /// <summary>
    /// Applies the strict governance profile for policy evaluation and ASP.NET Core endpoint governance options.
    /// </summary>
    /// <param name="services">The service collection to configure.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services" /> is <see langword="null" />.
    /// </exception>
    /// <remarks>
    /// This helper keeps strict posture explicit in the current 4.x line while giving production hosts a single opt-in call for
    /// fail-closed evaluation posture. It configures options only; hosts still own authentication, authorization,
    /// constraint registration, endpoint metadata, persistence, and execution enforcement.
    /// </remarks>
    public static IServiceCollection AddAsiBackboneStrictGovernance(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        _ = services.AddOptions<AsiBackbonePolicyEvaluatorOptions>()
            .Configure(ApplyStrictPolicyEvaluatorProfile)
            .Validate(static options => ValidateOptions(options), "Policy evaluator options must be valid.")
            .ValidateOnStart();

        _ = services.AddOptions<AsiBackboneEndpointGovernanceOptions>()
            .Configure(ApplyStrictEndpointGovernanceProfile)
            .Validate(static options => ValidateOptions(options), "Endpoint governance options must be valid.")
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Applies the strict governance profile through the explicit <c>AddAsiBackbone</c> builder facade.
    /// </summary>
    /// <param name="builder">The AsiBackbone builder facade being configured.</param>
    /// <returns>The same builder so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="builder" /> is <see langword="null" />.
    /// </exception>
    public static IAsiBackboneBuilder UseStrictGovernanceProfile(this IAsiBackboneBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.Services.AddAsiBackboneStrictGovernance();
        return builder;
    }

    private static void ApplyStrictPolicyEvaluatorProfile(AsiBackbonePolicyEvaluatorOptions options)
    {
        options.DenyWhenNoConstraints = true;
        options.TreatConstraintExceptionAsDenial = true;
        options.TreatThreatContributorExceptionAsDenial = true;
        options.PreventThreatAssessmentAllowDowngrade = true;
    }

    private static void ApplyStrictEndpointGovernanceProfile(AsiBackboneEndpointGovernanceOptions options)
    {
        options.FailClosedWhenPolicyEvaluatorMissing = true;
        options.FailClosedWhenCapabilityValidatorMissing = true;
        options.FailClosedWhenAuditSinkMissing = true;
        options.RequireGovernanceMetadata = true;
        options.IncludeDevelopmentDiagnosticsMetadataValues = false;
    }

    private static bool ValidateOptions(AsiBackbonePolicyEvaluatorOptions options)
    {
        try
        {
            options.Validate();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }

    private static bool ValidateOptions(AsiBackboneEndpointGovernanceOptions options)
    {
        try
        {
            options.Validate();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
    }
}
