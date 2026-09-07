using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Tests for the <see cref="GovernanceArtifactSigner"/> class, which provides methods for creating and signing governance artifacts such as AuditLedgerRecords and GovernanceOutboxEntries.
/// </summary>
public sealed class GovernanceArtifactSignerTests
{
    /// <summary>
    /// Tests that creating a signing-ready AuditLedgerRecord preserves the hash metadata without including a signature.
    /// </summary>
    [Fact]
    public void CreateSigningReadyAuditLedgerRecordPreservesHashMetadataWithoutSignature()
    {
        AuditLedgerRecord record = CreateAuditLedgerRecord();

        SignedGovernanceArtifact<AuditLedgerRecord> artifact = GovernanceArtifactSigner.CreateSigningReadyAuditLedgerRecord(record);

        Assert.False(artifact.IsSigned);
        Assert.True(artifact.IsSigningReady);
        Assert.False(artifact.HasNoSignature);
        Assert.Equal(record, artifact.Artifact);
        Assert.Equal(CanonicalArtifactTypes.AuditLedgerRecord, artifact.ArtifactType);
        Assert.Equal("record-1", artifact.ArtifactId);
        Assert.Equal(CanonicalPayloadOptions.DefaultHashAlgorithm, artifact.HashAlgorithm);
        Assert.Equal(artifact.SigningHash, artifact.SigningMetadata.SigningHash);
        Assert.Equal(artifact.HashAlgorithm, artifact.SigningMetadata.HashAlgorithm);
        Assert.Equal(CanonicalArtifactTypes.AuditLedgerRecord, artifact.SigningMetadata.Metadata["artifact_type"]);
        Assert.Equal("record-1", artifact.SigningMetadata.Metadata["artifact_id"]);
        Assert.Null(artifact.SigningMetadata.Signature);
    }

