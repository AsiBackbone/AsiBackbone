using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Focused branch coverage for <see cref="DefaultEndpointGovernanceService" /> orchestration.
/// </summary>
public sealed class DefaultAsiBackboneEndpointGovernanceServiceBranchTests
{
    /// <summary>
    /// Verifies a descriptor without governance metadata is allowed without invoking a policy evaluator.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncAllowsDescriptorWithoutGovernanceMetadata()
    {
        var evaluator = new DelegatePolicyEvaluator(static (_, _) =>
            ValueTask.FromException<GovernanceDecision>(
                new InvalidOperationException("The evaluator should not be invoked.")));
        using ServiceProvider services = CreateServices(evaluator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            EndpointGovernanceDescriptor.None("plain.operation"),
            TestContext.Current.CancellationToken);

        Assert.True(result.CanExecute);
        Assert.Null(result.Decision);
        Assert.Null(result.FailureResult);
        Assert.Equal(0, evaluator.CallCount);
    }

    /// <summary>
    /// Verifies an allowed policy decision continues through middleware to the selected endpoint.
    /// </summary>
    [Fact]
    public async Task MiddlewareContinuesForAllowedPolicyDecision()
    {
        var evaluator = new DelegatePolicyEvaluator(static (context, _) =>
            ValueTask.FromResult(GovernanceDecision.Allow(
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash)));
        using ServiceProvider services = CreateServices(evaluator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        httpContext.SetEndpoint(CreateEndpoint(
            "policy.allowed",
            new GovernancePolicyAttribute(typeof(SamplePolicy))));
        bool nextCalled = false;
        var middleware = new EndpointGovernanceMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            scope.ServiceProvider.GetRequiredService<IOptions<EndpointGovernanceOptions>>());
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        await middleware.InvokeAsync(httpContext, service);

        Assert.True(nextCalled);
        Assert.Equal(StatusCodes.Status200OK, httpContext.Response.StatusCode);
        Assert.Equal(1, evaluator.CallCount);
    }

