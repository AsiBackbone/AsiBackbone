using System.Security.Claims;
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Conformance specimens for the host-owned decision-before-side-effect boundary.
/// </summary>
/// <remarks>
/// AsiBackbone evaluates governance; it does not own the protected side effect. These tests make the supported host
/// integration invariant executable: invoke the protected executor only after the governed seam returns
/// <see cref="EndpointGovernanceResult.CanExecute" />. Calling the executor directly, or before evaluation, is an explicit
/// anti-pattern because no decision receipt can prove that execution was actually gated.
/// </remarks>
public sealed class HostEnforcementMisuseConformanceTests
{
    /// <summary>
    /// Verifies that a denied decision produces no protected executor calls.
    /// </summary>
    [Fact]
    public async Task DeniedDecisionProducesZeroProtectedExecutorCalls()
    {
        var executor = new ProtectedExecutor();
        EndpointGovernanceResult result = await EvaluateAsync(
            GovernanceDecision.Deny("policy.denied", "Denied."));

        await ExecuteOnlyWhenAllowedAsync(result, executor);

        Assert.False(result.CanExecute);
        Assert.Equal(0, executor.CallCount);
    }

    /// <summary>
    /// Verifies that a deferred decision produces no protected executor calls.
    /// </summary>
    [Fact]
    public async Task DeferredDecisionProducesZeroProtectedExecutorCalls()
    {
        var executor = new ProtectedExecutor();
        EndpointGovernanceResult result = await EvaluateAsync(
            GovernanceDecision.Defer("policy.deferred", "Deferred."));

        await ExecuteOnlyWhenAllowedAsync(result, executor);

        Assert.False(result.CanExecute);
        Assert.Equal(0, executor.CallCount);
    }

    /// <summary>
    /// Verifies that an unsatisfied acknowledgment produces no protected executor calls.
    /// </summary>
    [Fact]
    public async Task UnsatisfiedAcknowledgmentProducesZeroProtectedExecutorCalls()
    {
        var executor = new ProtectedExecutor();
        EndpointGovernanceResult result = await EvaluateAsync(
            GovernanceDecision.RequireAcknowledgment("ack.required", "Acknowledge."),
            new RequireAcknowledgmentAttribute());

        await ExecuteOnlyWhenAllowedAsync(result, executor);

        Assert.False(result.CanExecute);
        Assert.Equal(0, executor.CallCount);
    }

    /// <summary>
    /// Verifies that missing capability authority produces no protected executor calls.
    /// </summary>
    [Fact]
    public async Task MissingCapabilityAuthorityProducesZeroProtectedExecutorCalls()
    {
        var executor = new ProtectedExecutor();
        EndpointGovernanceResult result = await EvaluateAsync(
            GovernanceDecision.Allow(),
            new RequireCapabilityGrantAttribute("protected.execute"));

        await ExecuteOnlyWhenAllowedAsync(result, executor);

        Assert.False(result.CanExecute);
        Assert.Contains("endpoint.capability_validator.missing", result.Decision!.ReasonCodes);
        Assert.Equal(0, executor.CallCount);
    }

    /// <summary>
    /// Verifies that invalid capability authority produces no protected executor calls.
    /// </summary>
    [Fact]
    public async Task InvalidCapabilityAuthorityProducesZeroProtectedExecutorCalls()
    {
        var executor = new ProtectedExecutor();
        var validator = new RejectingCapabilityValidator();
        EndpointGovernanceResult result = await EvaluateAsync(
            GovernanceDecision.Allow(),
            validator,
            new RequireCapabilityGrantAttribute("protected.execute"));

        await ExecuteOnlyWhenAllowedAsync(result, executor);

        Assert.False(result.CanExecute);
        Assert.Contains("capability.invalid", result.Decision!.ReasonCodes);
        Assert.Equal(1, validator.CallCount);
        Assert.Equal(0, executor.CallCount);
    }

    /// <summary>
    /// Verifies that required evidence-persistence failure occurs before the protected executor is called.
    /// </summary>
    [Fact]
    public async Task EvidencePersistenceFailureProducesZeroProtectedExecutorCalls()
    {
        var executor = new ProtectedExecutor();

        _ = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await EvaluateAsync(
                decision: GovernanceDecision.Allow(),
                capabilityValidator: null,
                auditSink: new ThrowingReceiptSink(),
                metadata: [new EmitGovernanceAuditAttribute()]));

