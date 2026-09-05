using System.Security.Cryptography;
using System.Text;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Signing.LocalDevelopment;

/// <summary>
/// Provides local-development RSA signing and verification for AsiBackbone signing abstractions.
/// </summary>
/// <remarks>
/// This service generates an in-process RSA key for samples and tests. It is not a production managed-key provider.
/// </remarks>
public sealed class LocalDevelopmentSigningService : IAsiBackboneSigningService, IAsiBackboneSignatureVerificationService, IDisposable
{
    private const string SupportedHashAlgorithm = "SHA-256";
    private static readonly Encoding SigningEncoding = Encoding.UTF8;

    private readonly LocalDevelopmentSigningOptions options;
    private readonly RSA rsa;
    private readonly Lock rsaSync = new();
    private readonly int keySizeBits;
    private bool disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDevelopmentSigningService" /> class with default local-development options.
    /// </summary>
    public LocalDevelopmentSigningService()
        : this(LocalDevelopmentSigningOptions.Create())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="LocalDevelopmentSigningService" /> class.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the configured RSA key size is below the supported minimum.
    /// </exception>
    public LocalDevelopmentSigningService(LocalDevelopmentSigningOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        this.options = options;
        rsa = RSA.Create();
        rsa.KeySize = options.KeySizeBits;
        keySizeBits = rsa.KeySize;
    }

