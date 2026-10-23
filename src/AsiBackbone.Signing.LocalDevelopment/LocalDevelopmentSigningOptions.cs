namespace AsiBackbone.Signing.LocalDevelopment;

/// <summary>
/// Configures the local-development signing provider.
/// </summary>
/// <remarks>
/// This provider is intended for local development, samples, and tests. It is not a production managed-key provider and does not create tamper-evidence by itself.
/// </remarks>
public sealed class LocalDevelopmentSigningOptions
{
    /// <summary>
    /// Gets the minimum supported RSA key size for generated local-development keys.
    /// </summary>
    public const int MinimumKeySizeBits = 2048;

    /// <summary>
    /// Gets the default provider descriptor returned in signing metadata.
    /// </summary>
    public const string DefaultProviderName = "local-development";

    /// <summary>
    /// Gets the default local-development key identifier.
    /// </summary>
    public const string DefaultKeyId = "local-dev-key";

    /// <summary>
    /// Gets the default local-development key version.
    /// </summary>
    public const string DefaultKeyVersion = "dev";

    /// <summary>
    /// Gets the default provider-neutral signature algorithm descriptor.
    /// </summary>
    public const string DefaultSignatureAlgorithm = "RSASSA-PSS-SHA256-LOCAL-DEV";

    /// <summary>
    /// Gets the default RSA key size for generated local-development keys.
    /// </summary>
    public const int DefaultKeySizeBits = MinimumKeySizeBits;

    /// <summary>
    /// Gets or sets the provider descriptor returned in signing metadata.
    /// </summary>
    public string ProviderName { get; set; } = DefaultProviderName;

    /// <summary>
    /// Gets or sets the local-development key identifier returned in signing metadata.
    /// </summary>
    public string KeyId { get; set; } = DefaultKeyId;

    /// <summary>
    /// Gets or sets the local-development key version returned in signing metadata.
    /// </summary>
    public string KeyVersion { get; set; } = DefaultKeyVersion;

    /// <summary>
    /// Gets or sets the signature algorithm descriptor returned in signing metadata.
    /// </summary>
    public string SignatureAlgorithm { get; set; } = DefaultSignatureAlgorithm;

    /// <summary>
    /// Gets or sets the generated RSA key size in bits.
    /// </summary>
    /// <remarks>
    /// Values below <see cref="MinimumKeySizeBits" /> are invalid. Omitted configuration uses
    /// <see cref="DefaultKeySizeBits" />.
    /// </remarks>
    public int KeySizeBits { get; set; } = DefaultKeySizeBits;

