using System.Collections.ObjectModel;

namespace AsiBackbone.Core.Signing;

/// <summary>
/// Maps signature verification categories to host-facing verification policy actions.
/// </summary>
public sealed class VerificationPolicyOptions
{
    private static readonly IReadOnlyDictionary<SignatureVerificationCategory, VerificationPolicyAction> DefaultActionMap =
        new ReadOnlyDictionary<SignatureVerificationCategory, VerificationPolicyAction>(
            new Dictionary<SignatureVerificationCategory, VerificationPolicyAction>
            {
                [SignatureVerificationCategory.Valid] = VerificationPolicyAction.Allow,
                [SignatureVerificationCategory.InvalidSignature] = VerificationPolicyAction.Deny,
                [SignatureVerificationCategory.HashMismatch] = VerificationPolicyAction.Deny,
                [SignatureVerificationCategory.MissingSignature] = VerificationPolicyAction.RequireAcknowledgment,
                [SignatureVerificationCategory.UnknownKeyVersion] = VerificationPolicyAction.Escalate,
                [SignatureVerificationCategory.RevokedKey] = VerificationPolicyAction.Deny,
                [SignatureVerificationCategory.ProviderUnavailable] = VerificationPolicyAction.Defer,
                [SignatureVerificationCategory.CanonicalizationMismatch] = VerificationPolicyAction.Escalate,
                [SignatureVerificationCategory.UnsupportedAlgorithm] = VerificationPolicyAction.Deny,
                [SignatureVerificationCategory.UntrustedKey] = VerificationPolicyAction.Deny,
                [SignatureVerificationCategory.Failed] = VerificationPolicyAction.Escalate
            });

    private VerificationPolicyOptions(IReadOnlyDictionary<SignatureVerificationCategory, VerificationPolicyAction> actions)
    {
        Actions = actions;
    }

    /// <summary>
    /// Gets the default verification policy action map.
    /// </summary>
    public static VerificationPolicyOptions Default { get; } = new(DefaultActionMap);

    /// <summary>
    /// Gets the configured verification category to host action map.
    /// </summary>
    public IReadOnlyDictionary<SignatureVerificationCategory, VerificationPolicyAction> Actions { get; }

    /// <summary>
    /// Creates verification policy options with optional host overrides.
    /// </summary>
    /// <param name="actionOverrides">Category to action overrides applied over the defaults.</param>
    /// <param name="allowUnsafeAllowOverrides">
    /// When <see langword="true" />, permits mapping a failure category to <see cref="VerificationPolicyAction.Allow" />.
    /// Such a mapping makes a failed verification indistinguishable from a successful one, so it must be opted into
    /// deliberately rather than reached by configuration drift.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException">A category or action is undefined, or a failure category was mapped to <see cref="VerificationPolicyAction.Allow" /> without the opt-in.</exception>
    public static VerificationPolicyOptions Create(
        IReadOnlyDictionary<SignatureVerificationCategory, VerificationPolicyAction>? actionOverrides = null,
        bool allowUnsafeAllowOverrides = false)
    {
        Dictionary<SignatureVerificationCategory, VerificationPolicyAction> actions = new(DefaultActionMap);

        if (actionOverrides is not null)
        {
            foreach (KeyValuePair<SignatureVerificationCategory, VerificationPolicyAction> item in actionOverrides)
            {
                if (!Enum.IsDefined(item.Key))
                {
                    throw new ArgumentOutOfRangeException(nameof(actionOverrides), item.Key, "Verification category must be defined.");
                }

                if (!Enum.IsDefined(item.Value))
                {
                    throw new ArgumentOutOfRangeException(nameof(actionOverrides), item.Value, "Verification policy action must be defined.");
                }

                if (!allowUnsafeAllowOverrides
                    && item.Value is VerificationPolicyAction.Allow
                    && item.Key is not SignatureVerificationCategory.Valid)
                {
                    throw new ArgumentOutOfRangeException(
                        nameof(actionOverrides),
                        item.Key,
                        "Mapping a failure category to Allow requires allowUnsafeAllowOverrides, because it makes a failed verification indistinguishable from a valid one.");
                }

                actions[item.Key] = item.Value;
            }
        }

        return new VerificationPolicyOptions(new ReadOnlyDictionary<SignatureVerificationCategory, VerificationPolicyAction>(actions));
    }

    /// <summary>
    /// Gets the action configured for the supplied verification category.
    /// </summary>
    public VerificationPolicyAction GetAction(SignatureVerificationCategory category)
    {
        return !Enum.IsDefined(category)
            ? throw new ArgumentOutOfRangeException(nameof(category), category, "Verification category must be defined.")
            : Actions.TryGetValue(category, out VerificationPolicyAction action)
            ? action
            : VerificationPolicyAction.Escalate;
    }
}
