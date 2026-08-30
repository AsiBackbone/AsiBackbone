# Intent to Execution: Product Mapping

The stack-neutral accountability pattern is canonical in [ASI Backbone Learning](https://asibackbone.github.io/Learning/architecture/intent-to-execution-accountability-pattern.html).

This URL is retained for continuity and maps that pattern to current AsiBackbone implementation surfaces.

## AsiBackbone implementation mapping

| Pattern stage | Product surface |
| --- | --- |
| Intent / request | Host-defined operation plus AsiBackbone policy/evaluation context |
| Constraint evaluation | Core constraint and policy-evaluator contracts |
| Explicit decision | `GovernanceDecision` and its outcome/reason metadata |
| Acknowledgment | Liability/responsibility handshake request and acknowledgment records |
| Audit residue | Audit ledger, lifecycle events, correlation/policy metadata |
| Scoped continuation authority | Capability-grant contracts and validation profiles |
| Execution | Host-owned; not performed by Core |
| Reconciliation / evidence | Host execution records plus audit/outbox/provider evidence |

## Host responsibility

The host still owns the real side effect, authentication/authorization, authoritative resource context, secrets, transactions, idempotency, and operational safety.

## Product references

- [Core Governance Flow Diagrams](core-governance-flow-diagrams.md)
- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Acknowledgment Workflow](dynamic-liability-handshake.md)
- [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md)
- [Capability Grant Hardening](capability-grant-hardening.md)
- [Generated API Reference](../api/)

Use Learning for the architectural pattern and tradeoffs. Use this repository for exact product semantics.
