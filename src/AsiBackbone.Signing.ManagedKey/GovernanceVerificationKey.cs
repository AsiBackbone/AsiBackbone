using System.Security.Cryptography;
using System.Text.Json;

namespace AsiBackbone.Signing.ManagedKey;

/// <summary>Represents one trusted public verification-key version.</summary>
public sealed class GovernanceVerificationKey
{
    private GovernanceVerificationKey(
        string keyId,
        string keyVersion,
        string signatureAlgorithm,
        byte[] subjectPublicKeyInfo,
        bool isRevoked)
    {
        KeyId = keyId;
        KeyVersion = keyVersion;
        SignatureAlgorithm = signatureAlgorithm;
        SubjectPublicKeyInfo = subjectPublicKeyInfo;
        IsRevoked = isRevoked;
    }

    /// <summary>Gets the stable key identifier.</summary>
    public string KeyId { get; }

    /// <summary>Gets the stable key version.</summary>
    public string KeyVersion { get; }

    /// <summary>Gets the required signature algorithm descriptor.</summary>
    public string SignatureAlgorithm { get; }

    /// <summary>Gets a value indicating whether this version is revoked.</summary>
    public bool IsRevoked { get; }

    internal byte[] SubjectPublicKeyInfo { get; }

    /// <summary>Creates a trusted key from an SPKI public-key PEM.</summary>
    public static GovernanceVerificationKey FromPem(
        string keyId,
        string keyVersion,
        string signatureAlgorithm,
        string publicKeyPem,
        bool isRevoked = false,
        int minimumRsaKeySize = 2048)
    {
        ValidateIdentity(keyId, keyVersion, signatureAlgorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicKeyPem);

        if (publicKeyPem.Contains("PRIVATE KEY", StringComparison.Ordinal))
        {
            throw new ArgumentException("Trusted verification material must not contain a private key.", nameof(publicKeyPem));
        }

        try
        {
            return signatureAlgorithm switch
            {
                ManagedKeySignatureAlgorithms.RsaPssSha256 => FromRsaPem(
                    keyId.Trim(), keyVersion.Trim(), signatureAlgorithm, publicKeyPem, isRevoked, minimumRsaKeySize),
                ManagedKeySignatureAlgorithms.EcdsaP256Sha256 => FromEcdsaPem(
                    keyId.Trim(), keyVersion.Trim(), signatureAlgorithm, publicKeyPem, isRevoked),
                _ => throw new ArgumentException("The verification-key signature algorithm is unsupported.", nameof(signatureAlgorithm))
            };
        }
        catch (CryptographicException exception)
        {
            throw new ArgumentException("The verification key is not valid public-key material.", nameof(publicKeyPem), exception);
        }
    }

    /// <summary>Creates a trusted key from an RSA or EC public JWK.</summary>
    public static GovernanceVerificationKey FromJwk(
        string keyId,
        string keyVersion,
        string signatureAlgorithm,
        string publicJwk,
        bool isRevoked = false,
        int minimumRsaKeySize = 2048)
    {
        ValidateIdentity(keyId, keyVersion, signatureAlgorithm);
        ArgumentException.ThrowIfNullOrWhiteSpace(publicJwk);

        using var document = JsonDocument.Parse(publicJwk);
        JsonElement root = document.RootElement;
        if (root.TryGetProperty("d", out _)
            || root.TryGetProperty("p", out _)
            || root.TryGetProperty("q", out _)
            || root.TryGetProperty("dp", out _)
            || root.TryGetProperty("dq", out _)
            || root.TryGetProperty("qi", out _))
        {
            throw new ArgumentException("Trusted verification material must not contain a private key.", nameof(publicJwk));
        }

        string kty = RequiredString(root, "kty", nameof(publicJwk));
        try
        {
            if (string.Equals(kty, "RSA", StringComparison.Ordinal)
                && signatureAlgorithm == ManagedKeySignatureAlgorithms.RsaPssSha256)
            {
                using var rsa = RSA.Create();
                rsa.ImportParameters(new RSAParameters
                {
                    Modulus = DecodeBase64Url(RequiredString(root, "n", nameof(publicJwk))),
                    Exponent = DecodeBase64Url(RequiredString(root, "e", nameof(publicJwk)))
                });
                EnsureRsaSize(rsa, minimumRsaKeySize);
                return new(keyId.Trim(), keyVersion.Trim(), signatureAlgorithm, rsa.ExportSubjectPublicKeyInfo(), isRevoked);
            }

            if (string.Equals(kty, "EC", StringComparison.Ordinal)
                && signatureAlgorithm == ManagedKeySignatureAlgorithms.EcdsaP256Sha256
                && string.Equals(RequiredString(root, "crv", nameof(publicJwk)), "P-256", StringComparison.Ordinal))
            {
                using var ecdsa = ECDsa.Create(new ECParameters
                {
                    Curve = ECCurve.NamedCurves.nistP256,
                    Q = new ECPoint
                    {
                        X = DecodeBase64Url(RequiredString(root, "x", nameof(publicJwk))),
                        Y = DecodeBase64Url(RequiredString(root, "y", nameof(publicJwk)))
                    }
                });
                return new(keyId.Trim(), keyVersion.Trim(), signatureAlgorithm, ecdsa.ExportSubjectPublicKeyInfo(), isRevoked);
            }
        }
        catch (CryptographicException exception)
        {
            throw new ArgumentException("The JWK is not valid public-key material.", nameof(publicJwk), exception);
        }

        throw new ArgumentException("The JWK key type, curve, or signature algorithm is unsupported.", nameof(publicJwk));
    }

    private static GovernanceVerificationKey FromRsaPem(
        string keyId, string keyVersion, string algorithm, string pem, bool revoked, int minimumRsaKeySize)
    {
        using var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        EnsureRsaSize(rsa, minimumRsaKeySize);
        return new(keyId, keyVersion, algorithm, rsa.ExportSubjectPublicKeyInfo(), revoked);
    }

    private static GovernanceVerificationKey FromEcdsaPem(
        string keyId, string keyVersion, string algorithm, string pem, bool revoked)
    {
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(pem);
        return ecdsa.KeySize != 256
            ? throw new ArgumentException("ECDSA verification keys must use the P-256 curve.", nameof(pem))
            : new(keyId, keyVersion, algorithm, ecdsa.ExportSubjectPublicKeyInfo(), revoked);
    }

    private static void EnsureRsaSize(RSA rsa, int minimumRsaKeySize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minimumRsaKeySize, 2048);
        if (rsa.KeySize < minimumRsaKeySize)
        {
            throw new ArgumentException($"RSA verification keys must be at least {minimumRsaKeySize} bits.");
        }
    }

    private static void ValidateIdentity(string keyId, string keyVersion, string signatureAlgorithm)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureAlgorithm);
    }

    private static string RequiredString(JsonElement root, string name, string parameterName)
    {
        return !root.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString())
            ? throw new ArgumentException($"The JWK requires a non-empty '{name}' member.", parameterName)
            : value.GetString()!;
    }

    private static byte[] DecodeBase64Url(string value)
    {
        string padded = value.Replace('-', '+').Replace('_', '/');
        padded = padded.PadRight(padded.Length + ((4 - (padded.Length % 4)) % 4), '=');
        return Convert.FromBase64String(padded);
    }
}
