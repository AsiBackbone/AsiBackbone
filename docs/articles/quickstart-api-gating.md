# First 15 Minutes: Standard API Gating

This quickstart is the shortest practical path for a developer who wants to answer one ordinary host question:

> Should this API request continue, and can I leave an audit trail showing how that decision was made?

In this project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a governance spine for decision flow. It does not host AI models, make autonomous choices, or implement artificial superintelligence. Your application still owns authentication, authorization, persistence, business logic, and execution.

## The 80% mental model

For a normal ASP.NET Core API, think about AsiBackbone as four small steps around an endpoint:

1. The request arrives.
2. The host builds a small evaluation context from safe request data.
3. One or more constraints decide whether the request is allowed, warned, denied, deferred, or needs acknowledgment.
4. The host records audit residue, then either continues or returns a safe response.

In plain application terms:

| AsiBackbone term | Ordinary API meaning |
| --- | --- |
| Constraint | A small rule checked before the endpoint performs work. |
| Evaluation context | Safe request facts passed to the rule, such as correlation ID, policy version, region, operation name, and risk. |
| Governance decision | The result of the rule check: allow, deny, warning, defer, acknowledgment required, or escalation recommended. |
| Audit residue | The record that says who attempted what, what decision was reached, and which policy context was used. |
| Governance metadata | Endpoint metadata that names the governance intent for route-based orchestration. |

The example below keeps the first run explicit so each moving part is visible.

## Create a minimal API host

```bash
dotnet new web -n AsiBackboneQuickstart
cd AsiBackboneQuickstart
```

Install the three packages used by this quickstart:

```bash
dotnet add package AsiBackbone.Core
dotnet add package AsiBackbone.AspNetCore
dotnet add package AsiBackbone.Storage.InMemory
```

Package roles:

| Package | Why it is used here |
| --- | --- |
| `AsiBackbone.Core` | Defines constraints, evaluation context, decisions, and audit residue. |
| `AsiBackbone.AspNetCore` | Provides ASP.NET Core registration helpers, result mapping, and endpoint metadata helpers. |
| `AsiBackbone.Storage.InMemory` | Gives the sample a non-durable local audit sink so you can see records immediately. |

## Replace `Program.cs`

Paste the following into `Program.cs`.

```csharp
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.AspNetCore.Endpoints;
using AsiBackbone.Core.Actors;
using AsiBackbone.Core.Audit;
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.Evaluation;
using AsiBackbone.Storage.InMemory.Audit;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

// Registers the ASP.NET Core adapter layer.
// This does not register authentication, persistence, policy rules, or endpoints for you.
builder.Services.AddAsiBackboneAspNetCore();

// In-memory storage is useful for a first run, tests, and samples.
// It is not durable production audit storage.
builder.Services.AddSingleton<InMemoryAuditLedger>();
builder.Services.AddSingleton<IAsiBackboneAuditSink>(serviceProvider =>
    serviceProvider.GetRequiredService<InMemoryAuditLedger>());

// Register one host-owned rule. In real applications, this is where your policy rules begin.
builder.Services.AddSingleton<IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>, AllowedRegionConstraint>();

// Register the Core evaluator. It composes constraint results into a GovernanceDecision.
builder.Services.AddSingleton<IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>>(serviceProvider =>
    new DefaultAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>(
        serviceProvider.GetServices<IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>>(),
        decisionPolicy: null,
        options: new AsiBackbonePolicyEvaluatorOptions
        {
            // For real API gating, fail closed if the host expected constraints but none were registered.
            DenyWhenNoConstraints = true,

            // 3.x defaults eligible ordinary constraint exceptions to denied GovernanceDecision results
            // with stable reason codes. Set false only when the host intentionally wants exceptions
            // to propagate to an existing exception, transaction, retry, telemetry, or incident path.
            TreatConstraintExceptionAsDenial = true
        }));

WebApplication app = builder.Build();

app.MapGet("/", () => Results.Redirect("/api/audit"));

app.MapPost("/api/orders/{region}/approve", async (
    string region,
    HttpContext httpContext,
    IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext> evaluator,
    IAsiBackboneAuditSink auditSink,
    CancellationToken cancellationToken) =>
{
    const string operationName = "orders.approve";

    // The evaluation context is the small set of safe facts the policy rule needs.
    // Avoid putting secrets, tokens, request bodies, cookies, or unnecessary PII here.
    var metadata = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["operation"] = operationName,
        ["region"] = region,
        ["risk"] = "routine-api-write"
    };

    var context = new AsiBackboneConstraintEvaluationContext(
        correlationId: httpContext.TraceIdentifier,
        policyVersion: "quickstart-policy-v1",
        policyHash: "quickstart-policy-hash-v1",
        metadata: metadata);

    GovernanceDecision decision = await evaluator
        .EvaluateAsync(context, cancellationToken)
        .ConfigureAwait(false);

    var actor = AsiBackboneActorContext.Human(
        actorId: "quickstart-user",
        displayName: "Quickstart API caller");

    // Audit residue records the decision context whether the request is allowed or denied.
    AuditResidue residue = AuditResidue.FromDecision(
        actor,
        operationName,
        decision,
        metadata: context.Metadata);

    await auditSink.WriteAsync(residue, cancellationToken).ConfigureAwait(false);

    if (!decision.CanProceed)
    {
        return Results.Json(
            new
            {
                allowed = false,
                decision = decision.Outcome.ToString(),
                decision.ReasonCodes,
                decision.CorrelationId,
                auditEventId = residue.EventId
            },
            statusCode: StatusCodes.Status403Forbidden);
    }

    // This is where the real application would perform the protected operation.
    // AsiBackbone does not execute the operation; it gates and records the decision before execution.
    return Results.Ok(new
    {
        allowed = true,
        message = "Order approval would run here after governance evaluation.",
        decision = decision.Outcome.ToString(),
        decision.CorrelationId,
        auditEventId = residue.EventId
    });
})
// Endpoint metadata gives the route a governance identity for later middleware-based orchestration.
// In this first-run sample, the handler performs the gate explicitly so every step is visible.
.WithDisplayName("orders.approve")
.MarkGovernancePolicy<OrderApprovalPolicy>()
.EmitGovernanceAudit();

app.MapGet("/api/audit", (InMemoryAuditLedger auditLedger) => Results.Ok(auditLedger.Records));

app.MapGet("/api/audit/{correlationId}", (
    string correlationId,
    InMemoryAuditLedger auditLedger) => Results.Ok(auditLedger.GetByCorrelationId(correlationId)));

app.Run();

// Marker type used by endpoint metadata. The host decides what policy marker names mean.
internal sealed class OrderApprovalPolicy
{
}

// A single simple rule: allow normal regions, deny the intentionally blocked region.
internal sealed class AllowedRegionConstraint : IAsiBackboneConstraint<AsiBackboneConstraintEvaluationContext>
{
    public string Name => "quickstart.region.allowed";

    public ValueTask<ConstraintEvaluationResult> EvaluateAsync(
        AsiBackboneConstraintEvaluationContext context,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool hasRegion = context.Metadata.TryGetValue("region", out string? region)
            && !string.IsNullOrWhiteSpace(region);

        if (!hasRegion)
        {
            return ValueTask.FromResult(ConstraintEvaluationResult.Deny(
                "quickstart.region.missing",
                "A region is required before this API operation can continue."));
        }

        if (string.Equals(region, "blocked", StringComparison.OrdinalIgnoreCase))
        {
            return ValueTask.FromResult(ConstraintEvaluationResult.Deny(
                "quickstart.region.blocked",
                "This sample blocks the requested region before the endpoint performs work."));
        }

        return ValueTask.FromResult(ConstraintEvaluationResult.Allow());
    }
}
```