    /// <inheritdoc />
    public ValueTask<SigningResult> SignAsync(
        SigningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        string? validationFailure = ValidateSigningRequest(request);
        if (validationFailure is not null)
        {
            return ValueTask.FromResult(CreateUnsignedFailureResult(request, validationFailure, validationFailure));
        }

        try
        {
            lock (rsaSync)
            {
                ThrowIfDisposed();

                byte[] data = SigningEncoding.GetBytes(request.SigningHash);
                byte[] signature = rsa.SignData(data, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

                Dictionary<string, string> metadata = CreateBaseMetadata(request);
                metadata["signing_status"] = "signed";

                var signingMetadata = SigningMetadata.Create(
                    signingHash: request.SigningHash,
                    hashAlgorithm: NormalizeHashAlgorithm(request.HashAlgorithm),
                    signature: Convert.ToBase64String(signature),
                    signatureAlgorithm: NormalizeRequired(options.SignatureAlgorithm, LocalDevelopmentSigningOptions.DefaultSignatureAlgorithm),
                    keyId: NormalizeRequired(options.KeyId, LocalDevelopmentSigningOptions.DefaultKeyId),
                    keyVersion: NormalizeRequired(options.KeyVersion, LocalDevelopmentSigningOptions.DefaultKeyVersion),
                    provider: NormalizeRequired(options.ProviderName, LocalDevelopmentSigningOptions.DefaultProviderName),
                    signedUtc: DateTimeOffset.UtcNow,
                    metadata: metadata);

                return ValueTask.FromResult(SigningResult.FromMetadata(signingMetadata));
            }
        }
        catch (Exception exception) when (exception is CryptographicException or ObjectDisposedException or InvalidOperationException)
        {
            if (!options.ReturnUnsignedOnFailure)
            {
                throw;
            }

            return ValueTask.FromResult(CreateUnsignedFailureResult(request, "localdev.signing.failed", exception.GetType().Name));
        }
    }

    /// <inheritdoc />
    public ValueTask<SignatureVerificationResult> VerifyAsync(
        SignatureVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        SigningMetadata metadata = request.SigningMetadata;

        if (!metadata.HasSignature)
        {
            return ValueTask.FromResult(SignatureVerificationResult.MissingSignature("The local-development verifier received no signature value."));
        }

        if (!FixedTimeHashEquals(request.SigningHash, metadata.SigningHash))
        {
            return ValueTask.FromResult(SignatureVerificationResult.Failed(
                "localdev.signature.hash-mismatch",
                SignatureVerificationCategory.HashMismatch,
                "The verification hash does not match the hash recorded in signing metadata."));
        }

        if (!IsSupportedHashAlgorithm(metadata.HashAlgorithm))
        {
            return ValueTask.FromResult(SignatureVerificationResult.Failed(
                "localdev.signature.hash-algorithm-unsupported",
                SignatureVerificationCategory.UnsupportedAlgorithm,
                "The local-development verifier supports SHA-256 signing metadata only."));
        }

        if (!string.Equals(metadata.SignatureAlgorithm, NormalizeRequired(options.SignatureAlgorithm, LocalDevelopmentSigningOptions.DefaultSignatureAlgorithm), StringComparison.Ordinal))
        {
            return ValueTask.FromResult(SignatureVerificationResult.Failed(
                "localdev.signature.algorithm-mismatch",
                SignatureVerificationCategory.UnsupportedAlgorithm,
                "The signature algorithm does not match the configured local-development provider."));
        }

        if (!string.Equals(metadata.KeyId, NormalizeRequired(options.KeyId, LocalDevelopmentSigningOptions.DefaultKeyId), StringComparison.Ordinal)
            || !string.Equals(metadata.KeyVersion, NormalizeRequired(options.KeyVersion, LocalDevelopmentSigningOptions.DefaultKeyVersion), StringComparison.Ordinal))
        {
            return ValueTask.FromResult(SignatureVerificationResult.Failed(
                "localdev.signature.key-mismatch",
                SignatureVerificationCategory.UnknownKeyVersion,
                "The signature key reference does not match the configured local-development provider."));
        }

        try
        {
            lock (rsaSync)
            {
                ThrowIfDisposed();

                byte[] signature = Convert.FromBase64String(metadata.Signature!);
                byte[] data = SigningEncoding.GetBytes(request.SigningHash);
                bool verified = rsa.VerifyData(data, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);

                return ValueTask.FromResult(verified
                    ? SignatureVerificationResult.Verified()
                    : SignatureVerificationResult.Failed(
                        "localdev.signature.invalid",
                        SignatureVerificationCategory.InvalidSignature,
                        "The local-development signature did not verify."));
            }
        }
        catch (FormatException)
        {
            return ValueTask.FromResult(SignatureVerificationResult.Failed(
                "localdev.signature.malformed",
                SignatureVerificationCategory.InvalidSignature,
                "The signature value is not valid Base64."));
        }
        catch (Exception exception) when (exception is CryptographicException or ObjectDisposedException or InvalidOperationException)
        {
            return ValueTask.FromResult(SignatureVerificationResult.Failed(
                "localdev.verification.failed",
                SignatureVerificationCategory.Failed,
                exception.GetType().Name));
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (rsaSync)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            rsa.Dispose();
        }
    }

    private static string NormalizeRequired(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string NormalizeHashAlgorithm(string? hashAlgorithm)
    {
        return string.IsNullOrWhiteSpace(hashAlgorithm)
            ? SupportedHashAlgorithm
            : hashAlgorithm.Trim().Equals("SHA256", StringComparison.OrdinalIgnoreCase)
                ? SupportedHashAlgorithm
                : hashAlgorithm.Trim();
    }

    private static bool IsSupportedHashAlgorithm(string? hashAlgorithm)
    {
        string normalized = NormalizeHashAlgorithm(hashAlgorithm);

        return normalized.Equals(SupportedHashAlgorithm, StringComparison.OrdinalIgnoreCase);
    }

    private static bool FixedTimeHashEquals(string? expectedHash, string? actualHash)
    {
        if (expectedHash is null || actualHash is null)
        {
            return expectedHash is null && actualHash is null;
        }

        byte[] expectedBytes = SigningEncoding.GetBytes(expectedHash);
        byte[] actualBytes = SigningEncoding.GetBytes(actualHash);

        return CryptographicOperations.FixedTimeEquals(expectedBytes, actualBytes);
    }

    private string? ValidateSigningRequest(SigningRequest request)
    {
        if (Volatile.Read(ref disposed))
        {
            return "localdev.signing.disposed";
        }

        if (!IsSupportedHashAlgorithm(request.HashAlgorithm))
        {
            return "localdev.signing.hash-algorithm-unsupported";
        }

        string configuredKeyId = NormalizeRequired(options.KeyId, LocalDevelopmentSigningOptions.DefaultKeyId);
        if (request.KeyId is not null && !string.Equals(request.KeyId, configuredKeyId, StringComparison.Ordinal))
        {
            return "localdev.signing.key-mismatch";
        }

        string configuredKeyVersion = NormalizeRequired(options.KeyVersion, LocalDevelopmentSigningOptions.DefaultKeyVersion);
        return request.KeyVersion is not null && !string.Equals(request.KeyVersion, configuredKeyVersion, StringComparison.Ordinal)
            ? "localdev.signing.key-version-mismatch"
            : null;
    }

    private SigningResult CreateUnsignedFailureResult(SigningRequest request, string failureCode, string failureMessage)
    {
        Dictionary<string, string> metadata = CreateBaseMetadata(request);
        metadata["signing_status"] = "failed";
        metadata["failure_code"] = failureCode;
        metadata["failure_message"] = failureMessage;

        var signingMetadata = SigningMetadata.Create(
            signingHash: request.SigningHash,
            hashAlgorithm: NormalizeHashAlgorithm(request.HashAlgorithm),
            keyId: NormalizeRequired(options.KeyId, LocalDevelopmentSigningOptions.DefaultKeyId),
            keyVersion: NormalizeRequired(options.KeyVersion, LocalDevelopmentSigningOptions.DefaultKeyVersion),
            provider: NormalizeRequired(options.ProviderName, LocalDevelopmentSigningOptions.DefaultProviderName),
            metadata: metadata);

        return SigningResult.FromMetadata(signingMetadata);
    }

    private Dictionary<string, string> CreateBaseMetadata(SigningRequest request)
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["provider_kind"] = "local-development",
            ["provider_warning"] = "local-development-only",
            ["key_algorithm"] = "RSA",
            ["key_size_bits"] = keySizeBits.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        if (request.Purpose is not null)
        {
            metadata["purpose"] = request.Purpose;
        }

        foreach (KeyValuePair<string, string> item in request.Metadata)
        {
            if (string.IsNullOrWhiteSpace(item.Key))
            {
                continue;
            }

            metadata[item.Key.Trim()] = item.Value?.Trim() ?? string.Empty;
        }

        return metadata;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}
