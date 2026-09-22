using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Tests for the signing policy result branches, including canonical payload options normalization, verification policy options application, and verification policy outcome construction, ensuring that the signing and verification processes behave as expected under various scenarios.
/// </summary>
public sealed class SigningPolicyResultBranchTests
{
    /// <summary>
    /// Tests that the CanonicalPayloadOptions correctly normalizes the canonicalization version, hash algorithm, and metadata key allow list by trimming whitespace, removing duplicates, and applying default values where necessary, ensuring that the resulting options reflect the expected normalized state.
    /// </summary>
    [Fact]
    public void CanonicalPayloadOptionsNormalizesDefaultsAndAllowList()
    {
        var options = CanonicalPayloadOptions.Create(
            metadataKeyAllowList: [" beta ", "alpha", "", "alpha"],
            canonicalizationVersion: " custom-canonical-v1 ",
            hashAlgorithm: " SHA-512 ");

        Assert.Equal("custom-canonical-v1", options.CanonicalizationVersion);
        Assert.Equal("SHA-512", options.HashAlgorithm);
        Assert.Collection(
            options.MetadataKeyAllowList,
            key => Assert.Equal("alpha", key),
            key => Assert.Equal("beta", key));
        Assert.True(options.AllowsMetadataKey(" alpha "));
        Assert.False(options.AllowsMetadataKey(" gamma "));
        Assert.False(options.AllowsMetadataKey(" "));
    }

    /// <summary>
    /// Tests that the CanonicalPayloadOptions correctly applies default values and an empty allow list when provided with blank or whitespace inputs, ensuring that the resulting options reflect the expected defaults and do not allow any metadata keys.
    /// </summary>
    [Fact]
    public void CanonicalPayloadOptionsUsesDefaultsAndEmptyAllowListForBlankInputs()
    {
        var options = CanonicalPayloadOptions.Create(
            metadataKeyAllowList: [" ", ""],
            canonicalizationVersion: " ",
            hashAlgorithm: " ");

        Assert.Equal(CanonicalPayloadOptions.DefaultCanonicalizationVersion, options.CanonicalizationVersion);
        Assert.Equal(CanonicalPayloadOptions.DefaultHashAlgorithm, options.HashAlgorithm);
        Assert.Empty(options.MetadataKeyAllowList);
        Assert.False(options.AllowsMetadataKey("alpha"));
    }

