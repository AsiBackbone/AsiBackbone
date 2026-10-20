using System.Security.Claims;
using System.Security.Cryptography;
using AsiBackbone.AspNetCore.CapabilityGrants;
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.CapabilityGrants;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Signing;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Xunit;

namespace AsiBackbone.AspNetCore.Tests.CapabilityGrants;

/// <summary>
/// Builds a host wired for signed capability grant validation, and issues grants against it.
/// </summary>
internal sealed class SignedCapabilityGrantTestHost : IDisposable
{
    public const string Issuer = "https://issuer.test";
    public const string Audience = "orders-api";
    public const string ActorId = "user-123";
    public const string OperationName = "orders.approve";
    public const string Scope = "orders.approve";
    public const string PolicyVersion = "policy-v1";
    public const string PolicyHash = "policy-hash-1";

    public static readonly DateTimeOffset Now = new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly ServiceProvider services;

    public SignedCapabilityGrantTestHost(
        Func<GovernanceEvaluationContext, GovernanceDecision>? evaluate = null,
        bool registerVerifier = true,
        bool registerUseStore = true,
        bool registerBinding = true,
        Action<SignedCapabilityGrantOptions>? configure = null,
        Action<IServiceCollection>? customize = null)
    {
        ServiceCollection collection = new();
        _ = collection.Configure<EndpointGovernanceOptions>(options =>
        {
            options.PolicyVersion = PolicyVersion;
            options.PolicyHash = PolicyHash;
        });
        _ = collection.AddAsiBackboneAspNetCore();
        _ = collection.AddAsiBackboneSignedCapabilityGrants(options =>
        {
            options.Issuer = Issuer;
            options.Audience = Audience;
            configure?.Invoke(options);
        });

        if (registerBinding)
        {
            _ = collection.AddAsiBackboneFormFieldCapabilityGrantBinding();
        }

        if (registerVerifier)
        {
            _ = collection.AddSingleton<IGovernanceSignatureVerificationService>(Signer);
        }

        if (registerUseStore)
        {
            _ = collection.AddSingleton<ICapabilityGrantUseStore>(UseStore);
        }

        _ = collection.AddSingleton<TimeProvider>(Clock);
        _ = collection.AddSingleton<IDecisionReceiptSink>(Sink);
        _ = collection.AddSingleton<IGovernancePolicyEvaluator<GovernanceEvaluationContext>>(
            new DelegatePolicyEvaluator(evaluate ?? (context => GovernanceDecision.Allow(
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash))));
        customize?.Invoke(collection);

        services = collection.BuildServiceProvider(validateScopes: true);
    }

    public HmacSignatureService Signer { get; } = new();

    public CountingUseStore UseStore { get; } = new();

    public MutableTimeProvider Clock { get; } = new(Now);

    public CapturingReceiptSink Sink { get; } = new();

    public IServiceProvider Services => services;

    public static IReadOnlyDictionary<string, StringValues> DefaultFields { get; } =
        new Dictionary<string, StringValues>(StringComparer.Ordinal)
        {
            ["orderId"] = "order-42",
            ["amount"] = "100.00"
        };

    public static CapabilityGrant CreateGrant(
        string? tokenId = null,
        string issuer = Issuer,
        string audience = Audience,
        string? subjectId = ActorId,
        string? operationName = OperationName,
        string[]? scopes = null,
        string? policyVersion = PolicyVersion,
        string? policyHash = PolicyHash,
        IReadOnlyDictionary<string, StringValues>? boundFields = null,
        string? resourceBinding = null,
        bool noResourceBinding = false,
        DateTimeOffset? notBeforeUtc = null,
        DateTimeOffset? expiresUtc = null,
        int? maxUseCount = 1)
    {
        return CapabilityGrant.Create(
            tokenId: tokenId ?? Guid.NewGuid().ToString("N"),
            issuer: issuer,
            audience: audience,
            scopes: scopes ?? [Scope],
            issuedUtc: Now.AddMinutes(-1),
            expiresUtc: expiresUtc ?? Now.AddMinutes(5),
            notBeforeUtc: notBeforeUtc ?? Now.AddMinutes(-1),
            subjectId: subjectId,
            operationName: operationName,
            policyVersion: policyVersion,
            policyHash: policyHash,
            resourceBinding: noResourceBinding
                ? null
                : resourceBinding ?? FormFieldCapabilityGrantRequestBindingProvider.CreateBinding(boundFields ?? DefaultFields),
            maxUseCount: maxUseCount);
    }

    public async Task<string> IssueTokenAsync(CapabilityGrant? grant = null)
    {
        SignedGovernanceArtifact<CapabilityGrant> signed = await GovernanceArtifactSigner.SignCapabilityGrantAsync(
            grant ?? CreateGrant(),
            Signer,
            cancellationToken: TestContext.Current.CancellationToken);

        return CapabilityGrantToken.Encode(signed);
    }

    public static Endpoint CreateEndpoint(params object[] extraMetadata)
    {
        object[] metadata =
        [
            new GovernancePolicyAttribute(typeof(SamplePolicy)),
            new RequireCapabilityGrantAttribute(Scope),
            new EmitGovernanceAuditAttribute(),
            .. extraMetadata
        ];

        return new Endpoint(static _ => Task.CompletedTask, new EndpointMetadataCollection(metadata), OperationName);
    }

