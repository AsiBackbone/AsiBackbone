using System.Globalization;
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Core.Results;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsiBackbone.Benchmarks.BenchmarkDotNet;

/// <summary>
/// BenchmarkDotNet baseline for allocation-sensitive governance hot paths.
/// </summary>
[MemoryDiagnoser]
[RankColumn]
public class AsiBackboneHotPathBenchmarks
{
    private static readonly DateTimeOffset BenchmarkDrainUtc = new(2026, 6, 30, 18, 0, 0, TimeSpan.Zero);

    private readonly BdnBenchmarkPolicyContext zeroConstraintsContext = CreateContext("policy.zero_constraints");
    private readonly BdnBenchmarkPolicyContext allAllowContext = CreateContext("policy.all_allow_8");
    private readonly BdnBenchmarkPolicyContext mixedContext = CreateContext("policy.warning_and_denial_full");
    private readonly BdnBenchmarkPolicyContext firstDenialContext = CreateContext("policy.first_denial_short_circuit");
    private readonly BdnBenchmarkPolicyContext acknowledgmentContext = CreateContext("policy.acknowledgment_required");
    private readonly BdnBenchmarkPolicyContext escalationContext = CreateContext("policy.escalation_recommended");
    private readonly BdnBenchmarkPolicyContext exceptionAsDenialContext = CreateContext("policy.constraint_exception_as_denial");

    private readonly OperationReason operationReasonOne = OperationReason.Create(
        "operation.denied",
        "The benchmark operation was denied.");

    private readonly OperationReason[] operationReasonsMany =
    [
        OperationReason.Create("operation.first_denied", "The first benchmark operation reason denied execution."),
        OperationReason.Create("operation.second_denied", "The second benchmark operation reason denied execution."),
        OperationReason.Create("operation.third_denied", "The third benchmark operation reason denied execution.")
    ];

