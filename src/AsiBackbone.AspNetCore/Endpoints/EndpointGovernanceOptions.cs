using Microsoft.AspNetCore.Http;

namespace AsiBackbone.AspNetCore.Endpoints;

/// <summary>
/// Provides options for ergonomic ASP.NET Core endpoint governance integration.
/// </summary>
public sealed class EndpointGovernanceOptions
{
    /// <summary>
    /// Gets or sets the policy version attached to generated endpoint governance evaluation contexts.
    /// </summary>
    public string? PolicyVersion { get; set; }

    /// <summary>
    /// Gets or sets the policy hash attached to generated endpoint governance evaluation contexts.
    /// </summary>
    public string? PolicyHash { get; set; }

    /// <summary>
    /// Gets or sets the endpoint metadata mode used for governance evaluation contexts, decision receipt, acknowledgment challenges, and diagnostics.
    /// </summary>
    /// <remarks>
    /// The default keeps complete endpoint metadata for traceability. High-throughput hosts may choose
    /// <see cref="EndpointGovernanceMetadataMode.Reduced" /> to forward only the endpoint operation name
    /// through the hot path after they have confirmed host policies do not require the omitted metadata values.
    /// </remarks>
    public EndpointGovernanceMetadataMode MetadataMode { get; set; } = EndpointGovernanceMetadataMode.Full;

    /// <summary>
    /// Gets or sets a value indicating whether policy metadata should fail closed when no policy evaluator is configured.
    /// </summary>
    public bool FailClosedWhenPolicyEvaluatorMissing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether capability metadata should fail closed when no capability validator is configured.
    /// </summary>
    public bool FailClosedWhenCapabilityValidatorMissing { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether audit metadata should fail closed when no host-owned audit sink is configured.
    /// </summary>
    public bool FailClosedWhenAuditSinkMissing { get; set; } = true;

    /// <summary>
    /// Gets or sets the HTTP status code used when endpoint governance is missing required host configuration.
    /// </summary>
    public int ConfigurationFailureStatusCode { get; set; } = StatusCodes.Status500InternalServerError;

    /// <summary>
    /// Gets or sets the HTTP status code used when endpoint capability validation fails before policy evaluation returns a stricter status.
    /// </summary>
    public int CapabilityFailureStatusCode { get; set; } = StatusCodes.Status403Forbidden;

    /// <summary>
    /// Gets or sets the HTTP status code used when a governance decision requires acknowledgment and the endpoint requested a liability handshake.
    /// </summary>
    public int AcknowledgmentChallengeStatusCode { get; set; } = StatusCodes.Status428PreconditionRequired;

    /// <summary>
    /// Gets or sets a value indicating whether selected endpoints without AsiBackbone governance metadata should fail closed.
    /// </summary>
    public bool RequireGovernanceMetadata { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether local-development ProblemDetails diagnostics should be emitted for endpoint governance failures.
    /// </summary>
    public bool EnableDevelopmentDiagnostics { get; set; }

    /// <summary>
    /// Gets or sets the documentation base URL used when development diagnostics include a troubleshooting link.
    /// </summary>
    public string? DevelopmentDiagnosticsDocumentationBaseUrl { get; set; }

    /// <summary>
    /// Gets a collection of metadata keys whose values should be redacted from development diagnostics.
    /// </summary>
    public ISet<string> DevelopmentDiagnosticsRedactedMetadataKeys { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Gets or sets a value indicating whether non-sensitive metadata values may be included in development diagnostics.
    /// </summary>
    public bool IncludeDevelopmentDiagnosticsMetadataValues { get; set; } = true;

    /// <summary>
    /// Gets or sets an optional factory for the generic 403 response used by middleware when no explicit failure result was supplied.
    /// </summary>
    /// <remarks>
    /// Leave this unset for the low-allocation, bodyless default 403 response. Hosts that prefer richer API responses
    /// may provide a safe factory, such as a ProblemDetails result, while avoiding sensitive governance details.
    /// </remarks>
    public Func<HttpContext, IResult>? DefaultForbiddenResultFactory { get; set; }

    /// <summary>
    /// Validates endpoint governance options.
    /// </summary>
    public void Validate()
    {
        if (!Enum.IsDefined(MetadataMode))
        {
            throw new InvalidOperationException($"{nameof(MetadataMode)} must be a defined endpoint governance metadata mode.");
        }

        ValidateStatusCode(ConfigurationFailureStatusCode, nameof(ConfigurationFailureStatusCode));
        ValidateStatusCode(CapabilityFailureStatusCode, nameof(CapabilityFailureStatusCode));
        ValidateStatusCode(AcknowledgmentChallengeStatusCode, nameof(AcknowledgmentChallengeStatusCode));
    }

    private static void ValidateStatusCode(int statusCode, string propertyName)
    {
        if (statusCode is < 100 or > 599)
        {
            throw new InvalidOperationException($"{propertyName} must be a valid HTTP status code.");
        }
    }
}
