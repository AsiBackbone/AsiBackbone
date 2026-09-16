using System.Collections.ObjectModel;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Results;
using AsiBackbone.Core.ThreatModeling;
using Microsoft.Extensions.Logging;

namespace AsiBackbone.Core.Evaluation;

/// <summary>
/// Default policy evaluator that runs the active constraint structure and composes the result into a governance decision.
/// </summary>
/// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
public sealed class DefaultGovernancePolicyEvaluator<TContext> : IGovernancePolicyEvaluator<TContext>
    where TContext : IGovernanceEvaluationContext
{
    private static readonly IReadOnlyList<ConstraintEvaluationResult> EmptyConstraintResults =
        Array.AsReadOnly(Array.Empty<ConstraintEvaluationResult>());

    private const string InvalidAllowedThreatOutcomeMessage =
        "Threat model contributors cannot return an Allowed outcome. Use ThreatAssessment.NoThreat() for no finding, or return Warning, Denied, Deferred, AcknowledgmentRequired, or EscalationRecommended for actionable findings.";

    private static readonly Action<ILogger, string, string, string, Exception?> EmptyPolicyAllowedWarning =
        LoggerMessage.Define<string, string, string>(
            LogLevel.Warning,
            new EventId(4110, nameof(EmptyPolicyAllowedWarning)),
            "Policy evaluation ran with zero constraints while DenyWhenNoConstraints is false; default empty-policy behavior allows the decision. CorrelationId: {CorrelationId}; PolicyVersion: {PolicyVersion}; PolicyHash: {PolicyHash}");

    private static readonly Action<ILogger, string, string, string, string, string, Exception?> ConstraintExceptionDeniedError =
        LoggerMessage.Define<string, string, string, string, string>(
            LogLevel.Error,
            new EventId(4120, nameof(ConstraintExceptionDeniedError)),
            "Policy constraint '{ConstraintName}' threw during evaluation and was converted to a denied governance decision by exception-as-denial policy. ExceptionType: {ExceptionType}; CorrelationId: {CorrelationId}; PolicyVersion: {PolicyVersion}; PolicyHash: {PolicyHash}");

    private static readonly Action<ILogger, string, string, string, string, string, Exception?> ThreatContributorExceptionDeniedError =
        LoggerMessage.Define<string, string, string, string, string>(
            LogLevel.Error,
            new EventId(4130, nameof(ThreatContributorExceptionDeniedError)),
            "Threat model contributor '{ContributorName}' threw during evaluation and was converted to a denied governance decision by exception-as-denial policy. ExceptionType: {ExceptionType}; CorrelationId: {CorrelationId}; PolicyVersion: {PolicyVersion}; PolicyHash: {PolicyHash}");

    private readonly IGovernanceConstraint<TContext>[] constraints;
    private readonly IThreatModelContributor<TContext>[] threatModelContributors;
    private readonly IGovernanceDecisionPolicy<TContext>? decisionPolicy;
    private readonly ILogger<DefaultGovernancePolicyEvaluator<TContext>>? logger;
    private readonly GovernancePolicyOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultGovernancePolicyEvaluator{TContext}" /> class.
    /// </summary>
    /// <param name="constraints">The constraints that make up the active policy structure.</param>
    /// <param name="threatModelContributors">Threat model contributors that inspect the context before constraint composition.</param>
    /// <param name="decisionPolicy">Optional decision policy applied after composition.</param>
    /// <param name="options">Evaluator options applied during composition.</param>
    /// <param name="logger">Optional logger used to emit operational warning signals.</param>
    public DefaultGovernancePolicyEvaluator(
        IEnumerable<IGovernanceConstraint<TContext>> constraints,
        IEnumerable<IThreatModelContributor<TContext>>? threatModelContributors,
        IGovernanceDecisionPolicy<TContext>? decisionPolicy,
        GovernancePolicyOptions? options,
        ILogger<DefaultGovernancePolicyEvaluator<TContext>>? logger)
    {
        ArgumentNullException.ThrowIfNull(constraints);

        // Keep exact-sized private snapshots rather than wrapping caller-owned lists.
        // This avoids per-evaluator ReadOnlyCollection<T> wrappers and prevents later caller mutations
        // from changing deterministic constraint/contributor order or behavior.
        this.constraints = [.. constraints];
        this.threatModelContributors = threatModelContributors is null ? [] : [.. threatModelContributors];
        this.decisionPolicy = decisionPolicy;
        this.logger = logger;
        this.options = options ?? new GovernancePolicyOptions();
        this.options.Validate();
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceDecision> EvaluateAsync(
        TContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        ThreatEvaluationResult threatEvaluation = await EvaluateThreatModelContributorsAsync(
            context,
            cancellationToken)
            .ConfigureAwait(false);

        if (threatEvaluation.BlockingDecision is GovernanceDecision threatDecision)
        {
            return await ApplyDecisionPolicyAsync(
                context,
                threatDecision,
                EmptyConstraintResults,
                protectedThreatDecision: threatDecision,
                cancellationToken)
                .ConfigureAwait(false);
        }

        if (constraints.Length == 0)
        {
            return await EvaluateEmptyPolicyAsync(
                context,
                threatEvaluation.WarningReasons,
                cancellationToken)
                .ConfigureAwait(false);
        }

        List<ConstraintEvaluationResult>? results = CreateConstraintResultsBuffer();
        var denials = new OperationReasonAccumulator();
        var warnings = new OperationReasonAccumulator();
        warnings.AddRange(threatEvaluation.WarningReasons);

        foreach (IGovernanceConstraint<TContext> constraint in constraints)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ConstraintEvaluationResult result;
            try
            {
                result = await constraint
                    .EvaluateAsync(context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (ShouldConvertExceptionToDenial(exception, options.TreatConstraintExceptionAsDenial))
            {
                return await CreateConstraintExceptionDecisionAsync(
                    context,
                    constraint,
                    results,
                    exception,
                    cancellationToken)
                    .ConfigureAwait(false);
            }

            results?.Add(result);

            if (!AccumulateConstraintResult(
                    result,
                    ref denials,
                    ref warnings,
                    options.ShortCircuitOnFirstDenial))
            {
                break;
            }
        }

        GovernanceDecision composedDecision = Compose(
            context,
            denials,
            warnings,
            includeWarningsWhenDenied: options.ShortCircuitOnFirstDenial);

        return await ApplyDecisionPolicyAsync(
            context,
            composedDecision,
            CreateConstraintResultsView(results),
            CreateProtectedThreatWarningDecision(context, threatEvaluation.WarningReasons, composedDecision),
            cancellationToken)
            .ConfigureAwait(false);
    }

    private static bool ShouldConvertExceptionToDenial(Exception exception, bool treatExceptionAsDenial)
    {
        return treatExceptionAsDenial &&
               exception is not OperationCanceledException &&
               !IsCriticalException(exception);
    }

    private static bool IsCriticalException(Exception exception)
    {
        return exception is OutOfMemoryException or
               StackOverflowException or
               AccessViolationException or
               AppDomainUnloadedException or
               BadImageFormatException or
               InvalidProgramException ||
               (exception.InnerException is not null && IsCriticalException(exception.InnerException));
    }

    private List<ConstraintEvaluationResult>? CreateConstraintResultsBuffer()
    {
        return decisionPolicy is null
            ? null
            : new List<ConstraintEvaluationResult>(constraints.Length);
    }

    private static bool AccumulateConstraintResult(
        ConstraintEvaluationResult result,
        ref OperationReasonAccumulator denials,
        ref OperationReasonAccumulator warnings,
        bool shortCircuitOnFirstDenial)
    {
        if (result.IsDenied)
        {
            denials.AddRange(result.Reasons);

            if (!shortCircuitOnFirstDenial)
            {
                warnings = default;
            }

            return !shortCircuitOnFirstDenial;
        }

        if (result.IsWarning && denials.Count == 0)
        {
            warnings.AddRange(result.Reasons);
        }

        return true;
    }

    private async ValueTask<GovernanceDecision> EvaluateEmptyPolicyAsync(
        TContext context,
        IReadOnlyList<OperationReason> threatWarningReasons,
        CancellationToken cancellationToken)
    {
        if (!options.DenyWhenNoConstraints)
        {
            LogEmptyPolicyAllowed(context);
        }

        GovernanceDecision noConstraintDecision = CreateNoConstraintDecision(
            context,
            threatWarningReasons,
            options.DenyWhenNoConstraints);

        return await ApplyDecisionPolicyAsync(
            context,
            noConstraintDecision,
            EmptyConstraintResults,
            protectedThreatDecision: threatWarningReasons.Count > 0 ? noConstraintDecision : null,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private static GovernanceDecision Compose(
        TContext context,
        OperationReasonAccumulator denials,
        OperationReasonAccumulator warnings,
        bool includeWarningsWhenDenied)
    {
        return denials.Count > 0
            ? includeWarningsWhenDenied && warnings.Count > 0
                ? GovernanceDecision.Deny(
                    warnings.ConcatAsArray(denials),
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash)
                : CreateDeniedDecision(context, denials)
            : warnings.Count > 0
            ? CreateWarningDecision(context, warnings)
            : GovernanceDecision.Allow(
            correlationId: context.CorrelationId,
            policyVersion: context.PolicyVersion,
            policyHash: context.PolicyHash);
    }

    private static GovernanceDecision CreateNoConstraintDecision(
        TContext context,
        IReadOnlyList<OperationReason> threatWarningReasons)
    {
        return threatWarningReasons.Count > 0
            ? GovernanceDecision.Warning(
                threatWarningReasons,
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash)
            : GovernanceDecision.Allow(
            correlationId: context.CorrelationId,
            policyVersion: context.PolicyVersion,
            policyHash: context.PolicyHash);
    }

    private GovernanceDecision CreateNoConstraintDecision(
        TContext context,
        IReadOnlyList<OperationReason> threatWarningReasons,
        bool denyWhenNoConstraints)
    {
        return !denyWhenNoConstraints
            ? CreateNoConstraintDecision(context, threatWarningReasons)
            : GovernanceDecision.Deny(
            options.NoConstraintsReasonCode,
            options.NoConstraintsReasonMessage,
            correlationId: context.CorrelationId,
            policyVersion: context.PolicyVersion,
            policyHash: context.PolicyHash);
    }

    private static GovernanceDecision CreateDeniedDecision(
        TContext context,
        OperationReasonAccumulator denials)
    {
        return denials.Count == 1
            ? GovernanceDecision.Deny(
                denials.FirstReason!,
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash)
            : GovernanceDecision.Deny(
                denials.AsReadOnlyList(),
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash);
    }

    private static GovernanceDecision CreateWarningDecision(
        TContext context,
        OperationReasonAccumulator warnings)
    {
        return warnings.Count == 1
            ? GovernanceDecision.Warning(
                warnings.FirstReason!,
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash)
            : GovernanceDecision.Warning(
                warnings.AsReadOnlyList(),
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash);
    }

    private static IReadOnlyList<ConstraintEvaluationResult> CreateConstraintResultsView(
        List<ConstraintEvaluationResult>? results)
    {
        return results is null || results.Count == 0
            ? EmptyConstraintResults
            : results.AsReadOnly();
    }

    private static GovernanceDecision? CreateProtectedThreatWarningDecision(
        TContext context,
        IReadOnlyList<OperationReason> threatWarningReasons,
        GovernanceDecision composedDecision)
    {
        return threatWarningReasons.Count > 0 && composedDecision.CanProceed
            ? GovernanceDecision.Warning(
                threatWarningReasons,
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash)
            : null;
    }

    private async ValueTask<GovernanceDecision> ApplyDecisionPolicyAsync(
        TContext context,
        GovernanceDecision decision,
        IReadOnlyList<ConstraintEvaluationResult> results,
        GovernanceDecision? protectedThreatDecision,
        CancellationToken cancellationToken)
    {
        if (decisionPolicy is null)
        {
            return decision;
        }

        GovernanceDecision policyDecision = await decisionPolicy
            .ApplyAsync(context, decision, results, cancellationToken)
            .ConfigureAwait(false);

        return options.PreventThreatAssessmentAllowDowngrade && protectedThreatDecision is not null && policyDecision.IsAllowed
            ? protectedThreatDecision
            : policyDecision;
    }

    private async ValueTask<ThreatEvaluationResult> EvaluateThreatModelContributorsAsync(
        TContext context,
        CancellationToken cancellationToken)
    {
        if (threatModelContributors.Length == 0)
        {
            return ThreatEvaluationResult.Empty;
        }

        List<OperationReason>? reasons = null;
        OperationReason? selectedReason = null;
        GovernanceDecisionOutcome? selectedOutcome = null;

        foreach (IThreatModelContributor<TContext> contributor in threatModelContributors)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ThreatAssessment? assessment;
            try
            {
                assessment = await contributor
                    .AssessAsync(context, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception exception) when (ShouldConvertExceptionToDenial(exception, options.TreatThreatContributorExceptionAsDenial))
            {
                return CreateThreatContributorExceptionResult(context, contributor, exception);
            }

            if (assessment is null || !assessment.IsActionable)
            {
                continue;
            }

            GovernanceDecisionOutcome effectiveOutcome = GetEffectiveThreatOutcome(assessment);
            var reason = assessment.ToOperationReason(GetContributorName(contributor), effectiveOutcome);
            GovernanceDecisionOutcome moreRestrictiveOutcome = SelectMoreRestrictiveOutcome(selectedOutcome, effectiveOutcome);

            if (selectedOutcome != moreRestrictiveOutcome)
            {
                selectedOutcome = moreRestrictiveOutcome;
                selectedReason = reason;
            }

            reasons ??= [];
            reasons.Add(reason);
        }

        return reasons is null || selectedOutcome is null
            ? ThreatEvaluationResult.Empty
            : CreateThreatEvaluationResult(context, selectedOutcome.Value, selectedReason!, reasons.AsReadOnly());
    }

    private ThreatEvaluationResult CreateThreatContributorExceptionResult(
        TContext context,
        IThreatModelContributor<TContext> contributor,
        Exception exception)
    {
        string contributorName = GetContributorName(contributor);
        LogThreatContributorExceptionDenied(context, contributorName, exception);

        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["threat.contributor"] = contributorName,
            ["threat.failure"] = exception.GetType().Name
        };

        var reason = OperationReason.Create(
            options.ThreatContributorExceptionReasonCode,
            options.ThreatContributorExceptionReasonMessage,
            metadata);

        var decision = GovernanceDecision.Deny(
            reason,
            correlationId: context.CorrelationId,
            policyVersion: context.PolicyVersion,
            policyHash: context.PolicyHash);

        return ThreatEvaluationResult.ForBlockingDecision(decision);
    }

    private static ThreatEvaluationResult CreateThreatEvaluationResult(
        TContext context,
        GovernanceDecisionOutcome outcome,
        OperationReason selectedReason,
        ReadOnlyCollection<OperationReason> reasons)
    {
        return outcome switch
        {
            GovernanceDecisionOutcome.Denied => ThreatEvaluationResult.ForBlockingDecision(
                GovernanceDecision.Deny(
                    reasons,
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash)),
            GovernanceDecisionOutcome.Deferred => ThreatEvaluationResult.ForBlockingDecision(
                GovernanceDecision.Defer(
                    selectedReason.Code,
                    selectedReason.Message,
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash)),
            GovernanceDecisionOutcome.AcknowledgmentRequired => ThreatEvaluationResult.ForBlockingDecision(
                GovernanceDecision.RequireAcknowledgment(
                    selectedReason.Code,
                    selectedReason.Message,
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash)),
            GovernanceDecisionOutcome.EscalationRecommended => ThreatEvaluationResult.ForBlockingDecision(
                GovernanceDecision.Escalate(
                    selectedReason.Code,
                    selectedReason.Message,
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash)),
            GovernanceDecisionOutcome.Warning => ThreatEvaluationResult.ForWarningReasons(reasons),
            GovernanceDecisionOutcome.Allowed => throw CreateInvalidAllowedThreatOutcomeException(),
            _ => ThreatEvaluationResult.Empty
        };
    }

    private static GovernanceDecisionOutcome GetEffectiveThreatOutcome(ThreatAssessment assessment)
    {
        return assessment.RecommendedOutcome is GovernanceDecisionOutcome.Allowed
            ? throw CreateInvalidAllowedThreatOutcomeException()
            : assessment.RecommendedOutcome;
    }

    private static InvalidOperationException CreateInvalidAllowedThreatOutcomeException()
    {
        return new InvalidOperationException(InvalidAllowedThreatOutcomeMessage);
    }

    private static GovernanceDecisionOutcome SelectMoreRestrictiveOutcome(
        GovernanceDecisionOutcome? current,
        GovernanceDecisionOutcome candidate)
    {
        return current is null
            ? candidate
            : GetThreatOutcomeRank(candidate) > GetThreatOutcomeRank(current.Value)
            ? candidate
            : current.Value;
    }

    private static int GetThreatOutcomeRank(GovernanceDecisionOutcome outcome)
    {
        return outcome switch
        {
            GovernanceDecisionOutcome.Denied => 5,
            GovernanceDecisionOutcome.EscalationRecommended => 4,
            GovernanceDecisionOutcome.AcknowledgmentRequired => 3,
            GovernanceDecisionOutcome.Deferred => 2,
            GovernanceDecisionOutcome.Warning => 1,
            GovernanceDecisionOutcome.Allowed => throw CreateInvalidAllowedThreatOutcomeException(),
            _ => 0
        };
    }

    private static string GetContributorName(IThreatModelContributor<TContext> contributor)
    {
        return string.IsNullOrWhiteSpace(contributor.Name)
            ? "<unnamed>"
            : contributor.Name.Trim();
    }

    private async ValueTask<GovernanceDecision> CreateConstraintExceptionDecisionAsync(
        TContext context,
        IGovernanceConstraint<TContext> constraint,
        List<ConstraintEvaluationResult>? results,
        Exception exception,
        CancellationToken cancellationToken)
    {
        LogConstraintExceptionDenied(context, constraint.Name, exception);

        var exceptionResult = ConstraintEvaluationResult.Deny(
            options.ConstraintExceptionReasonCode,
            options.ConstraintExceptionReasonMessage);
        results?.Add(exceptionResult);

        var exceptionDecision = GovernanceDecision.Deny(
            exceptionResult.Reasons,
            correlationId: context.CorrelationId,
            policyVersion: context.PolicyVersion,
            policyHash: context.PolicyHash);

        return await ApplyDecisionPolicyAsync(
            context,
            exceptionDecision,
            CreateConstraintResultsView(results),
            protectedThreatDecision: null,
            cancellationToken)
            .ConfigureAwait(false);
    }

    private void LogEmptyPolicyAllowed(TContext context)
    {
        if (logger is null)
        {
            return;
        }

        EmptyPolicyAllowedWarning(
            logger,
            context.CorrelationId ?? string.Empty,
            context.PolicyVersion ?? string.Empty,
            context.PolicyHash ?? string.Empty,
            null);
    }

    private void LogConstraintExceptionDenied(
        TContext context,
        string constraintName,
        Exception exception)
    {
        if (logger is null)
        {
            return;
        }

        ConstraintExceptionDeniedError(
            logger,
            string.IsNullOrWhiteSpace(constraintName) ? "<unnamed>" : constraintName,
            exception.GetType().Name,
            context.CorrelationId ?? string.Empty,
            context.PolicyVersion ?? string.Empty,
            context.PolicyHash ?? string.Empty,
            exception);
    }

    private void LogThreatContributorExceptionDenied(
        TContext context,
        string contributorName,
        Exception exception)
    {
        if (logger is null)
        {
            return;
        }

        ThreatContributorExceptionDeniedError(
            logger,
            contributorName,
            exception.GetType().Name,
            context.CorrelationId ?? string.Empty,
            context.PolicyVersion ?? string.Empty,
            context.PolicyHash ?? string.Empty,
            exception);
    }

    private sealed class ThreatEvaluationResult
    {
        private static readonly IReadOnlyList<OperationReason> EmptyReasons =
            Array.AsReadOnly(Array.Empty<OperationReason>());

        private ThreatEvaluationResult(
            IReadOnlyList<OperationReason> warningReasons,
            GovernanceDecision? blockingDecision)
        {
            WarningReasons = warningReasons;
            BlockingDecision = blockingDecision;
        }

        public static ThreatEvaluationResult Empty { get; } = new(EmptyReasons, blockingDecision: null);

        public IReadOnlyList<OperationReason> WarningReasons { get; }

        public GovernanceDecision? BlockingDecision { get; }

        public static ThreatEvaluationResult ForWarningReasons(IReadOnlyList<OperationReason> reasons)
        {
            return new ThreatEvaluationResult(reasons, blockingDecision: null);
        }

        public static ThreatEvaluationResult ForBlockingDecision(GovernanceDecision decision)
        {
            return new ThreatEvaluationResult(EmptyReasons, decision);
        }
    }

    private struct OperationReasonAccumulator
    {
        private List<OperationReason>? additionalReasons;

        public OperationReason? FirstReason { get; private set; }

        public int Count { get; private set; }

        public void AddRange(IReadOnlyList<OperationReason> reasons)
        {
            for (int index = 0; index < reasons.Count; index++)
            {
                Add(reasons[index]);
            }
        }

        public readonly OperationReason[] ConcatAsArray(OperationReasonAccumulator other)
        {
            int totalCount = Count + other.Count;
            if (totalCount == 0)
            {
                return [];
            }

            var reasons = new OperationReason[totalCount];
            int nextIndex = CopyTo(reasons, 0);
            _ = other.CopyTo(reasons, nextIndex);

            return reasons;
        }

        public readonly ReadOnlyCollection<OperationReason> AsReadOnlyList()
        {
            return Count switch
            {
                0 => Array.AsReadOnly(Array.Empty<OperationReason>()),
                1 => Array.AsReadOnly([FirstReason!]),
                _ => additionalReasons!.AsReadOnly()
            };
        }

        private void Add(OperationReason? reason)
        {
            if (reason is null)
            {
                return;
            }

            if (Count == 0)
            {
                FirstReason = reason;
                Count = 1;
                return;
            }

            additionalReasons ??= [FirstReason!];
            additionalReasons.Add(reason);
            Count++;
        }

        private readonly int CopyTo(OperationReason[] destination, int startIndex)
        {
            if (Count == 0)
            {
                return startIndex;
            }

            if (Count == 1)
            {
                destination[startIndex] = FirstReason!;
                return startIndex + 1;
            }

            additionalReasons!.CopyTo(destination, startIndex);
            return startIndex + additionalReasons.Count;
        }
    }
}
