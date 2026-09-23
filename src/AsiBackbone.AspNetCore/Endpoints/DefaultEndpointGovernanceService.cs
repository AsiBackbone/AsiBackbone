using AsiBackbone.AspNetCore.Acknowledgments;
using AsiBackbone.AspNetCore.Actors;
using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.AspNetCore.Results;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Metadata;
using AsiBackbone.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AsiBackbone.AspNetCore.Endpoints;

/// <summary>
/// Default host-adapter implementation for ergonomic ASP.NET Core endpoint governance.
/// </summary>
public sealed class DefaultEndpointGovernanceService : IEndpointGovernanceService
{
    private const string MetadataSanitizationDeniedReasonCode = "endpoint.metadata_sanitization.denied";
    private const string MetadataSanitizationDeniedReasonMessage =
        "Endpoint governance metadata was denied by the configured sanitation policy.";

    private readonly IServiceProvider serviceProvider;
    private readonly IHttpGovernanceActorContextResolver actorContextResolver;
    private readonly IHttpGovernanceRequestCorrelationResolver requestCorrelationResolver;
    private readonly IAcknowledgmentChallengeService acknowledgmentChallengeService;
    private readonly EndpointGovernanceOptions endpointOptions;
    private readonly GovernanceHttpResultMappingOptions resultOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultEndpointGovernanceService" /> class.
    /// </summary>
    public DefaultEndpointGovernanceService(
        IServiceProvider serviceProvider,
        IHttpGovernanceActorContextResolver actorContextResolver,
        IHttpGovernanceRequestCorrelationResolver requestCorrelationResolver,
        IAcknowledgmentChallengeService acknowledgmentChallengeService,
        IOptions<EndpointGovernanceOptions> endpointOptions,
        IOptions<GovernanceHttpResultMappingOptions> resultOptions)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.actorContextResolver = actorContextResolver ?? throw new ArgumentNullException(nameof(actorContextResolver));
        this.requestCorrelationResolver = requestCorrelationResolver ?? throw new ArgumentNullException(nameof(requestCorrelationResolver));
        this.acknowledgmentChallengeService = acknowledgmentChallengeService ?? throw new ArgumentNullException(nameof(acknowledgmentChallengeService));
        this.endpointOptions = endpointOptions?.Value ?? throw new ArgumentNullException(nameof(endpointOptions));
        this.resultOptions = resultOptions?.Value ?? throw new ArgumentNullException(nameof(resultOptions));
        this.endpointOptions.Validate();
        this.resultOptions.Validate();
    }

    /// <inheritdoc />
    public async ValueTask<EndpointGovernanceResult> EvaluateAsync(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();

        if (!descriptor.HasGovernanceMetadata)
        {
            return EndpointGovernanceResult.Allow();
        }

        var optionalServices = new EndpointGovernanceOptionalServiceResolver(
            httpContext.RequestServices,
            serviceProvider);
        GovernanceHttpRequestCorrelation correlation = requestCorrelationResolver.ResolveRequestCorrelation();
        IReadOnlyDictionary<string, string> endpointMetadata = descriptor.ToMetadata(endpointOptions.MetadataMode);

        IReadOnlyDictionary<string, string> mergedMetadata = correlation.MergeMetadata(endpointMetadata);
        MetadataStageResult metadataStage = await SanitizeMetadataAsync(
            optionalServices,
            correlation,
            mergedMetadata,
            cancellationToken).ConfigureAwait(false);

        if (metadataStage.TerminalResult is not null)
        {
            return metadataStage.TerminalResult;
        }

        endpointMetadata = metadataStage.Metadata;

        // The sanitized dictionary already contains request metadata, so it is authoritative. Merging request
        // metadata underneath it again would reintroduce any entry the sanitizer dropped: a dropped key is simply
        // absent from the sanitized result, so there would be nothing to override the raw value.
        GovernanceEvaluationContext evaluationContext = correlation.ToEvaluationContext(
            endpointOptions.PolicyVersion,
            endpointOptions.PolicyHash,
            endpointMetadata,
            mergeRequestMetadata: false);

        DecisionStageResult policyStage = await EvaluatePolicyAsync(
            httpContext,
            descriptor,
            optionalServices,
            correlation,
            endpointMetadata,
            evaluationContext,
            cancellationToken).ConfigureAwait(false);

        if (policyStage.TerminalResult is not null)
        {
            return policyStage.TerminalResult;
        }

        DecisionStageResult capabilityStage = await ValidateCapabilityAsync(
            httpContext,
            descriptor,
            optionalServices,
            endpointMetadata,
            policyStage.Decision,
            cancellationToken).ConfigureAwait(false);

        if (capabilityStage.TerminalResult is not null)
        {
            return capabilityStage.TerminalResult;
        }

        AuditStageResult auditStage = await EmitAuditAsync(
            httpContext,
            descriptor,
            optionalServices,
            endpointMetadata,
            capabilityStage.Decision,
            cancellationToken).ConfigureAwait(false);

        return auditStage.TerminalResult is not null
            ? auditStage.TerminalResult
            : CreateCompletedResult(
            descriptor,
            endpointMetadata,
            capabilityStage.Decision,
            auditStage.Actor);
    }

    private async ValueTask<MetadataStageResult> SanitizeMetadataAsync(
        EndpointGovernanceOptionalServiceResolver optionalServices,
        GovernanceHttpRequestCorrelation correlation,
        IReadOnlyDictionary<string, string> endpointMetadata,
        CancellationToken cancellationToken)
    {
        IGovernanceMetadataSanitizer? metadataSanitizer = optionalServices.GetMetadataSanitizer();
        if (metadataSanitizer is null)
        {
            return new MetadataStageResult(endpointMetadata, TerminalResult: null);
        }

        GovernanceMetadataSanitizationResult sanitizationResult = await metadataSanitizer
            .SanitizeAsync(endpointMetadata, cancellationToken)
            .ConfigureAwait(false);

        return sanitizationResult.CanProceed
            ? new MetadataStageResult(sanitizationResult.SanitizedMetadata, TerminalResult: null)
            : new MetadataStageResult(
                endpointMetadata,
                CreateMetadataSanitizationFailure(correlation, sanitizationResult));
    }

    private async ValueTask<DecisionStageResult> EvaluatePolicyAsync(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        EndpointGovernanceOptionalServiceResolver optionalServices,
        GovernanceHttpRequestCorrelation correlation,
        IReadOnlyDictionary<string, string> endpointMetadata,
        GovernanceEvaluationContext evaluationContext,
        CancellationToken cancellationToken)
    {
        GovernanceDecision allowedDecision = CreateAllowDecision(evaluationContext, correlation.TraceId);
        if (descriptor.PolicyTypes.Count == 0)
        {
            return new DecisionStageResult(allowedDecision, TerminalResult: null);
        }

        IGovernancePolicyEvaluator<GovernanceEvaluationContext>? evaluator =
            optionalServices.GetPolicyEvaluator();
        if (evaluator is null)
        {
            EndpointGovernanceResult terminalResult = endpointOptions.FailClosedWhenPolicyEvaluatorMissing
                ? CreateConfigurationFailure(
                    httpContext,
                    descriptor,
                    endpointMetadata,
                    "endpoint.policy_evaluator.missing",
                    "Endpoint governance policy metadata was present, but no AsiBackbone policy evaluator was registered.",
                    allowedDecision,
                    "aspnetcore.endpoint.governance.configuration.policy_evaluator")
                : EndpointGovernanceResult.Allow(allowedDecision);

            return new DecisionStageResult(allowedDecision, terminalResult);
        }

        GovernanceDecision decision = await evaluator
            .EvaluateAsync(evaluationContext, cancellationToken)
            .ConfigureAwait(false);

        return new DecisionStageResult(decision, TerminalResult: null);
    }

    private async ValueTask<DecisionStageResult> ValidateCapabilityAsync(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        EndpointGovernanceOptionalServiceResolver optionalServices,
        IReadOnlyDictionary<string, string> endpointMetadata,
        GovernanceDecision decision,
        CancellationToken cancellationToken)
    {
        if (!decision.CanProceed || descriptor.CapabilityScopes.Count == 0)
        {
            return new DecisionStageResult(decision, TerminalResult: null);
        }

        IEndpointCapabilityGrantValidator? capabilityValidator =
            optionalServices.GetCapabilityValidator();
        if (capabilityValidator is null)
        {
            EndpointGovernanceResult terminalResult = endpointOptions.FailClosedWhenCapabilityValidatorMissing
                ? CreateCapabilityFailure(
                    httpContext,
                    descriptor,
                    endpointMetadata,
                    "endpoint.capability_validator.missing",
                    "Endpoint capability metadata was present, but no host-owned endpoint capability validator was registered.",
                    decision,
                    "aspnetcore.endpoint.governance.capability.configuration")
                : EndpointGovernanceResult.Allow(decision);

            return new DecisionStageResult(decision, terminalResult);
        }

        GovernanceDecision validatedDecision = await capabilityValidator
            .ValidateAsync(httpContext, descriptor, decision, cancellationToken)
            .ConfigureAwait(false);

        return new DecisionStageResult(validatedDecision, TerminalResult: null);
    }

    private async ValueTask<AuditStageResult> EmitAuditAsync(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        EndpointGovernanceOptionalServiceResolver optionalServices,
        IReadOnlyDictionary<string, string> endpointMetadata,
        GovernanceDecision decision,
        CancellationToken cancellationToken)
    {
        if (!descriptor.EmitGovernanceAudit)
        {
            return new AuditStageResult(Actor: null, TerminalResult: null);
        }

        IDecisionReceiptSink? auditSink = optionalServices.GetAuditSink();
        if (auditSink is null)
        {
            EndpointGovernanceResult terminalResult = endpointOptions.FailClosedWhenAuditSinkMissing
                ? CreateConfigurationFailure(
                    httpContext,
                    descriptor,
                    endpointMetadata,
                    "endpoint.audit_sink.missing",
                    "Endpoint governance audit emission was requested, but no host-owned audit sink was registered.",
                    decision,
                    "aspnetcore.endpoint.governance.configuration.audit_sink")
                : EndpointGovernanceResult.Allow(decision);

            return new AuditStageResult(Actor: null, terminalResult);
        }

        IGovernanceActorContext actor = actorContextResolver.ResolveActorContext();
        var residue = DecisionReceipt.FromDecision(
            actor,
            descriptor.OperationName,
            decision,
            metadata: endpointMetadata,
            decisionStage: "aspnetcore.endpoint.governance");

        await auditSink.WriteAsync(residue, cancellationToken).ConfigureAwait(false);
        return new AuditStageResult(actor, TerminalResult: null);
    }

    private EndpointGovernanceResult CreateCompletedResult(
        EndpointGovernanceDescriptor descriptor,
        IReadOnlyDictionary<string, string> endpointMetadata,
        GovernanceDecision decision,
        IGovernanceActorContext? actor)
    {
        if (decision.RequiresAcknowledgment && descriptor.RequiresAcknowledgment)
        {
            actor ??= actorContextResolver.ResolveActorContext();

            if (!AcknowledgmentActorBinding.IsSufficient(actor))
            {
                return EndpointGovernanceResult.Block(
                    Microsoft.AspNetCore.Http.Results.Problem(
                        title: "Acknowledgment challenge actor binding failed.",
                        detail: AcknowledgmentActorBinding.FailureMessage,
                        statusCode: StatusCodes.Status403Forbidden,
                        extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                        {
                            ["reasonCodes"] = new[] { AcknowledgmentActorBinding.FailureCode },
                            ["outcome"] = decision.Outcome.ToString()
                        }),
                    decision);
            }

            AcknowledgmentChallenge challenge = acknowledgmentChallengeService.CreateChallenge(
                actor,
                descriptor.OperationName,
                decision,
                endpointMetadata);

            IResult challengeResult = Microsoft.AspNetCore.Http.Results.Json(
                challenge,
                statusCode: endpointOptions.AcknowledgmentChallengeStatusCode);

            return EndpointGovernanceResult.Challenge(challenge, challengeResult, decision);
        }

        return decision.CanProceed
            ? EndpointGovernanceResult.Allow(decision)
            : CreateBlockedDecisionResult(decision);
    }

    private EndpointGovernanceResult CreateMetadataSanitizationFailure(
        GovernanceHttpRequestCorrelation correlation,
        GovernanceMetadataSanitizationResult sanitizationResult)
    {
        var reasons = new List<OperationReason>(sanitizationResult.Reasons.Count + 1)
        {
            OperationReason.Create(
                MetadataSanitizationDeniedReasonCode,
                MetadataSanitizationDeniedReasonMessage)
        };
        reasons.AddRange(sanitizationResult.Reasons);

        var decision = GovernanceDecision.Deny(
            reasons,
            correlationId: correlation.CorrelationId,
            traceId: correlation.TraceId,
            policyVersion: endpointOptions.PolicyVersion,
            policyHash: endpointOptions.PolicyHash);

        return CreateBlockedDecisionResult(decision);
    }

    private static GovernanceDecision CreateAllowDecision(
        GovernanceEvaluationContext evaluationContext,
        string? traceId)
    {
        return GovernanceDecision.Allow(
            correlationId: evaluationContext.CorrelationId,
            traceId: traceId,
            policyVersion: evaluationContext.PolicyVersion,
            policyHash: evaluationContext.PolicyHash);
    }

    private readonly record struct MetadataStageResult(
        IReadOnlyDictionary<string, string> Metadata,
        EndpointGovernanceResult? TerminalResult);

    private readonly record struct DecisionStageResult(
        GovernanceDecision Decision,
        EndpointGovernanceResult? TerminalResult);

    private readonly record struct AuditStageResult(
        IGovernanceActorContext? Actor,
        EndpointGovernanceResult? TerminalResult);

    private sealed class EndpointGovernanceOptionalServiceResolver(
        IServiceProvider requestServices,
        IServiceProvider governanceServices)
    {
        private IGovernancePolicyEvaluator<GovernanceEvaluationContext>? policyEvaluator;
        private IEndpointCapabilityGrantValidator? capabilityValidator;
        private IDecisionReceiptSink? auditSink;
        private IGovernanceMetadataSanitizer? metadataSanitizer;
        private bool policyEvaluatorResolved;
        private bool capabilityValidatorResolved;
        private bool auditSinkResolved;
        private bool metadataSanitizerResolved;

        public IGovernancePolicyEvaluator<GovernanceEvaluationContext>? GetPolicyEvaluator()
        {
            if (!policyEvaluatorResolved)
            {
                policyEvaluator = requestServices
                    .GetService<IGovernancePolicyEvaluator<GovernanceEvaluationContext>>();
                policyEvaluatorResolved = true;
            }

            return policyEvaluator;
        }

        public IEndpointCapabilityGrantValidator? GetCapabilityValidator()
        {
            if (!capabilityValidatorResolved)
            {
                capabilityValidator = requestServices.GetService<IEndpointCapabilityGrantValidator>();
                capabilityValidatorResolved = true;
            }

            return capabilityValidator;
        }

        public IDecisionReceiptSink? GetAuditSink()
        {
            if (!auditSinkResolved)
            {
                auditSink = governanceServices.GetService<IDecisionReceiptSink>();
                auditSinkResolved = true;
            }

            return auditSink;
        }

        public IGovernanceMetadataSanitizer? GetMetadataSanitizer()
        {
            if (!metadataSanitizerResolved)
            {
                metadataSanitizer = requestServices.GetService<IGovernanceMetadataSanitizer>();
                metadataSanitizerResolved = true;
            }

            return metadataSanitizer;
        }
    }

    private EndpointGovernanceResult CreateBlockedDecisionResult(GovernanceDecision decision)
    {
        return decision.IsDenied && resultOptions.DeniedStatusCode == StatusCodes.Status403Forbidden
            ? EndpointGovernanceResult.BlockWithDefaultFailure(decision)
            : EndpointGovernanceResult.Block(decision.ToHttpResult(resultOptions), decision);
    }

    private EndpointGovernanceResult CreateConfigurationFailure(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        IReadOnlyDictionary<string, string> metadata,
        string code,
        string message,
        GovernanceDecision currentDecision,
        string decisionStage)
    {
        var decision = GovernanceDecision.Deny(
            code,
            message,
            correlationId: currentDecision.CorrelationId,
            traceId: currentDecision.TraceId,
            policyVersion: currentDecision.PolicyVersion,
            policyHash: currentDecision.PolicyHash);

        return AsiBackboneEndpointGovernanceDevelopmentDiagnostics.IsEnabled(httpContext, endpointOptions)
            ? EndpointGovernanceResult.Block(
                AsiBackboneEndpointGovernanceDevelopmentDiagnostics.CreateProblem(
                    httpContext,
                    endpointOptions,
                    descriptor,
                    decision,
                    decisionStage,
                    title: "Endpoint governance configuration is incomplete.",
                    detail: message,
                    statusCode: endpointOptions.ConfigurationFailureStatusCode,
                    metadata: metadata),
                decision)
            : EndpointGovernanceResult.Block(
                Microsoft.AspNetCore.Http.Results.Problem(
                    title: "Endpoint governance configuration is incomplete.",
                    detail: message,
                    statusCode: endpointOptions.ConfigurationFailureStatusCode,
                    extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["reasonCodes"] = decision.ReasonCodes,
                        ["outcome"] = decision.Outcome.ToString()
                    }),
                decision);
    }

    private EndpointGovernanceResult CreateCapabilityFailure(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        IReadOnlyDictionary<string, string> metadata,
        string code,
        string message,
        GovernanceDecision currentDecision,
        string decisionStage)
    {
        var decision = GovernanceDecision.Deny(
            code,
            message,
            correlationId: currentDecision.CorrelationId,
            traceId: currentDecision.TraceId,
            policyVersion: currentDecision.PolicyVersion,
            policyHash: currentDecision.PolicyHash);

        return AsiBackboneEndpointGovernanceDevelopmentDiagnostics.IsEnabled(httpContext, endpointOptions)
            ? EndpointGovernanceResult.Block(
                AsiBackboneEndpointGovernanceDevelopmentDiagnostics.CreateProblem(
                    httpContext,
                    endpointOptions,
                    descriptor,
                    decision,
                    decisionStage,
                    title: "Endpoint capability grant validation failed.",
                    detail: message,
                    statusCode: endpointOptions.CapabilityFailureStatusCode,
                    metadata: metadata),
                decision)
            : EndpointGovernanceResult.Block(
                Microsoft.AspNetCore.Http.Results.Problem(
                    title: "Endpoint capability grant validation failed.",
                    detail: message,
                    statusCode: endpointOptions.CapabilityFailureStatusCode,
                    extensions: new Dictionary<string, object?>(StringComparer.Ordinal)
                    {
                        ["reasonCodes"] = decision.ReasonCodes,
                        ["outcome"] = decision.Outcome.ToString()
                    }),
                decision);
    }
}
