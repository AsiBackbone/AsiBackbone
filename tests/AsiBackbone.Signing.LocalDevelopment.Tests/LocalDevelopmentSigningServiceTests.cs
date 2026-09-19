using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Signing.LocalDevelopment.Tests;

/// <summary>
/// Tests for the <see cref="LocalDevelopmentSigningService"/> class, which provides signing and verification functionality for local development scenarios.
/// </summary>
public sealed class LocalDevelopmentSigningServiceTests
{
    /// <summary>
    /// Tests that the <see cref="LocalDevelopmentSigningService.SignAsync(SigningRequest, CancellationToken)"/> method returns signing metadata that is provider-neutral and contains the expected values.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of signing a request and verifying the returned metadata.
    /// </returns>
    [Fact]
    public async Task SignAsyncReturnsProviderNeutralSigningMetadata()
    {
        using var service = new LocalDevelopmentSigningService(LocalDevelopmentSigningOptions.Create(
            keyId: "test-key",
            keyVersion: "v1"));
        var request = new SigningRequest(
            "abc123",
            hashAlgorithm: "SHA-256",
            purpose: "audit-ledger-record",
            keyId: "test-key",
            keyVersion: "v1");

        SigningResult result = await service.SignAsync(request, TestContext.Current.CancellationToken);

        Assert.True(result.IsSigned);
        Assert.Equal("abc123", result.Metadata.SigningHash);
        Assert.Equal("SHA-256", result.Metadata.HashAlgorithm);
        Assert.NotNull(result.Metadata.Signature);
        Assert.Equal(LocalDevelopmentSigningOptions.DefaultSignatureAlgorithm, result.Metadata.SignatureAlgorithm);
        Assert.Equal("test-key", result.Metadata.KeyId);
        Assert.Equal("v1", result.Metadata.KeyVersion);
        Assert.Equal(LocalDevelopmentSigningOptions.DefaultProviderName, result.Metadata.Provider);
        Assert.True(result.Metadata.SignedUtc.HasValue);
        Assert.Equal("signed", result.Metadata.Metadata["signing_status"]);
        Assert.Equal("local-development-only", result.Metadata.Metadata["provider_warning"]);
    }

    /// <summary>
    /// Tests that the <see cref="LocalDevelopmentSigningService.VerifyAsync(SignatureVerificationRequest, CancellationToken)"/> method correctly validates a signature produced by the same provider instance.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of signing a request and verifying the returned signature.
    /// </returns>
    [Fact]
    public async Task VerifyAsyncValidatesSignatureProducedBySameProviderInstance()
    {
        using var service = new LocalDevelopmentSigningService(LocalDevelopmentSigningOptions.Create(
            keyId: "test-key",
            keyVersion: "v1"));
        var signingRequest = new SigningRequest(
            "def456",
            hashAlgorithm: "SHA-256",
            purpose: "audit-ledger-record",
            keyId: "test-key",
            keyVersion: "v1");
        SigningResult signingResult = await service.SignAsync(signingRequest, TestContext.Current.CancellationToken);

        var verificationRequest = new SignatureVerificationRequest(
            "def456",
            signingResult.Metadata,
            purpose: "audit-ledger-record");
        SignatureVerificationResult verificationResult = await service.VerifyAsync(verificationRequest, TestContext.Current.CancellationToken);

        Assert.True(verificationResult.IsValid);
        Assert.Equal("Verified", verificationResult.Status);
        Assert.Equal(SignatureVerificationCategory.Valid, verificationResult.Category);
        Assert.Null(verificationResult.FailureCode);
    }

    /// <summary>
    /// Tests that the <see cref="LocalDevelopmentSigningService.VerifyAsync(SignatureVerificationRequest, CancellationToken)"/> method fails when the signing hash does not match the expected value, even if the hash lengths are the same.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of signing a request and verifying the returned signature with a tampered hash.
    /// </returns>
    [Fact]
    public async Task VerifyAsyncFailsForSameLengthHashMismatch()
    {
        using var service = new LocalDevelopmentSigningService();
        SigningResult signingResult = await service.SignAsync(
            new SigningRequest("expected-hash", hashAlgorithm: "SHA-256"),
            TestContext.Current.CancellationToken);

        SignatureVerificationResult verificationResult = await service.VerifyAsync(
            new SignatureVerificationRequest("tampered-hash", signingResult.Metadata),
            TestContext.Current.CancellationToken);

        Assert.False(verificationResult.IsValid);
        Assert.Equal("localdev.signature.hash-mismatch", verificationResult.FailureCode);
        Assert.Equal(SignatureVerificationCategory.HashMismatch, verificationResult.Category);
    }

