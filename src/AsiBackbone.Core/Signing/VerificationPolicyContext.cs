using System.Collections.ObjectModel;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Provides host expectations used while evaluating signature verification policy.
/// </summary>
/// <remarks>
/// The context is provider-neutral. It can carry expected key references, policy identifiers, and request metadata without resolving provider-specific keys in Core.
/// </remarks>
public sealed class VerificationPolicyContext
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    private VerificationPolicyContext(
        string? purpose,
        string? expectedKeyId,
        string? expectedKeyVersion,
        string? expectedPolicyVersion,
        string? expectedPolicyHash,
        string? requiredProvider,
        string? requiredHashAlgorithm,
        IReadOnlyDictionary<string, string> metadata)
    {
        Purpose = NormalizeOptional(purpose);
        ExpectedKeyId = NormalizeOptional(expectedKeyId);
        ExpectedKeyVersion = NormalizeOptional(expectedKeyVersion);
        ExpectedPolicyVersion = NormalizeOptional(expectedPolicyVersion);
        ExpectedPolicyHash = NormalizeOptional(expectedPolicyHash);
        RequiredProvider = NormalizeOptional(requiredProvider);
        RequiredHashAlgorithm = NormalizeOptional(requiredHashAlgorithm);
        Metadata = metadata;
    }

    /// <summary>
    /// Gets a context with no additional host expectations.
    /// </summary>
    public static VerificationPolicyContext Default { get; } = new(null, null, null, null, null, null, null, EmptyMetadata);

    /// <summary>
    /// Gets the host-defined verification purpose.
    /// </summary>
    public string? Purpose { get; }

    /// <summary>
    /// Gets the expected signing key identifier, when required by host policy.
    /// </summary>
    public string? ExpectedKeyId { get; }

    /// <summary>
    /// Gets the expected signing key version, when required by host policy.
    /// </summary>
    public string? ExpectedKeyVersion { get; }

    /// <summary>
    /// Gets the expected policy version, when the signed metadata is expected to carry one.
    /// </summary>
    public string? ExpectedPolicyVersion { get; }

    /// <summary>
    /// Gets the expected policy hash, when the signed metadata is expected to carry one.
    /// </summary>
    public string? ExpectedPolicyHash { get; }

    /// <summary>
    /// Gets the required signing provider descriptor, when required by host policy.
    /// </summary>
    public string? RequiredProvider { get; }

    /// <summary>
    /// Gets the required hash algorithm descriptor, when required by host policy.
    /// </summary>
    public string? RequiredHashAlgorithm { get; }

    /// <summary>
    /// Gets additional provider-neutral verification request metadata.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether additional metadata is present.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Gets a value indicating whether verification may fall back to the pre-6.0 hash-only signature input.
    /// </summary>
    /// <remarks>
    /// Defaults to <see langword="false" />. When enabled, an artifact whose version 1 verification fails as an invalid
    /// signature is verified again against <see cref="GovernanceSignatureInput.CreateLegacy" />. A legacy signature
    /// authenticates the canonical payload hash only, so it cannot satisfy <see cref="ExpectedPolicyVersion" /> or
    /// <see cref="ExpectedPolicyHash" />; such pins deny with <c>signature.policy-context-not-authenticated</c>.
    /// </remarks>
    public bool AllowLegacySignatureInput { get; private init; }

    /// <summary>
    /// Creates a copy of this context that accepts artifacts signed with the pre-6.0 hash-only signature input.
    /// </summary>
    /// <remarks>
    /// Use this only for reviewing or migrating artifacts signed before 6.0. Signing metadata labels on such artifacts,
    /// including the policy version and policy hash, are not covered by the signature.
    /// </remarks>
    /// <returns>A context identical to this one with <see cref="AllowLegacySignatureInput" /> set.</returns>
    public VerificationPolicyContext WithLegacySignatureInputAllowed()
    {
        return new VerificationPolicyContext(
            Purpose,
            ExpectedKeyId,
            ExpectedKeyVersion,
            ExpectedPolicyVersion,
            ExpectedPolicyHash,
            RequiredProvider,
            RequiredHashAlgorithm,
            Metadata)
        {
            AllowLegacySignatureInput = true
        };
    }

    /// <summary>
    /// Creates a provider-neutral verification policy context.
    /// </summary>
    public static VerificationPolicyContext Create(
        string? purpose = null,
        string? expectedKeyId = null,
        string? expectedKeyVersion = null,
        string? expectedPolicyVersion = null,
        string? expectedPolicyHash = null,
        string? requiredProvider = null,
        string? requiredHashAlgorithm = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new VerificationPolicyContext(
            purpose,
            expectedKeyId,
            expectedKeyVersion,
            expectedPolicyVersion,
            expectedPolicyHash,
            requiredProvider,
            requiredHashAlgorithm,
            NormalizeMetadata(metadata));
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static IReadOnlyDictionary<string, string> NormalizeMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return EmptyMetadata;
        }

        Dictionary<string, string> normalizedMetadata = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in metadata)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            normalizedMetadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
        }

        return normalizedMetadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(normalizedMetadata);
    }
}
