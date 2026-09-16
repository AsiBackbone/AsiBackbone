namespace AsiBackbone.Core.Classification;

/// <summary>
/// Resolves provider-neutral DLP or classification failure behavior for a governed intent.
/// </summary>
public interface IDlpFailurePolicyResolver
{
    /// <summary>
    /// Resolves the policy behavior for the supplied DLP or classification failure context.
    /// </summary>
    /// <param name="context">The provider-neutral failure context.</param>
    /// <param name="cancellationToken">A token that can cancel asynchronous resolution.</param>
    /// <returns>The resolved failure policy result.</returns>
    ValueTask<DlpFailurePolicyResolution> ResolveAsync(
        DlpFailurePolicyContext context,
        CancellationToken cancellationToken = default);
}
