namespace AsiBackbone.Signing.ManagedKey;

/// <summary>Configures public-key signature verification.</summary>
public sealed class KeySignatureVerificationOptions
{
    private readonly List<GovernanceVerificationKey> keys = [];
    private readonly HashSet<string> allowedAlgorithms = new(StringComparer.Ordinal)
    {
        ManagedKeySignatureAlgorithms.RsaPssSha256,
        ManagedKeySignatureAlgorithms.EcdsaP256Sha256
    };

    /// <summary>Gets the configured trusted key versions.</summary>
    public IList<GovernanceVerificationKey> Keys => keys;

    /// <summary>Gets the explicit signature algorithm allow-list.</summary>
    public ISet<string> AllowedAlgorithms => allowedAlgorithms;

    /// <summary>Gets or sets whether pre-6.0 hash-only signature input is accepted.</summary>
    public bool AllowLegacySignatureInput { get; set; }

    /// <summary>Adds an SPKI PEM public key.</summary>
    public void AddPemKey(
        string keyId,
        string keyVersion,
        string signatureAlgorithm,
        string publicKeyPem,
        bool isRevoked = false,
        int minimumRsaKeySize = 2048)
    {
        keys.Add(GovernanceVerificationKey.FromPem(
            keyId, keyVersion, signatureAlgorithm, publicKeyPem, isRevoked, minimumRsaKeySize));
    }

    /// <summary>Adds an RSA or EC public JWK.</summary>
    public void AddJwkKey(
        string keyId,
        string keyVersion,
        string signatureAlgorithm,
        string publicJwk,
        bool isRevoked = false,
        int minimumRsaKeySize = 2048)
    {
        keys.Add(GovernanceVerificationKey.FromJwk(
            keyId, keyVersion, signatureAlgorithm, publicJwk, isRevoked, minimumRsaKeySize));
    }

    internal void Validate()
    {
        if (allowedAlgorithms.Count == 0 || allowedAlgorithms.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("At least one non-empty signature algorithm must be allowed.");
        }

        if (keys.GroupBy(key => (key.KeyId, key.KeyVersion)).Any(group => group.Count() > 1))
        {
            throw new InvalidOperationException("Each configured verification key ID and version pair must be unique.");
        }
    }
}