    /// <summary>
    /// Tests that signing a GovernanceOutboxEntry preserves the provider metadata and canonical descriptors in the resulting SignedGovernanceArtifact.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of signing a GovernanceOutboxEntry and verifying the resulting SignedGovernanceArtifact.
    /// </returns>
    [Fact]
    public async Task SignGovernanceOutboxEntryAsyncPreservesProviderMetadataAndCanonicalDescriptors()
    {
        GovernanceOutboxEntry entry = CreateGovernanceOutboxEntry();
        IAsiBackboneSigningService signer = new FakeSigningService();

        SignedGovernanceArtifact<GovernanceOutboxEntry> artifact = await GovernanceArtifactSigner.SignGovernanceOutboxEntryAsync(
            entry,
            signer,
            keyId: "key-1",
            keyVersion: "v1",
            metadata: new Dictionary<string, string>
            {
                ["workflow"] = "outbox-signing"
            },
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(artifact.IsSigned);
        Assert.False(artifact.IsSigningReady);
        Assert.False(artifact.HasNoSignature);
        Assert.Equal(CanonicalArtifactTypes.GovernanceOutboxEntry, artifact.ArtifactType);
        Assert.Equal("outbox-1", artifact.ArtifactId);
        Assert.Equal(artifact.SigningHash, artifact.SigningMetadata.SigningHash);
        Assert.Equal(artifact.HashAlgorithm, artifact.SigningMetadata.HashAlgorithm);
        Assert.Equal("key-1", artifact.SigningMetadata.KeyId);
        Assert.Equal("v1", artifact.SigningMetadata.KeyVersion);
        Assert.Equal("fake-signer", artifact.SigningMetadata.Provider);
        Assert.Equal("FAKE-SIGNATURE-V1", artifact.SigningMetadata.SignatureAlgorithm);
        Assert.True(artifact.SigningMetadata.SignedUtc.HasValue);
        Assert.Equal(CanonicalArtifactTypes.GovernanceOutboxEntry, artifact.SigningMetadata.Metadata["artifact_type"]);
        Assert.Equal("outbox-1", artifact.SigningMetadata.Metadata["artifact_id"]);
        Assert.Equal("outbox-signing", artifact.SigningMetadata.Metadata["workflow"]);
    }

    /// <summary>
    /// Tests that creating an unsigned GovernanceOutboxEntry preserves the canonical hash without including any signing metadata.
    /// </summary>
    [Fact]
    public void CreateUnsignedGovernanceOutboxEntryKeepsCanonicalHashWithoutSigningMetadata()
    {
        GovernanceOutboxEntry entry = CreateGovernanceOutboxEntry();

        SignedGovernanceArtifact<GovernanceOutboxEntry> artifact = GovernanceArtifactSigner.CreateUnsignedGovernanceOutboxEntry(entry);

        Assert.True(artifact.HasNoSignature);
        Assert.False(artifact.IsSigningReady);
        Assert.False(artifact.IsSigned);
        Assert.Equal(CanonicalArtifactTypes.GovernanceOutboxEntry, artifact.ArtifactType);
        Assert.Equal("outbox-1", artifact.ArtifactId);
        Assert.Equal(CanonicalPayloadOptions.DefaultHashAlgorithm, artifact.HashAlgorithm);
        Assert.Null(artifact.SigningMetadata.SigningHash);
        Assert.Null(artifact.SigningMetadata.HashAlgorithm);
        Assert.Null(artifact.SigningMetadata.Signature);
        Assert.Empty(artifact.SigningMetadata.Metadata);
    }

    /// <summary>
    /// Tests that signing an AuditLedgerRecord with a failing signing service propagates the failure metadata correctly in the resulting SignedGovernanceArtifact.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of signing an AuditLedgerRecord with a failing signing service and verifying the resulting SignedGovernanceArtifact contains the expected failure metadata.
    /// </returns>
    [Fact]
    public async Task SignAuditLedgerRecordAsyncPropagatesUnsignedFailureMetadata()
    {
        AuditLedgerRecord record = CreateAuditLedgerRecord();
        IAsiBackboneSigningService signer = new FailingSigningService();

        SignedGovernanceArtifact<AuditLedgerRecord> artifact = await GovernanceArtifactSigner.SignAuditLedgerRecordAsync(
            record,
            signer,
            requireSignature: false,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(artifact.IsSigned);
        Assert.True(artifact.IsSigningReady);
        Assert.Equal(artifact.SigningHash, artifact.SigningMetadata.SigningHash);
        Assert.Equal("failed", artifact.SigningMetadata.Metadata["signing_status"]);
        Assert.Equal("fake.signing.failed", artifact.SigningMetadata.Metadata["failure_code"]);
        Assert.Equal(CanonicalArtifactTypes.AuditLedgerRecord, artifact.SigningMetadata.Metadata["artifact_type"]);
        Assert.Equal("record-1", artifact.SigningMetadata.Metadata["artifact_id"]);
    }

    /// <summary>
    /// Verifies that a provider returning no signature throws by default rather than yielding an unsigned artifact silently.
    /// </summary>
    /// <returns>A task representing the asynchronous test operation.</returns>
    [Fact]
    public async Task SignAuditLedgerRecordAsyncThrowsWhenTheProviderReturnsNoSignature()
    {
        AuditLedgerRecord record = CreateAuditLedgerRecord();
        IAsiBackboneSigningService signer = new FailingSigningService();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await GovernanceArtifactSigner.SignAuditLedgerRecordAsync(
                record,
                signer,
                cancellationToken: TestContext.Current.CancellationToken));
    }

    private static AuditLedgerRecord CreateAuditLedgerRecord()
    {
        IAsiBackboneActorContext actor = AsiBackboneActorContext.Service("system-1", "System");
        var residue = AuditResidue.Create(
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

        return AuditLedgerRecord.FromResidue(
            residue,
            recordId: "record-1",
            recordedUtc: new DateTimeOffset(2026, 6, 16, 12, 0, 1, TimeSpan.Zero),
            handshakeId: "handshake-1",
            acknowledgmentId: "ack-1",
            capabilityTokenId: "capability-1");
    }

    private static GovernanceOutboxEntry CreateGovernanceOutboxEntry()
    {
        var payload = GovernanceEmissionPayload.Create(
            "audit-summary",
            schemaVersion: "v1",
            contentType: "application/json",
            contentHash: "payload-hash",
            sizeBytes: 128);
        var envelope = GovernanceEmissionEnvelope.Create(
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

        return GovernanceOutboxEntry.Create(
            envelope,
            outboxEntryId: "outbox-1",
            createdUtc: new DateTimeOffset(2026, 6, 16, 12, 0, 2, TimeSpan.Zero));
    }

    private sealed class FakeSigningService : IAsiBackboneSigningService
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

    private sealed class FailingSigningService : IAsiBackboneSigningService
    {
        public ValueTask<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();

            Dictionary<string, string> metadata = new(StringComparer.Ordinal);

            foreach (KeyValuePair<string, string> item in request.Metadata)
            {
                metadata[item.Key] = item.Value;
            }

            metadata["signing_status"] = "failed";
            metadata["failure_code"] = "fake.signing.failed";

            return ValueTask.FromResult(SigningResult.FromMetadata(SigningMetadata.Create(
                signingHash: request.SigningHash,
                hashAlgorithm: request.HashAlgorithm,
                metadata: metadata)));
        }
    }
}