    public static DefaultHttpContext CreateHttpContext(
        IServiceProvider requestServices,
        string? token = null,
        IReadOnlyDictionary<string, StringValues>? fields = null,
        bool authenticated = true,
        string actorId = ActorId)
    {
        var httpContext = new DefaultHttpContext
        {
            RequestServices = requestServices,
            TraceIdentifier = "signed-capability-grant-test"
        };
        httpContext.Response.Body = new MemoryStream();
        httpContext.Request.Method = HttpMethods.Post;
        httpContext.Request.ContentType = "application/x-www-form-urlencoded";
        httpContext.Request.Form = new FormCollection(new Dictionary<string, StringValues>(
            fields ?? DefaultFields,
            StringComparer.OrdinalIgnoreCase));

        if (token is not null)
        {
            httpContext.Request.Headers[SignedCapabilityGrantOptions.DefaultTokenHeaderName] = token;
        }

        if (authenticated)
        {
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, actorId)],
                authenticationType: "test"));
        }

        requestServices.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;
        return httpContext;
    }

    public void Dispose()
    {
        services.Dispose();
    }

    internal sealed class SamplePolicy
    {
    }

    internal sealed class DelegatePolicyEvaluator(Func<GovernanceEvaluationContext, GovernanceDecision> evaluate)
        : IGovernancePolicyEvaluator<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            return ValueTask.FromResult(evaluate(context));
        }
    }

    internal sealed class MutableTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = utcNow;

        public override DateTimeOffset GetUtcNow()
        {
            return UtcNow;
        }
    }

    internal sealed class CapturingReceiptSink : IDecisionReceiptSink
    {
        public List<IDecisionReceipt> Receipts { get; } = [];

        public ValueTask WriteAsync(IDecisionReceipt receipt, CancellationToken cancellationToken = default)
        {
            Receipts.Add(receipt);
            return ValueTask.CompletedTask;
        }
    }

    /// <summary>
    /// Counts grant uses per token, or returns a forced state when one is set.
    /// </summary>
    internal sealed class CountingUseStore : ICapabilityGrantUseStore
    {
        private readonly Dictionary<string, int> uses = new(StringComparer.Ordinal);

        public GrantUseState? ForcedState { get; set; }

        public bool Throw { get; set; }

        public int TotalUses => uses.Values.Sum();

        public ValueTask<CapabilityGrantUseResult> TryConsumeAsync(
            CapabilityGrant grant,
            int maxUseCount,
            DateTimeOffset usedUtc,
            CancellationToken cancellationToken = default)
        {
            if (Throw)
            {
                throw new InvalidOperationException("The use store is offline.");
            }

            if (ForcedState is GrantUseState state)
            {
                return ValueTask.FromResult(CreateResult(grant, state));
            }

            int used = uses.GetValueOrDefault(grant.TokenId);
            if (used >= maxUseCount)
            {
                return ValueTask.FromResult(CreateResult(grant, GrantUseState.UseLimitExceeded));
            }

            uses[grant.TokenId] = used + 1;
            return ValueTask.FromResult(CreateResult(grant, GrantUseState.Accepted));
        }

        private static CapabilityGrantUseResult CreateResult(CapabilityGrant grant, GrantUseState state)
        {
            ArgumentNullException.ThrowIfNull(grant);

            return state switch
            {
                GrantUseState.Accepted => CapabilityGrantUseResult.Accepted(1),
                GrantUseState.UseLimitExceeded => CapabilityGrantUseResult.UseLimitExceeded(1),
                GrantUseState.Stopped => CapabilityGrantUseResult.Stopped(),
                GrantUseState.Cancelled => CapabilityGrantUseResult.Cancelled(),
                GrantUseState.Unavailable => CapabilityGrantUseResult.Unavailable(),

                // No factory produces an unspecified state; a misbehaving store is simulated through the private constructor.
                GrantUseState.Unspecified => (CapabilityGrantUseResult)typeof(CapabilityGrantUseResult)
                    .GetConstructors(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)[0]
                    .Invoke([GrantUseState.Unspecified, 0, null, null]),
                _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Unsupported state.")
            };
        }
    }

    /// <summary>
    /// Signs and verifies with a keyed HMAC over the exact signature input, or returns a forced verification failure.
    /// </summary>
    internal sealed class HmacSignatureService : IGovernanceSigningService, IGovernanceSignatureVerificationService
    {
        private static readonly byte[] Key = [0x41, 0x53, 0x49, 0x42, 0x2D, 0x65, 0x70, 0x2D, 0x74, 0x65, 0x73, 0x74, 0x2D, 0x6B, 0x65, 0x79];

        public SignatureVerificationCategory? ForcedFailure { get; set; }

        public ValueTask<SigningResult> SignAsync(SigningRequest request, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            return ValueTask.FromResult(SigningResult.FromMetadata(SigningMetadata.Create(
                signingHash: request.SigningHash,
                hashAlgorithm: request.HashAlgorithm,
                signature: Convert.ToBase64String(HMACSHA256.HashData(Key, request.SignatureInput.Span)),
                signatureAlgorithm: "HMAC-SHA256-TEST",
                keyId: "test-key",
                keyVersion: "v1",
                provider: "test-provider",
                signedUtc: Now,
                metadata: request.Metadata)));
        }

        public ValueTask<SignatureVerificationResult> VerifyAsync(
            SignatureVerificationRequest request,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (ForcedFailure is SignatureVerificationCategory category)
            {
                return ValueTask.FromResult(SignatureVerificationResult.Failed("signature.forced", category, "Forced failure."));
            }

            byte[] expected = HMACSHA256.HashData(Key, request.SignatureInput.Span);
            byte[] actual = Convert.FromBase64String(request.SigningMetadata.Signature ?? string.Empty);

            return ValueTask.FromResult(CryptographicOperations.FixedTimeEquals(expected, actual)
                ? SignatureVerificationResult.Verified()
                : SignatureVerificationResult.Failed(
                    "signature.invalid",
                    SignatureVerificationCategory.InvalidSignature,
                    "The keyed signature does not match the signature input."));
        }
    }
}
