using System.Security.Cryptography;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Signing.LocalDevelopment.Tests;

/// <summary>
/// Tests for the configured-key mode of <see cref="LocalDevelopmentSigningService" />, selected by
/// <see cref="LocalDevelopmentSigningOptions.RsaKeyPem" />.
/// </summary>
public sealed class LocalDevelopmentSigningConfiguredKeyTests
{
    /// <summary>
    /// Verifies two providers configured with the same key verify each other's signatures, as a restart or a second
    /// process would need.
    /// </summary>
    [Fact]
    public async Task ProvidersConfiguredWithSameKeyVerifyEachOthersSignatures()
    {
        string pem = CreatePrivateKeyPem();
        using var issuer = new LocalDevelopmentSigningService(CreateOptions(pem));
        using var verifier = new LocalDevelopmentSigningService(CreateOptions(pem));
        CanonicalPayloadHash hash = CreateHash();

        SigningResult signed = await issuer.SignAsync(
            GovernanceArtifactSigner.CreateSigningRequest(hash),
            TestContext.Current.CancellationToken);
        SignatureVerificationResult verified = await verifier.VerifyAsync(
            CreateVerificationRequest(hash, signed.Metadata),
            TestContext.Current.CancellationToken);

        Assert.True(signed.IsSigned);
        Assert.True(verified.IsValid, verified.FailureCode);
    }

    /// <summary>
    /// Verifies a provider generating its own key cannot verify a signature from a configured-key provider.
    /// </summary>
    [Fact]
    public async Task GeneratedKeyProviderDoesNotVerifyConfiguredKeySignature()
    {
        using var issuer = new LocalDevelopmentSigningService(CreateOptions(CreatePrivateKeyPem()));
        using var verifier = new LocalDevelopmentSigningService(CreateOptions(rsaKeyPem: null));
        CanonicalPayloadHash hash = CreateHash();

        SigningResult signed = await issuer.SignAsync(
            GovernanceArtifactSigner.CreateSigningRequest(hash),
            TestContext.Current.CancellationToken);
        SignatureVerificationResult verified = await verifier.VerifyAsync(
            CreateVerificationRequest(hash, signed.Metadata),
            TestContext.Current.CancellationToken);

        Assert.False(verified.IsValid);
        Assert.Equal(SignatureVerificationCategory.InvalidSignature, verified.Category);
    }

    /// <summary>
    /// Verifies a provider configured with only the public key verifies signatures but cannot sign.
    /// </summary>
    [Fact]
    public async Task PublicKeyOnlyProviderVerifiesButDoesNotSign()
    {
        using var rsa = RSA.Create(LocalDevelopmentSigningOptions.MinimumKeySizeBits);
        using var issuer = new LocalDevelopmentSigningService(CreateOptions(rsa.ExportRSAPrivateKeyPem()));
        using var verifier = new LocalDevelopmentSigningService(CreateOptions(rsa.ExportSubjectPublicKeyInfoPem()));
        CanonicalPayloadHash hash = CreateHash();

        SigningResult signed = await issuer.SignAsync(
            GovernanceArtifactSigner.CreateSigningRequest(hash),
            TestContext.Current.CancellationToken);
        SignatureVerificationResult verified = await verifier.VerifyAsync(
            CreateVerificationRequest(hash, signed.Metadata),
            TestContext.Current.CancellationToken);
        SigningResult verifierSigned = await verifier.SignAsync(
            GovernanceArtifactSigner.CreateSigningRequest(hash),
            TestContext.Current.CancellationToken);

        Assert.True(verified.IsValid, verified.FailureCode);
        Assert.False(verifierSigned.IsSigned);
        Assert.Equal("localdev.signing.failed", verifierSigned.Metadata.Metadata["failure_code"]);
    }

    /// <summary>
    /// Verifies an unreadable key fails construction without echoing the configured text.
    /// </summary>
    [Fact]
    public void UnreadableKeyFailsWithoutEchoingKeyText()
    {
        const string NotAKey = "-----BEGIN RSA PRIVATE KEY-----\nbm90LWEta2V5LXZhbHVl\n-----END RSA PRIVATE KEY-----";

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new LocalDevelopmentSigningService(CreateOptions(NotAKey)));
        InvalidOperationException noPem = Assert.Throws<InvalidOperationException>(
            () => new LocalDevelopmentSigningService(CreateOptions("not a pem document")));

        Assert.DoesNotContain("bm90LWEta2V5LXZhbHVl", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("not a pem document", noPem.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies a configured key smaller than the supported minimum is refused.
    /// </summary>
    [Fact]
    public void KeyBelowMinimumSizeIsRefused()
    {
        using var rsa = RSA.Create(1024);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new LocalDevelopmentSigningService(CreateOptions(rsa.ExportRSAPrivateKeyPem())));

        Assert.Contains("1024", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies the configured key is snapshotted, so a later change to the options does not change the provider key.
    /// </summary>
    [Fact]
    public async Task ConfiguredKeyIsSnapshotted()
    {
        string pem = CreatePrivateKeyPem();
        LocalDevelopmentSigningOptions options = CreateOptions(pem);
        using var issuer = new LocalDevelopmentSigningService(options);
        options.RsaKeyPem = CreatePrivateKeyPem();
        using var verifier = new LocalDevelopmentSigningService(CreateOptions(pem));
        CanonicalPayloadHash hash = CreateHash();

        SigningResult signed = await issuer.SignAsync(
            GovernanceArtifactSigner.CreateSigningRequest(hash),
            TestContext.Current.CancellationToken);
        SignatureVerificationResult verified = await verifier.VerifyAsync(
            CreateVerificationRequest(hash, signed.Metadata),
            TestContext.Current.CancellationToken);

        Assert.True(verified.IsValid, verified.FailureCode);
    }

    private static LocalDevelopmentSigningOptions CreateOptions(string? rsaKeyPem)
    {
        var options = LocalDevelopmentSigningOptions.Create(keyId: "configured-key", keyVersion: "v1");
        options.RsaKeyPem = rsaKeyPem;
        return options;
    }

    private static string CreatePrivateKeyPem()
    {
        using var rsa = RSA.Create(LocalDevelopmentSigningOptions.MinimumKeySizeBits);
        return rsa.ExportPkcs8PrivateKeyPem();
    }

    private static CanonicalPayloadHash CreateHash()
    {
        return CanonicalPayloadHash.Create(
            CanonicalArtifactTypes.CapabilityGrant,
            "grant-1",
            "2.0.0",
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            CanonicalPayloadOptions.DefaultHashAlgorithm,
            "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef");
    }

    private static SignatureVerificationRequest CreateVerificationRequest(CanonicalPayloadHash hash, SigningMetadata metadata)
    {
        return new SignatureVerificationRequest(hash.HashValue, metadata, purpose: hash.ArtifactType)
        {
            SignatureInput = GovernanceSignatureInput.CreateV1(hash, metadata.Metadata)
        };
    }
}
