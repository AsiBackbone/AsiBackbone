using AsiBackbone.AspNetCore.Acknowledgments;
using AsiBackbone.AspNetCore.Actors;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Signing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace AsiBackbone.AspNetCore.CapabilityGrants;

/// <summary>
/// Opt-in <see cref="IEndpointCapabilityGrantValidator" /> that accepts only a signed, request-bound capability grant
/// carried as a <see cref="CapabilityGrantToken" />.
/// </summary>
/// <remarks>
/// <para>
/// For every decision that is not already denied, the validator reads the token from the request, decodes it, resolves
/// the request binding, and validates the grant with
/// <see cref="CapabilityGrantValidationOptions.CreateBoundExecutionBoundary" /> built from the authenticated actor, the
/// endpoint operation and scopes, the policy version and hash of the current decision, and the configured issuer and
/// audience. Proof verification is always required. A use is consumed for every outcome except acknowledgment-required:
/// that request is answered with a challenge and never executes, so its grant is verified but left for the acknowledged
/// retry, which is validated and consumed in turn.
/// </para>
/// <para>
/// Every result other than <see cref="CapabilityGrantValidationCategory.Valid" /> produces a denied decision with a
/// reason code from <see cref="SignedCapabilityGrantReasonCodes" />, whatever <see cref="VerificationPolicyAction" /> the
/// core validator attached. A not-yet-valid grant, an unavailable use store, an unspecified use state, or a grant missing
/// a required acknowledgment reference is therefore denied rather than deferred, escalated, or challenged. A valid grant
/// returns the current decision unchanged, so it never bypasses an acknowledgment challenge, and records the grant in
/// <see cref="IEndpointCapabilityGrantFeature" /> for the decision receipt. An existing denial is returned unchanged
/// before the token is read, so it consumes no use.
/// </para>
/// <para>
/// The host supplies the <see cref="IGovernanceSignatureVerificationService" />, the <see cref="ICapabilityGrantUseStore" />,
/// and an <see cref="ICapabilityGrantRequestBindingProvider" />. A missing service denies the request rather than
/// weakening validation.
/// </para>
/// </remarks>
public sealed class SignedCapabilityGrantEndpointValidator : IEndpointCapabilityGrantValidator
{
    private static readonly Action<ILogger, string, string, Exception?> LogGrantDenied =
        LoggerMessage.Define<string, string>(
            LogLevel.Information,
            new EventId(19811, nameof(LogGrantDenied)),
            "Signed capability grant denied with {ReasonCode} ({DetailCode}).");

    private static readonly Action<ILogger, string, Exception?> LogValidationFailed =
        LoggerMessage.Define<string>(
            LogLevel.Warning,
            new EventId(19812, nameof(LogValidationFailed)),
            "Signed capability grant validation failed unexpectedly for operation {OperationName}.");

