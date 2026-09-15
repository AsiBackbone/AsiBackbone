namespace AsiBackbone.Core.Actors;

/// <summary>
/// Defines the semantic governance contract for an actor participating in a governed operation.
/// </summary>
/// <remarks>
/// This is the preferred 6.0 domain-qualified contract. The product-prefixed
/// <see cref="IAsiBackboneActorContext" /> contract remains source-compatible for existing consumers.
/// </remarks>
public interface IGovernanceActorContext : IAsiBackboneActorContext
{
}
