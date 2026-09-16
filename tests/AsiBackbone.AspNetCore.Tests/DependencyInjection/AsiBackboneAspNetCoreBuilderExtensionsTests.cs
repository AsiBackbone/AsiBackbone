using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.AspNetCore.Outbox;
using AsiBackbone.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.DependencyInjection;

/// <summary>
/// Unit tests for the <see cref="AsiBackboneAspNetCoreBuilderExtensions" /> class.
/// </summary>
public sealed class AsiBackboneAspNetCoreBuilderExtensionsTests
{
    /// <summary>
    /// Verifies that the default endpoint-governance builder overload registers ASP.NET Core services and returns the same builder.
    /// </summary>
    [Fact]
    public void UseAspNetCoreEndpointGovernanceRegistersDefaultsAndReturnsSameBuilder()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        IAsiBackboneBuilder result = builder.UseAspNetCoreEndpointGovernance();

        using ServiceProvider provider = services.BuildServiceProvider();
        AspNetCoreGovernanceOptions options = provider
            .GetRequiredService<IOptions<AspNetCoreGovernanceOptions>>()
            .Value;

        Assert.Same(builder, result);
        Assert.True(options.IncludeRouteValues);
        Assert.False(options.IncludeRequestPath);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IEndpointGovernanceService)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    /// <summary>
    /// Verifies that the configured endpoint-governance builder overload applies options and returns the same builder.
    /// </summary>
    [Fact]
    public void UseAspNetCoreEndpointGovernanceAppliesOptionsAndReturnsSameBuilder()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        IAsiBackboneBuilder result = builder.UseAspNetCoreEndpointGovernance(options =>
        {
            options.IncludeRouteValues = false;
            options.IncludeRequestPath = true;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        AspNetCoreGovernanceOptions options = provider
            .GetRequiredService<IOptions<AspNetCoreGovernanceOptions>>()
            .Value;

        Assert.Same(builder, result);
        Assert.False(options.IncludeRouteValues);
        Assert.True(options.IncludeRequestPath);
    }

    /// <summary>
    /// Verifies that endpoint-governance builder registration rejects a null builder.
    /// </summary>
    [Fact]
    public void UseAspNetCoreEndpointGovernanceRejectsNullBuilder()
    {
        IAsiBackboneBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder!.UseAspNetCoreEndpointGovernance());

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Verifies that endpoint-governance builder registration rejects a null configuration callback.
    /// </summary>
    [Fact]
    public void UseAspNetCoreEndpointGovernanceRejectsNullConfigureCallback()
    {
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(new ServiceCollection());
        Action<AspNetCoreGovernanceOptions>? configure = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder.UseAspNetCoreEndpointGovernance(configure!));

        Assert.Equal("configure", exception.ParamName);
    }

    /// <summary>
    /// Verifies that the default outbox-drain builder overload registers the hosted worker and returns the same builder.
    /// </summary>
    [Fact]
    public void UseGovernanceOutboxDrainRegistersDefaultsAndReturnsSameBuilder()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        IAsiBackboneBuilder result = builder.UseGovernanceOutboxDrain();

        using ServiceProvider provider = services.BuildServiceProvider();
        GovernanceOutboxDrainWorkerOptions options = provider
            .GetRequiredService<IOptions<GovernanceOutboxDrainWorkerOptions>>()
            .Value;

        Assert.Same(builder, result);
        Assert.True(options.Enabled);
        Assert.Equal(100, options.BatchSize);
        Assert.Equal(TimeSpan.FromSeconds(30), options.PollingInterval);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService)
                && descriptor.ImplementationType == typeof(GovernanceOutboxDrainHostedService));
    }

    /// <summary>
    /// Verifies that the configured outbox-drain builder overload applies options and returns the same builder.
    /// </summary>
    [Fact]
    public void UseGovernanceOutboxDrainAppliesOptionsAndReturnsSameBuilder()
    {
        ServiceCollection services = new();
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(services);

        IAsiBackboneBuilder result = builder.UseGovernanceOutboxDrain(options =>
        {
            options.Enabled = false;
            options.BatchSize = 7;
            options.PollingInterval = TimeSpan.FromSeconds(5);
            options.DrainOnShutdown = true;
        });

        using ServiceProvider provider = services.BuildServiceProvider();
        GovernanceOutboxDrainWorkerOptions options = provider
            .GetRequiredService<IOptions<GovernanceOutboxDrainWorkerOptions>>()
            .Value;

        Assert.Same(builder, result);
        Assert.False(options.Enabled);
        Assert.Equal(7, options.BatchSize);
        Assert.Equal(TimeSpan.FromSeconds(5), options.PollingInterval);
        Assert.True(options.DrainOnShutdown);
    }

    /// <summary>
    /// Verifies that outbox-drain builder registration rejects a null builder.
    /// </summary>
    [Fact]
    public void UseGovernanceOutboxDrainRejectsNullBuilder()
    {
        IAsiBackboneBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder!.UseGovernanceOutboxDrain());

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Verifies that outbox-drain builder registration rejects a null configuration callback.
    /// </summary>
    [Fact]
    public void UseGovernanceOutboxDrainRejectsNullConfigureCallback()
    {
        IAsiBackboneBuilder builder = new AsiBackboneBuilder(new ServiceCollection());
        Action<GovernanceOutboxDrainWorkerOptions>? configure = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder.UseGovernanceOutboxDrain(configure!));

        Assert.Equal("configure", exception.ParamName);
    }
}
