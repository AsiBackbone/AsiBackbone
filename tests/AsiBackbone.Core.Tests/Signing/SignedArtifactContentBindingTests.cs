using System.Reflection;
using AsiBackbone.Core.CapabilityTokens;
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
        SignedGovernanceArtifact<CapabilityTokenGrant> artifact = CreateSignedGrant(CreateGrant());
        var verifier = new AlwaysValidVerificationService();

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(artifact, verifier, cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.ShouldAllow);
        Assert.True(verifier.WasCalled);
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
        SignedGovernanceArtifact<CapabilityTokenGrant> signed = CreateSignedGrant(CreateGrant());

        CapabilityTokenGrant tamperedGrant = CreateGrant(scopes: ["robotics.execute", "robotics.admin"]);
        var tampered = SignedGovernanceArtifacts.FromSigningMetadata(
            tamperedGrant,
            CanonicalPayloadBuilder.ForCapabilityTokenGrant(tamperedGrant),
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
        CapabilityTokenGrant grant = CreateGrant();
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityTokenGrant(grant);
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);

        var metadataWithoutArtifactType = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["artifact_id"] = hash.ArtifactId,
            ["canonicalization_version"] = hash.CanonicalizationVersion,
            ["payload_schema_version"] = hash.PayloadSchemaVersion
        };

        SignedGovernanceArtifact<CapabilityTokenGrant> artifact = CreateUncheckedArtifact(
            grant,
            payload,
            hash,
            metadataWithoutArtifactType);

        var verifier = new AlwaysValidVerificationService();

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(artifact, verifier, cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.ShouldAllow);
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
        SignedGovernanceArtifact<CapabilityTokenGrant> signed = CreateSignedGrant(CreateGrant());
        CapabilityTokenGrant tamperedGrant = CreateGrant(audience: "gateway-2");

        ArgumentException exception = Assert.Throws<ArgumentException>(() => SignedGovernanceArtifacts.Rehydrate(
            tamperedGrant,
            CanonicalPayloadBuilder.ForCapabilityTokenGrant(tamperedGrant),
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
        CapabilityTokenGrant grant = CreateGrant();
        SignedGovernanceArtifact<CapabilityTokenGrant> signed = CreateSignedGrant(grant);

        SignedGovernanceArtifact<CapabilityTokenGrant> rehydrated = SignedGovernanceArtifacts.Rehydrate(
            grant,
            CanonicalPayloadBuilder.ForCapabilityTokenGrant(grant),
            signed.CanonicalHash,
            signed.SigningMetadata);

        Assert.Equal(signed.SigningHash, rehydrated.SigningHash);
    }

    private static CapabilityTokenGrant CreateGrant(
        string tokenId = "grant-content-binding",
        string issuer = "issuer-1",
        string audience = "gateway-1",
        IEnumerable<string>? scopes = null)
    {
        return CapabilityTokenGrant.Create(
            tokenId: tokenId,
            issuer: issuer,
            audience: audience,
            scopes: scopes ?? ["robotics.execute"],
            issuedUtc: IssuedUtc,
            expiresUtc: IssuedUtc.AddMinutes(10),
            policyVersion: "policy-v1",
            policyHash: "policy-hash");
    }

    private static SignedGovernanceArtifact<CapabilityTokenGrant> CreateSignedGrant(CapabilityTokenGrant grant)
    {
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityTokenGrant(grant);
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

        return SignedGovernanceArtifacts.FromSigningMetadata(grant, payload, hash, signingMetadata);
    }

    /// <summary>
    /// Builds a signed artifact directly, bypassing the factory merge that restores canonical descriptors.
    /// </summary>
    /// <remarks>
    /// The shipped factories always write the four canonical descriptors, so an artifact missing one cannot be produced
    /// through the public surface. Constructing it directly exercises the verifier's own guard for artifacts that reach it
    /// from outside those factories.
    /// </remarks>
    private static SignedGovernanceArtifact<CapabilityTokenGrant> CreateUncheckedArtifact(
        CapabilityTokenGrant grant,
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

        ConstructorInfo constructor = typeof(SignedGovernanceArtifact<CapabilityTokenGrant>).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(CapabilityTokenGrant), typeof(CanonicalPayload), typeof(CanonicalPayloadHash), typeof(SigningMetadata)],
            modifiers: null)
            ?? throw new InvalidOperationException("SignedGovernanceArtifact constructor could not be located.");

        return (SignedGovernanceArtifact<CapabilityTokenGrant>)constructor.Invoke(
        [
            grant,
            payload,
            hash,
            signingMetadata
        ]);
    }

    private sealed class AlwaysValidVerificationService : IAsiBackboneSignatureVerificationService
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
