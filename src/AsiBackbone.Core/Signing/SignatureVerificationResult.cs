namespace AsiBackbone.Core.Signing;

/// <summary>
/// Represents the provider-neutral result of a signature verification operation.
/// </summary>
public sealed class SignatureVerificationResult
{
    private SignatureVerificationResult(
        bool isValid,
        string status,
        string? failureCode,
        string? failureMessage)
        : this(isValid, status, failureCode, failureMessage, category: null)
    {
    }

    private SignatureVerificationResult(
        bool isValid,
        string status,
        string? failureCode,
        string? failureMessage,
        SignatureVerificationCategory? category)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(status);

        IsValid = isValid;
        Status = status.Trim();
        FailureCode = NormalizeOptional(failureCode);
        FailureMessage = NormalizeOptional(failureMessage);
        Category = category;
    }

    /// <summary>
    /// Gets a value indicating whether the signature was verified successfully.
    /// </summary>
    public bool IsValid { get; }

    /// <summary>
    /// Gets a provider-neutral verification status.
    /// </summary>
    public string Status { get; }

    /// <summary>
    /// Gets a provider-neutral failure code when verification did not succeed.
    /// </summary>
    public string? FailureCode { get; }

    /// <summary>
    /// Gets a provider-neutral failure message when verification did not succeed.
    /// </summary>
    public string? FailureMessage { get; }

    /// <summary>
    /// Gets the explicit provider-neutral category, when the result producer supplied one.
    /// </summary>
    public SignatureVerificationCategory? Category { get; }

    /// <summary>
    /// Creates a successful verification result.
    /// </summary>
    public static SignatureVerificationResult Verified()
    {
        return new SignatureVerificationResult(true, "Verified", null, null, SignatureVerificationCategory.Valid);
    }

    /// <summary>
    /// Creates a failed verification result.
    /// </summary>
    public static SignatureVerificationResult Failed(string failureCode, string? failureMessage = null)
    {
        return new SignatureVerificationResult(false, "Failed", failureCode, failureMessage);
    }

    /// <summary>
    /// Creates a failed verification result with an explicit provider-neutral category.
    /// </summary>
    public static SignatureVerificationResult Failed(
        string failureCode,
        SignatureVerificationCategory category,
        string? failureMessage = null)
    {
        return !Enum.IsDefined(category) || category is SignatureVerificationCategory.Valid
            ? throw new ArgumentOutOfRangeException(nameof(category), category, "A failed verification result requires a defined failure category.")
            : new SignatureVerificationResult(false, "Failed", failureCode, failureMessage, category);
    }

    /// <summary>
    /// Creates a result indicating that no signature metadata was available to verify.
    /// </summary>
    public static SignatureVerificationResult MissingSignature(string? failureMessage = null)
    {
        return new SignatureVerificationResult(
            false,
            "MissingSignature",
            "signature.missing",
            failureMessage,
            SignatureVerificationCategory.MissingSignature);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
