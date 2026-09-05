using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace AsiBackbone.AspNetCore.Endpoints;

/// <summary>
/// Middleware that evaluates AsiBackbone endpoint governance metadata before endpoint execution.
/// </summary>
public sealed class AsiBackboneEndpointGovernanceMiddleware
{
    // Example: use a precomputed static result for the most common forbidden response
    private static readonly IResult LightweightForbiddenResult =
        Microsoft.AspNetCore.Http.Results.StatusCode(StatusCodes.Status403Forbidden);
    // Use this for default cases instead of constructing new ProblemDetails every time.

    private readonly RequestDelegate next;
    private readonly AsiBackboneEndpointGovernanceOptions options;

    /// <summary>
    /// Initializes a new instance of the <see cref="AsiBackboneEndpointGovernanceMiddleware" /> class.
    /// </summary>
    /// <param name="next">The next request delegate.</param>
    /// <param name="endpointOptions">The endpoint governance options.</param>
    public AsiBackboneEndpointGovernanceMiddleware(
        RequestDelegate next,
        IOptions<AsiBackboneEndpointGovernanceOptions> endpointOptions)
    {
        ArgumentNullException.ThrowIfNull(next);
        ArgumentNullException.ThrowIfNull(endpointOptions);

        this.next = next;
        options = endpointOptions.Value;
    }

    /// <summary>
    /// Evaluates endpoint governance metadata and either continues execution or writes a failure result.
    /// </summary>
    /// <param name="httpContext">The current HTTP context.</param>
    /// <param name="governanceService">The endpoint governance service.</param>
    /// <returns>A task that completes when the middleware has run.</returns>
    public async Task InvokeAsync(
        HttpContext httpContext,
        IAsiBackboneEndpointGovernanceService governanceService)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(governanceService);

        // Options validation is registered with ValidateOnStart in AddAsiBackboneAspNetCore.
        // IOptions<T> is singleton-backed, so keep the validated value from construction rather
        // than resolving IOptions<T> again through method injection on every request.
        Endpoint? endpoint = httpContext.GetEndpoint();
        var descriptor = AsiBackboneEndpointGovernanceDescriptor.FromEndpoint(endpoint);

        bool endpointAllowsMissingGovernance = endpoint?.Metadata
            .GetMetadata<AllowMissingGovernanceMetadataAttribute>() is not null;

        if (!descriptor.HasGovernanceMetadata)
        {
            if (options.RequireGovernanceMetadata && !endpointAllowsMissingGovernance)
            {
                // A null endpoint and an endpoint carrying no governance metadata both fail closed here, but they
                // have different causes and different fixes. A null endpoint usually means this middleware runs
                // before endpoint routing, in which case every request looks ungoverned; reporting it under the same
                // stage as "this endpoint declares no metadata" hides a pipeline-ordering fault as a metadata gap.
                string decisionStage = endpoint is null
                    ? "aspnetcore.endpoint.governance.unresolved_endpoint"
                    : "aspnetcore.endpoint.governance.metadata";

                IResult missingGovernanceResult = CreateDefaultForbiddenResult(
                    httpContext,
                    options,
                    descriptor,
                    result: null,
                    decisionStage: decisionStage);

                await missingGovernanceResult.ExecuteAsync(httpContext).ConfigureAwait(false);
                return;
            }

            await next(httpContext).ConfigureAwait(false);
            return;
        }

        AsiBackboneEndpointGovernanceResult result = await governanceService
            .EvaluateAsync(httpContext, descriptor, httpContext.RequestAborted)
            .ConfigureAwait(false);

        if (result.CanExecute)
        {
            await next(httpContext).ConfigureAwait(false);
            return;
        }

        IResult failureResult = result.FailureResult ?? CreateDefaultForbiddenResult(
            httpContext,
            options,
            descriptor,
            result,
            "aspnetcore.endpoint.governance.decision");

        await failureResult.ExecuteAsync(httpContext).ConfigureAwait(false);
    }

    private static IResult CreateDefaultForbiddenResult(
        HttpContext httpContext,
        AsiBackboneEndpointGovernanceOptions options,
        AsiBackboneEndpointGovernanceDescriptor descriptor,
        AsiBackboneEndpointGovernanceResult? result,
        string decisionStage)
    {
        return AsiBackboneEndpointGovernanceDevelopmentDiagnostics.IsEnabled(httpContext, options)
            ? AsiBackboneEndpointGovernanceDevelopmentDiagnostics.CreateProblem(
                httpContext,
                options,
                descriptor,
                result?.Decision,
                decisionStage,
                title: "Endpoint governance blocked execution.",
                detail: "Endpoint governance blocked this request before the selected endpoint executed.",
                statusCode: StatusCodes.Status403Forbidden)
            : options.DefaultForbiddenResultFactory is null
            ? LightweightForbiddenResult
            : options.DefaultForbiddenResultFactory(httpContext)
                ?? throw new InvalidOperationException($"{nameof(AsiBackboneEndpointGovernanceOptions.DefaultForbiddenResultFactory)} must return a non-null result.");
    }
}
