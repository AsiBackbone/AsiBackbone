using System.Security.Cryptography;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Verifies with a real keyed signature that the signing policy context is covered by the signature, and that pre-6.0
/// hash-only signatures are accepted only on explicit opt-in and never satisfy a policy pin.
/// </summary>
public sealed class SignatureInputBindingTests
{
    private static readonly DateTimeOffset SignedAt = new(2026, 9, 18, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that an artifact signed with a policy context verifies and satisfies matching policy pins.
    /// </summary>
    [Fact]
    public async Task SignedPolicyContextVerifiesAndSatisfiesMatchingPins()
    {
        var service = new HmacSignatureService();
        SignedGovernanceArtifact<AuditLedgerRecord> artifact = await SignWithPolicyContextAsync(service);

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            service,
            context: VerificationPolicyContext.Create(expectedPolicyVersion: "policy-v1", expectedPolicyHash: "policy-hash-1"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.ShouldAllow);
        Assert.Equal(SignatureVerificationCategory.Valid, outcome.Category);
    }

    /// <summary>
    /// Verifies that relabeling the policy version after signing invalidates the signature even when the verifier pins
    /// nothing. Before 6.0 the signature covered the hash text only and the relabeled artifact verified.
    /// </summary>
    [Fact]
    public async Task RelabeledPolicyVersionFailsVerification()
    {
        var service = new HmacSignatureService();
        SignedGovernanceArtifact<AuditLedgerRecord> artifact = await SignWithPolicyContextAsync(service);
        SignedGovernanceArtifact<AuditLedgerRecord> relabeled = WithSigningMetadataValue(
            artifact,
            GovernanceSignatureInput.PolicyVersionMetadataKey,
            "policy-v2");

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            relabeled,
            service,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.ShouldAllow);
        Assert.Equal(SignatureVerificationCategory.InvalidSignature, outcome.Category);
        Assert.Equal(VerificationPolicyAction.Deny, outcome.Action);
    }

