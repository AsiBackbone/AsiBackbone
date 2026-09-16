using AsiBackbone.Core.Decisions;
using Microsoft.AspNetCore.Http;

namespace AsiBackbone.AspNetCore.Endpoints;

/// <summary>
/// Evaluates AsiBackbone governance metadata attached to an ASP.NET Core endpoint before endpoint execution.
/// </summary>
public interface IEndpointGovernanceService
{
    /// <summary>
    /// Evaluates the selected endpoint against host-owned AsiBackbone governance services.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="descriptor">The normalized endpoint governance descriptor.</param>
    /// <param name="cancellationToken">A token that can cancel endpoint governance evaluation.</param>
    /// <returns>The endpoint governance result.</returns>
    ValueTask<EndpointGovernanceResult> EvaluateAsync(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Optional host-owned validator for endpoint capability-grant metadata.
/// </summary>
/// <remarks>
/// The ASP.NET Core package does not infer grant storage, proof verification, or replay semantics. Hosts that attach
/// capability metadata should register an implementation that validates the request against their grant source.
/// </remarks>
public interface IEndpointCapabilityGrantValidator
{
    /// <summary>
    /// Validates endpoint capability requirements before endpoint execution continues.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="descriptor">The endpoint governance descriptor.</param>
    /// <param name="currentDecision">The current governance decision.</param>
    /// <param name="cancellationToken">A token that can cancel validation.</param>
    /// <returns>A governance decision representing the capability validation result.</returns>
    ValueTask<GovernanceDecision> ValidateAsync(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        GovernanceDecision currentDecision,
        CancellationToken cancellationToken = default);
}
