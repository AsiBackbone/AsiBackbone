using AsiBackbone.AspNetCore.Outbox;
using AsiBackbone.DependencyInjection;

namespace AsiBackbone.AspNetCore.DependencyInjection;

/// <summary>
/// Provides explicit builder facade extension methods for ASP.NET Core host integration.
/// </summary>
public static class AsiBackboneAspNetCoreBuilderExtensions
{
    /// <summary>
    /// Adds ASP.NET Core endpoint governance services through the AsiBackbone builder facade using default options.
    /// </summary>
    public static IAsiBackboneBuilder UseAspNetCoreEndpointGovernance(this IAsiBackboneBuilder builder)
    {
        return builder.UseAspNetCoreEndpointGovernance(_ => { });
    }

    /// <summary>
    /// Adds ASP.NET Core endpoint governance services through the AsiBackbone builder facade using configured options.
    /// </summary>
    public static IAsiBackboneBuilder UseAspNetCoreEndpointGovernance(
        this IAsiBackboneBuilder builder,
        Action<AspNetCoreGovernanceOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        _ = builder.Services.AddAsiBackboneAspNetCore(configure);
        return builder;
    }

    /// <summary>
    /// Opts in to signed capability grant endpoint validation through the AsiBackbone builder facade.
    /// </summary>
    /// <remarks>
    /// See <see cref="AsiBackboneSignedCapabilityGrantServiceCollectionExtensions.AddAsiBackboneSignedCapabilityGrants" />
    /// for the services the host must also supply.
    /// </remarks>
    public static IAsiBackboneBuilder UseSignedCapabilityGrants(
        this IAsiBackboneBuilder builder,
        Action<CapabilityGrants.SignedCapabilityGrantOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        _ = builder.Services.AddAsiBackboneSignedCapabilityGrants(configure);
        return builder;
    }

    /// <summary>
    /// Adds the form-field capability grant request-binding provider through the AsiBackbone builder facade.
    /// </summary>
    public static IAsiBackboneBuilder UseFormFieldCapabilityGrantBinding(
        this IAsiBackboneBuilder builder,
        Action<CapabilityGrants.FormFieldCapabilityGrantBindingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        _ = builder.Services.AddAsiBackboneFormFieldCapabilityGrantBinding(configure);
        return builder;
    }

    /// <summary>
    /// Adds the host-owned outbox drain worker through the AsiBackbone builder facade using default options.
    /// </summary>
    public static IAsiBackboneBuilder UseGovernanceOutboxDrain(this IAsiBackboneBuilder builder)
    {
        return builder.UseGovernanceOutboxDrain(_ => { });
    }

    /// <summary>
    /// Adds the host-owned outbox drain worker through the AsiBackbone builder facade using configured options.
    /// </summary>
    public static IAsiBackboneBuilder UseGovernanceOutboxDrain(
        this IAsiBackboneBuilder builder,
        Action<GovernanceOutboxDrainWorkerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        _ = builder.Services.AddAsiBackboneGovernanceOutboxDrainWorker(configure);
        return builder;
    }
}
