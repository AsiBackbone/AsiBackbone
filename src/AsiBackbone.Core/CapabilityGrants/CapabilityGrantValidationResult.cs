using System.Collections.ObjectModel;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Core.CapabilityGrants;

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

    public bool IsValid { get; }

    public CapabilityGrantValidationCategory Category { get; }

    public VerificationPolicyAction Action { get; }

    public bool ShouldAllow => Action is VerificationPolicyAction.Allow;

    public string Status { get; }

    public string? FailureCode { get; }

    public string? FailureMessage { get; }

    public string TokenId { get; }

    public IReadOnlyDictionary<string, string> SafeMetadata { get; }

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
