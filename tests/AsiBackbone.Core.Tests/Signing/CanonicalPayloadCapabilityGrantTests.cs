using AsiBackbone.Core.CapabilityTokens;
using AsiBackbone.Core.Signing;
using Xunit;

namespace AsiBackbone.Core.Tests.Signing;

/// <summary>
/// Tests that the capability token grant canonical payload binds every field the validator enforces.
/// </summary>
public sealed class CanonicalPayloadCapabilityGrantTests
{
    private static readonly DateTimeOffset IssuedUtc = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// The grant fields that must appear in the canonical payload for the hash to bind the whole grant.
    /// </summary>
    public static TheoryData<string> BoundFieldNames =>
    [
        "acknowledgmentId",
        "audience",
        "expiresUtc",
        "gatewayBinding",
        "handshakeId",
        "issuedUtc",
        "issuer",
        "metadata",
        "notBeforeUtc",
        "operationName",
        "policyHash",
        "policyVersion",
        "resourceBinding",
        "scopes",
        "subjectId",
        "tokenId"
    ];

    /// <summary>
    /// Verifies the payload carries the artifact descriptors that identify what was signed.
    /// </summary>
    [Fact]
    public void ForCapabilityTokenGrantSetsArtifactDescriptors()
    {
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityTokenGrant(CreateGrant());

        Assert.Equal(CanonicalArtifactTypes.CapabilityTokenGrant, payload.ArtifactType);
        Assert.Equal("token-1", payload.ArtifactId);
    }

