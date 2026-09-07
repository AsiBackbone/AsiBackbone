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
    /// Gets or sets a value indicating whether signing failures should return unsigned metadata with explicit failure details instead of throwing during normal signing flow.
    /// </summary>
    public bool ReturnUnsignedOnFailure { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether this provider may be registered while the host reports a production environment.
    /// </summary>
    /// <remarks>
    /// The signing key is generated per process and never persisted, so signatures produced by this provider stop
    /// verifying after a restart and carry no key custody story. Registration therefore fails in production unless a host
    /// states this intent explicitly.
    /// </remarks>
    public bool AllowInProduction { get; set; }

    /// <summary>
    /// Gets or sets the environment name used by the production guard, overriding the ambient environment variables.
    /// </summary>
    /// <remarks>
    /// When null, the guard reads <c>DOTNET_ENVIRONMENT</c> and then <c>ASPNETCORE_ENVIRONMENT</c>. This package does not
    /// depend on the hosting abstractions, so a host that determines its environment another way supplies the name here.
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
