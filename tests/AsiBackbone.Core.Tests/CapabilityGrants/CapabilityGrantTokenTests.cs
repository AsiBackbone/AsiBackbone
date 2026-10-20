using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.Core.Serialization;
using AsiBackbone.Core.Signing;
using AsiBackbone.Storage.InMemory.CapabilityGrants;
using Xunit;

namespace AsiBackbone.Core.Tests.CapabilityGrants;

/// <summary>
/// Tests for <see cref="CapabilityGrantToken" /> and the capability grant helpers on <see cref="GovernanceArtifactSigner" />.
/// </summary>
public sealed class CapabilityGrantTokenTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// Provides grant fields, and a replacement value, that an attacker might edit after signing.
    /// </summary>
    public static TheoryData<string, string> EditedGrantFields => new()
    {
        { "tokenId", "\"grant-other\"" },
        { "issuer", "\"issuer-other\"" },
        { "audience", "\"audience-other\"" },
        { "subjectId", "\"user-other\"" },
        { "operationName", "\"orders.other\"" },
        { "resourceBinding", "\"form-sha256:other\"" },
        { "gatewayBinding", "\"gateway-other\"" },
        { "policyVersion", "\"policy-v9\"" },
        { "policyHash", "\"policy-hash-other\"" },
        { "acknowledgmentId", "\"ack-other\"" },
        { "handshakeId", "\"handshake-other\"" },
        { "issuedUtc", "\"2026-10-20T11:00:00.0000000Z\"" },
        { "notBeforeUtc", "\"2026-10-20T11:00:00.0000000Z\"" },
        { "expiresUtc", "\"2026-10-21T12:00:00.0000000Z\"" },
        { "maxUseCount", "5" },
        { "scopes", "[\"orders.approve\",\"orders.delete\"]" },
        { "schemaVersion", "\"1.0.0\"" }
    };

    /// <summary>
    /// Verifies a grant signed with the signer helper round-trips through the token encoding and validates at an
    /// execution boundary.
    /// </summary>
    [Fact]
    public async Task SignedGrantRoundTripsThroughTokenAndValidates()
    {
        var signer = new HmacSignatureService();
        SignedGovernanceArtifact<CapabilityGrant> signed = await SignAsync(CreateGrant(), signer);

        string token = CapabilityGrantToken.Encode(signed);
        CapabilityGrantTokenDecodeResult decoded = CapabilityGrantToken.Decode(token);

        Assert.StartsWith(CapabilityGrantToken.FormatPrefix, token, StringComparison.Ordinal);
        Assert.True(decoded.Succeeded);
        Assert.Null(decoded.FailureCode);
        Assert.Equal(signed.SigningHash, decoded.SignedGrant!.SigningHash);
        Assert.Equal(signed.SigningMetadata.Signature, decoded.SignedGrant.SigningMetadata.Signature);
        Assert.Equal("form-sha256:abc", decoded.SignedGrant.Artifact.ResourceBinding);
        Assert.Equal(1, decoded.SignedGrant.Artifact.MaxUseCount);

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            decoded.SignedGrant,
            CreateBoundOptions(),
            signer,
            new InMemoryCapabilityGrantUseStore(),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsValid, result.FailureCode);
        Assert.Equal("grant-1", result.TokenId);
    }

    /// <summary>
    /// Verifies the signer helper signs new grants under the schema version that binds the use limit.
    /// </summary>
    [Fact]
    public async Task SignCapabilityGrantAsyncDefaultsToSchemaThatBindsUseLimit()
    {
        SignedGovernanceArtifact<CapabilityGrant> signed = await SignAsync(CreateGrant(), new HmacSignatureService());

        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV2, signed.Artifact.SchemaVersion);
        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV2, signed.CanonicalHash.PayloadSchemaVersion);
        Assert.Contains("\"maxUseCount\":1", signed.CanonicalPayload.CanonicalJson, StringComparison.Ordinal);
        Assert.True(signed.IsSigned);
    }

    /// <summary>
    /// Verifies the unsigned and signing-ready helpers apply the same schema default as the signing helper.
    /// </summary>
    [Fact]
    public void UnsignedAndSigningReadyHelpersApplyTheSameSchemaDefault()
    {
        CapabilityGrant grant = CreateGrant();

        SignedGovernanceArtifact<CapabilityGrant> unsigned = GovernanceArtifactSigner.CreateUnsignedCapabilityGrant(grant);
        SignedGovernanceArtifact<CapabilityGrant> ready = GovernanceArtifactSigner.CreateSigningReadyCapabilityGrant(grant);

        Assert.True(unsigned.HasNoSignature);
        Assert.True(ready.IsSigningReady);
        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV2, unsigned.Artifact.SchemaVersion);
        Assert.Equal(unsigned.SigningHash, ready.SigningHash);
        Assert.Equal(CanonicalArtifactTypes.CapabilityGrant, ready.ArtifactType);
    }

    /// <summary>
    /// Verifies a grant without a use limit may still be signed under the version 1 schema for older verifiers.
    /// </summary>
    [Fact]
    public async Task SignCapabilityGrantAsyncAllowsVersionOneForUnlimitedGrant()
    {
        CapabilityGrant grant = CreateGrant(maxUseCount: null);

        SignedGovernanceArtifact<CapabilityGrant> signed = await GovernanceArtifactSigner.SignCapabilityGrantAsync(
            grant,
            new HmacSignatureService(),
            schemaVersion: GovernanceSchemaVersions.StableArtifactsV1,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(GovernanceSchemaVersions.StableArtifactsV1, signed.Artifact.SchemaVersion);
        Assert.DoesNotContain("maxUseCount", signed.CanonicalPayload.CanonicalJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies a use-limited grant cannot be signed under the version 1 schema, which would leave the limit unsigned.
    /// </summary>
    [Fact]
    public async Task SignCapabilityGrantAsyncRejectsVersionOneForUseLimitedGrant()
    {
        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await GovernanceArtifactSigner.SignCapabilityGrantAsync(
                CreateGrant(maxUseCount: 3),
                new HmacSignatureService(),
                schemaVersion: GovernanceSchemaVersions.StableArtifactsV1,
                cancellationToken: TestContext.Current.CancellationToken));

        Assert.Equal("schemaVersion", exception.ParamName);
        Assert.Throws<ArgumentException>(() => GovernanceArtifactSigner.CreateUnsignedCapabilityGrant(
            CreateGrant(maxUseCount: 3),
            schemaVersion: GovernanceSchemaVersions.StableArtifactsV1));
    }

    /// <summary>
    /// Verifies that editing any grant field after signing yields a token whose proof no longer verifies.
    /// </summary>
    /// <param name="propertyName">The grant property to edit.</param>
    /// <param name="replacementJson">The replacement JSON value.</param>
    [Theory]
    [MemberData(nameof(EditedGrantFields))]
    public async Task EditingAnyGrantFieldAfterSigningFailsVerification(string propertyName, string replacementJson)
    {
        var signer = new HmacSignatureService();
        string token = CapabilityGrantToken.Encode(await SignAsync(CreateGrant(), signer));

        string edited = EditDocument(token, document => document["grant"]![propertyName] = JsonNode.Parse(replacementJson));
        CapabilityGrantTokenDecodeResult decoded = CapabilityGrantToken.Decode(edited);

        Assert.True(decoded.Succeeded, decoded.FailureCode);

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            decoded.SignedGrant!,
            CreateBoundOptions(),
            signer,
            new InMemoryCapabilityGrantUseStore(),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsValid);
        Assert.Equal(CapabilityGrantValidationCategory.InvalidProof, result.Category);
    }

    /// <summary>
    /// Verifies that editing the signed policy context in the proof metadata fails verification.
    /// </summary>
    [Fact]
    public async Task EditingSignedPolicyContextFailsVerification()
    {
        var signer = new HmacSignatureService();
        SignedGovernanceArtifact<CapabilityGrant> signed = await GovernanceArtifactSigner.SignCapabilityGrantAsync(
            CreateGrant(),
            signer,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [GovernanceSignatureInput.PolicyVersionMetadataKey] = "proof-policy-v1"
            },
            cancellationToken: TestContext.Current.CancellationToken);
        string token = CapabilityGrantToken.Encode(signed);

        string edited = EditDocument(
            token,
            document => document["proof"]!["metadata"]![GovernanceSignatureInput.PolicyVersionMetadataKey] = "proof-policy-v2");

        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            CapabilityGrantToken.Decode(edited).SignedGrant!,
            CreateBoundOptions(),
            signer,
            new InMemoryCapabilityGrantUseStore(),
            TestContext.Current.CancellationToken);

        Assert.Equal(CapabilityGrantValidationCategory.InvalidProof, result.Category);
    }

    /// <summary>
    /// Verifies canonical descriptors carried in the proof metadata are replaced by the values derived on decode.
    /// </summary>
    [Fact]
    public async Task DecodeDerivesCanonicalDescriptorsInsteadOfReadingThem()
    {
        var signer = new HmacSignatureService();
        string token = CapabilityGrantToken.Encode(await SignAsync(CreateGrant(), signer));

        string edited = EditDocument(token, document =>
        {
            JsonObject proof = document["proof"]!.AsObject();
            JsonObject metadata = proof["metadata"]?.AsObject() ?? [];
            metadata["artifact_type"] = "asibackbone.audit-residue";
            metadata["artifact_id"] = "grant-other";
            proof["metadata"] = metadata;
        });

        SignedGovernanceArtifact<CapabilityGrant> decoded = CapabilityGrantToken.Decode(edited).SignedGrant!;
        CapabilityGrantValidationResult result = await CapabilityGrantValidator.ValidateAsync(
            decoded,
            CreateBoundOptions(),
            signer,
            new InMemoryCapabilityGrantUseStore(),
            TestContext.Current.CancellationToken);

        Assert.Equal(CanonicalArtifactTypes.CapabilityGrant, decoded.SigningMetadata.Metadata["artifact_type"]);
        Assert.Equal("grant-1", decoded.SigningMetadata.Metadata["artifact_id"]);
        Assert.True(result.IsValid, result.FailureCode);
    }

    /// <summary>
    /// Verifies only grant metadata covered by the canonical payload options is transported.
    /// </summary>
    [Fact]
    public async Task OnlyBoundGrantMetadataIsTransported()
    {
        var payloadOptions = CanonicalPayloadOptions.Create(metadataKeyAllowList: ["tenant"]);
        CapabilityGrant grant = CreateGrant(metadata: new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["tenant"] = "tenant-a",
            ["diagnostic"] = "unbound"
        });
        SignedGovernanceArtifact<CapabilityGrant> signed = await GovernanceArtifactSigner.SignCapabilityGrantAsync(
            grant,
            new HmacSignatureService(),
            options: payloadOptions,
            cancellationToken: TestContext.Current.CancellationToken);

        string token = CapabilityGrantToken.Encode(signed, payloadOptions);
        CapabilityGrant decoded = CapabilityGrantToken.Decode(token, payloadOptions).SignedGrant!.Artifact;

        Assert.Equal("tenant-a", decoded.Metadata["tenant"]);
        Assert.False(decoded.Metadata.ContainsKey("diagnostic"));
    }

    /// <summary>
    /// Verifies an unsigned grant cannot be encoded.
    /// </summary>
    [Fact]
    public void EncodeRejectsUnsignedGrant()
    {
        SignedGovernanceArtifact<CapabilityGrant> unsigned = GovernanceArtifactSigner.CreateUnsignedCapabilityGrant(CreateGrant());

        _ = Assert.Throws<InvalidOperationException>(() => CapabilityGrantToken.Encode(unsigned));
    }

    /// <summary>
    /// Verifies encoding with payload options other than the signing options is rejected, because the token could never
    /// verify.
    /// </summary>
    [Fact]
    public async Task EncodeRejectsMismatchedPayloadOptions()
    {
        CapabilityGrant grant = CreateGrant(metadata: new Dictionary<string, string>(StringComparer.Ordinal) { ["tenant"] = "a" });
        SignedGovernanceArtifact<CapabilityGrant> signed = await GovernanceArtifactSigner.SignCapabilityGrantAsync(
            grant,
            new HmacSignatureService(),
            options: CanonicalPayloadOptions.Create(metadataKeyAllowList: ["tenant"]),
            cancellationToken: TestContext.Current.CancellationToken);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => CapabilityGrantToken.Encode(signed));

        Assert.Equal("options", exception.ParamName);
    }

    /// <summary>
    /// Verifies encoding refuses to produce a token longer than the configured limit.
    /// </summary>
    [Fact]
    public async Task EncodeRejectsTokenLongerThanLimit()
    {
        SignedGovernanceArtifact<CapabilityGrant> signed = await SignAsync(CreateGrant(), new HmacSignatureService());

        _ = Assert.Throws<InvalidOperationException>(() => CapabilityGrantToken.Encode(signed, maxTokenLength: 32));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CapabilityGrantToken.Encode(signed, maxTokenLength: 0));
    }

    /// <summary>
    /// Verifies malformed, oversized, and unsupported tokens fail with stable failure codes instead of throwing.
    /// </summary>
    [Fact]
    public async Task DecodeFailsClosedWithStableFailureCodes()
    {
        string token = CapabilityGrantToken.Encode(await SignAsync(CreateGrant(), new HmacSignatureService()));

        AssertDecodeFailure(null, CapabilityGrantToken.MissingFailureCode);
        AssertDecodeFailure("   ", CapabilityGrantToken.MissingFailureCode);
        AssertDecodeFailure(token, CapabilityGrantToken.TooLargeFailureCode, maxTokenLength: token.Length - 1);
        AssertDecodeFailure("asibcg2." + token[CapabilityGrantToken.FormatPrefix.Length..], CapabilityGrantToken.UnsupportedFormatFailureCode);
        AssertDecodeFailure(CapabilityGrantToken.FormatPrefix, CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(CapabilityGrantToken.FormatPrefix + "!!!not-base64url", CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EncodeDocument("not json"), CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EncodeDocument("[]"), CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EncodeDocument(/*lang=json,strict*/ "{\"v\":1}"), CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EncodeDocument(/*lang=json,strict*/ "{\"v\":1,\"v\":1,\"grant\":{},\"proof\":{}}"), CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EncodeDocument(/*lang=json,strict*/ "{\"v\":[[[[[1]]]]]}"), CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EditDocument(token, document => document["v"] = 2), CapabilityGrantToken.UnsupportedFormatFailureCode);
        AssertDecodeFailure(EditDocument(token, document => document["extra"] = true), CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EditDocument(token, document => document["grant"]!["extra"] = "x"), CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EditDocument(token, document => document["grant"]!.AsObject().Remove("issuer")), CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EditDocument(token, document => document["grant"]!["maxUseCount"] = "1"), CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EditDocument(token, document => document["grant"]!["expiresUtc"] = "tomorrow"), CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EditDocument(token, document => document["grant"]!["scopes"] = new JsonArray(1)), CapabilityGrantToken.MalformedFailureCode);
        AssertDecodeFailure(EditDocument(token, document => document["grant"]!["maxUseCount"] = 0), CapabilityGrantToken.InvalidGrantFailureCode);
        AssertDecodeFailure(
            EditDocument(token, document => document["grant"]!["expiresUtc"] = "2026-10-19T12:00:00.0000000Z"),
            CapabilityGrantToken.InvalidGrantFailureCode);
        AssertDecodeFailure(
            EditDocument(token, document => document["proof"]!["hashAlgorithm"] = "MD5"),
            CapabilityGrantToken.UnsupportedHashAlgorithmFailureCode);
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => CapabilityGrantToken.Decode(token, maxTokenLength: 0));
    }

    /// <summary>
    /// Verifies decode failure messages never echo token content.
    /// </summary>
    [Fact]
    public void DecodeFailureMessagesDoNotEchoTokenContent()
    {
        const string Secret = "do-not-echo-this-value";

        CapabilityGrantTokenDecodeResult result = CapabilityGrantToken.Decode(
            EncodeDocument("{\"v\":1,\"" + Secret + "\":1}"));

        Assert.False(result.Succeeded);
        Assert.DoesNotContain(Secret, result.FailureMessage, StringComparison.Ordinal);
    }

    private static void AssertDecodeFailure(string? token, string expectedFailureCode, int maxTokenLength = CapabilityGrantToken.DefaultMaxTokenLength)
    {
        CapabilityGrantTokenDecodeResult result = CapabilityGrantToken.Decode(token, maxTokenLength: maxTokenLength);

        Assert.False(result.Succeeded);
        Assert.Null(result.SignedGrant);
        Assert.Equal(expectedFailureCode, result.FailureCode);
        Assert.False(string.IsNullOrWhiteSpace(result.FailureMessage));
    }

    private static string EncodeDocument(string json)
    {
        return CapabilityGrantToken.FormatPrefix + Base64Url.EncodeToString(Encoding.UTF8.GetBytes(json));
    }

    private static string EditDocument(string token, Action<JsonObject> edit)
    {
        byte[] bytes = Base64Url.DecodeFromChars(token.AsSpan(CapabilityGrantToken.FormatPrefix.Length));
        JsonObject document = JsonNode.Parse(bytes)!.AsObject();
        edit(document);
        return EncodeDocument(document.ToJsonString());
    }

    private static ValueTask<SignedGovernanceArtifact<CapabilityGrant>> SignAsync(
        CapabilityGrant grant,
        IGovernanceSigningService signer)
    {
        return GovernanceArtifactSigner.SignCapabilityGrantAsync(
            grant,
            signer,
            cancellationToken: TestContext.Current.CancellationToken);
    }

    private static CapabilityGrantValidationOptions CreateBoundOptions()
    {
        return CapabilityGrantValidationOptions.CreateBoundExecutionBoundary(
            CapabilityGrantBindingExpectations.Create("user-1", "orders.approve"),
            issuer: "issuer-1",
            audience: "audience-1",
            scopes: ["orders.approve"],
            validationUtc: Now,
            policyVersion: "policy-v1",
            policyHash: "policy-hash-1",
            resourceBinding: "form-sha256:abc");
    }

    private static CapabilityGrant CreateGrant(
        int? maxUseCount = 1,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return CapabilityGrant.Create(
            tokenId: "grant-1",
            issuer: "issuer-1",
            audience: "audience-1",
            scopes: ["orders.approve"],
            issuedUtc: Now.AddMinutes(-5),
            expiresUtc: Now.AddMinutes(5),
            notBeforeUtc: Now.AddMinutes(-5),
            subjectId: "user-1",
            operationName: "orders.approve",
            policyVersion: "policy-v1",
            policyHash: "policy-hash-1",
            acknowledgmentId: "ack-1",
            handshakeId: "handshake-1",
            gatewayBinding: "gateway-1",
            resourceBinding: "form-sha256:abc",
            metadata: metadata,
            maxUseCount: maxUseCount);
    }

    /// <summary>
    /// Signs and verifies with a keyed HMAC over the exact signature input.
    /// </summary>
    private sealed class HmacSignatureService : IGovernanceSigningService, IGovernanceSignatureVerificationService
    {
        private static readonly byte[] Key = [0x41, 0x53, 0x49, 0x42, 0x2D, 0x63, 0x67, 0x2D, 0x74, 0x65, 0x73, 0x74, 0x2D, 0x6B, 0x65, 0x79];

        public ValueTask<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            var signingMetadata = SigningMetadata.Create(
                signingHash: request.SigningHash,
                hashAlgorithm: request.HashAlgorithm,
                signature: Convert.ToBase64String(HMACSHA256.HashData(Key, request.SignatureInput.Span)),
                signatureAlgorithm: "HMAC-SHA256-TEST",
                keyId: "test-key",
                keyVersion: "v1",
                provider: "test-provider",
                signedUtc: Now,
                metadata: request.Metadata);

            return ValueTask.FromResult(SigningResult.FromMetadata(signingMetadata));
        }

        public ValueTask<SignatureVerificationResult> VerifyAsync(
            SignatureVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

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
