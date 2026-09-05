# ASP.NET Core Endpoint Governance

`AsiBackbone.AspNetCore` includes an optional ergonomic endpoint-governance layer for the common ASP.NET Core case where a host wants to protect a Minimal API endpoint or controller action with AsiBackbone metadata.

This layer is intentionally a host adapter. It does not replace Core primitives, make persistence durable, create transactions, or certify audit immutability. It reduces endpoint boilerplate while keeping storage, transaction boundaries, capability-grant validation, and outbox behavior under host ownership.

## Register services and middleware

```csharp
builder.Services.AddAsiBackboneAspNetCore();

WebApplication app = builder.Build();

app.UseAsiBackboneEndpointGovernance();
```

Place `UseAsiBackboneEndpointGovernance()` after routing has selected an endpoint and before the protected endpoint executes. In a typical minimal host this means before `MapControllers()` and before mapped endpoints are executed by endpoint routing.

Hosts that use policy metadata should register an `IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>`. Hosts that use capability metadata should register an `IAsiBackboneEndpointCapabilityGrantValidator`. Hosts that request audit emission should register a host-owned `IAsiBackboneAuditSink`.

Because those services may run before the protected endpoint executes, their implementation choices affect request throughput. Keep request-time evaluators, validators, and audit sinks async, cancellable, bounded, and free of blocking calls such as `.Result`, `.Wait()`, `Thread.Sleep`, synchronous network calls, synchronous database calls, or unbounded `Task.Run` work. See [High-Throughput Host Service Guidance](high-throughput-host-services.md) for request hot-path examples, anti-patterns, queue/backpressure guidance, and the framework/host responsibility boundary.

## Middleware ordering

Endpoint governance reads the *selected* endpoint's metadata, so it must run after endpoint routing has selected one. Register it after `UseRouting` and after authentication, so an actor context is available:

```csharp
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.UseAsiBackboneEndpointGovernance();
```

Placed before routing, every request reaches the middleware with no endpoint. No governance metadata is found, and the request is forwarded **ungoverned** unless `RequireGovernanceMetadata` is enabled. Nothing fails; endpoints that declare policies, capability grants, or handshakes are simply never evaluated.

A Minimal API host that never calls `UseRouting` is still correct: `WebApplication` inserts routing at the front of the pipeline when endpoints are mapped, so the endpoint is available by the time this middleware runs.

A host that calls `UseRouting` explicitly can turn the ordering requirement into a startup failure:

```csharp
app.UseRouting();
app.UseAsiBackboneEndpointGovernance(requireEndpointRoutingRegistered: true);
```

This is off by default because it cannot be checked reliably. The routing marker it inspects is legitimately absent in the Minimal API case above, so enabling it by default would fail startup for hosts that are correctly configured.

When `RequireGovernanceMetadata` is enabled, a request with no selected endpoint fails closed and is reported under the `aspnetcore.endpoint.governance.unresolved_endpoint` decision stage, distinct from the `aspnetcore.endpoint.governance.metadata` stage used when an endpoint was selected but declares no governance metadata. The two have different causes and different fixes.

## Options validation posture

`AddAsiBackboneAspNetCore()` registers endpoint-governance options validation with startup validation. Invalid endpoint-governance options fail through the configured options validation path instead of being revalidated on every request.

The middleware reads the validated `IOptions<AsiBackboneEndpointGovernanceOptions>.Value` and avoids repeating `Validate()` in the request hot path. This keeps invalid configuration fail-closed through startup/configured-options validation while avoiding per-request validation overhead for stable host options.

Hosts that intentionally mutate options at runtime should validate their mutation path before applying it. The endpoint-governance middleware does not treat live option mutation as the normal production path.

## Minimal API / fluent endpoint path

```csharp
app.MapPost("/high-risk-action", handler)
    .MarkGovernancePolicy<MyStrictPolicy>()
    .RequireLiabilityHandshake()
    .RequireCapabilityGrant("robotics.execute")
    .EmitGovernanceAudit();
```

The fluent methods add endpoint metadata. The middleware resolves that metadata into an `AsiBackboneEndpointGovernanceDescriptor`, builds a framework-neutral evaluation context, and delegates policy evaluation, capability validation, handshake creation, and audit emission to registered host-owned services.

### What the policy marker does, and what it does not

`MarkGovernancePolicy<TPolicy>()` records a marker. It does not select an evaluation path.

The framework does not resolve the marked type or derive a constraint set from it. The registered `IAsiBackbonePolicyEvaluator` evaluates **every** registered constraint on **every** governed endpoint, whichever policy type an endpoint carries. What the marker controls is whether policy evaluation runs at all: an endpoint with no policy marker skips the policy stage.

Two endpoints marked with different policy types therefore evaluate identically unless the host makes them differ. The marker reaches evaluation as the `endpoint.policy_types` metadata entry, so the supported way to vary behavior by policy is a host-supplied `IAsiBackboneDecisionPolicy<AsiBackboneConstraintEvaluationContext>` that reads that entry and adjusts the composed decision:

