using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Core.Signing;
using AsiBackbone.Storage.InMemory.Outbox;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace AsiBackbone.Testing;

/// <summary>
/// Marker type for the AsiBackbone.Testing assembly.
/// </summary>
public sealed class AsiBackboneTestingAssemblyMarker
{
    private AsiBackboneTestingAssemblyMarker()
    {
    }
}

/// <summary>
/// Configures deterministic services for exercising AsiBackbone-governed endpoints in tests.
/// </summary>
public sealed class AsiBackboneTestHarnessOptions
{
    private readonly Dictionary<string, GovernanceDecision> policyResults = new(StringComparer.Ordinal);

    /// <summary>
    /// Gets the default policy decision used when no explicit policy result is configured.
    /// </summary>
    public GovernanceDecision DefaultPolicyDecision { get; private set; } = GovernanceDecision.Allow(policyVersion: "test-harness");

    /// <summary>
    /// Gets the default capability-grant validation decision used by the test harness.
    /// </summary>
    public GovernanceDecision DefaultCapabilityGrantDecision { get; private set; } = GovernanceDecision.Allow(policyVersion: "test-harness");

    /// <summary>
    /// Gets a value indicating whether every policy marker must have an explicit configured result.
    /// </summary>
    public bool RequireExplicitPolicyResults { get; private set; }

    /// <summary>
    /// Gets or sets a value indicating whether the harness registers <see cref="AsiBackboneTestAuditSink" /> as the host audit sink.
    /// </summary>
    public bool RegisterInMemoryAuditSink { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the harness registers the non-durable in-memory governance outbox store.
    /// </summary>
    public bool RegisterInMemoryOutboxStore { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the harness registers a deterministic no-signature signing service.
    /// </summary>
    public bool RegisterDeterministicSigningService { get; set; } = true;

    /// <summary>
    /// Gets the configured deterministic policy results.
    /// </summary>
    public IReadOnlyDictionary<string, GovernanceDecision> PolicyResults => policyResults;

    /// <summary>
    /// Uses an allow decision as the default policy result.
    /// </summary>
    public AsiBackboneTestHarnessOptions AllowAllPolicies()
    {
        DefaultPolicyDecision = GovernanceDecision.Allow(policyVersion: "test-harness");
        RequireExplicitPolicyResults = false;
        return this;
    }

    /// <summary>
    /// Uses a deny decision as the default policy result.
    /// </summary>
    public AsiBackboneTestHarnessOptions DenyAllPolicies(
        string code = "test_harness.policy_denied",
        string message = "Denied by the AsiBackbone test harness.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        DefaultPolicyDecision = GovernanceDecision.Deny(code, message, policyVersion: "test-harness");
        RequireExplicitPolicyResults = false;
        return this;
    }

    /// <summary>
    /// Uses an allow decision as the default capability-grant validation result.
    /// </summary>
    public AsiBackboneTestHarnessOptions AllowCapabilityGrants()
    {
        DefaultCapabilityGrantDecision = GovernanceDecision.Allow(policyVersion: "test-harness");
        return this;
    }

    /// <summary>
    /// Uses a deny decision as the default capability-grant validation result.
    /// </summary>
    public AsiBackboneTestHarnessOptions DenyCapabilityGrants(
        string code = "test_harness.capability_denied",
        string message = "Capability grant denied by the AsiBackbone test harness.")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        DefaultCapabilityGrantDecision = GovernanceDecision.Deny(code, message, policyVersion: "test-harness");
        return this;
    }

    /// <summary>
    /// Sets a deterministic result for a policy marker type.
    /// </summary>
    public AsiBackboneTestHarnessOptions SetPolicyResult<TPolicy>(GovernanceDecision decision)
    {
        return SetPolicyResult(typeof(TPolicy), decision);
    }

    /// <summary>
    /// Sets a deterministic result for a policy marker type.
    /// </summary>
    /// <remarks>
    /// This is a harness-only capability. It simulates a host-supplied decision policy that reads the
    /// <c>endpoint.policy_types</c> metadata entry and varies its outcome by policy type. AsiBackbone itself does not
    /// select constraints or decisions from an endpoint's policy type, so configuring different results for two policy
    /// types asserts against host policy the application must actually implement. Without that host decision policy,
    /// both endpoints evaluate identically at runtime.
    /// </remarks>
    /// <param name="policyType">The policy marker type to configure a result for.</param>
    /// <param name="decision">The decision the harness returns for endpoints marked with that type.</param>
    /// <returns>The same options instance so calls can be chained.</returns>
    public AsiBackboneTestHarnessOptions SetPolicyResult(Type policyType, GovernanceDecision decision)
    {
        ArgumentNullException.ThrowIfNull(policyType);
        ArgumentNullException.ThrowIfNull(decision);

        policyResults[policyType.FullName ?? policyType.Name] = decision;
        policyResults[policyType.Name] = decision;
        return this;
    }

    /// <summary>
    /// Requires explicit deterministic policy results and configures the supplied marker type.
    /// </summary>
    public AsiBackboneTestHarnessOptions RequirePolicyResult<TPolicy>(GovernanceDecision decision)
    {
        return RequirePolicyResult(typeof(TPolicy), decision);
    }

    /// <summary>
    /// Requires explicit deterministic policy results and configures the supplied marker type.
    /// </summary>
    public AsiBackboneTestHarnessOptions RequirePolicyResult(Type policyType, GovernanceDecision decision)
    {
        RequireExplicitPolicyResults = true;
        return SetPolicyResult(policyType, decision);
    }

    internal bool TryGetPolicyDecision(string policyToken, out GovernanceDecision decision)
    {
        return policyResults.TryGetValue(policyToken, out decision!);
    }

    internal void Validate()
    {
        ArgumentNullException.ThrowIfNull(DefaultPolicyDecision);
        ArgumentNullException.ThrowIfNull(DefaultCapabilityGrantDecision);
    }
}

/// <summary>
/// Provides service registration helpers for the test-only AsiBackbone harness.
/// </summary>
public static class AsiBackboneTestHarnessServiceCollectionExtensions
{
    /// <summary>
    /// Adds the AsiBackbone test harness using default allow-all policy and capability behavior.
    /// </summary>
    public static IServiceCollection AddAsiBackboneTestHarness(this IServiceCollection services)
    {
        return services.AddAsiBackboneTestHarness(_ => { });
    }