    /// <summary>
    /// Verifies that adding a policy label to an artifact signed without one invalidates the signature, so a pin cannot be
    /// satisfied by a label the signer never asserted.
    /// </summary>
    [Fact]
    public async Task PolicyLabelAddedAfterSigningFailsVerification()
    {
        var service = new HmacSignatureService();
        SignedGovernanceArtifact<AuditLedgerRecord> artifact = await GovernanceArtifactSigner.SignAuditLedgerRecordAsync(
            CreateAuditLedgerRecord(),
            service,
            cancellationToken: TestContext.Current.CancellationToken);
        SignedGovernanceArtifact<AuditLedgerRecord> labeled = WithSigningMetadataValue(
            artifact,
            GovernanceSignatureInput.PolicyVersionMetadataKey,
            "policy-v1");

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            labeled,
            service,
            context: VerificationPolicyContext.Create(expectedPolicyVersion: "policy-v1"),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.ShouldAllow);
        Assert.Equal(SignatureVerificationCategory.InvalidSignature, outcome.Category);
    }

    /// <summary>
    /// Verifies that the artifact signer restores the signed policy context when a provider alters it in returned metadata.
    /// </summary>
    [Fact]
    public async Task SignerRestoresSignedPolicyContextWhenProviderAltersIt()
    {
        var service = new HmacSignatureService(returnedPolicyVersionOverride: "provider-rewritten");
        SignedGovernanceArtifact<AuditLedgerRecord> artifact = await SignWithPolicyContextAsync(service);

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            service,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("policy-v1", artifact.SigningMetadata.Metadata[GovernanceSignatureInput.PolicyVersionMetadataKey]);
        Assert.True(outcome.ShouldAllow);
    }

    /// <summary>
    /// Verifies that a pre-6.0 hash-only signature is denied by default and the verifier makes no second attempt.
    /// </summary>
    [Fact]
    public async Task LegacySignatureIsDeniedByDefault()
    {
        var service = new HmacSignatureService(signLegacyInput: true);
        SignedGovernanceArtifact<AuditLedgerRecord> artifact = await GovernanceArtifactSigner.SignAuditLedgerRecordAsync(
            CreateAuditLedgerRecord(),
            service,
            cancellationToken: TestContext.Current.CancellationToken);

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            service,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.ShouldAllow);
        Assert.Equal(SignatureVerificationCategory.InvalidSignature, outcome.Category);
        Assert.Equal(1, service.VerificationCount);
    }

    /// <summary>
    /// Verifies that a pre-6.0 hash-only signature is accepted when the verification context explicitly opts in.
    /// </summary>
    [Fact]
    public async Task LegacySignatureIsAcceptedWhenExplicitlyAllowed()
    {
        var service = new HmacSignatureService(signLegacyInput: true);
        SignedGovernanceArtifact<AuditLedgerRecord> artifact = await GovernanceArtifactSigner.SignAuditLedgerRecordAsync(
            CreateAuditLedgerRecord(),
            service,
            cancellationToken: TestContext.Current.CancellationToken);

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            service,
            context: VerificationPolicyContext.Default.WithLegacySignatureInputAllowed(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.ShouldAllow);
        Assert.Equal(2, service.VerificationCount);
    }

    /// <summary>
    /// Verifies that an accepted pre-6.0 signature cannot satisfy a policy pin, because its policy labels were never
    /// covered by the signature.
    /// </summary>
    [Fact]
    public async Task AllowedLegacySignatureCannotSatisfyPolicyPin()
    {
        var service = new HmacSignatureService(signLegacyInput: true);
        SignedGovernanceArtifact<AuditLedgerRecord> artifact = await SignWithPolicyContextAsync(service);

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            service,
            context: VerificationPolicyContext.Create(expectedPolicyVersion: "policy-v1").WithLegacySignatureInputAllowed(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.ShouldAllow);
        Assert.Equal(SignatureVerificationCategory.UntrustedSigningContext, outcome.Category);
        Assert.Equal(VerificationPolicyAction.Deny, outcome.Action);
        Assert.Equal("signature.policy-context-not-authenticated", outcome.FailureCode);
    }

    /// <summary>
    /// Verifies that opting into legacy input preserves every other context expectation.
    /// </summary>
    [Fact]
    public void WithLegacySignatureInputAllowedPreservesContextExpectations()
    {
        var context = VerificationPolicyContext.Create(
            purpose: "purpose-1",
            expectedKeyId: "key-1",
            expectedKeyVersion: "v1",
            expectedPolicyVersion: "policy-v1",
            expectedPolicyHash: "policy-hash-1",
            requiredProvider: "provider-1",
            requiredHashAlgorithm: "SHA-256",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal) { ["k"] = "v" });

        VerificationPolicyContext legacyContext = context.WithLegacySignatureInputAllowed();

        Assert.False(context.AllowLegacySignatureInput);
        Assert.False(VerificationPolicyContext.Default.AllowLegacySignatureInput);
        Assert.True(legacyContext.AllowLegacySignatureInput);
        Assert.Equal(context.Purpose, legacyContext.Purpose);
        Assert.Equal(context.ExpectedKeyId, legacyContext.ExpectedKeyId);
        Assert.Equal(context.ExpectedKeyVersion, legacyContext.ExpectedKeyVersion);
        Assert.Equal(context.ExpectedPolicyVersion, legacyContext.ExpectedPolicyVersion);
        Assert.Equal(context.ExpectedPolicyHash, legacyContext.ExpectedPolicyHash);
        Assert.Equal(context.RequiredProvider, legacyContext.RequiredProvider);
        Assert.Equal(context.RequiredHashAlgorithm, legacyContext.RequiredHashAlgorithm);
        Assert.Equal("v", legacyContext.Metadata["k"]);
    }

    private static ValueTask<SignedGovernanceArtifact<AuditLedgerRecord>> SignWithPolicyContextAsync(
        IGovernanceSigningService service)
    {
        return GovernanceArtifactSigner.SignAuditLedgerRecordAsync(
            CreateAuditLedgerRecord(),
            service,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GovernanceSignatureInput.PolicyVersionMetadataKey] = "policy-v1",
                [GovernanceSignatureInput.PolicyHashMetadataKey] = "policy-hash-1"
            },
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static SignedGovernanceArtifact<AuditLedgerRecord> WithSigningMetadataValue(
        SignedGovernanceArtifact<AuditLedgerRecord> artifact,
        string key,
        string value)
    {
        SigningMetadata original = artifact.SigningMetadata;
        Dictionary<string, string> metadata = new(original.Metadata, StringComparer.Ordinal)
        {
            [key] = value
        };

        var relabeled = SigningMetadata.Create(
            signingHash: original.SigningHash,
            hashAlgorithm: original.HashAlgorithm,
            signature: original.Signature,
            signatureAlgorithm: original.SignatureAlgorithm,
            keyId: original.KeyId,
            keyVersion: original.KeyVersion,
            provider: original.Provider,
            signedUtc: original.SignedUtc,
            metadata: metadata);

        return SignedGovernanceArtifacts.FromSigningMetadata(
            artifact.Artifact,
            artifact.CanonicalPayload,
            artifact.CanonicalHash,
            relabeled);
    }

    private static AuditLedgerRecord CreateAuditLedgerRecord()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Service("system-1", "System");
        var residue = DecisionReceipt.Create(
            actor,
            "gateway.execute",
            "Allowed",
            reasonCodes: ["policy.allowed"],
            eventId: "event-1",
            occurredUtc: new DateTimeOffset(2026, 9, 18, 11, 0, 0, TimeSpan.Zero),
            correlationId: "correlation-1",
            policyVersion: "policy-v1",
            policyHash: "policy-hash-1",
            auditResidueId: "residue-1");

        return AuditLedgerRecord.FromDecisionReceipt(
            residue,
            recordId: "record-1",
            recordedUtc: new DateTimeOffset(2026, 9, 18, 11, 0, 1, TimeSpan.Zero));
    }

    /// <summary>
    /// Signs and verifies with a keyed HMAC over the exact signature input, so a relabeled input genuinely fails.
    /// </summary>
    private sealed class HmacSignatureService(
        bool signLegacyInput = false,
        string? returnedPolicyVersionOverride = null) : IGovernanceSigningService, IGovernanceSignatureVerificationService
    {
        private static readonly byte[] Key = [0x41, 0x53, 0x49, 0x42, 0x2D, 0x74, 0x65, 0x73, 0x74, 0x2D, 0x6B, 0x65, 0x79, 0x2D, 0x30, 0x31];

        public int VerificationCount { get; private set; }

        public ValueTask<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            ReadOnlyMemory<byte> input = signLegacyInput
                ? GovernanceSignatureInput.CreateLegacy(request.SigningHash)
                : request.SignatureInput;
            Dictionary<string, string> metadata = new(request.Metadata, StringComparer.Ordinal);

            if (returnedPolicyVersionOverride is not null)
            {
                metadata[GovernanceSignatureInput.PolicyVersionMetadataKey] = returnedPolicyVersionOverride;
            }

            var signingMetadata = SigningMetadata.Create(
                signingHash: request.SigningHash,
                hashAlgorithm: request.HashAlgorithm,
                signature: Convert.ToBase64String(HMACSHA256.HashData(Key, input.Span)),
                signatureAlgorithm: "HMAC-SHA256-TEST",
                keyId: "test-key",
                keyVersion: "v1",
                provider: "test-provider",
                signedUtc: SignedAt,
                metadata: metadata);

            return ValueTask.FromResult(SigningResult.FromMetadata(signingMetadata));
        }

        public ValueTask<SignatureVerificationResult> VerifyAsync(
            SignatureVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            VerificationCount++;

            byte[] expected = HMACSHA256.HashData(Key, request.SignatureInput.Span);
            byte[] actual = Convert.FromBase64String(request.SigningMetadata.Signature ?? string.Empty);

            return ValueTask.FromResult(CryptographicOperations.FixedTimeEquals(expected, actual)
                ? SignatureVerificationResult.Verified()
                : SignatureVerificationResult.Failed(
                    "signature.invalid",
                    SignatureVerificationCategory.InvalidSignature,
                    "The keyed signature does not match the signature input."));
        }
    }
}
