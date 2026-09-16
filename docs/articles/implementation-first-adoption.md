# Implementation-First Adoption Path

This page is the default route for a pragmatic .NET adopter. It keeps the first experience grounded in familiar engineering concepts before introducing optional conceptual background.

## Page navigation

Use the left navigation tree for nearby adoption articles, the right **In this article** rail for headings on long pages, and the search box in the site header for package names, API concepts, or article titles. Search is enabled in the DocFX configuration for the published site; if a newly merged page does not appear immediately, wait for the documentation publish workflow to complete and refresh the browser cache.

## One-sentence mental model

AsiBackbone helps a host application evaluate a consequential request, return a structured decision, preserve a decision receipt, and let the host continue only when its own policy allows execution.

## Translate the project terms first

| Project term | Plain .NET / architecture mapping | First-use question |
| --- | --- | --- |
| Governance spine | A policy decision pipeline around consequential operations. | Should this request continue? |
| Constraint | A small rule or guard used by the decision pipeline. | Which rule allowed or blocked the request? |
| Evaluation context | A safe DTO of request facts such as operation, region, risk, policy version, and correlation ID. | What facts did the rules inspect? |
| Governance decision | A structured allow, deny, warn, defer, acknowledgment-required, or escalation result. | What did the policy pipeline decide? |
| Audit residue | A decision receipt or audit-log payload. | What record proves how the decision was made? |
| Acknowledgment handshake | An explicit acknowledgment workflow before a risky action. | Does the actor need to acknowledge risk before continuing? |
| Capability grant | A short-lived, scoped permission after a decision. | Is continuation limited to a specific operation, actor, scope, and time window? |
| Governance outbox | A durable outbox for governance events before external emission. | Can decision records survive restart and be retried safely? |
| OpenTelemetry projection | Optional telemetry emission after local audit/outbox handling. | Should decision envelopes appear in traces or metrics? |
| Host-owned execution boundary | The application code that actually performs or refuses the protected operation. | Where does AsiBackbone stop and the host begin? |
| Decision boundary | The moment a proposed action becomes a concrete governance decision. | What result must the host inspect before execution? |

## Recommended first reading path

Follow this order when evaluating the package as ordinary software:

1. [First 15 Minutes: Standard API Gating](quickstart-api-gating.md) — run one explicit endpoint gate.
2. [Reference Deployment: Plain ASP.NET Core Host Evidence](reference-deployment.md) — see the in-repository sample produce request, decision, audit, signing, and ledger output.
3. [Terminology Map](terminology-map.md) — translate project vocabulary to familiar .NET and architecture terms.
4. [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md) — move from explicit handler code to middleware and endpoint metadata.
5. [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md) — replace in-memory validation with durable host-owned persistence.
6. [OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md) — add optional telemetry projection after local records exist.
7. [Signing Provider Package Boundary](signing-provider-package-boundary.md) — understand signing-ready and provider-signed artifacts without overstating production tamper-evidence.

This path deliberately postpones broader conceptual background until after the runnable path is clear.

## What a first implementation should prove

A minimal adopter should be able to answer these engineering questions without knowing the broader framework theory:

- Which package registers the ASP.NET Core integration?
- Which host-owned rule or constraint evaluated the request?
- Which decision outcome was returned?
- Which reason codes explain the result?
- Where was the audit residue or decision receipt written?
- Where would durable persistence be added?
- Where would optional OpenTelemetry projection be added?
- Where does the host execute, deny, defer, or escalate the actual operation?

## Minimal adoption layers

| Layer | Add when | Main package or article |
| --- | --- | --- |
| Decision primitives | You need framework-neutral decisions and constraints. | `AsiBackbone.Core` |
| ASP.NET Core host integration | You want request correlation, endpoint metadata, result mapping, or middleware. | `AsiBackbone.AspNetCore` |
| Local sample/test records | You want visible decision records without durable storage. | `AsiBackbone.Storage.InMemory` |
| Durable host persistence | Records must survive restart or support review. | `AsiBackbone.EntityFrameworkCore` |
| Test harness | Endpoint tests need deterministic policy results without production persistence/signing setup. | `AsiBackbone.Testing` |
| Telemetry projection | Local audit/outbox records should be projected into diagnostics. | `AsiBackbone.OpenTelemetry` |
| Signing boundary | A host needs signing-ready or provider-signed governance artifacts. | Signing provider docs |

## Optional conceptual background

The broader framework language can still be useful for readers who want the origin story or the philosophical framing, but it is not required to use the packages.

Read these after the implementation route is clear:

- [Intent to Execution: An Accountability Pattern](intent-to-execution-pattern.md)
- [ASI Backbone Concept Synopsis](asi-backbone-concept.md)
- [Core Domain Language](core-domain-language.md)
- [Equations and Toy Models](equations-and-toy-models.md)
- Advanced scenario pages such as agent-gateway, robotics, or regional gateway examples

When writing implementation docs, prefer the plain engineering terms first: policy decision pipeline, decision receipt, outbox pattern, scoped capability token, OpenTelemetry projection, and host-owned execution boundary.

## Read next

- [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)
- [Reference Deployment: Plain ASP.NET Core Host Evidence](reference-deployment.md)
- [Terminology Map](terminology-map.md)
