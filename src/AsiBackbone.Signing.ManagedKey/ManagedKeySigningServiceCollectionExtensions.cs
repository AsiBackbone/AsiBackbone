using AsiBackbone.Core.Signing;
using Microsoft.Extensions.DependencyInjection;

namespace AsiBackbone.Signing.ManagedKey;

/// <summary>
/// Provides dependency injection registration helpers for managed-key signing.
/// </summary>
public static class ManagedKeySigningServiceCollectionExtensions
{
    /// <summary>
    /// Adds production-oriented managed-key signing with a host-owned managed-key client factory.
    /// </summary>
    /// <remarks>
    /// The production-oriented registration fails closed by default because <see cref="ManagedKeySigningOptions.ReturnUnsignedOnFailure" />
    /// defaults to <see langword="false" />.
    /// </remarks>
    public static IServiceCollection AddAsiBackboneManagedKeySigning(
        this IServiceCollection services,
        Action<ManagedKeySigningOptions> configure,
        Func<IServiceProvider, IManagedKeySigningClient> clientFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(clientFactory);

        ManagedKeySigningOptions options = new();
        configure(options);
        options.Validate();

        return AddManagedKeySigningCore(services, options, clientFactory);
    }

    /// <summary>
    /// Adds production-oriented managed-key signing using an already-registered <see cref="IManagedKeySigningClient" />.
    /// </summary>
    /// <remarks>
    /// The production-oriented registration fails closed by default because <see cref="ManagedKeySigningOptions.ReturnUnsignedOnFailure" />
    /// defaults to <see langword="false" />.
    /// </remarks>
    public static IServiceCollection AddAsiBackboneManagedKeySigning(
        this IServiceCollection services,
        Action<ManagedKeySigningOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        ManagedKeySigningOptions options = new();
        configure(options);
        options.Validate();

        return AddManagedKeySigningCore(services, options);
    }

    /// <summary>
    /// Adds local-validation managed-key signing with a host-owned managed-key client factory.
    /// </summary>
    /// <remarks>
    /// This helper explicitly sets <see cref="ManagedKeySigningOptions.ReturnUnsignedOnFailure" /> to <see langword="true" />
    /// so samples, tests, and diagnostics can inspect unsigned failure metadata. Do not use this helper as the default
    /// production registration unless host policy explicitly routes unsigned failure metadata.
    /// </remarks>
    public static IServiceCollection AddAsiBackboneManagedKeySigningForLocalValidation(
        this IServiceCollection services,
        Action<ManagedKeySigningOptions> configure,
        Func<IServiceProvider, IManagedKeySigningClient> clientFactory)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentNullException.ThrowIfNull(clientFactory);

        ManagedKeySigningOptions options = new();
        configure(options);
        options.ReturnUnsignedOnFailure = true;
        options.Validate();

        return AddManagedKeySigningCore(services, options, clientFactory);
    }

    /// <summary>
    /// Adds local-validation managed-key signing using an already-registered <see cref="IManagedKeySigningClient" />.
    /// </summary>
    /// <remarks>
    /// This helper explicitly sets <see cref="ManagedKeySigningOptions.ReturnUnsignedOnFailure" /> to <see langword="true" />
    /// so samples, tests, and diagnostics can inspect unsigned failure metadata. Do not use this helper as the default
    /// production registration unless host policy explicitly routes unsigned failure metadata.
    /// </remarks>
    public static IServiceCollection AddAsiBackboneManagedKeySigningForLocalValidation(
        this IServiceCollection services,
        Action<ManagedKeySigningOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        ManagedKeySigningOptions options = new();
        configure(options);
        options.ReturnUnsignedOnFailure = true;
        options.Validate();

        return AddManagedKeySigningCore(services, options);
    }

    private static IServiceCollection AddManagedKeySigningCore(
        IServiceCollection services,
        ManagedKeySigningOptions options,
        Func<IServiceProvider, IManagedKeySigningClient> clientFactory)
    {
        _ = services.AddSingleton(options);
        _ = services.AddSingleton(clientFactory);
        _ = services.AddSingleton(provider =>
        {
            ThrowIfProductionWithoutVerification(provider);
            return new ManagedKeySigningService(
                provider.GetRequiredService<ManagedKeySigningOptions>(),
                provider.GetRequiredService<IManagedKeySigningClient>());
        });
        _ = services.AddSingleton<IGovernanceSigningService>(provider =>
            provider.GetRequiredService<ManagedKeySigningService>());

        return services;
    }

    private static IServiceCollection AddManagedKeySigningCore(
        IServiceCollection services,
        ManagedKeySigningOptions options)
    {
        _ = services.AddSingleton(options);
        _ = services.AddSingleton(provider =>
        {
            ThrowIfProductionWithoutVerification(provider);
            return new ManagedKeySigningService(
                provider.GetRequiredService<ManagedKeySigningOptions>(),
                provider.GetRequiredService<IManagedKeySigningClient>());
        });
        _ = services.AddSingleton<IGovernanceSigningService>(provider =>
            provider.GetRequiredService<ManagedKeySigningService>());

        return services;
    }

    private static void ThrowIfProductionWithoutVerification(IServiceProvider serviceProvider)
    {
        string? environmentName = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        if (!string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (serviceProvider.GetService<IGovernanceSignatureVerificationService>() is null)
        {
            throw new InvalidOperationException(
                "Managed-key signing is being resolved in Production without an IGovernanceSignatureVerificationService. " +
                "This host signs artifacts but never verifies them, which silently breaks trust validation. " +
                "Register a verification implementation before resolving the signing service or move this registration behind a non-production guard.");
        }
    }
}
