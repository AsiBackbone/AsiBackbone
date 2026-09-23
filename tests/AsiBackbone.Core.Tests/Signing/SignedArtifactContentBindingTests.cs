using System.Reflection;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Tests that verification binds a signed artifact's content to its signature, so a valid signature over a stored hash
/// cannot certify content that was modified after signing.
/// </summary>
/// <remarks>
/// A provider verifies a signature over a hash. These tests cover the step that establishes the hash is the hash of the
/// artifact being verified, rather than a value the artifact carries about itself.
/// </remarks>
public sealed class SignedArtifactContentBindingTests
{
    private static readonly DateTimeOffset IssuedUtc = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Verifies that an untampered artifact still passes the content-binding check and reaches the provider.
    /// </summary>
    [Fact]
    public async Task VerifyAsyncAllowsArtifactWhosePayloadHashesToTheSignedHash()
    {
        SignedGovernanceArtifact<CapabilityGrant> artifact = CreateSignedGrant(CreateGrant());
        var verifier = new AlwaysValidVerificationService();

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(artifact, verifier, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.ShouldAllow);
        Assert.True(verifier.WasCalled);
    }

    /// <summary>
    /// Verifies that the typed binding path accepts matching content and reaches the provider.
    /// </summary>
    [Fact]
    public async Task VerifyTypedAsyncAllowsArtifactThatRebuildsToTheSignedHash()
    {
        SignedGovernanceArtifact<CapabilityGrant> artifact = CreateSignedGrant(CreateGrant());
        var verifier = new AlwaysValidVerificationService();

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyTypedAsync(
            artifact,
            verifier,
            grant => CanonicalPayloadBuilder.ForCapabilityGrant(grant),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.ShouldAllow);
        Assert.True(verifier.WasCalled);
    }

    /// <summary>
    /// Verifies that an authentic retained payload, hash, and signature cannot authenticate a different typed artifact.
    /// </summary>
    [Fact]
    public async Task VerifyTypedAsyncDeniesTypedArtifactThatDiffersFromTheAuthenticPayload()
    {
        SignedGovernanceArtifact<CapabilityGrant> signed = CreateSignedGrant(CreateGrant());
        CapabilityGrant tamperedGrant = CreateGrant(audience: "gateway-2");
        SignedGovernanceArtifact<CapabilityGrant> tampered = SignedGovernanceArtifacts.FromSigningMetadata(
            tamperedGrant,
            signed.CanonicalPayload,
            signed.CanonicalHash,
            signed.SigningMetadata);
        var verifier = new AlwaysValidVerificationService();

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyTypedAsync(
            tampered,
            verifier,
            grant => CanonicalPayloadBuilder.ForCapabilityGrant(grant),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.ShouldAllow);
        Assert.Equal(VerificationPolicyAction.Deny, outcome.Action);
        Assert.Equal(SignatureVerificationCategory.HashMismatch, outcome.Category);
        Assert.Equal("signature.typed-artifact-mismatch", outcome.FailureCode);
        Assert.False(verifier.WasCalled);
    }

