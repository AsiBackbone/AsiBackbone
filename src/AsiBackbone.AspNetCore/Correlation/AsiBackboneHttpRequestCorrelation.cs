using System.Collections.ObjectModel;
using AsiBackbone.Core.Constraints;

namespace AsiBackbone.AspNetCore.Correlation;

/// <summary>
/// Represents framework-neutral request correlation data resolved from the current ASP.NET Core HTTP request.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AsiBackboneHttpRequestCorrelation" /> class.
/// </remarks>
/// <param name="correlationId">The resolved correlation identifier, when available.</param>
/// <param name="traceId">The resolved trace identifier, when available.</param>
/// <param name="metadata">Safe request metadata resolved from the host.</param>
public sealed class AsiBackboneHttpRequestCorrelation(
    string? correlationId = null,
    string? traceId = null,
    IReadOnlyDictionary<string, string>? metadata = null)
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    /// <summary>
    /// Gets the request correlation identifier, when supplied by the host or propagated request headers.
    /// </summary>
    public string? CorrelationId { get; } = NormalizeOptional(correlationId);

    /// <summary>
    /// Gets the request trace identifier, when supplied by the host or current activity.
    /// </summary>
    public string? TraceId { get; } = NormalizeOptional(traceId);

    /// <summary>
    /// Gets safe request metadata supplied by the ASP.NET Core adapter.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; } = NormalizeMetadata(metadata);

    /// <summary>
    /// Gets a value indicating whether safe request metadata is available.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Creates a framework-neutral constraint evaluation context from the resolved request correlation data.
    /// </summary>
    /// <param name="policyVersion">Optional policy version.</param>
    /// <param name="policyHash">Optional policy hash.</param>
    /// <param name="metadata">Optional host-provided metadata to merge with safe request metadata.</param>
    /// <param name="mergeRequestMetadata">
    /// When <see langword="true" /> (the default), <see cref="Metadata" /> is merged underneath
    /// <paramref name="metadata" />. Pass <see langword="false" /> when <paramref name="metadata" /> already contains
    /// request metadata and is authoritative — for example after sanitization, where re-merging would reintroduce
    /// entries the sanitizer removed. <paramref name="metadata" /> is still key-normalized either way.
    /// </param>
    /// <returns>A framework-neutral constraint evaluation context.</returns>
    /// <remarks>
    /// Merging layers <paramref name="metadata" /> over <see cref="Metadata" /> by key, so a caller that rewrites a
    /// request-derived value overrides it, but a caller that <em>removes</em> one does not: with nothing to override
    /// the raw entry, the merge puts it back. Sanitized metadata must therefore be passed with
    /// <paramref name="mergeRequestMetadata" /> set to <see langword="false" />.
    /// </remarks>
    public AsiBackboneConstraintEvaluationContext ToEvaluationContext(
        string? policyVersion = null,
        string? policyHash = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool mergeRequestMetadata = true)
    {
        return new AsiBackboneConstraintEvaluationContext(
            CorrelationId,
            policyVersion,
            policyHash,
            mergeRequestMetadata ? MergeMetadata(metadata) : NormalizeSuppliedMetadata(metadata));
    }

    /// <summary>
    /// Normalizes supplied metadata keys and values without merging request metadata underneath them.
    /// </summary>
    /// <param name="metadata">The metadata to normalize.</param>
    /// <returns>A normalized metadata dictionary containing only the supplied entries.</returns>
    private static IReadOnlyDictionary<string, string> NormalizeSuppliedMetadata(
        IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return EmptyMetadata;
        }

        Dictionary<string, string> normalized = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in metadata)
        {
            AddIfValid(normalized, item.Key, item.Value);
        }

        return normalized.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(normalized);
    }

    /// <summary>
    /// Merges safe request metadata with host-provided metadata.
    /// </summary>
    /// <param name="metadata">Optional host-provided metadata.</param>
    /// <returns>A normalized metadata dictionary.</returns>
    public IReadOnlyDictionary<string, string> MergeMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if ((metadata is null || metadata.Count == 0) && Metadata.Count == 0)
        {
            return EmptyMetadata;
        }

        Dictionary<string, string> merged = new(StringComparer.Ordinal);

        foreach (KeyValuePair<string, string> item in Metadata)
        {
            AddIfValid(merged, item.Key, item.Value);
        }

        if (metadata is not null)
        {
            foreach (KeyValuePair<string, string> item in metadata)
            {
                AddIfValid(merged, item.Key, item.Value);
            }
        }

        return merged.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(merged);
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
            AddIfValid(normalizedMetadata, item.Key, item.Value);
        }

        return normalizedMetadata.Count == 0
            ? EmptyMetadata
            : new ReadOnlyDictionary<string, string>(normalizedMetadata);
    }

    private static void AddIfValid(Dictionary<string, string> metadata, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        metadata[key.Trim()] = value?.Trim() ?? string.Empty;
    }
}
