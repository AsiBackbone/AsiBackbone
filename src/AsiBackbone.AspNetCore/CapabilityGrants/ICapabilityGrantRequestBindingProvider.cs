using AsiBackbone.AspNetCore.Endpoints;
using Microsoft.AspNetCore.Http;

namespace AsiBackbone.AspNetCore.CapabilityGrants;

/// <summary>
/// Defines what a capability grant's <see cref="Core.CapabilityGrants.CapabilityGrant.ResourceBinding" /> covers for the
/// current request.
/// </summary>
/// <remarks>
/// <para>
/// The provider turns the parts of a request that a grant authorizes, such as form fields, a JSON body, or route values,
/// into a binding value. <see cref="SignedCapabilityGrantEndpointValidator" /> requires the grant's resource binding to
/// equal that value exactly, so the issuer must compute the binding with the same rules when it issues the grant.
/// </para>
/// <para>
/// A provider that cannot bind the request returns <see cref="CapabilityGrantRequestBinding.Unavailable" />, which denies
/// the request. Returning <see cref="CapabilityGrantRequestBinding.None" /> states that the request has no bound
/// resource; the validator then accepts only grants that carry no resource binding.
/// </para>
/// </remarks>
public interface ICapabilityGrantRequestBindingProvider
{
    /// <summary>
    /// Resolves the resource binding expected for the current request.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="descriptor">The endpoint governance descriptor.</param>
    /// <param name="cancellationToken">A token that can cancel binding resolution.</param>
    /// <returns>The resolved binding.</returns>
    ValueTask<CapabilityGrantRequestBinding> ResolveAsync(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents the resource binding a request requires of a capability grant.
/// </summary>
public sealed class CapabilityGrantRequestBinding
{
    private CapabilityGrantRequestBinding(bool isAvailable, string? value, string? failureMessage)
    {
        IsAvailable = isAvailable;
        Value = value;
        FailureMessage = failureMessage;
    }

    /// <summary>
    /// Gets a binding stating that the request has no bound resource.
    /// </summary>
    public static CapabilityGrantRequestBinding None { get; } = new(isAvailable: true, value: null, failureMessage: null);

    /// <summary>
    /// Gets a value indicating whether the request could be bound.
    /// </summary>
    public bool IsAvailable { get; }

    /// <summary>
    /// Gets the binding value the grant must carry, or <see langword="null" /> when the request has no bound resource.
    /// </summary>
    public string? Value { get; }

    /// <summary>
    /// Gets a message describing why the request could not be bound, when it could not.
    /// </summary>
    public string? FailureMessage { get; }

    /// <summary>
    /// Creates a binding the grant's resource binding must equal.
    /// </summary>
    /// <param name="value">The binding value.</param>
    /// <returns>The binding.</returns>
    /// <exception cref="ArgumentException"><paramref name="value" /> is null, empty, or whitespace.</exception>
    public static CapabilityGrantRequestBinding Bound(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return new CapabilityGrantRequestBinding(isAvailable: true, value.Trim(), failureMessage: null);
    }

    /// <summary>
    /// Creates a result stating that the request cannot be bound, which denies it.
    /// </summary>
    /// <param name="failureMessage">A message describing why the request cannot be bound. It must not contain request content.</param>
    /// <returns>The unavailable binding.</returns>
    /// <exception cref="ArgumentException"><paramref name="failureMessage" /> is null, empty, or whitespace.</exception>
    public static CapabilityGrantRequestBinding Unavailable(string failureMessage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureMessage);
        return new CapabilityGrantRequestBinding(isAvailable: false, value: null, failureMessage.Trim());
    }
}
