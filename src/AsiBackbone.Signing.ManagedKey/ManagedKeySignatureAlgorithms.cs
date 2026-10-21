namespace AsiBackbone.Signing.ManagedKey;

/// <summary>
/// Stable provider-neutral descriptors understood by managed-key signing clients and the built-in public-key verifier.
/// </summary>
public static class ManagedKeySignatureAlgorithms
{
    /// <summary>RSA-PSS with SHA-256 and a SHA-256 salt.</summary>
    public const string RsaPssSha256 = "RSASSA-PSS-SHA256-MANAGED-KEY";

    /// <summary>ECDSA over the NIST P-256 curve with SHA-256.</summary>
    public const string EcdsaP256Sha256 = "ECDSA-P256-SHA256-MANAGED-KEY";
}
