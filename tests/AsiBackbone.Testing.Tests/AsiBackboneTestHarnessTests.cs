using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Core.Signing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.Testing.Tests;

/// <summary>
/// Tests for the AsiBackbone test harness and its integration with ASP.NET Core.
/// </summary>
public sealed class AsiBackboneTestHarnessTests
{
    /// <summary>
    /// Tests that the AsiBackbone test harness allows a protected endpoint to be executed without requiring host persistence or signing setup.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation. 
    /// </returns>
    [Fact]
    public async Task AddAsiBackboneTestHarnessAllowsProtectedEndpointWithoutHostPersistenceOrSigningSetup()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddAsiBackboneAspNetCore()
            .AddAsiBackboneTestHarness(harness =>
            {
                _ = harness.AllowAllPolicies();
                _ = harness.AllowCapabilityGrants();
            })
            .BuildServiceProvider(validateScopes: true);

        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider, "trace-protected");
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new GovernancePolicyAttribute(typeof(SamplePolicy)),
                new RequireCapabilityGrantAttribute("robotics.execute"),
                new EmitGovernanceAuditAttribute()),
            "testing.protected");
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IEndpointGovernanceService service = scope.ServiceProvider.GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.True(result.CanExecute);
        Assert.NotNull(result.Decision);
        Assert.True(result.Decision.IsAllowed);

        GovernanceTestDecisionReceiptSink auditSink = scope.ServiceProvider.GetRequiredService<GovernanceTestDecisionReceiptSink>();
        _ = Assert.Single(auditSink.Entries);
        Assert.Equal("testing.protected", auditSink.Entries[0].OperationName);
    }

    /// <summary>
    /// Tests that the AsiBackbone test harness can be configured to return a deterministic decision for a specific policy marker type, and that this decision is respected when evaluating an endpoint that requires that policy.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task SetPolicyResultUsesDeterministicDecisionByPolicyMarkerType()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddAsiBackboneAspNetCore()
            .AddAsiBackboneTestHarness(harness =>
            {
                _ = harness.AllowAllPolicies();
                _ = harness.SetPolicyResult<StrictPolicy>(GovernanceDecision.Deny(
                    "test.denied",
                    "Denied by test harness."));
            })
            .BuildServiceProvider(validateScopes: true);

        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider, "trace-denied");
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new GovernancePolicyAttribute(typeof(StrictPolicy))),
            "testing.denied");
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IEndpointGovernanceService service = scope.ServiceProvider.GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.NotNull(result.Decision);
        Assert.True(result.Decision.IsDenied);
        Assert.Contains("test.denied", result.Decision.ReasonCodes);
    }

    /// <summary>
    /// Tests that the AsiBackbone test harness can be configured to deny all policies by default, and that this default deny decision is applied when evaluating a policy that has not been explicitly configured.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DenyAllPoliciesAppliesDefaultDenyDecision()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddAsiBackboneAspNetCore()
            .AddAsiBackboneTestHarness(harness => harness.DenyAllPolicies("test.default_denied", "Default denied by test harness."))
            .BuildServiceProvider(validateScopes: true);

        using IServiceScope scope = services.CreateScope();
        IGovernancePolicyEvaluator<GovernanceEvaluationContext> evaluator =
            scope.ServiceProvider.GetRequiredService<IGovernancePolicyEvaluator<GovernanceEvaluationContext>>();
        var context = new GovernanceEvaluationContext(
            correlationId: "correlation-1",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["endpoint.policy_types"] = typeof(SamplePolicy).FullName ?? nameof(SamplePolicy)
            });

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Equal("correlation-1", decision.CorrelationId);
        Assert.Contains("test.default_denied", decision.ReasonCodes);
    }

    /// <summary>
    /// Tests that the AsiBackbone test harness returns a denied decision when evaluating a policy that has not been explicitly configured, and that the decision includes a reason code indicating that the policy result is missing.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task RequirePolicyResultFailsClosedForUnconfiguredPolicyMarker()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddAsiBackboneAspNetCore()
            .AddAsiBackboneTestHarness(harness => harness.RequirePolicyResult<StrictPolicy>(GovernanceDecision.Allow()))
            .BuildServiceProvider(validateScopes: true);

        using IServiceScope scope = services.CreateScope();
        IGovernancePolicyEvaluator<GovernanceEvaluationContext> evaluator =
            scope.ServiceProvider.GetRequiredService<IGovernancePolicyEvaluator<GovernanceEvaluationContext>>();
        var context = new GovernanceEvaluationContext(
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["endpoint.policy_types"] = typeof(SamplePolicy).FullName ?? nameof(SamplePolicy)
            });

        GovernanceDecision decision = await evaluator.EvaluateAsync(context, TestContext.Current.CancellationToken);

        Assert.True(decision.IsDenied);
        Assert.Contains("test_harness.policy_result.missing", decision.ReasonCodes);
    }

    /// <summary>
    /// Tests that the AsiBackbone test harness can be configured to deny specific capability grants, and that this denial is respected when evaluating an endpoint that requires that capability.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DenyCapabilityGrantsBlocksEndpointAfterAllowedPolicyDecision()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddAsiBackboneAspNetCore()
            .AddAsiBackboneTestHarness(harness =>
            {
                _ = harness.AllowAllPolicies();
                _ = harness.DenyCapabilityGrants("test.capability_denied", "Capability denied by test harness.");
            })
            .BuildServiceProvider(validateScopes: true);

        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider, "trace-capability");
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new GovernancePolicyAttribute(typeof(SamplePolicy)),
                new RequireCapabilityGrantAttribute("robotics.execute")),
            "testing.capability");
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IEndpointGovernanceService service = scope.ServiceProvider.GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.NotNull(result.Decision);
        Assert.True(result.Decision.IsDenied);
        Assert.Contains("test.capability_denied", result.Decision.ReasonCodes);
    }

    /// <summary>
    /// Tests that the AsiBackbone test harness can be configured to return a deterministic signing result, and that this result is respected when attempting to sign a payload.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DeterministicSigningServiceReturnsNoSignatureResult()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddAsiBackboneTestHarness()
            .BuildServiceProvider(validateScopes: true);

        IGovernanceSigningService signingService = services.GetRequiredService<IGovernanceSigningService>();

        SigningResult result = await signingService.SignAsync(
            new SigningRequest("test-payload-hash", "SHA256", "test"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSigned);
    }

    /// <summary>
    /// Tests that disabling the optional harness registrations leaves the audit sink, outbox store, and signing service unregistered.
    /// </summary>
    [Fact]
    public void AddAsiBackboneTestHarnessSkipsDisabledOptionalRegistrations()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddAsiBackboneTestHarness(harness =>
            {
                harness.RegisterInMemoryAuditSink = false;
                harness.RegisterInMemoryOutboxStore = false;
                harness.RegisterDeterministicSigningService = false;
            })
            .BuildServiceProvider(validateScopes: true);

        Assert.Null(services.GetService<IDecisionReceiptSink>());
        Assert.Null(services.GetService<IGovernanceOutboxStore>());
        Assert.Null(services.GetService<IGovernanceSigningService>());
        Assert.NotNull(services.GetService<IEndpointCapabilityGrantValidator>());
    }

    /// <summary>
    /// Tests that a policy marker type without a full name is keyed by its simple name.
    /// </summary>
    [Fact]
    public void SetPolicyResultAcceptsPolicyTypeWithoutFullName()
    {
        Type genericParameter = typeof(List<>).GetGenericArguments()[0];
        Assert.Null(genericParameter.FullName);

        GovernanceTestHarnessOptions options = new();

        Assert.Same(options, options.SetPolicyResult(genericParameter, GovernanceDecision.Allow()));
    }

    /// <summary>
    /// Tests that the harness capability-grant validator requires options.
    /// </summary>
    [Fact]
    public void CapabilityGrantValidatorRejectsMissingOptions()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new GovernanceTestHarnessEndpointCapabilityGrantValidator(null!));
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider serviceProvider, string traceIdentifier)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
            TraceIdentifier = traceIdentifier
        };

        serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        return httpContext;
    }

    private sealed class SamplePolicy
    {
    }

    private sealed class StrictPolicy
    {
    }
}
