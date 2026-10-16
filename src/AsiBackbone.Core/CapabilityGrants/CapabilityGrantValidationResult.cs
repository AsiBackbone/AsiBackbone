using System.Collections.ObjectModel;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.CapabilityGrants;

/// <summary>
/// Represents the outcome of validating a capability grant, including the policy action the host should take.
/// </summary>
/// <remarks>
/// Hosts should gate execution on <see cref="ShouldAllow" /> rather than <see cref="IsValid" /> alone, and act on
/// <see cref="Action" /> for failed results, because some failures defer or require acknowledgment instead of denying.
/// </remarks>
public sealed class CapabilityGrantValidationResult
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(new Dictionary<string, string>(StringComparer.Ordinal));

    private CapabilityGrantValidationResult(
        bool isValid,
        CapabilityGrantValidationCategory category,
        VerificationPolicyAction action,
        string status,
        string? failureCode,
        string? failureMessage,
        string tokenId,
        IReadOnlyDictionary<string, string> safeMetadata)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        ArgumentException.ThrowIfNullOrWhiteSpace(tokenId);

        if (!Enum.IsDefined(category))
        {
            throw new ArgumentOutOfRangeException(nameof(category), category, "Validation category must be defined.");
        }

        if (!Enum.IsDefined(action))
        {
            throw new ArgumentOutOfRangeException(nameof(action), action, "Validation action must be defined.");
        }

        IsValid = isValid;
        Category = category;
        Action = action;
        Status = status.Trim();
        FailureCode = NormalizeOptional(failureCode);
        FailureMessage = NormalizeOptional(failureMessage);
        TokenId = tokenId.Trim();
        SafeMetadata = safeMetadata;
    }

    /// <summary>
    /// Gets a value indicating whether the grant satisfied every configured validation check.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets the validation category that describes why the grant passed or failed.
    /// </summary>
    public CapabilityGrantValidationCategory Category { get; }

    /// <summary>
    /// Gets the policy action the host should take for this result.
    /// </summary>
    public VerificationPolicyAction Action { get; }

    /// <summary>
    /// Gets a value indicating whether the host may proceed with the governed operation.
    /// </summary>
    public bool ShouldAllow => Action is VerificationPolicyAction.Allow;

    /// <summary>
    /// Gets the result status, either <c>Valid</c> or <c>Failed</c>.
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Gets the machine-readable failure code, such as <c>capability.expired</c>, when validation failed.
    /// </summary>
    public string? FailureCode { get; }

    /// <summary>
    /// Gets the human-readable failure message, when one was supplied.
    /// </summary>
    public string? FailureMessage { get; }

    /// <summary>
    /// Gets the token identifier of the validated grant.
    /// </summary>
    public string TokenId { get; }

    /// <summary>
    /// Gets non-secret grant and outcome metadata, such as issuer, audience, policy action, and failure code, that is
    /// safe to log or attach to audit records.
    /// </summary>
    public IReadOnlyDictionary<string, string> SafeMetadata { get; }

    /// <summary>
    /// Creates a successful result that allows the governed operation.
    /// </summary>
    /// <param name="grant">The grant that passed validation.</param>
    /// <returns>A valid result with <see cref="VerificationPolicyAction.Allow" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grant" /> is <see langword="null" />.</exception>
    public static CapabilityGrantValidationResult Valid(CapabilityGrant grant)
    {
        ArgumentNullException.ThrowIfNull(grant);

        return new CapabilityGrantValidationResult(
            true,
            CapabilityGrantValidationCategory.Valid,
            VerificationPolicyAction.Allow,
            "Valid",
            null,
            null,
            grant.TokenId,
            BuildSafeMetadata(grant, CapabilityGrantValidationCategory.Valid, VerificationPolicyAction.Allow, null));
    }

    /// <summary>
    /// Creates a failed result with the category and policy action the host should apply.
    /// </summary>
    /// <param name="grant">The grant that failed validation.</param>
    /// <param name="category">The validation category that describes the failure.</param>
    /// <param name="action">The policy action the host should take, such as deny, defer, or require acknowledgment.</param>
    /// <param name="failureCode">The machine-readable failure code.</param>
    /// <param name="failureMessage">An optional human-readable failure message.</param>
    /// <returns>A failed result.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="grant" /> or <paramref name="failureCode" /> is <see langword="null" />.</exception>
    /// <exception cref="ArgumentException"><paramref name="failureCode" /> is empty or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="category" /> or <paramref name="action" /> is not a defined value.</exception>
    public static CapabilityGrantValidationResult Failed(
        CapabilityGrant grant,
        CapabilityGrantValidationCategory category,
        VerificationPolicyAction action,
        string failureCode,
        string? failureMessage = null)
    {
        ArgumentNullException.ThrowIfNull(grant);
        ArgumentException.ThrowIfNullOrWhiteSpace(failureCode);

        return new CapabilityGrantValidationResult(
            false,
            category,
            action,
            "Failed",
            failureCode,
            failureMessage,
            grant.TokenId,
            BuildSafeMetadata(grant, category, action, failureCode));
    }

    private static IReadOnlyDictionary<string, string> BuildSafeMetadata(
        CapabilityGrant grant,
        CapabilityGrantValidationCategory category,
        VerificationPolicyAction action,
        string? failureCode)
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["audience"] = grant.Audience,
            ["category"] = category.ToString(),
            ["grant_id"] = grant.TokenId,
            ["issuer"] = grant.Issuer,
            ["policy_action"] = action.ToString()
        };

        AddIfPresent(metadata, "acknowledgment_id", grant.AcknowledgmentId);
        AddIfPresent(metadata, "failure_code", failureCode);
        AddIfPresent(metadata, "handshake_id", grant.HandshakeId);
        AddIfPresent(metadata, "policy_hash", grant.PolicyHash);
        AddIfPresent(metadata, "policy_version", grant.PolicyVersion);
        AddIfPresent(metadata, "resource_binding", grant.ResourceBinding);

        return metadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(metadata);
    }

    private static void AddIfPresent(Dictionary<string, string> metadata, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            metadata[key] = value.Trim();
        }
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
