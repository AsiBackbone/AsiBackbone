# AsiBackbone.AspNetCore

ASP.NET Core host adapters for Accountable Systems Infrastructure governance primitives.

Stable `3.2.x` package family. `3.2.3` is the current patch release for this package.

This package acts as a thin web-host adapter around `AsiBackbone.Core`.

> **New to AsiBackbone?** Start with the concept, not this package: [Intent to Execution: An Accountability Pattern](https://asibackbone.github.io/AsiBackbone/articles/intent-to-execution-pattern.html) and the [documentation site](https://asibackbone.github.io/AsiBackbone/). This README covers one package in the family.

> **Important:**
> This package provides host adapters only. Low-level helpers do not enforce decisions automatically; endpoint-governance middleware acts only when the host explicitly adds it to the pipeline and registers the required host-owned policy, capability, audit, persistence, and transaction services. Attributes and route-builder calls do not by themselves make audit records durable, immutable, tamper-evident, or transactionally safe.

## Service registration

Register the ASP.NET Core integration package from a plain ASP.NET Core host through `IServiceCollection`.

```csharp
using AsiBackbone.AspNetCore.DependencyInjection;

builder.Services.AddAsiBackboneAspNetCore();
```

Host applications may configure the integration options explicitly.

```csharp
builder.Services.AddAsiBackboneAspNetCore(options =>
{
    options.IncludeRouteValues = true;
    options.IncludeEndpointMetadata = true;
    options.IncludeRequestMethod = true;
    options.IncludeRequestPath = false;
    options.CorrelationIdHeaderNames = ["X-Correlation-ID", "X-Request-ID"];
});
```

The base registration is intentionally narrow. It does not register persistence, EF Core, authentication handlers, MVC, Razor Pages, Minimal API endpoints, policy evaluators, capability grant validators, or host-specific authorization behavior.

## Ergonomic endpoint governance

`UseAsiBackboneEndpointGovernance()` evaluates AsiBackbone endpoint metadata before endpoint execution.

```csharp
using AsiBackbone.AspNetCore.Endpoints;

app.UseAsiBackboneEndpointGovernance();

app.MapPost("/high-risk-action", handler)
    .RequireGovernancePolicy<MyStrictPolicy>()
    .RequireLiabilityHandshake()
    .RequireCapabilityGrant("robotics.execute")
    .EmitGovernanceAudit();
```

Controller/action attributes are also available:

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

The metadata layer is optional and ergonomic. It does not replace full manual wire-up. Hosts that attach policy metadata should register an `IAsiBackbonePolicyEvaluator<AsiBackboneConstraintEvaluationContext>`. Hosts that attach capability metadata should register an `IAsiBackboneEndpointCapabilityGrantValidator`. Hosts that request audit emission should register a host-owned `IAsiBackboneAuditSink`.

## Hosted governance outbox drain

`AddAsiBackboneGovernanceOutboxDrainWorker` registers a host-owned background worker that runs the provider-neutral Core `AsiBackboneGovernanceOutboxDrain` through dependency injection.

```csharp
using AsiBackbone.AspNetCore.DependencyInjection;
using AsiBackbone.Core.Emissions;
using AsiBackbone.Core.Outbox;
using AsiBackbone.Storage.InMemory.Outbox;

builder.Services.AddSingleton<IAsiBackboneGovernanceOutboxStore, InMemoryGovernanceOutboxStore>();
builder.Services.AddSingleton<IAsiBackboneGovernanceEmitter>(NoOpGovernanceEmitter.Instance);

builder.Services.Configure<AsiBackboneGovernanceOutboxOptions>(options =>
{
    options.RetryDelay = TimeSpan.FromMinutes(2);
    options.DeferredDelay = TimeSpan.FromMinutes(5);
});

builder.Services.AddAsiBackboneGovernanceOutboxDrainWorker(options =>
{
    options.BatchSize = 25;
    options.PollingInterval = TimeSpan.FromSeconds(15);
});
```

`AsiBackboneGovernanceOutboxOptions` controls persisted retry timing when an emitter does not supply its own `RetryAfterUtc`. `RetryDelay` applies to unexpected emitter exceptions converted to retryable failures. `DeferredDelay` applies to pending/deferred emission results without a retry-after timestamp. Both default to one minute to preserve the original drain behavior.

The worker resolves the drain from a scoped service provider so host-owned durable stores can depend on scoped infrastructure such as EF Core `DbContext` instances. Production hosts should avoid duplicate active drain workers against the same durable outbox unless their store implements leasing, row claiming, partitioning, or provider-side idempotency.

## Request correlation and audit enrichment

`IAsiBackboneHttpRequestCorrelationResolver` resolves request correlation data from the current `HttpContext` without making Core depend on ASP.NET Core types.

The default resolver:

- checks configured correlation headers such as `X-Correlation-ID` and `X-Request-ID`;
- trims and preserves valid printable client values up to `AsiBackboneIdentifierLimits.MaximumLength` characters;
- ignores whitespace-only, oversized, or control-character-bearing values and continues to another configured value or the normal fallback;
- falls back to `HttpContext.TraceIdentifier` when no acceptable configured header value is available;
- captures a trace identifier from `Activity.Current` or the ASP.NET Core trace identifier;
- emits safe request metadata such as method, route pattern, endpoint display name, and route values;
- excludes sensitive request data such as headers, query strings, request bodies, cookies, and tokens by default.

Invalid correlation headers are not truncated or partially sanitized. The entire client value is discarded so two distinct hostile values cannot collapse into the same accepted identifier and control characters cannot reach logging, governance records, or bounded persistence columns. Server-generated fallback behavior is unchanged.

Example usage:

```csharp
using AsiBackbone.AspNetCore.Correlation;
using AsiBackbone.Core.Audit;

AsiBackboneHttpRequestCorrelation correlation = correlationResolver.ResolveRequestCorrelation();

AuditResidue residue = correlation.CreateAuditResidue(
    actor,
    "ApproveWidget",
    decision);
```

Use `AsiBackboneHttpRequestCorrelation.ToEvaluationContext(...)` when a web host needs to carry the resolved correlation identifier and safe request metadata into a framework-neutral Core policy evaluation context.

## HTTP result mapping

`AsiBackboneHttpResultMappingExtensions` maps Core `GovernanceDecision` and `OperationResult` instances into ASP.NET Core `IResult` responses through explicit helpers.

```csharp
using AsiBackbone.AspNetCore.Results;
using AsiBackbone.Core.Decisions;

GovernanceDecision decision = GovernanceDecision.Deny(
    "policy.denied",
    "Internal policy detail for audit only.",
    correlationId: "request-123");

return decision.ToHttpResult();
```

Default governance decision mapping:

| Core outcome | Default HTTP behavior |
| --- | --- |
| `Allowed` | `200 OK` JSON response. |
| `Warning` | `200 OK` JSON response with retained reason codes. |
| `Denied` | `403 Forbidden` Problem Details response. |
| `Deferred` | `202 Accepted` Problem Details response. |
| `AcknowledgmentRequired` | `428 Precondition Required` Problem Details response. |
| `EscalationRecommended` | `409 Conflict` Problem Details response. |

Default operation-result mapping:

| Core result | Default HTTP behavior |
| --- | --- |
| Success | `200 OK` JSON response. |
| Failure | `400 Bad Request` Problem Details response. |

Reason codes and correlation identifiers are preserved by default when available. Reason messages, trace identifiers, policy versions, and policy hashes are not exposed by default because those values may reveal sensitive policy internals or diagnostic details.
