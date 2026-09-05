using AsiBackbone.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Tests the pipeline-ordering behavior of endpoint governance: what happens when the middleware runs before endpoint
/// routing has selected an endpoint, and the opt-in startup check for that ordering.
/// </summary>
public sealed class AsiBackboneEndpointGovernanceOrderingTests
{
    /// <summary>
    /// Verifies that a request with no selected endpoint is forwarded under default options.
    /// </summary>
    /// <remarks>
    /// This is the permissive default the ordering requirement exists to guard. A null endpoint is also normal for a
    /// request that simply matched no route, so forwarding is correct here; it is only dangerous when it is caused by
    /// the middleware running before routing, where it makes every request look ungoverned.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task NullEndpointIsForwardedUnderDefaultOptions()
    {
        bool nextCalled = false;
        AsiBackboneEndpointGovernanceMiddleware middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            });
        HttpContext httpContext = CreateHttpContext();

        await middleware.InvokeAsync(httpContext, new AllowingGovernanceService());

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Verifies that a request with no selected endpoint fails closed when governance metadata is required.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task NullEndpointFailsClosedWhenGovernanceMetadataIsRequired()
    {
        bool nextCalled = false;
        AsiBackboneEndpointGovernanceMiddleware middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new AsiBackboneEndpointGovernanceOptions { RequireGovernanceMetadata = true });
        HttpContext httpContext = CreateHttpContext();

        await middleware.InvokeAsync(httpContext, new AllowingGovernanceService());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Verifies that a null endpoint is reported under its own decision stage rather than as a missing-metadata gap.
    /// </summary>
    /// <remarks>
    /// A null endpoint and an endpoint that declares no governance metadata both fail closed, but they have different
    /// causes and different fixes. Reporting both under the metadata stage hides a pipeline-ordering fault.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task NullEndpointReportsItsOwnDecisionStage()
    {
        AsiBackboneEndpointGovernanceMiddleware middleware = CreateMiddleware(
            static _ => Task.CompletedTask,
            new AsiBackboneEndpointGovernanceOptions
            {
                RequireGovernanceMetadata = true,
                EnableDevelopmentDiagnostics = true
            });
        HttpContext httpContext = CreateHttpContext();

        await middleware.InvokeAsync(httpContext, new AllowingGovernanceService());

        string body = await ReadResponseBodyAsync(httpContext);
        Assert.Contains("unresolved_endpoint", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that an endpoint declaring no governance metadata is still reported under the metadata stage.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task EndpointWithoutGovernanceMetadataStillReportsTheMetadataStage()
    {
        AsiBackboneEndpointGovernanceMiddleware middleware = CreateMiddleware(
            static _ => Task.CompletedTask,
            new AsiBackboneEndpointGovernanceOptions
            {
                RequireGovernanceMetadata = true,
                EnableDevelopmentDiagnostics = true
            });
        HttpContext httpContext = CreateHttpContext();
        httpContext.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(),
            "ungoverned-endpoint"));

        await middleware.InvokeAsync(httpContext, new AllowingGovernanceService());

        string body = await ReadResponseBodyAsync(httpContext);
        Assert.DoesNotContain("unresolved_endpoint", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the opt-in ordering check throws when endpoint routing has not been registered.
    /// </summary>
    [Fact]
    public void StrictOrderingCheckThrowsWhenRoutingIsNotRegistered()
    {
        ApplicationBuilder app = CreateApplicationBuilder();

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => app.UseAsiBackboneEndpointGovernance(requireEndpointRoutingRegistered: true));

        Assert.Contains("UseRouting", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the opt-in ordering check passes once endpoint routing has been registered.
    /// </summary>
    [Fact]
    public void StrictOrderingCheckPassesAfterUseRouting()
    {
        ApplicationBuilder app = CreateApplicationBuilder();
        _ = app.UseRouting();

        IApplicationBuilder returned = app.UseAsiBackboneEndpointGovernance(requireEndpointRoutingRegistered: true);

        Assert.Same(app, returned);
    }

    /// <summary>
    /// Verifies that the ordering check is off by default, because a Minimal API host that never calls UseRouting
    /// still routes correctly and would otherwise fail startup.
    /// </summary>
    [Fact]
    public void OrderingCheckIsOffByDefault()
    {
        ApplicationBuilder app = CreateApplicationBuilder();

        IApplicationBuilder returned = app.UseAsiBackboneEndpointGovernance();

        Assert.Same(app, returned);
    }

    private static ApplicationBuilder CreateApplicationBuilder()
    {
        ServiceCollection services = new();
        _ = services.AddRouting();
        _ = services.AddLogging();

        return new ApplicationBuilder(services.BuildServiceProvider());
    }

    private static AsiBackboneEndpointGovernanceMiddleware CreateMiddleware(
        RequestDelegate next,
        AsiBackboneEndpointGovernanceOptions? options = null)
    {
        return new AsiBackboneEndpointGovernanceMiddleware(
            next,
            Options.Create(options ?? new AsiBackboneEndpointGovernanceOptions()));
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        ServiceCollection services = new();
        _ = services.AddLogging();
        _ = services.AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment());

        return new DefaultHttpContext
        {
            RequestServices = services.BuildServiceProvider(),
            Response = { Body = new MemoryStream() }
        };
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Development";

        public string ApplicationName { get; set; } = "AsiBackbone.AspNetCore.Tests";

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);

        return await reader.ReadToEndAsync();
    }

    private sealed class AllowingGovernanceService : IAsiBackboneEndpointGovernanceService
    {
        public ValueTask<AsiBackboneEndpointGovernanceResult> EvaluateAsync(
            HttpContext httpContext,
            AsiBackboneEndpointGovernanceDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException(
                "Governance evaluation must not run for a request with no governance metadata.");
        }
    }
}
