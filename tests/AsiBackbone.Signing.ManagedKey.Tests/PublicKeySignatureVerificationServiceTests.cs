using System.Security.Cryptography;
using AsiBackbone.Core.Signing;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.Signing.ManagedKey.Tests;

/// <summary>Tests trusted public-key signature verification.</summary>
public sealed class PublicKeySignatureVerificationServiceTests
{
    /// <summary>Verifies current and previous versions during rotation.</summary>
    [Fact]
    public async Task ConfiguredCurrentAndPreviousRsaVersionsVerify()
    {
        using var previous = RSA.Create(2048);
        using var current = RSA.Create(2048);
        IGovernanceSignatureVerificationService verifier = CreateVerifier(
            ("v1", previous.ExportSubjectPublicKeyInfoPem(), false),
            ("v2", current.ExportSubjectPublicKeyInfoPem(), false));

        SignatureVerificationResult previousResult = await verifier.VerifyAsync(
            CreateRequest(previous, "v1"), TestContext.Current.CancellationToken);
        SignatureVerificationResult currentResult = await verifier.VerifyAsync(
            CreateRequest(current, "v2"), TestContext.Current.CancellationToken);

        Assert.True(previousResult.IsValid);
        Assert.True(currentResult.IsValid);
    }

    /// <summary>Verifies stable failures for revoked and unknown versions.</summary>
    [Fact]
    public async Task RevokedAndUnknownVersionsReturnStableFailures()
    {
        using var rsa = RSA.Create(2048);
        IGovernanceSignatureVerificationService verifier = CreateVerifier(
            ("revoked", rsa.ExportSubjectPublicKeyInfoPem(), true));

        SignatureVerificationResult revoked = await verifier.VerifyAsync(
            CreateRequest(rsa, "revoked"), TestContext.Current.CancellationToken);
        SignatureVerificationResult unknown = await verifier.VerifyAsync(
            CreateRequest(rsa, "unknown"), TestContext.Current.CancellationToken);
        SignatureVerificationResult unknownKey = await verifier.VerifyAsync(
            CreateRequest(rsa, "unknown", "missing-key"), TestContext.Current.CancellationToken);

        Assert.Equal("signature.key-revoked", revoked.FailureCode);
        Assert.Equal(SignatureVerificationCategory.RevokedKey, revoked.Category);
        Assert.Equal("signature.key-version-unknown", unknown.FailureCode);
        Assert.Equal(SignatureVerificationCategory.UnknownKeyVersion, unknown.Category);
        Assert.Equal("signature.key-unknown", unknownKey.FailureCode);
    }

    /// <summary>Verifies the explicit algorithm allow-list.</summary>
    [Fact]
    public async Task DisallowedAlgorithmIsRejectedBeforeCryptographicVerification()
    {
        using var rsa = RSA.Create(2048);
        ServiceProvider provider = new ServiceCollection()
            .AddAsiBackboneKeySignatureVerification(options =>
            {
                options.AddPemKey("key-1", "v1", ManagedKeySignatureAlgorithms.RsaPssSha256,
                    rsa.ExportSubjectPublicKeyInfoPem());
                options.AllowedAlgorithms.Clear();
                _ = options.AllowedAlgorithms.Add(ManagedKeySignatureAlgorithms.EcdsaP256Sha256);
            })
            .BuildServiceProvider();

        SignatureVerificationResult result = await provider
            .GetRequiredService<IGovernanceSignatureVerificationService>()
            .VerifyAsync(CreateRequest(rsa, "v1"), TestContext.Current.CancellationToken);

        Assert.Equal("signature.algorithm-unsupported", result.FailureCode);
        Assert.Equal(SignatureVerificationCategory.UnsupportedAlgorithm, result.Category);
    }

    /// <summary>Verifies that trusted material can contain public keys only and meets minimum strength.</summary>
    [Fact]
    public void PrivateAndUndersizedRsaKeysAreRejected()
    {
        using var privateRsa = RSA.Create(2048);
        using var weakRsa = RSA.Create(1024);

        _ = Assert.Throws<ArgumentException>(() => GovernanceVerificationKey.FromPem(
            "key-1", "v1", ManagedKeySignatureAlgorithms.RsaPssSha256, privateRsa.ExportPkcs8PrivateKeyPem()));
        _ = Assert.Throws<ArgumentException>(() => GovernanceVerificationKey.FromPem(
            "key-1", "v1", ManagedKeySignatureAlgorithms.RsaPssSha256, weakRsa.ExportSubjectPublicKeyInfoPem()));
    }

