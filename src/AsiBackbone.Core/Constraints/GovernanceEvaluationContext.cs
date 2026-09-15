using System.Collections.ObjectModel;

namespace AsiBackbone.Core.Constraints;

/// <summary>
/// Default framework-neutral governance evaluation context.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="GovernanceEvaluationContext" /> class.
/// </remarks>
/// <param name="correlationId">Optional correlation identifier.</param>
/// <param name="policyVersion">Optional policy version.</param>
/// <param name="policyHash">Optional policy hash.</param>
/// <param name="metadata">Optional host-provided metadata.</param>
public sealed class GovernanceEvaluationContext(
    string? correlationId = null,
    string? policyVersion = null,
    string? policyHash = null,
    IReadOnlyDictionary<string, string>? metadata = null) : IGovernanceEvaluationContext
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    /// <inheritdoc />
    public string? CorrelationId { get; } = NormalizeOptional(correlationId);

    /// <inheritdoc />
    public string? PolicyVersion { get; } = NormalizeOptional(policyVersion);

    /// <inheritdoc />
    public string? PolicyHash { get; } = NormalizeOptional(policyHash);

    /// <inheritdoc />
    public IReadOnlyDictionary<string, string> Metadata { get; } = NormalizeMetadata(metadata);

    /// <summary>
    /// Gets a value indicating whether this context contains metadata.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

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
