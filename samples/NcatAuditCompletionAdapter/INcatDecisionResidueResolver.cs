using AsiBackbone.Core.Audit;

namespace AsiBackbone.Samples.NcatAuditCompletionAdapter;

/// <summary>
/// Resolves the AsiBackbone decision residue associated with an NCAT completion handoff.
/// </summary>
/// <remarks>
/// The consuming host owns this lookup because decision persistence may use any supported
/// AsiBackbone store or a host-defined repository. Returning <see langword="null" /> defers
/// delivery without acknowledging the NCAT completion entry.
/// </remarks>
public interface INcatDecisionResidueResolver
{
    /// <summary>
    /// Resolves the decision residue associated with the supplied decision and correlation identifiers.
    /// </summary>
    ValueTask<IDecisionReceipt?> ResolveAsync(
        string decisionAuditRecordId,
        string? correlationId,
        CancellationToken cancellationToken = default);
}
