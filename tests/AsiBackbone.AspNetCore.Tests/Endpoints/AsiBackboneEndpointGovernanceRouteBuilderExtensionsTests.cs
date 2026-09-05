using System.Reflection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Patterns;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Unit tests for the <see cref="AsiBackboneEndpointGovernanceRouteBuilderExtensions"/> class.
/// </summary>
public sealed class AsiBackboneEndpointGovernanceRouteBuilderExtensionsTests
{
    /// <summary>
    /// Tests that the <see cref="AsiBackboneEndpointGovernanceRouteBuilderExtensions.MarkGovernancePolicy{TPolicy}(RouteHandlerBuilder)"/> method returns the same <see cref="RouteHandlerBuilder"/> instance.
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

        RequireGovernancePolicyAttribute metadata =
            Assert.Single(endpointBuilder.Metadata.OfType<RequireGovernancePolicyAttribute>());

        Assert.Equal(typeof(TestPolicy), metadata.PolicyType);
    }

    /// <summary>
    /// Tests that the obsolete generic marker still records the same metadata as its replacement so existing callers keep working.
    /// </summary>
    [Fact]
    public void ObsoleteRequireGovernancePolicy_RecordsSameMetadataAsMarkGovernancePolicy()
    {
        var obsoleteBuilder = new CapturingEndpointConventionBuilder();
        var currentBuilder = new CapturingEndpointConventionBuilder();

        // The obsolete overloads are exercised deliberately: they remain the supported path for existing callers
        // until they are removed, so their forwarding behavior needs coverage.
#pragma warning disable CS0618 // Type or member is obsolete
        _ = obsoleteBuilder.RequireGovernancePolicy(typeof(TestPolicy));
#pragma warning restore CS0618
        _ = currentBuilder.MarkGovernancePolicy(typeof(TestPolicy));

        EndpointBuilder obsoleteEndpoint = CreateEndpointBuilder();
        EndpointBuilder currentEndpoint = CreateEndpointBuilder();
        Assert.Single(obsoleteBuilder.Conventions)(obsoleteEndpoint);
        Assert.Single(currentBuilder.Conventions)(currentEndpoint);

        RequireGovernancePolicyAttribute obsoleteMetadata =
            Assert.Single(obsoleteEndpoint.Metadata.OfType<RequireGovernancePolicyAttribute>());
        RequireGovernancePolicyAttribute currentMetadata =
            Assert.Single(currentEndpoint.Metadata.OfType<RequireGovernancePolicyAttribute>());

        Assert.Equal(currentMetadata.PolicyType, obsoleteMetadata.PolicyType);
    }

    /// <summary>
    /// Tests that the obsolete overloads carry an <see cref="ObsoleteAttribute"/> pointing callers at the replacement.
    /// </summary>
    [Fact]
    public void ObsoleteRequireGovernancePolicyOverloadsNameTheReplacement()
    {
        MethodInfo[] obsoleteOverloads = [.. typeof(AsiBackboneEndpointGovernanceRouteBuilderExtensions)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == "RequireGovernancePolicy")];

        Assert.Equal(2, obsoleteOverloads.Length);
        Assert.All(obsoleteOverloads, method =>
        {
            ObsoleteAttribute? obsolete = method.GetCustomAttribute<ObsoleteAttribute>();
            Assert.NotNull(obsolete);
            Assert.Contains("MarkGovernancePolicy", obsolete.Message, StringComparison.Ordinal);
        });
    }

    /// <summary>
    /// Tests that the <c>AsiBackboneEndpointGovernanceRouteBuilderExtensions.RequireLiabilityHandshake(IEndpointConventionBuilder)</c> method adds the correct metadata to the endpoint and returns the same <see cref="IEndpointConventionBuilder"/> instance.
    /// </summary>
    [Fact]
    public void RequireLiabilityHandshake_AddsMetadataAndReturnsSameBuilder()
    {
        var builder = new CapturingEndpointConventionBuilder();

        CapturingEndpointConventionBuilder returned = builder.RequireLiabilityHandshake();

        Assert.Same(builder, returned);

        EndpointBuilder endpointBuilder = CreateEndpointBuilder();
        Action<EndpointBuilder> convention = Assert.Single(builder.Conventions);
        convention(endpointBuilder);

        _ = Assert.Single(endpointBuilder.Metadata.OfType<RequireLiabilityHandshakeAttribute>());
    }

    /// <summary>
    /// Tests that the <c>AsiBackboneEndpointGovernanceRouteBuilderExtensions.RequireCapabilityGrant(IEndpointConventionBuilder, string)</c> method adds the correct metadata to the endpoint and returns the same <see cref="IEndpointConventionBuilder"/> instance.
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
    /// Tests that the <c>AsiBackboneEndpointGovernanceRouteBuilderExtensions.EmitGovernanceAudit(IEndpointConventionBuilder)</c> method adds the correct metadata to the endpoint and returns the same <see cref="IEndpointConventionBuilder"/> instance.
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
    /// Tests that the <c>AsiBackboneEndpointGovernanceRouteBuilderExtensions.RequireLiabilityHandshake(IEndpointConventionBuilder)</c> method throws an <see cref="ArgumentNullException"/> when the builder is null.
    /// </summary>
    [Fact]
    public void MetadataExtensions_ThrowWhenBuilderIsNull()
    {
        CapturingEndpointConventionBuilder? builder = null;

        ArgumentNullException exception = Assert.Throws<ArgumentNullException>(
            () => builder!.RequireLiabilityHandshake());

        Assert.Equal("builder", exception.ParamName);
    }

    /// <summary>
    /// Tests that the <c>AsiBackboneEndpointGovernanceRouteBuilderExtensions.AddEndpointMetadata{TBuilder}(TBuilder, object)</c> method throws an <see cref="ArgumentNullException"/> when the metadata is null.
    /// </summary>
    [Fact]
    public void AddEndpointMetadata_ThrowsWhenMetadataIsNull()
    {
        MethodInfo method = typeof(AsiBackboneEndpointGovernanceRouteBuilderExtensions)
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
    /// Tests that the <c>AsiBackboneEndpointGovernanceRouteBuilderExtensions.AllowMissingGovernanceMetadata(IEndpointConventionBuilder)</c> method adds the correct metadata to the endpoint and returns the same <see cref="IEndpointConventionBuilder"/> instance.
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

    private sealed class TestDecisionPolicy : IAsiBackboneDecisionPolicy<AsiBackboneConstraintEvaluationContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            AsiBackboneConstraintEvaluationContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(composedDecision);
        }
    }
}
