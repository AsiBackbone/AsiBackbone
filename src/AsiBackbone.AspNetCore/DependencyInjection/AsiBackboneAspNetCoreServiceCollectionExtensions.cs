using AsiBackbone.AspNetCore.Actors;
using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.AspNetCore.Handshakes;
using AsiBackbone.AspNetCore.Outbox;
using AsiBackbone.AspNetCore.Results;
using AsiBackbone.Core.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AsiBackbone.AspNetCore.DependencyInjection;

/// <summary>
/// Provides dependency injection registration helpers for ASP.NET Core host integration.
/// </summary>
public static class AsiBackboneAspNetCoreServiceCollectionExtensions
{
    /// <summary>
    /// Adds ASP.NET Core integration services for AsiBackbone using default options.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddAsiBackboneAspNetCore(this IServiceCollection services)
    {
        return services.AddAsiBackboneAspNetCore(_ => { });
    }

    /// <summary>
    /// Adds ASP.NET Core integration services for AsiBackbone using configured options.
    /// </summary>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">The options configuration callback.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services" /> or <paramref name="configure" /> is <see langword="null" />.
    /// </exception>
    public static IServiceCollection AddAsiBackboneAspNetCore(
        this IServiceCollection services,
        Action<AsiBackboneAspNetCoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AsiBackboneAspNetCoreOptions options = new();
        configure(options);
        options.Validate();

        _ = services.AddOptions<AsiBackboneAspNetCoreOptions>()
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
            }, "ASP.NET Core integration options must be valid.")
            .ValidateOnStart();

        _ = services.AddOptions<AsiBackboneHttpActorContextOptions>()
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
            }, "HTTP actor context options must be valid.")
            .ValidateOnStart();

        _ = services.AddOptions<AsiBackboneHttpResultMappingOptions>()
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
            }, "HTTP result mapping options must be valid.")
            .ValidateOnStart();

        _ = services.AddOptions<AsiBackboneAcknowledgmentChallengeOptions>()
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
            }, "Acknowledgment challenge options must be valid.")
            .ValidateOnStart();

        _ = services.AddOptions<AsiBackboneEndpointGovernanceOptions>()
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
            }, "Endpoint governance options must be valid.")
            .ValidateOnStart();

        _ = services.AddLogging();
        _ = services.AddHttpContextAccessor();
        _ = services.AddScoped<IAsiBackboneHttpActorContextResolver, HttpContextAsiBackboneActorContextResolver>();
        _ = services.AddScoped<IAsiBackboneHttpRequestCorrelationResolver, HttpContextAsiBackboneRequestCorrelationResolver>();
        _ = services.AddScoped<IAsiBackboneAcknowledgmentChallengeService, DefaultAsiBackboneAcknowledgmentChallengeService>();
        _ = services.AddScoped<IAsiBackboneEndpointGovernanceService, DefaultAsiBackboneEndpointGovernanceService>();

        return services;
    }

    /// <summary>
    /// Adds the host-owned governance outbox drain worker using default scheduling options.
    /// </summary>
    /// <remarks>
    /// The worker is local to the process in which it is registered, so every replica of a horizontally scaled host runs
    /// its own worker against the same durable outbox. Coordination before provider emission comes from
    /// <see cref="AsiBackboneGovernanceOutboxOptions.UseClaimLeases" />, which is enabled by default and requires a store
    /// implementing <see cref="IAsiBackboneGovernanceOutboxClaimStore" />; the drain throws when it is enabled against a
    /// store that cannot claim. Disabling it allows two replicas to select and emit the same envelope, so a host that opts
    /// out should partition work, run the worker on a single role, or rely on provider-side idempotency.
    /// Claiming coordinates workers; it does not by itself create an exactly-once delivery guarantee.
    /// </remarks>
    /// <param name="services">The service collection to add services to.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddAsiBackboneGovernanceOutboxDrainWorker(this IServiceCollection services)
    {
        return services.AddAsiBackboneGovernanceOutboxDrainWorker(_ => { });
    }

    /// <summary>
    /// Adds the host-owned governance outbox drain worker using configured scheduling options.
    /// </summary>
    /// <remarks>
    /// The worker is local to the process in which it is registered, so every replica of a horizontally scaled host runs
    /// its own worker against the same durable outbox. Coordination before provider emission comes from
    /// <see cref="AsiBackboneGovernanceOutboxOptions.UseClaimLeases" />, which is enabled by default and requires a store
    /// implementing <see cref="IAsiBackboneGovernanceOutboxClaimStore" />; the drain throws when it is enabled against a
    /// store that cannot claim. Disabling it allows two replicas to select and emit the same envelope, so a host that opts
    /// out should partition work, run the worker on a single role, or rely on provider-side idempotency.
    /// Claiming coordinates workers; it does not by itself create an exactly-once delivery guarantee.
    /// </remarks>
    /// <param name="services">The service collection to add services to.</param>
    /// <param name="configure">The worker options configuration callback.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services" /> or <paramref name="configure" /> is <see langword="null" />.
    /// </exception>
    public static IServiceCollection AddAsiBackboneGovernanceOutboxDrainWorker(
        this IServiceCollection services,
        Action<AsiBackboneGovernanceOutboxDrainWorkerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AsiBackboneGovernanceOutboxDrainWorkerOptions options = new();
        configure(options);
        options.Validate();

        _ = services.AddOptions<AsiBackboneGovernanceOutboxDrainWorkerOptions>()
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
            }, "Governance outbox drain worker options must be valid.")
            .ValidateOnStart();

        _ = services.AddOptions<AsiBackboneGovernanceOutboxOptions>()
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
            }, "Governance outbox options must be valid.")
            .ValidateOnStart();

        services.TryAddScoped<AsiBackboneGovernanceOutboxDrain>();
        _ = services.AddHostedService<AsiBackboneGovernanceOutboxDrainHostedService>();

        return services;
    }
}
