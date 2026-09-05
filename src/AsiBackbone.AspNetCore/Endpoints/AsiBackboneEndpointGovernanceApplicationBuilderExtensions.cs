using Microsoft.AspNetCore.Builder;

namespace AsiBackbone.AspNetCore.Endpoints;

/// <summary>
/// Provides ASP.NET Core application builder extensions for AsiBackbone endpoint governance.
/// </summary>
public static class AsiBackboneEndpointGovernanceApplicationBuilderExtensions
{
    private const string EndpointRouteBuilderKey = "__EndpointRouteBuilder";

    /// <summary>
    /// Adds AsiBackbone endpoint governance middleware to the ASP.NET Core pipeline.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This middleware governs the <em>selected</em> endpoint, so it must run after endpoint routing has selected one.
    /// Placed before routing, every request reaches it with no endpoint, no governance metadata is found, and the
    /// request is forwarded ungoverned unless <see cref="AsiBackboneEndpointGovernanceOptions.RequireGovernanceMetadata" />
    /// is enabled. Register it after <c>UseRouting</c> and after authentication, so an actor context is available.
    /// </para>
    /// <para>
    /// A host that calls <c>UseRouting</c> explicitly can pass <paramref name="requireEndpointRoutingRegistered" /> to
    /// turn that ordering requirement into a startup failure. It is off by default because it cannot be checked
    /// reliably: a Minimal API host that never calls <c>UseRouting</c> still routes correctly, because
    /// <c>WebApplication</c> inserts routing at the front of the pipeline when endpoints are mapped, and in that case
    /// the routing marker is legitimately absent when this method runs.
    /// </para>
    /// </remarks>
    /// <param name="app">The application builder.</param>
    /// <param name="requireEndpointRoutingRegistered">
    /// When <see langword="true" />, throws if endpoint routing has not been registered before this call. Only use this
    /// in hosts that call <c>UseRouting</c> explicitly.
    /// </param>
    /// <returns>The same application builder so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="app" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <paramref name="requireEndpointRoutingRegistered" /> is <see langword="true" /> and endpoint routing
    /// has not been registered before this call.
    /// </exception>
    public static IApplicationBuilder UseAsiBackboneEndpointGovernance(
        this IApplicationBuilder app,
        bool requireEndpointRoutingRegistered = false)
    {
        ArgumentNullException.ThrowIfNull(app);

        return requireEndpointRoutingRegistered && !app.Properties.ContainsKey(EndpointRouteBuilderKey)
            ? throw new InvalidOperationException(
                "AsiBackbone endpoint governance was registered before endpoint routing. Endpoint governance reads the selected endpoint's governance metadata, so it must run after UseRouting; placed earlier, every request arrives with no endpoint and is forwarded ungoverned unless RequireGovernanceMetadata is enabled. Call UseRouting before UseAsiBackboneEndpointGovernance, or pass requireEndpointRoutingRegistered: false if this host relies on WebApplication inserting routing automatically.")
            : app.UseMiddleware<AsiBackboneEndpointGovernanceMiddleware>();
    }
}