    /// <summary>
    /// Documents that the compatibility verification path authenticates only the retained canonical payload.
    /// </summary>
    [Fact]
    public async Task VerifyAsyncPayloadOnlyPathDoesNotBindTheSeparatelySuppliedTypedArtifact()
    {
        SignedGovernanceArtifact<CapabilityGrant> signed = CreateSignedGrant(CreateGrant());
        SignedGovernanceArtifact<CapabilityGrant> structurallyValid = SignedGovernanceArtifacts.FromSigningMetadata(
            CreateGrant(audience: "gateway-2"),
            signed.CanonicalPayload,
            signed.CanonicalHash,
            signed.SigningMetadata);
        var verifier = new AlwaysValidVerificationService();

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            structurallyValid,
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.ShouldAllow);
        Assert.True(verifier.WasCalled);
    }

    /// <summary>
    /// Covers the typed binding path with every first-party canonical builder used by the generic signed artifact wrapper.
    /// </summary>
    [Fact]
    public async Task VerifyTypedAsyncSupportsEveryFirstPartyCanonicalBuilder()
    {
        DecisionReceipt receipt = CreateDecisionReceipt();
        var ledgerRecord = AuditLedgerRecord.FromDecisionReceipt(
            receipt,
            recordId: "record-content-binding",
            recordedUtc: IssuedUtc.AddSeconds(1));
        var lifecycleEvent = DecisionReceiptLifecycleEvent.Create(
            DecisionReceiptLifecycleStage.ExternalEmissionQueued,
            "correlation-content-binding",
            decisionReceiptId: receipt.DecisionReceiptId,
            eventId: "lifecycle-content-binding",
            occurredUtc: IssuedUtc.AddSeconds(2));
        GovernanceEmissionEnvelope envelope = CreateEnvelope();
        var outboxEntry = GovernanceOutboxEntry.Create(
            envelope,
            outboxEntryId: "outbox-content-binding",
            createdUtc: IssuedUtc.AddSeconds(4));
        CapabilityGrant grant = CreateGrant();

        await AssertTypedBuilderAllowsAsync(receipt, value => CanonicalPayloadBuilder.ForDecisionReceipt(value));
        await AssertTypedBuilderAllowsAsync(ledgerRecord, value => CanonicalPayloadBuilder.ForAuditLedgerRecord(value));
        await AssertTypedBuilderAllowsAsync(lifecycleEvent, value => CanonicalPayloadBuilder.ForDecisionReceiptLifecycleEvent(value));
        await AssertTypedBuilderAllowsAsync(envelope, value => CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(value));
        await AssertTypedBuilderAllowsAsync(outboxEntry, value => CanonicalPayloadBuilder.ForGovernanceOutboxEntry(value));
        await AssertTypedBuilderAllowsAsync(grant, value => CanonicalPayloadBuilder.ForCapabilityGrant(value));
    }

    /// <summary>
    /// Verifies that content modified after signing is denied even though the stored hash and signature still agree.
    /// </summary>
    /// <remarks>
    /// This is the tampering shape the content-binding check exists to stop: an authentic hash and signature pair carried
    /// beside a payload that no longer hashes to that value.
    /// </remarks>
    [Fact]
    public async Task VerifyAsyncDeniesArtifactWhosePayloadWasReplacedAfterSigning()
    {
        SignedGovernanceArtifact<CapabilityGrant> signed = CreateSignedGrant(CreateGrant());

        CapabilityGrant tamperedGrant = CreateGrant(scopes: ["robotics.execute", "robotics.admin"]);
        SignedGovernanceArtifact<CapabilityGrant> tampered = SignedGovernanceArtifacts.FromSigningMetadata(
            tamperedGrant,
            CanonicalPayloadBuilder.ForCapabilityGrant(tamperedGrant),
            signed.CanonicalHash,
            signed.SigningMetadata);

        var verifier = new AlwaysValidVerificationService();

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(tampered, verifier, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.ShouldAllow);
        Assert.Equal(VerificationPolicyAction.Deny, outcome.Action);
        Assert.Equal(SignatureVerificationCategory.HashMismatch, outcome.Category);
        Assert.Equal("signature.hash-mismatch", outcome.FailureCode);
        Assert.False(verifier.WasCalled);
    }

    /// <summary>
    /// Verifies that a missing canonical descriptor is rejected rather than treated as a match.
    /// </summary>
    [Fact]
    public async Task VerifyAsyncDeniesArtifactMissingACanonicalDescriptor()
    {
        CapabilityGrant grant = CreateGrant();
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityGrant(grant);
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);

        var metadataWithoutArtifactType = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["artifact_id"] = hash.ArtifactId,
            ["canonicalization_version"] = hash.CanonicalizationVersion,
            ["payload_schema_version"] = hash.PayloadSchemaVersion
        };

        SignedGovernanceArtifact<CapabilityGrant> artifact = CreateUncheckedArtifact(
            grant,
            payload,
            hash,
            metadataWithoutArtifactType);

        var verifier = new AlwaysValidVerificationService();

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(artifact, verifier, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.ShouldAllow);
        Assert.Equal(VerificationPolicyAction.Deny, outcome.Action);
        Assert.Equal(SignatureVerificationCategory.CanonicalizationMismatch, outcome.Category);
        Assert.Equal("signature.canonicalization-mismatch", outcome.FailureCode);
        Assert.False(verifier.WasCalled);
    }

    /// <summary>
    /// Verifies that rehydrating a stored payload and hash that disagree is rejected at construction.
    /// </summary>
    [Fact]
    public void RehydrateRejectsAPayloadThatDoesNotHashToTheStoredHash()
    {
        SignedGovernanceArtifact<CapabilityGrant> signed = CreateSignedGrant(CreateGrant());
        CapabilityGrant tamperedGrant = CreateGrant(audience: "gateway-2");

        ArgumentException exception = Assert.Throws<ArgumentException>(() => SignedGovernanceArtifacts.Rehydrate(
            tamperedGrant,
            CanonicalPayloadBuilder.ForCapabilityGrant(tamperedGrant),
            signed.CanonicalHash,
            signed.SigningMetadata));

        Assert.Equal("canonicalHash", exception.ParamName);
    }

    /// <summary>
    /// Verifies that rehydrating a matching payload and hash succeeds.
    /// </summary>
    [Fact]
    public void RehydrateAcceptsAPayloadThatHashesToTheStoredHash()
    {
        CapabilityGrant grant = CreateGrant();
        SignedGovernanceArtifact<CapabilityGrant> signed = CreateSignedGrant(grant);

        SignedGovernanceArtifact<CapabilityGrant> rehydrated = SignedGovernanceArtifacts.Rehydrate(
            grant,
            CanonicalPayloadBuilder.ForCapabilityGrant(grant),
            signed.CanonicalHash,
            signed.SigningMetadata);

        Assert.Equal(signed.SigningHash, rehydrated.SigningHash);
    }

    private static CapabilityGrant CreateGrant(
        string tokenId = "grant-content-binding",
        string issuer = "issuer-1",
        string audience = "gateway-1",
        IEnumerable<string>? scopes = null)
    {
        return CapabilityGrant.Create(
            tokenId: tokenId,
            issuer: issuer,
            audience: audience,
            scopes: scopes ?? ["robotics.execute"],
            issuedUtc: IssuedUtc,
            expiresUtc: IssuedUtc.AddMinutes(10),
            policyVersion: "policy-v1",
            policyHash: "policy-hash");
    }

    private static DecisionReceipt CreateDecisionReceipt()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Service("system-content-binding", "System");

        return DecisionReceipt.Create(
            actor,
            "gateway.execute",
            "Allowed",
            reasonCodes: ["policy.allowed"],
            eventId: "event-content-binding",
            occurredUtc: IssuedUtc,
            correlationId: "correlation-content-binding",
            policyVersion: "policy-v1",
            policyHash: "policy-hash",
            decisionReceiptId: "receipt-content-binding");
    }

    private static GovernanceEmissionEnvelope CreateEnvelope()
    {
        var payload = GovernanceEmissionPayload.Create(
            "audit-summary",
            schemaVersion: "v1",
            contentType: "application/json",
            contentHash: "payload-hash",
            sizeBytes: 128);

        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.DecisionReceipt,
            eventId: "event-content-binding",
            occurredUtc: IssuedUtc,
            envelopeId: "envelope-content-binding",
            createdUtc: IssuedUtc.AddSeconds(3),
            correlationId: "correlation-content-binding",
            decisionReceiptId: "receipt-content-binding",
            policyVersion: "policy-v1",
            policyHash: "policy-hash",
            payload: payload);
    }

    private static async Task AssertTypedBuilderAllowsAsync<TArtifact>(
        TArtifact artifact,
        Func<TArtifact, CanonicalPayload> canonicalPayloadBuilder)
    {
        CanonicalPayload payload = canonicalPayloadBuilder(artifact);
        SignedGovernanceArtifact<TArtifact> signed = CreateSignedArtifact(artifact, payload);
        var verifier = new AlwaysValidVerificationService();

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyTypedAsync(
            signed,
            verifier,
            canonicalPayloadBuilder,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.ShouldAllow);
        Assert.True(verifier.WasCalled);
    }

    private static SignedGovernanceArtifact<CapabilityGrant> CreateSignedGrant(CapabilityGrant grant)
    {
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityGrant(grant);
        return CreateSignedArtifact(grant, payload);
    }

    private static SignedGovernanceArtifact<TArtifact> CreateSignedArtifact<TArtifact>(
        TArtifact artifact,
        CanonicalPayload payload)
    {
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);

        var signingMetadata = SigningMetadata.Create(
            signingHash: hash.HashValue,
            hashAlgorithm: hash.HashAlgorithm,
            signature: "fake-signature",
            signatureAlgorithm: "FAKE-SIGNATURE-V1",
            keyId: "key-1",
            keyVersion: "v1",
            provider: "fake-provider",
            signedUtc: IssuedUtc);

        return SignedGovernanceArtifacts.FromSigningMetadata(artifact, payload, hash, signingMetadata);
    }

    /// <summary>
    /// Builds a signed artifact directly, bypassing the factory merge that restores canonical descriptors.
    /// </summary>
    /// <remarks>
    /// The shipped factories always write the four canonical descriptors, so an artifact missing one cannot be produced
    /// through the public surface. Constructing it directly exercises the verifier's own guard for artifacts that reach it
    /// from outside those factories.
    /// </remarks>
    private static SignedGovernanceArtifact<CapabilityGrant> CreateUncheckedArtifact(
        CapabilityGrant grant,
        CanonicalPayload payload,
        CanonicalPayloadHash hash,
        IReadOnlyDictionary<string, string> metadata)
    {
        var signingMetadata = SigningMetadata.Create(
            signingHash: hash.HashValue,
            hashAlgorithm: hash.HashAlgorithm,
            signature: "fake-signature",
            signatureAlgorithm: "FAKE-SIGNATURE-V1",
            keyId: "key-1",
            keyVersion: "v1",
            provider: "fake-provider",
            signedUtc: IssuedUtc,
            metadata: metadata);

        ConstructorInfo constructor = typeof(SignedGovernanceArtifact<CapabilityGrant>).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(CapabilityGrant), typeof(CanonicalPayload), typeof(CanonicalPayloadHash), typeof(SigningMetadata)],
            modifiers: null)
            ?? throw new InvalidOperationException("SignedGovernanceArtifact constructor could not be located.");

        return (SignedGovernanceArtifact<CapabilityGrant>)constructor.Invoke(
        [
            grant,
            payload,
            hash,
            signingMetadata
        ]);
    }

    private sealed class AlwaysValidVerificationService : IGovernanceSignatureVerificationService
    {
        public bool WasCalled { get; private set; }

        public ValueTask<SignatureVerificationResult> VerifyAsync(
            SignatureVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;

            return ValueTask.FromResult(SignatureVerificationResult.Verified());
        }
    }
}
