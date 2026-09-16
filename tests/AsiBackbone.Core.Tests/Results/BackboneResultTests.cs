using Xunit;

namespace AsiBackbone.Core.Tests.Results;

/// <summary>
/// Unit tests for the GovernanceOperationResult class, verifying the behavior of success and failure result creation, message normalization, and handling of blank messages.
/// </summary>
public sealed class BackboneResultTests
{
    /// <summary>
    /// Verifies that the Success method creates a result with Succeeded = true, Failed = false, and no messages.
    /// </summary>
    [Fact]
    public void SuccessCreatesSucceededResult()
    {
        var result = GovernanceOperationResult.Success();

        Assert.True(result.Succeeded);
        Assert.False(result.Failed);
        Assert.Empty(result.Messages);
    }

    /// <summary>
    /// Verifies that the Success method with a message creates a result with Succeeded = true, Failed = false, and the message is stored without leading/trailing whitespace.
    /// </summary>
    [Fact]
    public void SuccessWithMessageStoresNormalizedMessage()
    {
        var result = GovernanceOperationResult.Success(" Operation completed. ");

        Assert.True(result.Succeeded);
        Assert.False(result.Failed);
        _ = Assert.Single(result.Messages);
        Assert.Equal("Operation completed.", result.Messages[0]);
    }

    /// <summary>
    /// Verifies that the Success method with populated messages creates a result with normalized messages.
    /// </summary>
    [Fact]
    public void SuccessWithMessagesStoresNormalizedMessages()
    {
        var result = GovernanceOperationResult.Success(
        [
            " First success. ",
            " Second success. "
        ]);

        Assert.True(result.Succeeded);
        Assert.False(result.Failed);
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("First success.", result.Messages[0]);
        Assert.Equal("Second success.", result.Messages[1]);
    }

    /// <summary>
    /// Verifies that the Success method with multiple messages creates a result with Succeeded = true, Failed = false, and all messages are stored without leading/trailing whitespace.
    /// </summary>
    [Fact]
    public void SuccessWithBlankMessagesReturnsEmptyMessages()
    {
        var result = GovernanceOperationResult.Success(["", "   "]);

        Assert.True(result.Succeeded);
        Assert.Empty(result.Messages);
    }

    /// <summary>
    /// Verifies that the Failure method creates a result with Succeeded = false, Failed = true, and the message is stored without leading/trailing whitespace.
    /// </summary>
    [Fact]
    public void FailureCreatesFailedResult()
    {
        var result = GovernanceOperationResult.Failure(" Operation failed for validation reasons. ");

        Assert.False(result.Succeeded);
        Assert.True(result.Failed);
        _ = Assert.Single(result.Messages);
        Assert.Equal("Operation failed for validation reasons.", result.Messages[0]);
    }

    /// <summary>
    /// Verifies that the Failure method with multiple messages creates a result with Succeeded = false, Failed = true, and all messages are stored without leading/trailing whitespace.
    /// </summary>
    [Fact]
    public void FailureWithMessagesStoresNormalizedMessages()
    {
        var result = GovernanceOperationResult.Failure(
        [
            " First error. ",
            "",
            " Second error. "
        ]);

        Assert.False(result.Succeeded);
        Assert.True(result.Failed);
        Assert.Equal(2, result.Messages.Count);
        Assert.Equal("First error.", result.Messages[0]);
        Assert.Equal("Second error.", result.Messages[1]);
    }

    /// <summary>
    /// Verifies that the Failure method with blank messages creates a result with Succeeded = false, Failed = true, and a single default failure message.
    /// </summary>
    [Fact]
    public void FailureWithBlankMessagesUsesDefaultFailureMessage()
    {
        var result = GovernanceOperationResult.Failure(["", "   "]);

        Assert.False(result.Succeeded);
        Assert.True(result.Failed);
        _ = Assert.Single(result.Messages);
        Assert.Equal("Operation failed.", result.Messages[0]);
    }

    /// <summary>
    /// Verifies that the Success method with null messages creates a result with Succeeded = true, Failed = false, and no messages.
    /// </summary>
    [Fact]
    public void SuccessWithNullMessagesReturnsEmptyMessages()
    {
        var result = GovernanceOperationResult.Success((IEnumerable<string>?)null!);

        Assert.True(result.Succeeded);
        Assert.False(result.Failed);
        Assert.Empty(result.Messages);
    }

    /// <summary>
    /// Verifies that the Success method with a null message creates a result with Succeeded = true, Failed = false, and no messages.
    /// </summary>
    [Fact]
    public void SuccessWithNullMessageReturnsEmptyMessages()
    {
        var result = GovernanceOperationResult.Success((string)null!);

        Assert.True(result.Succeeded);
        Assert.False(result.Failed);
        Assert.Empty(result.Messages);
    }

    /// <summary>
    /// Verifies that the Failure method with null messages creates a result with Succeeded = false, Failed = true, and a single default failure message.
    /// </summary>
    [Fact]
    public void FailureWithNullMessagesUsesDefaultFailureMessage()
    {
        var result = GovernanceOperationResult.Failure((IEnumerable<string>?)null!);

        Assert.False(result.Succeeded);
        Assert.True(result.Failed);
        Assert.Equal("Operation failed.", Assert.Single(result.Messages));
    }

    /// <summary>
    /// Verifies that the Failure method with a null message creates a result with Succeeded = false, Failed = true, and a single default failure message.
    /// </summary>
    [Fact]
    public void FailureWithNullMessageUsesDefaultFailureMessage()
    {
        var result = GovernanceOperationResult.Failure((string)null!);

        Assert.False(result.Succeeded);
        Assert.True(result.Failed);
        Assert.Equal("Operation failed.", Assert.Single(result.Messages));
    }

    /// <summary>
    /// Verifies that the Success method with messages containing null values creates a result with Succeeded = true, Failed = false, and only the non-null messages are stored without leading/trailing whitespace.
    /// </summary>
    [Fact]
    public void SuccessWithMessagesContainingNullFiltersNullMessages()
    {
        string[] messages =
        [
            " Operation completed. ",
            null!
        ];

        var result = GovernanceOperationResult.Success(messages);

        Assert.True(result.Succeeded);
        Assert.Equal("Operation completed.", Assert.Single(result.Messages));
    }

    /// <summary>
    /// Verifies that the Failure method with messages containing null values creates a result with Succeeded = false, Failed = true, and only the non-null messages are stored without leading/trailing whitespace.
    /// </summary>
    [Fact]
    public void FailureWithMessagesContainingNullFiltersNullMessages()
    {
        string[] messages =
        [
            " Operation failed for validation reasons. ",
            null!
        ];

        var result = GovernanceOperationResult.Failure(messages);

        Assert.False(result.Succeeded);
        Assert.Equal("Operation failed for validation reasons.", Assert.Single(result.Messages));
    }
}
