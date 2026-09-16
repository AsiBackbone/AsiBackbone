using AsiBackbone.Core.Emissions;
using AsiBackbone.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

namespace AsiBackbone.OpenTelemetry;

/// <summary>
/// Provides explicit builder facade extension methods for OpenTelemetry governance emission.
/// </summary>
public static class OpenTelemetryGovernanceBuilderExtensions
{
    /// <summary>
    /// Adds the OpenTelemetry governance emission provider through the AsiBackbone builder facade using default options.
    /// </summary>
    public static IAsiBackboneBuilder UseOpenTelemetryEmission(this IAsiBackboneBuilder builder)
    {
        return builder.UseOpenTelemetryEmission(_ => { });
    }

    /// <summary>
    /// Adds the OpenTelemetry governance emission provider through the AsiBackbone builder facade using configured options.
    /// </summary>
    public static IAsiBackboneBuilder UseOpenTelemetryEmission(
        this IAsiBackboneBuilder builder,
        Action<OpenTelemetryGovernanceEmitterOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        OpenTelemetryGovernanceEmitterOptions options = new();
        configure(options);
        options.Validate();

        _ = builder.Services.AddSingleton(options);
        _ = builder.Services.AddSingleton<OpenTelemetryGovernanceEmitter>();
        _ = builder.Services.AddSingleton<IGovernanceEmitter>(serviceProvider =>
            serviceProvider.GetRequiredService<OpenTelemetryGovernanceEmitter>());

        return builder;
    }
}
