using AsiBackbone.Core.Metadata;
using Xunit;

namespace AsiBackbone.Core.Tests.Metadata;

/// <summary>
/// Unit tests for provider-neutral governance metadata result and context types.
/// </summary>
public sealed class GovernanceMetadataResultTests
{
    /// <summary>
    /// Verifies that an allow classification has no reason or replacement value.
    /// </summary>
    [Fact]
    public void ClassificationAllowHasNoReasonOrReplacement()
    {
        var result = GovernanceMetadataClassificationResult.Allow();

        Assert.Equal(GovernanceMetadataSanitizationAction.Allow, result.Action);
        Assert.Null(result.Reason);
        Assert.Null(result.ReplacementValue);
    }

    /// <summary>
    /// Verifies that non-allow classification factories preserve their action and normalized reason.
    /// </summary>
    /// <param name="action">The classification action exercised by the test.</param>
    [Theory]
    [InlineData(GovernanceMetadataSanitizationAction.Warn)]
    [InlineData(GovernanceMetadataSanitizationAction.Drop)]
    [InlineData(GovernanceMetadataSanitizationAction.Deny)]
    public void ClassificationFactoriesPreserveReason(GovernanceMetadataSanitizationAction action)
    {
        GovernanceMetadataClassificationResult result = action switch
        {
            GovernanceMetadataSanitizationAction.Warn => GovernanceMetadataClassificationResult.Warn(" metadata.warn ", " Warn message. "),
            GovernanceMetadataSanitizationAction.Drop => GovernanceMetadataClassificationResult.Drop(" metadata.drop ", " Drop message. "),
            GovernanceMetadataSanitizationAction.Deny => GovernanceMetadataClassificationResult.Deny(" metadata.deny ", " Deny message. "),
            GovernanceMetadataSanitizationAction.Allow => throw new NotImplementedException(),
            GovernanceMetadataSanitizationAction.Redact => throw new NotImplementedException(),
            _ => throw new InvalidOperationException("Unsupported test action.")
        };

        Assert.Equal(action, result.Action);
        Assert.NotNull(result.Reason);
        Assert.Equal($"metadata.{action.ToString().ToLowerInvariant()}", result.Reason.Code);
        Assert.Equal($"{action} message.", result.Reason.Message);
        Assert.Null(result.ReplacementValue);
    }

    /// <summary>
    /// Verifies that redaction uses the documented default replacement when none is supplied.
    /// </summary>
    [Fact]
    public void RedactUsesDefaultReplacementWhenOmitted()
    {
        var result = GovernanceMetadataClassificationResult.Redact(
            "metadata.redact",
            "Redact message.");

        Assert.Equal(GovernanceMetadataSanitizationAction.Redact, result.Action);
        Assert.Equal(GovernanceMetadataClassificationResult.DefaultRedactedValue, result.ReplacementValue);
        Assert.Equal("metadata.redact", result.Reason?.Code);
        Assert.Equal("Redact message.", result.Reason?.Message);
    }

    /// <summary>
    /// Verifies that redaction preserves a caller-supplied replacement value.
    /// </summary>
    [Fact]
    public void RedactPreservesCustomReplacement()
    {
        var result = GovernanceMetadataClassificationResult.Redact(
            "metadata.redact",
            "Redact message.",
            "safe-value");

        Assert.Equal("safe-value", result.ReplacementValue);
    }

    /// <summary>
    /// Verifies that redaction rejects a null replacement value.
    /// </summary>
    [Fact]
    public void RedactRejectsNullReplacement()
    {
        _ = Assert.Throws<ArgumentNullException>(() => GovernanceMetadataClassificationResult.Redact(
            "metadata.redact",
            "Redact message.",
            null!));
    }

    /// <summary>
    /// Verifies that classification factories reject null, empty, or whitespace reason members.
    /// </summary>
    /// <param name="action">The classification action exercised by the test.</param>
    /// <param name="code">The reason code supplied to the factory.</param>
    /// <param name="message">The reason message supplied to the factory.</param>
    [Theory]
    [InlineData(GovernanceMetadataSanitizationAction.Warn, null, "message")]
    [InlineData(GovernanceMetadataSanitizationAction.Warn, "", "message")]
    [InlineData(GovernanceMetadataSanitizationAction.Drop, "code", " ")]
    [InlineData(GovernanceMetadataSanitizationAction.Deny, " ", "message")]
    [InlineData(GovernanceMetadataSanitizationAction.Redact, "code", null)]
    public void ClassificationFactoriesRejectInvalidReasons(
        GovernanceMetadataSanitizationAction action,
        string? code,
        string? message)
    {
        _ = Assert.ThrowsAny<ArgumentException>(() => action switch
        {
            GovernanceMetadataSanitizationAction.Warn => GovernanceMetadataClassificationResult.Warn(code!, message!),
            GovernanceMetadataSanitizationAction.Redact => GovernanceMetadataClassificationResult.Redact(code!, message!),
            GovernanceMetadataSanitizationAction.Drop => GovernanceMetadataClassificationResult.Drop(code!, message!),
            GovernanceMetadataSanitizationAction.Deny => GovernanceMetadataClassificationResult.Deny(code!, message!),
            GovernanceMetadataSanitizationAction.Allow => throw new NotImplementedException(),
            _ => throw new InvalidOperationException("Unsupported test action.")
        });
    }

