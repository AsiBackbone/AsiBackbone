using AsiBackbone.Core.Acknowledgments;

namespace AsiBackbone.AspNetCore.Acknowledgments;

/// <summary>
/// Represents an ASP.NET Core-friendly acknowledgment challenge that a host can render using any UI or transport.
/// </summary>
public sealed class AcknowledgmentChallenge
{
    internal AcknowledgmentChallenge(
        AcknowledgmentRequest handshakeRequest,
        string handshakeId,
        string operationName,
        string reasonCode,
        string? reasonMessage,
        string requiredAcknowledgmentCode,
        string requiredAcknowledgmentText,
        AcknowledgmentRiskLevel riskLevel,
        string? riskCategory,
        string? correlationId,
        string? traceId,
        string? policyVersion,
        string? policyHash,
        IReadOnlyDictionary<string, string> metadata)
    {
        // Defensive validation for internal construction invariants.
        // Public inputs are validated through FromAcknowledgmentRequest and AcknowledgmentRequest.
        ArgumentNullException.ThrowIfNull(handshakeRequest);
        ArgumentException.ThrowIfNullOrWhiteSpace(handshakeId);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationName);
        ArgumentException.ThrowIfNullOrWhiteSpace(reasonCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredAcknowledgmentCode);
        ArgumentException.ThrowIfNullOrWhiteSpace(requiredAcknowledgmentText);

        AcknowledgmentRequest = handshakeRequest;
        HandshakeId = handshakeId.Trim();
        OperationName = operationName.Trim();
        ReasonCode = reasonCode.Trim();
        ReasonMessage = NormalizeOptional(reasonMessage);
        RequiredAcknowledgmentCode = requiredAcknowledgmentCode.Trim();
        RequiredAcknowledgmentText = requiredAcknowledgmentText.Trim();
        RiskLevel = riskLevel;
        RiskCategory = NormalizeOptional(riskCategory);
        CorrelationId = NormalizeOptional(correlationId);
        TraceId = NormalizeOptional(traceId);
        PolicyVersion = NormalizeOptional(policyVersion);
        PolicyHash = NormalizeOptional(policyHash);
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the framework-neutral Core handshake request used for round-tripping acknowledgment responses.
    /// </summary>
    public AcknowledgmentRequest AcknowledgmentRequest { get; }

    /// <summary>
    /// Gets the stable handshake identifier.
    /// </summary>
    public string HandshakeId { get; }

    /// <summary>
    /// Gets the operation name requiring acknowledgment.
    /// </summary>
    public string OperationName { get; }

    /// <summary>
    /// Gets the machine-readable reason code for the challenge.
    /// </summary>
    public string ReasonCode { get; }

    /// <summary>
    /// Gets the optional user-facing reason message for the challenge.
    /// </summary>
    public string? ReasonMessage { get; }

    /// <summary>
    /// Gets the required acknowledgment code.
    /// </summary>
    public string RequiredAcknowledgmentCode { get; }

    /// <summary>
    /// Gets the required acknowledgment text.
    /// </summary>
    public string RequiredAcknowledgmentText { get; }

    /// <summary>
    /// Gets the risk level associated with the challenge.
    /// </summary>
    public AcknowledgmentRiskLevel RiskLevel { get; }

    /// <summary>
    /// Gets the optional host-defined risk category.
    /// </summary>
    public string? RiskCategory { get; }

    /// <summary>
    /// Gets the correlation identifier associated with the challenge.
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// Gets the optional trace identifier associated with the challenge.
    /// </summary>
    public string? TraceId { get; }

    /// <summary>
    /// Gets the optional policy version associated with the challenge.
    /// </summary>
    public string? PolicyVersion { get; }

    /// <summary>
    /// Gets the optional policy hash associated with the challenge.
    /// </summary>
    public string? PolicyHash { get; }

    /// <summary>
    /// Gets additional host-provided challenge metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Creates a host-friendly challenge from a Core handshake request.
    /// </summary>
    /// <param name="request">The Core handshake request.</param>
    /// <param name="options">The challenge options.</param>
    /// <returns>A host-friendly acknowledgment challenge.</returns>
    public static AcknowledgmentChallenge FromAcknowledgmentRequest(
        AcknowledgmentRequest request,
        AcknowledgmentChallengeOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        options ??= new AcknowledgmentChallengeOptions();
        options.Validate();

        return new AcknowledgmentChallenge(
            request,
            request.HandshakeId,
            request.OperationName,
            request.ReasonCode,
            options.IncludeReasonMessage ? request.Message : null,
            request.RequiredAcknowledgmentCode,
            request.RequiredAcknowledgmentText,
            request.RiskLevel,
            request.RiskCategory,
            request.CorrelationId,
            options.IncludeTraceId ? request.TraceId : null,
            options.IncludePolicyMetadata ? request.PolicyVersion : null,
            options.IncludePolicyMetadata ? request.PolicyHash : null,
            request.Metadata);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
