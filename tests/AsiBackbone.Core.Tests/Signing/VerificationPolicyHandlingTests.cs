using System.Reflection;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Contains unit tests for the verification policy handling logic in the AsiBackbone.Core.Signing namespace, specifically focusing on the behavior of the GovernanceArtifactVerifier and its interaction with signature verification results, preflight checks, and policy evaluation.
/// </summary>
public sealed class VerificationPolicyHandlingTests
{
    /// <summary>
    /// Provides test cases for various failure scenarios that should result in preflight verification failures.
    /// </summary>
    public static TheoryData<string, SignatureVerificationCategory, VerificationPolicyAction, string> PreflightFailureCases => new()
    {
        { "missing-signing-hash", SignatureVerificationCategory.MissingSignature, VerificationPolicyAction.RequireAcknowledgment, "signature.missing" },
        { "signing-hash-mismatch", SignatureVerificationCategory.HashMismatch, VerificationPolicyAction.Deny, "signature.hash-mismatch" },
        { "metadata-hash-algorithm-mismatch", SignatureVerificationCategory.HashMismatch, VerificationPolicyAction.Deny, "signature.hash-algorithm-unsupported" },
        { "required-hash-algorithm-mismatch", SignatureVerificationCategory.HashMismatch, VerificationPolicyAction.Deny, "signature.hash-algorithm-unsupported" },
        { "canonical-artifact-id-mismatch", SignatureVerificationCategory.CanonicalizationMismatch, VerificationPolicyAction.Escalate, "signature.canonicalization-mismatch" },
        { "canonical-artifact-type-mismatch", SignatureVerificationCategory.CanonicalizationMismatch, VerificationPolicyAction.Escalate, "signature.canonicalization-mismatch" },
        { "canonicalization-version-mismatch", SignatureVerificationCategory.CanonicalizationMismatch, VerificationPolicyAction.Escalate, "signature.canonicalization-mismatch" },
        { "payload-schema-version-mismatch", SignatureVerificationCategory.CanonicalizationMismatch, VerificationPolicyAction.Escalate, "signature.canonicalization-mismatch" },
        { "expected-key-id-mismatch", SignatureVerificationCategory.UnknownKeyVersion, VerificationPolicyAction.Escalate, "signature.key-version-unknown" },
        { "required-provider-mismatch", SignatureVerificationCategory.ProviderUnavailable, VerificationPolicyAction.Defer, "signature.provider-unavailable" },
        { "expected-policy-version-mismatch", SignatureVerificationCategory.CanonicalizationMismatch, VerificationPolicyAction.Escalate, "signature.canonicalization-mismatch" },
        { "expected-policy-hash-mismatch", SignatureVerificationCategory.CanonicalizationMismatch, VerificationPolicyAction.Escalate, "signature.canonicalization-mismatch" }
    };

    /// <summary>
    /// Provides test cases for mapping various failure codes and status patterns to the expected SignatureVerificationCategory.
    /// </summary>
    public static TheoryData<string, SignatureVerificationCategory> CategoryMappingCases => new()
    {
        { "missing-signature-status", SignatureVerificationCategory.MissingSignature },
        { "missing-failure-code", SignatureVerificationCategory.MissingSignature },
        { "hash-mismatch", SignatureVerificationCategory.HashMismatch },
        { "canonicalization-mismatch", SignatureVerificationCategory.CanonicalizationMismatch },
        { "payload-schema-mismatch", SignatureVerificationCategory.CanonicalizationMismatch },
        { "artifact-mismatch", SignatureVerificationCategory.CanonicalizationMismatch },
        { "unsupported-signature", SignatureVerificationCategory.UnsupportedAlgorithm },
        { "unsupported-algorithm", SignatureVerificationCategory.UnsupportedAlgorithm },
        { "revoked-key", SignatureVerificationCategory.RevokedKey },
        { "disabled-key", SignatureVerificationCategory.RevokedKey },
        { "unknown-key", SignatureVerificationCategory.UnknownKeyVersion },
        { "key-version-unknown", SignatureVerificationCategory.UnknownKeyVersion },
        { "key-dot-mismatch", SignatureVerificationCategory.UnknownKeyVersion },
        { "key-dash-mismatch", SignatureVerificationCategory.UnknownKeyVersion },
        { "provider-unavailable", SignatureVerificationCategory.ProviderUnavailable },
        { "unavailable", SignatureVerificationCategory.ProviderUnavailable },
        { "timeout", SignatureVerificationCategory.ProviderUnavailable },
        { "network", SignatureVerificationCategory.ProviderUnavailable },
        { "invalid-signature", SignatureVerificationCategory.InvalidSignature },
        { "malformed-signature", SignatureVerificationCategory.InvalidSignature },
        { "signature-failure", SignatureVerificationCategory.InvalidSignature },
        { "failed-fallback", SignatureVerificationCategory.Failed }
    };

