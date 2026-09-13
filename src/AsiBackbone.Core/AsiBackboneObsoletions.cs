namespace AsiBackbone.Core;

/// <summary>
/// Diagnostic identifiers and messages for AsiBackbone APIs scheduled for removal.
/// </summary>
internal static class AsiBackboneObsoletions
{
    internal const string PolicyEvaluatorConstructorUrl = "https://asibackbone.github.io/AsiBackbone/articles/asib900-policy-evaluator-constructors.html";

    internal const string PolicyEvaluatorConstructorDiagnosticId = "ASIB900";

    internal const string PolicyEvaluatorConstructorMessage =
        "Use DefaultAsiBackbonePolicyEvaluator.CreateBuilder<TContext>() or the constructor that accepts all dependencies. This overload will be removed in AsiBackbone 6.0.";
}
