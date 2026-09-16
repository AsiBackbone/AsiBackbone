using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Decisions;
using Microsoft.AspNetCore.Http;

namespace AsiBackbone.Testing.Contracts;

/// <summary>
/// Reusable contract fixture for <see cref="IEndpointCapabilityGrantValidator" /> implementations.
/// </summary>
public abstract class EndpointCapabilityGrantValidatorContract
{
    /// <summary>
    /// Creates the capability-grant validator implementation under test.
    /// </summary>
    /// <returns>The capability-grant validator implementation to validate.</returns>
    protected abstract IEndpointCapabilityGrantValidator CreateValidator();

    /// <summary>
    /// Creates the HTTP context supplied to the validator implementation under test.
    /// </summary>
    /// <returns>The HTTP context to validate with.</returns>
    protected virtual HttpContext CreateHttpContext()
    {
        return new DefaultHttpContext
        {
            TraceIdentifier = "asibackbone-contract-capability"
        };
    }

    /// <summary>
    /// Creates a descriptor containing a capability requirement for the invalid-grant contract path.
    /// </summary>
    /// <returns>The descriptor supplied to the validator implementation under test.</returns>
    protected virtual EndpointGovernanceDescriptor CreateCapabilityDescriptor()
    {
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequireCapabilityGrantAttribute("asibackbone.contract.invalid")),
            "asibackbone.contract.capability");

        return EndpointGovernanceDescriptor.FromEndpoint(endpoint);
    }

    /// <summary>
    /// Creates the current decision supplied to the validator implementation under test.
    /// </summary>
    /// <returns>The current governance decision.</returns>
    protected virtual GovernanceDecision CreateCurrentDecision()
    {
        return GovernanceDecision.Allow(
            correlationId: "asibackbone-contract-capability",
            policyVersion: "contract-policy-v1",
            policyHash: "contract-policy-hash");
    }

    /// <summary>
    /// Allows derived contracts to place a known-invalid grant, token, header, or request state into the context.
    /// </summary>
    /// <param name="httpContext">The HTTP context supplied to the validator.</param>
    /// <param name="descriptor">The endpoint governance descriptor supplied to the validator.</param>
    protected virtual void ConfigureKnownInvalidCapabilityGrant(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor)
    {
    }

    /// <summary>
    /// Verifies that a known invalid capability-grant scenario does not produce an allow decision.
    /// </summary>
    /// <param name="cancellationToken">A token that can cancel the contract validation.</param>
    /// <returns>The verified governance decision.</returns>
    public async ValueTask<GovernanceDecision> VerifyKnownInvalidCapabilityGrantDoesNotAllowAsync(CancellationToken cancellationToken = default)
    {
        IEndpointCapabilityGrantValidator validator = CreateValidator()
            ?? throw new GovernanceContractViolationException("Capability-grant contract must provide a validator instance.");
        HttpContext httpContext = CreateHttpContext()
            ?? throw new GovernanceContractViolationException("Capability-grant contract must provide an HTTP context.");
        EndpointGovernanceDescriptor descriptor = CreateCapabilityDescriptor()
            ?? throw new GovernanceContractViolationException("Capability-grant contract must provide an endpoint descriptor.");
        GovernanceDecision currentDecision = CreateCurrentDecision()
            ?? throw new GovernanceContractViolationException("Capability-grant contract must provide a current decision.");

        ConfigureKnownInvalidCapabilityGrant(httpContext, descriptor);

        try
        {
            GovernanceDecision decision = await validator.ValidateAsync(httpContext, descriptor, currentDecision, cancellationToken).ConfigureAwait(false);
            return GovernanceDecisionContract.VerifyInvalidCapabilityGrantDoesNotAllow(decision, "Capability-grant validator decision");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (GovernanceContractViolationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new GovernanceContractViolationException(
                "Capability-grant validators must fail closed, defer, or escalate for invalid grants instead of throwing during normal contract validation.",
                exception);
        }
    }
}
