using System.Security.Claims;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Decisions;

namespace Company.AsibackboneTemplate.Governance;

/// <summary>
/// Sample endpoint capability validator. Production hosts should replace this with host-owned scope, expiry, replay, actor, and downstream authorization checks.
/// </summary>
public sealed class SampleCapabilityGrantValidator : IAsiBackboneEndpointCapabilityGrantValidator
{
    public ValueTask<GovernanceDecision> ValidateAsync(
        HttpContext httpContext,
        AsiBackboneEndpointGovernanceDescriptor descriptor,
        GovernanceDecision currentDecision,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(currentDecision);

        // The scopes on the descriptor are the endpoint's own declaration, so comparing them against themselves always
        // succeeded and admitted every caller. A capability check has to read what the caller presented and compare that
        // against what the endpoint requires.
        if (httpContext.User.Identity?.IsAuthenticated is not true)
        {
            return ValueTask.FromResult(Deny(
                currentDecision,
                "template.capability.unauthenticated",
                "The caller is not authenticated, so no capability scopes were presented."));
        }

        HashSet<string> presentedScopes = ReadPresentedScopes(httpContext);

        foreach (string requiredScope in descriptor.CapabilityScopes)
        {
            if (!presentedScopes.Contains(requiredScope))
            {
                return ValueTask.FromResult(Deny(
                    currentDecision,
                    "template.capability.missing",
                    "The caller did not present every capability scope this endpoint requires."));
            }
        }

        return ValueTask.FromResult(currentDecision);
    }

    /// <summary>
    /// Reads the scopes the caller presented from the standard scope claims.
    /// </summary>
    /// <remarks>
    /// This template registers no authentication scheme, so this validator denies until a host adds one. That is
    /// deliberate: the scaffold refuses callers until its owner decides what identity means for it.
    /// </remarks>
    private static HashSet<string> ReadPresentedScopes(HttpContext httpContext)
    {
        HashSet<string> scopes = new(StringComparer.Ordinal);

        foreach (Claim claim in httpContext.User.Claims)
        {
            if (!string.Equals(claim.Type, "scope", StringComparison.Ordinal)
                && !string.Equals(claim.Type, "scp", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string scope in claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                _ = scopes.Add(scope);
            }
        }

        return scopes;
    }

    private static GovernanceDecision Deny(GovernanceDecision currentDecision, string reasonCode, string reasonMessage)
    {
        return GovernanceDecision.Deny(
            reasonCode,
            reasonMessage,
            correlationId: currentDecision.CorrelationId,
            traceId: currentDecision.TraceId,
            policyVersion: currentDecision.PolicyVersion,
            policyHash: currentDecision.PolicyHash);
    }
}