    /// <summary>
    /// Gets or sets a PEM-encoded RSA key to use instead of generating one per process.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When null or blank, the provider generates a <see cref="KeySizeBits" /> key in each process, so signatures stop
    /// verifying after a restart and cannot be verified by another process. Supplying a key gives every process
    /// configured with it the same signing identity, which lets a capability grant issued by one process, or before a
    /// restart, verify in another.
    /// </para>
    /// <para>
    /// A private key (<c>RSA PRIVATE KEY</c> or <c>PRIVATE KEY</c>) supports signing and verification. A public key
    /// (<c>PUBLIC KEY</c> or <c>RSA PUBLIC KEY</c>) supports verification only; signing with it fails. The key must be at
    /// least <see cref="MinimumKeySizeBits" /> bits, and <see cref="KeySizeBits" /> is ignored. Encrypted PEM is not
    /// supported.
    /// </para>
    /// <para>
    /// This remains a local-development provider: the key is held in process memory with no custody, rotation, or
    /// revocation story. Load it from a development secret store rather than source control, and use a managed key
    /// provider in production.
    /// </para>
    /// </remarks>
    public string? RsaKeyPem { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether signing failures should return unsigned metadata with explicit failure details instead of throwing during normal signing flow.
    /// </summary>
    public bool ReturnUnsignedOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this provider may be registered while the host reports a production environment.
    /// </summary>
    /// <remarks>
    /// Registration fails in Production by default. This provider is not a production key-custody control, even when a
    /// configured PEM is supplied. Setting this property to <see langword="true" /> is an explicit escape hatch for
    /// exceptional test/demo scenarios only; it does not make LocalDevelopment signing suitable for normal production
    /// use. Production hosts should use <c>AsiBackbone.Signing.ManagedKey</c> with a reviewed host-owned managed-key
    /// implementation. See the signing-provider package-boundary guidance in the AsiBackbone documentation.
    /// </remarks>
    public bool AllowInProduction { get; set; }

    /// <summary>
    /// Gets or sets the environment name used by the production guard, overriding the ambient environment variables.
    /// </summary>
    /// <remarks>
    /// When null, the guard reads <c>DOTNET_ENVIRONMENT</c> and then <c>ASPNETCORE_ENVIRONMENT</c>. This package does not
    /// depend on the hosting abstractions, so non-hosted/unit-test scenarios remain usable without fabricating a host
    /// environment. A host that determines its environment another way supplies the name here.
    /// </remarks>
    public string? EnvironmentName { get; set; }

    /// <summary>
    /// Validates the configured local-development signing options.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <see cref="KeySizeBits" /> is below <see cref="MinimumKeySizeBits" />.
    /// </exception>
    public void Validate()
    {
        if (KeySizeBits < MinimumKeySizeBits)
        {
            throw new InvalidOperationException(
                $"Local-development RSA key size must be at least {MinimumKeySizeBits} bits. Configured value: {KeySizeBits}.");
        }
    }

    /// <summary>
    /// Creates an independent copy of these options.
    /// </summary>
    /// <remarks>
    /// Registration and the signing service each hold a snapshot rather than the caller's instance. Every property has a
    /// public setter, so holding the caller's instance would let a later assignment, such as setting
    /// <see cref="AllowInProduction" /> after the production guard ran, change a registered service's behavior.
    /// </remarks>
    /// <returns>A copy whose later changes do not affect this instance, and the reverse.</returns>
    internal LocalDevelopmentSigningOptions Snapshot()
    {
        return new LocalDevelopmentSigningOptions
        {
            ProviderName = ProviderName,
            KeyId = KeyId,
            KeyVersion = KeyVersion,
            SignatureAlgorithm = SignatureAlgorithm,
            KeySizeBits = KeySizeBits,
            RsaKeyPem = RsaKeyPem,
            ReturnUnsignedOnFailure = ReturnUnsignedOnFailure,
            AllowInProduction = AllowInProduction,
            EnvironmentName = EnvironmentName
        };
    }

    /// <summary>
    /// Creates options for the local-development signing provider.
    /// </summary>
    public static LocalDevelopmentSigningOptions Create(
        string? providerName = null,
        string? keyId = null,
        string? keyVersion = null,
        string? signatureAlgorithm = null,
        int keySizeBits = DefaultKeySizeBits,
        bool returnUnsignedOnFailure = true,
        bool allowInProduction = false,
        string? environmentName = null)
    {
        return new LocalDevelopmentSigningOptions
        {
            AllowInProduction = allowInProduction,
            EnvironmentName = string.IsNullOrWhiteSpace(environmentName) ? null : environmentName.Trim(),
            ProviderName = string.IsNullOrWhiteSpace(providerName)
                ? DefaultProviderName
                : providerName.Trim(),
            KeyId = string.IsNullOrWhiteSpace(keyId)
                ? DefaultKeyId
                : keyId.Trim(),
            KeyVersion = string.IsNullOrWhiteSpace(keyVersion)
                ? DefaultKeyVersion
                : keyVersion.Trim(),
            SignatureAlgorithm = string.IsNullOrWhiteSpace(signatureAlgorithm)
                ? DefaultSignatureAlgorithm
                : signatureAlgorithm.Trim(),
            KeySizeBits = keySizeBits,
            ReturnUnsignedOnFailure = returnUnsignedOnFailure
        };
    }
}
