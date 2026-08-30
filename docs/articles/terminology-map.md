---
description: Map canonical ASI Backbone Learning concepts to concrete AsiBackbone packages, public APIs, and implementation-specific semantics.
---

# AsiBackbone API Terminology Map

This page maps the terminology taught in ASI Backbone Learning to the concrete `AsiBackbone.*` product surface.

For **canonical educational definitions**, use the [ASI Backbone Learning Architecture Glossary](https://asibackbone.github.io/Learning/architecture/glossary.html). For terminology lineage and comparisons with ABAC, least privilege, authorization, provenance, workflow, reference-monitor patterns, and AI tool calling, use [Terminology and Established Architecture Concepts](https://asibackbone.github.io/Learning/architecture/terminology-and-established-concepts.html).

This repository does not maintain a second organization-level teaching glossary.

> [!IMPORTANT]
> `AsiBackbone/AsiBackbone` is authoritative for exact package IDs, namespaces, public types, members, defaults, reason codes, runtime semantics, security posture, provider behavior, compatibility, and releases. Learning is authoritative for architecture teaching and terminology lineage.

## Authority boundary

| Question | Canonical source |
| --- | --- |
| What does this architecture term mean for teaching and comparison? | ASI Backbone Learning |
| Which established concept is it related to? | ASI Backbone Learning |
| Which package or type implements it today? | AsiBackbone product documentation and API reference |
| What exact outcomes, fields, defaults, or runtime invariants exist? | AsiBackbone product documentation and source |
| Does a Learning sample redefine a released API contract? | No |
| Can a product page redefine the organization-level teaching vocabulary independently? | No; product pages should map the concept to implementation semantics. |

## Learning concept to product surface

| Learning concept | Concrete AsiBackbone realization | Primary package / reference | Implementation note |
| --- | --- | --- | --- |
| Governance spine | Composition of constraints, evaluator, decision, audit, acknowledgment/capability surfaces, and host execution boundaries | `AsiBackbone.Core`; [Policy Evaluator Pipeline](policy-evaluator-pipeline.md) | No single public `GovernanceSpine` type exists. |
| Intent / request | Proposed operation data carried into evaluation | [`AsiBackboneConstraintEvaluationContext`](xref:AsiBackbone.Core.Constraints.AsiBackboneConstraintEvaluationContext) | No universal `Intent` base type is required. |
| Policy context | Host-supplied evaluation facts | [`IAsiBackboneConstraintEvaluationContext`](xref:AsiBackbone.Core.Constraints.IAsiBackboneConstraintEvaluationContext) | Host integrations remain responsible for authoritative identity/resource/context data. |
| Constraint | Independently evaluated product rule | [`IAsiBackboneConstraint<TContext>`](xref:AsiBackbone.Core.Constraints.IAsiBackboneConstraint`1) | Constraint evaluation is separated from side-effect execution. |
| Policy evaluation | Constraint composition into a governance decision | [`IAsiBackbonePolicyEvaluator<TContext>`](xref:AsiBackbone.Core.Evaluation.IAsiBackbonePolicyEvaluator`1) | Evaluator output is decision data, not execution. |
| Decision outcome | Product decision plus enum outcome | [`GovernanceDecision`](xref:AsiBackbone.Core.Decisions.GovernanceDecision), [`GovernanceDecisionOutcome`](xref:AsiBackbone.Core.Decisions.GovernanceDecisionOutcome) | Product includes the `Warning` outcome in addition to the foundational Learning set. |
| Acknowledgment | Handshake request/response plus host challenge integration | [`LiabilityHandshakeRequest`](xref:AsiBackbone.Core.Handshakes.LiabilityHandshakeRequest), [`LiabilityHandshakeAcknowledgment`](xref:AsiBackbone.Core.Handshakes.LiabilityHandshakeAcknowledgment) | Naming is retained for API compatibility; documentation does not claim legal protection. |
| Audit residue | Structured product decision evidence | [`AuditResidue`](xref:AsiBackbone.Core.Audit.AuditResidue) | Storage and integrity guarantees depend on the configured host/provider path. |
| Decision provenance | Correlated decision, audit, acknowledgment, capability, lifecycle, and execution records | Audit/lifecycle/outbox APIs | Provenance is a relationship across records, not one universal type. |
| Scoped capability | Bounded grant plus validation | [`CapabilityTokenGrant`](xref:AsiBackbone.Core.CapabilityTokens.CapabilityTokenGrant), [`CapabilityGrantValidator`](xref:AsiBackbone.Core.CapabilityTokens.CapabilityGrantValidator) | The implementation uses a grant/token vocabulary; scope and validation semantics matter more than token format. |
| Host-owned execution | Application or gateway performs the real side effect | [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md) | Core intentionally does not own a universal executor. |
| Operational gateway | Host mediation before an external tool/API/device/workflow side effect | [AI Agent Gateway Scenario](scenarios/ai-agent-gateway.md) | Pattern-level mapping; no mandatory universal gateway base type. |
| Policy version | Readable policy generation | `GovernanceDecision.PolicyVersion` | Version is a label, not exact content identity. |
| Policy fingerprint | Effective-policy fingerprint | `GovernanceDecision.PolicyHash` | Current product property name is `PolicyHash`. |
| Correlation | Request/decision/audit linkage | [`AsiBackboneHttpRequestCorrelation`](xref:AsiBackbone.AspNetCore.Correlation.AsiBackboneHttpRequestCorrelation) and Core record fields | Correlation is diagnostic/provenance metadata, not authority. |
| Governance outbox | Durable pending governance emission records | `AsiBackbone.EntityFrameworkCore` integration and outbox guides | Production persistence and concurrency semantics are product-owned. |
| Governance emission | Projection to downstream observability or governance systems | `AsiBackbone.OpenTelemetry` and provider boundaries | Emission is optional and should not replace required local audit/outbox handling. |
| Signing / verification | Product signing-ready records and configured signing providers | `AsiBackbone.Signing.LocalDevelopment`, `AsiBackbone.Signing.ManagedKey` | Key custody, verification policy, and production trust remain host responsibilities. |

## Decision-outcome mapping

Learning commonly teaches the core workflow with five outcomes. The released product currently has six:

| Learning workflow term | Current product enum |
| --- | --- |
| Allow | `GovernanceDecisionOutcome.Allowed` |
| Deny | `GovernanceDecisionOutcome.Denied` |
| Defer | `GovernanceDecisionOutcome.Deferred` |
| Require acknowledgment | `GovernanceDecisionOutcome.AcknowledgmentRequired` |
| Escalate | `GovernanceDecisionOutcome.EscalationRecommended` |
| Product-specific continuation-with-warning state | `GovernanceDecisionOutcome.Warning` |

The presence of `Warning` is an implementation fact. It does not require Learning to expand every foundational teaching example to six states.

## Package ownership map

| Need | Product package / documentation |
| --- | --- |
| Framework-neutral policy decisions, constraints, audit primitives, acknowledgment, and capability grants | `AsiBackbone.Core` |
| Registration and package-family composition | `AsiBackbone.DependencyInjection` |
| ASP.NET Core request correlation, endpoint metadata, result mapping, and acknowledgment challenge seams | `AsiBackbone.AspNetCore` |
| Local sample/test audit storage | `AsiBackbone.Storage.InMemory` |
| Host-owned EF Core persistence and outbox integration | `AsiBackbone.EntityFrameworkCore` |
| Provider-neutral governance emission through .NET diagnostics | `AsiBackbone.OpenTelemetry` |
| Development signing proof paths | `AsiBackbone.Signing.LocalDevelopment` |
| Managed-key signing adapter boundary | `AsiBackbone.Signing.ManagedKey` |
| Static-analysis guidance | `AsiBackbone.Analyzers` |

## Product-specific distinctions

These distinctions are enforced by the product documentation even when Learning explains the broader architecture:

- `GovernanceDecision` is a policy result; it does not perform the side effect.
- `OperationResult` reports package operation success/failure and is not a governance outcome.
- `LiabilityHandshakeAcknowledgment` records acknowledgment and does not override authorization.
- `CapabilityTokenGrant` represents bounded authority but still requires execution-boundary validation.
- `AuditResidue` does not promise durable, immutable, signed, or tamper-evident storage by itself.
- `GovernanceDecision.PolicyVersion` and `GovernanceDecision.PolicyHash` are separate fields.
- Host-owned execution and operational gateway are architectural relationships rather than one required class hierarchy.

## Reading path

For architecture learning, continue in Learning:

1. [Architecture Glossary](https://asibackbone.github.io/Learning/architecture/glossary.html)
2. [Terminology and Established Architecture Concepts](https://asibackbone.github.io/Learning/architecture/terminology-and-established-concepts.html)
3. [Learning Tutorials](https://asibackbone.github.io/Learning/tutorials/)

For implementation work, continue here:

1. [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)
2. [Core API Domain Model](core-domain-language.md)
3. [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
4. [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
5. [Generated API Reference](../api/)