    /// <summary>Verifies RSA public JWK parsing without private material.</summary>
    [Fact]
    public async Task PublicRsaJwkVerifies()
    {
        using var rsa = RSA.Create(2048);
        RSAParameters parameters = rsa.ExportParameters(includePrivateParameters: false);
        string jwk = $$"""{"kty":"RSA","n":"{{Base64Url(parameters.Modulus!)}}","e":"{{Base64Url(parameters.Exponent!)}}"}""";
        ServiceProvider provider = new ServiceCollection()
            .AddAsiBackboneKeySignatureVerification(options => options.AddJwkKey(
                "key-1", "v1", ManagedKeySignatureAlgorithms.RsaPssSha256, jwk))
            .BuildServiceProvider();

        SignatureVerificationResult result = await provider
            .GetRequiredService<IGovernanceSignatureVerificationService>()
            .VerifyAsync(CreateRequest(rsa, "v1"), TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    /// <summary>Verifies the allowed ECDSA P-256 algorithm with public PEM material.</summary>
    [Fact]
    public async Task EcdsaP256PublicPemVerifies()
    {
        using var ecdsa = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        byte[] input = "version-1-signature-input"u8.ToArray();
        string signature = Convert.ToBase64String(ecdsa.SignData(input, HashAlgorithmName.SHA256));
        var metadata = SigningMetadata.Create(
            signingHash: "hash-value",
            hashAlgorithm: "SHA-256",
            signature: signature,
            signatureAlgorithm: ManagedKeySignatureAlgorithms.EcdsaP256Sha256,
            keyId: "key-1",
            keyVersion: "v1",
            provider: ManagedKeySigningOptions.DefaultProviderName);
        var request = new SignatureVerificationRequest("hash-value", metadata) { SignatureInput = input };
        ServiceProvider provider = new ServiceCollection()
            .AddAsiBackboneKeySignatureVerification(options => options.AddPemKey(
                "key-1",
                "v1",
                ManagedKeySignatureAlgorithms.EcdsaP256Sha256,
                ecdsa.ExportSubjectPublicKeyInfoPem()))
            .BuildServiceProvider();

        SignatureVerificationResult result = await provider
            .GetRequiredService<IGovernanceSignatureVerificationService>()
            .VerifyAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
    }

    private static IGovernanceSignatureVerificationService CreateVerifier(
        params (string Version, string Pem, bool Revoked)[] keys)
    {
        ServiceProvider provider = new ServiceCollection()
            .AddAsiBackboneKeySignatureVerification(options =>
            {
                foreach ((string version, string pem, bool revoked) in keys)
                {
                    options.AddPemKey(
                        "key-1", version, ManagedKeySignatureAlgorithms.RsaPssSha256, pem, revoked);
                }
            })
            .BuildServiceProvider();
        return provider.GetRequiredService<IGovernanceSignatureVerificationService>();
    }

    private static SignatureVerificationRequest CreateRequest(RSA rsa, string version, string keyId = "key-1")
    {
        byte[] input = "version-1-signature-input"u8.ToArray();
        string signature = Convert.ToBase64String(
            rsa.SignData(input, HashAlgorithmName.SHA256, RSASignaturePadding.Pss));
        var metadata = SigningMetadata.Create(
            signingHash: "hash-value",
            hashAlgorithm: "SHA-256",
            signature: signature,
            signatureAlgorithm: ManagedKeySignatureAlgorithms.RsaPssSha256,
            keyId: keyId,
            keyVersion: version,
            provider: ManagedKeySigningOptions.DefaultProviderName);
        return new SignatureVerificationRequest("hash-value", metadata) { SignatureInput = input };
    }

    private static string Base64Url(byte[] value)
    {
        return Convert.ToBase64String(value).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }
}
