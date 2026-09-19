using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Line coverage tests for governance artifact signing helper overloads.
/// </summary>
public sealed class GovernanceArtifactSignerLineCoverageTests
{
    /// <summary>
    /// Covers unsigned, signing-ready, and signed decision receipt helper overloads.
    /// </summary>
    [Fact]
    public async Task DecisionReceiptHelpersCoverUnsignedSigningReadyAndSignedPaths()
    {
        DecisionReceipt receipt = CreateDecisionReceipt();

        SignedGovernanceArtifact<IDecisionReceipt> unsigned = GovernanceArtifactSigner.CreateUnsignedDecisionReceipt(receipt: receipt);
        SignedGovernanceArtifact<IDecisionReceipt> signingReady = GovernanceArtifactSigner.CreateSigningReadyDecisionReceipt(
            receipt: receipt,
            metadata: new Dictionary<string, string>
            {
                ["workflow"] = "audit-residue-ready"
            });
        SignedGovernanceArtifact<IDecisionReceipt> signed = await GovernanceArtifactSigner.SignDecisionReceiptAsync(
            receipt: receipt,
            signingService: new FakeSigningService(),
            keyId: "residue-key",
            keyVersion: "v1",
            metadata: new Dictionary<string, string>
            {
                ["workflow"] = "audit-residue-signing"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(unsigned.HasNoSignature);
        Assert.Equal(CanonicalArtifactTypes.AuditResidue, unsigned.ArtifactType);
        Assert.Equal("residue-1", unsigned.ArtifactId);
        Assert.Equal(CanonicalPayloadOptions.DefaultHashAlgorithm, unsigned.HashAlgorithm);

        Assert.True(signingReady.IsSigningReady);
        Assert.Equal(CanonicalArtifactTypes.AuditResidue, signingReady.SigningMetadata.Metadata["artifact_type"]);
        Assert.Equal("residue-1", signingReady.SigningMetadata.Metadata["artifact_id"]);
        Assert.Equal("audit-residue-ready", signingReady.SigningMetadata.Metadata["workflow"]);

        Assert.True(signed.IsSigned);
        Assert.Equal("residue-key", signed.SigningMetadata.KeyId);
        Assert.Equal("v1", signed.SigningMetadata.KeyVersion);
        Assert.Equal("audit-residue-signing", signed.SigningMetadata.Metadata["workflow"]);
    }

    /// <summary>
    /// Covers unsigned, signing-ready, and signed decision receipt lifecycle event helper overloads.
    /// </summary>
    [Fact]
    public async Task DecisionReceiptLifecycleEventHelpersCoverUnsignedSigningReadyAndSignedPaths()
    {
        DecisionReceiptLifecycleEvent lifecycleEvent = CreateLifecycleEvent();

        SignedGovernanceArtifact<DecisionReceiptLifecycleEvent> unsigned = GovernanceArtifactSigner.CreateUnsignedDecisionReceiptLifecycleEvent(lifecycleEvent);
        SignedGovernanceArtifact<DecisionReceiptLifecycleEvent> signingReady = GovernanceArtifactSigner.CreateSigningReadyDecisionReceiptLifecycleEvent(
            lifecycleEvent,
            metadata: new Dictionary<string, string>
            {
                ["workflow"] = "lifecycle-ready"
            });
        SignedGovernanceArtifact<DecisionReceiptLifecycleEvent> signed = await GovernanceArtifactSigner.SignDecisionReceiptLifecycleEventAsync(
            lifecycleEvent,
            new FakeSigningService(),
            keyId: "lifecycle-key",
            keyVersion: "v2",
            metadata: new Dictionary<string, string>
            {
                ["workflow"] = "lifecycle-signing"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(unsigned.HasNoSignature);
        Assert.Equal(CanonicalArtifactTypes.AuditResidueLifecycleEvent, unsigned.ArtifactType);
        Assert.Equal("lifecycle-1", unsigned.ArtifactId);

        Assert.True(signingReady.IsSigningReady);
        Assert.Equal(CanonicalArtifactTypes.AuditResidueLifecycleEvent, signingReady.SigningMetadata.Metadata["artifact_type"]);
        Assert.Equal("lifecycle-1", signingReady.SigningMetadata.Metadata["artifact_id"]);
        Assert.Equal("lifecycle-ready", signingReady.SigningMetadata.Metadata["workflow"]);

        Assert.True(signed.IsSigned);
        Assert.Equal("lifecycle-key", signed.SigningMetadata.KeyId);
        Assert.Equal("v2", signed.SigningMetadata.KeyVersion);
        Assert.Equal("lifecycle-signing", signed.SigningMetadata.Metadata["workflow"]);
    }

    /// <summary>
    /// Covers unsigned, signing-ready, and signed governance emission envelope helper overloads.
    /// </summary>
    [Fact]
    public async Task GovernanceEmissionEnvelopeHelpersCoverUnsignedSigningReadyAndSignedPaths()
    {
        GovernanceEmissionEnvelope envelope = CreateGovernanceEmissionEnvelope();

        SignedGovernanceArtifact<GovernanceEmissionEnvelope> unsigned = GovernanceArtifactSigner.CreateUnsignedGovernanceEmissionEnvelope(envelope);
        SignedGovernanceArtifact<GovernanceEmissionEnvelope> signingReady = GovernanceArtifactSigner.CreateSigningReadyGovernanceEmissionEnvelope(
            envelope,
            metadata: new Dictionary<string, string>
            {
                ["workflow"] = "envelope-ready"
            });
        SignedGovernanceArtifact<GovernanceEmissionEnvelope> signed = await GovernanceArtifactSigner.SignGovernanceEmissionEnvelopeAsync(
            envelope,
            new FakeSigningService(),
            keyId: "envelope-key",
            keyVersion: "v3",
            metadata: new Dictionary<string, string>
            {
                ["workflow"] = "envelope-signing"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(unsigned.HasNoSignature);
        Assert.Equal(CanonicalArtifactTypes.GovernanceEmissionEnvelope, unsigned.ArtifactType);
        Assert.Equal("envelope-1", unsigned.ArtifactId);

        Assert.True(signingReady.IsSigningReady);
        Assert.Equal(CanonicalArtifactTypes.GovernanceEmissionEnvelope, signingReady.SigningMetadata.Metadata["artifact_type"]);
        Assert.Equal("envelope-1", signingReady.SigningMetadata.Metadata["artifact_id"]);
        Assert.Equal("envelope-ready", signingReady.SigningMetadata.Metadata["workflow"]);

        Assert.True(signed.IsSigned);
        Assert.Equal("envelope-key", signed.SigningMetadata.KeyId);
        Assert.Equal("v3", signed.SigningMetadata.KeyVersion);
        Assert.Equal("envelope-signing", signed.SigningMetadata.Metadata["workflow"]);
    }

    /// <summary>
    /// Covers the signing-ready governance outbox entry helper overload.
    /// </summary>
    [Fact]
    public void CreateSigningReadyGovernanceOutboxEntryKeepsMetadataWithoutSignature()
    {
        GovernanceOutboxEntry entry = CreateGovernanceOutboxEntry();

        SignedGovernanceArtifact<GovernanceOutboxEntry> artifact = GovernanceArtifactSigner.CreateSigningReadyGovernanceOutboxEntry(
            entry,
            metadata: new Dictionary<string, string>
            {
                ["workflow"] = "outbox-ready"
            });

        Assert.True(artifact.IsSigningReady);
        Assert.False(artifact.IsSigned);
        Assert.Equal(CanonicalArtifactTypes.GovernanceOutboxEntry, artifact.ArtifactType);
        Assert.Equal("outbox-1", artifact.ArtifactId);
        Assert.Equal(CanonicalArtifactTypes.GovernanceOutboxEntry, artifact.SigningMetadata.Metadata["artifact_type"]);
        Assert.Equal("outbox-1", artifact.SigningMetadata.Metadata["artifact_id"]);
        Assert.Equal("outbox-ready", artifact.SigningMetadata.Metadata["workflow"]);
    }

    /// <summary>
    /// Covers direct signing request creation from canonical hash metadata.
    /// </summary>
    [Fact]
    public void CreateSigningRequestCopiesCanonicalHashAndMergesMetadata()
    {
        var canonicalHash = CanonicalPayloadHash.Create(
            CanonicalArtifactTypes.GovernanceEmissionEnvelope,
            "envelope-1",
            "v1",
            "canonical-v1",
            CanonicalPayloadOptions.DefaultHashAlgorithm,
            "ABC123");

        SigningRequest request = GovernanceArtifactSigner.CreateSigningRequest(
            canonicalHash,
            keyId: "key-1",
            keyVersion: "v1",
            metadata: new Dictionary<string, string>
            {
                [" workflow "] = " envelope-signing ",
                [" "] = "ignored"
            });

        Assert.Equal("abc123", request.SigningHash);
        Assert.Equal(CanonicalPayloadOptions.DefaultHashAlgorithm, request.HashAlgorithm);
        Assert.Equal(CanonicalArtifactTypes.GovernanceEmissionEnvelope, request.Purpose);
        Assert.Equal("key-1", request.KeyId);
        Assert.Equal("v1", request.KeyVersion);
        Assert.Equal(CanonicalArtifactTypes.GovernanceEmissionEnvelope, request.Metadata["artifact_type"]);
        Assert.Equal("envelope-1", request.Metadata["artifact_id"]);
        Assert.Equal("envelope-signing", request.Metadata["workflow"]);
        Assert.False(request.Metadata.ContainsKey(string.Empty));
    }

    private static DecisionReceipt CreateDecisionReceipt()
    {
        IGovernanceActorContext actor = GovernanceActorContext.Service("system-1", "System");

        return DecisionReceipt.Create(
            actor,
            "gateway.execute",
            "Allowed",
            reasonCodes: ["policy.allowed"],
            eventId: "event-1",
            occurredUtc: new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero),
            correlationId: "correlation-1",
            policyVersion: "policy-v1",
            policyHash: "policy-hash",
            auditResidueId: "residue-1");
    }

    private static DecisionReceiptLifecycleEvent CreateLifecycleEvent()
    {
        return DecisionReceiptLifecycleEvent.Create(
            DecisionReceiptLifecycleStage.ExternalEmissionQueued,
            "correlation-1",
            auditResidueId: "residue-1",
            eventId: "lifecycle-1",
            occurredUtc: new DateTimeOffset(2026, 6, 16, 12, 0, 3, TimeSpan.Zero),
            operationName: "gateway.emit",
            outcome: "Queued");
    }

    private static GovernanceEmissionEnvelope CreateGovernanceEmissionEnvelope()
    {
        var payload = GovernanceEmissionPayload.Create(
            "audit-summary",
            schemaVersion: "v1",
            contentType: "application/json",
            contentHash: "payload-hash",
            sizeBytes: 128);

        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditResidue,
            eventId: "event-1",
            occurredUtc: new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero),
            envelopeId: "envelope-1",
            createdUtc: new DateTimeOffset(2026, 6, 16, 12, 0, 1, TimeSpan.Zero),
            correlationId: "correlation-1",
            auditResidueId: "residue-1",
            policyVersion: "policy-v1",
            policyHash: "policy-hash",
            payload: payload);
    }

    private static GovernanceOutboxEntry CreateGovernanceOutboxEntry()
    {
        return GovernanceOutboxEntry.Create(
            CreateGovernanceEmissionEnvelope(),
            outboxEntryId: "outbox-1",
            createdUtc: new DateTimeOffset(2026, 6, 16, 12, 0, 2, TimeSpan.Zero));
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
                signedUtc: new DateTimeOffset(2026, 6, 16, 12, 30, 0, TimeSpan.Zero),
                metadata: request.Metadata);

            return ValueTask.FromResult(SigningResult.FromMetadata(metadata));
        }
    }
}