## Run the sample

```bash
dotnet run
```

Use the HTTPS or HTTP URL printed by `dotnet run`. The examples below assume `http://localhost:5000`.

Allowed request:

```bash
curl -X POST http://localhost:5000/api/orders/US-LA/approve
```

Expected shape:

```json
{
  "allowed": true,
  "message": "Order approval would run here after governance evaluation.",
  "decision": "Allowed",
  "correlationId": "...",
  "auditEventId": "..."
}
```

Denied request:

```bash
curl -X POST http://localhost:5000/api/orders/blocked/approve
```

Expected shape:

```json
{
  "allowed": false,
  "decision": "Denied",
  "reasonCodes": [
    "quickstart.region.blocked"
  ],
  "correlationId": "...",
  "auditEventId": "..."
}
```

View the in-memory audit records:

```bash
curl http://localhost:5000/api/audit
```

You should see records for both the allowed and denied requests. That is the main point of the first run: the host can preserve decision evidence even when the protected operation is blocked.

## What happened

The endpoint followed this path:

```text
POST /api/orders/{region}/approve
  -> build safe evaluation context
  -> run AllowedRegionConstraint
  -> compose GovernanceDecision
  -> write AuditResidue to InMemoryAuditLedger
  -> continue only when decision.CanProceed is true
```

The important boundary is that AsiBackbone does not perform the order approval. The host performs the operation only after checking `decision.CanProceed`.

## Production configuration note

This quickstart makes two evaluator choices explicit:

- `DenyWhenNoConstraints = true` because governed production surfaces should fail closed if expected constraints are missing.
- `TreatConstraintExceptionAsDenial = true` because the `3.x` stable line defaults eligible ordinary constraint failures into denied governance decisions with the monitored `asibackbone.policy.constraint_exception` reason code.

Set `TreatConstraintExceptionAsDenial = false` only when the host has intentionally decided that constraint exceptions should propagate to an existing exception, transaction, retry, telemetry, or incident-response path and the host still records enough audit evidence for governed attempts.

## Where endpoint metadata fits

The quickstart attaches endpoint metadata with:

```csharp
.MarkGovernancePolicy<OrderApprovalPolicy>()
.EmitGovernanceAudit();
```

That metadata names the route's governance intent. The first-run sample still performs the evaluation directly inside the handler because direct code is easier to understand in the first 15 minutes.

For standard APIs that want the package to read endpoint metadata before execution, move next to [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md). That article covers `UseAsiBackboneEndpointGovernance()`, route-builder metadata, controller attributes, capability checks, acknowledgment challenges, and fail-closed behavior for missing host services.

## Where OpenTelemetry fits

OpenTelemetry is not required for the first run. The quickstart writes audit residue into an in-memory ledger so you can inspect the decision locally.

In a production-style host, the usual progression is:

1. Write local durable audit/outbox records first.
2. Drain those records through a governance emitter.
3. Use `AsiBackbone.OpenTelemetry` to project governance envelopes into tracing and metrics when an exporter is configured by the host.

See [OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md) after the basic API gate is working.

## Next steps

- Read [Policy Evaluator Pipeline](policy-evaluator-pipeline.md) for decision composition rules.
- Read [Production Hardening: Evaluator and Outbox Configuration](production-hardening-evaluator-and-outbox.md) before promoting evaluator exception handling or durable outbox storage to production.
- Read [Custom Decision Policy Examples](custom-decision-policy-examples.md) when you need host-owned acknowledgment, regional overlay, or gateway orchestration rules.
- Read [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md) for middleware-based endpoint metadata orchestration.
- Read [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md) before replacing the in-memory sample sink with durable storage.
- Read [Production Wording and Alpha Limitations](production-wording-and-alpha-limitations.md) for safe production claims and host responsibilities.