    private readonly IReadOnlyDictionary<string, string> auditBuilderMetadataOne = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["benchmark"] = "audit_residue.builder_one_metadata"
    };

    private readonly IReadOnlyDictionary<string, string> auditBuilderMetadataMany = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["benchmark"] = "audit_residue.builder_many_metadata",
        ["risk"] = "routine",
        ["policy.scope"] = "benchmark"
    };

    private readonly IAsiBackbonePolicyEvaluator<BdnBenchmarkPolicyContext> zeroConstraintsEvaluator =
        DefaultAsiBackbonePolicyEvaluator.CreateBuilder<BdnBenchmarkPolicyContext>()
            .Build();

    private readonly IAsiBackbonePolicyEvaluator<BdnBenchmarkPolicyContext> allAllowEvaluator =
        DefaultAsiBackbonePolicyEvaluator.CreateBuilder<BdnBenchmarkPolicyContext>()
            .AddConstraints(CreateStaticConstraints(8, ConstraintEvaluationResult.Allow()))
            .Build();

    private readonly IAsiBackbonePolicyEvaluator<BdnBenchmarkPolicyContext> mixedEvaluator =
        DefaultAsiBackbonePolicyEvaluator.CreateBuilder<BdnBenchmarkPolicyContext>()
            .AddConstraints(CreateMixedConstraints())
            .Build();

    private readonly IAsiBackbonePolicyEvaluator<BdnBenchmarkPolicyContext> firstDenialEvaluator =
        DefaultAsiBackbonePolicyEvaluator.CreateBuilder<BdnBenchmarkPolicyContext>()
            .AddConstraints(CreateMixedConstraints())
            .WithOptions(new AsiBackbonePolicyEvaluatorOptions { ShortCircuitOnFirstDenial = true })
            .Build();

    private readonly IAsiBackbonePolicyEvaluator<BdnBenchmarkPolicyContext> acknowledgmentEvaluator =
        DefaultAsiBackbonePolicyEvaluator.CreateBuilder<BdnBenchmarkPolicyContext>()
            .AddConstraints(CreateStaticConstraints(4, ConstraintEvaluationResult.Allow()))
            .WithDecisionPolicy(new BdnRequireAcknowledgmentPolicy())
            .Build();

    private readonly IAsiBackbonePolicyEvaluator<BdnBenchmarkPolicyContext> escalationEvaluator =
        DefaultAsiBackbonePolicyEvaluator.CreateBuilder<BdnBenchmarkPolicyContext>()
            .AddConstraints(CreateStaticConstraints(4, ConstraintEvaluationResult.Allow()))
            .WithDecisionPolicy(new BdnEscalatePolicy())
            .Build();

    private readonly IAsiBackbonePolicyEvaluator<BdnBenchmarkPolicyContext> exceptionAsDenialEvaluator =
        DefaultAsiBackbonePolicyEvaluator.CreateBuilder<BdnBenchmarkPolicyContext>()
            .AddConstraint(new BdnThrowingConstraint(new InvalidOperationException("Benchmark constraint failure.")))
            .WithOptions(new AsiBackbonePolicyEvaluatorOptions { TreatConstraintExceptionAsDenial = true })
            .Build();

    private readonly EndpointGovernanceHarness endpointAllow = new("endpoint_governance.policy_allow", EndpointDecisionKind.Allow);
    private readonly EndpointGovernanceHarness endpointWarning = new("endpoint_governance.policy_warning", EndpointDecisionKind.Warning);
    private readonly EndpointGovernanceHarness endpointDeny = new("endpoint_governance.policy_deny", EndpointDecisionKind.Deny);
    private readonly AsiBackboneGovernanceOutboxDrain outboxDrain25 = new(new BdnBenchmarkOutboxStore(25), NoOpGovernanceEmitter.Instance);
    private readonly AsiBackboneGovernanceOutboxDrain outboxDrain100 = new(new BdnBenchmarkOutboxStore(100), NoOpGovernanceEmitter.Instance);
    private readonly ScopedOutboxDrainHarness scopedOutbox100 = new(100);
    private readonly IAsiBackboneActorContext actor = AsiBackboneActorContext.Service("benchmark-service");
    private readonly GovernanceDecision auditDecision = GovernanceDecision.Deny(
        "policy.denied",
        "Policy denied the benchmark operation.",
        correlationId: "benchmark-correlation",
        traceId: "benchmark-trace",
        policyVersion: "benchmark-policy-v1",
        policyHash: "benchmark-policy-hash");
    private readonly IReadOnlyDictionary<string, string> auditMetadata = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["benchmark"] = "audit_residue.from_decision",
        ["risk"] = "routine"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="AsiBackboneHotPathBenchmarks"/> class
    /// with reusable benchmark fixtures for governance hot-path measurements.
    /// </summary>
    public AsiBackboneHotPathBenchmarks()
    {
    }

    /// <summary>
    /// Cleans up resources after all benchmarks have completed.
    /// </summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        endpointAllow.Dispose();
        endpointWarning.Dispose();
        endpointDeny.Dispose();
        scopedOutbox100.Dispose();
    }

    /// <summary>
    /// Benchmarks the creation of a governance decision that allows an operation with no reasons.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the decision outcome and reason count, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "decision.allow_no_reasons")]
    public int DecisionAllowNoReasons()
    {
        var decision = GovernanceDecision.Allow(
            correlationId: "benchmark-correlation",
            traceId: "benchmark-trace",
            policyVersion: "benchmark-policy-v1",
            policyHash: "benchmark-policy-hash");

        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks the creation of a governance decision that denies an operation with one reason.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the decision outcome and reason count, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "decision.deny_one_reason")]
    public int DecisionDenyOneReason()
    {
        var decision = GovernanceDecision.Deny(
            operationReasonOne,
            correlationId: "benchmark-correlation",
            traceId: "benchmark-trace",
            policyVersion: "benchmark-policy-v1",
            policyHash: "benchmark-policy-hash");

        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks the creation of a governance decision that denies an operation with multiple reasons.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the decision outcome and reason count, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "decision.deny_multiple_reasons")]
    public int DecisionDenyMultipleReasons()
    {
        var decision = GovernanceDecision.Deny(
            operationReasonsMany,
            correlationId: "benchmark-correlation",
            traceId: "benchmark-trace",
            policyVersion: "benchmark-policy-v1",
            policyHash: "benchmark-policy-hash");

        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks the creation of a governance decision that escalates an operation with one reason.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the decision outcome and reason count, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "decision.escalate_one_reason")]
    public int DecisionEscalateOneReason()
    {
        var decision = GovernanceDecision.Escalate(
            "decision.escalate",
            "The benchmark operation requires escalation.",
            correlationId: "benchmark-correlation",
            traceId: "benchmark-trace",
            policyVersion: "benchmark-policy-v1",
            policyHash: "benchmark-policy-hash");

        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks the creation of a governance decision that requires acknowledgment for an operation with one reason.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the decision outcome and reason count, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "operation_result.success_no_reasons")]
    public int OperationResultSuccessNoReasons()
    {
        var result = OperationResult.Success();
        return Checksum(result);
    }

    /// <summary>
    /// Benchmarks the creation of a governance decision that fails an operation with one reason.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the decision outcome and reason count, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "operation_result.failure_one_reason")]
    public int OperationResultFailureOneReason()
    {
        var result = OperationResult.Failure(operationReasonOne);
        return Checksum(result);
    }

    /// <summary>
    /// Benchmarks the creation of a governance decision that fails an operation with multiple reasons.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the decision outcome and reason count, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "operation_result.failure_multiple_reasons")]
    public int OperationResultFailureMultipleReasons()
    {
        var result = OperationResult.Failure(operationReasonsMany);
        return Checksum(result);
    }

    /// <summary>
    /// Benchmarks the evaluation of a policy with zero constraints, which should allow the operation by default.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "policy.zero_constraints")]
    public int PolicyZeroConstraints()
    {
        GovernanceDecision decision = zeroConstraintsEvaluator.EvaluateAsync(zeroConstraintsContext).GetAwaiter().GetResult();
        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks the evaluation of a policy with eight constraints that all allow the operation, resulting in an overall allow decision.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "policy.all_allow_8")]
    public int PolicyAllAllow8()
    {
        GovernanceDecision decision = allAllowEvaluator.EvaluateAsync(allAllowContext).GetAwaiter().GetResult();
        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks the evaluation of a policy with mixed constraints that produce warnings and denials, resulting in an overall decision that reflects the most severe outcome.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "policy.warning_and_denial_full")]
    public int PolicyWarningAndDenialFull()
    {
        GovernanceDecision decision = mixedEvaluator.EvaluateAsync(mixedContext).GetAwaiter().GetResult();
        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks the evaluation of a policy with mixed constraints that short-circuits on the first denial, resulting in an overall denial decision without evaluating remaining constraints.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "policy.first_denial_short_circuit")]
    public int PolicyFirstDenialShortCircuit()
    {
        GovernanceDecision decision = firstDenialEvaluator.EvaluateAsync(firstDenialContext).GetAwaiter().GetResult();
        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks the evaluation of a policy that requires acknowledgment for an operation, resulting in a decision that indicates acknowledgment is required.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "policy.acknowledgment_required")]
    public int PolicyAcknowledgmentRequired()
    {
        GovernanceDecision decision = acknowledgmentEvaluator.EvaluateAsync(acknowledgmentContext).GetAwaiter().GetResult();
        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks the evaluation of a policy that recommends escalation for an operation, resulting in a decision that indicates escalation is recommended.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "policy.escalation_recommended")]
    public int PolicyEscalationRecommended()
    {
        GovernanceDecision decision = escalationEvaluator.EvaluateAsync(escalationContext).GetAwaiter().GetResult();
        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks the evaluation of a policy that treats constraint exceptions as denials, resulting in an overall denial decision when a constraint throws an exception during evaluation.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "policy.constraint_exception_as_denial")]
    public int PolicyConstraintExceptionAsDenial()
    {
        GovernanceDecision decision = exceptionAsDenialEvaluator.EvaluateAsync(exceptionAsDenialContext).GetAwaiter().GetResult();
        return Checksum(decision);
    }

    /// <summary>
    /// Benchmarks the evaluation of an endpoint governance policy that allows the operation, resulting in a decision that indicates the endpoint can be executed.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "endpoint_governance.policy_allow")]
    public int EndpointGovernancePolicyAllow() => endpointAllow.Evaluate();

    /// <summary>
    /// Benchmarks endpoint governance evaluation when the endpoint policy returns a warning decision.
    /// </summary>
    /// <returns>
    /// An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.
    /// </returns>
    [Benchmark(Description = "endpoint_governance.policy_warning")]
    public int EndpointGovernancePolicyWarning() => endpointWarning.Evaluate();

    /// <summary>
    /// Benchmarks endpoint governance evaluation when the endpoint policy denies execution.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.</returns>
    [Benchmark(Description = "endpoint_governance.policy_deny")]
    public int EndpointGovernancePolicyDeny() => endpointDeny.Evaluate();

    /// <summary>
    /// Benchmarks draining a small batch of pending governance outbox entries.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.</returns>
    [Benchmark(Description = "outbox_drain.small_batch_25")]
    public int OutboxDrainSmallBatch25()
    {
        IReadOnlyList<GovernanceOutboxEntry> entries = outboxDrain25.DrainAsync(BenchmarkDrainUtc, 25).GetAwaiter().GetResult();
        return entries.Count ^ entries[0].Metadata.Count;
    }

    /// <summary>
    /// Benchmarks draining a medium batch of pending governance outbox entries.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.</returns>
    [Benchmark(Description = "outbox_drain.medium_batch_100")]
    public int OutboxDrainMediumBatch100()
    {
        IReadOnlyList<GovernanceOutboxEntry> entries = outboxDrain100.DrainAsync(BenchmarkDrainUtc, 100).GetAwaiter().GetResult();
        return entries.Count ^ entries[0].Metadata.Count;
    }

    /// <summary>
    /// Benchmarks resolving a scoped outbox drain service and draining a medium batch of pending entries.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.</returns>
    [Benchmark(Description = "outbox_drain.scoped_medium_batch_100")]
    public int OutboxDrainScopedMediumBatch100() => scopedOutbox100.Drain(BenchmarkDrainUtc);

    /// <summary>
    /// Benchmarks creating an audit residue directly from a governance decision and metadata.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.</returns>
    [Benchmark(Description = "audit_residue.from_decision")]
    public int AuditResidueFromDecision()
    {
        var residue = AuditResidue.FromDecision(
            actor,
            "benchmark.operation",
            auditDecision,
            eventId: "benchmark-event",
            metadata: auditMetadata,
            decisionLatencyMs: 42,
            constraintSetHash: "benchmark-constraint-set",
            constraintCount: 4,
            riskScore: 0.25,
            policyScope: "benchmark",
            emitterStatus: "queued",
            emitterProvider: "local");

        return Checksum(residue);
    }

    /// <summary>
    /// Benchmarks creating an audit residue through the builder without metadata.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.</returns>
    [Benchmark(Description = "audit_residue.builder_no_metadata")]
    public int AuditResidueBuilderNoMetadata()
    {
        AuditResidue residue = AuditResidueBuilder.Create(actor, "benchmark.operation", "Allowed")
            .WithEventId("benchmark-builder-no-metadata")
            .WithOccurredUtc(BenchmarkDrainUtc)
            .WithCorrelationId("benchmark-correlation")
            .WithTraceId("benchmark-trace")
            .WithPolicyVersion("benchmark-policy-v1")
            .WithPolicyHash("benchmark-policy-hash")
            .Build();

        return Checksum(residue);
    }

    /// <summary>
    /// Benchmarks creating an audit residue through the builder with one metadata entry.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.</returns>
    [Benchmark(Description = "audit_residue.builder_one_metadata")]
    public int AuditResidueBuilderOneMetadata()
    {
        AuditResidue residue = AuditResidueBuilder.Create(actor, "benchmark.operation", "Allowed")
            .WithEventId("benchmark-builder-one-metadata")
            .WithOccurredUtc(BenchmarkDrainUtc)
            .WithCorrelationId("benchmark-correlation")
            .WithTraceId("benchmark-trace")
            .WithPolicyVersion("benchmark-policy-v1")
            .WithPolicyHash("benchmark-policy-hash")
            .WithMetadata(auditBuilderMetadataOne)
            .Build();

        return Checksum(residue);
    }

    /// <summary>
    /// Benchmarks creating an audit residue through the builder with multiple metadata entries.
    /// </summary>
    /// <returns>An integer checksum representing the policy evaluation outcome, used for validation in benchmarks.</returns>
    [Benchmark(Description = "audit_residue.builder_many_metadata")]
    public int AuditResidueBuilderManyMetadata()
    {
        AuditResidue residue = AuditResidueBuilder.Create(actor, "benchmark.operation", "Allowed")
            .WithEventId("benchmark-builder-many-metadata")
            .WithOccurredUtc(BenchmarkDrainUtc)
            .WithCorrelationId("benchmark-correlation")
            .WithTraceId("benchmark-trace")
            .WithPolicyVersion("benchmark-policy-v1")
            .WithPolicyHash("benchmark-policy-hash")
            .WithMetadata(auditBuilderMetadataMany)
            .Build();

        return Checksum(residue);
    }

    private static int Checksum(GovernanceDecision decision) => ((int)decision.Outcome * 397) ^ decision.ReasonCodes.Count;

    private static int Checksum(OperationResult result) => (result.Succeeded ? 17 : 31) ^ result.ReasonCodes.Count ^ result.Warnings.Count;

    private static int Checksum(AuditResidue residue) => residue.ReasonCodes.Count ^ residue.Metadata.Count ^ residue.EventId.Length;

    private static BdnBenchmarkPolicyContext CreateContext(string scenarioName)
    {
        return new BdnBenchmarkPolicyContext
        {
            CorrelationId = "benchmark-correlation",
            PolicyVersion = "benchmark-policy-v1",
            PolicyHash = "benchmark-policy-hash",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["benchmark"] = scenarioName,
                ["source"] = "AsiBackbone.Benchmarks.BenchmarkDotNet"
            }
        };
    }

    private static IAsiBackboneConstraint<BdnBenchmarkPolicyContext>[] CreateStaticConstraints(int count, ConstraintEvaluationResult result)
    {
        var constraints = new IAsiBackboneConstraint<BdnBenchmarkPolicyContext>[count];

        for (int index = 0; index < constraints.Length; index++)
        {
            constraints[index] = new BdnStaticConstraint($"constraint-{index.ToString(CultureInfo.InvariantCulture)}", result);
        }

        return constraints;
    }

    private static IAsiBackboneConstraint<BdnBenchmarkPolicyContext>[] CreateMixedConstraints()
    {
        return
        [
            new BdnStaticConstraint("allow-1", ConstraintEvaluationResult.Allow()),
            new BdnStaticConstraint("warning-1", ConstraintEvaluationResult.Warning("policy.warning", "Policy produced a warning.")),
            new BdnStaticConstraint("deny-1", ConstraintEvaluationResult.Deny("policy.denied", "Policy denied the operation.")),
            new BdnStaticConstraint("allow-2", ConstraintEvaluationResult.Allow()),
            new BdnStaticConstraint("warning-2", ConstraintEvaluationResult.Warning("policy.second_warning", "Second policy warning.")),
            new BdnStaticConstraint("deny-2", ConstraintEvaluationResult.Deny("policy.second_denied", "Second policy denial."))
        ];
    }

    private static GovernanceEmissionEnvelope CreateEnvelope(int index)
    {
        string suffix = index.ToString("D6", CultureInfo.InvariantCulture);

        return GovernanceEmissionEnvelope.Create(
            GovernanceEmissionEventType.AuditLifecycle,
            eventId: $"event-{suffix}",
            occurredUtc: new DateTimeOffset(2026, 6, 30, 17, 59, 0, TimeSpan.Zero),
            envelopeId: $"envelope-{suffix}",
            correlationId: $"correlation-{suffix}",
            auditResidueId: $"residue-{suffix}",
            lifecycleStage: AuditResidueLifecycleStage.ExternalEmissionQueued,
            policyVersion: "benchmark-policy-v1",
            policyHash: "benchmark-policy-hash",
            traceId: $"trace-{suffix}",
            spanId: $"span-{suffix}",
            parentSpanId: $"parent-span-{suffix}",
            operationName: "governance.emit.benchmark",
            outcome: "Queued",
            emitterStatus: "pending",
            emitterProvider: "outbox",
            outboxSequence: index,
            metadata: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["benchmark"] = "outbox_drain",
                ["batch.index"] = suffix
            });
    }

    private sealed class EndpointGovernanceHarness : IDisposable
    {
        private readonly ServiceProvider services;
        private readonly IServiceScope serviceScope;
        private readonly HttpContext httpContext;
        private readonly AsiBackboneEndpointGovernanceDescriptor descriptor;
        private readonly IAsiBackboneEndpointGovernanceService service;

        public EndpointGovernanceHarness(string name, EndpointDecisionKind decisionKind)
        {
            services = new ServiceCollection()
                .AddAsiBackboneAspNetCore()
                .AddSingleton<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>>(new BdnFixedEndpointPolicyEvaluator(decisionKind))
                .BuildServiceProvider(validateScopes: true);

            serviceScope = services.CreateScope();
            httpContext = new DefaultHttpContext
            {
                RequestServices = serviceScope.ServiceProvider,
                TraceIdentifier = $"{name}.trace"
            };
            serviceScope.ServiceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext = httpContext;

            var endpoint = new Endpoint(
                static _ => Task.CompletedTask,
                new EndpointMetadataCollection(new RequireGovernancePolicyAttribute(typeof(BdnBenchmarkEndpointPolicy))),
                name);

            descriptor = AsiBackboneEndpointGovernanceDescriptor.FromEndpoint(endpoint);
            service = serviceScope.ServiceProvider.GetRequiredService<IAsiBackboneEndpointGovernanceService>();
        }

        public int Evaluate()
        {
            AsiBackboneEndpointGovernanceResult result = service.EvaluateAsync(httpContext, descriptor).GetAwaiter().GetResult();
            GovernanceDecision? decision = result.Decision;
            return (result.CanExecute ? 17 : 31) ^ (decision is null ? 0 : ((int)decision.Outcome * 397) ^ decision.ReasonCodes.Count);
        }

        public void Dispose()
        {
            serviceScope.Dispose();
            services.Dispose();
        }
    }

    private sealed class ScopedOutboxDrainHarness : IDisposable
    {
        private readonly int batchSize;
        private readonly ServiceProvider services;

        public ScopedOutboxDrainHarness(int batchSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);
            this.batchSize = batchSize;
            services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IAsiBackboneGovernanceOutboxStore>(_ => new BdnBenchmarkOutboxStore(batchSize))
                .AddSingleton<IAsiBackboneGovernanceEmitter>(NoOpGovernanceEmitter.Instance)
                .AddScoped<AsiBackboneGovernanceOutboxDrain>()
                .BuildServiceProvider(validateScopes: true);
        }

        public int Drain(DateTimeOffset utcNow)
        {
            using IServiceScope scope = services.CreateScope();
            AsiBackboneGovernanceOutboxDrain drain = scope.ServiceProvider.GetRequiredService<AsiBackboneGovernanceOutboxDrain>();
            IReadOnlyList<GovernanceOutboxEntry> entries = drain.DrainAsync(utcNow, batchSize).GetAwaiter().GetResult();
            return entries.Count ^ entries[0].Metadata.Count;
        }

        public void Dispose() => services.Dispose();
    }

    private sealed class BdnBenchmarkOutboxStore : IAsiBackboneGovernanceOutboxStore
    {
        private readonly GovernanceOutboxEntry[] pendingEntries;
        private readonly Dictionary<string, GovernanceOutboxEntry> entriesById;

        public BdnBenchmarkOutboxStore(int pendingCount)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pendingCount);
            pendingEntries = new GovernanceOutboxEntry[pendingCount];
            entriesById = new Dictionary<string, GovernanceOutboxEntry>(pendingCount, StringComparer.Ordinal);

            for (int index = 0; index < pendingEntries.Length; index++)
            {
                var entry = GovernanceOutboxEntry.Create(CreateEnvelope(index));
                pendingEntries[index] = entry;
                entriesById.Add(entry.OutboxEntryId, entry);
            }
        }

        public ValueTask<GovernanceOutboxEntry> EnqueueAsync(GovernanceEmissionEnvelope envelope, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(GovernanceOutboxEntry.Create(envelope));
        }

        public ValueTask<GovernanceOutboxEntry> SaveAsync(GovernanceOutboxEntry entry, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entry);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(entry);
        }

        public ValueTask<GovernanceOutboxEntry?> FindByOutboxEntryIdAsync(string outboxEntryId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
            cancellationToken.ThrowIfCancellationRequested();
            _ = entriesById.TryGetValue(outboxEntryId, out GovernanceOutboxEntry? entry);
            return ValueTask.FromResult(entry);
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindPendingAsync(int maxCount = 100, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int count = Math.Min(maxCount, pendingEntries.Length);
            IReadOnlyList<GovernanceOutboxEntry> entries = pendingEntries.AsSpan(0, count).ToArray();
            return ValueTask.FromResult(entries);
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindRetryReadyAsync(
            DateTimeOffset utcNow,
            int maxCount = 100,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            IReadOnlyList<GovernanceOutboxEntry> entries = Array.Empty<GovernanceOutboxEntry>();
            return ValueTask.FromResult(entries);
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeliveredAsync(
            string outboxEntryId,
            GovernanceEmissionResult result,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
            ArgumentNullException.ThrowIfNull(result);
            cancellationToken.ThrowIfCancellationRequested();
            GovernanceOutboxEntry entry = entriesById[outboxEntryId];
            return ValueTask.FromResult(entry.MarkDelivered(result));
        }

        public ValueTask<GovernanceOutboxEntry> MarkFailedAsync(
            string outboxEntryId,
            GovernanceEmissionError governanceEmissionError,
            DateTimeOffset? nextRetryUtc = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
            ArgumentNullException.ThrowIfNull(governanceEmissionError);
            cancellationToken.ThrowIfCancellationRequested();
            GovernanceOutboxEntry entry = entriesById[outboxEntryId];
            return ValueTask.FromResult(entry.MarkFailed(governanceEmissionError, nextRetryUtc));
        }

        public ValueTask<GovernanceOutboxEntry> MarkDeadLetteredAsync(
            string outboxEntryId,
            GovernanceEmissionError governanceEmissionError,
            string? deadLetterReason = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
            ArgumentNullException.ThrowIfNull(governanceEmissionError);
            cancellationToken.ThrowIfCancellationRequested();
            GovernanceOutboxEntry entry = entriesById[outboxEntryId];
            return ValueTask.FromResult(entry.MarkDeadLettered(governanceEmissionError, deadLetterReason));
        }
    }

    private sealed class BdnStaticConstraint(string name, ConstraintEvaluationResult result) : IAsiBackboneConstraint<BdnBenchmarkPolicyContext>
    {
        public string Name { get; } = name;

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(BdnBenchmarkPolicyContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class BdnThrowingConstraint(Exception exception) : IAsiBackboneConstraint<BdnBenchmarkPolicyContext>
    {
        public string Name => "throwing-benchmark-constraint";

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(BdnBenchmarkPolicyContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw exception;
        }
    }

    private sealed class BdnFixedEndpointPolicyEvaluator(EndpointDecisionKind decisionKind)
        : IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>
    {
        public ValueTask<GovernanceDecision> EvaluateAsync(AsiBackboneConstraintEvaluationContext context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            GovernanceDecision decision = decisionKind switch
            {
                EndpointDecisionKind.Allow => GovernanceDecision.Allow(correlationId: context.CorrelationId, policyVersion: context.PolicyVersion, policyHash: context.PolicyHash),
                EndpointDecisionKind.Warning => GovernanceDecision.Warning("endpoint.policy.warning", "Endpoint governance benchmark returned warning.", correlationId: context.CorrelationId, policyVersion: context.PolicyVersion, policyHash: context.PolicyHash),
                EndpointDecisionKind.Deny => GovernanceDecision.Deny("endpoint.policy.denied", "Endpoint governance benchmark returned deny.", correlationId: context.CorrelationId, policyVersion: context.PolicyVersion, policyHash: context.PolicyHash),
                _ => throw new InvalidOperationException($"Unsupported endpoint decision kind: {decisionKind}.")
            };
            return ValueTask.FromResult(decision);
        }
    }

    private sealed class BdnRequireAcknowledgmentPolicy : IAsiBackboneDecisionPolicy<BdnBenchmarkPolicyContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            BdnBenchmarkPolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(GovernanceDecision.RequireAcknowledgment(
                "policy.acknowledgment_required",
                "Acknowledgment is required for the benchmark operation.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }
    }

    private sealed class BdnEscalatePolicy : IAsiBackboneDecisionPolicy<BdnBenchmarkPolicyContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            BdnBenchmarkPolicyContext context,
            GovernanceDecision composedDecision,
            IReadOnlyList<ConstraintEvaluationResult> constraintResults,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(GovernanceDecision.Escalate(
                "policy.escalation_recommended",
                "Escalation is recommended for the benchmark operation.",
                correlationId: context.CorrelationId,
                policyVersion: context.PolicyVersion,
                policyHash: context.PolicyHash));
        }
    }

    private sealed class BdnBenchmarkPolicyContext : IAsiBackboneConstraintEvaluationContext
    {
        public string? CorrelationId { get; init; }
        public string? PolicyVersion { get; init; }
        public string? PolicyHash { get; init; }
        public IReadOnlyDictionary<string, string> Metadata { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private enum EndpointDecisionKind
    {
        Allow,
        Warning,
        Deny
    }

    private sealed class BdnBenchmarkEndpointPolicy
    {
    }
}
