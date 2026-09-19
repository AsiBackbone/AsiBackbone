using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.Endpoints;

/// <summary>
/// Contains unit tests for the EndpointGovernanceMiddleware and related classes, verifying that endpoint governance metadata is correctly read, that middleware behaves as expected under various conditions, and that configuration options are validated properly.
/// </summary>
public sealed class AsiBackboneEndpointGovernanceTests
{
    /// <summary>
    /// Verifies that the EndpointGovernanceDescriptor correctly reads governance metadata from an endpoint, including policy types, liability handshake requirement, capability scopes, and audit emission settings.
    /// </summary>
    [Fact]
    public void DescriptorReadsAttributeMetadataFromEndpoint()
    {
        var endpoint = new Endpoint(
            static context => Task.CompletedTask,
            new EndpointMetadataCollection(
                new GovernancePolicyAttribute(typeof(SamplePolicy)),
                new RequireLiabilityHandshakeAttribute(),
                new RequireCapabilityGrantAttribute("robotics.execute"),
                new EmitGovernanceAuditAttribute()),
            "sample.robotics.execute");

        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);

        Assert.True(descriptor.HasGovernanceMetadata);
        Assert.Equal("sample.robotics.execute", descriptor.OperationName);
        Assert.Contains(typeof(SamplePolicy), descriptor.PolicyTypes);
        Assert.True(descriptor.RequiresLiabilityHandshake);
        Assert.Contains("robotics.execute", descriptor.CapabilityScopes);
        Assert.True(descriptor.EmitGovernanceAudit);
        Assert.Equal("sample.robotics.execute", descriptor.ToMetadata()["endpoint.operation_name"]);
    }

    /// <summary>
    /// Verifies that the middleware skips governance when the endpoint has no governance metadata.
    /// </summary>
    /// <returns>A task representing the asynchronous operation.</returns>
    [Fact]
    public async Task MiddlewareSkipsGovernanceWhenEndpointHasNoGovernanceMetadata()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(new Endpoint(static _ => Task.CompletedTask, new EndpointMetadataCollection(), "plain"));
        bool nextCalled = false;
        EndpointGovernanceMiddleware middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            httpContext,
            new ThrowingEndpointGovernanceService());

        Assert.True(nextCalled);
    }

    /// <summary>
    /// Verifies that the middleware blocks execution when the governance service indicates that execution is blocked, and that the next delegate is not called.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task MiddlewareBlocksWhenGovernanceServiceBlocksExecution()
    {
        using ServiceProvider requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices
        };
        httpContext.Response.Body = new MemoryStream();
        httpContext.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequireCapabilityGrantAttribute("robotics.execute")),
            "blocked"));
        bool nextCalled = false;
        EndpointGovernanceMiddleware middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            httpContext,
            new BlockingEndpointGovernanceService());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
    }

    /// <summary>
    /// Verifies that the middleware uses a bodyless default forbidden result when the governance service blocks execution without providing a failure result, and that the next delegate is not called.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task MiddlewareUsesBodylessDefaultForbiddenResultWhenBlockedWithoutFailureResult()
    {
        using ServiceProvider requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices
        };
        httpContext.Response.Body = new MemoryStream();
        httpContext.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequireCapabilityGrantAttribute("robotics.execute")),
            "blocked.default"));
        bool nextCalled = false;
        EndpointGovernanceMiddleware middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            httpContext,
            new DefaultBlockingEndpointGovernanceService());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    /// <summary>
    /// Verifies that the middleware includes development diagnostics in the response body when execution is blocked and development diagnostics are enabled in a development environment, and that the next delegate is not called.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task MiddlewareIncludesDevelopmentDiagnosticsWhenEnabledInDevelopment()
    {
        using ServiceProvider requestServices = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment("Development"))
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices
        };
        httpContext.Response.Body = new MemoryStream();
        httpContext.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(
                new GovernancePolicyAttribute(typeof(SamplePolicy)),
                new RequireCapabilityGrantAttribute("robotics.execute")),
            "blocked.diagnostics"));
        bool nextCalled = false;
        EndpointGovernanceMiddleware middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new EndpointGovernanceOptions
            {
                EnableDevelopmentDiagnostics = true,
                DevelopmentDiagnosticsDocumentationBaseUrl = "https://asibackbone.github.io/AsiBackbone/articles/"
            });

        await middleware.InvokeAsync(
            httpContext,
            new DecisionDefaultBlockingEndpointGovernanceService());

        string body = await ReadResponseBodyAsync(httpContext);
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Contains("Endpoint governance blocked execution.", body, StringComparison.Ordinal);
        Assert.Contains("policy.denied", body, StringComparison.Ordinal);
        Assert.Contains("aspnetcore.endpoint.governance.decision", body, StringComparison.Ordinal);
        Assert.Contains("endpointPolicyTypes", body, StringComparison.Ordinal);
        Assert.Contains("robotics.execute", body, StringComparison.Ordinal);
        Assert.Contains("endpoint-governance-development-diagnostics.html", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the middleware does not include development diagnostics in the response body when execution is blocked, even in a development environment, if development diagnostics are not enabled, and that the next delegate is not called.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task MiddlewareExcludesDevelopmentDiagnosticsByDefaultEvenInDevelopment()
    {
        using ServiceProvider requestServices = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment("Development"))
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices
        };
        httpContext.Response.Body = new MemoryStream();
        httpContext.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new GovernancePolicyAttribute(typeof(SamplePolicy))),
            "blocked.default.development"));
        bool nextCalled = false;
        EndpointGovernanceMiddleware middleware = CreateMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            httpContext,
            new DecisionDefaultBlockingEndpointGovernanceService());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    /// <summary>
    /// Verifies that the middleware does not include development diagnostics in the response body when execution is blocked, even if development diagnostics are enabled, when the environment is not set to "Development", and that the next delegate is not called.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task MiddlewareExcludesDevelopmentDiagnosticsOutsideDevelopment()
    {
        using ServiceProvider requestServices = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment("Production"))
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices
        };
        httpContext.Response.Body = new MemoryStream();
        httpContext.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new GovernancePolicyAttribute(typeof(SamplePolicy))),
            "blocked.production"));
        bool nextCalled = false;
        EndpointGovernanceMiddleware middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new EndpointGovernanceOptions
            {
                EnableDevelopmentDiagnostics = true
            });

        await middleware.InvokeAsync(
            httpContext,
            new DecisionDefaultBlockingEndpointGovernanceService());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    /// <summary>
    /// Verifies that the middleware uses a configured default forbidden result factory to generate a rich failure response when execution is blocked without a specific failure result, and that the next delegate is not called.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task MiddlewareUsesConfiguredDefaultForbiddenResultFactoryWhenBlockedWithoutFailureResult()
    {
        using ServiceProvider requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices
        };
        httpContext.Response.Body = new MemoryStream();
        httpContext.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequireCapabilityGrantAttribute("robotics.execute")),
            "blocked.configured"));
        bool nextCalled = false;
        EndpointGovernanceMiddleware middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new EndpointGovernanceOptions
            {
                DefaultForbiddenResultFactory = _ => Microsoft.AspNetCore.Http.Results.Text(
                    "rich failure response",
                    statusCode: StatusCodes.Status403Forbidden)
            });

        await middleware.InvokeAsync(
            httpContext,
            new DefaultBlockingEndpointGovernanceService());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal("rich failure response", await ReadResponseBodyAsync(httpContext));
    }

    /// <summary>
    /// Verifies that the middleware preserves a custom failure result provided by the governance service, even when a configured default forbidden result factory exists, and that the next delegate is not called.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task MiddlewarePreservesCustomFailureResultWhenConfiguredDefaultForbiddenResultFactoryExists()
    {
        using ServiceProvider requestServices = new ServiceCollection().AddLogging().BuildServiceProvider();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices
        };
        httpContext.Response.Body = new MemoryStream();
        httpContext.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequireCapabilityGrantAttribute("robotics.execute")),
            "blocked.custom"));
        bool nextCalled = false;
        EndpointGovernanceMiddleware middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new EndpointGovernanceOptions
            {
                DefaultForbiddenResultFactory = _ => Microsoft.AspNetCore.Http.Results.Text(
                    "default factory response",
                    statusCode: StatusCodes.Status403Forbidden)
            });

        await middleware.InvokeAsync(
            httpContext,
            new TextBlockingEndpointGovernanceService("custom failure response", StatusCodes.Status409Conflict));

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status409Conflict, httpContext.Response.StatusCode);
        Assert.Equal("custom failure response", await ReadResponseBodyAsync(httpContext));
    }

    /// <summary>
    /// Verifies that the default governance service fails closed when a capability validator is missing, returning a failure result and a decision indicating the missing capability validator, and that execution is not allowed.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DefaultServiceFailsClosedWhenCapabilityValidatorIsMissing()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddAsiBackboneAspNetCore()
            .BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = services.CreateScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            TraceIdentifier = "trace-123"
        };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequireCapabilityGrantAttribute("robotics.execute")),
            "robotics.execute");
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IEndpointGovernanceService service = scope.ServiceProvider.GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(httpContext, descriptor, TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.NotNull(result.FailureResult);
        Assert.NotNull(result.Decision);
        Assert.Contains("endpoint.capability_validator.missing", result.Decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that the default governance service includes development diagnostics in the response body when a capability validator is missing, if development diagnostics are enabled and the environment is set to "Development", and that execution is not allowed.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DefaultServiceIncludesDevelopmentDiagnosticsForMissingCapabilityValidator()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddSingleton<IWebHostEnvironment>(new TestWebHostEnvironment("Development"))
            .Configure<EndpointGovernanceOptions>(options =>
            {
                options.EnableDevelopmentDiagnostics = true;
                options.DevelopmentDiagnosticsDocumentationBaseUrl = "https://asibackbone.github.io/AsiBackbone/articles";
            })
            .AddAsiBackboneAspNetCore()
            .BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = services.CreateScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            TraceIdentifier = "trace-dev-capability"
        };
        httpContext.Response.Body = new MemoryStream();
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequireCapabilityGrantAttribute("robotics.execute")),
            "robotics.execute");
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IEndpointGovernanceService service = scope.ServiceProvider.GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(httpContext, descriptor, TestContext.Current.CancellationToken);
        Assert.NotNull(result.FailureResult);
        await result.FailureResult.ExecuteAsync(httpContext);

        string body = await ReadResponseBodyAsync(httpContext);
        Assert.False(result.CanExecute);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Contains("endpoint.capability_validator.missing", body, StringComparison.Ordinal);
        Assert.Contains("Endpoint capability grant validation failed.", body, StringComparison.Ordinal);
        Assert.Contains("aspnetcore.endpoint.governance.capability.configuration", body, StringComparison.Ordinal);
        Assert.Contains("endpoint.operation_name", body, StringComparison.Ordinal);
        Assert.Contains("endpoint-governance-development-diagnostics.html", body, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the default governance service uses a default failure result when a policy decision is denied, returning a decision indicating the denial and no specific failure result, and that execution is not allowed.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task DefaultServiceUsesDefaultFailureForDeniedPolicyDecision()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddAsiBackboneAspNetCore()
            .AddSingleton<IGovernancePolicyEvaluator<GovernanceEvaluationContext>>(new DenyingPolicyEvaluator())
            .BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = services.CreateScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            TraceIdentifier = "trace-denied"
        };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new GovernancePolicyAttribute(typeof(SamplePolicy))),
            "policy.denied");
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IEndpointGovernanceService service = scope.ServiceProvider.GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await service.EvaluateAsync(httpContext, descriptor, TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.Null(result.FailureResult);
        Assert.NotNull(result.Decision);
        Assert.True(result.Decision.IsDenied);
        Assert.Contains("policy.denied", result.Decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies that the middleware blocks execution for an endpoint without governance metadata when the RequireGovernanceMetadata option is enabled, returning a 403 Forbidden status code and not calling the next delegate.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task MiddlewareBlocksUngovernedEndpointWhenRequireGovernanceMetadataIsEnabled()
    {
        using ServiceProvider requestServices = new ServiceCollection()
            .AddLogging()
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices
        };

        httpContext.Response.Body = new MemoryStream();
        httpContext.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(),
            "plain"));

        bool nextCalled = false;
        EndpointGovernanceMiddleware middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new EndpointGovernanceOptions
            {
                RequireGovernanceMetadata = true
            });

        await middleware.InvokeAsync(
            httpContext,
            new ThrowingEndpointGovernanceService());

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, httpContext.Response.StatusCode);
        Assert.Equal(0, httpContext.Response.Body.Length);
    }

    /// <summary>
    /// Verifies that the middleware allows execution for an endpoint without governance metadata when the RequireGovernanceMetadata option is enabled, but the endpoint has an explicit AllowMissingGovernanceMetadataAttribute, and that the next delegate is called.
    /// </summary>
    /// <returns>
    /// A task representing the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task MiddlewareAllowsUngovernedEndpointWithExplicitMissingGovernanceMetadataOptOut()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.SetEndpoint(new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowMissingGovernanceMetadataAttribute()),
            "public"));

        bool nextCalled = false;
        EndpointGovernanceMiddleware middleware = CreateMiddleware(
            _ =>
            {
                nextCalled = true;
                return Task.CompletedTask;
            },
            new EndpointGovernanceOptions
            {
                RequireGovernanceMetadata = true
            });

        await middleware.InvokeAsync(
            httpContext,
            new ThrowingEndpointGovernanceService());

        Assert.True(nextCalled);
    }

    /// <summary>
    /// Verifies that the EndpointGovernanceOptions.Validate method throws an InvalidOperationException when an invalid status code is configured for ConfigurationFailureStatusCode, and that the exception message contains the property name.
    /// </summary>
    [Fact]
    public void EndpointGovernanceOptionsValidateRejectsInvalidStatusCode()
    {
        var options = new EndpointGovernanceOptions
        {
            ConfigurationFailureStatusCode = 99
        };

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(nameof(EndpointGovernanceOptions.ConfigurationFailureStatusCode), exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies that the EndpointGovernanceOptions validation rejects post-configured invalid options, throwing an OptionsValidationException when the CapabilityFailureStatusCode is set to an invalid value, and that the exception message contains the expected text.
    /// </summary>
    [Fact]
    public void EndpointGovernanceOptionsValidationRejectsPostConfiguredInvalidOptions()
    {
        using ServiceProvider services = new ServiceCollection()
            .AddAsiBackboneAspNetCore()
            .Configure<EndpointGovernanceOptions>(options => options.CapabilityFailureStatusCode = 700)
            .BuildServiceProvider(validateScopes: true);

        OptionsValidationException exception = Assert.Throws<OptionsValidationException>(() =>
            _ = services.GetRequiredService<IOptions<EndpointGovernanceOptions>>().Value);

        Assert.Contains("Endpoint governance options must be valid.", exception.Message, StringComparison.Ordinal);
    }

    private static EndpointGovernanceMiddleware CreateMiddleware(
        RequestDelegate next,
        EndpointGovernanceOptions? options = null)
    {
        return new EndpointGovernanceMiddleware(
            next,
            Options.Create(options ?? new EndpointGovernanceOptions()));
    }

    private static async Task<string> ReadResponseBodyAsync(HttpContext httpContext)
    {
        httpContext.Response.Body.Position = 0;
        using var reader = new StreamReader(httpContext.Response.Body, leaveOpen: true);

        return await reader.ReadToEndAsync();
    }

    private sealed class SamplePolicy
    {
    }

    private sealed class ThrowingEndpointGovernanceService : IEndpointGovernanceService
    {
        public ValueTask<EndpointGovernanceResult> EvaluateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("Governance service should not be invoked for endpoints without governance metadata.");
        }
    }

    private sealed class BlockingEndpointGovernanceService : IEndpointGovernanceService
    {
        public ValueTask<EndpointGovernanceResult> EvaluateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(EndpointGovernanceResult.Block(
                Microsoft.AspNetCore.Http.Results.StatusCode(StatusCodes.Status403Forbidden)));
        }
    }

    private sealed class DefaultBlockingEndpointGovernanceService : IEndpointGovernanceService
    {
        public ValueTask<EndpointGovernanceResult> EvaluateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(EndpointGovernanceResult.BlockWithDefaultFailure());
        }
    }

    private sealed class DecisionDefaultBlockingEndpointGovernanceService : IEndpointGovernanceService
    {
        public ValueTask<EndpointGovernanceResult> EvaluateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(EndpointGovernanceResult.BlockWithDefaultFailure(
                GovernanceDecision.Deny(
                    "policy.denied",
                    "The policy denied execution.",
                    correlationId: "correlation-diagnostics",
                    traceId: "trace-diagnostics",
                    policyVersion: "test-policy",
                    policyHash: "test-policy-hash")));
        }
    }

    private sealed class TextBlockingEndpointGovernanceService(string body, int statusCode) : IEndpointGovernanceService
    {
        public ValueTask<EndpointGovernanceResult> EvaluateAsync(
            HttpContext httpContext,
            EndpointGovernanceDescriptor descriptor,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(EndpointGovernanceResult.Block(
                Microsoft.AspNetCore.Http.Results.Text(body, statusCode: statusCode)));
        }
    }

    private sealed class DenyingPolicyEvaluator : IGovernancePolicyEvaluator<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return new ValueTask<GovernanceDecision>(GovernanceDecision.Deny(
                "policy.denied",
                "The policy denied execution.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }
    }

    private sealed class TestWebHostEnvironment(string environmentName) : IWebHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "AsiBackbone.AspNetCore.Tests";

        public string WebRootPath { get; set; } = string.Empty;

        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();

        public string ContentRootPath { get; set; } = string.Empty;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
