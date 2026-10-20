using AsiBackbone.Core.Audit;
using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Core.Serialization;

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
        IDecisionReceipt receipt,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null)
    {
        return CreateUnsigned(receipt, CanonicalPayloadBuilder.ForDecisionReceipt(receipt, options), hashAlgorithm);
    }

    /// <summary>
    /// Creates signing-ready metadata for decision receipt without invoking a signing provider.
    /// </summary>
    public static SignedGovernanceArtifact<IDecisionReceipt> CreateSigningReadyDecisionReceipt(
        IDecisionReceipt receipt,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return CreateSigningReady(receipt, CanonicalPayloadBuilder.ForDecisionReceipt(receipt, options), hashAlgorithm, metadata);
    }

    /// <summary>
    /// Signs decision receipt after canonical payload hashing.
    /// </summary>
    public static ValueTask<SignedGovernanceArtifact<IDecisionReceipt>> SignDecisionReceiptAsync(
        IDecisionReceipt receipt,
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
            receipt,
            CanonicalPayloadBuilder.ForDecisionReceipt(receipt, options),
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
    /// Creates an unsigned wrapper for a capability grant.
    /// </summary>
    /// <remarks>
    /// The grant is wrapped under <paramref name="schemaVersion" />, which defaults to
    /// <see cref="GovernanceSchemaVersions.StableArtifactsV2" /> so <see cref="CapabilityGrant.MaxUseCount" /> is part of
    /// the canonical payload. The returned <see cref="SignedGovernanceArtifact{TArtifact}.Artifact" /> carries that schema
    /// version and is the grant to transport.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="schemaVersion" /> is <see cref="GovernanceSchemaVersions.StableArtifactsV1" /> and the grant carries a
    /// use limit that the version 1 payload cannot bind.
    /// </exception>
    public static SignedGovernanceArtifact<CapabilityGrant> CreateUnsignedCapabilityGrant(
        CapabilityGrant grant,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        string? schemaVersion = null)
    {
        CapabilityGrant versionedGrant = ApplyCapabilityGrantSchemaVersion(grant, schemaVersion);
        return CreateUnsigned(versionedGrant, CanonicalPayloadBuilder.ForCapabilityGrant(versionedGrant, options), hashAlgorithm);
    }

    /// <summary>
    /// Creates signing-ready metadata for a capability grant without invoking a signing provider.
    /// </summary>
    /// <remarks>
    /// The grant schema version is applied as described for <see cref="CreateUnsignedCapabilityGrant" />.
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="schemaVersion" /> is <see cref="GovernanceSchemaVersions.StableArtifactsV1" /> and the grant carries a
    /// use limit that the version 1 payload cannot bind.
    /// </exception>
    public static SignedGovernanceArtifact<CapabilityGrant> CreateSigningReadyCapabilityGrant(
        CapabilityGrant grant,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        string? schemaVersion = null)
    {
        CapabilityGrant versionedGrant = ApplyCapabilityGrantSchemaVersion(grant, schemaVersion);
        return CreateSigningReady(versionedGrant, CanonicalPayloadBuilder.ForCapabilityGrant(versionedGrant, options), hashAlgorithm, metadata);
    }

    /// <summary>
    /// Signs a capability grant after canonical payload hashing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The grant is signed under <paramref name="schemaVersion" />, which defaults to
    /// <see cref="GovernanceSchemaVersions.StableArtifactsV2" /> so the issuer-bound
    /// <see cref="CapabilityGrant.MaxUseCount" /> is covered by the signature. A grant created with another schema version
    /// is re-created under the requested one, and the returned
    /// <see cref="SignedGovernanceArtifact{TArtifact}.Artifact" /> is the grant that was signed.
    /// </para>
    /// <para>
    /// Verifiers rebuild the payload with <see cref="CapabilityGrantValidationOptions.ProofPayloadOptions" />, so
    /// <paramref name="options" /> must match what the relying party configures. Use
    /// <see cref="CapabilityGrantToken" /> to carry the result to a relying party.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentException">
    /// <paramref name="schemaVersion" /> is <see cref="GovernanceSchemaVersions.StableArtifactsV1" /> and the grant carries a
    /// use limit that the version 1 payload cannot bind.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// <paramref name="requireSignature" /> is <see langword="true" /> and the signing provider returned no signature.
    /// </exception>
    public static ValueTask<SignedGovernanceArtifact<CapabilityGrant>> SignCapabilityGrantAsync(
        CapabilityGrant grant,
        IGovernanceSigningService signingService,
        CanonicalPayloadOptions? options = null,
        string? hashAlgorithm = null,
        string? keyId = null,
        string? keyVersion = null,
        IReadOnlyDictionary<string, string>? metadata = null,
        bool requireSignature = true,
        string? schemaVersion = null,
        CancellationToken cancellationToken = default)
    {
        CapabilityGrant versionedGrant = ApplyCapabilityGrantSchemaVersion(grant, schemaVersion);

        return SignAsync(
            versionedGrant,
            CanonicalPayloadBuilder.ForCapabilityGrant(versionedGrant, options),
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

    private static CapabilityGrant ApplyCapabilityGrantSchemaVersion(CapabilityGrant grant, string? schemaVersion)
    {
        ArgumentNullException.ThrowIfNull(grant);

        string effectiveSchemaVersion = string.IsNullOrWhiteSpace(schemaVersion)
            ? GovernanceSchemaVersions.StableArtifactsV2
            : schemaVersion.Trim();

        // The version 1 payload omits the use limit, so signing a limited grant under it would leave the limit outside the
        // proof while the validator still enforces it. Refuse rather than produce a grant whose limit a relying party
        // could not trust.
        return string.Equals(effectiveSchemaVersion, GovernanceSchemaVersions.StableArtifactsV1, StringComparison.Ordinal)
            && grant.MaxUseCount.HasValue
            ? throw new ArgumentException(
                $"A capability grant with a use limit cannot be signed under schema version {GovernanceSchemaVersions.StableArtifactsV1}, whose canonical payload does not bind the limit. Use {GovernanceSchemaVersions.StableArtifactsV2}.",
                nameof(schemaVersion))
            : grant.WithSchemaVersion(effectiveSchemaVersion);
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
