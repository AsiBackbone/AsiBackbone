# ASP.NET Core Integration Boundary

This design note defines the implemented boundary for the `AsiBackbone.AspNetCore` package.

The package makes AsiBackbone easier to wire into ASP.NET Core hosts without moving host ownership, persistence ownership, or domain policy ownership out of the consuming application.

## Purpose

`AsiBackbone.AspNetCore` acts as a thin host-integration layer around the framework-neutral Core primitives.

It helps an ASP.NET Core application:

- register AsiBackbone ASP.NET Core adapter services through standard dependency injection;
- resolve request correlation and safe request metadata;
- enrich audit residue from HTTP request context;
- map Core governance decisions and operation results into HTTP-friendly results when explicitly used by the host;
- create and handle acknowledgment challenge models for host-owned UI flows;
- remain usable in plain ASP.NET Core hosts and NetCoreApplicationTemplate-based hosts.

The package does not become the policy engine, persistence layer, application template, authentication provider, authorization system, endpoint owner, middleware enforcement layer, or execution gateway for the host.

## Package Dependencies

The implemented dependency direction is:

```text
AsiBackbone.AspNetCore
  -> AsiBackbone.Core
  -> Microsoft.AspNetCore.* abstractions needed for HTTP host integration
```

The ASP.NET Core package depends on the minimal ASP.NET Core abstractions needed for host integration, such as HTTP context access, dependency injection, and HTTP result mapping.

It avoids direct dependencies on:

- Entity Framework Core;
- concrete database providers;
- NetCoreApplicationTemplate;
- signing/key-management implementations;
- robotics or physical execution packages;
- AI model hosting, training, inference, or orchestration libraries.

EF Core-backed storage remains in `AsiBackbone.EntityFrameworkCore`. In-memory helpers remain in `AsiBackbone.Storage.InMemory`. Signing and verification remain a later signing package area.

## What Belongs in ASP.NET Core

`AsiBackbone.AspNetCore` includes:

- service registration extensions such as `AddAsiBackboneAspNetCore(...)`;
- options objects for HTTP integration behavior;
- request-correlation resolution;
- safe request metadata capture;
- audit enrichment helpers for creating Core audit residue from HTTP request context;
- HTTP result mapping helpers for Core `GovernanceDecision` and `OperationResult` values;
- acknowledgment challenge models and response handling helpers.

The package remains a web boundary adapter. It translates ASP.NET Core request information into Core domain language and translates Core outcomes back into HTTP-friendly shapes when explicitly used by the host.

## What Does Not Belong in ASP.NET Core

`AsiBackbone.AspNetCore` avoids:

- defining the Core decision model;
- implementing durable audit storage;
- owning EF Core `DbContext` configuration or migrations;
- choosing the database provider;
- choosing the authentication provider;
- replacing ASP.NET Core authorization;
- requiring Identity, OpenID Connect, SAML, Microsoft Entra ID, Google, or any other specific provider;
- requiring NetCoreApplicationTemplate;
- hiding host security policy in package defaults;
- registering policy evaluators or concrete policy rules by default;
- registering middleware or endpoint routes by default;
- executing external or robotic commands directly;
- performing AI model inference or orchestration.

The host must remain responsible for its authentication scheme, authorization policies, routing model, database lifecycle, policy definitions, and operational execution boundaries.

## Service Registration Pattern

The primary integration surface is explicit dependency injection extension methods.

```csharp
using AsiBackbone.AspNetCore.DependencyInjection;

builder.Services.AddAsiBackboneAspNetCore();
```

Host applications may configure the first integration options explicitly.

```csharp
builder.Services.AddAsiBackboneAspNetCore(options =>
{
    options.IncludeRouteValues = true;
    options.IncludeEndpointMetadata = true;
    options.IncludeRequestMethod = true;
    options.IncludeRequestPath = false;
    options.TrustInboundCorrelationIdHeaders = true;
    options.CorrelationIdHeaderNames = ["X-Correlation-ID", "X-Request-ID"];
});
```

Provider-specific or host-specific services should be added separately:

```csharp
builder.Services.AddAsiBackboneInMemoryStorage();
builder.Services.AddAsiBackboneEntityFrameworkCore();
```

The ASP.NET Core package does not implicitly register EF Core, in-memory persistence, signing providers, concrete policy rules, host authentication handlers, MVC, Razor Pages, Minimal API endpoints, or middleware enforcement.

## Request Correlation and Audit Enrichment

`IAsiBackboneHttpRequestCorrelationResolver` resolves request correlation data from the current `HttpContext` without making Core depend on ASP.NET Core types.

The default resolver:

- ignores caller-controlled correlation headers;
- falls back to `HttpContext.TraceIdentifier` when header trust is disabled or no valid trusted header is present;
- captures a trace identifier from `Activity.Current` or the ASP.NET Core trace identifier;
- always records the server-owned `HttpContext.TraceIdentifier` as `http.trace_identifier` metadata;
- emits safe request metadata such as method, route pattern, endpoint display name, and route values;
- excludes sensitive request data such as headers, query strings, request bodies, cookies, and tokens by default.

Set `TrustInboundCorrelationIdHeaders` to `true` only behind a trusted ingress that removes any caller-supplied values for the configured headers and writes its own correlation identifier. The resolver continues to reject blank, overlength, and control-character values after opt-in. Do not enable header trust on an endpoint that receives requests directly from untrusted callers, because doing so lets a caller choose the audit correlation key.

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

