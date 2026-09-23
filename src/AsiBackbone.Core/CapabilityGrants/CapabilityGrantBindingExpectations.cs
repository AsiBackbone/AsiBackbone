namespace AsiBackbone.Core.CapabilityGrants;

/// <summary>
/// Provides authoritative subject and operation expectations for capability-grant execution validation.
/// </summary>
public sealed class CapabilityGrantBindingExpectations
{
    private CapabilityGrantBindingExpectations(string subjectId, string? operationName)
    {
        SubjectId = subjectId.Trim();
        OperationName = string.IsNullOrWhiteSpace(operationName) ? null : operationName.Trim();
    }

    /// <summary>
    /// Gets the authenticated subject that must match the grant.
    /// </summary>
    public string SubjectId { get; }

    /// <summary>
    /// Gets the requested operation that must match the grant, when required.
    /// </summary>
    public string? OperationName { get; }

    /// <summary>
    /// Creates execution-binding expectations from authoritative host context.
    /// </summary>
    public static CapabilityGrantBindingExpectations Create(string subjectId, string? operationName = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(subjectId);
        return new CapabilityGrantBindingExpectations(subjectId, operationName);
    }
}
