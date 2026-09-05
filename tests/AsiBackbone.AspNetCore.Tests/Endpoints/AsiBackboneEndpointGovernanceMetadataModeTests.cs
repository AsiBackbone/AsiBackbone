using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Tests for the AsiBackboneEndpointGovernanceMetadataMode enumeration and its effects on endpoint governance metadata generation and evaluation.
/// </summary>
public sealed class AsiBackboneEndpointGovernanceMetadataModeTests
{
    /// <summary>
    /// Tests that the default behavior of converting an AsiBackboneEndpointGovernanceDescriptor to metadata includes all expected metadata entries when using the full metadata mode.
    /// </summary>
    [Fact]
    public void DescriptorToMetadataDefaultsToFullMetadata()
    {
        AsiBackboneEndpointGovernanceDescriptor descriptor = CreateDescriptor();

        IReadOnlyDictionary<string, string> metadata = descriptor.ToMetadata();

        Assert.Equal("sample.metadata", metadata["endpoint.operation_name"]);
        Assert.Equal("true", metadata["endpoint.requires_liability_handshake"]);
        Assert.Equal("true", metadata["endpoint.emit_governance_audit"]);
        Assert.Contains(typeof(SamplePolicy).FullName!, metadata["endpoint.policy_types"], StringComparison.Ordinal);
        Assert.Equal("robotics.execute", metadata["endpoint.capability_scopes"]);
    }

    /// <summary>
    /// Tests that reduced metadata mode keeps the operation name and the policy marker while excluding the remaining entries.
    /// </summary>
    [Fact]
    public void DescriptorToMetadataCanUseReducedMode()
    {
        AsiBackboneEndpointGovernanceDescriptor descriptor = CreateDescriptor();

        IReadOnlyDictionary<string, string> metadata = descriptor.ToMetadata(AsiBackboneEndpointGovernanceMetadataMode.Reduced);

        Assert.Equal("sample.metadata", metadata["endpoint.operation_name"]);

        // The policy marker survives reduction so a host decision policy keyed on it cannot be silently disabled by a
        // metadata setting. Everything else is still dropped from the hot path.
        Assert.Equal(
            descriptor.ToMetadata()["endpoint.policy_types"],
            metadata["endpoint.policy_types"]);
        Assert.False(metadata.ContainsKey("endpoint.requires_liability_handshake"));
        Assert.False(metadata.ContainsKey("endpoint.emit_governance_audit"));
        Assert.False(metadata.ContainsKey("endpoint.capability_scopes"));
    }

