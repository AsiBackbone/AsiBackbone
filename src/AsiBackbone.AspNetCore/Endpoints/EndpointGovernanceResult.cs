using AsiBackbone.AspNetCore.Acknowledgments;
using AsiBackbone.Core.Decisions;
using Microsoft.AspNetCore.Http;

namespace AsiBackbone.AspNetCore.Endpoints;

/// <summary>
/// Represents the outcome of ASP.NET Core endpoint governance evaluation before endpoint execution.
/// </summary>
public sealed class EndpointGovernanceResult
{
    private EndpointGovernanceResult(
        bool canExecute,
        GovernanceDecision? decision,
        IResult? failureResult,
        AcknowledgmentChallenge? acknowledgmentChallenge)
    {
        CanExecute = canExecute;
        Decision = decision;
        FailureResult = failureResult;
        AcknowledgmentChallenge = acknowledgmentChallenge;
    }

    /// <summary>
    /// Gets a value indicating whether the ASP.NET Core endpoint may continue execution.
    /// </summary>
    public bool CanExecute { get; }

    /// <summary>
    /// Gets the governance decision associated with the endpoint evaluation, when one was produced.
    /// </summary>
    public GovernanceDecision? Decision { get; }

    /// <summary>
    /// Gets the HTTP result that should be returned instead of executing the endpoint, when execution is blocked.
    /// </summary>
    public IResult? FailureResult { get; }

    /// <summary>
    /// Gets the acknowledgment challenge produced when a decision required acknowledgment.
    /// </summary>
    public AcknowledgmentChallenge? AcknowledgmentChallenge { get; }

    /// <summary>
    /// Creates a result that allows endpoint execution to continue.
    /// </summary>
    /// <param name="decision">The governance decision that allowed execution.</param>
    /// <returns>An allow result.</returns>
    public static EndpointGovernanceResult Allow(GovernanceDecision? decision = null)
    {
        return new EndpointGovernanceResult(
            canExecute: true,
            decision,
            failureResult: null,
            acknowledgmentChallenge: null);
    }

    /// <summary>
    /// Creates a result that blocks endpoint execution and lets middleware write the configured generic failure response.
    /// </summary>
    /// <param name="decision">The governance decision that blocked execution.</param>
    /// <returns>A blocked result without a custom failure response.</returns>
    public static EndpointGovernanceResult BlockWithDefaultFailure(GovernanceDecision? decision = null)
    {
        return new EndpointGovernanceResult(
            canExecute: false,
            decision,
            failureResult: null,
            acknowledgmentChallenge: null);
    }

    /// <summary>
    /// Creates a result that blocks endpoint execution and returns a host-safe failure result.
    /// </summary>
    /// <param name="failureResult">The HTTP result to execute.</param>
    /// <param name="decision">The governance decision that blocked execution.</param>
    /// <returns>A blocked result.</returns>
    public static EndpointGovernanceResult Block(IResult failureResult, GovernanceDecision? decision = null)
    {
        ArgumentNullException.ThrowIfNull(failureResult);

        return new EndpointGovernanceResult(
            canExecute: false,
            decision,
            failureResult,
            acknowledgmentChallenge: null);
    }

    /// <summary>
    /// Creates a result that blocks execution with an acknowledgment challenge.
    /// </summary>
    /// <param name="challenge">The acknowledgment challenge.</param>
    /// <param name="failureResult">The HTTP result to execute.</param>
    /// <param name="decision">The governance decision that required acknowledgment.</param>
    /// <returns>An acknowledgment challenge result.</returns>
    public static EndpointGovernanceResult Challenge(
        AcknowledgmentChallenge challenge,
        IResult failureResult,
        GovernanceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(challenge);
        ArgumentNullException.ThrowIfNull(failureResult);
        ArgumentNullException.ThrowIfNull(decision);

        return new EndpointGovernanceResult(
            canExecute: false,
            decision,
            failureResult,
            challenge);
    }
}
