using AsiBackbone.AspNetCore.CapabilityGrants;
using AsiBackbone.AspNetCore.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace AsiBackbone.AspNetCore.DependencyInjection;

/// <summary>
/// Provides opt-in registration for signed capability grant endpoint validation.
/// </summary>
/// <remarks>
/// Nothing here is registered by <see cref="AsiBackboneAspNetCoreServiceCollectionExtensions.AddAsiBackboneAspNetCore(IServiceCollection)" />.
/// Hosts that keep a claim-backed <see cref="IEndpointCapabilityGrantValidator" /> see no change.
/// </remarks>
public static class AsiBackboneSignedCapabilityGrantServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="SignedCapabilityGrantEndpointValidator" /> as the endpoint capability grant validator.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The host must also register an <see cref="Core.Signing.IGovernanceSignatureVerificationService" />, an
    /// <see cref="Core.CapabilityGrants.ICapabilityGrantUseStore" />, and an <see cref="ICapabilityGrantRequestBindingProvider" />,
    /// for example with <see cref="AddAsiBackboneFormFieldCapabilityGrantBinding" />. Any of them missing denies every
    /// request that requires a capability grant.
    /// </para>
    /// <para>
    /// Option validation runs through the Microsoft.Extensions.Options pipeline and at host startup.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">The options configuration callback, which must set the issuer and audience.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> or <paramref name="configure" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Another <see cref="IEndpointCapabilityGrantValidator" /> is already registered.</exception>
    public static IServiceCollection AddAsiBackboneSignedCapabilityGrants(
        this IServiceCollection services,
        Action<SignedCapabilityGrantOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        // Two validators would leave the effective one to registration order, which is the kind of choice a host should
        // make explicitly, so a conflicting registration is refused.
        foreach (ServiceDescriptor descriptor in services)
        {
            if (descriptor.ServiceType == typeof(IEndpointCapabilityGrantValidator)
                && descriptor.ImplementationType != typeof(SignedCapabilityGrantEndpointValidator))
            {
                throw new InvalidOperationException(
                    "An IEndpointCapabilityGrantValidator is already registered. Register either signed capability grants or a host validator, not both.");
            }
        }

        _ = services.AddOptions<SignedCapabilityGrantOptions>()
            .Configure(configure)
            .Validate(static options =>
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
            }, "Signed capability grant options must set an issuer, an audience, and valid limits.")
            .ValidateOnStart();

        _ = services.AddLogging();
        services.TryAddScoped<IEndpointCapabilityGrantValidator, SignedCapabilityGrantEndpointValidator>();

        return services;
    }

    /// <summary>
    /// Registers <see cref="FormFieldCapabilityGrantRequestBindingProvider" /> as the capability grant request-binding
    /// provider.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">An optional callback that configures the excluded field names.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    public static IServiceCollection AddAsiBackboneFormFieldCapabilityGrantBinding(
        this IServiceCollection services,
        Action<FormFieldCapabilityGrantBindingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        OptionsBuilder<FormFieldCapabilityGrantBindingOptions> optionsBuilder =
            services.AddOptions<FormFieldCapabilityGrantBindingOptions>();

        if (configure is not null)
        {
            _ = optionsBuilder.Configure(configure);
        }

        services.TryAddScoped<ICapabilityGrantRequestBindingProvider, FormFieldCapabilityGrantRequestBindingProvider>();

        return services;
    }
}
