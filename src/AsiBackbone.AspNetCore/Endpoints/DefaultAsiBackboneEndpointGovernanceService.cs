using AsiBackbone.AspNetCore.Actors;
using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.AspNetCore.Handshakes;
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
public sealed class DefaultAsiBackboneEndpointGovernanceService : IAsiBackboneEndpointGovernanceService
{
    private const string MetadataSanitizationDeniedReasonCode = "endpoint.metadata_sanitization.denied";
    private const string MetadataSanitizationDeniedReasonMessage =
        "Endpoint governance metadata was denied by the configured sanitation policy.";

    private readonly IServiceProvider serviceProvider;
    private readonly IAsiBackboneHttpActorContextResolver actorContextResolver;
    private readonly IAsiBackboneHttpRequestCorrelationResolver requestCorrelationResolver;
    private readonly IAsiBackboneAcknowledgmentChallengeService acknowledgmentChallengeService;
    private readonly AsiBackboneEndpointGovernanceOptions endpointOptions;
    private readonly AsiBackboneHttpResultMappingOptions resultOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAsiBackboneEndpointGovernanceService" /> class.
    /// </summary>
    public DefaultAsiBackboneEndpointGovernanceService(
        IServiceProvider serviceProvider,
        IAsiBackboneHttpActorContextResolver actorContextResolver,
        IAsiBackboneHttpRequestCorrelationResolver requestCorrelationResolver,
        IAsiBackboneAcknowledgmentChallengeService acknowledgmentChallengeService,
        IOptions<AsiBackboneEndpointGovernanceOptions> endpointOptions,
        IOptions<AsiBackboneHttpResultMappingOptions> resultOptions)
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
    public async ValueTask<AsiBackboneEndpointGovernanceResult> EvaluateAsync(
        HttpContext httpContext,
        AsiBackboneEndpointGovernanceDescriptor descriptor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(descriptor);
        cancellationToken.ThrowIfCancellationRequested();

        if (!descriptor.HasGovernanceMetadata)
        {
            return AsiBackboneEndpointGovernanceResult.Allow();
        }

        var optionalServices = new EndpointGovernanceOptionalServiceResolver(
            httpContext.RequestServices,
            serviceProvider);
        AsiBackboneHttpRequestCorrelation correlation = requestCorrelationResolver.ResolveRequestCorrelation();
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
        AsiBackboneConstraintEvaluationContext evaluationContext = correlation.ToEvaluationContext(
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
        AsiBackboneHttpRequestCorrelation correlation,
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
        AsiBackboneEndpointGovernanceDescriptor descriptor,
        EndpointGovernanceOptionalServiceResolver optionalServices,
        AsiBackboneHttpRequestCorrelation correlation,
        IReadOnlyDictionary<string, string> endpointMetadata,
        AsiBackboneConstraintEvaluationContext evaluationContext,
        CancellationToken cancellationToken)
    {
        GovernanceDecision allowedDecision = CreateAllowDecision(evaluationContext, correlation.TraceId);
        if (descriptor.PolicyTypes.Count == 0)
        {
            return new DecisionStageResult(allowedDecision, TerminalResult: null);
        }

        IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>? evaluator =
            optionalServices.GetPolicyEvaluator();
        if (evaluator is null)
        {
            AsiBackboneEndpointGovernanceResult terminalResult = endpointOptions.FailClosedWhenPolicyEvaluatorMissing
                ? CreateConfigurationFailure(
                    httpContext,
                    descriptor,
                    endpointMetadata,
                    "endpoint.policy_evaluator.missing",
                    "Endpoint governance policy metadata was present, but no AsiBackbone policy evaluator was registered.",
                    allowedDecision,
                    "aspnetcore.endpoint.governance.configuration.policy_evaluator")
                : AsiBackboneEndpointGovernanceResult.Allow(allowedDecision);

            return new DecisionStageResult(allowedDecision, terminalResult);
        }

        GovernanceDecision decision = await evaluator
            .EvaluateAsync(evaluationContext, cancellationToken)
            .ConfigureAwait(false);

        return new DecisionStageResult(decision, TerminalResult: null);
    }

    private async ValueTask<DecisionStageResult> ValidateCapabilityAsync(
        HttpContext httpContext,
        AsiBackboneEndpointGovernanceDescriptor descriptor,
        EndpointGovernanceOptionalServiceResolver optionalServices,
        IReadOnlyDictionary<string, string> endpointMetadata,
        GovernanceDecision decision,
        CancellationToken cancellationToken)
    {
        if (!decision.CanProceed || descriptor.CapabilityScopes.Count == 0)
        {
            return new DecisionStageResult(decision, TerminalResult: null);
        }

        IAsiBackboneEndpointCapabilityGrantValidator? capabilityValidator =
            optionalServices.GetCapabilityValidator();
        if (capabilityValidator is null)
        {
            AsiBackboneEndpointGovernanceResult terminalResult = endpointOptions.FailClosedWhenCapabilityValidatorMissing
                ? CreateCapabilityFailure(
                    httpContext,
                    descriptor,
                    endpointMetadata,
                    "endpoint.capability_validator.missing",
                    "Endpoint capability metadata was present, but no host-owned endpoint capability validator was registered.",
                    decision,
                    "aspnetcore.endpoint.governance.capability.configuration")
                : AsiBackboneEndpointGovernanceResult.Allow(decision);

            return new DecisionStageResult(decision, terminalResult);
        }

        GovernanceDecision validatedDecision = await capabilityValidator
            .ValidateAsync(httpContext, descriptor, decision, cancellationToken)
            .ConfigureAwait(false);

        return new DecisionStageResult(validatedDecision, TerminalResult: null);
    }

    private async ValueTask<AuditStageResult> EmitAuditAsync(
        HttpContext httpContext,
        AsiBackboneEndpointGovernanceDescriptor descriptor,
        EndpointGovernanceOptionalServiceResolver optionalServices,
        IReadOnlyDictionary<string, string> endpointMetadata,
        GovernanceDecision decision,
        CancellationToken cancellationToken)
    {
        if (!descriptor.EmitGovernanceAudit)
        {
            return new AuditStageResult(Actor: null, TerminalResult: null);
        }

        IAsiBackboneAuditSink? auditSink = optionalServices.GetAuditSink();
        if (auditSink is null)
        {
            AsiBackboneEndpointGovernanceResult terminalResult = endpointOptions.FailClosedWhenAuditSinkMissing
                ? CreateConfigurationFailure(
                    httpContext,
                    descriptor,
                    endpointMetadata,
                    "endpoint.audit_sink.missing",
                    "Endpoint governance audit emission was requested, but no host-owned audit sink was registered.",
                    decision,
                    "aspnetcore.endpoint.governance.configuration.audit_sink")
                : AsiBackboneEndpointGovernanceResult.Allow(decision);

            return new AuditStageResult(Actor: null, terminalResult);
        }

        IAsiBackboneActorContext actor = actorContextResolver.ResolveActorContext();
        var residue = AuditResidue.FromDecision(
            actor,
            descriptor.OperationName,
            decision,
            metadata: endpointMetadata,
            decisionStage: "aspnetcore.endpoint.governance");

        await auditSink.WriteAsync(residue, cancellationToken).ConfigureAwait(false);
        return new AuditStageResult(actor, TerminalResult: null);
    }

    private AsiBackboneEndpointGovernanceResult CreateCompletedResult(
        AsiBackboneEndpointGovernanceDescriptor descriptor,
        IReadOnlyDictionary<string, string> endpointMetadata,
        GovernanceDecision decision,
        IAsiBackboneActorContext? actor)
    {
        if (decision.RequiresAcknowledgment && descriptor.RequiresLiabilityHandshake)
        {
            actor ??= actorContextResolver.ResolveActorContext();
            AsiBackboneAcknowledgmentChallenge challenge = acknowledgmentChallengeService.CreateChallenge(
                actor,
                descriptor.OperationName,
                decision,
                endpointMetadata);

            IResult challengeResult = Microsoft.AspNetCore.Http.Results.Json(
                challenge,
                statusCode: endpointOptions.AcknowledgmentChallengeStatusCode);

            return AsiBackboneEndpointGovernanceResult.Challenge(challenge, challengeResult, decision);
        }

        return decision.CanProceed
            ? AsiBackboneEndpointGovernanceResult.Allow(decision)
            : CreateBlockedDecisionResult(decision);
    }

    private AsiBackboneEndpointGovernanceResult CreateMetadataSanitizationFailure(
        AsiBackboneHttpRequestCorrelation correlation,
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
        AsiBackboneConstraintEvaluationContext evaluationContext,
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
        AsiBackboneEndpointGovernanceResult? TerminalResult);

    private readonly record struct DecisionStageResult(
        GovernanceDecision Decision,
        AsiBackboneEndpointGovernanceResult? TerminalResult);

    private readonly record struct AuditStageResult(
        IAsiBackboneActorContext? Actor,
        AsiBackboneEndpointGovernanceResult? TerminalResult);

    private sealed class EndpointGovernanceOptionalServiceResolver(
        IServiceProvider requestServices,
        IServiceProvider governanceServices)
    {
        private IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>? policyEvaluator;
        private IAsiBackboneEndpointCapabilityGrantValidator? capabilityValidator;
        private IAsiBackboneAuditSink? auditSink;
        private IGovernanceMetadataSanitizer? metadataSanitizer;
        private bool policyEvaluatorResolved;
        private bool capabilityValidatorResolved;
        private bool auditSinkResolved;
        private bool metadataSanitizerResolved;

        public IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>? GetPolicyEvaluator()
        {
            if (!policyEvaluatorResolved)
            {
                policyEvaluator = requestServices
                    .GetService<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>>();
                policyEvaluatorResolved = true;
            }

            return policyEvaluator;
        }

        public IAsiBackboneEndpointCapabilityGrantValidator? GetCapabilityValidator()
        {
            if (!capabilityValidatorResolved)
            {
                capabilityValidator = requestServices.GetService<IAsiBackboneEndpointCapabilityGrantValidator>();
                capabilityValidatorResolved = true;
            }

            return capabilityValidator;
        }

        public IAsiBackboneAuditSink? GetAuditSink()
        {
            if (!auditSinkResolved)
            {
                auditSink = governanceServices.GetService<IAsiBackboneAuditSink>();
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

    private AsiBackboneEndpointGovernanceResult CreateBlockedDecisionResult(GovernanceDecision decision)
    {
        return decision.IsDenied && resultOptions.DeniedStatusCode == StatusCodes.Status403Forbidden
            ? AsiBackboneEndpointGovernanceResult.BlockWithDefaultFailure(decision)
            : AsiBackboneEndpointGovernanceResult.Block(decision.ToHttpResult(resultOptions), decision);
    }

    private AsiBackboneEndpointGovernanceResult CreateConfigurationFailure(
        HttpContext httpContext,
        AsiBackboneEndpointGovernanceDescriptor descriptor,
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
            ? AsiBackboneEndpointGovernanceResult.Block(
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
            : AsiBackboneEndpointGovernanceResult.Block(
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

    private AsiBackboneEndpointGovernanceResult CreateCapabilityFailure(
        HttpContext httpContext,
        AsiBackboneEndpointGovernanceDescriptor descriptor,
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
            ? AsiBackboneEndpointGovernanceResult.Block(
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
            : AsiBackboneEndpointGovernanceResult.Block(
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
