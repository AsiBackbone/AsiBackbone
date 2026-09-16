using AsiBackbone.AspNetCore.Results;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Results;

/// <summary>
/// Unit tests for the <see cref="GovernanceHttpResultMappingExtensions"/> class, verifying the mapping of governance decisions and operation results to HTTP responses in an ASP.NET Core context.
/// </summary>
public sealed class AsiBackboneHttpResultMappingExtensionsTests
{
    /// <summary>
    /// Verifies that a governance decision of type "Allow" is correctly mapped to an HTTP 200 OK response, including the expected content in the response body.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultMapsAllowedDecisionToSuccessResponse()
    {
        var decision = GovernanceDecision.Allow(correlationId: " correlation-123 ");

        HttpResultCapture capture = await ExecuteAsync(decision.ToHttpResult());

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        Assert.Contains("\"allowed\":true", capture.Body, StringComparison.Ordinal);
        Assert.Contains("Allowed", capture.Body, StringComparison.Ordinal);
        Assert.Contains("correlation-123", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a governance decision of type "Warning" is correctly mapped to an HTTP response with a configurable warning status code, and that the response body contains the expected warning details.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultMapsWarningDecisionToConfiguredWarningStatusCode()
    {
        var decision = GovernanceDecision.Warning("policy.warning", "Warning detail.");
        var options = new GovernanceHttpResultMappingOptions { WarningStatusCode = StatusCodes.Status206PartialContent };

        HttpResultCapture capture = await ExecuteAsync(decision.ToHttpResult(options));

        Assert.Equal(StatusCodes.Status206PartialContent, capture.StatusCode);
        Assert.Contains("Warning", capture.Body, StringComparison.Ordinal);
        Assert.Contains("policy.warning", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a governance decision of type "Deny" is correctly mapped to an HTTP 403 Forbidden response, and that the response body contains the expected denial details while omitting sensitive information by default.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultMapsDeniedDecisionToProblemDetails()
    {
        var decision = GovernanceDecision.Deny(
            "policy.denied",
            "Policy detail.",
            correlationId: "correlation-deny",
            traceId: "trace-deny",
            policyVersion: "v1",
            policyHash: "hash-value");

        HttpResultCapture capture = await ExecuteAsync(decision.ToHttpResult());

        Assert.Equal(StatusCodes.Status403Forbidden, capture.StatusCode);
        Assert.Contains("Governance decision denied execution.", capture.Body, StringComparison.Ordinal);
        Assert.Contains("policy.denied", capture.Body, StringComparison.Ordinal);
        Assert.Contains("correlation-deny", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Policy detail", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("trace-deny", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("hash-value", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a governance decision of type "Defer" is correctly mapped to an HTTP 202 Accepted response, and that the response body contains the expected deferred execution details.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultMapsDeferredDecisionToAcceptedProblemDetails()
    {
        var decision = GovernanceDecision.Defer("policy.deferred", "Try again later.");

        HttpResultCapture capture = await ExecuteAsync(decision.ToHttpResult());

        Assert.Equal(StatusCodes.Status202Accepted, capture.StatusCode);
        Assert.Contains("Governance decision deferred execution.", capture.Body, StringComparison.Ordinal);
        Assert.Contains("policy.deferred", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a governance decision of type "RequireAcknowledgment" is correctly mapped to an HTTP 428 Precondition Required response, and that the response body contains the expected acknowledgment details.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultMapsAcknowledgmentRequiredDecisionToPreconditionProblemDetails()
    {
        var decision = GovernanceDecision.RequireAcknowledgment("ack.required", "User acknowledgment required.");

        HttpResultCapture capture = await ExecuteAsync(decision.ToHttpResult());

        Assert.Equal(StatusCodes.Status428PreconditionRequired, capture.StatusCode);
        Assert.Contains("Governance decision requires acknowledgment.", capture.Body, StringComparison.Ordinal);
        Assert.Contains("ack.required", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a governance decision of type "Escalate" is correctly mapped to an HTTP 409 Conflict response, and that the response body contains the expected escalation details.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultMapsEscalationRecommendedDecisionToConflictProblemDetails()
    {
        var decision = GovernanceDecision.Escalate("escalation.required", "Manual review required.");

        HttpResultCapture capture = await ExecuteAsync(decision.ToHttpResult());

        Assert.Equal(StatusCodes.Status409Conflict, capture.StatusCode);
        Assert.Contains("Governance decision recommends escalation.", capture.Body, StringComparison.Ordinal);
        Assert.Contains("escalation.required", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the HTTP result mapping can expose reason messages and diagnostic metadata when configured.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultCanExposeReasonMessagesAndDiagnosticMetadataWhenConfigured()
    {
        var decision = GovernanceDecision.Deny(
            "policy.denied",
            "Public detail.",
            correlationId: "correlation-public",
            traceId: "trace-public",
            policyVersion: "v2",
            policyHash: "hash-public");
        var options = new GovernanceHttpResultMappingOptions
        {
            IncludeReasonMessages = true,
            IncludeTraceId = true,
            IncludePolicyMetadata = true,
        };

        HttpResultCapture capture = await ExecuteAsync(decision.ToHttpResult(options));

        Assert.Contains("Public detail.", capture.Body, StringComparison.Ordinal);
        Assert.Contains("trace-public", capture.Body, StringComparison.Ordinal);
        Assert.Contains("v2", capture.Body, StringComparison.Ordinal);
        Assert.Contains("hash-public", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that when diagnostics are enabled, missing optional decision metadata is omitted from the HTTP result, ensuring that the response does not include null or empty fields for reason codes, reason messages, correlation ID, trace ID, policy
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultOmitsMissingOptionalDecisionMetadataWhenDiagnosticsAreEnabled()
    {
        var decision = GovernanceDecision.Allow();
        var options = new GovernanceHttpResultMappingOptions
        {
            IncludeReasonMessages = true,
            IncludeTraceId = true,
            IncludePolicyMetadata = true,
        };

        HttpResultCapture capture = await ExecuteAsync(decision.ToHttpResult(options));

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        Assert.DoesNotContain("reasonCodes", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("reasonMessages", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("correlationId", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("traceId", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("policyVersion", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("policyHash", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a successful operation result is correctly mapped to an HTTP 200 OK response, and that the response body contains the expected success details along with any warning messages provided.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultMapsSuccessfulOperationResultToSuccessResponse()
    {
        var result = OperationResult.Success(["Completed with warning."]);

        HttpResultCapture capture = await ExecuteAsync(result.ToHttpResult());

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        Assert.Contains("\"succeeded\":true", capture.Body, StringComparison.Ordinal);
        Assert.Contains("Completed with warning.", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a successful operation result without any warnings is correctly mapped to an HTTP 200 OK response, and that the response body contains the expected success details without any warning messages.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultMapsSuccessfulOperationResultWithoutWarnings()
    {
        var result = OperationResult.Success();

        HttpResultCapture capture = await ExecuteAsync(result.ToHttpResult());

        Assert.Equal(StatusCodes.Status200OK, capture.StatusCode);
        Assert.Contains("\"succeeded\":true", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("warnings", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a failed operation result is correctly mapped to an HTTP 400 Bad Request response, and that the response body contains the expected failure details while omitting sensitive reason messages by default.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultMapsFailedOperationResultToProblemDetailsWithoutReasonMessagesByDefault()
    {
        var result = OperationResult.Failure("operation.denied", "Failure detail.");

        HttpResultCapture capture = await ExecuteAsync(result.ToHttpResult());

        Assert.Equal(StatusCodes.Status400BadRequest, capture.StatusCode);
        Assert.Contains("operation.denied", capture.Body, StringComparison.Ordinal);
        Assert.Contains("The operation did not complete successfully.", capture.Body, StringComparison.Ordinal);
        Assert.DoesNotContain("Failure detail", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that a failed operation result can expose reason messages when the corresponding option is configured.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation of executing the HTTP result and capturing the response for assertion.
    /// </returns>
    [Fact]
    public async Task ToHttpResultCanExposeFailedOperationReasonMessagesWhenConfigured()
    {
        var result = OperationResult.Failure("operation.denied", "Public operation detail.");
        var options = new GovernanceHttpResultMappingOptions
        {
            IncludeReasonMessages = true,
            OperationFailureStatusCode = StatusCodes.Status409Conflict,
            OperationFailureMessage = "Custom safe failure message.",
        };

        HttpResultCapture capture = await ExecuteAsync(result.ToHttpResult(options));

        Assert.Equal(StatusCodes.Status409Conflict, capture.StatusCode);
        Assert.Contains("Custom safe failure message.", capture.Body, StringComparison.Ordinal);
        Assert.Contains("Public operation detail.", capture.Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the ToHttpResult extension method throws an ArgumentNullException when a null GovernanceDecision is passed, ensuring that the method enforces non-null input for proper HTTP result mapping.
    /// </summary>
    [Fact]
    public void ToHttpResultRejectsNullDecision()
    {
        GovernanceDecision? decision = null;

        _ = Assert.Throws<ArgumentNullException>(() => decision!.ToHttpResult());
    }

    /// <summary>
    /// Verifies that the ToHttpResult extension method throws an ArgumentNullException when a null GovernanceHttpResultMappingOptions is passed, ensuring that the method enforces non-null options for proper HTTP result mapping.
    /// </summary>
    [Fact]
    public void ToHttpResultRejectsNullDecisionOptions()
    {
        var decision = GovernanceDecision.Allow();
        GovernanceHttpResultMappingOptions? options = null;

        _ = Assert.Throws<ArgumentNullException>(() => decision.ToHttpResult(options!));
    }

    /// <summary>
    /// Verifies that the ToHttpResult extension method throws an ArgumentNullException when a null OperationResult is passed, ensuring that the method enforces non-null input for proper HTTP result mapping.
    /// </summary>
    [Fact]
    public void ToHttpResultRejectsNullOperationResult()
    {
        OperationResult? result = null;

        _ = Assert.Throws<ArgumentNullException>(() => result!.ToHttpResult());
    }

    /// <summary>
    /// Verifies that the ToHttpResult extension method throws an ArgumentNullException when a null GovernanceHttpResultMappingOptions is passed for an OperationResult, ensuring that the method enforces non-null options for proper HTTP result mapping.
    /// </summary>
    [Fact]
    public void ToHttpResultRejectsNullOperationResultOptions()
    {
        var result = OperationResult.Success();
        GovernanceHttpResultMappingOptions? options = null;

        _ = Assert.Throws<ArgumentNullException>(() => result.ToHttpResult(options!));
    }

    /// <summary>
    /// Verifies that the GovernanceHttpResultMappingOptions class rejects invalid HTTP status codes for its properties, ensuring that only valid status codes are accepted and that an InvalidOperationException is thrown when an invalid code is set.
    /// </summary>
    /// <param name="propertyName">
    /// The name of the property in GovernanceHttpResultMappingOptions to test for invalid status code assignment.
    /// </param>
    [Theory]
    [InlineData(nameof(GovernanceHttpResultMappingOptions.SuccessStatusCode))]
    [InlineData(nameof(GovernanceHttpResultMappingOptions.WarningStatusCode))]
    [InlineData(nameof(GovernanceHttpResultMappingOptions.DeniedStatusCode))]
    [InlineData(nameof(GovernanceHttpResultMappingOptions.DeferredStatusCode))]
    [InlineData(nameof(GovernanceHttpResultMappingOptions.AcknowledgmentRequiredStatusCode))]
    [InlineData(nameof(GovernanceHttpResultMappingOptions.EscalationRecommendedStatusCode))]
    [InlineData(nameof(GovernanceHttpResultMappingOptions.OperationFailureStatusCode))]
    public void ResultMappingOptionsRejectInvalidStatusCode(string propertyName)
    {
        var options = new GovernanceHttpResultMappingOptions();
        SetStatusCode(options, propertyName, 99);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(propertyName, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the GovernanceHttpResultMappingOptions class rejects blank or whitespace-only safe messages for its properties, ensuring that meaningful messages are provided and that an InvalidOperationException is thrown when a blank message is set.
    /// </summary>
    /// <param name="propertyName">
    /// The name of the property in GovernanceHttpResultMappingOptions to test for blank safe message assignment.
    /// </param>
    [Theory]
    [InlineData(nameof(GovernanceHttpResultMappingOptions.GovernanceDecisionNotAllowedMessage))]
    [InlineData(nameof(GovernanceHttpResultMappingOptions.OperationFailureMessage))]
    public void ResultMappingOptionsRejectBlankSafeMessages(string propertyName)
    {
        var options = new GovernanceHttpResultMappingOptions();
        SetSafeMessage(options, propertyName, "   ");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains("safe", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static void SetStatusCode(
        GovernanceHttpResultMappingOptions options,
        string propertyName,
        int statusCode)
    {
        switch (propertyName)
        {
            case nameof(GovernanceHttpResultMappingOptions.SuccessStatusCode):
                options.SuccessStatusCode = statusCode;
                break;
            case nameof(GovernanceHttpResultMappingOptions.WarningStatusCode):
                options.WarningStatusCode = statusCode;
                break;
            case nameof(GovernanceHttpResultMappingOptions.DeniedStatusCode):
                options.DeniedStatusCode = statusCode;
                break;
            case nameof(GovernanceHttpResultMappingOptions.DeferredStatusCode):
                options.DeferredStatusCode = statusCode;
                break;
            case nameof(GovernanceHttpResultMappingOptions.AcknowledgmentRequiredStatusCode):
                options.AcknowledgmentRequiredStatusCode = statusCode;
                break;
            case nameof(GovernanceHttpResultMappingOptions.EscalationRecommendedStatusCode):
                options.EscalationRecommendedStatusCode = statusCode;
                break;
            case nameof(GovernanceHttpResultMappingOptions.OperationFailureStatusCode):
                options.OperationFailureStatusCode = statusCode;
                break;
            default:
                throw new InvalidOperationException("Unknown status code option.");
        }
    }

    private static void SetSafeMessage(
        GovernanceHttpResultMappingOptions options,
        string propertyName,
        string message)
    {
        switch (propertyName)
        {
            case nameof(GovernanceHttpResultMappingOptions.GovernanceDecisionNotAllowedMessage):
                options.GovernanceDecisionNotAllowedMessage = message;
                break;
            case nameof(GovernanceHttpResultMappingOptions.OperationFailureMessage):
                options.OperationFailureMessage = message;
                break;
            default:
                throw new InvalidOperationException("Unknown message option.");
        }
    }

    private static async Task<HttpResultCapture> ExecuteAsync(IResult result)
    {
        using ServiceProvider serviceProvider = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
        };
        await using var body = new MemoryStream();
        httpContext.Response.Body = body;

        await result.ExecuteAsync(httpContext);

        _ = body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(body);
        string content = await reader.ReadToEndAsync();

        return new HttpResultCapture(httpContext.Response.StatusCode, content);
    }

    private sealed record HttpResultCapture(int StatusCode, string Body);
}
