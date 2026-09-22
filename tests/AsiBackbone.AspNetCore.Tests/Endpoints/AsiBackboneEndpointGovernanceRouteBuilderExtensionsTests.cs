using System.Reflection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Unit tests for the <see cref="EndpointGovernanceRouteBuilderExtensions"/> class.
/// </summary>
public sealed class AsiBackboneEndpointGovernanceRouteBuilderExtensionsTests
{
    /// <summary>
    /// Tests that the <see cref="EndpointGovernanceRouteBuilderExtensions.MarkGovernancePolicy{TPolicy}(RouteHandlerBuilder)"/> method returns the same <see cref="RouteHandlerBuilder"/> instance.
    /// </summary>
    [Fact]
    public void MarkGovernancePolicy_RouteHandlerBuilder_ReturnsSameBuilder()
    {
        var app = WebApplication.Create();

        RouteHandlerBuilder routeBuilder = app.MapGet(
            "/governed",
            static () => Microsoft.AspNetCore.Http.Results.Ok());

        RouteHandlerBuilder returned = routeBuilder.MarkGovernancePolicy<TestDecisionPolicy>();

        Assert.Same(routeBuilder, returned);

        Endpoint endpoint = Assert.Single(((IEndpointRouteBuilder)app).DataSources.SelectMany(source => source.Endpoints));
        GovernancePolicyAttribute metadata =
            Assert.Single(endpoint.Metadata.OfType<GovernancePolicyAttribute>());
        Assert.Equal(typeof(TestDecisionPolicy), metadata.PolicyType);
    }

    /// <summary>
    /// Tests that the <c>MarkGovernancePolicy(IEndpointConventionBuilder, Type)</c> method adds the correct metadata to the endpoint and returns the same builder instance.
    /// </summary>
    [Fact]
    public void MarkGovernancePolicy_EndpointConventionBuilder_AddsPolicyMetadataAndReturnsSameBuilder()
    {
        var builder = new CapturingEndpointConventionBuilder();

        CapturingEndpointConventionBuilder returned =
            builder.MarkGovernancePolicy(typeof(TestPolicy));

        Assert.Same(builder, returned);

        EndpointBuilder endpointBuilder = CreateEndpointBuilder();
        Action<EndpointBuilder> convention = Assert.Single(builder.Conventions);
        convention(endpointBuilder);

        GovernancePolicyAttribute metadata =
            Assert.Single(endpointBuilder.Metadata.OfType<GovernancePolicyAttribute>());

        Assert.Equal(typeof(TestPolicy), metadata.PolicyType);
    }

    /// <summary>
    /// Tests that the <c>EndpointGovernanceRouteBuilderExtensions.RequireAcknowledgment(IEndpointConventionBuilder)</c> method adds the correct metadata to the endpoint and returns the same <see cref="IEndpointConventionBuilder"/> instance.
    /// </summary>
    [Fact]
    public void RequireAcknowledgment_AddsMetadataAndReturnsSameBuilder()
    {
        var builder = new CapturingEndpointConventionBuilder();

        CapturingEndpointConventionBuilder returned = builder.RequireAcknowledgment();

        Assert.Same(builder, returned);

        EndpointBuilder endpointBuilder = CreateEndpointBuilder();
        Action<EndpointBuilder> convention = Assert.Single(builder.Conventions);
        convention(endpointBuilder);

        _ = Assert.Single(endpointBuilder.Metadata.OfType<RequireAcknowledgmentAttribute>());
    }

    /// <summary>
    /// Tests that the <c>EndpointGovernanceRouteBuilderExtensions.RequireCapabilityGrant(IEndpointConventionBuilder, string)</c> method adds the correct metadata to the endpoint and returns the same <see cref="IEndpointConventionBuilder"/> instance.
    /// </summary>
    [Fact]
    public void RequireCapabilityGrant_AddsMetadataAndReturnsSameBuilder()
    {
        var builder = new CapturingEndpointConventionBuilder();

        CapturingEndpointConventionBuilder returned = builder.RequireCapabilityGrant(" payments.approve ");

        Assert.Same(builder, returned);

        EndpointBuilder endpointBuilder = CreateEndpointBuilder();
        Action<EndpointBuilder> convention = Assert.Single(builder.Conventions);
        convention(endpointBuilder);

        RequireCapabilityGrantAttribute metadata =
            Assert.Single(endpointBuilder.Metadata.OfType<RequireCapabilityGrantAttribute>());

        Assert.Equal("payments.approve", metadata.Scope);
    }