    /// <summary>
    /// Adds the AsiBackbone test harness using deterministic test-only substitutions.
    /// </summary>
    public static IServiceCollection AddAsiBackboneTestHarness(
        this IServiceCollection services,
        Action<AsiBackboneTestHarnessOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AsiBackboneTestHarnessOptions options = new();
        configure(options);
        options.Validate();

        _ = services.AddSingleton(options);
        _ = services.AddSingleton<AsiBackboneTestAuditSink>();
        _ = services.AddSingleton<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>, AsiBackboneTestHarnessPolicyEvaluator>();
        _ = services.AddSingleton<IAsiBackboneEndpointCapabilityGrantValidator, AsiBackboneTestHarnessEndpointCapabilityGrantValidator>();

        if (options.RegisterInMemoryAuditSink)
        {
            _ = services.AddSingleton<IAsiBackboneAuditSink>(static serviceProvider =>
                serviceProvider.GetRequiredService<AsiBackboneTestAuditSink>());
        }

        if (options.RegisterInMemoryOutboxStore)
        {
            services.TryAddSingleton<InMemoryGovernanceOutboxStore>();
            services.TryAddSingleton<IAsiBackboneGovernanceOutboxStore>(static serviceProvider =>
                serviceProvider.GetRequiredService<InMemoryGovernanceOutboxStore>());
        }

        if (options.RegisterDeterministicSigningService)
        {
            services.TryAddSingleton<IAsiBackboneSigningService, AsiBackboneTestSigningService>();
        }

        return services;
    }
}

/// <summary>
/// Deterministic policy evaluator used by the test harness.
/// </summary>
/// <remarks>
/// <para>
/// Selecting a decision per policy token simulates a host-supplied decision policy that reads the
/// <c>endpoint.policy_types</c> metadata entry and varies its outcome. It does not mirror framework behavior: the
/// framework never resolves an endpoint's policy type or selects constraints from it, and the registered evaluator
/// evaluates every registered constraint on every governed endpoint. A test that configures different results for
/// two policy tokens is asserting against host policy it must actually implement, not against something AsiBackbone
/// provides. Without such a host decision policy, both endpoints evaluate identically in production.
/// </para>
/// <para>
/// Initializes a new instance of the <see cref="AsiBackboneTestHarnessPolicyEvaluator" /> class.
/// </para>
/// </remarks>
public sealed class AsiBackboneTestHarnessPolicyEvaluator(AsiBackboneTestHarnessOptions options) : IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>
{
    private const string PolicyTypesMetadataKey = "endpoint.policy_types";
    private readonly AsiBackboneTestHarnessOptions options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public ValueTask<GovernanceDecision> EvaluateAsync(
        AsiBackboneConstraintEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (string policyToken in ResolvePolicyTokens(context))
        {
            if (options.TryGetPolicyDecision(policyToken, out GovernanceDecision? decision))
            {
                return ValueTask.FromResult(AsiBackboneTestHarnessDecisionFactory.WithTelemetry(decision, context));
            }
        }

        return options.RequireExplicitPolicyResults
            ? ValueTask.FromResult(GovernanceDecision.Deny(
                "test_harness.policy_result.missing",
                "The AsiBackbone test harness requires an explicit policy result for this endpoint policy marker.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash))
            : ValueTask.FromResult(AsiBackboneTestHarnessDecisionFactory.WithTelemetry(options.DefaultPolicyDecision, context));
    }

    private static IEnumerable<string> ResolvePolicyTokens(AsiBackboneConstraintEvaluationContext context)
    {
        if (!context.Metadata.TryGetValue(PolicyTypesMetadataKey, out string? policyTypesValue)
            || string.IsNullOrWhiteSpace(policyTypesValue))
        {
            yield break;
        }

        foreach (string policyToken in policyTypesValue.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrWhiteSpace(policyToken))
            {
                yield return policyToken;
            }
        }
    }
}

/// <summary>
/// Deterministic capability-grant validator used by the test harness.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="AsiBackboneTestHarnessEndpointCapabilityGrantValidator" /> class.
/// </remarks>
public sealed class AsiBackboneTestHarnessEndpointCapabilityGrantValidator(AsiBackboneTestHarnessOptions options) : IAsiBackboneEndpointCapabilityGrantValidator
{
    private readonly AsiBackboneTestHarnessOptions options = options ?? throw new ArgumentNullException(nameof(options));

