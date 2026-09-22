# Core Governance Flow Diagrams

This page provides lightweight Mermaid diagrams for the main AsiBackbone governance flows. The diagrams are intentionally implementation-facing: they show where the package family can help structure policy evaluation, acknowledgment, decision receipt, capability boundaries, and optional governance emission without claiming that AsiBackbone owns the host application's execution path.

## How to read these diagrams

- **Host-owned execution** means the consumer application still owns authorization, business rules, side effects, infrastructure access, database provider choices, retries, and operational safeguards.
- **Released provider** means a package exists in the stable package family.
- **Design-only** or **strategy-only** means the surface is documentation or future-provider strategy unless a later stable release explicitly ships it.
- **Decision receipt** and **outbox records** are governance records. They do not replace host security controls, legal review, operational monitoring, or production key custody.

## Intent-to-execution spine

This diagram shows the highest-level flow: a request enters the policy decision pipeline, receives a decision, may require acknowledgment, may produce decision receipt or outbox records, and only reaches execution through a host-owned boundary.

```mermaid
flowchart LR
    Intent["Intent or request"] --> Context["Host builds policy context"]
    Context --> Constraints["Evaluate constraints"]
    Constraints --> Decision["GovernanceDecision"]

    Decision -->|"Allowed or Warning"| PersistBeforeExecution["Persist decision receipt or outbox before execution"]
    Decision -->|"AcknowledgmentRequired"| Ack["Host-owned acknowledgment challenge"]
    Ack -->|"Accepted and host policy permits"| PersistBeforeExecution
    Ack -->|"Rejected or incomplete"| PersistNoExecution["Persist governed outcome and do not execute"]
    Decision -->|"Denied Deferred EscalationRecommended"| PersistNoExecution

    PersistBeforeExecution --> OptionalToken["Optional scoped capability grant"]
    OptionalToken --> HostExecution["Host-owned execution boundary"]
    PersistBeforeExecution --> HostExecution
    HostExecution --> FollowUp["Optional reconciliation and review"]
```

**Caption:** AsiBackbone structures the decision spine. The host remains responsible for execution and for refusing execution when the decision, acknowledgment state, capability scope, or local policy does not permit continuation.

## Policy evaluator pipeline

This diagram shows the framework-neutral policy-evaluation shape. The host supplies context and policy components; AsiBackbone composes results into a `GovernanceDecision`; the host branches on the result.

```mermaid
flowchart LR
    HostContext["Host-provided evaluation context"] --> Evaluator["Policy evaluator"]
    Constraints["Registered constraints"] --> Evaluator
    DecisionPolicy["Optional decision policy"] --> Evaluator
    Metadata["Correlation, policy version, policy hash, and metadata"] --> HostContext

    Evaluator --> ConstraintResults["Constraint results"]
    ConstraintResults --> Composition["Decision composition"]
    DecisionPolicy --> Composition
    Composition --> GovernanceDecision["GovernanceDecision"]

    GovernanceDecision -->|"Can proceed"| ProceedPath["Host persists receipt and may continue"]
    GovernanceDecision -->|"Cannot proceed"| StopPath["Host persists receipt and stops, defers, or escalates"]
    GovernanceDecision -->|"Requires acknowledgment"| AckPath["Host presents acknowledgment before continuation"]
```

**Caption:** The evaluator returns structured decision data. It does not execute the requested operation and does not replace host authorization or business-policy enforcement.

## Acknowledgment Workflow sequence

The Acknowledgment Workflow is a reflexive acknowledgment pattern for consequential actions. It records that an actor was presented with a structured challenge and responded, but it is not by itself a legal waiver, authorization grant, or production compliance guarantee.

```mermaid
flowchart LR
    Actor["Actor"] -->|"Proposes or requests consequential action"| Host["Host application"]
    Host -->|"Evaluate policy context"| Backbone["AsiBackbone policy layer"]
    Backbone -->|"GovernanceDecision: AcknowledgmentRequired"| Host
    Host -->|"Create challenge with reason codes and context"| Handshake["Acknowledgment challenge service"]
    Handshake -->|"Present consequences and acknowledgment text"| Actor
    Actor -->|"Accepts or rejects challenge"| Handshake
    Handshake -->|"Acknowledgment result"| Host
    Host -->|"Persist decision receipt and acknowledgment record"| Audit["Decision receipt sink or outbox"]
    Host -->|"Accepted and host policy permits"| Execute["Continue through host-owned execution boundary"]
    Host -->|"Missing, rejected, expired, or host policy blocks"| Stop["Do not execute governed action"]
```

**Caption:** The handshake makes acknowledgment explicit and auditable. The host still owns identity, authorization, UI, response handling, persistence, and final execution behavior.

## Capability grant scoping and expiration

Capability-token primitives can help model a narrow grant after a governance decision, but the host or provider implementation must still enforce scope, expiry, replay handling, custody, and downstream authorization.

```mermaid
flowchart LR
    Decision["Allowed decision or acknowledged continuation"] --> GrantRequest["Capability grant request"]
    GrantRequest --> Scope["Scope operation target actor policy"]
    Scope --> Expiry["Expiration and metadata"]
    Expiry --> Token["Capability grant or grant artifact"]

    Token --> Presented["Presented to downstream host boundary"]
    Presented --> Validate["Host validates scope, expiry, actor, replay policy, and local authorization"]
    Validate -->|"Valid"| Execute["Host-owned execution"]
    Validate -->|"Invalid expired replayed or out of scope"| Deny["Deny, audit, and do not execute"]
```

**Caption:** A token is not broad execution authority. It is only useful when the host validates the exact scope and refuses continuation outside that scope.

## Durable outbox and governance emission path

This diagram separates local durable governance records from optional external observability and governance-enrichment systems. Local persistence should remain the first reliability boundary; external emission is additive.

```mermaid
flowchart LR
    Decision["GovernanceDecision"] --> Residue["Decision receipt"]
    Residue --> LocalDurable["Host-owned durable audit ledger or outbox"]
    LocalDurable --> Drain["Hosted outbox drain with retry policy"]
    Drain --> OTel["Released OpenTelemetry provider"]
    Drain --> EventHubs["Event Hubs provider design-only"]
    Drain --> Purview["Purview lineage enrichment strategy-only"]

    OTel --> Observability["External observability backend"]
    EventHubs --> GovernanceStream["Future governance stream"]
    Purview --> Catalog["Future lineage or catalog enrichment"]
    LocalDurable --> Operations["Host-owned monitoring reconciliation and retention"]
```

**Caption:** External emission does not substitute for local durable records, host-owned retry behavior, dead-letter handling, retention policy, or production signing and key-management choices.

## Placement summary

| Diagram | Primary documentation use | Boundary reminder |
| --- | --- | --- |
| Intent-to-execution spine | First explanation of the governance backbone shape. | Host owns execution. |
| Policy evaluator pipeline | Core policy and constraint composition. | Evaluator returns decisions only. |
| Acknowledgment Workflow | Acknowledgment-required decisions. | Acknowledgment is auditable intent recognition, not authorization by itself. |
| Capability grant scoping | Scoped continuation after an allowed or acknowledged decision. | Host must validate scope and expiration. |
| Durable outbox and governance emission | Audit persistence and provider emission. | External systems are additive; design-only providers are labeled as such. |

These diagrams are meant to reduce onboarding friction without expanding implementation claims beyond the shipped package boundaries.
