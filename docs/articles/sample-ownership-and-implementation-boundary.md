# Sample Ownership and Implementation Boundary

This page defines the sample and tutorial ownership boundary between `AsiBackbone/AsiBackbone` and `AsiBackbone/Learning`.

> [!IMPORTANT]
> `AsiBackbone/AsiBackbone` is authoritative for shipped package/API behavior. `AsiBackbone/Learning` is authoritative for vendor-neutral architecture teaching, tutorials, labs, exercises, comparisons, and standalone teaching samples. Learning does not redefine product APIs, defaults, runtime semantics, compatibility, security posture, or release behavior.

## Ownership test

> If an example remains useful after removing all AsiBackbone package references, it belongs in Learning. If its purpose is to install, register, call, configure, test, migrate, or verify concrete AsiBackbone packages and public APIs, it belongs in AsiBackbone.

Mixed topics should keep compile-ready product usage here and link to Learning for the broader architecture, alternatives, and tradeoffs.

## Learning owns

- vendor-neutral architecture teaching;
- alternative patterns and tradeoff analysis;
- exercises and labs;
- standalone conceptual samples;
- generalized AI/tool-governance teaching;
- comparisons;
- examples that intentionally do not depend on AsiBackbone.

Browse the [Learning executable sample catalog](https://asibackbone.github.io/Learning/samples/) for runnable teaching material.

## AsiBackbone owns

- install and registration quickstarts;
- compile-ready package usage;
- exact ASP.NET Core, EF Core, signing, OpenTelemetry, testing, and provider integration examples;
- `dotnet new` templates;
- package-specific sample hosts;
- public API examples;
- migration and upgrade examples;
- consumer verification examples.

## Current candidate classification

| Item | Classification | Decision |
| --- | --- | --- |
| [First 15 Minutes: Standard API Gating](quickstart-api-gating.md) | Package quickstart | Keep in AsiBackbone. It installs and exercises shipped packages. |
| [AddAsiBackbone Builder Facade](add-asibackbone-builder-facade.md) | Package registration guide | Keep in AsiBackbone. It documents the concrete builder surface. |
| [Plain ASP.NET Core Host Sample](plain-aspnetcore-host-sample.md) | Runnable package sample | Keep in AsiBackbone. It exercises current package APIs in a standard host. |
| [Aspire AppHost Sample](aspire-apphost-sample.md) | Package orchestration sample | Keep in AsiBackbone. It orchestrates AsiBackbone sample services while Aspire remains optional. |
| [Custom Decision Policy Examples](custom-decision-policy-examples.md) | Public API examples | Keep package-specific code here; use Learning for general policy-design education. |
| [Testing Harness](testing-harness.md) | Package-specific testing guide | Keep in AsiBackbone. It documents `AsiBackbone.Testing`. |
| [dotnet new Templates](templates.md) | Package-specific scaffold | Keep in AsiBackbone. It documents the shipped templates package. |

The runnable projects under [`samples/`](https://github.com/AsiBackbone/AsiBackbone/tree/main/samples) are implementation samples, not a parallel teaching curriculum.

## Scenario-by-scenario classification

The pages under `docs/articles/scenarios/` remain here as **product integration scenarios** because they map current AsiBackbone primitives into host-owned application boundaries. Deeper architecture teaching belongs in Learning.

| Scenario | Product role retained here | Deeper Learning material |
| --- | --- | --- |
| [AI Agent Gateway](scenarios/ai-agent-gateway.md) | Maps model-proposed intent to current evaluator, decision, acknowledgment, and audit APIs. | [Governed AI Tool Gateway](https://asibackbone.github.io/Learning/tutorials/governed-ai-tool-gateway.html) |
| [Human Approval Before AI Tool Execution](scenarios/human-approval-before-ai-tool-execution.md) | Maps acknowledgment-required outcomes to host-owned continuation. | [Acknowledgment and Audit Residue](https://asibackbone.github.io/Learning/tutorials/acknowledgment-and-audit-residue.html) |
| [High-Risk Administrative Action](scenarios/high-risk-administrative-action.md) | Shows a concrete product decision boundary around host-owned administration. | [Constraint-Conditioned Decision Model](https://asibackbone.github.io/Learning/architecture/constraint-conditioned-decision-model.html) |
| [Sensitive Data Access Request](scenarios/sensitive-data-access-request.md) | Shows product governance before a host-owned data path. | [Governed Execution in Regulated Systems](https://asibackbone.github.io/Learning/advanced/governed-execution-in-regulated-systems.html) |
| [Deployment or Infrastructure Change Gate](scenarios/deployment-or-infrastructure-change-gate.md) | Shows product governance before host-owned automation. | [Regional Policy and Operational Gateways](https://asibackbone.github.io/Learning/advanced/regional-policy-and-operational-gateways.html) |
| [Robotics Operational Gateway](scenarios/robotics-operational-gateway.md) | Keeps the current package boundary explicit without claiming a robotics runtime. | [Simulated Robotics-Command Governance Boundary](https://asibackbone.github.io/Learning/case-studies/simulated-robotics-command-governance-boundary.html) |

## Required shape for implementation samples

An AsiBackbone implementation sample should identify:

1. the shipped package(s) and public API(s) it exercises;
2. how to build, run, or consume the example;
3. which responsibilities remain host-owned;
4. relevant production and security boundaries;
5. the [Generated API Reference](../api-reference.md) where practical;
6. a Learning link for deeper architecture explanation rather than duplicating the lesson.

Preferred cross-link language:

> Want to understand the architectural pattern and alternatives before using this API? See [ASI Backbone Learning](https://asibackbone.github.io/Learning/).

## Authority when examples differ

Learning is the educational source of truth. This repository remains the runtime and API source of truth. If a Learning example and a released package differ, use the current product documentation, generated API reference, release notes, and source code to determine package behavior.
