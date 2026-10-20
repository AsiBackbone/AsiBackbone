namespace AsiBackbone.AspNetCore.Endpoints;

/// <summary>
/// Request feature that records the capability grant an <see cref="IEndpointCapabilityGrantValidator" /> accepted for
/// the current endpoint governance evaluation.
/// </summary>
/// <remarks>
/// <see cref="DefaultEndpointGovernanceService" /> clears this feature before it calls the validator and, when audit is
/// requested, copies <see cref="GrantId" /> into the decision receipt's
/// <see cref="Core.Audit.IDecisionReceipt.CapabilityGrantId" />. <see cref="Core.Audit.AuditLedgerRecord.FromDecisionReceipt" />
/// then records it as <see cref="Core.Audit.AuditLedgerRecord.CapabilityTokenId" />. A validator should set the feature only
/// after the grant was accepted, and never when it returns a denial.
/// </remarks>
public interface IEndpointCapabilityGrantFeature
{
    /// <summary>
    /// Gets the identifier of the accepted capability grant.
    /// </summary>
    string GrantId { get; }
}

/// <summary>
/// Default <see cref="IEndpointCapabilityGrantFeature" /> implementation.
/// </summary>
public sealed class EndpointCapabilityGrantFeature : IEndpointCapabilityGrantFeature
{
    /// <summary>
    /// Initializes a new instance of the <see cref="EndpointCapabilityGrantFeature" /> class.
    /// </summary>
    /// <param name="grantId">The identifier of the accepted capability grant.</param>
    /// <exception cref="ArgumentException"><paramref name="grantId" /> is null, empty, or whitespace.</exception>
    public EndpointCapabilityGrantFeature(string grantId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(grantId);
        GrantId = grantId.Trim();
    }

    /// <inheritdoc />
    public string GrantId { get; }
}
