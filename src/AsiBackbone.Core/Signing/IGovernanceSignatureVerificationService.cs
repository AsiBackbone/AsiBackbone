namespace AsiBackbone.Core.Signing;

/// <summary>
/// Defines the provider-neutral boundary for verifying signed governance artifacts.
/// </summary>
/// <remarks>
/// Concrete implementations may resolve key versions through Azure Key Vault, local development keys, HSM-backed keys, or other key-management systems. Core does not assume a specific signing algorithm or key provider.
/// </remarks>
public interface IGovernanceSignatureVerificationService
{
    /// <summary>
    /// Verifies the supplied signature verification request.
    /// </summary>
    /// <param name="request">The verification request.</param>
    /// <param name="cancellationToken">A token used to observe cancellation.</param>
    /// <returns>The verification result.</returns>
    ValueTask<SignatureVerificationResult> VerifyAsync(
        SignatureVerificationRequest request,
        CancellationToken cancellationToken = default);
}
