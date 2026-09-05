using AsiBackbone.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Tests for the <see cref="AsiBackboneEndpointGovernanceDescriptor"/> class, focusing on caching behavior of metadata generation.
/// </summary>
public sealed class AsiBackboneEndpointGovernanceDescriptorCacheTests
{
    /// <summary>
    /// Tests that the <c>AsiBackboneEndpointGovernanceDescriptor.ToMetadata</c> method returns cached metadata for repeated calls, both for full and reduced metadata modes.
    /// </summary>
    [Fact]
    public void ToMetadataReturnsCachedMetadataForRepeatedHotPathUse()
    {
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new RequireGovernancePolicyAttribute(typeof(SamplePolicy)),
                new RequireLiabilityHandshakeAttribute(),
                new RequireCapabilityGrantAttribute("robotics.execute"),
                new EmitGovernanceAuditAttribute()),
            "sample.robotics.execute");
        var descriptor = AsiBackboneEndpointGovernanceDescriptor.FromEndpoint(endpoint);

        IReadOnlyDictionary<string, string> firstFullMetadata = descriptor.ToMetadata();
        IReadOnlyDictionary<string, string> secondFullMetadata = descriptor.ToMetadata();
        IReadOnlyDictionary<string, string> firstReducedMetadata = descriptor.ToMetadata(AsiBackboneEndpointGovernanceMetadataMode.Reduced);
        IReadOnlyDictionary<string, string> secondReducedMetadata = descriptor.ToMetadata(AsiBackboneEndpointGovernanceMetadataMode.Reduced);

        Assert.Same(firstFullMetadata, secondFullMetadata);
        Assert.Same(firstReducedMetadata, secondReducedMetadata);
        Assert.NotSame(firstFullMetadata, firstReducedMetadata);
        Assert.Equal("sample.robotics.execute", firstFullMetadata["endpoint.operation_name"]);
        Assert.Equal("sample.robotics.execute", firstReducedMetadata["endpoint.operation_name"]);
        Assert.Equal("true", firstFullMetadata["endpoint.requires_liability_handshake"]);
        Assert.Equal("true", firstFullMetadata["endpoint.emit_governance_audit"]);
        Assert.Contains(nameof(SamplePolicy), firstFullMetadata["endpoint.policy_types"], StringComparison.Ordinal);
        Assert.Equal("robotics.execute", firstFullMetadata["endpoint.capability_scopes"]);
        // The policy marker is retained in reduced metadata; the remaining full-mode entries are not.
        Assert.Equal(firstFullMetadata["endpoint.policy_types"], firstReducedMetadata["endpoint.policy_types"]);
        Assert.DoesNotContain("endpoint.capability_scopes", firstReducedMetadata.Keys);
    }

    private sealed class SamplePolicy
    {
    }
}
