using AsiBackbone.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Tests for the AsiBackboneEndpointPolicyEvaluationOptionsMetadata class, which is responsible for reading and writing policy evaluation options metadata from ASP.NET Core endpoints.
/// </summary>
public sealed class AsiBackboneEndpointPolicyEvaluationOptionsMetadataTests
{
    /// <summary>
    /// Tests that the EndpointGovernanceDescriptor correctly reads the ShortCircuitOnFirstDenial metadata from an endpoint and short-circuits on the first denial.
    /// </summary>
    [Fact]
    public void DescriptorReadsShortCircuitOnFirstDenialMetadataFromEndpoint()
    {
        var endpoint = new Endpoint(
            static context => Task.CompletedTask,
            new EndpointMetadataCollection(
                new RequireGovernancePolicyAttribute(typeof(SamplePolicy)),
                new ShortCircuitOnFirstDenialAttribute()),
            "sample.fast-abort");

        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IReadOnlyDictionary<string, string> metadata = descriptor.ToMetadata();

        Assert.True(descriptor.HasGovernanceMetadata);
        Assert.True(descriptor.ShortCircuitOnFirstDenial.HasValue);
        Assert.True(descriptor.ShortCircuitOnFirstDenial.Value);
        Assert.Equal("true", metadata["endpoint.short_circuit_on_first_denial"]);
    }

    /// <summary>
    /// Tests that the EndpointGovernanceDescriptor correctly reads the ShortCircuitOnFirstDenial metadata from an endpoint and uses the last value when multiple attributes are present.
    /// </summary>
    [Fact]
    public void DescriptorUsesLastShortCircuitOnFirstDenialMetadataValue()
    {
        var endpoint = new Endpoint(
            static context => Task.CompletedTask,
            new EndpointMetadataCollection(
                new ShortCircuitOnFirstDenialAttribute(),
                new ShortCircuitOnFirstDenialAttribute(enabled: false)),
            "sample.fast-abort.override");

        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IReadOnlyDictionary<string, string> metadata = descriptor.ToMetadata();

        Assert.True(descriptor.HasGovernanceMetadata);
        Assert.True(descriptor.ShortCircuitOnFirstDenial.HasValue);
        Assert.False(descriptor.ShortCircuitOnFirstDenial.Value);
        Assert.Equal("false", metadata["endpoint.short_circuit_on_first_denial"]);
    }

    /// <summary>
    /// Tests that the RouteBuilderShortCircuitOnFirstDenial extension method adds the ShortCircuitOnFirstDenial metadata to the endpoint and returns the same builder instance.
    /// </summary>
    [Fact]
    public void RouteBuilderShortCircuitOnFirstDenialAddsMetadataAndReturnsSameBuilder()
    {
        var builder = new CapturingEndpointConventionBuilder();

        CapturingEndpointConventionBuilder returned = builder.ShortCircuitOnFirstDenial();

        Assert.Same(builder, returned);

        EndpointBuilder endpointBuilder = CreateEndpointBuilder();
        Action<EndpointBuilder> convention = Assert.Single(builder.Conventions);
        convention(endpointBuilder);

        ShortCircuitOnFirstDenialAttribute metadata =
            Assert.Single(endpointBuilder.Metadata.OfType<ShortCircuitOnFirstDenialAttribute>());

        Assert.True(metadata.ShortCircuitOnFirstDenial.HasValue);
        Assert.True(metadata.ShortCircuitOnFirstDenial.Value);
    }

    /// <summary>
    /// Tests that the RouteBuilderShortCircuitOnFirstDenial extension method can disable the ShortCircuitOnFirstDenial metadata value when called with enabled: false.
    /// </summary>
    [Fact]
    public void RouteBuilderShortCircuitOnFirstDenialCanDisableMetadataValue()
    {
        var builder = new CapturingEndpointConventionBuilder();

        _ = builder.ShortCircuitOnFirstDenial(enabled: false);

        EndpointBuilder endpointBuilder = CreateEndpointBuilder();
        Action<EndpointBuilder> convention = Assert.Single(builder.Conventions);
        convention(endpointBuilder);

        ShortCircuitOnFirstDenialAttribute metadata =
            Assert.Single(endpointBuilder.Metadata.OfType<ShortCircuitOnFirstDenialAttribute>());

        Assert.True(metadata.ShortCircuitOnFirstDenial.HasValue);
        Assert.False(metadata.ShortCircuitOnFirstDenial.Value);
    }

    private static RouteEndpointBuilder CreateEndpointBuilder()
    {
        return new RouteEndpointBuilder(
            static _ => Task.CompletedTask,
            RoutePatternFactory.Parse("/"),
            order: 0);
    }

    private sealed class CapturingEndpointConventionBuilder : IEndpointConventionBuilder
    {
        public List<Action<EndpointBuilder>> Conventions { get; } = [];

        public void Add(Action<EndpointBuilder> convention)
        {
            ArgumentNullException.ThrowIfNull(convention);
            Conventions.Add(convention);
        }
    }

    private sealed class SamplePolicy
    {
    }
}
