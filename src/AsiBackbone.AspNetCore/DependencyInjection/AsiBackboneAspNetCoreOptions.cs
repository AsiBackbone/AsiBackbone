namespace AsiBackbone.AspNetCore.DependencyInjection;

/// <summary>
/// Provides configuration options for ASP.NET Core host integration.
/// </summary>
public sealed class AsiBackboneAspNetCoreOptions
{
    /// <summary>
    /// Gets the default header names checked for correlation identifier propagation.
    /// </summary>
    public static IReadOnlyList<string> DefaultCorrelationIdHeaderNames { get; } =
    [
        "X-Correlation-ID",
        "X-Request-ID",
        "Traceparent",
    ];

    /// <summary>
    /// Gets or sets a value indicating whether HTTP route values may be included by request-context adapters.
    /// </summary>
    public bool IncludeRouteValues { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether endpoint metadata may be inspected by request-context adapters.
    /// </summary>
    public bool IncludeEndpointMetadata
    {
        get => IncludeEndpointDisplayName || IncludeRoutePattern;
        set
        {
            IncludeEndpointDisplayName = value;
            IncludeRoutePattern = value;
        }
    }

    /// <summary>
    /// Gets or sets a value indicating whether the HTTP method may be included as safe audit metadata.
    /// </summary>
    public bool IncludeRequestMethod { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the HTTP path may be included as safe audit metadata.
    /// </summary>
    public bool IncludeRequestPath { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether endpoint display names may be included as safe audit metadata.
    /// </summary>
    public bool IncludeEndpointDisplayName { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether endpoint route patterns may be included as safe audit metadata.
    /// </summary>
    public bool IncludeRoutePattern { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the ASP.NET Core trace identifier is used as the correlation identifier
    /// when no configured correlation header is present.
    /// </summary>
    public bool UseHttpContextTraceIdentifierAsCorrelationId { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether correlation identifiers supplied through configured HTTP headers are
    /// trusted. The default is <see langword="false" />.
    /// </summary>
    /// <remarks>
    /// Enable this only when a trusted ingress removes caller-supplied correlation headers and writes its own values.
    /// Enabling it for requests received directly from untrusted callers lets those callers choose audit correlation
    /// identifiers.
    /// </remarks>
    public bool TrustInboundCorrelationIdHeaders { get; set; }

    /// <summary>
    /// Gets or sets the primary HTTP header name preferred for correlation identifier propagation.
    /// </summary>
    public string CorrelationIdHeaderName
    {
        get => CorrelationIdHeaderNames.FirstOrDefault() ?? string.Empty;
        set => CorrelationIdHeaderNames = [value];
    }

    /// <summary>
    /// Gets or sets the HTTP header names checked for correlation identifier propagation.
    /// </summary>
    public IList<string> CorrelationIdHeaderNames { get; set; } = [.. DefaultCorrelationIdHeaderNames];

    /// <summary>
    /// Validates the options.
    /// </summary>
    public void Validate()
    {
        if (CorrelationIdHeaderNames is null || CorrelationIdHeaderNames.Count == 0 || CorrelationIdHeaderNames.All(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("At least one correlation identifier header name must be configured.");
        }
    }
}
