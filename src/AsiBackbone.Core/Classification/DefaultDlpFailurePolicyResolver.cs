namespace AsiBackbone.Core.Classification;

/// <summary>
/// Default provider-neutral resolver for DLP or classification failure behavior.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="DefaultDlpFailurePolicyResolver" /> class.
/// </remarks>
/// <param name="options">Optional policy options. When omitted, conservative defaults are used.</param>
public sealed class DefaultDlpFailurePolicyResolver(DlpFailurePolicyOptions? options = null) : IDlpFailurePolicyResolver
{
    private readonly DlpFailurePolicyOptions options = options ?? new DlpFailurePolicyOptions();

    /// <inheritdoc />
    public ValueTask<DlpFailurePolicyResolution> ResolveAsync(
        DlpFailurePolicyContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        DlpFailureBehavior behavior = options.GetBehavior(context);

        return ValueTask.FromResult(
            DlpFailurePolicyResolution.Create(context, behavior));
    }
}
