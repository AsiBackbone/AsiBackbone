using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Unit tests for the <see cref="EndpointGovernanceApplicationBuilderExtensions" /> class.
/// </summary>
public sealed class AsiBackboneEndpointGovernanceApplicationBuilderExtensionsTests
{
    /// <summary>
    /// Verifies that endpoint-governance middleware is added to the pipeline and the same application builder is returned.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task UseAsiBackboneEndpointGovernanceAddsMiddlewareAndReturnsSameBuilder()
    {
        ServiceCollection services = new();
        _ = services.AddAsiBackboneAspNetCore();
        using ServiceProvider provider = services.BuildServiceProvider();
        var app = new ApplicationBuilder(provider);
        bool terminalCalled = false;

        IApplicationBuilder result = app.UseAsiBackboneEndpointGovernance();
        app.Run(context =>
        {
            terminalCalled = true;
            context.Response.StatusCode = StatusCodes.Status204NoContent;
            return Task.CompletedTask;
        });

        RequestDelegate pipeline = app.Build();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = provider
        };

        await pipeline(httpContext);

        Assert.Same(app, result);
        Assert.True(terminalCalled);
        Assert.Equal(StatusCodes.Status204NoContent, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Verifies that endpoint-governance middleware registration rejects a null application builder.
    /// </summary>
    [Fact]
    public void UseAsiBackboneEndpointGovernanceRejectsNullApplicationBuilder()
    {
        IApplicationBuilder? app = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => app!.UseAsiBackboneEndpointGovernance());

        Assert.Equal("app", exception.ParamName);
    }
}
