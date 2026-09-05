using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Metadata;
using AsiBackbone.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Tests that request-derived metadata reaches the sanitizer and that its decisions survive into the evaluation
/// context, including entries the sanitizer removes.
/// </summary>
public sealed class EndpointGovernanceMetadataSanitizationTests
{
    private const string RouteMetadataKey = "route.email";
    private const string RouteMetadataValue = "person@example.com";

    /// <summary>
    /// Verifies that request-derived metadata is presented to the sanitizer.
    /// </summary>
    /// <remarks>
    /// Sanitizing endpoint metadata before merging request metadata meant per-request values such as the email
    /// segment of <c>/users/{email}</c> were never inspected.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task SanitizerSeesRequestDerivedMetadata()
    {
        var sanitizer = new RecordingSanitizer(GovernanceMetadataSanitizationAction.Allow);
        var evaluator = new CapturingPolicyEvaluator();

        _ = await EvaluateAsync(sanitizer, evaluator);

        Assert.NotNull(sanitizer.ObservedMetadata);
        Assert.Equal(RouteMetadataValue, sanitizer.ObservedMetadata[RouteMetadataKey]);
    }

    /// <summary>
    /// Verifies that a redacted request-derived value reaches evaluation redacted rather than raw.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task RedactedRequestMetadataReachesEvaluationRedacted()
    {
        var sanitizer = new RecordingSanitizer(GovernanceMetadataSanitizationAction.Redact);
        var evaluator = new CapturingPolicyEvaluator();

        _ = await EvaluateAsync(sanitizer, evaluator);

        Assert.NotNull(evaluator.CapturedMetadata);
        Assert.Equal(RecordingSanitizer.RedactedValue, evaluator.CapturedMetadata[RouteMetadataKey]);
    }

    /// <summary>
    /// Verifies that a dropped request-derived key does not reappear in the evaluation context.
    /// </summary>
    /// <remarks>
    /// The evaluation context was previously built by merging raw request metadata underneath the sanitized
    /// dictionary. A redacted value overrode the raw entry by key, but a dropped key was simply absent from the
    /// sanitized dictionary, so the merge put the original value back and the strongest non-terminal sanitization
    /// action had no effect.
    /// </remarks>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DroppedRequestMetadataDoesNotReappearInEvaluationContext()
    {
        var sanitizer = new RecordingSanitizer(GovernanceMetadataSanitizationAction.Drop);
        var evaluator = new CapturingPolicyEvaluator();

        _ = await EvaluateAsync(sanitizer, evaluator);

        Assert.NotNull(evaluator.CapturedMetadata);
        Assert.DoesNotContain(RouteMetadataKey, evaluator.CapturedMetadata.Keys, StringComparer.Ordinal);
    }

    /// <summary>
    /// Verifies that endpoint metadata still reaches evaluation when a request-derived key is dropped.
    /// </summary>
    /// <returns>A task that represents the asynchronous test operation.</returns>
    [Fact]
    public async Task DroppingRequestMetadataPreservesEndpointMetadata()
    {
        var sanitizer = new RecordingSanitizer(GovernanceMetadataSanitizationAction.Drop);
        var evaluator = new CapturingPolicyEvaluator();

        _ = await EvaluateAsync(sanitizer, evaluator);

        Assert.NotNull(evaluator.CapturedMetadata);
        Assert.Equal("sample.sanitization", evaluator.CapturedMetadata["endpoint.operation_name"]);
    }

