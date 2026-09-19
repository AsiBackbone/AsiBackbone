using Microsoft.AspNetCore.Http;

namespace AsiBackbone.AspNetCore.Results;

/// <summary>
/// Provides host-overridable defaults for mapping AsiBackbone Core outcomes into HTTP responses.
/// </summary>
public sealed class GovernanceHttpResultMappingOptions
{
    /// <summary>
    /// Gets or sets the status code returned for allowed governance decisions and successful operation results.
    /// </summary>
    public int SuccessStatusCode { get; set; } = StatusCodes.Status200OK;

    /// <summary>
    /// Gets or sets the status code returned for warning governance decisions.
    /// </summary>
    public int WarningStatusCode { get; set; } = StatusCodes.Status200OK;

    /// <summary>
    /// Gets or sets the status code returned for denied governance decisions.
    /// </summary>
    public int DeniedStatusCode { get; set; } = StatusCodes.Status403Forbidden;

    /// <summary>
    /// Gets or sets the status code returned for deferred governance decisions.
    /// </summary>
    public int DeferredStatusCode { get; set; } = StatusCodes.Status202Accepted;

    /// <summary>
    /// Gets or sets the status code returned for acknowledgment-required governance decisions.
    /// </summary>
    public int AcknowledgmentRequiredStatusCode { get; set; } = StatusCodes.Status428PreconditionRequired;

    /// <summary>
    /// Gets or sets the status code returned for escalation-recommended governance decisions.
    /// </summary>
    public int EscalationRecommendedStatusCode { get; set; } = StatusCodes.Status409Conflict;

    /// <summary>
    /// Gets or sets the status code returned for failed operation results.
    /// </summary>
    public int OperationFailureStatusCode { get; set; } = StatusCodes.Status400BadRequest;

    /// <summary>
    /// Gets or sets the safe user-facing message used for non-success governance decisions.
    /// </summary>
    public string GovernanceDecisionNotAllowedMessage { get; set; } =
        "The governance decision did not allow immediate execution.";

    /// <summary>
    /// Gets or sets the safe user-facing message used for failed operation results.
    /// </summary>
    public string OperationFailureMessage { get; set; } = "The operation did not complete successfully.";

    /// <summary>
    /// Gets or sets a value indicating whether reason messages may be exposed in HTTP responses.
    /// </summary>
    public bool IncludeReasonMessages { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether trace identifiers may be exposed in HTTP responses.
    /// </summary>
    public bool IncludeTraceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether policy version and policy hash values may be exposed in HTTP responses.
    /// </summary>
    public bool IncludePolicyMetadata { get; set; }

    /// <summary>
    /// Validates the configured mapping options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a configured status code or message is invalid.</exception>
    public void Validate()
    {
        ValidateStatusCode(SuccessStatusCode, nameof(SuccessStatusCode));
        ValidateStatusCode(WarningStatusCode, nameof(WarningStatusCode));
        ValidateStatusCode(DeniedStatusCode, nameof(DeniedStatusCode));
        ValidateStatusCode(DeferredStatusCode, nameof(DeferredStatusCode));
        ValidateStatusCode(AcknowledgmentRequiredStatusCode, nameof(AcknowledgmentRequiredStatusCode));
        ValidateStatusCode(EscalationRecommendedStatusCode, nameof(EscalationRecommendedStatusCode));
        ValidateStatusCode(OperationFailureStatusCode, nameof(OperationFailureStatusCode));

        if (string.IsNullOrWhiteSpace(GovernanceDecisionNotAllowedMessage))
        {
            throw new InvalidOperationException("A safe governance decision response message must be configured.");
        }

        if (string.IsNullOrWhiteSpace(OperationFailureMessage))
        {
            throw new InvalidOperationException("A safe operation failure response message must be configured.");
        }
    }

    private static void ValidateStatusCode(int statusCode, string propertyName)
    {
        if (statusCode is < 100 or > 599)
        {
            throw new InvalidOperationException($"{propertyName} must be a valid HTTP status code.");
        }
    }
}
