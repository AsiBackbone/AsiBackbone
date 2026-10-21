using System.Security.Cryptography;
using System.Text;
using AsiBackbone.Core.Signing;

namespace AsiBackbone.Signing.ManagedKey;

/// <summary>Verifies governance signatures with trusted public keys only.</summary>
public sealed class PublicKeySignatureVerificationService : IGovernanceSignatureVerificationService
{
    private readonly KeySignatureVerificationOptions options;
    private readonly IGovernanceVerificationKeyResolver resolver;

    /// <summary>Initializes a public-key verifier.</summary>
    public PublicKeySignatureVerificationService(
        KeySignatureVerificationOptions options,
        IGovernanceVerificationKeyResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(resolver);
        options.Validate();
        this.options = options;
        this.resolver = resolver;
    }

    /// <inheritdoc />
    public async ValueTask<SignatureVerificationResult> VerifyAsync(
        SignatureVerificationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        SigningMetadata metadata = request.SigningMetadata;
        if (!metadata.HasSignature)
        {
            return SignatureVerificationResult.MissingSignature();
        }

        if (metadata.SigningHash is null || !FixedTimeEquals(request.SigningHash, metadata.SigningHash))
        {
            return Fail("signature.hash-mismatch", SignatureVerificationCategory.HashMismatch);
        }

        if (request.UsesLegacySignatureInput && !options.AllowLegacySignatureInput)
        {
            return Fail("signature.legacy-input-not-allowed", SignatureVerificationCategory.UntrustedSigningContext);
        }

        if (metadata.SignatureAlgorithm is null || !options.AllowedAlgorithms.Contains(metadata.SignatureAlgorithm))
        {
            return Fail("signature.algorithm-unsupported", SignatureVerificationCategory.UnsupportedAlgorithm);
        }

        if (metadata.KeyId is null)
        {
            return Fail("signature.key-unknown", SignatureVerificationCategory.UnknownKeyVersion);
        }

        if (metadata.KeyVersion is null)
        {
            return Fail("signature.key-version-unknown", SignatureVerificationCategory.UnknownKeyVersion);
        }

        GovernanceVerificationKey? key;
        try
        {
            key = await resolver.ResolveAsync(metadata.KeyId, metadata.KeyVersion, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
        {
            return SignatureVerificationResult.Failed(
                "signature.key-provider-unavailable",
                SignatureVerificationCategory.ProviderUnavailable,
                "The verification key resolver could not answer the request.");
        }

        if (key is null)
        {
            bool knownKey;
            try
            {
                knownKey = await resolver.IsKnownKeyAsync(metadata.KeyId, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (exception is InvalidOperationException or TimeoutException)
            {
                return SignatureVerificationResult.Failed(
                    "signature.key-provider-unavailable",
                    SignatureVerificationCategory.ProviderUnavailable,
                    "The verification key resolver could not answer the request.");
            }

            return Fail(
                knownKey ? "signature.key-version-unknown" : "signature.key-unknown",
                SignatureVerificationCategory.UnknownKeyVersion);
        }

        if (key.IsRevoked)
        {
            return Fail("signature.key-revoked", SignatureVerificationCategory.RevokedKey);
        }

        if (!string.Equals(key.SignatureAlgorithm, metadata.SignatureAlgorithm, StringComparison.Ordinal))
        {
            return Fail("signature.algorithm-unsupported", SignatureVerificationCategory.UnsupportedAlgorithm);
        }

        byte[] signature;
        try
        {
            signature = Convert.FromBase64String(metadata.Signature!);
        }
        catch (FormatException)
        {
            return Fail("signature.invalid", SignatureVerificationCategory.InvalidSignature);
        }

        bool verified;
        try
        {
            verified = Verify(key, request.SignatureInput.Span, signature);
        }
        catch (CryptographicException)
        {
            verified = false;
        }

        return verified
            ? SignatureVerificationResult.Verified()
            : Fail("signature.invalid", SignatureVerificationCategory.InvalidSignature);
    }

    private static bool Verify(GovernanceVerificationKey key, ReadOnlySpan<byte> input, ReadOnlySpan<byte> signature)
    {
        if (key.SignatureAlgorithm == ManagedKeySignatureAlgorithms.RsaPssSha256)
        {
            using var rsa = RSA.Create();
            rsa.ImportSubjectPublicKeyInfo(key.SubjectPublicKeyInfo, out _);
            return rsa.VerifyData(input, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
        }

        using var ecdsa = ECDsa.Create();
        ecdsa.ImportSubjectPublicKeyInfo(key.SubjectPublicKeyInfo, out _);
        return ecdsa.VerifyData(input, signature, HashAlgorithmName.SHA256);
    }

    private static bool FixedTimeEquals(string left, string right)
    {
        byte[] leftBytes = Encoding.UTF8.GetBytes(left);
        byte[] rightBytes = Encoding.UTF8.GetBytes(right);
        return leftBytes.Length == rightBytes.Length
            && CryptographicOperations.FixedTimeEquals(leftBytes, rightBytes);
    }

    private static SignatureVerificationResult Fail(string code, SignatureVerificationCategory category)
    {
        return SignatureVerificationResult.Failed(code, category);
    }
}