    /// <summary>
    /// Verifies that opting out of merging leaves supplied metadata authoritative while still normalizing keys.
    /// </summary>
    [Fact]
    public void ToEvaluationContextCanTreatSuppliedMetadataAsAuthoritative()
    {
        var correlation = new AsiBackboneHttpRequestCorrelation(
            correlationId: "correlation-1",
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [RouteMetadataKey] = RouteMetadataValue
            });
        var supplied = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [" endpoint.operation_name "] = " sample "
        };

        AsiBackboneConstraintEvaluationContext merged = correlation.ToEvaluationContext(metadata: supplied);
        AsiBackboneConstraintEvaluationContext authoritative = correlation.ToEvaluationContext(
            metadata: supplied,
            mergeRequestMetadata: false);

        Assert.Contains(RouteMetadataKey, merged.Metadata.Keys, StringComparer.Ordinal);
        Assert.DoesNotContain(RouteMetadataKey, authoritative.Metadata.Keys, StringComparer.Ordinal);
        Assert.Equal("sample", authoritative.Metadata["endpoint.operation_name"]);
    }

    private static async Task<AsiBackboneEndpointGovernanceResult> EvaluateAsync(
        IGovernanceMetadataSanitizer sanitizer,
        CapturingPolicyEvaluator evaluator)
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton(sanitizer)
            .AddAsiBackboneAspNetCore()
            .AddSingleton<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>>(evaluator)
            .AddSingleton<IAsiBackboneHttpRequestCorrelationResolver>(new StubCorrelationResolver())
            .BuildServiceProvider(validateScopes: true);

        using IServiceScope scope = services.CreateScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            TraceIdentifier = "trace-sanitization"
        };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequireGovernancePolicyAttribute(typeof(SanitizationPolicy))),
            "sample.sanitization");
        var descriptor = AsiBackboneEndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IAsiBackboneEndpointGovernanceService service =
            scope.ServiceProvider.GetRequiredService<IAsiBackboneEndpointGovernanceService>();

        return await service.EvaluateAsync(httpContext, descriptor, TestContext.Current.CancellationToken);
    }

    private sealed class SanitizationPolicy
    {
    }

    private sealed class StubCorrelationResolver : IAsiBackboneHttpRequestCorrelationResolver
    {
        public AsiBackboneHttpRequestCorrelation ResolveRequestCorrelation()
        {
            return new AsiBackboneHttpRequestCorrelation(
                correlationId: "correlation-sanitization",
                traceId: "trace-sanitization",
                metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    [RouteMetadataKey] = RouteMetadataValue
                });
        }
    }

    private sealed class RecordingSanitizer(GovernanceMetadataSanitizationAction routeKeyAction)
        : IGovernanceMetadataSanitizer
    {
        public const string RedactedValue = "[redacted]";

        public IReadOnlyDictionary<string, string>? ObservedMetadata { get; private set; }

        public ValueTask<GovernanceMetadataSanitizationResult> SanitizeAsync(
            IReadOnlyDictionary<string, string>? metadata,
            CancellationToken cancellationToken = default)
        {
            ObservedMetadata = metadata;

            Dictionary<string, string> sanitized = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> item in metadata ?? new Dictionary<string, string>(StringComparer.Ordinal))
            {
                if (!string.Equals(item.Key, RouteMetadataKey, StringComparison.Ordinal))
                {
                    sanitized[item.Key] = item.Value;
                    continue;
                }

                switch (routeKeyAction)
                {
                    case GovernanceMetadataSanitizationAction.Redact:
                        sanitized[item.Key] = RedactedValue;
                        break;
                    case GovernanceMetadataSanitizationAction.Drop:
                        // Mirrors DefaultGovernanceMetadataSanitizer: a dropped key is simply not carried forward.
                        break;
                    case GovernanceMetadataSanitizationAction.Allow:
                        break;
                    case GovernanceMetadataSanitizationAction.Warn:
                        break;
                    case GovernanceMetadataSanitizationAction.Deny:
                        break;
                    default:
                        sanitized[item.Key] = item.Value;
                        break;
                }
            }

            GovernanceMetadataSanitizationAction overallAction =
                routeKeyAction is GovernanceMetadataSanitizationAction.Drop
                    ? GovernanceMetadataSanitizationAction.Drop
                    : routeKeyAction;

            return ValueTask.FromResult(GovernanceMetadataSanitizationResult.Create(
                overallAction,
                sanitized,
                Array.Empty<OperationReason>(),
                GovernanceMetadataBudgetValidationResult.Create(sanitized, Array.Empty<string>(), 0)));
        }
    }

    private sealed class CapturingPolicyEvaluator : IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>
    {
        public IReadOnlyDictionary<string, string>? CapturedMetadata { get; private set; }

        public ValueTask<GovernanceDecision> EvaluateAsync(
            AsiBackboneConstraintEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            CapturedMetadata = context.Metadata;

            return ValueTask.FromResult(GovernanceDecision.Allow(correlationId: context.CorrelationId));
        }
    }
}