        Assert.Equal(0, executor.CallCount);
    }

    /// <summary>
    /// Verifies that an allowed governance decision remains allowed when subsequent host execution fails.
    /// </summary>
    [Fact]
    public async Task AllowedDecisionDoesNotHideHostExecutionFailure()
    {
        var executor = new ProtectedExecutor { ThrowOnExecute = true };
        EndpointGovernanceResult result = await EvaluateAsync(GovernanceDecision.Allow());

        Assert.True(result.CanExecute);
        Assert.True(result.Decision!.IsAllowed);

        _ = await Assert.ThrowsAsync<InvalidOperationException>(
            async () => await ExecuteOnlyWhenAllowedAsync(result, executor));

        Assert.True(result.Decision.IsAllowed);
        Assert.Equal(1, executor.CallCount);
    }

    /// <summary>
    /// Demonstrates that a direct executor call is an explicit anti-pattern that bypasses governance evidence.
    /// </summary>
    [Fact]
    public async Task DirectExecutorCallIsExplicitAntiPatternAndBypassesGovernanceEvidence()
    {
        var executor = new ProtectedExecutor();

        // ANTI-PATTERN: alternate code paths can call host-owned executors directly. AsiBackbone cannot infer that a
        // side effect was gated merely because a decision receipt exists elsewhere.
        await executor.ExecuteAsync();

        Assert.Equal(1, executor.CallCount);
    }

    /// <summary>
    /// Demonstrates that executing a side effect before evaluation is an explicit anti-pattern.
    /// </summary>
    [Fact]
    public async Task SideEffectBeforeEvaluationIsExplicitAntiPattern()
    {
        var executor = new ProtectedExecutor();

        // ANTI-PATTERN: executing first cannot be repaired by evaluating or persisting a decision afterward.
        await executor.ExecuteAsync();
        EndpointGovernanceResult result = await EvaluateAsync(
            GovernanceDecision.Deny("policy.denied", "Denied after the effect."));

        Assert.False(result.CanExecute);
        Assert.Equal(1, executor.CallCount);
    }

    private static async Task ExecuteOnlyWhenAllowedAsync(
        EndpointGovernanceResult result,
        ProtectedExecutor executor)
    {
        if (result.CanExecute)
        {
            await executor.ExecuteAsync();
        }
    }

    private static Task<EndpointGovernanceResult> EvaluateAsync(
        GovernanceDecision decision,
        params object[] metadata)
    {
        return EvaluateAsync(decision, capabilityValidator: null, auditSink: null, metadata);
    }

    private static Task<EndpointGovernanceResult> EvaluateAsync(
        GovernanceDecision decision,
        IEndpointCapabilityGrantValidator capabilityValidator,
        params object[] metadata)
    {
        return EvaluateAsync(decision, capabilityValidator, auditSink: null, metadata);
    }

    private static async Task<EndpointGovernanceResult> EvaluateAsync(
        GovernanceDecision decision,
        IEndpointCapabilityGrantValidator? capabilityValidator = null,
        IDecisionReceiptSink? auditSink = null,
        params object[] metadata)
    {
        ServiceCollection services = new();
        _ = services.AddAsiBackboneAspNetCore();
        _ = services.AddSingleton<IGovernancePolicyEvaluator<GovernanceEvaluationContext>>(
            new FixedDecisionEvaluator(decision));

        if (capabilityValidator is not null)
        {
            _ = services.AddSingleton(capabilityValidator);
        }

        if (auditSink is not null)
        {
            _ = services.AddSingleton(auditSink);
        }

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            TraceIdentifier = "host-enforcement-conformance",
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, "conformance-user")],
                authenticationType: "test"))
        };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                [new GovernancePolicyAttribute(typeof(ConformancePolicy)), .. metadata]),
            "host.enforcement.conformance");
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IEndpointGovernanceService governance = scope.ServiceProvider.GetRequiredService<IEndpointGovernanceService>();

        return await governance.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);
    }

    private sealed class ConformancePolicy;

    private sealed class FixedDecisionEvaluator(GovernanceDecision decision)
        : IGovernancePolicyEvaluator<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class ProtectedExecutor
    {
        public int CallCount { get; private set; }

        public bool ThrowOnExecute { get; init; }

        public Task ExecuteAsync()
        {
            CallCount++;
            return ThrowOnExecute
                ? Task.FromException(new InvalidOperationException("Host execution failed."))
                : Task.CompletedTask;
        }
    }

    private sealed class RejectingCapabilityValidator : IEndpointCapabilityGrantValidator
    {
        public int CallCount { get; private set; }

        public ValueTask<GovernanceDecision> ValidateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            GovernanceDecision currentDecision,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return ValueTask.FromResult(GovernanceDecision.Deny(
                "capability.invalid",
                "The supplied capability authority is invalid.",
                correlationId: currentDecision.CorrelationId));
        }
    }

    private sealed class ThrowingReceiptSink : IDecisionReceiptSink
    {
        public ValueTask WriteAsync(
            IDecisionReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromException(new InvalidOperationException("Evidence persistence failed."));
        }
    }
}
