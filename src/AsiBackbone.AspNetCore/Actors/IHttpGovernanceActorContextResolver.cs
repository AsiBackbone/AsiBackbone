using AsiBackbone.Core.Actors;

namespace AsiBackbone.AspNetCore.Actors;

/// <summary>
/// Resolves the current ASP.NET Core request actor into the framework-neutral AsiBackbone actor context model.
/// </summary>
public interface IHttpGovernanceActorContextResolver
{
    /// <summary>
    /// Resolves the current HTTP or host actor context.
    /// </summary>
    /// <returns>A framework-neutral actor context.</returns>
    IGovernanceActorContext ResolveActorContext();
}
