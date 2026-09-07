using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Provides provider-neutral helper methods for preparing and signing AsiBackbone governance artifacts.
/// </summary>
/// <remarks>
/// The helpers canonicalize and hash artifacts before optionally invoking <see cref="IAsiBackboneSigningService" />.
/// They do not verify signatures, persist records, provide immutable storage, or make tamper-evidence claims.
/// </remarks>
public static class GovernanceArtifactSigner
{
    /// <summary>
    /// Creates an unsigned wrapper for audit residue.
    /// </summary>
    public static SignedGovernanceArtifact<IAsiBackboneAuditResidue> CreateUnsignedAuditResidue(
        IAsiBackboneAuditResidue residue,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null)
    {
        return CreateUnsigned(residue, CanonicalPayloadBuilder.ForAuditResidue(residue, options), hashAlgorithm);
    }

    /// <summary>
    /// Creates signing-ready metadata for audit residue without invoking a signing provider.
    /// </summary>
    public static SignedGovernanceArtifact<IAsiBackboneAuditResidue> CreateSigningReadyAuditResidue(
        IAsiBackboneAuditResidue residue,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return CreateSigningReady(residue, CanonicalPayloadBuilder.ForAuditResidue(residue, options), hashAlgorithm, metadata);
    }

    /// <summary>
    /// Signs audit residue after canonical payload hashing.
    /// </summary>
    public static ValueTask<SignedGovernanceArtifact<IAsiBackboneAuditResidue>> SignAuditResidueAsync(
        IAsiBackboneAuditResidue residue,
        IAsiBackboneSigningService signingService,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        string? keyId = null,
        string? keyVersion = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool requireSignature = true,
        CancellationToken cancellationToken = default)
    {
        return SignAsync(
            residue,
            CanonicalPayloadBuilder.ForAuditResidue(residue, options),
            signingService,
            hashAlgorithm,
            keyId,
            keyVersion,
            metadata,
            requireSignature,
            cancellationToken);
    }

    /// <summary>
    /// Creates an unsigned wrapper for a persistence-ready audit ledger record.
    /// </summary>
    public static SignedGovernanceArtifact<AuditLedgerRecord> CreateUnsignedAuditLedgerRecord(
        AuditLedgerRecord record,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null)
    {
        return CreateUnsigned(record, CanonicalPayloadBuilder.ForAuditLedgerRecord(record, options), hashAlgorithm);
    }

    /// <summary>
    /// Creates signing-ready metadata for a persistence-ready audit ledger record without invoking a signing provider.
    /// </summary>
    public static SignedGovernanceArtifact<AuditLedgerRecord> CreateSigningReadyAuditLedgerRecord(
        AuditLedgerRecord record,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return CreateSigningReady(record, CanonicalPayloadBuilder.ForAuditLedgerRecord(record, options), hashAlgorithm, metadata);
    }

    /// <summary>
    /// Signs a persistence-ready audit ledger record after canonical payload hashing.
    /// </summary>
    public static ValueTask<SignedGovernanceArtifact<AuditLedgerRecord>> SignAuditLedgerRecordAsync(
        AuditLedgerRecord record,
        IAsiBackboneSigningService signingService,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        string? keyId = null,
        string? keyVersion = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool requireSignature = true,
        CancellationToken cancellationToken = default)
    {
        return SignAsync(
            record,
            CanonicalPayloadBuilder.ForAuditLedgerRecord(record, options),
            signingService,
            hashAlgorithm,
            keyId,
            keyVersion,
            metadata,
            requireSignature,
            cancellationToken);
    }

    /// <summary>
    /// Creates an unsigned wrapper for an audit residue lifecycle event.
    /// </summary>
    public static SignedGovernanceArtifact<AuditResidueLifecycleEvent> CreateUnsignedAuditResidueLifecycleEvent(
        AuditResidueLifecycleEvent lifecycleEvent,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null)
    {
        return CreateUnsigned(lifecycleEvent, CanonicalPayloadBuilder.ForAuditResidueLifecycleEvent(lifecycleEvent, options), hashAlgorithm);
    }

    /// <summary>
    /// Creates signing-ready metadata for an audit residue lifecycle event without invoking a signing provider.
    /// </summary>
    public static SignedGovernanceArtifact<AuditResidueLifecycleEvent> CreateSigningReadyAuditResidueLifecycleEvent(
        AuditResidueLifecycleEvent lifecycleEvent,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return CreateSigningReady(lifecycleEvent, CanonicalPayloadBuilder.ForAuditResidueLifecycleEvent(lifecycleEvent, options), hashAlgorithm, metadata);
    }

    /// <summary>
    /// Signs an audit residue lifecycle event after canonical payload hashing.
    /// </summary>
    public static ValueTask<SignedGovernanceArtifact<AuditResidueLifecycleEvent>> SignAuditResidueLifecycleEventAsync(
        AuditResidueLifecycleEvent lifecycleEvent,
        IAsiBackboneSigningService signingService,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        string? keyId = null,
        string? keyVersion = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool requireSignature = true,
        CancellationToken cancellationToken = default)
    {
        return SignAsync(
            lifecycleEvent,
            CanonicalPayloadBuilder.ForAuditResidueLifecycleEvent(lifecycleEvent, options),
            signingService,
            hashAlgorithm,
            keyId,
            keyVersion,
            metadata,
            requireSignature,
            cancellationToken);
    }

    /// <summary>
    /// Creates an unsigned wrapper for a governance emission envelope.
    /// </summary>
    public static SignedGovernanceArtifact<GovernanceEmissionEnvelope> CreateUnsignedGovernanceEmissionEnvelope(
        GovernanceEmissionEnvelope envelope,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null)
    {
        return CreateUnsigned(envelope, CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(envelope, options), hashAlgorithm);
    }