    /// <summary>
    /// Tests that when the AsiBackboneEndpointGovernanceService is configured to use reduced metadata mode, the policy evaluator receives only the reduced metadata during evaluation.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the endpoint governance with reduced metadata mode.
    /// </returns>
    [Fact]
    public async Task DefaultServicePassesReducedMetadataToPolicyEvaluator()
    {
        var evaluator = new CapturingPolicyEvaluator();
        using ServiceProvider services = new ServiceCollection()
            .Configure<AsiBackboneEndpointGovernanceOptions>(options => options.MetadataMode = AsiBackboneEndpointGovernanceMetadataMode.Reduced)
            .AddAsiBackboneAspNetCore()
            .AddSingleton<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>>(evaluator)
            .BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = services.CreateScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            TraceIdentifier = "trace-reduced-metadata"
        };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new RequireGovernancePolicyAttribute(typeof(SamplePolicy)),
                new RequireLiabilityHandshakeAttribute()),
            "sample.metadata.reduced");
        var descriptor = AsiBackboneEndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IAsiBackboneEndpointGovernanceService service = scope.ServiceProvider.GetRequiredService<IAsiBackboneEndpointGovernanceService>();

        AsiBackboneEndpointGovernanceResult result = await service.EvaluateAsync(httpContext, descriptor, TestContext.Current.CancellationToken);

        Assert.True(result.CanExecute);
        Assert.NotNull(evaluator.CapturedMetadata);

        // Reduced mode keeps the policy marker. Dropping it would let a metadata setting silently disable a host
        // decision policy that varies its outcome by policy type, which is a permissive change made by the wrong knob.
        Assert.Equal(2, evaluator.CapturedMetadata.Count);
        Assert.Equal("sample.metadata.reduced", evaluator.CapturedMetadata["endpoint.operation_name"]);
        Assert.Equal(
            descriptor.ToMetadata(AsiBackboneEndpointGovernanceMetadataMode.Full)["endpoint.policy_types"],
            evaluator.CapturedMetadata["endpoint.policy_types"]);
        Assert.DoesNotContain("endpoint.requires_liability_handshake", evaluator.CapturedMetadata.Keys, StringComparer.Ordinal);
    }

    /// <summary>
    /// Tests that reduced metadata mode omits the policy marker entirely when the endpoint carries no policy type.
    /// </summary>
    [Fact]
    public void ReducedMetadataOmitsPolicyTypesWhenEndpointHasNoPolicyMarker()
    {
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new EmitGovernanceAuditAttribute()),
            "sample.metadata.reduced.nopolicy");
        var descriptor = AsiBackboneEndpointGovernanceDescriptor.FromEndpoint(endpoint);

        IReadOnlyDictionary<string, string> reduced =
            descriptor.ToMetadata(AsiBackboneEndpointGovernanceMetadataMode.Reduced);

        KeyValuePair<string, string> item = Assert.Single(reduced);
        Assert.Equal("endpoint.operation_name", item.Key);
        Assert.Equal("sample.metadata.reduced.nopolicy", item.Value);
    }

    /// <summary>
    /// Tests that reduced metadata mode records every policy marker on an endpoint that carries more than one.
    /// </summary>
    [Fact]
    public void ReducedMetadataRecordsEveryPolicyMarkerOnTheEndpoint()
    {
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new RequireGovernancePolicyAttribute(typeof(SamplePolicy)),
                new RequireGovernancePolicyAttribute(typeof(SecondSamplePolicy))),
            "sample.metadata.reduced.multi");
        var descriptor = AsiBackboneEndpointGovernanceDescriptor.FromEndpoint(endpoint);

        IReadOnlyDictionary<string, string> reduced =
            descriptor.ToMetadata(AsiBackboneEndpointGovernanceMetadataMode.Reduced);

        Assert.Equal(
            descriptor.ToMetadata(AsiBackboneEndpointGovernanceMetadataMode.Full)["endpoint.policy_types"],
            reduced["endpoint.policy_types"]);
    }

    private sealed class SecondSamplePolicy
    {
    }

    /// <summary>
    /// Tests that when the AsiBackboneEndpointGovernanceService is configured to enable development diagnostics and use reduced metadata mode, the failure result includes diagnostic information indicating that reduced metadata mode is in effect.
    /// </summary>
    /// <returns>
    /// A task that represents the asynchronous operation of evaluating the endpoint governance with development diagnostics and reduced metadata mode.
    /// </returns>
    [Fact]
    public async Task DefaultServiceDevelopmentDiagnosticsHonorsReducedMetadataMode()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment("Development"))
            .Configure<AsiBackboneEndpointGovernanceOptions>(options =>
            {
                options.EnableDevelopmentDiagnostics = true;
                options.MetadataMode = AsiBackboneEndpointGovernanceMetadataMode.Reduced;
            })
            .AddAsiBackboneAspNetCore()
            .BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = services.CreateScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            TraceIdentifier = "trace-reduced-diagnostics"
        };
        httpContext.Response.Body = new MemoryStream();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequireCapabilityGrantAttribute("robotics.execute")),
            "sample.metadata.diagnostics");
        var descriptor = AsiBackboneEndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IAsiBackboneEndpointGovernanceService service = scope.ServiceProvider.GetRequiredService<IAsiBackboneEndpointGovernanceService>();

        AsiBackboneEndpointGovernanceResult result = await service.EvaluateAsync(httpContext, descriptor, TestContext.Current.CancellationToken);
        Assert.NotNull(result.FailureResult);
        await result.FailureResult.ExecuteAsync(httpContext);

        string body = await ReadResponseBodyAsync(httpContext);
        Assert.Contains("Reduced", body, StringComparison.Ordinal);
        Assert.Contains("endpoint.operation_name", body, StringComparison.Ordinal);
        Assert.DoesNotContain("endpoint.capability_scopes", body, StringComparison.Ordinal);
        Assert.DoesNotContain("endpoint.emit_governance_audit", body, StringComparison.Ordinal);
    }

    private static AsiBackboneEndpointGovernanceDescriptor CreateDescriptor()
    {
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new RequireGovernancePolicyAttribute(typeof(SamplePolicy)),
                new RequireLiabilityHandshakeAttribute(),
                new RequireCapabilityGrantAttribute("robotics.execute"),
                new EmitGovernanceAuditAttribute()),
            "sample.metadata");

        return AsiBackboneEndpointGovernanceDescriptor.FromEndpoint(endpoint);
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);

        return await reader.ReadToEndAsync();
    }

    private sealed class SamplePolicy
    {
    }

    private sealed class CapturingPolicyEvaluator : IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>
    {
        public IReadOnlyDictionary<string, string>? CapturedMetadata { get; private set; }

        public ValueTask<GovernanceDecision> EvaluateAsync(
            AsiBackboneConstraintEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CapturedMetadata = context.Metadata;

            return ValueTask.FromResult(GovernanceDecision.Allow(
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }
    }

    private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "AsiBackbone.AspNetCore.Tests";

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
