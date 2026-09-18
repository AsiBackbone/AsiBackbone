using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Provides provider-neutral helper methods for preparing and signing AsiBackbone governance artifacts.
/// </summary>
/// <remarks>
/// The helpers canonicalize and hash artifacts before optionally invoking <see cref="IGovernanceSigningService" />.
/// They do not verify signatures, persist records, provide immutable storage, or make tamper-evidence claims.
/// </remarks>
public static class GovernanceArtifactSigner
{
    /// <summary>
    /// Creates an unsigned wrapper for decision receipt.
    /// </summary>
    public static SignedGovernanceArtifact<IDecisionReceipt> CreateUnsignedDecisionReceipt(
        IDecisionReceipt residue,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null)
    {
        return CreateUnsigned(residue, CanonicalPayloadBuilder.ForDecisionReceipt(residue, options), hashAlgorithm);
    }

    /// <summary>
    /// Creates signing-ready metadata for decision receipt without invoking a signing provider.
    /// </summary>
    public static SignedGovernanceArtifact<IDecisionReceipt> CreateSigningReadyDecisionReceipt(
        IDecisionReceipt residue,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return CreateSigningReady(residue, CanonicalPayloadBuilder.ForDecisionReceipt(residue, options), hashAlgorithm, metadata);
    }

    /// <summary>
    /// Signs decision receipt after canonical payload hashing.
    /// </summary>
    public static ValueTask<SignedGovernanceArtifact<IDecisionReceipt>> SignDecisionReceiptAsync(
        IDecisionReceipt residue,
        IGovernanceSigningService signingService,
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
            CanonicalPayloadBuilder.ForDecisionReceipt(residue, options),
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
        IGovernanceSigningService signingService,
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
    /// Creates an unsigned wrapper for an decision receipt lifecycle event.
    /// </summary>
    public static SignedGovernanceArtifact<DecisionReceiptLifecycleEvent> CreateUnsignedDecisionReceiptLifecycleEvent(
        DecisionReceiptLifecycleEvent lifecycleEvent,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null)
    {
        return CreateUnsigned(lifecycleEvent, CanonicalPayloadBuilder.ForDecisionReceiptLifecycleEvent(lifecycleEvent, options), hashAlgorithm);
    }

    /// <summary>
    /// Creates signing-ready metadata for an decision receipt lifecycle event without invoking a signing provider.
    /// </summary>
    public static SignedGovernanceArtifact<DecisionReceiptLifecycleEvent> CreateSigningReadyDecisionReceiptLifecycleEvent(
        DecisionReceiptLifecycleEvent lifecycleEvent,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return CreateSigningReady(lifecycleEvent, CanonicalPayloadBuilder.ForDecisionReceiptLifecycleEvent(lifecycleEvent, options), hashAlgorithm, metadata);
    }

    /// <summary>
    /// Signs an decision receipt lifecycle event after canonical payload hashing.
    /// </summary>
    public static ValueTask<SignedGovernanceArtifact<DecisionReceiptLifecycleEvent>> SignDecisionReceiptLifecycleEventAsync(
        DecisionReceiptLifecycleEvent lifecycleEvent,
        IGovernanceSigningService signingService,
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
            CanonicalPayloadBuilder.ForDecisionReceiptLifecycleEvent(lifecycleEvent, options),
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
        IGovernanceSigningService signingService,
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
    /// Creates an unsigned wrapper for an outbox entry.
    /// </summary>
    public static SignedGovernanceArtifact<GovernanceOutboxEntry> CreateUnsignedGovernanceOutboxEntry(
        GovernanceOutboxEntry entry,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null)
    {
        return CreateUnsigned(entry, CanonicalPayloadBuilder.ForGovernanceOutboxEntry(entry, options), hashAlgorithm);
    }

    /// <summary>
    /// Creates signing-ready metadata for an outbox entry without invoking a signing provider.
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
    /// Signs an outbox entry after canonical payload hashing.
    /// </summary>
    public static ValueTask<SignedGovernanceArtifact<GovernanceOutboxEntry>> SignGovernanceOutboxEntryAsync(
        GovernanceOutboxEntry entry,
        IGovernanceSigningService signingService,
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
            metadata: signingReadyMetadata.Metadata)
        {
            SignatureInput = GovernanceSignatureInput.CreateV1(canonicalHash, signingReadyMetadata.Metadata)
        };
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
        IGovernanceSigningService signingService,
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
        SigningRequest signingRequest = CreateSigningRequest(hash, keyId, keyVersion, metadata);
        SigningResult signingResult = await signingService
            .SignAsync(signingRequest, cancellationToken)
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
                BindSignedPolicyContext(signingResult.Metadata, signingRequest.Metadata));
    }

    /// <summary>
    /// Restores the signing policy context that was bound into the signature input.
    /// </summary>
    /// <remarks>
    /// The version 1 signature input binds the policy version and policy hash the signer was asked to sign. A provider that
    /// dropped, altered, or added either key in its returned metadata would otherwise produce an artifact whose recorded
    /// policy context no longer rebuilds the signed input: it would fail verification, or carry a label that differs from
    /// what was signed.
    /// </remarks>
    private static SigningMetadata BindSignedPolicyContext(
        SigningMetadata providerMetadata,
        IReadOnlyDictionary<string, string> requestMetadata)
    {
        Dictionary<string, string> metadata = new(providerMetadata.Metadata, StringComparer.Ordinal);
        CopyBoundValue(requestMetadata, metadata, GovernanceSignatureInput.PolicyVersionMetadataKey);
        CopyBoundValue(requestMetadata, metadata, GovernanceSignatureInput.PolicyHashMetadataKey);

        return SigningMetadata.Create(
            signingHash: providerMetadata.SigningHash,
            hashAlgorithm: providerMetadata.HashAlgorithm,
            signature: providerMetadata.Signature,
            signatureAlgorithm: providerMetadata.SignatureAlgorithm,
            keyId: providerMetadata.KeyId,
            keyVersion: providerMetadata.KeyVersion,
            provider: providerMetadata.Provider,
            signedUtc: providerMetadata.SignedUtc,
            metadata: metadata);
    }

    private static void CopyBoundValue(
        IReadOnlyDictionary<string, string> source,
        Dictionary<string, string> target,
        string key)
    {
        if (source.TryGetValue(key, out string? value))
        {
            target[key] = value;
        }
        else
        {
            _ = target.Remove(key);
        }
    }
}