    /// <summary>
    /// Verifies a denied policy decision remains a bodyless default 403 response through middleware.
    /// </summary>
    [Fact]
    public async Task MiddlewareBlocksDeniedPolicyDecisionWithDefaultForbiddenResponse()
    {
        var evaluator = new DelegatePolicyEvaluator(static (context, _) =>
            ValueTask.FromResult(GovernanceDecision.Deny(
                "policy.denied",
                "The policy denied execution.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash)));
        using ServiceProvider services = CreateServices(evaluator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        httpContext.SetEndpoint(CreateEndpoint(
            "policy.denied",
            new GovernancePolicyAttribute(typeof(SamplePolicy))));
        bool nextCalled = false;
        var middleware = new EndpointGovernanceMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            scope.ServiceProvider.GetRequiredService<IOptions<EndpointGovernanceOptions>>());
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        await middleware.InvokeAsync(httpContext, service);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(0, httpContext.Response.Body.Length);
        Assert.Equal(1, evaluator.CallCount);
    }

    /// <summary>
    /// Verifies an acknowledgment-required decision produces a challenge with the configured status code.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncCreatesAcknowledgmentChallenge()
    {
        var evaluator = new DelegatePolicyEvaluator(static (context, _) =>
            ValueTask.FromResult(GovernanceDecision.RequireAcknowledgment(
                "acknowledgment.required",
                "The operation requires acknowledgment.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash)));
        using ServiceProvider services = CreateServices(evaluator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        Endpoint endpoint = CreateEndpoint(
            "policy.acknowledgment",
            new GovernancePolicyAttribute(typeof(SamplePolicy)),
            new RequireAcknowledgmentAttribute());
        var descriptor =
            EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.NotNull(result.Decision);
        Assert.True(result.Decision.RequiresAcknowledgment);
        Assert.NotNull(result.AcknowledgmentChallenge);
        Assert.NotNull(result.FailureResult);

        await result.FailureResult.ExecuteAsync(httpContext);

        Assert.Equal(StatusCodes.Status428PreconditionRequired, httpContext.Response.StatusCode);
        Assert.True(httpContext.Response.Body.Length > 0);
        Assert.Equal(1, evaluator.CallCount);
    }

    /// <summary>
    /// Verifies cancellation is observed before optional governance services are invoked.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncStopsBeforeEvaluationWhenCancellationIsRequested()
    {
        var evaluator = new DelegatePolicyEvaluator(static (context, _) =>
            ValueTask.FromResult(GovernanceDecision.Allow(correlationId: context.CorrelationId)));
        using ServiceProvider services = CreateServices(evaluator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        var descriptor =
            EndpointGovernanceDescriptor.FromEndpoint(
                CreateEndpoint(
                    "policy.canceled",
                    new GovernancePolicyAttribute(typeof(SamplePolicy))));
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();
        using var source = CancellationTokenSource.CreateLinkedTokenSource(
            TestContext.Current.CancellationToken);
        source.Cancel();

        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () =>
            await service.EvaluateAsync(httpContext, descriptor, source.Token));

        Assert.Equal(0, evaluator.CallCount);
    }

    /// <summary>
    /// Verifies a policy-evaluator failure propagates without being translated into another outcome.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncPropagatesPolicyEvaluatorException()
    {
        var expectedException = new InvalidOperationException("Policy evaluation failed.");
        var evaluator = new DelegatePolicyEvaluator((_, _) =>
            ValueTask.FromException<GovernanceDecision>(expectedException));
        using ServiceProvider services = CreateServices(evaluator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        var descriptor =
            EndpointGovernanceDescriptor.FromEndpoint(
                CreateEndpoint(
                    "policy.exception",
                    new GovernancePolicyAttribute(typeof(SamplePolicy))));
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        InvalidOperationException actualException = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await service.EvaluateAsync(
                httpContext,
                descriptor,
                TestContext.Current.CancellationToken));

        Assert.Same(expectedException, actualException);
        Assert.Equal(1, evaluator.CallCount);
    }

    /// <summary>
    /// Verifies policy metadata fails closed when no evaluator is registered.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncFailsClosedWhenPolicyEvaluatorIsMissing()
    {
        using ServiceProvider services = CreateServices();
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        var descriptor =
            EndpointGovernanceDescriptor.FromEndpoint(
                CreateEndpoint(
                    "policy.missing",
                    new GovernancePolicyAttribute(typeof(SamplePolicy))));
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.NotNull(result.Decision);
        Assert.NotNull(result.FailureResult);
        Assert.Contains("endpoint.policy_evaluator.missing", result.Decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies the explicit fail-open option allows execution when no evaluator is registered.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncFailsOpenWhenPolicyEvaluatorIsMissingAndConfigured()
    {
        using ServiceProvider services = CreateServices(
            configure: static options => options.FailClosedWhenPolicyEvaluatorMissing = false);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        var descriptor =
            EndpointGovernanceDescriptor.FromEndpoint(
                CreateEndpoint(
                    "policy.missing.fail-open",
                    new GovernancePolicyAttribute(typeof(SamplePolicy))));
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.True(result.CanExecute);
        Assert.NotNull(result.Decision);
        Assert.True(result.Decision.IsAllowed);
        Assert.Null(result.FailureResult);
    }

    /// <summary>
    /// Verifies requested audit emission fails closed when no decision receipt sink is registered.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncFailsClosedWhenAuditSinkIsMissing()
    {
        using ServiceProvider services = CreateServices();
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        var descriptor =
            EndpointGovernanceDescriptor.FromEndpoint(
                CreateEndpoint("audit.missing", new EmitGovernanceAuditAttribute()));
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.NotNull(result.Decision);
        Assert.NotNull(result.FailureResult);
        Assert.Contains("endpoint.audit_sink.missing", result.Decision.ReasonCodes);
    }

    private static ServiceProvider CreateServices(
        IGovernancePolicyEvaluator<GovernanceEvaluationContext>? evaluator = null,
        Action<EndpointGovernanceOptions>? configure = null)
    {
        ServiceCollection services = new();
        if (configure is not null)
        {
            _ = services.Configure(configure);
        }

        _ = services.AddAsiBackboneAspNetCore();
        if (evaluator is not null)
        {
            _ = services.AddSingleton(evaluator);
        }

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static DefaultHttpContext CreateHttpContext(IServiceProvider requestServices)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices,
            TraceIdentifier = "endpoint-governance-branch-test"
        };
        httpContext.Response.Body = new MemoryStream();
        requestServices.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        return httpContext;
    }

    private static Endpoint CreateEndpoint(string operationName, params object[] metadata)
    {
        return new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(metadata),
            operationName);
    }

    private sealed class SamplePolicy
    {
    }

    private sealed class DelegatePolicyEvaluator(
        Func<GovernanceEvaluationContext, CancellationToken, ValueTask<GovernanceDecision>> evaluate)
        : IGovernancePolicyEvaluator<GovernanceEvaluationContext>
    {
        public int CallCount { get; private set; }

        public ValueTask<GovernanceDecision> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return evaluate(context, cancellationToken);
        }
    }
}
