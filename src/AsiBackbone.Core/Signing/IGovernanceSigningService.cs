namespace AsiBackbone.Core.Signing;

/// <summary>
/// Defines the provider-neutral boundary for signing governance artifacts.
/// </summary>
/// <remarks>
/// Concrete implementations may use Azure Key Vault, local development keys, HSM-backed keys, or other key-management systems. Core does not retrieve or store raw signing secrets.
/// </remarks>
public interface IGovernanceSigningService
{
    /// <summary>
    /// Signs the supplied signing request and returns provider-neutral signing metadata.
    /// </summary>
    /// <param name="request">The signing request.</param>
    /// <param name="cancellationToken">A token used to observe cancellation.</param>
    /// <returns>The signing result.</returns>
    ValueTask<SigningResult> SignAsync(
        SigningRequest request,
        CancellationToken cancellationToken = default);
}
