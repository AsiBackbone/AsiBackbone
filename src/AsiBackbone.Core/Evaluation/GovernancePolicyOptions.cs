namespace AsiBackbone.Core.Evaluation;

/// <summary>
/// Provides the preferred domain-qualified options surface for governance policy evaluation.
/// </summary>
/// <remarks>
/// This type derives from <see cref="AsiBackbonePolicyEvaluatorOptions" /> so it can be supplied anywhere the existing
/// 5.x options type is accepted while 6.0 documentation and new code can use domain language instead of a product prefix.
/// </remarks>
public sealed class GovernancePolicyOptions : AsiBackbonePolicyEvaluatorOptions
{
}
