using AsiBackbone.Core.Signing;
using AsiBackbone.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AsiBackbone.Signing.LocalDevelopment;

/// <summary>
/// Provides explicit builder facade extension methods for local-development signing.
/// </summary>
public static class LocalDevelopmentSigningBuilderExtensions
{
    /// <summary>
    /// Adds local-development signing and verification through the AsiBackbone builder facade using default options.
    /// </summary>
    public static IAsiBackboneBuilder UseLocalDevelopmentSigning(this IAsiBackboneBuilder builder)
    {
        return builder.UseLocalDevelopmentSigning(LocalDevelopmentSigningOptions.Create());
    }

    /// <summary>
    /// Adds local-development signing and verification through the AsiBackbone builder facade using configured options.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured local-development signing options are invalid.
    /// </exception>
    public static IAsiBackboneBuilder UseLocalDevelopmentSigning(
        this IAsiBackboneBuilder builder,
        LocalDevelopmentSigningOptions options)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();
        ThrowIfProduction(options);

        _ = builder.Services.AddSingleton(options);
        _ = builder.Services.AddSingleton<LocalDevelopmentSigningService>();
        _ = builder.Services.AddSingleton<IAsiBackboneSigningService>(serviceProvider =>
            serviceProvider.GetRequiredService<LocalDevelopmentSigningService>());
        _ = builder.Services.AddSingleton<IAsiBackboneSignatureVerificationService>(serviceProvider =>
            serviceProvider.GetRequiredService<LocalDevelopmentSigningService>());

        return builder;
    }

    /// <summary>
    /// Rejects registration of the local-development signing provider in a production environment.
    /// </summary>
    /// <remarks>
    /// The provider generates its key per process, so artifacts it signs stop verifying after a restart. An analyzer can
    /// only see a call it can read; this guard covers the unconditional registration that reaches production at runtime.
    /// </remarks>
    /// <exception cref="InvalidOperationException">The environment is production and the options do not allow it.</exception>
    private static void ThrowIfProduction(LocalDevelopmentSigningOptions options)
    {
        if (options.AllowInProduction)
        {
            return;
        }

        string? environmentName = options.EnvironmentName
            ?? Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT")
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

        if (string.Equals(environmentName, "Production", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Local-development signing cannot be registered in the Production environment. Its key is generated per process and is never persisted, so signatures stop verifying after a restart. Register a managed key provider instead, or set LocalDevelopmentSigningOptions.AllowInProduction when this is deliberate.");
        }
    }
}
