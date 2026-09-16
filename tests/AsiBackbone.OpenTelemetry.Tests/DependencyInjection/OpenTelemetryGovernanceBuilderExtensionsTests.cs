using AsiBackbone.Core.Emissions;
using AsiBackbone.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.OpenTelemetry.Tests.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="OpenTelemetryGovernanceBuilderExtensions" /> class.
/// </summary>
public sealed class OpenTelemetryGovernanceBuilderExtensionsTests
{
    /// <summary>
    /// Verifies that default OpenTelemetry emission registration adds the expected singleton services and returns the same builder.
    /// </summary>
    [Fact]
    public void UseOpenTelemetryEmissionRegistersDefaultsAndReturnsSameBuilder()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        IAsiBackboneBuilder result = builder.UseOpenTelemetryEmission();

        Assert.Same(builder, result);
        AssertSingletonRegistrations(services);

        using ServiceProvider provider = services.BuildServiceProvider();
        OpenTelemetryGovernanceEmitterOptions options =
            provider.GetRequiredService<OpenTelemetryGovernanceEmitterOptions>();
        OpenTelemetryGovernanceEmitter concreteEmitter =
            provider.GetRequiredService<OpenTelemetryGovernanceEmitter>();
        IGovernanceEmitter emitter =
            provider.GetRequiredService<IGovernanceEmitter>();

        Assert.True(options.EmitActivityEvents);
        Assert.True(options.EmitMetrics);
        Assert.Equal(OpenTelemetryGovernanceInstrumentation.ProviderName, options.ProviderName);
        Assert.Equal(OpenTelemetryGovernanceInstrumentation.DefaultActivityName, options.DefaultActivityName);
        Assert.Null(options.BeforeEmitAsync);
        Assert.Same(concreteEmitter, emitter);
    }

    /// <summary>
    /// Verifies that configured OpenTelemetry emission registration applies options and exposes them through dependency injection.
    /// </summary>
    [Fact]
    public void UseOpenTelemetryEmissionAppliesConfiguredOptionsAndReturnsSameBuilder()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);
        bool configureInvoked = false;

        IAsiBackboneBuilder result = builder.UseOpenTelemetryEmission(options =>
        {
            configureInvoked = true;
            options.EmitActivityEvents = false;
            options.EmitMetrics = false;
            options.ProviderName = "custom-open-telemetry";
            options.DefaultActivityName = "custom.governance.emit";
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        OpenTelemetryGovernanceEmitterOptions options =
            provider.GetRequiredService<OpenTelemetryGovernanceEmitterOptions>();
        OpenTelemetryGovernanceEmitter concreteEmitter =
            provider.GetRequiredService<OpenTelemetryGovernanceEmitter>();
        IGovernanceEmitter emitter =
            provider.GetRequiredService<IGovernanceEmitter>();

        Assert.Same(builder, result);
        Assert.True(configureInvoked);
        Assert.False(options.EmitActivityEvents);
        Assert.False(options.EmitMetrics);
        Assert.Equal("custom-open-telemetry", options.ProviderName);
        Assert.Equal("custom.governance.emit", options.DefaultActivityName);
        Assert.Same(concreteEmitter, emitter);
    }

    /// <summary>
    /// Verifies that default OpenTelemetry emission registration rejects a null builder.
    /// </summary>
    [Fact]
    public void UseOpenTelemetryEmissionDefaultOverloadRejectsNullBuilder()
    {
        IAsiBackboneBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder!.UseOpenTelemetryEmission());

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Verifies that configured OpenTelemetry emission registration rejects a null builder.
    /// </summary>
    [Fact]
    public void UseOpenTelemetryEmissionConfiguredOverloadRejectsNullBuilder()
    {
        IAsiBackboneBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder!.UseOpenTelemetryEmission(static _ => { }));

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Verifies that configured OpenTelemetry emission registration rejects a null configuration callback.
    /// </summary>
    [Fact]
    public void UseOpenTelemetryEmissionRejectsNullConfigureCallback()
    {
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(new ServiceCollection());
        Action<OpenTelemetryGovernanceEmitterOptions>? configure = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder.UseOpenTelemetryEmission(configure!));

        Assert.Equal("configure", exception.ParamName);
    }

    /// <summary>
    /// Verifies that invalid configured options are rejected before emitter services are registered.
    /// </summary>
    [Fact]
    public void UseOpenTelemetryEmissionRejectsInvalidConfiguredOptionsBeforeRegistration()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => builder.UseOpenTelemetryEmission(options => options.ProviderName = " "));

        Assert.Equal("OpenTelemetry governance provider name is required.", exception.Message);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(OpenTelemetryGovernanceEmitterOptions)
                || descriptor.ServiceType == typeof(OpenTelemetryGovernanceEmitter)
                || descriptor.ServiceType == typeof(IGovernanceEmitter));
    }

    private static void AssertSingletonRegistrations(IServiceCollection services)
    {
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(OpenTelemetryGovernanceEmitterOptions)
                && descriptor.Lifetime == ServiceLifetime.Singleton
                && descriptor.ImplementationInstance is OpenTelemetryGovernanceEmitterOptions);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(OpenTelemetryGovernanceEmitter)
                && descriptor.ImplementationType == typeof(OpenTelemetryGovernanceEmitter)
                && descriptor.Lifetime == ServiceLifetime.Singleton);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IGovernanceEmitter)
                && descriptor.ImplementationFactory is not null
                && descriptor.Lifetime == ServiceLifetime.Singleton);
    }
}
