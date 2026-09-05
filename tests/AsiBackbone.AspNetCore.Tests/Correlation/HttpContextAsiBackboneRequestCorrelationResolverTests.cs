using System.Diagnostics;
using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Correlation;

/// <summary>
/// Unit tests for <see cref="HttpContextAsiBackboneRequestCorrelationResolver"/> class.
/// </summary>
public sealed class HttpContextAsiBackboneRequestCorrelationResolverTests
{
    /// <summary>
    /// Tests that <see cref="HttpContextAsiBackboneRequestCorrelationResolver.ResolveRequestCorrelation"/> ignores inbound correlation identifiers by default.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationIgnoresConfiguredHeaderByDefault()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-123",
        };
        httpContext.Request.Headers["X-Correlation-ID"] = "  correlation-456  ";

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(httpContext);

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.Equal("trace-123", correlation.CorrelationId);
        Assert.Equal("trace-123", correlation.TraceId);
        Assert.Equal("trace-123", correlation.Metadata[AsiBackboneHttpRequestMetadataKeys.TraceIdentifier]);
    }

    /// <summary>
    /// Verifies that a valid client correlation identifier at the shared maximum length is preserved.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationPreservesMaximumLengthHeader()
    {
        string maximumLengthValue = new('a', AsiBackboneIdentifierLimits.MaximumLength);
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-maximum",
        };
        httpContext.Request.Headers["X-Correlation-ID"] = maximumLengthValue;

        AsiBackboneHttpRequestCorrelation correlation = CreateResolver(
            httpContext,
            options => options.TrustInboundCorrelationIdHeaders = true).ResolveRequestCorrelation();

        Assert.Equal(maximumLengthValue, correlation.CorrelationId);
        Assert.Equal("trace-maximum", correlation.Metadata[AsiBackboneHttpRequestMetadataKeys.TraceIdentifier]);
    }

    /// <summary>
    /// Verifies that an oversized client correlation identifier is ignored before it can reach governance persistence.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationIgnoresOverlengthHeaderAndUsesFallback()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-overlength",
        };
        httpContext.Request.Headers["X-Correlation-ID"] = new string(
            'a',
            AsiBackboneIdentifierLimits.MaximumLength + 1);

        AsiBackboneHttpRequestCorrelation correlation = CreateResolver(
            httpContext,
            options => options.TrustInboundCorrelationIdHeaders = true).ResolveRequestCorrelation();

        Assert.Equal("trace-overlength", correlation.CorrelationId);
    }

    /// <summary>
    /// Verifies that control characters cause the client value to be ignored rather than entering logs or governance records.
    /// </summary>
    /// <param name="controlCharacter">The control character embedded in the client value.</param>
    [Theory]
    [InlineData('\r')]
    [InlineData('\n')]
    [InlineData('\t')]
    [InlineData('\0')]
    [InlineData('\u001F')]
    [InlineData('\u007F')]
    public void ResolveRequestCorrelationIgnoresHeaderContainingControlCharacter(char controlCharacter)
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-control",
        };
        httpContext.Request.Headers["X-Correlation-ID"] = $"correlation{controlCharacter}forged";

        AsiBackboneHttpRequestCorrelation correlation = CreateResolver(
            httpContext,
            options => options.TrustInboundCorrelationIdHeaders = true).ResolveRequestCorrelation();

        Assert.Equal("trace-control", correlation.CorrelationId);
        Assert.DoesNotContain(controlCharacter, correlation.CorrelationId!);
    }

    /// <summary>
    /// Verifies that invalid values are skipped when a later value from the configured header is valid.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationUsesLaterValidHeaderValue()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-multiple",
        };
        httpContext.Request.Headers["X-Correlation-ID"] = new StringValues(
        [
            "invalid\nvalue",
            "  valid-correlation  ",
        ]);

        AsiBackboneHttpRequestCorrelation correlation = CreateResolver(
            httpContext,
            options => options.TrustInboundCorrelationIdHeaders = true).ResolveRequestCorrelation();

        Assert.Equal("valid-correlation", correlation.CorrelationId);
    }

    /// <summary>
    /// Verifies that whitespace-only client values use the existing trace-identifier fallback.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationIgnoresWhitespaceOnlyHeader()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-whitespace",
        };
        httpContext.Request.Headers["X-Correlation-ID"] = "   ";

        AsiBackboneHttpRequestCorrelation correlation = CreateResolver(
            httpContext,
            options => options.TrustInboundCorrelationIdHeaders = true).ResolveRequestCorrelation();

        Assert.Equal("trace-whitespace", correlation.CorrelationId);
    }

    /// <summary>
    /// Tests that <see cref="HttpContextAsiBackboneRequestCorrelationResolver.ResolveRequestCorrelation"/> falls back to the trace identifier.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationFallsBackToTraceIdentifierByDefault()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-789",
        };

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(httpContext);

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.Equal("trace-789", correlation.CorrelationId);
        Assert.Equal("trace-789", correlation.TraceId);
    }

    /// <summary>
    /// Tests that <see cref="HttpContextAsiBackboneRequestCorrelationResolver.ResolveRequestCorrelation"/> supports custom configured header names for correlation ID.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationSupportsConfiguredHeaderNames()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-abc",
        };
        httpContext.Request.Headers["X-Tenant-Correlation"] = "tenant-correlation";

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(
            httpContext,
            options =>
            {
                options.TrustInboundCorrelationIdHeaders = true;
                options.CorrelationIdHeaderNames = ["X-Tenant-Correlation"];
            });

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.Equal("tenant-correlation", correlation.CorrelationId);
    }

    /// <summary>
    /// Tests that <see cref="HttpContextAsiBackboneRequestCorrelationResolver.ResolveRequestCorrelation"/> adds safe request metadata such as HTTP method, route pattern, endpoint display name, and route values, while excluding sensitive data like query parameters and headers.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationAddsSafeRequestMetadata()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-route",
        };
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.Path = "/api/widgets/42";
        httpContext.Request.RouteValues["id"] = "42";
        httpContext.SetEndpoint(CreateRouteEndpoint("/api/widgets/{id}", "Widget endpoint"));

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(httpContext);

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.Equal(HttpMethods.Post, correlation.Metadata[AsiBackboneHttpRequestMetadataKeys.Method]);
        Assert.Equal("trace-route", correlation.Metadata[AsiBackboneHttpRequestMetadataKeys.TraceIdentifier]);
        Assert.Equal("/api/widgets/{id}", correlation.Metadata[AsiBackboneHttpRequestMetadataKeys.RoutePattern]);
        Assert.Equal("Widget endpoint", correlation.Metadata[AsiBackboneHttpRequestMetadataKeys.EndpointDisplayName]);
        Assert.Equal("42", correlation.Metadata[$"{AsiBackboneHttpRequestMetadataKeys.RouteValuePrefix}id"]);
        Assert.False(correlation.Metadata.ContainsKey(AsiBackboneHttpRequestMetadataKeys.Path));
    }

    /// <summary>
    /// Tests that <see cref="HttpContextAsiBackboneRequestCorrelationResolver.ResolveRequestCorrelation"/> includes the request path in the metadata when configured to do so.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationCanIncludeRequestPathWhenConfigured()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-path",
        };
        httpContext.Request.Path = "/api/widgets/42";

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(
            httpContext,
            options => options.IncludeRequestPath = true);

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.Equal("/api/widgets/42", correlation.Metadata[AsiBackboneHttpRequestMetadataKeys.Path]);
    }

    /// <summary>
    /// Tests that <see cref="HttpContextAsiBackboneRequestCorrelationResolver.ResolveRequestCorrelation"/> excludes sensitive request data such as query parameters and headers by default.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationExcludesSensitiveRequestDataByDefault()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "trace-sensitive",
        };
        httpContext.Request.Method = HttpMethods.Get;
        httpContext.Request.Path = "/api/widgets";
        httpContext.Request.QueryString = new QueryString("?access_token=secret-token");
        httpContext.Request.Headers.Authorization = "Bearer secret-token";
        httpContext.Request.Headers.Cookie = "session=secret-cookie";
        httpContext.Request.Headers["X-Api-Key"] = "secret-key";

        HttpContextAsiBackboneRequestCorrelationResolver resolver = CreateResolver(httpContext);

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.DoesNotContain(correlation.Metadata.Keys, key => key.Contains("header", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(correlation.Metadata.Keys, key => key.Contains("query", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(correlation.Metadata.Values, value => value.Contains("secret", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(correlation.Metadata.Values, value => value.Contains("Bearer", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Tests that <see cref="HttpContextAsiBackboneRequestCorrelationResolver.ResolveRequestCorrelation"/> returns only the trace identifier.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationReturnsTraceOnlyForBackgroundScenario()
    {
        HttpContextAccessor httpContextAccessor = new();
        HttpContextAsiBackboneRequestCorrelationResolver resolver = new(
            httpContextAccessor,
            Options.Create(new AsiBackboneAspNetCoreOptions()));

        AsiBackboneHttpRequestCorrelation correlation = resolver.ResolveRequestCorrelation();

        Assert.Null(correlation.CorrelationId);
        Assert.Empty(correlation.Metadata);
    }

    /// <summary>
    /// Verifies that the server-owned trace identifier remains available when an Activity supplies a different trace ID.
    /// </summary>
    [Fact]
    public void ResolveRequestCorrelationRecordsServerTraceIdentifierAlongsideActivityTrace()
    {
        DefaultHttpContext httpContext = new()
        {
            TraceIdentifier = "server-trace-123",
        };
        using Activity activity = new("request");
        _ = activity.Start();

        AsiBackboneHttpRequestCorrelation correlation = CreateResolver(httpContext).ResolveRequestCorrelation();

        Assert.Equal(activity.Id, correlation.TraceId);
        Assert.Equal("server-trace-123", correlation.CorrelationId);
        Assert.Equal("server-trace-123", correlation.Metadata[AsiBackboneHttpRequestMetadataKeys.TraceIdentifier]);
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
        static Task requestDelegate(HttpContext _)
        {
            return Task.CompletedTask;
        }

        Endpoint endpoint = new RouteEndpointBuilder(requestDelegate, RoutePatternFactory.Parse(pattern), order: 0)
        {
            DisplayName = displayName,
        }.Build();

        return (RouteEndpoint)endpoint;
    }
}