## HTTP Result Mapping

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

Hosts can opt into broader detail only when appropriate:

```csharp
using AsiBackbone.AspNetCore.Results;

AsiBackboneHttpResultMappingOptions mappingOptions = new()
{
    IncludeReasonMessages = true,
    IncludeTraceId = true,
    IncludePolicyMetadata = true,
};

return decision.ToHttpResult(mappingOptions);
```

Status-code policy remains host-overridable through `AsiBackboneHttpResultMappingOptions`. Hosts that intentionally mask denial or scanner traffic with alternate status codes should configure their own mapping rather than relying on the defaults.

## Acknowledgment Challenge Flow

`IAsiBackboneAcknowledgmentChallengeService` provides a host-friendly bridge for Core `AcknowledgmentRequired` decisions. It builds an `AsiBackboneAcknowledgmentChallenge` that MVC, Razor Pages, Minimal APIs, a SPA, or another UI layer can render without the package taking a dependency on that stack.

```csharp
using AsiBackbone.AspNetCore.Handshakes;
using AsiBackbone.Core.Decisions;

GovernanceDecision decision = GovernanceDecision.RequireAcknowledgment(
    "risk.high",
    "Manual acknowledgment is required before execution.",
    correlationId: "request-123");

AsiBackboneAcknowledgmentChallenge challenge = acknowledgmentChallengeService.CreateChallenge(
    actor,
    "PublishEpisode",
    decision);
```

The challenge preserves safe round-trip fields such as handshake identifier, operation name, reason code, required acknowledgment code/text, risk level, risk category, and correlation identifier. Trace identifiers and policy metadata are hidden by default and can be enabled through `AsiBackboneAcknowledgmentChallengeOptions` only when the host intentionally wants to expose those diagnostics.

Hosts can round-trip a submitted acknowledgment response back into Core handshake models:

```csharp
AsiBackboneAcknowledgmentChallengeResult result = acknowledgmentChallengeService.HandleResponse(
    challenge,
    actor,
    new AsiBackboneAcknowledgmentChallengeRequest
    {
        HandshakeId = challenge.HandshakeId,
        AcknowledgmentCode = challenge.RequiredAcknowledgmentCode,
        Acknowledged = true,
    });
```

A successful challenge result contains a Core `LiabilityHandshakeAcknowledgment`. Failed responses return an `OperationResult` with a reason code, such as a handshake mismatch or acknowledgment-code mismatch. The package does not persist challenge state; hosts decide whether to store the Core handshake request, serialize it into protected state, or associate it with an existing workflow.

## Plain ASP.NET Core Host Compatibility

A plain ASP.NET Core application can use the package with only standard ASP.NET Core dependencies.

The plain-host path supports:

- normal `Program.cs` service registration;
- configurable request-correlation behavior;
- audit enrichment from safe HTTP metadata;
- HTTP result mapping helpers;
- acknowledgment challenge helpers;
- any host-selected authentication and authorization configuration;
- any host-selected storage package or no storage package.

No NetCoreApplicationTemplate conventions are required.

## NetCoreApplicationTemplate Host Compatibility

NetCoreApplicationTemplate can remain a preferred validation host, but not a package dependency.

A NetCoreApplicationTemplate host may supply richer integrations, such as:

- existing correlation ID conventions;
- existing claims translation;
- existing Problem Details behavior;
- existing security-header and rate-limiting posture;
- existing logging conventions;
- existing authentication endpoint boundaries.

These integrations should live in host code, samples, documentation, or optional adapters only if a later package justifies them. The ASP.NET Core package itself remains usable without the template.

## Hidden Host Assumptions to Avoid

The package design avoids assumptions such as:

- every host uses MVC;
- every host uses minimal APIs;
- every host uses Identity;
- every host exposes public acknowledgment endpoints;
- every host stores audit records in a database;
- every host wants AsiBackbone to enforce decisions through middleware;
- every actor is a human user;
- every request has a tenant, region, or email;
- every denial should be returned as the same HTTP status code;
- every host wants reason messages, trace identifiers, policy versions, or policy hashes exposed in HTTP responses.

The integration should prefer explicit host registration over automatic discovery or hidden behavior.

## Follow-up Implementation Issues

This package currently provides thin HTTP adapters. Later issues may add additional host-integration surfaces if they preserve explicit host ownership, such as:

1. Optional request-context middleware.
2. Optional endpoint mapping helpers for acknowledgment and decision receipt workflows.
3. Additional host-customizable policy-context builder seams.
4. Additional sample hosts or validation scenarios.

## Boundary Summary

`AsiBackbone.AspNetCore` is the web host adapter for AsiBackbone.

It belongs at the edge between ASP.NET Core requests and Core governance primitives. It can prepare request context, resolve safe correlation metadata, enrich audit residue, map decision outcomes into HTTP-friendly responses, and support acknowledgment challenges.

It does not own persistence, policy definitions, authentication schemes, authorization rules, database migrations, signing providers, NetCoreApplicationTemplate conventions, endpoint exposure, middleware enforcement, or external execution.

That boundary keeps the package useful for both plain ASP.NET Core applications and NetCoreApplicationTemplate hosts while preserving the broader AsiBackbone principle: governance spine first, host assumptions last.
