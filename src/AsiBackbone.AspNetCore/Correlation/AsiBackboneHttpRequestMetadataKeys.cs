namespace AsiBackbone.AspNetCore.Correlation;

/// <summary>
/// Defines metadata keys emitted by ASP.NET Core request correlation adapters.
/// </summary>
public static class AsiBackboneHttpRequestMetadataKeys
{
    /// <summary>
    /// Metadata key for the server-owned ASP.NET Core HTTP context trace identifier.
    /// </summary>
    public const string TraceIdentifier = "http.trace_identifier";

    /// <summary>
    /// Metadata key for the safe HTTP method value.
    /// </summary>
    public const string Method = "http.method";

    /// <summary>
    /// Metadata key for the safe HTTP request path value.
    /// </summary>
    public const string Path = "http.path";

    /// <summary>
    /// Metadata key for the safe endpoint display name.
    /// </summary>
    public const string EndpointDisplayName = "http.endpoint.display_name";

    /// <summary>
    /// Metadata key for the safe endpoint route pattern.
    /// </summary>
    public const string RoutePattern = "http.route_pattern";

    /// <summary>
    /// Metadata key prefix for route values.
    /// </summary>
    public const string RouteValuePrefix = "http.route.value.";
}