    /// <summary>
    /// Tests that the <c>EndpointGovernanceRouteBuilderExtensions.EmitGovernanceAudit(IEndpointConventionBuilder)</c> method adds the correct metadata to the endpoint and returns the same <see cref="IEndpointConventionBuilder"/> instance.
    /// </summary>
    [Fact]
    public void EmitGovernanceAudit_AddsMetadataAndReturnsSameBuilder()
    {
        var builder = new CapturingEndpointConventionBuilder();

        CapturingEndpointConventionBuilder returned = builder.EmitGovernanceAudit();

        Assert.Same(builder, returned);

        EndpointBuilder endpointBuilder = CreateEndpointBuilder();
        Action<EndpointBuilder> convention = Assert.Single(builder.Conventions);
        convention(endpointBuilder);

        _ = Assert.Single(endpointBuilder.Metadata.OfType<EmitGovernanceAuditAttribute>());
    }

    /// <summary>
    /// Tests that the <c>MarkGovernancePolicy(IEndpointConventionBuilder, Type)</c> method throws an <see cref="ArgumentNullException"/> when the policy type is null.
    /// </summary>
    [Fact]
    public void MarkGovernancePolicy_ThrowsWhenPolicyTypeIsNull()
    {
        var builder = new CapturingEndpointConventionBuilder();

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder.MarkGovernancePolicy(null!));

        Assert.Equal("policyType", exception.ParamName);
    }

    /// <summary>
    /// Tests that the <c>EndpointGovernanceRouteBuilderExtensions.RequireAcknowledgment(IEndpointConventionBuilder)</c> method throws an <see cref="ArgumentNullException"/> when the builder is null.
    /// </summary>
    [Fact]
    public void MetadataExtensions_ThrowWhenBuilderIsNull()
    {
        CapturingEndpointConventionBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder!.RequireAcknowledgment());

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Tests that the <c>EndpointGovernanceRouteBuilderExtensions.AddEndpointMetadata{TBuilder}(TBuilder, object)</c> method throws an <see cref="ArgumentNullException"/> when the metadata is null.
    /// </summary>
    [Fact]
    public void AddEndpointMetadata_ThrowsWhenMetadataIsNull()
    {
        MethodInfo method = typeof(EndpointGovernanceRouteBuilderExtensions)
            .GetMethod("AddEndpointMetadata", BindingFlags.NonPublic | BindingFlags.Static)!
            .MakeGenericMethod(typeof(CapturingEndpointConventionBuilder));

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(
                null,
                [new CapturingEndpointConventionBuilder(), null!]));

        ArgumentNullException innerException =
            Assert.IsType<ArgumentNullException>(exception.InnerException);

        Assert.Equal("metadata", innerException.ParamName);
    }

    /// <summary>
    /// Tests that the <c>EndpointGovernanceRouteBuilderExtensions.AllowMissingGovernanceMetadata(IEndpointConventionBuilder)</c> method adds the correct metadata to the endpoint and returns the same <see cref="IEndpointConventionBuilder"/> instance.
    /// </summary>
    [Fact]
    public void AllowMissingGovernanceMetadata_AddsMetadataAndReturnsSameBuilder()
    {
        var builder = new CapturingEndpointConventionBuilder();

        CapturingEndpointConventionBuilder returned = builder.AllowMissingGovernanceMetadata();

        Assert.Same(builder, returned);

        EndpointBuilder endpointBuilder = CreateEndpointBuilder();
        Action<EndpointBuilder> convention = Assert.Single(builder.Conventions);
        convention(endpointBuilder);

        _ = Assert.Single(endpointBuilder.Metadata.OfType<AllowMissingGovernanceMetadataAttribute>());
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

    private sealed class TestPolicy
    {
    }

    private sealed class TestDecisionPolicy : IGovernanceDecisionPolicy<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            GovernanceEvaluationContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(composedDecision);
        }
    }
}