    /// <inheritdoc />
    public ValueTask<GovernanceDecision> ValidateAsync(
        HttpContext httpContext,
        AsiBackboneEndpointGovernanceDescriptor descriptor,
        GovernanceDecision currentDecision,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(descriptor);
        ArgumentNullException.ThrowIfNull(currentDecision);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(AsiBackboneTestHarnessDecisionFactory.WithTelemetry(
            options.DefaultCapabilityGrantDecision,
            currentDecision.CorrelationId,
            currentDecision.TraceId,
            currentDecision.PolicyVersion,
            currentDecision.PolicyHash));
    }
}

/// <summary>
/// In-memory audit sink with inspection helpers for tests.
/// </summary>
public sealed class AsiBackboneTestAuditSink : IAsiBackboneAuditSink
{
    private readonly Lock gate = new();
    private readonly List<IAsiBackboneAuditResidue> entries = [];

    /// <summary>
    /// Gets a snapshot of audit residue captured by the test sink.
    /// </summary>
    public IReadOnlyList<IAsiBackboneAuditResidue> Entries
    {
        get
        {
            lock (gate)
            {
                return entries.ToArray();
            }
        }
    }

    /// <summary>
    /// Clears captured audit residue.
    /// </summary>
    public void Clear()
    {
        lock (gate)
        {
            entries.Clear();
        }
    }