    /// <summary>
    /// Creates signing-ready metadata for a governance emission envelope without invoking a signing provider.
    /// </summary>
    public static SignedGovernanceArtifact<GovernanceEmissionEnvelope> CreateSigningReadyGovernanceEmissionEnvelope(
        GovernanceEmissionEnvelope envelope,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return CreateSigningReady(envelope, CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(envelope, options), hashAlgorithm, metadata);
    }

    /// <summary>
    /// Signs a governance emission envelope after canonical payload hashing.
    /// </summary>
    public static ValueTask<SignedGovernanceArtifact<GovernanceEmissionEnvelope>> SignGovernanceEmissionEnvelopeAsync(
        GovernanceEmissionEnvelope envelope,
        IAsiBackboneSigningService signingService,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        string? keyId = null,
        string? keyVersion = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool requireSignature = true,
        CancellationToken cancellationToken = default)
    {
        return SignAsync(
            envelope,
            CanonicalPayloadBuilder.ForGovernanceEmissionEnvelope(envelope, options),
            signingService,
            hashAlgorithm,
            keyId,
            keyVersion,
            metadata,
            requireSignature,
            cancellationToken);
    }

    /// <summary>
    /// Creates an unsigned wrapper for a governance outbox entry.
    /// </summary>
    public static SignedGovernanceArtifact<GovernanceOutboxEntry> CreateUnsignedGovernanceOutboxEntry(
        GovernanceOutboxEntry entry,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null)
    {
        return CreateUnsigned(entry, CanonicalPayloadBuilder.ForGovernanceOutboxEntry(entry, options), hashAlgorithm);
    }

    /// <summary>
    /// Creates signing-ready metadata for a governance outbox entry without invoking a signing provider.
    /// </summary>
    public static SignedGovernanceArtifact<GovernanceOutboxEntry> CreateSigningReadyGovernanceOutboxEntry(
        GovernanceOutboxEntry entry,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return CreateSigningReady(entry, CanonicalPayloadBuilder.ForGovernanceOutboxEntry(entry, options), hashAlgorithm, metadata);
    }

    /// <summary>
    /// Signs a governance outbox entry after canonical payload hashing.
    /// </summary>
    public static ValueTask<SignedGovernanceArtifact<GovernanceOutboxEntry>> SignGovernanceOutboxEntryAsync(
        GovernanceOutboxEntry entry,
        IAsiBackboneSigningService signingService,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        string? keyId = null,
        string? keyVersion = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool requireSignature = true,
        CancellationToken cancellationToken = default)
    {
        return SignAsync(
            entry,
            CanonicalPayloadBuilder.ForGovernanceOutboxEntry(entry, options),
            signingService,
            hashAlgorithm,
            keyId,
            keyVersion,
            metadata,
            requireSignature,
            cancellationToken);
    }

    /// <summary>
    /// Creates a signing request from canonical payload hash metadata.
    /// </summary>
    public static SigningRequest CreateSigningRequest(
        CanonicalPayloadHash canonicalHash,
        string? keyId = null,
        string? keyVersion = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        ArgumentNullException.ThrowIfNull(canonicalHash);

        var signingReadyMetadata = canonicalHash.ToSigningMetadata(metadata);

        return new SigningRequest(
            canonicalHash.HashValue,
            canonicalHash.HashAlgorithm,
            purpose: canonicalHash.ArtifactType,
            keyId: keyId,
            keyVersion: keyVersion,
            metadata: signingReadyMetadata.Metadata);
    }

    private static SignedGovernanceArtifact<TArtifact> CreateUnsigned<TArtifact>(
        TArtifact artifact,
        CanonicalPayload payload,
        string? hashAlgorithm)
    {
        return SignedGovernanceArtifacts.WithoutSignature(
            artifact,
            payload,
            CanonicalPayloadHasher.ComputeHash(payload, hashAlgorithm));
    }

    private static SignedGovernanceArtifact<TArtifact> CreateSigningReady<TArtifact>(
        TArtifact artifact,
        CanonicalPayload payload,
        string? hashAlgorithm,
        IReadOnlyDictionary<string, string>? metadata)
    {
        return SignedGovernanceArtifacts.SigningReady(
            artifact,
            payload,
            CanonicalPayloadHasher.ComputeHash(payload, hashAlgorithm),
            metadata);
    }

    private static async ValueTask<SignedGovernanceArtifact<TArtifact>> SignAsync<TArtifact>(
        TArtifact artifact,
        CanonicalPayload payload,
        IAsiBackboneSigningService signingService,
        string? hashAlgorithm,
        string? keyId,
        string? keyVersion,
        IReadOnlyDictionary<string, string>? metadata,
        bool requireSignature,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(signingService);
        cancellationToken.ThrowIfCancellationRequested();

        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload, hashAlgorithm);
        SigningResult signingResult = await signingService
            .SignAsync(CreateSigningRequest(hash, keyId, keyVersion, metadata), cancellationToken)
            .ConfigureAwait(false);

        // A provider returning a failure or no-signature result previously produced an artifact with IsSigned false and
        // no exception, so a caller that asked to sign could carry on holding an unsigned artifact. Callers that want the
        // unsigned result back pass requireSignature: false and inspect IsSigned themselves.
        return requireSignature && !signingResult.Metadata.IsSigned
            ? throw new InvalidOperationException(
                $"The signing provider returned no signature for artifact type '{payload.ArtifactType}'. Pass requireSignature: false to accept and inspect an unsigned result.")
            : SignedGovernanceArtifacts.FromSigningMetadata(
                artifact,
                payload,
                hash,
                signingResult.Metadata);
    }
}
