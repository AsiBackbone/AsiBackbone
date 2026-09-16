using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AsiBackbone.AspNetCore.Results;

/// <summary>
/// Provides helpers for translating AsiBackbone Core decisions and operation results into ASP.NET Core HTTP results.
/// </summary>
public static class GovernanceHttpResultMappingExtensions
{
    private const string OutcomeExtensionName = "outcome";
    private const string ReasonCodesExtensionName = "reasonCodes";
    private const string ReasonMessagesExtensionName = "reasonMessages";
    private const string CorrelationIdExtensionName = "correlationId";
    private const string TraceIdExtensionName = "traceId";
    private const string PolicyVersionExtensionName = "policyVersion";
    private const string PolicyHashExtensionName = "policyHash";

    /// <summary>
    /// Maps a governance decision into an ASP.NET Core result using default safe HTTP mapping options.
    /// </summary>
    /// <param name="decision">The governance decision to map.</param>
    /// <returns>An ASP.NET Core result.</returns>
    public static IResult ToHttpResult(this GovernanceDecision decision)
    {
        return decision.ToHttpResult(new GovernanceHttpResultMappingOptions());
    }

    /// <summary>
    /// Maps a governance decision into an ASP.NET Core result using host-provided HTTP mapping options.
    /// </summary>
    /// <param name="decision">The governance decision to map.</param>
    /// <param name="options">The mapping options.</param>
    /// <returns>An ASP.NET Core result.</returns>
    public static IResult ToHttpResult(this GovernanceDecision decision, GovernanceHttpResultMappingOptions options)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return decision.CanProceed
            ? Microsoft.AspNetCore.Http.Results.Json(CreateDecisionBody(decision, options, allowed: true), statusCode: ResolveDecisionStatusCode(decision, options))
            : Microsoft.AspNetCore.Http.Results.Problem(CreateDecisionProblemDetails(decision, options));
    }

    /// <summary>
    /// Maps an operation result into an ASP.NET Core result using default safe HTTP mapping options.
    /// </summary>
    /// <param name="result">The operation result to map.</param>
    /// <returns>An ASP.NET Core result.</returns>
    public static IResult ToHttpResult(this OperationResult result)
    {
        return result.ToHttpResult(new GovernanceHttpResultMappingOptions());
    }

    /// <summary>
    /// Maps an operation result into an ASP.NET Core result using host-provided HTTP mapping options.
    /// </summary>
    /// <param name="result">The operation result to map.</param>
    /// <param name="options">The mapping options.</param>
    /// <returns>An ASP.NET Core result.</returns>
    public static IResult ToHttpResult(this OperationResult result, GovernanceHttpResultMappingOptions options)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return result.Succeeded
            ? Microsoft.AspNetCore.Http.Results.Json(CreateOperationBody(result, options), statusCode: options.SuccessStatusCode)
            : Microsoft.AspNetCore.Http.Results.Problem(CreateOperationProblemDetails(result, options));
    }

    private static int ResolveDecisionStatusCode(GovernanceDecision decision, GovernanceHttpResultMappingOptions options)
    {
        return decision.Outcome switch
        {
            GovernanceDecisionOutcome.Allowed => options.SuccessStatusCode,
            GovernanceDecisionOutcome.Warning => options.WarningStatusCode,
            GovernanceDecisionOutcome.Denied => options.DeniedStatusCode,
            GovernanceDecisionOutcome.Deferred => options.DeferredStatusCode,
            GovernanceDecisionOutcome.AcknowledgmentRequired => options.AcknowledgmentRequiredStatusCode,
            GovernanceDecisionOutcome.EscalationRecommended => options.EscalationRecommendedStatusCode,
            _ => options.OperationFailureStatusCode,
        };
    }

    private static ProblemDetails CreateDecisionProblemDetails(
        GovernanceDecision decision,
        GovernanceHttpResultMappingOptions options)
    {
        int statusCode = ResolveDecisionStatusCode(decision, options);
        ProblemDetails problemDetails = new()
        {
            Status = statusCode,
            Title = ResolveDecisionTitle(decision.Outcome),
            Detail = options.GovernanceDecisionNotAllowedMessage.Trim(),
            Type = "https://tools.ietf.org/html/rfc9110",
        };

        AddDecisionExtensions(problemDetails.Extensions, decision, options);

        return problemDetails;
    }

    private static ProblemDetails CreateOperationProblemDetails(
        OperationResult result,
        GovernanceHttpResultMappingOptions options)
    {
        ProblemDetails problemDetails = new()
        {
            Status = options.OperationFailureStatusCode,
            Title = "Operation failed.",
            Detail = options.OperationFailureMessage.Trim(),
            Type = "https://tools.ietf.org/html/rfc9110",
        };

        AddOperationExtensions(problemDetails.Extensions, result, options);

        return problemDetails;
    }

    private static Dictionary<string, object?> CreateDecisionBody(
        GovernanceDecision decision,
        GovernanceHttpResultMappingOptions options,
        bool allowed)
    {
        Dictionary<string, object?> body = new(StringComparer.Ordinal)
        {
            ["allowed"] = allowed,
            [OutcomeExtensionName] = decision.Outcome.ToString(),
        };

        AddDecisionPayload(body, decision, options);

        return body;
    }

    private static Dictionary<string, object?> CreateOperationBody(OperationResult result, GovernanceHttpResultMappingOptions options)
    {
        Dictionary<string, object?> body = new(StringComparer.Ordinal)
        {
            ["succeeded"] = result.Succeeded,
        };

        AddOperationPayload(body, result, options);

        return body;
    }

    private static string ResolveDecisionTitle(GovernanceDecisionOutcome outcome)
    {
        return outcome switch
        {
            GovernanceDecisionOutcome.Allowed => "Governance decision allowed execution.",
            GovernanceDecisionOutcome.Warning => "Governance decision allowed execution with warnings.",
            GovernanceDecisionOutcome.Denied => "Governance decision denied execution.",
            GovernanceDecisionOutcome.Deferred => "Governance decision deferred execution.",
            GovernanceDecisionOutcome.AcknowledgmentRequired => "Governance decision requires acknowledgment.",
            GovernanceDecisionOutcome.EscalationRecommended => "Governance decision recommends escalation.",
            _ => "Governance decision did not allow immediate execution.",
        };
    }

    private static void AddDecisionExtensions(
        IDictionary<string, object?> extensions,
        GovernanceDecision decision,
        GovernanceHttpResultMappingOptions options)
    {
        extensions[OutcomeExtensionName] = decision.Outcome.ToString();
        AddDecisionPayload(extensions, decision, options);
    }

    private static void AddOperationExtensions(
        IDictionary<string, object?> extensions,
        OperationResult result,
        GovernanceHttpResultMappingOptions options)
    {
        AddOperationPayload(extensions, result, options);
    }

    private static void AddDecisionPayload(
        IDictionary<string, object?> payload,
        GovernanceDecision decision,
        GovernanceHttpResultMappingOptions options)
    {
        if (decision.ReasonCodes.Count > 0)
        {
            payload[ReasonCodesExtensionName] = decision.ReasonCodes;
        }

        if (options.IncludeReasonMessages && decision.Reasons.Count > 0)
        {
            payload[ReasonMessagesExtensionName] = decision.Reasons.Select(reason => reason.Message).ToArray();
        }

        if (!string.IsNullOrWhiteSpace(decision.CorrelationId))
        {
            payload[CorrelationIdExtensionName] = decision.CorrelationId;
        }

        if (options.IncludeTraceId && !string.IsNullOrWhiteSpace(decision.TraceId))
        {
            payload[TraceIdExtensionName] = decision.TraceId;
        }

        if (options.IncludePolicyMetadata)
        {
            if (!string.IsNullOrWhiteSpace(decision.PolicyVersion))
            {
                payload[PolicyVersionExtensionName] = decision.PolicyVersion;
            }

            if (!string.IsNullOrWhiteSpace(decision.PolicyHash))
            {
                payload[PolicyHashExtensionName] = decision.PolicyHash;
            }
        }
    }

    private static void AddOperationPayload(
        IDictionary<string, object?> payload,
        OperationResult result,
        GovernanceHttpResultMappingOptions options)
    {
        if (result.ReasonCodes.Count > 0)
        {
            payload[ReasonCodesExtensionName] = result.ReasonCodes;
        }

        if (result.HasWarnings)
        {
            payload["warnings"] = result.Warnings;
        }

        if (options.IncludeReasonMessages && result.Reasons.Count > 0)
        {
            payload[ReasonMessagesExtensionName] = result.Reasons.Select(reason => reason.Message).ToArray();
        }
    }
}