```csharp
public sealed class PolicyAwareDecisionPolicy : IAsiBackboneDecisionPolicy<AsiBackboneConstraintEvaluationContext>
{
    public ValueTask<GovernanceDecision> ApplyAsync(
        AsiBackboneConstraintEvaluationContext context,
        GovernanceDecision composedDecision,
        IReadOnlyList<ConstraintEvaluationResult> constraintResults,
        CancellationToken cancellationToken = default)
    {
        // context.Metadata["endpoint.policy_types"] carries the marked type names.
        return ValueTask.FromResult(composedDecision);
    }
}
```

`endpoint.policy_types` is retained under `AsiBackboneEndpointGovernanceMetadataMode.Reduced` for exactly this reason: dropping it would let a metadata setting silently disable a host decision policy that depends on it.

`RequireGovernancePolicy` is the former name of this method and is obsolete. It never resolved the policy type either; the name overstated what it did. It still records the same metadata and can be replaced with `MarkGovernancePolicy` without behavior change.

Endpoints that intentionally prefer a latency-optimized first-block fast-abort policy path can add endpoint metadata:

```csharp
app.MapPost("/high-risk-action", handler)
    .MarkGovernancePolicy<MyStrictPolicy>()
    .ShortCircuitOnFirstDenial();
```

The descriptor exposes this as `ShortCircuitOnFirstDenial` and includes `endpoint.short_circuit_on_first_denial` in descriptor metadata. Host-owned policy wiring remains responsible for mapping that endpoint preference into `AsiBackbonePolicyEvaluatorOptions.ShortCircuitOnFirstDenial` when constructing or resolving the evaluator.

## Controller/action attribute path

```csharp
[RequireGovernancePolicy(typeof(MyStrictPolicy))]
[RequireLiabilityHandshake]
[RequireCapabilityGrant("robotics.execute")]
[EmitGovernanceAudit]
public IActionResult ExecuteHighRiskAction()
{
    return Ok();
}
```

The attribute model is designed to feel familiar to ASP.NET Core developers who already use attributes such as `[Authorize]`. The attributes only add metadata. They do not by themselves validate capability grants, write durable audit records, or provide transaction guarantees.

Endpoint-scoped fast-abort metadata is also available as an attribute:

```csharp
[RequireGovernancePolicy(typeof(MyStrictPolicy))]
[ShortCircuitOnFirstDenial]
public IActionResult ExecuteLatencySensitiveAction()
{
    return Ok();
}
```

## What the ergonomic layer does

When endpoint governance metadata is present, the middleware can:

1. Resolve the selected endpoint metadata.
2. Build a safe `AsiBackboneConstraintEvaluationContext` using HTTP request correlation data.
3. Invoke the host-registered policy evaluator when policy metadata exists.
4. Invoke the host-registered capability validator when capability scopes exist.
5. Emit `AuditResidue` through the host-owned audit sink when audit emission is requested.
6. Return an acknowledgment challenge when the governance decision requires acknowledgment and the endpoint requested liability-handshake support.
7. Block execution with a safe HTTP result when policy, capability, or configuration checks fail closed.

## Host-owned boundaries

The ergonomic endpoint layer deliberately does not own persistence. Durable audit storage, outbox persistence, transactional consistency, signing, key management, and replay protection remain host responsibilities.

| Concern | Owner |
| --- | --- |
| Endpoint metadata and middleware orchestration | `AsiBackbone.AspNetCore` |
| Policy constraints and decision policy | Host/Core evaluator registration |
| Capability-grant source, proof validation, and replay handling | Host-owned `IAsiBackboneEndpointCapabilityGrantValidator` |
| Audit sink, ledger store, outbox store, and transactions | Host-owned storage/integration layer |
| Legal/compliance interpretation | Host governance process |

High-throughput hosts should treat every host-owned row in this table as production code that can dominate latency. If a host needs expensive provider delivery, DLP/classification, signing, or SIEM export, prefer a local durable record plus outbox handoff instead of performing that work synchronously inside request middleware.

## Failure behavior

`AsiBackboneEndpointGovernanceOptions` controls fail-closed behavior for missing host services:

```csharp
builder.Services.Configure<AsiBackboneEndpointGovernanceOptions>(options =>
{
    options.FailClosedWhenPolicyEvaluatorMissing = true;
    options.FailClosedWhenCapabilityValidatorMissing = true;
    options.FailClosedWhenAuditSinkMissing = true;
});
```

Failing closed is the default because an endpoint that declares governance intent should not silently bypass missing policy, capability, or audit services. Advanced hosts can relax this behavior during migration, but should document why.

When middleware blocks execution and no explicit `FailureResult` is supplied, the default response is a bodyless `403 Forbidden` status result. This low-allocation default is intentional for high-volume rejection traffic such as probing, credential stuffing, or denial flooding, and it avoids exposing governance reason codes or policy details in generic denial responses.

