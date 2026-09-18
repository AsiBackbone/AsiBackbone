using System.Text;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Locks the version 1 signature input bytes and the signing requests that carry them.
/// </summary>
public sealed class GovernanceSignatureInputTests
{
    /// <summary>
    /// Locks the version 1 signature input as canonical JSON with ordinal property order and the bound policy context, so
    /// non-.NET signers and verifiers can reproduce it byte for byte.
    /// </summary>
    [Fact]
    public void CreateV1MatchesGoldenCanonicalJson()
    {
        CanonicalPayloadHash hash = CreateHash();
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [GovernanceSignatureInput.PolicyVersionMetadataKey] = " policy-v1 ",
            [GovernanceSignatureInput.PolicyHashMetadataKey] = "policy-hash-1",
            ["unbound_label"] = "ignored"
        };

        ReadOnlyMemory<byte> input = GovernanceSignatureInput.CreateV1(hash, metadata);

        const string expectedJson = /*lang=json,strict*/ "{\"artifactId\":\"artifact-1\",\"artifactType\":\"artifact-type\",\"canonicalizationVersion\":\"asibackbone.canonical-json.v1\",\"format\":\"asibackbone.signature-input.v1\",\"hashAlgorithm\":\"SHA-256\",\"hashValue\":\"0123abcd\",\"payloadSchemaVersion\":\"schema-v1\",\"policyHash\":\"policy-hash-1\",\"policyVersion\":\"policy-v1\"}";

        Assert.Equal(expectedJson, Encoding.UTF8.GetString(input.Span));
    }

    /// <summary>
    /// Verifies that an absent, empty, or whitespace-only policy value is bound as JSON null rather than omitted, so a
    /// policy label added after signing changes the input.
    /// </summary>
    [Fact]
    public void CreateV1BindsMissingPolicyContextAsNull()
    {
        CanonicalPayloadHash hash = CreateHash();
        Dictionary<string, string> blankMetadata = new(StringComparer.Ordinal)
        {
            [GovernanceSignatureInput.PolicyVersionMetadataKey] = " ",
            [GovernanceSignatureInput.PolicyHashMetadataKey] = string.Empty
        };

        string withoutMetadata = Encoding.UTF8.GetString(GovernanceSignatureInput.CreateV1(hash).Span);
        string withBlankMetadata = Encoding.UTF8.GetString(GovernanceSignatureInput.CreateV1(hash, blankMetadata).Span);

        Assert.Contains("\"policyHash\":null", withoutMetadata, StringComparison.Ordinal);
        Assert.Contains("\"policyVersion\":null", withoutMetadata, StringComparison.Ordinal);
        Assert.Equal(withoutMetadata, withBlankMetadata);
    }

    /// <summary>
    /// Verifies that a changed policy label produces a different version 1 input.
    /// </summary>
    [Fact]
    public void CreateV1ChangesWhenPolicyContextChanges()
    {
        CanonicalPayloadHash hash = CreateHash();
        Dictionary<string, string> original = new(StringComparer.Ordinal)
        {
            [GovernanceSignatureInput.PolicyVersionMetadataKey] = "policy-v1"
        };
        Dictionary<string, string> relabeled = new(StringComparer.Ordinal)
        {
            [GovernanceSignatureInput.PolicyVersionMetadataKey] = "policy-v2"
        };

        ReadOnlyMemory<byte> originalInput = GovernanceSignatureInput.CreateV1(hash, original);
        ReadOnlyMemory<byte> relabeledInput = GovernanceSignatureInput.CreateV1(hash, relabeled);

        Assert.False(originalInput.Span.SequenceEqual(relabeledInput.Span));
    }

    /// <summary>
    /// Verifies that the legacy input is the UTF-8 text of the trimmed signing hash, and differs from the version 1 input.
    /// </summary>
    [Fact]
    public void CreateLegacyIsTheSigningHashTextAndDiffersFromV1()
    {
        CanonicalPayloadHash hash = CreateHash();

        ReadOnlyMemory<byte> legacyInput = GovernanceSignatureInput.CreateLegacy(" 0123abcd ");

        Assert.Equal("0123abcd", Encoding.UTF8.GetString(legacyInput.Span));
        Assert.False(legacyInput.Span.SequenceEqual(GovernanceSignatureInput.CreateV1(hash).Span));
    }

    /// <summary>
    /// Verifies that a request constructed without an explicit input falls back to the legacy input and reports it.
    /// </summary>
    [Fact]
    public void SigningRequestWithoutExplicitInputUsesLegacyInput()
    {
        var request = new SigningRequest("0123abcd", "SHA-256");

        Assert.True(request.UsesLegacySignatureInput);
        Assert.Equal("0123abcd", Encoding.UTF8.GetString(request.SignatureInput.Span));
    }

    /// <summary>
    /// Verifies that the artifact signer supplies the version 1 input with the requested policy context.
    /// </summary>
    [Fact]
    public void CreateSigningRequestSuppliesVersionOneInput()
    {
        CanonicalPayloadHash hash = CreateHash();
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [GovernanceSignatureInput.PolicyVersionMetadataKey] = "policy-v1"
        };

        SigningRequest request = GovernanceArtifactSigner.CreateSigningRequest(hash, metadata: metadata);

        Assert.False(request.UsesLegacySignatureInput);
        Assert.True(request.SignatureInput.Span.SequenceEqual(GovernanceSignatureInput.CreateV1(hash, metadata).Span));
    }

    /// <summary>
    /// Verifies that the request copies the supplied input, so later mutation of the caller's buffer cannot change what is
    /// signed.
    /// </summary>
    [Fact]
    public void SigningRequestCopiesSuppliedInput()
    {
        byte[] buffer = [1, 2, 3];
        var request = new SigningRequest("0123abcd", "SHA-256")
        {
            SignatureInput = buffer
        };

        buffer[0] = 9;

        Assert.Equal(1, request.SignatureInput.Span[0]);
    }

    private static CanonicalPayloadHash CreateHash()
    {
        return CanonicalPayloadHash.Create(
            "artifact-type",
            "artifact-1",
            "schema-v1",
            CanonicalPayloadOptions.DefaultCanonicalizationVersion,
            CanonicalPayloadOptions.DefaultHashAlgorithm,
            "0123ABCD");
    }
}