    /// <summary>
    /// Tests that the VerificationPolicyOptions correctly applies overrides for specific signature verification categories, while rejecting undefined inputs and ensuring that default actions are applied for categories not explicitly overridden.
    /// </summary>
    [Fact]
    public void VerificationPolicyOptionsAppliesOverridesAndRejectsUndefinedInputs()
    {
        var options = VerificationPolicyOptions.Create(
            new Dictionary<SignatureVerificationCategory, VerificationPolicyAction>
            {
                [SignatureVerificationCategory.MissingSignature] = VerificationPolicyAction.Deny,
                [SignatureVerificationCategory.ProviderUnavailable] = VerificationPolicyAction.Escalate
            });

        Assert.Equal(VerificationPolicyAction.Deny, options.GetAction(SignatureVerificationCategory.MissingSignature));
        Assert.Equal(VerificationPolicyAction.Escalate, options.GetAction(SignatureVerificationCategory.ProviderUnavailable));
        Assert.Equal(VerificationPolicyAction.Deny, options.GetAction(SignatureVerificationCategory.InvalidSignature));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            VerificationPolicyOptions.Create(new Dictionary<SignatureVerificationCategory, VerificationPolicyAction>
            {
                [(SignatureVerificationCategory)999] = VerificationPolicyAction.Deny
            }));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            VerificationPolicyOptions.Create(new Dictionary<SignatureVerificationCategory, VerificationPolicyAction>
            {
                [SignatureVerificationCategory.Failed] = (VerificationPolicyAction)999
            }));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() =>
            VerificationPolicyOptions.Default.GetAction((SignatureVerificationCategory)999));
    }

    /// <summary>
    /// Tests that the VerificationPolicyOutcome correctly builds safe metadata and normalizes provider fields from the signing metadata of a signed artifact, while also reflecting the signature verification result.
    /// </summary>
    [Fact]
    public void VerificationPolicyOutcomeBuildsSafeMetadataAndNormalizesProviderFields()
    {
        SignedGovernanceArtifact<CanonicalPayload> signedArtifact = CreateArtifact(
            SigningMetadata.Create(
                hashAlgorithm: " SHA-256 ",
                signature: " provider-sig ",
                signatureAlgorithm: " ECDSA-P256 ",
                keyId: " key-1 ",
                keyVersion: " v2 ",
                provider: " local-dev ",
                signedUtc: new DateTimeOffset(2026, 6, 18, 7, 30, 0, TimeSpan.FromHours(-5)),
                metadata: new Dictionary<string, string>
                {
                    [" public_hint "] = " include-me ",
                    [" "] = " exclude-me "
                }));

        var outcome = VerificationPolicyOutcome.Create(
            signedArtifact,
            SignatureVerificationResult.Failed(" signature.invalid ", " Bad signature. "));

        Assert.False(outcome.IsVerified);
        Assert.False(outcome.ShouldAllow);
        Assert.Equal(SignatureVerificationCategory.InvalidSignature, outcome.Category);
        Assert.Equal(VerificationPolicyAction.Deny, outcome.Action);
        Assert.Equal("signature.invalid", outcome.FailureCode);
        Assert.Equal("Bad signature.", outcome.FailureMessage);
        Assert.Equal("key-1", outcome.KeyId);
        Assert.Equal("v2", outcome.KeyVersion);
        Assert.Equal("ECDSA-P256", outcome.SignatureAlgorithm);
        Assert.Equal("local-dev", outcome.Provider);
        Assert.Equal("include-me", outcome.SafeMetadata["public_hint"]);
    }

    /// <summary>
    /// Tests that the VerificationPolicyOutcome correctly allows a verified artifact when the verification policy options are overridden to allow valid signatures, ensuring that the outcome reflects the expected category and action without any failure codes.
    /// </summary>
    [Fact]
    public void VerificationPolicyOutcomeAllowsVerifiedArtifactWithOverrideOptions()
    {
        SignedGovernanceArtifact<CanonicalPayload> artifact = CreateArtifact(SigningMetadata.Create(
            hashAlgorithm: "SHA-256"));
        var options = VerificationPolicyOptions.Create(
            new Dictionary<SignatureVerificationCategory, VerificationPolicyAction>
            {
                [SignatureVerificationCategory.Valid] = VerificationPolicyAction.Allow
            });

        var outcome = VerificationPolicyOutcome.Create(
            artifact,
            SignatureVerificationResult.Verified(),
            options);

        Assert.True(outcome.IsVerified);
        Assert.True(outcome.ShouldAllow);
        Assert.Equal(SignatureVerificationCategory.Valid, outcome.Category);
        Assert.Equal(VerificationPolicyAction.Allow, outcome.Action);
        Assert.Null(outcome.FailureCode);
    }

    /// <summary>
    /// Tests that signed governance artifacts expose unsigned, signing-ready, and mismatch branches correctly.
    /// </summary>
    [Fact]
    public void SignedGovernanceArtifactsExposeUnsignedSigningReadyAndMismatchBranches()
    {
        CanonicalPayload payload = CreatePayload("artifact-1");
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);
        SignedGovernanceArtifact<CanonicalPayload> unsigned = SignedGovernanceArtifacts.WithoutSignature(payload, payload, hash);
        SignedGovernanceArtifact<CanonicalPayload> signingReady = SignedGovernanceArtifacts.SigningReady(payload, payload, hash);
        CanonicalPayload otherPayload = CreatePayload("artifact-2");
        CanonicalPayloadHash mismatchedHash = CanonicalPayloadHasher.ComputeHash(otherPayload);

        Assert.True(unsigned.HasNoSignature);
        Assert.False(unsigned.IsSigned);
        Assert.False(unsigned.IsSigningReady);
        Assert.False(signingReady.HasNoSignature);
        Assert.True(signingReady.IsSigningReady);
        Assert.False(signingReady.IsSigned);
        _ = Assert.Throws<ArgumentException>(() =>
            SignedGovernanceArtifacts.WithoutSignature(payload, payload, mismatchedHash));
    }

    private static SignedGovernanceArtifact<CanonicalPayload> CreateArtifact(SigningMetadata signingMetadata)
    {
        CanonicalPayload payload = CreatePayload("artifact-1");
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);
        var normalized = SigningMetadata.Create(
            signingHash: hash.HashValue,
            hashAlgorithm: signingMetadata.HashAlgorithm,
            signature: signingMetadata.Signature,
            signatureAlgorithm: signingMetadata.SignatureAlgorithm,
            keyId: signingMetadata.KeyId,
            keyVersion: signingMetadata.KeyVersion,
            provider: signingMetadata.Provider,
            signedUtc: signingMetadata.SignedUtc,
            metadata: signingMetadata.Metadata);

        return SignedGovernanceArtifacts.FromSigningMetadata(payload, payload, hash, normalized);
    }

    private static CanonicalPayload CreatePayload(string artifactId)
    {
        return CanonicalPayload.Create(
            CanonicalArtifactTypes.DecisionReceipt,
            artifactId,
            "schema-v1",
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            new Dictionary<string, object?>
            {
                ["artifactId"] = artifactId,
                ["outcome"] = "Allowed"
            });
    }
}
