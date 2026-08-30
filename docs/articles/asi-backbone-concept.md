# ASI Backbone Concept: Product Boundary

In this software project, **ASI** means **Accountable Systems Infrastructure**.

The broader architecture teaching for Accountable Systems Infrastructure is canonical in [ASI Backbone Learning](https://asibackbone.github.io/Learning/architecture/accountable-systems-infrastructure-and-governed-execution.html).

This page is retained at its existing URL for continuity and documents only the concrete product boundary.

## What AsiBackbone implements today

AsiBackbone is a .NET governance spine for consequential software actions. The stable package family provides implementation surfaces for:

- policy context and constraint evaluation;
- explicit governance decisions;
- acknowledgment/responsibility workflows;
- audit residue and lifecycle events;
- capability-scoped continuation authority;
- durable local audit/outbox persistence;
- optional governance emission;
- host-owned execution boundaries.

The generated API reference and implementation guides define the exact public types and runtime semantics.

## Package and API surface

Use these product-owned references:

- [Core API Domain Model](core-domain-language.md)
- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Acknowledgment Workflow](dynamic-liability-handshake.md)
- [Capability Grant Hardening](capability-grant-hardening.md)
- [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md)
- [Generated API Reference](../api-reference.md)

## What the host still owns

The consuming application remains responsible for:

- authentication and ordinary authorization;
- authoritative actor/resource lookup;
- policy authorship and policy-source retention;
- persistence registration and migrations;
- UI and workflow presentation;
- secrets, credentials, and key custody;
- external or physical execution;
- operational safety and compliance review.

## Production and security boundaries

AsiBackbone does not implement artificial superintelligence, train or host AI models, control robots, certify compliance, or make an audit record tamper-evident by default.

Use the product security and operations documentation for exact signing, verification, outbox, persistence, and production-hardening behavior.

## Deeper Learning material

For the general architecture and conceptual lineage, use:

- [Accountable Systems Infrastructure and Governed Execution](https://asibackbone.github.io/Learning/architecture/accountable-systems-infrastructure-and-governed-execution.html)
- [Intent to Execution: An Accountability Pattern](https://asibackbone.github.io/Learning/architecture/intent-to-execution-accountability-pattern.html)
- [Constraint-Conditioned Decision Model](https://asibackbone.github.io/Learning/architecture/constraint-conditioned-decision-model.html)

Learning is the educational source of truth. This repository remains authoritative for package/API/runtime truth.
