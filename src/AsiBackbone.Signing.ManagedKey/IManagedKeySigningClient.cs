namespace AsiBackbone.Signing.ManagedKey;

/// <summary>
/// Defines the host-owned managed-key signing boundary used by the AsiBackbone managed-key signing provider.
/// </summary>
/// <remarks>
/// <para>
/// Implementations should call a managed key system, HSM, cloud KMS, or equivalent key-management service without
/// exposing raw private key material to AsiBackbone Core or to this provider package.
/// </para>
/// <para>
/// Implementations must sign <see cref="ManagedKeySignRequest.SignatureInput" />, not
/// <see cref="ManagedKeySignRequest.SigningHash" />. Since 6.0 the signature input binds the canonical descriptors, hash,
/// and signing policy context; signing the hash text leaves that context unauthenticated and fails verification.
/// </para>
/// </remarks>
public interface IManagedKeySigningClient
{
    /// <summary>
    /// Signs the governance artifact signature input through a managed-key boundary.
    /// </summary>
    /// <param name="request">The managed-key signing request.</param>
    /// <param name="cancellationToken">A token used to observe cancellation.</param>
    /// <returns>The managed-key signing result.</returns>
    ValueTask<ManagedKeySignResult> SignAsync(
        ManagedKeySignRequest request,
        CancellationToken cancellationToken = default);
}
