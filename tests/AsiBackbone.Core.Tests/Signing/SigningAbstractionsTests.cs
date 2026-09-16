using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Tests for signing abstractions, including signing metadata, signing requests, signing results, and signature verification.
/// </summary>
public sealed class SigningAbstractionsTests
{
    /// <summary>
    /// Tests that the <see cref="SigningMetadata.NoSignature"/> instance has no signature or key reference, and that its properties are null or empty as expected.
    /// </summary>
    [Fact]
    public void NoSignatureMetadataHasNoSignatureOrKeyReference()
    {
        SigningMetadata metadata = SigningMetadata.NoSignature;

        Assert.False(metadata.HasSignature);
        Assert.False(metadata.HasKeyReference);
        Assert.False(metadata.IsSigned);
        Assert.Null(metadata.SigningHash);
        Assert.Null(metadata.KeyId);
        Assert.Null(metadata.KeyVersion);
        Assert.Empty(metadata.Metadata);
    }

    /// <summary>
    /// Tests that a <see cref="SigningRequest"/> instance normalizes its key references and metadata by trimming whitespace and ignoring empty keys, ensuring that the properties are set correctly.
    /// </summary>
    [Fact]
    public void SigningRequestNormalizesKeyReferencesAndMetadata()
    {
        var request = new SigningRequest(
            " hash-123 ",
            hashAlgorithm: " SHA-256 ",
            purpose: " audit-receipt ",
            keyId: " key-1 ",
            keyVersion: " v2 ",
            metadata: new Dictionary<string, string>
            {
                [" provider_hint "] = " local-dev ",
                [" "] = " ignored "
            });

        Assert.Equal("hash-123", request.SigningHash);
        Assert.Equal("SHA-256", request.HashAlgorithm);
        Assert.Equal("audit-receipt", request.Purpose);
        Assert.Equal("key-1", request.KeyId);
        Assert.Equal("v2", request.KeyVersion);
        Assert.True(request.HasMetadata);
        Assert.Equal("local-dev", request.Metadata["provider_hint"]);
        Assert.False(request.Metadata.ContainsKey(string.Empty));
    }

    /// <summary>
    /// Tests that a fake signing service can produce signing metadata that is provider-neutral and contains the expected values based on the input signing request.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of signing and verifying the signing metadata.
    /// </returns>
    [Fact]
    public async Task FakeSignerReturnsProviderNeutralSigningMetadata()
    {
        IGovernanceSigningService signer = new FakeSigningService();
        var request = new SigningRequest(
            "audit-hash-123",
            hashAlgorithm: "BLAKE3-test",
            purpose: "audit-ledger-record",
            keyId: "dev-key",
            keyVersion: "2026-06");

        SigningResult result = await signer.SignAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal("audit-hash-123", result.Metadata.SigningHash);
        Assert.Equal("BLAKE3-test", result.Metadata.HashAlgorithm);
        Assert.Equal("fake-signature:audit-hash-123", result.Metadata.Signature);
        Assert.Equal("FAKE-SIGNATURE-V1", result.Metadata.SignatureAlgorithm);
        Assert.Equal("dev-key", result.Metadata.KeyId);
        Assert.Equal("2026-06", result.Metadata.KeyVersion);
        Assert.Equal("fake-signer", result.Metadata.Provider);
    }

    /// <summary>
    /// Tests that a fake verification service can validate signing metadata that is provider-neutral and contains the expected values based on the input signing request.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of verifying the signing metadata.
    /// </returns>
    [Fact]
    public async Task FakeVerifierValidatesProviderNeutralSigningMetadata()
    {
        var metadata = SigningMetadata.Create(
            signingHash: "audit-hash-123",
            hashAlgorithm: "custom-hash",
            signature: "fake-signature:audit-hash-123",
            signatureAlgorithm: "custom-signature",
            keyId: "key-a",
            keyVersion: "v7",
            provider: "fake-signer");

        IGovernanceSignatureVerificationService verifier = new FakeVerificationService();
        var request = new SignatureVerificationRequest("audit-hash-123", metadata);

        SignatureVerificationResult result = await verifier.VerifyAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsValid);
        Assert.Equal("Verified", result.Status);
        Assert.Null(result.FailureCode);
    }

    /// <summary>
    /// Tests that an audit ledger record can carry signing metadata and a reference to a capability token, and that the properties are correctly set and normalized.
    /// </summary>
    [Fact]
    public void AuditLedgerRecordCarriesSigningMetadataAndCapabilityTokenReference()
    {
        var actor = GovernanceActorContext.Service("system-1", "System");
        DateTimeOffset signedUtc = new(2026, 6, 15, 9, 30, 0, TimeSpan.FromHours(-5));
        var residue = DecisionReceipt.Create(
            actor,
            "gateway.execute",
            "Allowed",
            eventId: "event-1",
            policyVersion: "policy-v1",
            policyHash: "policy-hash");

        var record = AuditLedgerRecord.FromResidue(
            residue,
            capabilityTokenId: " capability-token-1 ",
            recordHash: " record-hash ",
            signingHash: " signing-hash ",
            signatureKeyId: " key-id ",
            signatureKeyVersion: " key-version ",
            signatureAlgorithm: " signature-algorithm ",
            signatureValue: " signature-value ",
            signatureProvider: " signing-provider ",
            signedUtc: signedUtc);

        Assert.Equal("capability-token-1", record.CapabilityTokenId);
        Assert.Equal("record-hash", record.RecordHash);
        Assert.Equal("signing-hash", record.SigningHash);
        Assert.Equal("key-id", record.SignatureKeyId);
        Assert.Equal("key-version", record.SignatureKeyVersion);
        Assert.Equal("signature-algorithm", record.SignatureAlgorithm);
        Assert.Equal("signature-value", record.SignatureValue);
        Assert.Equal("signing-provider", record.SignatureProvider);
        Assert.Equal(new DateTimeOffset(2026, 6, 15, 14, 30, 0, TimeSpan.Zero), record.SignedUtc!.Value);
        Assert.True(record.SigningMetadata.IsSigned);
        Assert.Equal(record.SigningHash, record.SigningMetadata.SigningHash);
        Assert.Equal(record.SignatureKeyVersion, record.SigningMetadata.KeyVersion);
    }

    private sealed class FakeSigningService : IGovernanceSigningService
    {
        public ValueTask<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            var metadata = SigningMetadata.Create(
                signingHash: request.SigningHash,
                hashAlgorithm: request.HashAlgorithm,
                signature: $"fake-signature:{request.SigningHash}",
                signatureAlgorithm: "FAKE-SIGNATURE-V1",
                keyId: request.KeyId,
                keyVersion: request.KeyVersion,
                provider: "fake-signer",
                signedUtc: DateTimeOffset.UtcNow);

            return ValueTask.FromResult(SigningResult.FromMetadata(metadata));
        }
    }

    private sealed class FakeVerificationService : IGovernanceSignatureVerificationService
    {
        public ValueTask<SignatureVerificationResult> VerifyAsync(SignatureVerificationRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            SignatureVerificationResult result = request.SigningMetadata.Signature == $"fake-signature:{request.SigningHash}"
                ? SignatureVerificationResult.Verified()
                : SignatureVerificationResult.Failed("signature.invalid", "The fake signature did not match the expected value.");

            return ValueTask.FromResult(result);
        }
    }
}
