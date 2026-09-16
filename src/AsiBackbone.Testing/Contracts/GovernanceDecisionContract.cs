using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Results;

namespace AsiBackbone.Testing.Contracts;

/// <summary>
/// Provides reusable safe-collapse assertions for governance decisions and decision receipt.
/// </summary>
public static class GovernanceDecisionContract
{
    /// <summary>
    /// Verifies that a governance decision is present and carries the minimum shape required for safe downstream handling.
    /// </summary>
    /// <param name="decision">The decision returned by an implementation under test.</param>
    /// <param name="contractName">The human-readable contract name used in failure messages.</param>
    /// <returns>The verified decision.</returns>
    public static GovernanceDecision VerifySafeDecision(
        GovernanceDecision? decision,
        string contractName = "Governance decision")
    {
        if (decision is null)
        {
            throw new GovernanceContractViolationException($"{contractName} must return a decision and must never return null.");
        }

        VerifySupportedOutcome(decision, contractName);

        if (RequiresReasonCodes(decision) && decision.Reasons.Count == 0)
        {
            throw new GovernanceContractViolationException($"{contractName} outcome '{decision.Outcome}' must include at least one reason code.");
        }

        for (int index = 0; index < decision.Reasons.Count; index++)
        {
            OperationReason reason = decision.Reasons[index] ?? throw new GovernanceContractViolationException($"{contractName} contains a null reason at index {index}.");

            if (string.IsNullOrWhiteSpace(reason.Code))
            {
                throw new GovernanceContractViolationException($"{contractName} contains a reason with an empty code at index {index}.");
            }

            if (string.IsNullOrWhiteSpace(reason.Message))
            {
                throw new GovernanceContractViolationException($"{contractName} contains a reason with an empty message at index {index}.");
            }
        }

        return decision;
    }

    /// <summary>
    /// Verifies decision shape, context correlation propagation, and policy telemetry presence when supplied.
    /// </summary>
    /// <typeparam name="TContext">The framework-neutral evaluation context type.</typeparam>
    /// <param name="decision">The decision returned by an implementation under test.</param>
    /// <param name="context">The context supplied to the implementation under test.</param>
    /// <param name="contractName">The human-readable contract name used in failure messages.</param>
    /// <returns>The verified decision.</returns>
    public static GovernanceDecision VerifyTelemetryFromContext<TContext>(
        GovernanceDecision? decision,
        TContext context,
        string contractName = "Governance decision")
        where TContext : IGovernanceEvaluationContext
    {
        ArgumentNullException.ThrowIfNull(context);

        GovernanceDecision verifiedDecision = VerifySafeDecision(decision, contractName);
        VerifyCorrelationTelemetryValue(context.CorrelationId, verifiedDecision.CorrelationId, contractName);
        VerifyTelemetryPresence(context.PolicyVersion, verifiedDecision.PolicyVersion, "policy version", contractName);
        VerifyTelemetryPresence(context.PolicyHash, verifiedDecision.PolicyHash, "policy hash", contractName);
        return verifiedDecision;
    }

    /// <summary>
    /// Verifies that a known invalid capability-grant scenario does not produce an allow decision.
    /// </summary>
    /// <param name="decision">The decision returned by the capability validator for a known invalid scenario.</param>
    /// <param name="contractName">The human-readable contract name used in failure messages.</param>
    /// <returns>The verified decision.</returns>
    public static GovernanceDecision VerifyInvalidCapabilityGrantDoesNotAllow(
        GovernanceDecision? decision,
        string contractName = "Invalid capability grant")
    {
        GovernanceDecision verifiedDecision = VerifySafeDecision(decision, contractName);

        return verifiedDecision.IsAllowed
            ? throw new GovernanceContractViolationException($"{contractName} must not return Allow for an invalid or unsupported capability grant.")
            : verifiedDecision;
    }

    /// <summary>
    /// Verifies that decision receipt contains the minimum identity, operation, outcome, and policy telemetry shape.
    /// </summary>
    /// <param name="residue">The decision receipt to verify.</param>
    /// <param name="contractName">The human-readable contract name used in failure messages.</param>
    /// <returns>The verified decision receipt.</returns>
    public static IDecisionReceipt VerifyAuditResidue(
        IDecisionReceipt? residue,
        string contractName = "Decision receipt")
    {
        if (residue is null)
        {
            throw new GovernanceContractViolationException($"{contractName} must not be null.");
        }

        VerifyRequiredString(residue.EventId, "event ID", contractName);
        VerifyRequiredString(residue.ActorId, "actor ID", contractName);
        VerifyRequiredString(residue.OperationName, "operation name", contractName);
        VerifyRequiredString(residue.Outcome, "outcome", contractName);

        if (residue.ReasonCodes is null)
        {
            throw new GovernanceContractViolationException($"{contractName} reason-code collection must not be null.");
        }

        for (int index = 0; index < residue.ReasonCodes.Count; index++)
        {
            if (string.IsNullOrWhiteSpace(residue.ReasonCodes[index]))
            {
                throw new GovernanceContractViolationException($"{contractName} contains an empty reason code at index {index}.");
            }
        }

        return residue.Metadata is null
            ? throw new GovernanceContractViolationException($"{contractName} metadata collection must not be null.")
            : residue;
    }

    private static void VerifySupportedOutcome(GovernanceDecision decision, string contractName)
    {
        if (decision.Outcome is not GovernanceDecisionOutcome.Allowed
            and not GovernanceDecisionOutcome.Warning
            and not GovernanceDecisionOutcome.Denied
            and not GovernanceDecisionOutcome.Deferred
            and not GovernanceDecisionOutcome.AcknowledgmentRequired
            and not GovernanceDecisionOutcome.EscalationRecommended)
        {
            throw new GovernanceContractViolationException($"{contractName} returned unsupported outcome '{decision.Outcome}'.");
        }
    }

    private static bool RequiresReasonCodes(GovernanceDecision decision)
    {
        return decision.Outcome is GovernanceDecisionOutcome.Warning
            or GovernanceDecisionOutcome.Denied
            or GovernanceDecisionOutcome.Deferred
            or GovernanceDecisionOutcome.AcknowledgmentRequired
            or GovernanceDecisionOutcome.EscalationRecommended;
    }

    private static void VerifyCorrelationTelemetryValue(
        string? expected,
        string? actual,
        string contractName)
    {
        if (!string.IsNullOrWhiteSpace(expected) && !string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw new GovernanceContractViolationException($"{contractName} must preserve the supplied correlation ID when present.");
        }
    }

    private static void VerifyTelemetryPresence(
        string? expected,
        string? actual,
        string telemetryName,
        string contractName)
    {
        if (!string.IsNullOrWhiteSpace(expected) && string.IsNullOrWhiteSpace(actual))
        {
            throw new GovernanceContractViolationException($"{contractName} must include a {telemetryName} when one is supplied by the context or resolved by the implementation.");
        }
    }

    private static void VerifyRequiredString(string? value, string name, string contractName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new GovernanceContractViolationException($"{contractName} must include a non-empty {name}.");
        }
    }
}