    /// <summary>
    /// Tests that the <see cref="LocalDevelopmentSigningService.SignAsync(SigningRequest, CancellationToken)"/> method returns an unsigned failure metadata when an unsupported hash algorithm is specified in the signing request.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of signing a request with an unsupported hash algorithm and verifying that the returned metadata indicates failure.
    /// </returns>
    [Fact]
    public async Task SignAsyncReturnsUnsignedFailureMetadataForUnsupportedAlgorithm()
    {
        using var service = new LocalDevelopmentSigningService();
        var request = new SigningRequest(
            "abc123",
            hashAlgorithm: "SHA-512",
            purpose: "audit-ledger-record");

        SigningResult result = await service.SignAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.IsSigned);
        Assert.Null(result.Metadata.Signature);
        Assert.Equal("failed", result.Metadata.Metadata["signing_status"]);
        Assert.Equal("localdev.signing.hash-algorithm-unsupported", result.Metadata.Metadata["failure_code"]);
    }

    /// <summary>
    /// Tests that a canonical audit ledger record can be signed and verified end-to-end using the <see cref="LocalDevelopmentSigningService"/>. This test creates an audit residue, converts it to an audit ledger record, computes its canonical payload hash, signs it, and then verifies the signature.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of signing and verifying a canonical audit ledger record end-to-end.
    /// </returns>
    [Fact]
    public async Task CanonicalAuditLedgerRecordCanBeSignedAndVerifiedEndToEnd()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Service("system-1", "System");
        var residue = DecisionReceipt.Create(
            actor,
            "sample.governed-operation",
            "Allowed",
            reasonCodes: ["sample.allowed"],
            eventId: "event-1",
            occurredUtc: new DateTimeOffset(2026, 6, 16, 10, 0, 0, TimeSpan.Zero),
            correlationId: "correlation-1",
            policyVersion: "policy-v1",
            policyHash: "policy-hash");
        var record = AuditLedgerRecord.FromDecisionReceipt(
            residue,
            recordId: "record-1",
            recordedUtc: new DateTimeOffset(2026, 6, 16, 10, 0, 1, TimeSpan.Zero));
        CanonicalPayload payload = CanonicalPayloadBuilder.ForAuditLedgerRecord(record);
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);

        using var service = new LocalDevelopmentSigningService(LocalDevelopmentSigningOptions.Create(
            keyId: "test-key",
            keyVersion: "v1"));
        SigningResult signingResult = await service.SignAsync(
            new SigningRequest(
                hash.HashValue,
                hash.HashAlgorithm,
                purpose: CanonicalArtifactTypes.AuditLedgerRecord,
                keyId: "test-key",
                keyVersion: "v1",
                metadata: hash.ToSigningMetadata().Metadata),
            TestContext.Current.CancellationToken);
        SignatureVerificationResult verificationResult = await service.VerifyAsync(
            new SignatureVerificationRequest(
                hash.HashValue,
                signingResult.Metadata,
                purpose: CanonicalArtifactTypes.AuditLedgerRecord),
            TestContext.Current.CancellationToken);

        Assert.True(signingResult.IsSigned);
        Assert.True(verificationResult.IsValid);
        Assert.Equal(hash.HashValue, signingResult.Metadata.SigningHash);
        Assert.Equal(CanonicalArtifactTypes.AuditLedgerRecord, signingResult.Metadata.Metadata["artifact_type"]);
    }

    /// <summary>
    /// Verifies that the provider signs the supplied signature input rather than the hash text, so a signature over the
    /// version 1 input does not verify against the pre-6.0 hash-only input.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task SignAsyncSignsTheSuppliedSignatureInput()
    {
        using var service = new LocalDevelopmentSigningService();
        CanonicalPayloadHash hash = CreateCanonicalHash();
        SigningRequest signingRequest = GovernanceArtifactSigner.CreateSigningRequest(hash);
        SigningResult signingResult = await service.SignAsync(signingRequest, TestContext.Current.CancellationToken);

        SignatureVerificationResult versionOneResult = await service.VerifyAsync(
            new SignatureVerificationRequest(hash.HashValue, signingResult.Metadata)
            {
                SignatureInput = GovernanceSignatureInput.CreateV1(hash, signingResult.Metadata.Metadata)
            },
            TestContext.Current.CancellationToken);
        SignatureVerificationResult legacyResult = await service.VerifyAsync(
            new SignatureVerificationRequest(hash.HashValue, signingResult.Metadata),
            TestContext.Current.CancellationToken);

        Assert.True(versionOneResult.IsValid);
        Assert.False(legacyResult.IsValid);
        Assert.Equal("localdev.signature.invalid", legacyResult.FailureCode);
    }

    /// <summary>
    /// Verifies end to end with real RSA-PSS signatures that the signing policy context is covered by the signature: an
    /// artifact relabeled after signing is denied even when the verifier pins nothing.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task RelabeledSigningPolicyContextFailsEndToEndVerification()
    {
        using var service = new LocalDevelopmentSigningService();
        SignedGovernanceArtifact<AuditLedgerRecord> artifact = await GovernanceArtifactSigner.SignAuditLedgerRecordAsync(
            CreateAuditLedgerRecord(),
            service,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GovernanceSignatureInput.PolicyVersionMetadataKey] = "policy-v1"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        VerificationPolicyOutcome original = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            service,
            context: VerificationPolicyContext.Create(expectedPolicyVersion: "policy-v1"),
            cancellationToken: TestContext.Current.CancellationToken);
        VerificationPolicyOutcome relabeled = await GovernanceArtifactVerifier.VerifyAsync(
            WithSigningMetadata(artifact, artifact.SigningMetadata.Provider, GovernanceSignatureInput.PolicyVersionMetadataKey, "policy-v2"),
            service,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(original.ShouldAllow);
        Assert.False(relabeled.ShouldAllow);
        Assert.Equal(SignatureVerificationCategory.InvalidSignature, relabeled.Category);
    }

    /// <summary>
    /// Verifies that the verifier refuses a provider label it does not own. The provider label is not part of the
    /// signature input, so this check is what authenticates it.
    /// </summary>
    /// <returns>A task that represents the asynchronous test.</returns>
    [Fact]
    public async Task VerifyAsyncRejectsForeignProviderLabel()
    {
        using var service = new LocalDevelopmentSigningService();
        SignedGovernanceArtifact<AuditLedgerRecord> artifact = await GovernanceArtifactSigner.SignAuditLedgerRecordAsync(
            CreateAuditLedgerRecord(),
            service,
            cancellationToken: TestContext.Current.CancellationToken);

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            WithSigningMetadata(artifact, "other-provider", "workflow", "relabeled"),
            service,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.ShouldAllow);
        Assert.Equal(SignatureVerificationCategory.UntrustedSigningContext, outcome.Category);
        Assert.Equal(VerificationPolicyAction.Deny, outcome.Action);
        Assert.Equal("localdev.signature.provider-not-trusted", outcome.FailureCode);
    }

    private static CanonicalPayloadHash CreateCanonicalHash()
    {
        return CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForAuditLedgerRecord(CreateAuditLedgerRecord()));
    }

    private static AuditLedgerRecord CreateAuditLedgerRecord()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Service("system-1", "System");
        var residue = DecisionReceipt.Create(
            actor,
            "sample.governed-operation",
            "Allowed",
            reasonCodes: ["sample.allowed"],
            eventId: "event-1",
            occurredUtc: new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero),
            correlationId: "correlation-1",
            policyVersion: "policy-v1",
            policyHash: "policy-hash");

        return AuditLedgerRecord.FromDecisionReceipt(
            residue,
            recordId: "record-1",
            recordedUtc: new DateTimeOffset(2026, 9, 18, 10, 0, 1, TimeSpan.Zero));
    }

    private static SignedGovernanceArtifact<AuditLedgerRecord> WithSigningMetadata(
        SignedGovernanceArtifact<AuditLedgerRecord> artifact,
        string? provider,
        string key,
        string value)
    {
        SigningMetadata original = artifact.SigningMetadata;
        Dictionary<string, string> metadata = new(original.Metadata, StringComparer.Ordinal)
        {
            [key] = value
        };

        var changed = SigningMetadata.Create(
            signingHash: original.SigningHash,
            hashAlgorithm: original.HashAlgorithm,
            signature: original.Signature,
            signatureAlgorithm: original.SignatureAlgorithm,
            keyId: original.KeyId,
            keyVersion: original.KeyVersion,
            provider: provider,
            signedUtc: original.SignedUtc,
            metadata: metadata);

        return SignedGovernanceArtifacts.FromSigningMetadata(
            artifact.Artifact,
            artifact.CanonicalPayload,
            artifact.CanonicalHash,
            changed);
    }
}