The default endpoint governance service uses this generic 403 path for ordinary denied governance decisions when `DeniedStatusCode` remains `403`. Configuration failures and capability-validator setup failures still return explicit ProblemDetails responses because they indicate host setup issues rather than routine request denial. Non-403 governance outcomes continue to use the HTTP result mapping options.

Custom failure results remain supported. A host-owned governance service can return an explicit `FailureResult`, and that result is executed instead of the generic default. Hosts that prefer richer API responses for generic 403 denials can configure a safe factory:

```csharp
builder.Services.Configure<AsiBackboneEndpointGovernanceOptions>(options =>
{
    options.DefaultForbiddenResultFactory = _ => Results.Problem(
        title: "Forbidden.",
        detail: "The request is not allowed.",
        statusCode: StatusCodes.Status403Forbidden);
});
```

Use richer ProblemDetails responses only when the response body is safe for the deployment. Do not include sensitive policy internals, capability-token details, audit identifiers, or reason messages unless the host has explicitly decided those values are safe to expose.

## Endpoint metadata mode

Endpoint governance builds a normalized metadata dictionary for policy evaluation, audit residue, acknowledgment challenges, and development diagnostics. The default `Full` mode preserves the existing traceability behavior and includes values such as:

- `endpoint.operation_name`
- `endpoint.requires_liability_handshake`
- `endpoint.emit_governance_audit`
- `endpoint.policy_types`
- `endpoint.short_circuit_on_first_denial`, when configured
- `endpoint.capability_scopes`, when configured

High-throughput production hosts that have measured endpoint metadata creation as meaningful overhead can opt into reduced metadata:

```csharp
builder.Services.Configure<AsiBackboneEndpointGovernanceOptions>(options =>
{
    options.MetadataMode = AsiBackboneEndpointGovernanceMetadataMode.Reduced;
});
```

`Reduced` mode forwards only `endpoint.operation_name` through the metadata dictionary. The descriptor still uses the full ASP.NET Core endpoint metadata internally to decide whether policy evaluation, capability validation, audit emission, or acknowledgment handling should run. The tradeoff is that host policy evaluators, audit sinks, acknowledgment stores, and development diagnostics will not receive the omitted metadata values through `AsiBackboneConstraintEvaluationContext.Metadata` or related metadata payloads.

Do not enable reduced metadata if host policies depend on `endpoint.policy_types`, `endpoint.capability_scopes`, or other endpoint metadata values. Prefer the default `Full` mode until benchmark output shows that the reduced path is worth the loss of diagnostic context.

## Development diagnostics

Local development hosts can opt into richer ProblemDetails diagnostics for endpoint governance failures:

```csharp
builder.Services.Configure<AsiBackboneEndpointGovernanceOptions>(options =>
{
    options.EnableDevelopmentDiagnostics = builder.Environment.IsDevelopment();
    options.DevelopmentDiagnosticsDocumentationBaseUrl = "https://asibackbone.github.io/AsiBackbone/articles/";
});
```

Diagnostics are emitted only when enabled and when the request service provider exposes an `IWebHostEnvironment` whose environment name is `Development`. Production-safe defaults remain conservative: diagnostics are off by default and ordinary denied decisions keep the bodyless generic `403` path unless diagnostics are explicitly enabled in development.

Development diagnostics may include the governance outcome, reason codes and messages, endpoint operation name, policy marker types, requested capability scopes, decision stage, correlation/trace identifiers, metadata mode, redacted metadata, and a troubleshooting documentation link. When `MetadataMode` is `Reduced`, the diagnostic metadata keys and redacted metadata payload reflect the reduced metadata dictionary, while descriptor-derived fields such as policy marker types and capability scopes may still appear in development-only diagnostics.

See [Endpoint Governance Development Diagnostics](endpoint-governance-development-diagnostics.md) for response examples, common failures, and redaction rules.

## Strict metadata enforcement

By default, endpoint governance remains opt-in. Endpoints without AsiBackbone governance metadata pass through to the next middleware. This mirrors common ASP.NET Core adoption patterns and preserves backwards compatibility.

Regulated or governance-sensitive hosts can enable fail-closed metadata enforcement:

```csharp
builder.Services.Configure<AsiBackboneEndpointGovernanceOptions>(options =>
{
    options.RequireGovernanceMetadata = true;
});
```

When enabled, selected endpoints without governance metadata are blocked before execution unless the endpoint explicitly allows missing governance metadata. This option does not replace ASP.NET Core authentication or authorization. It only prevents accidental governance bypass caused by missing AsiBackbone endpoint metadata. The default strict-metadata rejection also uses the bodyless generic `403 Forbidden` response unless `DefaultForbiddenResultFactory` is configured or development diagnostics are enabled in a Development environment.

## Relation to full manual wire-up

Manual wire-up remains the most explicit path for complex flows. Use manual integration when the endpoint requires a custom transaction boundary, multiple persistence stores, custom signing, outbox enqueue-before-execution semantics, or workflow-specific acknowledgment handling.

Use the ergonomic layer when the endpoint follows the common pattern: read metadata, evaluate policy, validate capability, optionally emit audit residue, and either continue or return a safe governance response.
