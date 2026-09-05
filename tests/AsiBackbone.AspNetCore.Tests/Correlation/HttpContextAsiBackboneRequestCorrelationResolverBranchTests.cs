using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.AspNetCore.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Correlation;

/// <summary>
/// Unit tests for the <see cref="HttpContextAsiBackboneRequestCorrelationResolver"/> class, focusing on branch coverage and edge cases.
/// </summary>
public sealed class HttpContextAsiBackboneRequestCorrelationResolverBranchTests
{
    /// <summary>
    /// Tests that the resolver correctly skips blank header names and blank header values when resolving the correlation ID.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationSkipsBlankHeaderNamesAndBlankHeaderValues()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-blank-header",
        };
        httpContext.Request.Headers["X-Blank"] = "   ";
        httpContext.Request.Headers["X-Valid"] = " correlation-valid ";

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(
            httpContext,
            options =>
            {
                options.TrustInboundCorrelationIdHeaders = true;
                options.CorrelationIdHeaderNames = [" ", "X-Blank", "X-Valid"];
            });

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.Equal("correlation-valid", correlation.CorrelationId);
    }

    /// <summary>
    /// Tests that the resolver returns a null correlation ID when the trace identifier
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationReturnsNullCorrelationIdWhenTraceFallbackDisabled()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-no-fallback",
        };

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(
            httpContext,
            options => options.UseHttpContextTraceIdentifierAsCorrelationId = false);

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.Null(correlation.CorrelationId);
        Assert.Equal("trace-no-fallback", correlation.TraceId);
    }

    /// <summary>
    /// Tests that the resolver omits the request method from metadata when it is blank or whitespace.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationOmitsRequestMethodWhenBlank()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-no-method",
        };
        httpContext.Request.Method = " ";

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(httpContext);

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.False(correlation.Metadata.ContainsKey(AsiBackboneHttpRequestMetadataKeys.Method));
    }

    /// <summary>
    /// Tests that the resolver omits route metadata when the endpoint is not a route endpoint.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationOmitsRouteMetadataWhenEndpointIsNotRouteEndpoint()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-non-route-endpoint",
        };
        httpContext.SetEndpoint(new Endpoint(_ => Task.CompletedTask, EndpointMetadataCollection.Empty, "Plain endpoint"));

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(httpContext);

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.Equal("Plain endpoint", correlation.Metadata[AsiBackboneHttpRequestMetadataKeys.EndpointDisplayName]);
        Assert.False(correlation.Metadata.ContainsKey(AsiBackboneHttpRequestMetadataKeys.RoutePattern));
    }

    /// <summary>
    /// Tests that the resolver omits the endpoint display name from metadata when it is blank or whitespace.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationOmitsEndpointDisplayNameWhenBlank()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-blank-endpoint-name",
        };
        httpContext.SetEndpoint(CreateRouteEndpoint("/items/{id}", " "));

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(httpContext);

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.False(correlation.Metadata.ContainsKey(AsiBackboneHttpRequestMetadataKeys.EndpointDisplayName));
        Assert.Equal("/items/{id}", correlation.Metadata[AsiBackboneHttpRequestMetadataKeys.RoutePattern]);
    }

    /// <summary>
    /// Tests that the resolver skips null and blank route values when resolving request correlation metadata.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationSkipsNullAndBlankRouteValues()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-route-values",
        };
        httpContext.Request.RouteValues["id"] = "42";
        httpContext.Request.RouteValues["optional"] = null;
        httpContext.Request.RouteValues[" "] = "blank-key";

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(httpContext);

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.Equal("42", correlation.Metadata[$"{AsiBackboneHttpRequestMetadataKeys.RouteValuePrefix}id"]);
        Assert.DoesNotContain(correlation.Metadata.Keys, key => key.Contains("optional", StringComparison.Ordinal));
        Assert.DoesNotContain(correlation.Metadata.Values, value => value == "blank-key");
    }

    /// <summary>
    /// Tests that the resolver respects the metadata exclusion options and does not include request method, endpoint metadata, or route values when they are disabled in the options.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationHonorsMetadataExclusionOptions()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-exclusions",
        };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/items/42";
        httpContext.Request.RouteValues["id"] = "42";
        httpContext.SetEndpoint(CreateRouteEndpoint("/items/{id}", "Items endpoint"));

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(
            httpContext,
            options =>
            {
                options.IncludeRequestMethod = false;
                options.IncludeEndpointMetadata = false;
                options.IncludeRouteValues = false;
            });

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        _ = Assert.Single(correlation.Metadata);
        Assert.Equal("trace-exclusions", correlation.Metadata[AsiBackboneHttpRequestMetadataKeys.TraceIdentifier]);
    }

    private static HttpContextAsiBackboneRequestCorrelationResolver CreateResolver(
        HttpContext httpContext,
        Action<AsiBackboneAspNetCoreOptions>? configure = null)
    {
        AsiBackboneAspNetCoreOptions options = new();
        configure?.Invoke(options);

        return new HttpContextAsiBackboneRequestCorrelationResolver(
            new HttpContextAccessor { HttpContext = httpContext },
            Options.Create(options));
    }

    private static RouteEndpoint CreateRouteEndpoint(string pattern, string displayName)
    {
        static Task RequestDelegate(HttpContext _)
        {
            return Task.CompletedTask;
        }

        Endpoint endpoint = new RouteEndpointBuilder(RequestDelegate, RoutePatternFactory.Parse(pattern), order: 0)
        {
            DisplayName = displayName,
        }.Build();

        return (RouteEndpoint)endpoint;
    }
}
