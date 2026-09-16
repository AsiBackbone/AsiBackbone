using System.Collections.ObjectModel;
using System.Reflection;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Xunit;

namespace AsiBackbone.Core.Tests.Evaluation;

/// <summary>
/// Targeted coverage for invalid threat contributor outcome handling.
/// </summary>
public sealed class DefaultAsiBackbonePolicyEvaluatorInvalidThreatOutcomeTests
{
    /// <summary>
    /// Tests that the CreateThreatEvaluationResult method rejects an Allowed outcome from a threat model contributor.
    /// </summary>
    [Fact]
    public void CreateThreatEvaluationResultRejectsAllowedOutcome()
    {
        MethodInfo method = GetPrivateStaticMethod(nameof(CreateThreatEvaluationResultRejectsAllowedOutcome), "CreateThreatEvaluationResult");
        var selectedReason = OperationReason.Create(
            "threat.allowed_invalid",
            "Allowed threat outcome is invalid.");
        ReadOnlyCollection<OperationReason> reasons = Array.AsReadOnly([selectedReason]);

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(null, [CreateContext(), GovernanceDecisionOutcome.Allowed, selectedReason, reasons]));

        InvalidOperationException innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("Threat model contributors cannot return an Allowed outcome", innerException.Message);
    }

    /// <summary>
    /// Tests that the GetThreatOutcomeRank method rejects an Allowed outcome from a threat model contributor.
    /// </summary>
    [Fact]
    public void GetThreatOutcomeRankRejectsAllowedOutcome()
    {
        MethodInfo method = GetPrivateStaticMethod(nameof(GetThreatOutcomeRankRejectsAllowedOutcome), "GetThreatOutcomeRank");

        TargetInvocationException exception = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(null, [GovernanceDecisionOutcome.Allowed]));

        InvalidOperationException innerException = Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.Contains("Threat model contributors cannot return an Allowed outcome", innerException.Message);
    }

    private static MethodInfo GetPrivateStaticMethod(string testName, string methodName)
    {
        return typeof(DefaultGovernancePolicyEvaluator<TestPolicyContext>).GetMethod(
            methodName,
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"{testName} could not find {methodName}.");
    }

    private static TestPolicyContext CreateContext()
    {
        return new TestPolicyContext
        {
            CorrelationId = "corr-invalid-threat-outcome",
            PolicyVersion = "v-invalid-threat-outcome",
            PolicyHash = "hash-invalid-threat-outcome"
        };
    }

    private sealed class TestPolicyContext : IGovernanceEvaluationContext
    {
        public string? CorrelationId { get; init; }

        public string? PolicyVersion { get; init; }

        public string? PolicyHash { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
