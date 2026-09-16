using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Metadata;
using AsiBackbone.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.DependencyInjection;

/// <summary>
/// Tests for the regulated governance convenience registration profile.
/// </summary>
public sealed class RegulatedGovernanceProfileTests
{
    /// <summary>
    /// Verifies that the regulated profile applies strict options and registers the metadata sanitizer.
    /// </summary>
    [Fact]
    public void AddAsiBackboneRegulatedGovernanceRegistersConservativeProfile()
    {
        ServiceCollection services = new();

        _ = services.AddAsiBackboneRegulatedGovernance();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        GovernancePolicyOptions evaluatorOptions = provider
            .GetRequiredService<IOptions<GovernancePolicyOptions>>()
            .Value;
        EndpointGovernanceOptions endpointOptions = provider
            .GetRequiredService<IOptions<EndpointGovernanceOptions>>()
            .Value;

        Assert.True(evaluatorOptions.DenyWhenNoConstraints);
        Assert.True(evaluatorOptions.TreatConstraintExceptionAsDenial);
        Assert.True(evaluatorOptions.TreatThreatContributorExceptionAsDenial);
        Assert.True(evaluatorOptions.PreventThreatAssessmentAllowDowngrade);
        Assert.True(endpointOptions.FailClosedWhenPolicyEvaluatorMissing);
        Assert.True(endpointOptions.FailClosedWhenCapabilityValidatorMissing);
        Assert.True(endpointOptions.FailClosedWhenAuditSinkMissing);
        Assert.True(endpointOptions.RequireGovernanceMetadata);
        Assert.False(endpointOptions.IncludeDevelopmentDiagnosticsMetadataValues);

        using IServiceScope scope = provider.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IGovernanceMetadataSanitizer>();
        _ = scope.ServiceProvider.GetRequiredService<IEndpointGovernanceService>();
    }

    /// <summary>
    /// Verifies that the regulated profile can be selected through the builder facade.
    /// </summary>
    [Fact]
    public void UseRegulatedGovernanceProfileConfiguresBuilderFacade()
    {
        ServiceCollection services = new();

        _ = services.AddAsiBackbone(builder => builder.UseRegulatedGovernanceProfile());

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        EndpointGovernanceOptions endpointOptions = provider
            .GetRequiredService<IOptions<EndpointGovernanceOptions>>()
            .Value;

        Assert.True(endpointOptions.RequireGovernanceMetadata);

        using IServiceScope scope = provider.CreateScope();
        _ = scope.ServiceProvider.GetRequiredService<IGovernanceMetadataSanitizer>();
    }

    /// <summary>
    /// Verifies that a host-owned classifier can fail closed before endpoint policy evaluation or audit emission.
    /// </summary>
    [Fact]
    public async Task RegulatedProfileBlocksMetadataDeniedByClassifier()
    {
        ServiceCollection services = new();
        _ = services.AddSingleton<IGovernanceMetadataClassifier, DenyEndpointMetadataClassifier>();
        _ = services.AddAsiBackboneRegulatedGovernance();

        using ServiceProvider provider = services.BuildServiceProvider(validateScopes: true);
        using IServiceScope scope = provider.CreateScope();
        var httpContext = new DefaultHttpContext
        {
            RequestServices = scope.ServiceProvider,
            TraceIdentifier = "regulated-trace"
        };
        scope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

        var endpoint = new Endpoint(
            static _ => Task.CompletedTask,
            new EndpointMetadataCollection(new RequireGovernancePolicyAttribute(typeof(RegulatedPolicy))),
            "regulated.operation");
        var descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
        IEndpointGovernanceService governanceService = scope.ServiceProvider
            .GetRequiredService<IEndpointGovernanceService>();

        EndpointGovernanceResult result = await governanceService
            .EvaluateAsync(httpContext, descriptor, TestContext.Current.CancellationToken);

        Assert.False(result.CanExecute);
        Assert.NotNull(result.Decision);
        Assert.Contains("endpoint.metadata_sanitization.denied", result.Decision.ReasonCodes);
        Assert.Contains("test.metadata.denied", result.Decision.ReasonCodes);
        Assert.DoesNotContain("endpoint.policy_evaluator.missing", result.Decision.ReasonCodes);
    }

    /// <summary>
    /// Verifies null service-collection validation.
    /// </summary>
    [Fact]
    public void AddAsiBackboneRegulatedGovernanceRejectsNullServices()
    {
        IServiceCollection? services = null;

        _ = Assert.Throws<ArgumentNullException>(() => services!.AddAsiBackboneRegulatedGovernance());
    }

    private sealed class RegulatedPolicy
    {
    }

    private sealed class DenyEndpointMetadataClassifier : IGovernanceMetadataClassifier
    {
        public ValueTask<GovernanceMetadataClassificationResult> ClassifyAsync(
            GovernanceMetadataClassificationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            return ValueTask.FromResult(
                GovernanceMetadataClassificationResult.Deny(
                    "test.metadata.denied",
                    "The test classifier denied endpoint governance metadata."));
        }
    }
}