    /// <summary>
    /// Verifies that classification contexts reject null, empty, or whitespace metadata keys.
    /// </summary>
    /// <param name="key">The metadata key supplied to the context.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ClassificationContextRejectsInvalidKeys(string? key)
    {
        _ = Assert.ThrowsAny<ArgumentException>(() => new GovernanceMetadataClassificationContext(
            key!,
            "value",
            new Dictionary<string, string>()));
    }

    /// <summary>
    /// Verifies that classification contexts reject null metadata values.
    /// </summary>
    [Fact]
    public void ClassificationContextRejectsNullValue()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new GovernanceMetadataClassificationContext(
            "key",
            null!,
            new Dictionary<string, string>()));
    }

    /// <summary>
    /// Verifies that classification contexts reject a null metadata collection.
    /// </summary>
    [Fact]
    public void ClassificationContextRejectsNullMetadata()
    {
        _ = Assert.Throws<ArgumentNullException>(() => new GovernanceMetadataClassificationContext(
            "key",
            "value",
            null!));
    }

    /// <summary>
    /// Verifies that classification contexts preserve supplied values and the metadata reference.
    /// </summary>
    [Fact]
    public void ClassificationContextPreservesSuppliedValuesAndReference()
    {
        IReadOnlyDictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["key"] = "value"
        };

        var context = new GovernanceMetadataClassificationContext("key", "value", metadata);

        Assert.Equal("key", context.Key);
        Assert.Equal("value", context.Value);
        Assert.Same(metadata, context.Metadata);
    }

    /// <summary>
    /// Verifies that sanitation results reject undefined action values.
    /// </summary>
    [Fact]
    public void SanitationResultRejectsUndefinedAction()
    {
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => GovernanceMetadataSanitizationResult.Create(
            (GovernanceMetadataSanitizationAction)999,
            new Dictionary<string, string>(),
            Array.Empty<OperationReason>(),
            CreateValidBudgetValidation()));
    }

    /// <summary>
    /// Verifies that sanitation results reject null metadata, reasons, and budget-validation arguments.
    /// </summary>
    [Fact]
    public void SanitationResultRejectsNullArguments()
    {
        _ = Assert.Throws<ArgumentNullException>(() => GovernanceMetadataSanitizationResult.Create(
            GovernanceMetadataSanitizationAction.Allow,
            null!,
            Array.Empty<OperationReason>(),
            CreateValidBudgetValidation()));

        _ = Assert.Throws<ArgumentNullException>(() => GovernanceMetadataSanitizationResult.Create(
            GovernanceMetadataSanitizationAction.Allow,
            new Dictionary<string, string>(),
            null!,
            CreateValidBudgetValidation()));

        _ = Assert.Throws<ArgumentNullException>(() => GovernanceMetadataSanitizationResult.Create(
            GovernanceMetadataSanitizationAction.Allow,
            new Dictionary<string, string>(),
            Array.Empty<OperationReason>(),
            null!));
    }

    /// <summary>
    /// Verifies continuation and denial state for every defined sanitation action.
    /// </summary>
    /// <param name="action">The sanitation action exercised by the test.</param>
    /// <param name="canProceed">The expected continuation state.</param>
    /// <param name="isDenied">The expected denial state.</param>
    [Theory]
    [InlineData(GovernanceMetadataSanitizationAction.Allow, true, false)]
    [InlineData(GovernanceMetadataSanitizationAction.Warn, true, false)]
    [InlineData(GovernanceMetadataSanitizationAction.Redact, true, false)]
    [InlineData(GovernanceMetadataSanitizationAction.Drop, true, false)]
    [InlineData(GovernanceMetadataSanitizationAction.Deny, false, true)]
    public void SanitationResultReportsContinuationState(
        GovernanceMetadataSanitizationAction action,
        bool canProceed,
        bool isDenied)
    {
        var result = GovernanceMetadataSanitizationResult.Create(
            action,
            new Dictionary<string, string>(),
            Array.Empty<OperationReason>(),
            CreateValidBudgetValidation());

        Assert.Equal(canProceed, result.CanProceed);
        Assert.Equal(isDenied, result.IsDenied);
    }

    /// <summary>
    /// Verifies that non-denied sanitation results do not throw.
    /// </summary>
    /// <param name="action">The non-denied sanitation action exercised by the test.</param>
    [Theory]
    [InlineData(GovernanceMetadataSanitizationAction.Allow)]
    [InlineData(GovernanceMetadataSanitizationAction.Warn)]
    [InlineData(GovernanceMetadataSanitizationAction.Redact)]
    [InlineData(GovernanceMetadataSanitizationAction.Drop)]
    public void ThrowIfDeniedIsNoOpForNonDeniedResults(GovernanceMetadataSanitizationAction action)
    {
        var result = GovernanceMetadataSanitizationResult.Create(
            action,
            new Dictionary<string, string>(),
            Array.Empty<OperationReason>(),
            CreateValidBudgetValidation());

        result.ThrowIfDenied();
    }

    /// <summary>
    /// Verifies that denied results without reasons use the stable fallback message and parameter name.
    /// </summary>
    /// <param name="parameterName">The optional parameter name supplied to the denial guard.</param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void DeniedResultWithoutReasonsUsesFallbackMessageAndParameter(string? parameterName)
    {
        var result = GovernanceMetadataSanitizationResult.Create(
            GovernanceMetadataSanitizationAction.Deny,
            new Dictionary<string, string>(),
            Array.Empty<OperationReason>(),
            CreateValidBudgetValidation());

        ArgumentException exception = Assert.Throws<ArgumentException>(() => result.ThrowIfDenied(parameterName));

        Assert.Equal("metadata", exception.ParamName);
        Assert.Contains("Governance metadata sanitation denied the metadata collection.", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that denied results compose multiple reason messages and preserve a supplied parameter name.
    /// </summary>
    [Fact]
    public void DeniedResultComposesMultipleReasonMessagesAndPreservesParameterName()
    {
        var result = GovernanceMetadataSanitizationResult.Create(
            GovernanceMetadataSanitizationAction.Deny,
            new Dictionary<string, string>(),
            [
                OperationReason.Create("metadata.first", "First reason."),
                OperationReason.Create("metadata.second", "Second reason.")
            ],
            CreateValidBudgetValidation());

        ArgumentException exception = Assert.Throws<ArgumentException>(() => result.ThrowIfDenied("governanceMetadata"));

        Assert.Equal("governanceMetadata", exception.ParamName);
        Assert.Contains("First reason.; Second reason.", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that sanitation results preserve supplied metadata, reasons, and budget-validation references.
    /// </summary>
    [Fact]
    public void SanitationResultPreservesSuppliedReferences()
    {
        IReadOnlyDictionary<string, string> metadata = new Dictionary<string, string>
        {
            ["key"] = "value"
        };
        IReadOnlyList<OperationReason> reasons = [OperationReason.Create("metadata.warning", "Warning.")];
        GovernanceMetadataBudgetValidationResult budget = CreateValidBudgetValidation();

        var result = GovernanceMetadataSanitizationResult.Create(
            GovernanceMetadataSanitizationAction.Warn,
            metadata,
            reasons,
            budget);

        Assert.Same(metadata, result.SanitizedMetadata);
        Assert.Same(reasons, result.Reasons);
        Assert.Same(budget, result.BudgetValidation);
    }

    /// <summary>
    /// Verifies that a valid budget result does not throw.
    /// </summary>
    [Fact]
    public void BudgetThrowIfInvalidIsNoOpForValidResult()
    {
        CreateValidBudgetValidation().ThrowIfInvalid("governanceMetadata");
    }

    /// <summary>
    /// Verifies that invalid budget results compose violations and use the fallback parameter name when none is supplied.
    /// </summary>
    /// <param name="parameterName">The optional parameter name supplied to the budget guard.</param>
    /// <param name="expectedParameterName">The parameter name expected on the thrown exception.</param>
    [Theory]
    [InlineData(null, "metadata")]
    [InlineData("   ", "metadata")]
    [InlineData("governanceMetadata", "governanceMetadata")]
    public void BudgetThrowIfInvalidUsesSuppliedOrFallbackParameterName(string? parameterName, string expectedParameterName)
    {
        var result = GovernanceMetadataBudgetValidationResult.Create(
            new Dictionary<string, string>(),
            ["Too many entries.", "Value too long."],
            estimatedSerializedBytes: 128);

        ArgumentException exception = Assert.Throws<ArgumentException>(() => result.ThrowIfInvalid(parameterName));

        Assert.False(result.IsValid);
        Assert.Equal(expectedParameterName, exception.ParamName);
        Assert.Contains("Too many entries.; Value too long.", exception.Message, StringComparison.Ordinal);
    }

    private static GovernanceMetadataBudgetValidationResult CreateValidBudgetValidation()
    {
        return GovernanceMetadataBudgetValidationResult.Create(
            new Dictionary<string, string>(),
            Array.Empty<string>(),
            estimatedSerializedBytes: 0);
    }
}
