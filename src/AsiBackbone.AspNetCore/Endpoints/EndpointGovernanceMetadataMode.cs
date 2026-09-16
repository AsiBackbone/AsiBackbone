namespace AsiBackbone.AspNetCore.Endpoints;

/// <summary>
/// Controls how much endpoint metadata is attached to endpoint governance evaluation, audit, and diagnostics paths.
/// </summary>
public enum EndpointGovernanceMetadataMode
{
    /// <summary>
    /// Includes the complete normalized endpoint metadata dictionary.
    /// </summary>
    Full = 0,

    /// <summary>
    /// Includes only the endpoint operation name in metadata forwarded through the governance hot path.
    /// </summary>
    Reduced = 1
}
