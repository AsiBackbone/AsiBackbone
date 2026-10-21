namespace AsiBackbone.Signing.ManagedKey;

/// <summary>
/// Configures the managed-key signing provider.
/// </summary>
/// <remarks>
/// The options carry provider-neutral key references and operational behavior. They must not contain private keys,
/// credentials, connection strings, client secrets, or managed identity tokens.
/// </remarks>
public sealed class ManagedKeySigningOptions
{
    /// <summary>
    /// Gets the default provider descriptor returned in signing metadata.
    /// </summary>
    public const string DefaultProviderName = "managed-key";

    /// <summary>
    /// Gets the default provider-neutral signature algorithm descriptor.
    /// </summary>
    public const string DefaultSignatureAlgorithm = ManagedKeySignatureAlgorithms.RsaPssSha256;

    /// <summary>
    /// Gets the default supported hash algorithm descriptor.
    /// </summary>
    public const string DefaultHashAlgorithm = "SHA-256";

    /// <summary>
    /// Gets the default maximum retry delay.
    /// </summary>
    public static TimeSpan DefaultMaxRetryDelay { get; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the provider descriptor returned in signing metadata.
    /// </summary>
    public string ProviderName { get; set; } = DefaultProviderName;

    /// <summary>
    /// Gets or sets the managed key identifier or key URI reference.
    /// </summary>
    public string KeyId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the managed key version expected for signing.
    /// </summary>
    public string? KeyVersion { get; set; }

    /// <summary>
    /// Gets or sets the provider-neutral signature algorithm descriptor requested from the managed-key client.
    /// </summary>
    public string SignatureAlgorithm { get; set; } = DefaultSignatureAlgorithm;

    /// <summary>
    /// Gets or sets the hash algorithm expected on incoming signing requests.
    /// </summary>
    public string HashAlgorithm { get; set; } = DefaultHashAlgorithm;

    /// <summary>
    /// Gets or sets a value indicating whether signing requests must specify or resolve a key version.
    /// </summary>
    public bool RequireKeyVersion { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether signing failures should return unsigned failure metadata instead of throwing.
    /// </summary>
    /// <remarks>
    /// The production-oriented default is <see langword="false" /> so signing failures fail closed unless a host explicitly
    /// opts into unsigned failure metadata for local validation, samples, or policy-routed fallback behavior.
    /// </remarks>
    public bool ReturnUnsignedOnFailure { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retry attempts after the initial managed-key signing call.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 2;

    /// <summary>
    /// Gets or sets the base delay used by the exponential retry backoff calculation.
    /// </summary>
    /// <remarks>
    /// A zero value keeps retries immediate and disables jittered waiting.
    /// </remarks>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Gets or sets the maximum delay applied before any single retry attempt.
    /// </summary>
    public TimeSpan MaxRetryDelay { get; set; } = DefaultMaxRetryDelay;

    /// <summary>
    /// Creates production-oriented managed-key signing options that fail closed by default when signing cannot complete.
    /// </summary>
    public static ManagedKeySigningOptions Create(
        string keyId,
        string? keyVersion = null,
        string? providerName = null,
        string? signatureAlgorithm = null,
        string? hashAlgorithm = null,
        bool requireKeyVersion = true,
        bool returnUnsignedOnFailure = false,
        int maxRetryAttempts = 2,
        TimeSpan? retryDelay = null)
    {
        var options = new ManagedKeySigningOptions
        {
            KeyId = NormalizeRequired(keyId, string.Empty),
            KeyVersion = NormalizeOptional(keyVersion),
            ProviderName = NormalizeRequired(providerName, DefaultProviderName),
            SignatureAlgorithm = NormalizeRequired(signatureAlgorithm, DefaultSignatureAlgorithm),
            HashAlgorithm = NormalizeRequired(hashAlgorithm, DefaultHashAlgorithm),
            RequireKeyVersion = requireKeyVersion,
            ReturnUnsignedOnFailure = returnUnsignedOnFailure,
            MaxRetryAttempts = maxRetryAttempts,
            RetryDelay = retryDelay ?? TimeSpan.FromMilliseconds(200)
        };

        options.Validate();
        return options;
    }

    /// <summary>
    /// Creates local-validation managed-key signing options that return unsigned failure metadata instead of throwing.
    /// </summary>
    /// <remarks>
    /// Use this only for samples, tests, diagnostics, or hosts that explicitly route unsigned failure metadata through policy.
    /// Production signing paths should prefer <see cref="Create" /> or set <see cref="ReturnUnsignedOnFailure" /> to
    /// <see langword="false" />.
    /// </remarks>
    public static ManagedKeySigningOptions CreateLocalValidation(
        string keyId,
        string? keyVersion = null,
        string? providerName = null,
        string? signatureAlgorithm = null,
        string? hashAlgorithm = null,
        bool requireKeyVersion = true,
        int maxRetryAttempts = 2,
        TimeSpan? retryDelay = null)
    {
        return Create(
            keyId,
            keyVersion,
            providerName,
            signatureAlgorithm,
            hashAlgorithm,
            requireKeyVersion,
            returnUnsignedOnFailure: true,
            maxRetryAttempts: maxRetryAttempts,
            retryDelay: retryDelay);
    }

    /// <summary>
    /// Validates the managed-key signing options.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(ProviderName))
        {
            throw new InvalidOperationException("Managed-key signing provider name is required.");
        }

        if (string.IsNullOrWhiteSpace(KeyId))
        {
            throw new InvalidOperationException("Managed-key signing key ID is required.");
        }

        if (string.IsNullOrWhiteSpace(SignatureAlgorithm))
        {
            throw new InvalidOperationException("Managed-key signing signature algorithm is required.");
        }

        if (string.IsNullOrWhiteSpace(HashAlgorithm))
        {
            throw new InvalidOperationException("Managed-key signing hash algorithm is required.");
        }

        if (MaxRetryAttempts < 0)
        {
            throw new InvalidOperationException("Managed-key signing retry attempts must be greater than or equal to zero.");
        }

        if (RetryDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Managed-key signing retry delay must be greater than or equal to zero.");
        }

        if (MaxRetryDelay < TimeSpan.Zero)
        {
            throw new InvalidOperationException("Managed-key signing maximum retry delay must be greater than or equal to zero.");
        }

        if (MaxRetryDelay < RetryDelay)
        {
            throw new InvalidOperationException("Managed-key signing maximum retry delay must be greater than or equal to the base retry delay.");
        }
    }

    private static string NormalizeRequired(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
