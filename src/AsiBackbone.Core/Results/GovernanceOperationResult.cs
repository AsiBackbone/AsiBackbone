namespace AsiBackbone.Core.Results;

/// <summary>
/// Represents the outcome of an ASI Backbone operation.
/// </summary>
public sealed class GovernanceOperationResult
{
    private const string DefaultFailureMessage = "Operation failed.";

    private static readonly string[] EmptyMessages = [];

    private GovernanceOperationResult(bool succeeded, IReadOnlyList<string> messages)
    {
        Succeeded = succeeded;
        Messages = messages;
    }

    /// <summary>
    /// Gets a value indicating whether the operation succeeded.
    /// </summary>
    public bool Succeeded { get; }

    /// <summary>
    /// Gets a value indicating whether the operation failed.
    /// </summary>
    public bool Failed => !Succeeded;

    /// <summary>
    /// Gets the messages associated with the operation result.
    /// </summary>
    public IReadOnlyList<string> Messages { get; }

    /// <summary>
    /// Creates a successful result with no messages.
    /// </summary>
    /// <returns>A successful operation result.</returns>
    public static GovernanceOperationResult Success()
    {
        return new GovernanceOperationResult(true, EmptyMessages);
    }

    /// <summary>
    /// Creates a successful result with one message.
    /// </summary>
    /// <param name="message">The message associated with the successful result.</param>
    /// <returns>A successful operation result.</returns>
    public static GovernanceOperationResult Success(string message)
    {
        return new GovernanceOperationResult(true, NormalizeMessages([message]));
    }

    /// <summary>
    /// Creates a successful result with one or more messages.
    /// </summary>
    /// <param name="messages">The messages associated with the successful result.</param>
    /// <returns>A successful operation result.</returns>
    public static GovernanceOperationResult Success(IEnumerable<string> messages)
    {
        return new GovernanceOperationResult(true, NormalizeMessages(messages));
    }

    /// <summary>
    /// Creates a failed result with one message.
    /// </summary>
    /// <param name="message">The message associated with the failed result.</param>
    /// <returns>A failed operation result.</returns>
    public static GovernanceOperationResult Failure(string message)
    {
        return new GovernanceOperationResult(false, NormalizeMessages([message], DefaultFailureMessage));
    }

    /// <summary>
    /// Creates a failed result with one or more messages.
    /// </summary>
    /// <param name="messages">The messages associated with the failed result.</param>
    /// <returns>A failed operation result.</returns>
    public static GovernanceOperationResult Failure(IEnumerable<string> messages)
    {
        return new GovernanceOperationResult(false, NormalizeMessages(messages, DefaultFailureMessage));
    }

    private static IReadOnlyList<string> NormalizeMessages(
        IEnumerable<string>? messages,
        string? fallbackMessage = null)
    {
        string[] normalizedMessages = messages?
            .Where(message => !string.IsNullOrWhiteSpace(message))
            .Select(message => message.Trim())
            .ToArray() ?? [];

        return normalizedMessages.Length == 0
            ? fallbackMessage is null
                ? EmptyMessages
                : Array.AsReadOnly([fallbackMessage])
            : Array.AsReadOnly(normalizedMessages);
    }
}