    /// <summary>
    /// Verifies that every grant field appears in the canonical JSON that is actually hashed.
    /// </summary>
    /// <param name="fieldName">The canonical content key expected to be present.</param>
    [Theory]
    [MemberData(nameof(BoundFieldNames))]
    public void ForCapabilityTokenGrantBindsEveryGrantField(string fieldName)
    {
        CanonicalPayload payload = CanonicalPayloadBuilder.ForCapabilityTokenGrant(CreateGrant());

        Assert.Contains($"\"{fieldName}\":", payload.CanonicalJson, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that changing any single enforced field changes the hash, which is what makes the hash a binding.
    /// </summary>
    [Fact]
    public void ChangingAnyEnforcedFieldChangesTheHash()
    {
        string baseline = HashOf(CreateGrant());

        Assert.NotEqual(baseline, HashOf(CreateGrant(notBeforeUtc: IssuedUtc.AddMinutes(1))));
        Assert.NotEqual(baseline, HashOf(CreateGrant(policyVersion: "policy-v2")));
        Assert.NotEqual(baseline, HashOf(CreateGrant(policyHash: "policy-hash-2")));
        Assert.NotEqual(baseline, HashOf(CreateGrant(acknowledgmentId: "ack-2")));
        Assert.NotEqual(baseline, HashOf(CreateGrant(handshakeId: "handshake-2")));
        Assert.NotEqual(baseline, HashOf(CreateGrant(gatewayBinding: "gateway-2")));
        Assert.NotEqual(baseline, HashOf(CreateGrant(resourceBinding: "resource-2")));
        Assert.NotEqual(baseline, HashOf(CreateGrant(subjectId: "subject-2")));
        Assert.NotEqual(baseline, HashOf(CreateGrant(operationName: "operation-2")));
        Assert.NotEqual(baseline, HashOf(CreateGrant(issuedUtc: IssuedUtc.AddSeconds(1))));
    }

    /// <summary>
    /// Verifies that metadata outside the configured allow-list is not bound by the hash.
    /// </summary>
    /// <remarks>
    /// The default allow-list is empty, so by default no grant metadata reaches the proof at all. This keeps
    /// unbounded and potentially sensitive host data out of hashed payloads, but it also means a host that puts
    /// security-relevant data in grant metadata gets no binding for it until that key is allow-listed.
    /// </remarks>
    [Fact]
    public void MetadataOutsideTheAllowListIsNotBound()
    {
        string baseline = HashOf(CreateGrant());
        string withMetadata = HashOf(CreateGrant(
            metadata: new Dictionary<string, string>(StringComparer.Ordinal) { ["region"] = "eu" }));

        Assert.Equal(baseline, withMetadata);
    }

    /// <summary>
    /// Verifies that metadata inside the configured allow-list is bound by the hash.
    /// </summary>
    [Fact]
    public void AllowListedMetadataIsBound()
    {
        var options = CanonicalPayloadOptions.Create(metadataKeyAllowList: ["region"]);

        string withoutRegion = HashWith(CreateGrant(), options);
        string withRegion = HashWith(
            CreateGrant(metadata: new Dictionary<string, string>(StringComparer.Ordinal) { ["region"] = "eu" }),
            options);
        string withDifferentRegion = HashWith(
            CreateGrant(metadata: new Dictionary<string, string>(StringComparer.Ordinal) { ["region"] = "us" }),
            options);

        Assert.NotEqual(withoutRegion, withRegion);
        Assert.NotEqual(withRegion, withDifferentRegion);
    }

    private static string HashWith(CapabilityTokenGrant grant, CanonicalPayloadOptions options)
    {
        return CanonicalPayloadHasher
            .ComputeHash(CanonicalPayloadBuilder.ForCapabilityTokenGrant(grant, options))
            .HashValue;
    }

    /// <summary>
    /// Verifies that scope ordering and duplication do not change the hash, so equivalent grants agree.
    /// </summary>
    [Fact]
    public void ScopeOrderAndDuplicationDoNotChangeTheHash()
    {
        string ordered = HashOf(CreateGrant(scopes: ["a.execute", "b.execute"]));
        string reordered = HashOf(CreateGrant(scopes: ["b.execute", "a.execute"]));
        string duplicated = HashOf(CreateGrant(scopes: ["b.execute", "a.execute", "a.execute"]));

        Assert.Equal(ordered, reordered);
        Assert.Equal(ordered, duplicated);
    }

    /// <summary>
    /// Verifies that a different scope set still changes the hash, so normalization does not erase meaning.
    /// </summary>
    [Fact]
    public void DifferentScopeSetChangesTheHash()
    {
        Assert.NotEqual(
            HashOf(CreateGrant(scopes: ["a.execute"])),
            HashOf(CreateGrant(scopes: ["a.execute", "b.execute"])));
    }

    /// <summary>
    /// Verifies that the builder rejects a null grant.
    /// </summary>
    [Fact]
    public void ForCapabilityTokenGrantRejectsNullGrant()
    {
        _ = Assert.Throws<ArgumentNullException>(
            () => CanonicalPayloadBuilder.ForCapabilityTokenGrant(null!));
    }

    private static string HashOf(CapabilityTokenGrant grant)
    {
        return CanonicalPayloadHasher.ComputeHash(CanonicalPayloadBuilder.ForCapabilityTokenGrant(grant)).HashValue;
    }

    private static CapabilityTokenGrant CreateGrant(
        IEnumerable<string>? scopes = null,
        DateTimeOffset? issuedUtc = null,
        DateTimeOffset? notBeforeUtc = null,
        string? subjectId = "subject-1",
        string? operationName = "operation-1",
        string? policyVersion = "policy-v1",
        string? policyHash = "policy-hash-1",
        string? acknowledgmentId = "ack-1",
        string? handshakeId = "handshake-1",
        string? gatewayBinding = "gateway-1",
        string? resourceBinding = "resource-1",
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        return CapabilityTokenGrant.Create(
            tokenId: "token-1",
            issuer: "issuer-1",
            audience: "audience-1",
            scopes: scopes ?? ["a.execute"],
            issuedUtc: issuedUtc ?? IssuedUtc,
            expiresUtc: IssuedUtc.AddMinutes(10),
            notBeforeUtc: notBeforeUtc,
            subjectId: subjectId,
            operationName: operationName,
            policyVersion: policyVersion,
            policyHash: policyHash,
            acknowledgmentId: acknowledgmentId,
            handshakeId: handshakeId,
            gatewayBinding: gatewayBinding,
            resourceBinding: resourceBinding,
            metadata: metadata);
    }
}
