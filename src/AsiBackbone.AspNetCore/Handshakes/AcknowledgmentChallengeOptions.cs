using AsiBackbone.Core.Handshakes;

namespace AsiBackbone.AspNetCore.Handshakes;

/// <summary>
/// Provides host-overridable defaults for building acknowledgment challenges from Core governance decisions.
/// </summary>
public sealed class AcknowledgmentChallengeOptions
{
    /// <summary>
    /// Gets or sets the default acknowledgment code required before continuing.
    /// </summary>
    public string RequiredAcknowledgmentCode { get; set; } = "ACKNOWLEDGE_RESPONSIBILITY";

    /// <summary>
    /// Gets or sets the default acknowledgment text shown by host applications.
    /// </summary>
    public string RequiredAcknowledgmentText { get; set; } =
        "I understand the responsibility notice and acknowledge that this operation may continue.";

    /// <summary>
    /// Gets or sets the default risk level associated with acknowledgment challenges.
    /// </summary>
    public LiabilityHandshakeRiskLevel RiskLevel { get; set; } = LiabilityHandshakeRiskLevel.Unspecified;

    /// <summary>
    /// Gets or sets the optional host-defined default risk category.
    /// </summary>
    public string? RiskCategory { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the challenge should expose the Core reason message.
    /// </summary>
    public bool IncludeReasonMessage { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether trace identifiers may be exposed in challenge payloads.
    /// </summary>
    public bool IncludeTraceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether policy version and policy hash values may be exposed in challenge payloads.
    /// </summary>
    public bool IncludePolicyMetadata { get; set; }

    /// <summary>
    /// Validates the configured challenge options.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when a required challenge option is invalid.</exception>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RequiredAcknowledgmentCode))
        {
            throw new InvalidOperationException("A required acknowledgment code must be configured.");
        }

        if (string.IsNullOrWhiteSpace(RequiredAcknowledgmentText))
        {
            throw new InvalidOperationException("Required acknowledgment text must be configured.");
        }
    }
}