    /// <inheritdoc />
    public ValueTask WriteAsync(
        IAsiBackboneAuditResidue residue,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(residue);
        cancellationToken.ThrowIfCancellationRequested();

        lock (gate)
        {
            entries.Add(residue);
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Deterministic no-signature signing service for tests.
/// </summary>
public sealed class AsiBackboneTestSigningService : IAsiBackboneSigningService
{
    /// <inheritdoc />
    public ValueTask<SigningResult> SignAsync(
        SigningRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        return ValueTask.FromResult(SigningResult.NoSignature);
    }
}

internal static class AsiBackboneTestHarnessDecisionFactory
{
    internal static GovernanceDecision WithTelemetry(
        GovernanceDecision decision,
        AsiBackboneConstraintEvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return WithTelemetry(
            decision,
            context.CorrelationId,
            traceId: null,
            context.PolicyVersion,
            context.PolicyHash);
    }

    internal static GovernanceDecision WithTelemetry(
        GovernanceDecision decision,
        string? correlationId,
        string? traceId,
        string? policyVersion,
        string? policyHash)
    {
        ArgumentNullException.ThrowIfNull(decision);

        string? resolvedCorrelationId = decision.CorrelationId ?? correlationId;
        string? resolvedTraceId = decision.TraceId ?? traceId;
        string? resolvedPolicyVersion = decision.PolicyVersion ?? policyVersion;
        string? resolvedPolicyHash = decision.PolicyHash ?? policyHash;

        return decision.Outcome switch
        {
            GovernanceDecisionOutcome.Allowed => GovernanceDecision.Allow(
                resolvedCorrelationId,
                resolvedTraceId,
                resolvedPolicyVersion,
                resolvedPolicyHash),
            GovernanceDecisionOutcome.Warning => GovernanceDecision.Warning(
                decision.Reasons,
                resolvedCorrelationId,
                resolvedTraceId,
                resolvedPolicyVersion,
                resolvedPolicyHash),
            GovernanceDecisionOutcome.Denied => GovernanceDecision.Deny(
                decision.Reasons,
                resolvedCorrelationId,
                resolvedTraceId,
                resolvedPolicyVersion,
                resolvedPolicyHash),
            GovernanceDecisionOutcome.Deferred => GovernanceDecision.Defer(
                FirstReasonCode(decision, "test_harness.deferred"),
                FirstReasonMessage(decision, "Deferred by the AsiBackbone test harness."),
                resolvedCorrelationId,
                resolvedTraceId,
                resolvedPolicyVersion,
                resolvedPolicyHash),
            GovernanceDecisionOutcome.AcknowledgmentRequired => GovernanceDecision.RequireAcknowledgment(
                FirstReasonCode(decision, "test_harness.acknowledgment_required"),
                FirstReasonMessage(decision, "Acknowledgment required by the AsiBackbone test harness."),
                resolvedCorrelationId,
                resolvedTraceId,
                resolvedPolicyVersion,
                resolvedPolicyHash),
            GovernanceDecisionOutcome.EscalationRecommended => GovernanceDecision.Escalate(
                FirstReasonCode(decision, "test_harness.escalation_recommended"),
                FirstReasonMessage(decision, "Escalation recommended by the AsiBackbone test harness."),
                resolvedCorrelationId,
                resolvedTraceId,
                resolvedPolicyVersion,
                resolvedPolicyHash),
            _ => decision
        };
    }

    private static string FirstReasonCode(GovernanceDecision decision, string fallback)
    {
        return decision.Reasons.Count == 0
            ? fallback
            : decision.Reasons[0].Code;
    }

    private static string FirstReasonMessage(GovernanceDecision decision, string fallback)
    {
        return decision.Reasons.Count == 0
            ? fallback
            : decision.Reasons[0].Message;
    }
}