    /// <summary>
    /// Verifies that the VerifyAsync method correctly maps a valid signature to the expected SignatureVerificationCategory and VerificationPolicyAction, resulting in an allow outcome.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of verifying the artifact and asserting the expected outcome for a valid signature scenario.
    /// </returns>
    [Fact]
    public async Task VerifyAsyncMapsValidSignatureToAllow()
    {
        SignedGovernanceArtifact<string> artifact = CreateSignedArtifact();
        var verifier = new StubVerificationService(SignatureVerificationResult.Verified());

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.IsVerified);
        Assert.True(outcome.ShouldAllow);
        Assert.Equal(SignatureVerificationCategory.Valid, outcome.Category);
        Assert.Equal(VerificationPolicyAction.Allow, outcome.Action);
        Assert.Equal("Verified", outcome.Status);
        Assert.Equal("record-1", outcome.ArtifactId);
        Assert.Equal("asibackbone.test-artifact", outcome.ArtifactType);
        Assert.Equal("key-1", outcome.KeyId);
        Assert.Equal("v1", outcome.KeyVersion);
        Assert.False(outcome.SafeMetadata.ContainsKey("signature"));
        Assert.True(outcome.SafeMetadata.ContainsKey("signed_utc"));
    }

    /// <summary>
    /// Verifies that the VerifyAsync method correctly maps an invalid signature to the expected SignatureVerificationCategory and VerificationPolicyAction, resulting in a deny outcome.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of verifying the artifact and asserting the expected outcome for an invalid signature scenario.
    /// </returns>
    [Fact]
    public async Task VerifyAsyncMapsInvalidSignatureToDeny()
    {
        SignedGovernanceArtifact<string> artifact = CreateSignedArtifact();
        var verifier = new StubVerificationService(SignatureVerificationResult.Failed(
            "verification-error",
            SignatureVerificationCategory.InvalidSignature,
            "The signature did not verify."));

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.IsVerified);
        Assert.False(outcome.ShouldAllow);
        Assert.Equal(SignatureVerificationCategory.InvalidSignature, outcome.Category);
        Assert.Equal(VerificationPolicyAction.Deny, outcome.Action);
        Assert.Equal("verification-error", outcome.FailureCode);
    }

    /// <summary>
    /// Verifies that the VerifyAsync method correctly maps a missing signature to the expected SignatureVerificationCategory and VerificationPolicyAction, resulting in a require acknowledgment outcome.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of verifying the artifact and asserting the expected outcome for a missing signature scenario.
    /// </returns>
    [Fact]
    public async Task VerifyAsyncMapsMissingSignatureToRequireAcknowledgment()
    {
        SignedGovernanceArtifact<string> artifact = CreateArtifactWithoutSignature();
        var verifier = new StubVerificationService(SignatureVerificationResult.Verified());

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.IsVerified);
        Assert.Equal(SignatureVerificationCategory.MissingSignature, outcome.Category);
        Assert.Equal(VerificationPolicyAction.RequireAcknowledgment, outcome.Action);
        Assert.Equal("signature.missing", outcome.FailureCode);
        Assert.False(verifier.WasCalled);
    }

    /// <summary>
    /// Verifies that the VerifyAsync method correctly maps an unknown key version to the expected SignatureVerificationCategory and VerificationPolicyAction, resulting in an escalated outcome.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of verifying the artifact and asserting the expected outcome for an unknown key version scenario.
    /// </returns>
    [Fact]
    public async Task VerifyAsyncMapsUnknownKeyVersionToEscalate()
    {
        SignedGovernanceArtifact<string> artifact = CreateSignedArtifact(keyVersion: "retired");
        var verifier = new StubVerificationService(SignatureVerificationResult.Verified());
        var context = VerificationPolicyContext.Create(expectedKeyVersion: "v1");

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            verifier,
            context: context,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.IsVerified);
        Assert.Equal(SignatureVerificationCategory.UnknownKeyVersion, outcome.Category);
        Assert.Equal(VerificationPolicyAction.Escalate, outcome.Action);
        Assert.Equal("signature.key-version-unknown", outcome.FailureCode);
        Assert.False(verifier.WasCalled);
    }

    /// <summary>
    /// Verifies that the VerifyAsync method correctly maps a provider unavailable exception to the expected SignatureVerificationCategory and VerificationPolicyAction, resulting in a deferred outcome.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of verifying the artifact and asserting the expected outcome for a provider unavailable scenario.
    /// </returns>
    [Fact]
    public async Task VerifyAsyncMapsProviderUnavailableToDefer()
    {
        SignedGovernanceArtifact<string> artifact = CreateSignedArtifact();
        var verifier = new StubVerificationService(
            SignatureVerificationResult.Verified(),
            CreateProviderException("invalid-operation"));

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.IsVerified);
        Assert.Equal(SignatureVerificationCategory.ProviderUnavailable, outcome.Category);
        Assert.Equal(VerificationPolicyAction.Defer, outcome.Action);
        Assert.Equal("signature.provider-unavailable", outcome.FailureCode);
        Assert.True(verifier.WasCalled);
    }

    /// <summary>
    /// Verifies that the VerifyAsync method correctly applies host policy overrides specified in the VerificationPolicyOptions, resulting in the expected SignatureVerificationCategory and VerificationPolicyAction for a missing signature scenario.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of verifying the artifact with host policy overrides and asserting the expected outcome.
    /// </returns>
    [Fact]
    public async Task VerifyAsyncSupportsHostPolicyOverrides()
    {
        SignedGovernanceArtifact<string> artifact = CreateArtifactWithoutSignature();
        var verifier = new StubVerificationService(SignatureVerificationResult.Verified());
        var options = VerificationPolicyOptions.Create(new Dictionary<SignatureVerificationCategory, VerificationPolicyAction>
        {
            [SignatureVerificationCategory.MissingSignature] = VerificationPolicyAction.DeadLetter
        });

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            verifier,
            options,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(SignatureVerificationCategory.MissingSignature, outcome.Category);
        Assert.Equal(VerificationPolicyAction.DeadLetter, outcome.Action);
    }

    /// <summary>
    /// Verifies that the VerifyAsync method correctly identifies and handles various preflight failure scenarios, returning the expected SignatureVerificationCategory, VerificationPolicyAction, and failure code without invoking the signature verification service.
    /// </summary>
    /// <param name="scenario">
    /// The scenario name that represents a specific preflight failure condition to be tested. This parameter is used to create a SignedGovernanceArtifact and an optional VerificationPolicyContext that simulate the corresponding failure condition.
    /// </param>
    /// <param name="expectedCategory">
    /// The expected SignatureVerificationCategory that the VerifyAsync method should return for the given preflight failure scenario. This parameter is used to assert that the actual category returned by the method matches the expected category.
    /// </param>
    /// <param name="expectedAction">
    /// The expected VerificationPolicyAction that the VerifyAsync method should return for the given preflight failure scenario. This parameter is used to assert that the actual action returned by the method matches the expected action.
    /// </param>
    /// <param name="expectedFailureCode">
    /// The expected failure code that the VerifyAsync method should return for the given preflight failure scenario. This parameter is used to assert that the actual failure code returned by the method matches the expected failure code.
    /// </param>
    /// <returns></returns>
    [Theory]
    [MemberData(nameof(PreflightFailureCases))]
    public async Task VerifyAsyncReturnsExpectedPreflightFailures(
        string scenario,
        SignatureVerificationCategory expectedCategory,
        VerificationPolicyAction expectedAction,
        string expectedFailureCode)
    {
        (SignedGovernanceArtifact<string> artifact, VerificationPolicyContext? context) = CreatePreflightScenario(scenario);
        var verifier = new StubVerificationService(SignatureVerificationResult.Verified());

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            verifier,
            context: context,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.IsVerified);
        Assert.Equal(expectedCategory, outcome.Category);
        Assert.Equal(expectedAction, outcome.Action);
        Assert.Equal(expectedFailureCode, outcome.FailureCode);
        Assert.Equal(expectedFailureCode, outcome.SafeMetadata["failure_code"]);
        Assert.False(verifier.WasCalled);
    }

    /// <summary>
    /// Verifies that the VerifyAsync method correctly maps various provider exceptions to the expected SignatureVerificationCategory and VerificationPolicyAction.
    /// </summary>
    /// <param name="scenario">
    /// The scenario name that represents a specific provider exception to be tested. This parameter is used to create an exception instance that simulates the corresponding provider failure condition.
    /// </param>
    /// <returns>
    /// A task that represents the asynchronous operation of verifying the artifact and asserting the expected outcome based on the provider exception scenario.
    /// </returns>
    [Theory]
    [InlineData("invalid-operation")]
    [InlineData("not-supported")]
    [InlineData("timeout")]
    public async Task VerifyAsyncMapsProviderExceptionsToDefer(string scenario)
    {
        SignedGovernanceArtifact<string> artifact = CreateSignedArtifact();
        var verifier = new StubVerificationService(
            SignatureVerificationResult.Verified(),
            CreateProviderException(scenario));

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.False(outcome.IsVerified);
        Assert.Equal(SignatureVerificationCategory.ProviderUnavailable, outcome.Category);
        Assert.Equal(VerificationPolicyAction.Defer, outcome.Action);
        Assert.Equal("signature.provider-unavailable", outcome.FailureCode);
        Assert.True(verifier.WasCalled);
    }

    /// <summary>
    /// Verifies that the Categorize method correctly maps various failure codes and status patterns to the expected SignatureVerificationCategory.
    /// </summary>
    /// <param name="scenario">
    /// The scenario name that represents a specific failure code and status pattern to be tested. This parameter is used to create a SignatureVerificationResult instance that simulates the corresponding failure condition.
    /// </param>
    /// <param name="expectedCategory">
    /// The expected SignatureVerificationCategory that the Categorize method should return for the given scenario. This parameter is used to assert that the actual category returned by the Categorize method matches the expected category.
    /// </param>
    [Theory]
    [MemberData(nameof(CategoryMappingCases))]
    public void CategorizeMapsFailureCodeAndStatusPatterns(
        string scenario,
        SignatureVerificationCategory expectedCategory)
    {
        SignatureVerificationResult verificationResult = CreateCategoryMappingResult(scenario);

        SignatureVerificationCategory category = VerificationPolicyEvaluator.Categorize(verificationResult);

        Assert.Equal(expectedCategory, category);
    }

    /// <summary>
    /// Verifies that the VerifyAsync method builds a safe metadata dictionary that includes the signed UTC timestamp and filters out any unsafe custom keys.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation of verifying the artifact and building the safe metadata dictionary.</returns>
    [Fact]
    public async Task VerifyAsyncBuildsSafeMetadataWithSignedUtcAndFiltersUnsafeCustomKeys()
    {
        SignedGovernanceArtifact<string> artifact = CreateSignedArtifact(
            keyId: " ",
            keyVersion: " ",
            signatureAlgorithm: null,
            provider: " ",
            signedUtc: null,
            includeSignedUtc: false,
            metadataOverrides: new Dictionary<string, string>
            {
                [" custom_context "] = " context-value ",
                ["signature_hint"] = "do-not-log",
                ["secret_name"] = "do-not-log",
                ["access_token"] = "do-not-log",
                ["credential_id"] = "do-not-log",
                ["private_key_ref"] = "do-not-log"
            });
        var verifier = new StubVerificationService(SignatureVerificationResult.Verified());

        VerificationPolicyOutcome outcome = await GovernanceArtifactVerifier.VerifyAsync(
            artifact,
            verifier,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.True(outcome.IsVerified);
        Assert.Equal("context-value", outcome.SafeMetadata["custom_context"]);
        Assert.False(outcome.SafeMetadata.ContainsKey("key_id"));
        Assert.False(outcome.SafeMetadata.ContainsKey("key_version"));
        Assert.False(outcome.SafeMetadata.ContainsKey("provider"));
        Assert.False(outcome.SafeMetadata.ContainsKey("signature_algorithm"));
        Assert.False(outcome.SafeMetadata.ContainsKey("signed_utc"));
        Assert.False(outcome.SafeMetadata.ContainsKey("signature_hint"));
        Assert.False(outcome.SafeMetadata.ContainsKey("secret_name"));
        Assert.False(outcome.SafeMetadata.ContainsKey("access_token"));
        Assert.False(outcome.SafeMetadata.ContainsKey("credential_id"));
        Assert.False(outcome.SafeMetadata.ContainsKey("private_key_ref"));
    }

    private static (SignedGovernanceArtifact<string> Artifact, VerificationPolicyContext? Context) CreatePreflightScenario(string scenario)
    {
        return scenario switch
        {
            "missing-signing-hash" => (CreateUncheckedArtifact(CreateUncheckedSigningMetadata(signingHash: null)), null),
            "signing-hash-mismatch" => (CreateUncheckedArtifact(CreateUncheckedSigningMetadata(signingHash: "deadbeef")), null),
            "metadata-hash-algorithm-mismatch" => (CreateSignedArtifact(hashAlgorithm: "SHA512"), null),
            "required-hash-algorithm-mismatch" => (CreateSignedArtifact(), VerificationPolicyContext.Create(requiredHashAlgorithm: "SHA512")),
            "canonical-artifact-id-mismatch" => (CreateSignedArtifact(metadataOverrides: new Dictionary<string, string> { ["artifact_id"] = "record-2" }), null),
            "canonical-artifact-type-mismatch" => (CreateSignedArtifact(metadataOverrides: new Dictionary<string, string> { ["artifact_type"] = "asibackbone.other-artifact" }), null),
            "canonicalization-version-mismatch" => (CreateSignedArtifact(metadataOverrides: new Dictionary<string, string> { ["canonicalization_version"] = "canonical-json-v0" }), null),
            "payload-schema-version-mismatch" => (CreateSignedArtifact(metadataOverrides: new Dictionary<string, string> { ["payload_schema_version"] = "test-schema-v2" }), null),
            "expected-key-id-mismatch" => (CreateSignedArtifact(keyId: "key-2"), VerificationPolicyContext.Create(expectedKeyId: "key-1")),
            "required-provider-mismatch" => (CreateSignedArtifact(provider: "other-provider"), VerificationPolicyContext.Create(requiredProvider: "fake-provider")),
            "expected-policy-version-mismatch" => (CreateSignedArtifact(metadataOverrides: new Dictionary<string, string> { ["policy_version"] = "policy-v2" }), VerificationPolicyContext.Create(expectedPolicyVersion: "policy-v1")),
            "expected-policy-hash-mismatch" => (CreateSignedArtifact(metadataOverrides: new Dictionary<string, string> { ["policy_hash"] = "policy-hash-v2" }), VerificationPolicyContext.Create(expectedPolicyHash: "policy-hash")),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown preflight scenario.")
        };
    }

    private static SignedGovernanceArtifact<string> CreateSignedArtifact(
        string signingHash = "",
        string hashAlgorithm = "",
        string? signature = "fake-signature",
        string? signatureAlgorithm = "FAKE-SIGNATURE-V1",
        string keyId = "key-1",
        string keyVersion = "v1",
        string provider = "fake-provider",
        DateTimeOffset? signedUtc = null,
        bool includeSignedUtc = true,
        IReadOnlyDictionary<string, string>? metadataOverrides = null)
    {
        CanonicalPayload payload = CreatePayload();
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);
        string effectiveSigningHash = string.IsNullOrWhiteSpace(signingHash)
            ? hash.HashValue
            : signingHash;
        string effectiveHashAlgorithm = string.IsNullOrWhiteSpace(hashAlgorithm)
            ? hash.HashAlgorithm
            : hashAlgorithm;
        Dictionary<string, string> metadata = CreateDefaultMetadata(hash, metadataOverrides);

        var signingMetadata = SigningMetadata.Create(
            signingHash: effectiveSigningHash,
            hashAlgorithm: effectiveHashAlgorithm,
            signature: signature,
            signatureAlgorithm: signatureAlgorithm,
            keyId: keyId,
            keyVersion: keyVersion,
            provider: provider,
            signedUtc: includeSignedUtc
                ? signedUtc ?? new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero)
                : null,
            metadata: metadata);

        return SignedGovernanceArtifacts.FromSigningMetadata(
            "artifact",
            payload,
            hash,
            signingMetadata);
    }

    private static SignedGovernanceArtifact<string> CreateArtifactWithoutSignature()
    {
        CanonicalPayload payload = CreatePayload();
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);

        return SignedGovernanceArtifacts.WithoutSignature(
            "artifact",
            payload,
            hash);
    }

    private static SigningMetadata CreateUncheckedSigningMetadata(string? signingHash)
    {
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(CreatePayload());

        return SigningMetadata.Create(
            signingHash: signingHash,
            hashAlgorithm: hash.HashAlgorithm,
            signature: "fake-signature",
            signatureAlgorithm: "FAKE-SIGNATURE-V1",
            keyId: "key-1",
            keyVersion: "v1",
            provider: "fake-provider",
            signedUtc: new DateTimeOffset(2026, 6, 16, 12, 0, 0, TimeSpan.Zero),
            metadata: CreateDefaultMetadata(hash));
    }

    private static SignedGovernanceArtifact<string> CreateUncheckedArtifact(SigningMetadata signingMetadata)
    {
        CanonicalPayload payload = CreatePayload();
        CanonicalPayloadHash hash = CanonicalPayloadHasher.ComputeHash(payload);
        ConstructorInfo constructor = typeof(SignedGovernanceArtifact<string>).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            types: [typeof(string), typeof(CanonicalPayload), typeof(CanonicalPayloadHash), typeof(SigningMetadata)],
            modifiers: null)
            ?? throw new InvalidOperationException("SignedGovernanceArtifact constructor could not be located.");

        return (SignedGovernanceArtifact<string>)constructor.Invoke(
        [
            "artifact",
            payload,
            hash,
            signingMetadata
        ]);
    }

    private static Dictionary<string, string> CreateDefaultMetadata(
        CanonicalPayloadHash hash,
        IReadOnlyDictionary<string, string>? metadataOverrides = null)
    {
        Dictionary<string, string> metadata = new(hash.ToSigningMetadata().Metadata, StringComparer.Ordinal)
        {
            ["policy_hash"] = "policy-hash",
            ["policy_version"] = "policy-v1"
        };

        if (metadataOverrides is not null)
        {
            foreach (KeyValuePair<string, string> item in metadataOverrides)
            {
                metadata[item.Key] = item.Value;
            }
        }

        return metadata;
    }

    private static SignatureVerificationResult CreateCategoryMappingResult(string scenario)
    {
        return scenario switch
        {
            "missing-signature-status" => SignatureVerificationResult.MissingSignature("Missing signature."),
            "missing-failure-code" => SignatureVerificationResult.Failed("proof.missing", "Missing proof."),
            "hash-mismatch" => SignatureVerificationResult.Failed("signature.hash-mismatch", "Hash mismatch."),
            "canonicalization-mismatch" => SignatureVerificationResult.Failed("signature.canonicalization-mismatch", "Canonicalization mismatch."),
            "payload-schema-mismatch" => SignatureVerificationResult.Failed("payload-schema.mismatch", "Payload schema mismatch."),
            "artifact-mismatch" => SignatureVerificationResult.Failed("artifact.mismatch", "Artifact mismatch."),
            "unsupported-signature" => SignatureVerificationResult.Failed("signature.unsupported", "Unsupported signature."),
            "unsupported-algorithm" => SignatureVerificationResult.Failed("signature.algorithm-unsupported", "Unsupported algorithm."),
            "revoked-key" => SignatureVerificationResult.Failed("signature.revoked", "Revoked key."),
            "disabled-key" => SignatureVerificationResult.Failed("signature.disabled", "Disabled key."),
            "unknown-key" => SignatureVerificationResult.Failed("signature.unknown-key", "Unknown key."),
            "key-version-unknown" => SignatureVerificationResult.Failed("signature.key-version-unknown", "Unknown key version."),
            "key-dot-mismatch" => SignatureVerificationResult.Failed("signature.key.mismatch", "Key mismatch."),
            "key-dash-mismatch" => SignatureVerificationResult.Failed("signature.key-mismatch", "Key mismatch."),
            "provider-unavailable" => SignatureVerificationResult.Failed("signature.provider-unavailable", "Provider unavailable."),
            "unavailable" => SignatureVerificationResult.Failed("provider.unavailable", "Provider unavailable."),
            "timeout" => SignatureVerificationResult.Failed("provider.timeout", "Provider timeout."),
            "network" => SignatureVerificationResult.Failed("provider.network", "Provider network failure."),
            "invalid-signature" => SignatureVerificationResult.Failed("signature.invalid", "Invalid signature."),
            "malformed-signature" => SignatureVerificationResult.Failed("signature.malformed", "Malformed signature."),
            "signature-failure" => SignatureVerificationResult.Failed("signature.bad", "Bad signature."),
            "failed-fallback" => SignatureVerificationResult.Failed("verification.failure", "Verification failed."),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown category mapping scenario.")
        };
    }

    private static Exception CreateProviderException(string scenario)
    {
        return scenario switch
        {
            "invalid-operation" => new InvalidOperationException("Verification provider unavailable."),
            "not-supported" => new NotSupportedException("Verification provider is not supported."),
            "timeout" => new TimeoutException("Verification provider timed out."),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario), scenario, "Unknown provider exception scenario.")
        };
    }

    private static CanonicalPayload CreatePayload()
    {
        return CanonicalPayload.Create(
            "asibackbone.test-artifact",
            "record-1",
            "test-schema-v1",
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            new Dictionary<string, object?>
            {
                ["eventId"] = "event-1",
                ["outcome"] = "Allowed"
            });
    }

    private sealed class StubVerificationService(
        SignatureVerificationResult result,
        Exception? exception = null) : IAsiBackboneSignatureVerificationService
    {
        public bool WasCalled { get; private set; }

        public ValueTask<SignatureVerificationResult> VerifyAsync(
            SignatureVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            cancellationToken.ThrowIfCancellationRequested();
            WasCalled = true;

            return exception is not null
                ? throw exception
                : ValueTask.FromResult(result);
        }
    }
}
