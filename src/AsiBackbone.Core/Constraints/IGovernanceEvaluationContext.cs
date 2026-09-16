namespace AsiBackbone.Core.Constraints;

/// <summary>
/// Represents framework-neutral context data commonly useful during constraint evaluation.
/// </summary>
public interface IGovernanceEvaluationContext
{
    /// <summary>
    /// Gets the correlation identifier associated with the evaluation, when supplied by the host.
    /// </summary>
    string? CorrelationId { get; }

    /// <summary>
    /// Gets the policy version associated with the evaluation, when supplied by the host.
    /// </summary>
    string? PolicyVersion { get; }

    /// <summary>
    /// Gets the policy hash associated with the evaluation, when supplied by the host.
    /// </summary>
    string? PolicyHash { get; }

    /// <summary>
    /// Gets additional framework-neutral metadata supplied by the host.
    /// </summary>
    IReadOnlyDictionary<string, string> Metadata { get; }
}
