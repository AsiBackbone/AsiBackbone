using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Core.Outbox;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace AsiBackbone.Benchmarks;

internal static class Program
{
    private static readonly DateTimeOffset BenchmarkDrainUtc = new(2026, 6, 30, 18, 0, 0, TimeSpan.Zero);

    private static int benchmarkSink;

    public static async Task<int> Main(string[] args)
    {
        var options = BenchmarkOptions.Parse(args);

        if (options.ShowHelp)
        {
            Console.WriteLine(BenchmarkOptions.HelpText);
            return 0;
        }

        IBenchmarkScenario[] scenarios =
        [
            new PolicyEvaluationScenario(
                "policy.zero_constraints",
                "Evaluate with no registered constraints.",
                DefaultGovernancePolicyEvaluator.CreateBuilder<BenchmarkPolicyContext>()
                    .Build()),
            new PolicyEvaluationScenario(
                "policy.all_allow_8",
                "Evaluate eight allow constraints.",
                DefaultGovernancePolicyEvaluator.CreateBuilder<BenchmarkPolicyContext>()
                    .AddConstraints(CreateStaticConstraints(8, ConstraintEvaluationResult.Allow()))
                    .Build()),
            new PolicyEvaluationScenario(
                "policy.warning_and_denial_full",
                "Evaluate mixed allow, warning, and denial constraints with full aggregation.",
                DefaultGovernancePolicyEvaluator.CreateBuilder<BenchmarkPolicyContext>()
                    .AddConstraints(CreateMixedConstraints())
                    .Build()),
            new PolicyEvaluationScenario(
                "policy.first_denial_short_circuit",
                "Evaluate mixed constraints with first-denial short-circuit enabled.",
                DefaultGovernancePolicyEvaluator.CreateBuilder<BenchmarkPolicyContext>()
                    .AddConstraints(CreateMixedConstraints())
                    .WithOptions(new GovernancePolicyOptions
                    {
                        ShortCircuitOnFirstDenial = true
                    })
                    .Build()),
            new PolicyEvaluationScenario(
                "policy.acknowledgment_required",
                "Evaluate allow constraints followed by acknowledgment-required decision policy.",
                DefaultGovernancePolicyEvaluator.CreateBuilder<BenchmarkPolicyContext>()
                    .AddConstraints(CreateStaticConstraints(4, ConstraintEvaluationResult.Allow()))
                    .WithDecisionPolicy(new RequireAcknowledgmentPolicy())
                    .Build()),
            new PolicyEvaluationScenario(
                "policy.escalation_recommended",
                "Evaluate allow constraints followed by escalation-recommended decision policy.",
                DefaultGovernancePolicyEvaluator.CreateBuilder<BenchmarkPolicyContext>()
                    .AddConstraints(CreateStaticConstraints(4, ConstraintEvaluationResult.Allow()))
                    .WithDecisionPolicy(new EscalatePolicy())
                    .Build()),
            new EndpointGovernanceScenario(
                "endpoint_governance.policy_allow",
                "Evaluate DefaultEndpointGovernanceService with a host policy evaluator returning allow.",
                EndpointDecisionKind.Allow),
            new EndpointGovernanceScenario(
                "endpoint_governance.policy_warning",
                "Evaluate DefaultEndpointGovernanceService with a host policy evaluator returning warning.",
                EndpointDecisionKind.Warning),
            new EndpointGovernanceScenario(
                "endpoint_governance.policy_deny",
                "Evaluate DefaultEndpointGovernanceService with a host policy evaluator returning deny.",
                EndpointDecisionKind.Deny),
            new OutboxDrainScenario(
                "outbox_drain.small_batch_25",
                "Drain a provider-neutral governance outbox batch of 25 pending entries through the no-op emitter.",
                batchSize: 25),
            new OutboxDrainScenario(
                "outbox_drain.medium_batch_100",
                "Drain a provider-neutral governance outbox batch of 100 pending entries through the no-op emitter.",
                batchSize: 100),
            new ScopedOutboxDrainScenario(
                "outbox_drain.scoped_medium_batch_100",
                "Create a DI scope, resolve GovernanceOutboxDrain, and drain 100 pending entries.",
                batchSize: 100),
            new AuditResidueFromDecisionScenario()
        ];

        Console.WriteLine("# AsiBackbone hot-path benchmark baseline");
        Console.WriteLine();
        Console.WriteLine($"Runtime: {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"OS: {RuntimeInformation.OSDescription}");
        Console.WriteLine($"Process architecture: {RuntimeInformation.ProcessArchitecture}");
        Console.WriteLine($"Warmup iterations: {options.WarmupIterations.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine($"Measurement iterations: {options.Iterations.ToString(CultureInfo.InvariantCulture)}");
        Console.WriteLine();
        Console.WriteLine("Results are for trend detection on the same machine and runtime only. They are not absolute performance guarantees.");
        Console.WriteLine();

        var results = new List<BenchmarkResult>(scenarios.Length);

        foreach (IBenchmarkScenario scenario in scenarios)
        {
            BenchmarkResult result = await RunScenarioAsync(scenario, options).ConfigureAwait(false);
            results.Add(result);
        }

        Console.WriteLine(ToMarkdownTable(results));
        Console.WriteLine();
        Console.WriteLine($"Benchmark sink: {benchmarkSink.ToString(CultureInfo.InvariantCulture)}");
        return 0;
    }

    private static async Task<BenchmarkResult> RunScenarioAsync(
        IBenchmarkScenario scenario,
        BenchmarkOptions options)
    {
        int checksum = 0;

        for (int iteration = 0; iteration < options.WarmupIterations; iteration++)
        {
            checksum ^= await scenario.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long gen0Before = GC.CollectionCount(0);
        var stopwatch = Stopwatch.StartNew();

        for (int iteration = 0; iteration < options.Iterations; iteration++)
        {
            checksum ^= await scenario.ExecuteAsync(CancellationToken.None).ConfigureAwait(false);
        }

        stopwatch.Stop();
        long allocatedAfter = GC.GetAllocatedBytesForCurrentThread();
        long gen0After = GC.CollectionCount(0);

        benchmarkSink ^= checksum;

        long allocatedBytes = Math.Max(0, allocatedAfter - allocatedBefore);
        double nanosecondsPerOperation = stopwatch.Elapsed.TotalMilliseconds * 1_000_000d / options.Iterations;
        double bytesPerOperation = allocatedBytes / (double)options.Iterations;

        return new BenchmarkResult(
            scenario.Name,
            scenario.Description,
            options.Iterations,
            nanosecondsPerOperation,
            bytesPerOperation,
            allocatedBytes,
            gen0After - gen0Before,
            stopwatch.Elapsed);
    }

    private static string ToMarkdownTable(IEnumerable<BenchmarkResult> results)
    {
        var builder = new StringBuilder();
        _ = builder.AppendLine("| Scenario | Description | Iterations | Mean ns/op | Alloc B/op | Total allocated bytes | Gen0 collections | Elapsed ms |");
        _ = builder.AppendLine("| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |");

        foreach (BenchmarkResult result in results)
        {
            _ = builder.Append("| ")
                .Append(result.Name)
                .Append(" | ")
                .Append(result.Description)
                .Append(" | ")
                .Append(result.Iterations.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(result.NanosecondsPerOperation.ToString("N2", CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(result.BytesPerOperation.ToString("N2", CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(result.TotalAllocatedBytes.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(result.Gen0Collections.ToString("N0", CultureInfo.InvariantCulture))
                .Append(" | ")
                .Append(result.Elapsed.TotalMilliseconds.ToString("N2", CultureInfo.InvariantCulture))
                .AppendLine(" |");
        }

        return builder.ToString();
    }

    private static IGovernanceConstraint<BenchmarkPolicyContext>[] CreateStaticConstraints(
        int count,
        ConstraintEvaluationResult result)
    {
        var constraints = new IGovernanceConstraint<BenchmarkPolicyContext>[count];

        for (int index = 0; index < constraints.Length; index++)
        {
            constraints[index] = new StaticConstraint($"constraint-{index.ToString(CultureInfo.InvariantCulture)}", result);
        }

        return constraints;
    }

    private static IGovernanceConstraint<BenchmarkPolicyContext>[] CreateMixedConstraints()
    {
        return
        [
            new StaticConstraint("allow-1", ConstraintEvaluationResult.Allow()),
            new StaticConstraint("warning-1", ConstraintEvaluationResult.Warning("policy.warning", "Policy produced a warning.")),
            new StaticConstraint("deny-1", ConstraintEvaluationResult.Deny("policy.denied", "Policy denied the operation.")),
            new StaticConstraint("allow-2", ConstraintEvaluationResult.Allow()),
            new StaticConstraint("warning-2", ConstraintEvaluationResult.Warning("policy.second_warning", "Second policy warning.")),
            new StaticConstraint("deny-2", ConstraintEvaluationResult.Deny("policy.second_denied", "Second policy denial."))
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
            lifecycleStage: DecisionReceiptLifecycleStage.ExternalEmissionQueued,
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

    private sealed class BenchmarkOptions
    {
        public const int DefaultWarmupIterations = 10_000;
        public const int DefaultMeasurementIterations = 200_000;

        public static readonly string HelpText = """
            AsiBackbone benchmark runner

            Usage:
              dotnet run -c Release --project benchmarks/AsiBackbone.Benchmarks -- [options]

            Options:
              --iterations <number>   Measurement iterations per scenario. Default: 200000
              --warmup <number>       Warmup iterations per scenario. Default: 10000
              --help                  Show this help text.
            """;

        public int Iterations { get; private init; } = DefaultMeasurementIterations;

        public int WarmupIterations { get; private init; } = DefaultWarmupIterations;

        public bool ShowHelp { get; private init; }

        public static BenchmarkOptions Parse(string[] args)
        {
            int iterations = DefaultMeasurementIterations;
            int warmupIterations = DefaultWarmupIterations;
            bool showHelp = false;

            for (int index = 0; index < args.Length; index++)
            {
                string arg = args[index];

                if (string.Equals(arg, "--help", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(arg, "-h", StringComparison.OrdinalIgnoreCase))
                {
                    showHelp = true;
                    continue;
                }

                if (string.Equals(arg, "--iterations", StringComparison.OrdinalIgnoreCase))
                {
                    iterations = ReadPositiveInt(args, ref index, arg);
                    continue;
                }

                if (string.Equals(arg, "--warmup", StringComparison.OrdinalIgnoreCase))
                {
                    warmupIterations = ReadPositiveInt(args, ref index, arg);
                    continue;
                }

                throw new ArgumentException($"Unknown benchmark argument: '{arg}'. Use --help for usage.");
            }

            return new BenchmarkOptions
            {
                Iterations = iterations,
                WarmupIterations = warmupIterations,
                ShowHelp = showHelp
            };
        }

        private static int ReadPositiveInt(string[] args, ref int index, string optionName)
        {
            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Option '{optionName}' requires a numeric value.");
            }

            index++;

            return int.TryParse(args[index], NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value > 0
                ? value
                : throw new ArgumentException($"Option '{optionName}' requires a positive integer value.");
        }
    }

    private readonly record struct BenchmarkResult(
        string Name,
        string Description,
        int Iterations,
        double NanosecondsPerOperation,
        double BytesPerOperation,
        long TotalAllocatedBytes,
        long Gen0Collections,
        TimeSpan Elapsed);

    private interface IBenchmarkScenario
    {
        string Name { get; }

        string Description { get; }

        ValueTask<int> ExecuteAsync(CancellationToken cancellationToken);
    }

    private sealed class PolicyEvaluationScenario(
        string name,
        string description,
        IGovernancePolicyEvaluator<BenchmarkPolicyContext> evaluator) : IBenchmarkScenario
    {
        private readonly BenchmarkPolicyContext context = new()
        {
            CorrelationId = "benchmark-correlation",
            PolicyVersion = "benchmark-policy-v1",
            PolicyHash = "benchmark-policy-hash",
            Metadata = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["benchmark"] = name,
                ["source"] = "AsiBackbone.Benchmarks"
            }
        };

        public string Name { get; } = name;

        public string Description { get; } = description;

        public async ValueTask<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            GovernanceDecision decision = await evaluator.EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
            return ((int)decision.Outcome * 397) ^ decision.ReasonCodes.Count;
        }
    }

    private sealed class EndpointGovernanceScenario : IBenchmarkScenario
    {
        private readonly IServiceScope serviceScope;
        private readonly HttpContext httpContext;
        private readonly EndpointGovernanceDescriptor descriptor;
        private readonly IEndpointGovernanceService service;

        public EndpointGovernanceScenario(
            string name,
            string description,
            EndpointDecisionKind decisionKind)
        {
            Name = name;
            Description = description;

            ServiceProvider services = new ServiceCollection()
                .AddAsiBackboneAspNetCore()
                .AddSingleton<IGovernancePolicyEvaluator<GovernanceEvaluationContext>>(new FixedEndpointPolicyEvaluator(decisionKind))
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
                new EndpointMetadataCollection(new GovernancePolicyAttribute(typeof(BenchmarkEndpointPolicy))),
                name);

            descriptor = EndpointGovernanceDescriptor.FromEndpoint(endpoint);
            service = serviceScope.ServiceProvider.GetRequiredService<IEndpointGovernanceService>();
        }

        public string Name { get; }

        public string Description { get; }

        public async ValueTask<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            EndpointGovernanceResult result = await service
                .EvaluateAsync(httpContext, descriptor, cancellationToken)
                .ConfigureAwait(false);

            GovernanceDecision? decision = result.Decision;
            return (result.CanExecute ? 17 : 31) ^ (decision is null ? 0 : ((int)decision.Outcome * 397) ^ decision.ReasonCodes.Count);
        }
    }

    private sealed class OutboxDrainScenario : IBenchmarkScenario
    {
        private readonly int batchSize;
        private readonly GovernanceOutboxDrain drain;

        public OutboxDrainScenario(
            string name,
            string description,
            int batchSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

            Name = name;
            Description = description;
            this.batchSize = batchSize;
            drain = new GovernanceOutboxDrain(
                new BenchmarkOutboxStore(batchSize),
                NoOpGovernanceEmitter.Instance);
        }

        public string Name { get; }

        public string Description { get; }

        public async ValueTask<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<GovernanceOutboxEntry> entries = await drain
                .DrainAsync(BenchmarkDrainUtc, batchSize, cancellationToken)
                .ConfigureAwait(false);

            return entries.Count ^ entries[0].Metadata.Count;
        }
    }

    private sealed class ScopedOutboxDrainScenario : IBenchmarkScenario
    {
        private readonly int batchSize;
        private readonly ServiceProvider services;

        public ScopedOutboxDrainScenario(
            string name,
            string description,
            int batchSize)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(batchSize);

            Name = name;
            Description = description;
            this.batchSize = batchSize;
            services = new ServiceCollection()
                .AddLogging()
                .AddSingleton<IGovernanceOutboxStore>(_ => new BenchmarkOutboxStore(batchSize))
                .AddSingleton<IGovernanceEmitter>(NoOpGovernanceEmitter.Instance)
                .AddScoped<GovernanceOutboxDrain>()
                .BuildServiceProvider(validateScopes: true);
        }

        public string Name { get; }

        public string Description { get; }

        public async ValueTask<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            using IServiceScope scope = services.CreateScope();
            GovernanceOutboxDrain drain = scope.ServiceProvider.GetRequiredService<GovernanceOutboxDrain>();
            IReadOnlyList<GovernanceOutboxEntry> entries = await drain
                .DrainAsync(BenchmarkDrainUtc, batchSize, cancellationToken)
                .ConfigureAwait(false);

            return entries.Count ^ entries[0].Metadata.Count;
        }
    }

    private sealed class AuditResidueFromDecisionScenario : IBenchmarkScenario
    {
        private readonly IGovernanceActorContext actor = GovernanceActorContext.Service("benchmark-service");

        private readonly GovernanceDecision decision = GovernanceDecision.Deny(
            "policy.denied",
            "Policy denied the benchmark operation.",
            correlationId: "benchmark-correlation",
            traceId: "benchmark-trace",
            policyVersion: "benchmark-policy-v1",
            policyHash: "benchmark-policy-hash");

        private readonly IReadOnlyDictionary<string, string> metadata = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["benchmark"] = "audit_residue.from_decision",
            ["risk"] = "routine"
        };

        public string Name => "audit_residue.from_decision";

        public string Description => "Create audit residue from a governance decision.";

        public ValueTask<int> ExecuteAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var residue = DecisionReceipt.FromDecision(
                actor,
                "benchmark.operation",
                decision,
                eventId: "benchmark-event",
                metadata: metadata,
                decisionLatencyMs: 42,
                constraintSetHash: "benchmark-constraint-set",
                constraintCount: 4,
                riskScore: 0.25,
                policyScope: "benchmark",
                emitterStatus: "queued",
                emitterProvider: "local");

            return ValueTask.FromResult(residue.ReasonCodes.Count ^ residue.Metadata.Count);
        }
    }

    private sealed class BenchmarkPolicyContext : IGovernanceEvaluationContext
    {
        public string? CorrelationId { get; init; }

        public string? PolicyVersion { get; init; }

        public string? PolicyHash { get; init; }

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>(StringComparer.Ordinal);
    }

    private sealed class StaticConstraint(
        string name,
        ConstraintEvaluationResult result) : IGovernanceConstraint<BenchmarkPolicyContext>
    {
        public string Name { get; } = name;

        public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
            BenchmarkPolicyContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(result);
        }
    }

    private sealed class FixedEndpointPolicyEvaluator(EndpointDecisionKind decisionKind)
        : IGovernancePolicyEvaluator<GovernanceEvaluationContext>
    {
        public ValueTask<GovernanceDecision> EvaluateAsync(
            GovernanceEvaluationContext context,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            GovernanceDecision decision = decisionKind switch
            {
                EndpointDecisionKind.Allow => GovernanceDecision.Allow(
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash),
                EndpointDecisionKind.Warning => GovernanceDecision.Warning(
                    "endpoint.policy.warning",
                    "Endpoint governance benchmark returned warning.",
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash),
                EndpointDecisionKind.Deny => GovernanceDecision.Deny(
                    "endpoint.policy.denied",
                    "Endpoint governance benchmark returned deny.",
                    correlationId: context.CorrelationId,
                    policyVersion: context.PolicyVersion,
                    policyHash: context.PolicyHash),
                _ => throw new InvalidOperationException($"Unsupported endpoint decision kind: {decisionKind}.")
            };

            return ValueTask.FromResult(decision);
        }
    }

    private sealed class BenchmarkOutboxStore : IGovernanceOutboxStore
    {
        private readonly GovernanceOutboxEntry[] pendingEntries;
        private readonly Dictionary<string, GovernanceOutboxEntry> entriesById;

        public BenchmarkOutboxStore(int pendingCount)
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

        public ValueTask<GovernanceOutboxEntry> EnqueueAsync(
            GovernanceEmissionEnvelope envelope,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(envelope);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(GovernanceOutboxEntry.Create(envelope));
        }

        public ValueTask<GovernanceOutboxEntry> SaveAsync(
            GovernanceOutboxEntry entry,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(entry);
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(entry);
        }

        public ValueTask<GovernanceOutboxEntry?> FindByOutboxEntryIdAsync(
            string outboxEntryId,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(outboxEntryId);
            cancellationToken.ThrowIfCancellationRequested();
            _ = entriesById.TryGetValue(outboxEntryId, out GovernanceOutboxEntry? entry);
            return ValueTask.FromResult(entry);
        }

        public ValueTask<IReadOnlyList<GovernanceOutboxEntry>> FindPendingAsync(
            int maxCount = 100,
            CancellationToken cancellationToken = default)
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

    private sealed class RequireAcknowledgmentPolicy : IGovernanceDecisionPolicy<BenchmarkPolicyContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            BenchmarkPolicyContext context,
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

    private sealed class EscalatePolicy : IGovernanceDecisionPolicy<BenchmarkPolicyContext>
    {
        public ValueTask<GovernanceDecision> ApplyAsync(
            BenchmarkPolicyContext context,
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

    private enum EndpointDecisionKind
    {
        Allow,
        Warning,
        Deny
    }

    private sealed class BenchmarkEndpointPolicy
    {
    }
}
