namespace AsiBackbone.Testing.Contracts;

/// <summary>
/// Represents a reusable AsiBackbone contract-test invariant failure.
/// </summary>
/// <remarks>
/// Contract helpers throw this exception instead of binding the testing package to a specific unit-test framework.
/// xUnit, NUnit, MSTest, and custom harnesses can assert on this exception type while sharing the same invariant checks.
/// </remarks>
public sealed class GovernanceContractViolationException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GovernanceContractViolationException" /> class.
    /// </summary>
    /// <param name="message">The contract failure message.</param>
    public GovernanceContractViolationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GovernanceContractViolationException" /> class.
    /// </summary>
    /// <param name="message">The contract failure message.</param>
    /// <param name="innerException">The implementation exception that triggered the contract failure.</param>
    public GovernanceContractViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