    private readonly IServiceProvider serviceProvider;
    private readonly IHttpGovernanceActorContextResolver actorContextResolver;
    private readonly SignedCapabilityGrantOptions options;
    private readonly TimeProvider timeProvider;
    private readonly ILogger logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SignedCapabilityGrantEndpointValidator" /> class.
    /// </summary>
    /// <param name="serviceProvider">The request service provider used to resolve host-supplied services.</param>
    /// <param name="actorContextResolver">The actor resolver that supplies the authenticated subject.</param>
    /// <param name="options">The signed capability grant options.</param>
    /// <param name="logger">An optional logger for denial diagnostics; token content is never logged.</param>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The options are invalid.</exception>
    public SignedCapabilityGrantEndpointValidator(
        IServiceProvider serviceProvider,
        IHttpGovernanceActorContextResolver actorContextResolver,
        IOptions<SignedCapabilityGrantOptions> options,
        ILogger<SignedCapabilityGrantEndpointValidator>? logger = null)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        this.actorContextResolver = actorContextResolver ?? throw new ArgumentNullException(nameof(actorContextResolver));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.options.Validate();
        timeProvider = serviceProvider.GetService<TimeProvider>() ?? TimeProvider.System;
        this.logger = logger ?? NullLogger<SignedCapabilityGrantEndpointValidator>.Instance;
    }

    /// <inheritdoc />
    public async ValueTask<GovernanceDecision> ValidateAsync(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        GovernanceDecision currentDecision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(currentDecision);
        cancellationToken.ThrowIfCancellationRequested();

        // An existing denial is final. Returning it before the token is read keeps a denied request from consuming a use.
        if (currentDecision.IsDenied || descriptor.CapabilityScopes.Count == 0)
        {
            return currentDecision;
        }

        IGovernanceActorContext actor = actorContextResolver.ResolveActorContext();
        if (!AcknowledgmentActorBinding.IsSufficient(actor))
        {
            return Deny(
                currentDecision,
                SignedCapabilityGrantReasonCodes.ActorUnresolved,
                "Signed capability grants require a known, authenticated actor to bind the grant to.");
        }

        TokenReadResult tokenRead = await ReadTokenAsync(httpContext, cancellationToken).ConfigureAwait(false);
        if (tokenRead.ReasonCode is not null)
        {
            return Deny(currentDecision, tokenRead.ReasonCode, tokenRead.Message!);
        }

        CapabilityGrantTokenDecodeResult decoded = CapabilityGrantToken.Decode(
            tokenRead.Token,
            options.ProofPayloadOptions,
            options.MaxTokenLength);

        if (!decoded.Succeeded)
        {
            return Deny(
                currentDecision,
                SignedCapabilityGrantReasonCodes.Malformed,
                "The capability grant token could not be decoded.",
                decoded.FailureCode);
        }

        SignedGovernanceArtifact<CapabilityGrant> signedGrant = decoded.SignedGrant!;

        CapabilityGrantRequestBinding binding = await ResolveBindingAsync(httpContext, descriptor, cancellationToken)
            .ConfigureAwait(false);

        if (!binding.IsAvailable)
        {
            return Deny(
                currentDecision,
                SignedCapabilityGrantReasonCodes.BindingUnavailable,
                binding.FailureMessage ?? "The request could not be bound to a capability grant.");
        }

        // The core validator checks a resource binding only when one is expected. When the request has no bound resource,
        // a grant issued for a specific resource is still the wrong grant, so it is denied here before any use is consumed.
        if (binding.Value is null && signedGrant.Artifact.ResourceBinding is not null)
        {
            return Deny(
                currentDecision,
                SignedCapabilityGrantReasonCodes.ResourceMismatch,
                "The capability grant is bound to a resource, but the request has no bound resource.");
        }

        CapabilityGrantValidationResult result;

        try
        {
            result = await CapabilityGrantValidator.ValidateAsync(
                signedGrant,
                CreateValidationOptions(actor, descriptor, currentDecision, binding.Value),
                serviceProvider.GetService<IGovernanceSignatureVerificationService>(),
                serviceProvider.GetService<ICapabilityGrantUseStore>(),
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // A verifier or use store that throws has not established that the grant is valid. Denying keeps an
            // infrastructure fault from becoming an exception the host might handle permissively.
            LogValidationFailed(logger, descriptor.OperationName, exception);

            return Deny(
                currentDecision,
                SignedCapabilityGrantReasonCodes.Failed,
                "Capability grant validation failed unexpectedly.");
        }

        GovernanceDecision? rejection = MapRejection(currentDecision, result);
        if (rejection is not null)
        {
            LogGrantDenied(logger, rejection.ReasonCodes[0], result.FailureCode ?? "none", null);
            return rejection;
        }

        httpContext.Features.Set<IEndpointCapabilityGrantFeature>(new EndpointCapabilityGrantFeature(result.TokenId));
        return currentDecision;
    }

    /// <summary>
    /// Returns the denial for a rejected grant, or <see langword="null" /> when the grant was accepted.
    /// </summary>
    /// <remarks>
    /// The core result's <see cref="CapabilityGrantValidationResult.Action" /> is deliberately not mapped to an outcome. At
    /// an execution boundary a deferral, an escalation, or an acknowledgment challenge would let a request continue toward
    /// execution on a grant that did not validate, so every rejection is a denial.
    /// </remarks>
    internal static GovernanceDecision? MapRejection(
        GovernanceDecision currentDecision,
        CapabilityGrantValidationResult result)
    {
        return result.IsValid && result.ShouldAllow
            ? null
            : CreateDenial(
                currentDecision,
                SignedCapabilityGrantReasonCodes.ForCategory(result.Category),
                $"The capability grant was rejected: {result.Category}.");
    }

    private CapabilityGrantValidationOptions CreateValidationOptions(
        IGovernanceActorContext actor,
        EndpointGovernanceDescriptor descriptor,
        GovernanceDecision currentDecision,
        string? resourceBinding)
    {
        return CapabilityGrantValidationOptions.CreateBoundExecutionBoundary(
            CapabilityGrantBindingExpectations.Create(actor.ActorId, descriptor.OperationName),
            issuer: options.Issuer,
            audience: options.Audience,
            scopes: descriptor.CapabilityScopes,
            validationUtc: timeProvider.GetUtcNow(),
            policyVersion: options.BindPolicy ? currentDecision.PolicyVersion : null,
            policyHash: options.BindPolicy ? currentDecision.PolicyHash : null,
            resourceBinding: resourceBinding,

            // An acknowledgment-required request is answered with a challenge and never executes, and the acknowledged
            // retry is validated again. Consuming a use here would leave a single-use grant spent before the retry. Every
            // other outcome can lead to execution from this evaluation, including a deferral or escalation the host later
            // approves, so it consumes a use.
            requireUseCheck: !currentDecision.RequiresAcknowledgment,
            maxUseCount: options.MaxUseCount,
            allowedClockSkew: options.AllowedClockSkew,
            expectedProofKeyId: options.ExpectedProofKeyId,
            expectedProofKeyVersion: options.ExpectedProofKeyVersion,
            requiredProofProvider: options.RequiredProofProvider,
            requiredProofHashAlgorithm: options.RequiredProofHashAlgorithm,
            proofPayloadOptions: options.ProofPayloadOptions);
    }

    private async ValueTask<CapabilityGrantRequestBinding> ResolveBindingAsync(
        HttpContext httpContext,
        EndpointGovernanceDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        ICapabilityGrantRequestBindingProvider? bindingProvider =
            serviceProvider.GetService<ICapabilityGrantRequestBindingProvider>();

        return bindingProvider is null
            ? CapabilityGrantRequestBinding.Unavailable("No capability grant request-binding provider is registered.")
            : await bindingProvider.ResolveAsync(httpContext, descriptor, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<TokenReadResult> ReadTokenAsync(HttpContext httpContext, CancellationToken cancellationToken)
    {
        HttpRequest request = httpContext.Request;

        if (!string.IsNullOrWhiteSpace(options.TokenHeaderName)
            && request.Headers.TryGetValue(options.TokenHeaderName, out StringValues headerValues)
            && headerValues.Count > 0)
        {
            return ReadSingle(headerValues);
        }

        if (string.IsNullOrWhiteSpace(options.TokenFormFieldName) || !request.HasFormContentType)
        {
            return TokenReadResult.Missing;
        }

        IFormCollection form;

        try
        {
            form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException or BadHttpRequestException)
        {
            return TokenReadResult.Unreadable;
        }

        return form.TryGetValue(options.TokenFormFieldName, out StringValues formValues) && formValues.Count > 0
            ? ReadSingle(formValues)
            : TokenReadResult.Missing;
    }

    private static TokenReadResult ReadSingle(StringValues values)
    {
        // Two tokens leave it ambiguous which one the request means to present, so neither is chosen.
        return values.Count > 1
            ? TokenReadResult.Ambiguous
            : string.IsNullOrWhiteSpace(values[0])
            ? TokenReadResult.Missing
            : new TokenReadResult(values[0], ReasonCode: null, Message: null);
    }

    private GovernanceDecision Deny(
        GovernanceDecision currentDecision,
        string reasonCode,
        string message,
        string? detailCode = null)
    {
        LogGrantDenied(logger, reasonCode, detailCode ?? "none", null);
        return CreateDenial(currentDecision, reasonCode, message);
    }

    private static GovernanceDecision CreateDenial(GovernanceDecision currentDecision, string reasonCode, string message)
    {
        return GovernanceDecision.Deny(
            reasonCode,
            message,
            correlationId: currentDecision.CorrelationId,
            traceId: currentDecision.TraceId,
            policyVersion: currentDecision.PolicyVersion,
            policyHash: currentDecision.PolicyHash);
    }

    private readonly record struct TokenReadResult(string? Token, string? ReasonCode, string? Message)
    {
        public static TokenReadResult Missing { get; } = new(
            null,
            SignedCapabilityGrantReasonCodes.Missing,
            "No capability grant token was presented.");

        public static TokenReadResult Ambiguous { get; } = new(
            null,
            SignedCapabilityGrantReasonCodes.Malformed,
            "More than one capability grant token was presented.");

        public static TokenReadResult Unreadable { get; } = new(
            null,
            SignedCapabilityGrantReasonCodes.Malformed,
            "The request form carrying the capability grant token could not be read.");
    }
}
