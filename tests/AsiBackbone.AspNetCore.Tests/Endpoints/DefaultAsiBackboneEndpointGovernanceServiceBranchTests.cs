using System.Diagnostics;
using System.Security.Claims;
using AsiBackbone.AspNetCore.Acknowledgments;
using AsiBackbone.AspNetCore.Actors;
using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.AspNetCore.Results;
using AsiBackbone.Core.Audit;
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
    /// Verifies a custom policy evaluator that omits trace correlation still produces a receipt linked to the active
    /// request activity.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncBackfillsActivityTraceIdIntoCustomPolicyDecisionReceipt()
    {
        using Activity activity = new("governed-request");
        activity.SetIdFormat(ActivityIdFormat.W3C);
        _ = activity.Start();
        Assert.NotNull(activity.Id);

        var evaluator = new DelegatePolicyEvaluator(static (_, _) =>
            ValueTask.FromResult(GovernanceDecision.Allow()));
        var auditSink = new CapturingReceiptSink();
        using ServiceProvider services = CreateServices(evaluator, auditSink: auditSink);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(CreateEndpoint(
            "policy.trace.activity",
            new GovernancePolicyAttribute(typeof(SamplePolicy)),
            new EmitGovernanceAuditAttribute()));
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.Equal(activity.Id, result.Decision?.TraceId);
        Assert.Equal(activity.Id, Assert.Single(auditSink.Receipts).TraceId);
    }

    /// <summary>
    /// Verifies the server trace identifier is used when no activity exists and an evaluator omits trace correlation.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncBackfillsHttpContextTraceIdWhenActivityIsMissing()
    {
        Activity? previousActivity = Activity.Current;
        Activity.Current = null;

        try
        {
            var evaluator = new DelegatePolicyEvaluator(static (_, _) =>
                ValueTask.FromResult(GovernanceDecision.Allow()));
            var auditSink = new CapturingReceiptSink();
            using ServiceProvider services = CreateServices(evaluator, auditSink: auditSink);
            using IServiceScope scope = services.CreateScope();
            DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
            var descriptor = EndpointGovernanceDescriptor.FromEndpoint(CreateEndpoint(
                "policy.trace.fallback",
                new GovernancePolicyAttribute(typeof(SamplePolicy)),
                new EmitGovernanceAuditAttribute()));
            IEndpointGovernanceService service = scope.ServiceProvider
                .GetRequiredService<IEndpointGovernanceService>();

            EndpointGovernanceResult result = await service.EvaluateAsync(
                httpContext,
                descriptor,
                TestContext.Current.CancellationToken);

            Assert.Equal(httpContext.TraceIdentifier, result.Decision?.TraceId);
            Assert.Equal(httpContext.TraceIdentifier, Assert.Single(auditSink.Receipts).TraceId);
        }
        finally
        {
            Activity.Current = previousActivity;
        }
    }

    /// <summary>
    /// Verifies a trace identifier supplied by the policy evaluator remains authoritative.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncPreservesEvaluatorTraceId()
    {
        var evaluator = new DelegatePolicyEvaluator(static (_, _) =>
            ValueTask.FromResult(GovernanceDecision.Allow(traceId: "evaluator-trace")));
        using ServiceProvider services = CreateServices(evaluator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(CreateEndpoint(
            "policy.trace.preserved",
            new GovernancePolicyAttribute(typeof(SamplePolicy))));
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.Equal("evaluator-trace", result.Decision?.TraceId);
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
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-123")],
            authenticationType: "test"));
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
        Assert.Equal(httpContext.TraceIdentifier, result.Decision.TraceId);
        Assert.NotNull(result.AcknowledgmentChallenge);
        Assert.NotNull(result.FailureResult);

        await result.FailureResult.ExecuteAsync(httpContext);

        Assert.Equal(StatusCodes.Status428PreconditionRequired, httpContext.Response.StatusCode);
        Assert.True(httpContext.Response.Body.Length > 0);
        Assert.Equal(1, evaluator.CallCount);
    }

    /// <summary>
    /// Verifies a missing capability grant denies an acknowledgment-required decision before a challenge is created.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncCapabilityDenialTakesPrecedenceOverAcknowledgmentChallenge()
    {
        var evaluator = new DelegatePolicyEvaluator(static (context, _) =>
            ValueTask.FromResult(GovernanceDecision.RequireAcknowledgment(
                "acknowledgment.required",
                "The operation requires acknowledgment.",
                correlationId: context.CorrelationId)));
        var capabilityValidator = new DelegateCapabilityGrantValidator(static (_, _, currentDecision, _) =>
            ValueTask.FromResult(GovernanceDecision.Deny(
                "capability.missing",
                "The required capability grant is missing.",
                correlationId: currentDecision.CorrelationId)));
        using ServiceProvider services = CreateServices(evaluator, capabilityValidator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateAuthenticatedHttpContext(scope.ServiceProvider);
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(CreateEndpoint(
            "policy.acknowledgment.capability-denied",
            new GovernancePolicyAttribute(typeof(SamplePolicy)),
            new RequireCapabilityGrantAttribute("robotics.execute"),
            new RequireAcknowledgmentAttribute()));
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.NotNull(result.Decision);
        Assert.True(result.Decision.IsDenied);
        Assert.Contains("capability.missing", result.Decision.ReasonCodes);
        Assert.Equal(httpContext.TraceIdentifier, result.Decision.TraceId);
        Assert.Null(result.AcknowledgmentChallenge);
        Assert.Equal(1, capabilityValidator.CallCount);
        Assert.True(capabilityValidator.CurrentDecision?.RequiresAcknowledgment);
    }

    /// <summary>
    /// Verifies a held capability preserves the acknowledgment-required decision and produces a challenge.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncHeldCapabilityAllowsAcknowledgmentChallenge()
    {
        var evaluator = new DelegatePolicyEvaluator(static (context, _) =>
            ValueTask.FromResult(GovernanceDecision.RequireAcknowledgment(
                "acknowledgment.required",
                "The operation requires acknowledgment.",
                correlationId: context.CorrelationId)));
        var capabilityValidator = new DelegateCapabilityGrantValidator(
            static (_, _, currentDecision, _) => ValueTask.FromResult(currentDecision));
        using ServiceProvider services = CreateServices(evaluator, capabilityValidator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateAuthenticatedHttpContext(scope.ServiceProvider);
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(CreateEndpoint(
            "policy.acknowledgment.capability-held",
            new GovernancePolicyAttribute(typeof(SamplePolicy)),
            new RequireCapabilityGrantAttribute("robotics.execute"),
            new RequireAcknowledgmentAttribute()));
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
        Assert.Equal(1, capabilityValidator.CallCount);
        Assert.True(capabilityValidator.CurrentDecision?.RequiresAcknowledgment);
    }

    /// <summary>
    /// Verifies the fail-closed missing-validator path also takes precedence over an acknowledgment challenge.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncAcknowledgmentRequiredFailsClosedWhenCapabilityValidatorIsMissing()
    {
        var evaluator = new DelegatePolicyEvaluator(static (context, _) =>
            ValueTask.FromResult(GovernanceDecision.RequireAcknowledgment(
                "acknowledgment.required",
                "The operation requires acknowledgment.",
                correlationId: context.CorrelationId)));
        using ServiceProvider services = CreateServices(evaluator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateAuthenticatedHttpContext(scope.ServiceProvider);
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(CreateEndpoint(
            "policy.acknowledgment.capability-validator-missing",
            new GovernancePolicyAttribute(typeof(SamplePolicy)),
            new RequireCapabilityGrantAttribute("robotics.execute"),
            new RequireAcknowledgmentAttribute()));
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.NotNull(result.Decision);
        Assert.True(result.Decision.IsDenied);
        Assert.Contains("endpoint.capability_validator.missing", result.Decision.ReasonCodes);
        Assert.Null(result.AcknowledgmentChallenge);
        Assert.NotNull(result.FailureResult);
    }

    /// <summary>
    /// Verifies opting out of the missing-validator failure preserves, rather than bypasses, an acknowledgment
    /// challenge.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncAcknowledgmentRequiredPreservesChallengeWhenMissingCapabilityValidatorFailsOpen()
    {
        var evaluator = new DelegatePolicyEvaluator(static (context, _) =>
            ValueTask.FromResult(GovernanceDecision.RequireAcknowledgment(
                "acknowledgment.required",
                "The operation requires acknowledgment.",
                correlationId: context.CorrelationId)));
        using ServiceProvider services = CreateServices(
            evaluator,
            configure: static options => options.FailClosedWhenCapabilityValidatorMissing = false);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateAuthenticatedHttpContext(scope.ServiceProvider);
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(CreateEndpoint(
            "policy.acknowledgment.capability-validator-missing.fail-open",
            new GovernancePolicyAttribute(typeof(SamplePolicy)),
            new RequireCapabilityGrantAttribute("robotics.execute"),
            new RequireAcknowledgmentAttribute()));
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
    }

    /// <summary>
    /// Verifies the capability validator runs for every non-denied outcome, and that a missing grant replaces each of
    /// them with a denial, so no later deferral, escalation, or warning flow can proceed without the grant.
    /// </summary>
    /// <param name="outcome">The policy outcome returned before capability validation.</param>
    [Theory]
    [InlineData(GovernanceDecisionOutcome.Warning)]
    [InlineData(GovernanceDecisionOutcome.Deferred)]
    [InlineData(GovernanceDecisionOutcome.EscalationRecommended)]
    public async Task EvaluateAsyncCapabilityDenialTakesPrecedenceOverNonDeniedOutcome(GovernanceDecisionOutcome outcome)
    {
        var evaluator = new DelegatePolicyEvaluator((context, _) =>
            ValueTask.FromResult(CreateDecision(outcome, context.CorrelationId)));
        var capabilityValidator = new DelegateCapabilityGrantValidator(static (_, _, currentDecision, _) =>
            ValueTask.FromResult(GovernanceDecision.Deny(
                "capability.missing",
                "The required capability grant is missing.",
                correlationId: currentDecision.CorrelationId)));
        using ServiceProvider services = CreateServices(evaluator, capabilityValidator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateAuthenticatedHttpContext(scope.ServiceProvider);
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(CreateEndpoint(
            $"policy.{outcome}.capability-denied",
            new GovernancePolicyAttribute(typeof(SamplePolicy)),
            new RequireCapabilityGrantAttribute("robotics.execute")));
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.NotNull(result.Decision);
        Assert.True(result.Decision.IsDenied);
        Assert.Contains("capability.missing", result.Decision.ReasonCodes);
        Assert.Equal(1, capabilityValidator.CallCount);
        Assert.Equal(outcome, capabilityValidator.CurrentDecision?.Outcome);
    }

    /// <summary>
    /// Verifies that opting out of the missing-validator failure skips only the capability check: a requested audit is
    /// still enforced, so a missing decision receipt sink still fails closed.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncMissingCapabilityValidatorFailOpenStillEnforcesAudit()
    {
        using ServiceProvider services = CreateServices(
            configure: static options => options.FailClosedWhenCapabilityValidatorMissing = false);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(CreateEndpoint(
            "capability.validator-missing.fail-open.audit",
            new RequireCapabilityGrantAttribute("robotics.execute"),
            new EmitGovernanceAuditAttribute()));
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

    /// <summary>
    /// Verifies an acknowledgment-required endpoint fails closed with a stable code when the request is anonymous.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncRejectsAnonymousAcknowledgmentChallenge()
    {
        var evaluator = new DelegatePolicyEvaluator(static (context, _) =>
            ValueTask.FromResult(GovernanceDecision.RequireAcknowledgment(
                "acknowledgment.required",
                "The operation requires acknowledgment.",
                correlationId: context.CorrelationId)));
        using ServiceProvider services = CreateServices(evaluator);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        Endpoint endpoint = CreateEndpoint(
            "policy.anonymous-acknowledgment",
            new GovernancePolicyAttribute(typeof(SamplePolicy)),
            new RequireAcknowledgmentAttribute());
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.Null(result.AcknowledgmentChallenge);
        Assert.NotNull(result.FailureResult);

        await result.FailureResult.ExecuteAsync(httpContext);
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);
        string responseBody = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Contains("acknowledgment.challenge.actor_unbound", responseBody, StringComparison.Ordinal);
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

    /// <summary>
    /// Verifies requested audit emission is allowed when the host opts out of failing closed for a missing sink.
    /// </summary>
    [Fact]
    public async Task EvaluateAsyncAllowsMissingAuditSinkWhenFailOpenIsConfigured()
    {
        using ServiceProvider services = CreateServices(
            configure: static options => options.FailClosedWhenAuditSinkMissing = false);
        using IServiceScope scope = services.CreateScope();
        DefaultHttpContext httpContext = CreateHttpContext(scope.ServiceProvider);
        var descriptor =
            EndpointGovernanceDescriptor.FromEndpoint(
                CreateEndpoint("audit.missing.fail-open", new EmitGovernanceAuditAttribute()));
        IEndpointGovernanceService service = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(
            httpContext,
            descriptor,
            TestContext.Current.CancellationToken);

        Assert.True(result.CanExecute);
        Assert.Null(result.FailureResult);
    }

    /// <summary>
    /// Verifies the service constructor rejects every missing dependency and options value.
    /// </summary>
    [Fact]
    public void ConstructorRejectsMissingDependencies()
    {
        using ServiceProvider services = CreateServices();
        using IServiceScope scope = services.CreateScope();
        IServiceProvider provider = scope.ServiceProvider;
        IHttpGovernanceActorContextResolver actorResolver = provider.GetRequiredService<IHttpGovernanceActorContextResolver>();
        IHttpGovernanceRequestCorrelationResolver correlationResolver = provider.GetRequiredService<IHttpGovernanceRequestCorrelationResolver>();
        IAcknowledgmentChallengeService acknowledgmentService = provider.GetRequiredService<IAcknowledgmentChallengeService>();
        IOptions<EndpointGovernanceOptions> endpointOptions = Options.Create(new EndpointGovernanceOptions());
        IOptions<GovernanceHttpResultMappingOptions> resultOptions = Options.Create(new GovernanceHttpResultMappingOptions());

        Assert.Equal("serviceProvider", Assert.Throws<ArgumentNullException>(() => new DefaultEndpointGovernanceService(
            null!, actorResolver, correlationResolver, acknowledgmentService, endpointOptions, resultOptions)).ParamName);
        Assert.Equal("actorContextResolver", Assert.Throws<ArgumentNullException>(() => new DefaultEndpointGovernanceService(
            provider, null!, correlationResolver, acknowledgmentService, endpointOptions, resultOptions)).ParamName);
        Assert.Equal("requestCorrelationResolver", Assert.Throws<ArgumentNullException>(() => new DefaultEndpointGovernanceService(
            provider, actorResolver, null!, acknowledgmentService, endpointOptions, resultOptions)).ParamName);
        Assert.Equal("acknowledgmentChallengeService", Assert.Throws<ArgumentNullException>(() => new DefaultEndpointGovernanceService(
            provider, actorResolver, correlationResolver, null!, endpointOptions, resultOptions)).ParamName);
        Assert.Equal("endpointOptions", Assert.Throws<ArgumentNullException>(() => new DefaultEndpointGovernanceService(
            provider, actorResolver, correlationResolver, acknowledgmentService, null!, resultOptions)).ParamName);
        Assert.Equal("endpointOptions", Assert.Throws<ArgumentNullException>(() => new DefaultEndpointGovernanceService(
            provider, actorResolver, correlationResolver, acknowledgmentService, new NullValueOptions<EndpointGovernanceOptions>(), resultOptions)).ParamName);
        Assert.Equal("resultOptions", Assert.Throws<ArgumentNullException>(() => new DefaultEndpointGovernanceService(
            provider, actorResolver, correlationResolver, acknowledgmentService, endpointOptions, null!)).ParamName);
        Assert.Equal("resultOptions", Assert.Throws<ArgumentNullException>(() => new DefaultEndpointGovernanceService(
            provider, actorResolver, correlationResolver, acknowledgmentService, endpointOptions, new NullValueOptions<GovernanceHttpResultMappingOptions>())).ParamName);
    }

    private static ServiceProvider CreateServices(
        IGovernancePolicyEvaluator<GovernanceEvaluationContext>? evaluator = null,
        IEndpointCapabilityGrantValidator? capabilityValidator = null,
        Action<EndpointGovernanceOptions>? configure = null,
        IDecisionReceiptSink? auditSink = null)
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

        if (capabilityValidator is not null)
        {
            _ = services.AddSingleton(capabilityValidator);
        }

        if (auditSink is not null)
        {
            _ = services.AddSingleton(auditSink);
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

    private static GovernanceDecision CreateDecision(GovernanceDecisionOutcome outcome, string? correlationId)
    {
        return outcome switch
        {
            GovernanceDecisionOutcome.Warning => GovernanceDecision.Warning(
                "policy.warning",
                "The operation carries a warning.",
                correlationId: correlationId),
            GovernanceDecisionOutcome.Deferred => GovernanceDecision.Defer(
                "policy.deferred",
                "The operation is deferred for later review.",
                correlationId: correlationId),
            GovernanceDecisionOutcome.EscalationRecommended => GovernanceDecision.Escalate(
                "policy.escalation",
                "The operation should be escalated.",
                correlationId: correlationId),
            GovernanceDecisionOutcome.Allowed or GovernanceDecisionOutcome.Denied or GovernanceDecisionOutcome.AcknowledgmentRequired =>
                throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Covered by dedicated tests."),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, "Unsupported test outcome.")
        };
    }

    private static DefaultHttpContext CreateAuthenticatedHttpContext(IServiceProvider requestServices)
    {
        DefaultHttpContext httpContext = CreateHttpContext(requestServices);
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.NameIdentifier, "user-123")],
            authenticationType: "test"));
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

    private sealed class NullValueOptions<TOptions> : IOptions<TOptions>
        where TOptions : class
    {
        public TOptions Value => null!;
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

    private sealed class DelegateCapabilityGrantValidator(
        Func<HttpContext, EndpointGovernanceDescriptor, GovernanceDecision, CancellationToken, ValueTask<GovernanceDecision>>
            validate) : IEndpointCapabilityGrantValidator
    {
        public int CallCount { get; private set; }

        public GovernanceDecision? CurrentDecision { get; private set; }

        public ValueTask<GovernanceDecision> ValidateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            GovernanceDecision currentDecision,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            CurrentDecision = currentDecision;
            return validate(httpContext, descriptor, currentDecision, cancellationToken);
        }
    }

    private sealed class CapturingReceiptSink : IDecisionReceiptSink
    {
        public List<IDecisionReceipt> Receipts { get; } = [];

        public ValueTask WriteAsync(
            IDecisionReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            Receipts.Add(receipt);
            return ValueTask.CompletedTask;
        }
    }
}
