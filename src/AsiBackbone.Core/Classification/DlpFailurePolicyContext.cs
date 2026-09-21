using System.Collections.ObjectModel;

namespace AsiBackbone.Core.Classification;

/// <summary>
/// Represents provider-neutral context for resolving DLP or classification failure behavior.
/// </summary>
public sealed class DlpFailurePolicyContext
{
    private static readonly IReadOnlyDictionary<string, string> EmptyMetadata =
        new ReadOnlyDictionary<string, string>(
            new Dictionary<string, string>(StringComparer.Ordinal));

    private DlpFailurePolicyContext(
        DlpClassificationFailureKind failureKind,
        DlpIntentRiskLevel riskLevel,
        string? intentCategory,
        string? environment,
        string? correlationId,
        string? traceId,
        string? policyVersion,
        string? policyHash,
        TimeSpan? timeout,
        IReadOnlyDictionary<string, string> metadata)
    {
        if (!Enum.IsDefined(failureKind))
        {
            throw new ArgumentOutOfRangeException(nameof(failureKind), failureKind, "DLP failure kind must be defined.");
        }

        if (!Enum.IsDefined(riskLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(riskLevel), riskLevel, "DLP intent risk level must be defined.");
        }

        // The host assigns the risk tier, and every tier maps to a different failure behavior. An unassigned tier is
        // rejected here rather than treated as the lowest one, so a caller that never set it fails closed at the boundary
        // instead of resolving to the most permissive policy.
        if (riskLevel == DlpIntentRiskLevel.Unspecified)
        {
            throw new ArgumentOutOfRangeException(
                nameof(riskLevel),
                riskLevel,
                "DLP intent risk level must be assigned. Supply Low, Medium, or High rather than leaving the risk level unspecified.");
        }

        if (timeout < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeout), timeout, "Timeout must be greater than or equal to zero.");
        }

        FailureKind = failureKind;
        RiskLevel = riskLevel;
        IntentCategory = NormalizeOptional(intentCategory);
        Environment = NormalizeOptional(environment);
        CorrelationId = NormalizeOptional(correlationId);
        TraceId = NormalizeOptional(traceId);
        PolicyVersion = NormalizeOptional(policyVersion);
        PolicyHash = NormalizeOptional(policyHash);
        Timeout = timeout;
        Metadata = metadata;
    }

    /// <summary>
    /// Gets the provider-neutral DLP or classification failure kind.
    /// </summary>
    public DlpClassificationFailureKind FailureKind { get; }

    /// <summary>
    /// Gets the host-assigned risk level for the intent.
    /// </summary>
    public DlpIntentRiskLevel RiskLevel { get; }

    /// <summary>
    /// Gets the host-defined intent category, when supplied.
    /// </summary>
    public string? IntentCategory { get; }

    /// <summary>
    /// Gets the host-defined environment, when supplied.
    /// </summary>
    public string? Environment { get; }

    /// <summary>
    /// Gets the correlation identifier associated with the governed flow, when supplied.
    /// </summary>
    public string? CorrelationId { get; }

    /// <summary>
    /// Gets the trace identifier associated with the governed flow, when supplied.
    /// </summary>
    public string? TraceId { get; }

    /// <summary>
    /// Gets the policy version associated with the governed flow, when supplied.
    /// </summary>
    public string? PolicyVersion { get; }

    /// <summary>
    /// Gets the policy hash associated with the governed flow, when supplied.
    /// </summary>
    public string? PolicyHash { get; }

    /// <summary>
    /// Gets the timeout value associated with the failure, when applicable.
    /// </summary>
    public TimeSpan? Timeout { get; }

    /// <summary>
    /// Gets provider-neutral host metadata associated with the failure.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; }

    /// <summary>
    /// Gets a value indicating whether timeout context is present.
    /// </summary>
    public bool HasTimeout => Timeout.HasValue;

    /// <summary>
    /// Gets a value indicating whether metadata is present.
    /// </summary>
    public bool HasMetadata => Metadata.Count > 0;

    /// <summary>
    /// Creates a provider-neutral DLP or classification failure policy context.
    /// </summary>
    public static DlpFailurePolicyContext Create(
        DlpClassificationFailureKind failureKind,
        DlpIntentRiskLevel riskLevel,
        string? intentCategory = null,
        string? environment = null,
        string? correlationId = null,
        string? traceId = null,
        string? policyVersion = null,
        string? policyHash = null,
        TimeSpan? timeout = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return new DlpFailurePolicyContext(
            failureKind,
            riskLevel,
            intentCategory,
            environment,
            correlationId,
            traceId,
            policyVersion,
            policyHash,
            timeout,
            NormalizeMetadata(metadata));
    }

    /// <summary>
    /// Creates timeout-specific failure policy context.
    /// </summary>
    public static DlpFailurePolicyContext TimeoutFailure(
        DlpIntentRiskLevel riskLevel,
        TimeSpan? timeout = null,
        string? intentCategory = null,
        string? environment = null,
        string? correlationId = null,
        string? traceId = null,
        string? policyVersion = null,
        string? policyHash = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return Create(
            DlpClassificationFailureKind.Timeout,
            riskLevel,
            intentCategory,
            environment,
            correlationId,
            traceId,
            policyVersion,
            policyHash,
            timeout,
            metadata);
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
