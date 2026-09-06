# Getting Started

This guide explains the current direction of the AsiBackbone repository and how to begin working with the project.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a stable `4.x` .NET package family for governance-oriented decision flow. The foundation package is `AsiBackbone.Core`, with optional integration packages for in-memory validation, EF Core host-owned persistence, ASP.NET Core host integration, analyzer guidance, OpenTelemetry projection, and signing-provider boundaries.

> [!IMPORTANT]
> This project does not implement artificial superintelligence. It provides Accountable Systems Infrastructure: governance-oriented software building blocks inspired by broader Backbone framework concepts.

## Current status

The repository includes the Core foundation and optional packages for in-memory validation, EF Core host-owned persistence, ASP.NET Core host integration, Roslyn analyzer safety rails, OpenTelemetry governance emission, local-development signing, and managed-key signing adapter wiring.

The current stable package lineup is:

```text
AsiBackbone.Core
AsiBackbone.DependencyInjection
AsiBackbone.Storage.InMemory
AsiBackbone.EntityFrameworkCore
AsiBackbone.AspNetCore
AsiBackbone.Testing
AsiBackbone.Templates
AsiBackbone.Analyzers
AsiBackbone.OpenTelemetry
AsiBackbone.Signing.LocalDevelopment
AsiBackbone.Signing.ManagedKey
```

Planned or later package areas remain separate from the implemented stable lineup:

```text
AsiBackbone.EventHubs
AsiBackbone.Purview
AsiBackbone.Robotics
AsiBackbone.ImmutableStorage
```

The current implementation direction is:

1. Core governance primitives
2. Policy evaluator pipeline
3. Decision result model
4. Acknowledgment/handshake workflow
5. Audit residue and audit ledger contracts
6. Capability token abstractions
7. In-memory local validation storage
8. EF Core host-owned persistence integration
9. ASP.NET Core host integration
10. Durable audit lifecycle and governance outbox persistence
11. Provider-neutral governance emission contracts
12. OpenTelemetry provider projection
13. Analyzer safety rails
14. Signing-ready metadata and provider package boundaries
15. Plain ASP.NET Core sample host
16. Documentation and host-validation guidance

## Prerequisites

Install the following before working with the repository:

* .NET SDK
* Git
* A code editor such as Visual Studio, Visual Studio Code, or JetBrains Rider

The repository uses local .NET tools for documentation and reporting. Restore them after cloning the repository.

```bash
dotnet tool restore
```

## Clone the repository

```bash
git clone https://github.com/AsiBackbone/AsiBackbone.git
cd AsiBackbone
```

## Restore, build, and test

```bash
dotnet restore AsiBackbone.slnx
dotnet build AsiBackbone.slnx --configuration Release
dotnet test AsiBackbone.slnx --configuration Release --no-build --no-restore
```

## Build the documentation site locally

The documentation site uses DocFX.

```bash
dotnet tool restore
dotnet tool run docfx docs/docfx.json --serve
```

DocFX will build the documentation and serve it locally. Use the local URL printed in the terminal to preview changes before opening a pull request.

## Project orientation

AsiBackbone should be understood as a governance spine for Accountable Systems Infrastructure.

A typical AsiBackbone-style flow is:

```text
Intent or request
  -> Policy context
  -> Policy evaluation
  -> Decision result
  -> Optional acknowledgment
  -> Audit receipt
  -> Optional capability token
  -> Local audit/outbox persistence
  -> Optional signing or verification
  -> Optional provider emission
  -> Gateway or host execution
```

The goal is controlled, auditable decision flow.

A request should not move directly into action. It should pass through explicit policy, constraint, acknowledgment, audit, persistence, optional signing/verification, and optional provider-emission boundaries first.

## Core concepts

## Intent or request

An intent represents the action being requested.

Examples:

* Approve a document
* Call an external API
* Start an administrative workflow
* Execute a simulated gateway command
* Request access to a protected operation

## Policy context

A policy context gathers the information needed to evaluate the request.

Possible context values include:

* Actor or user
* Requested action
* Target resource
* Region or jurisdiction
* Risk level
* Current policy version
* Correlation ID
* Host application metadata

## Policy evaluation

A policy evaluator determines whether the request can proceed.

Implemented governance outcomes include:

* `Allowed`
* `Warning`
* `Denied`
* `Deferred`
* `AcknowledgmentRequired`
* `EscalationRecommended`

These outcomes are represented by `GovernanceDecision` and `GovernanceDecisionOutcome` so every package can reason about decisions consistently.

## Decision result

The decision result is one of the central objects in the package family.

A decision result should answer:

* What was requested?
* Who or what requested it?
* What decision was made?
* Which policy version produced the decision?
* Was acknowledgment required?
* Was escalation required?
* What reason codes explain the decision?
* What correlation ID links this decision to logs and audit records?

## Acknowledgment workflow

Some actions may require a user or system acknowledgment before proceeding.

This workflow supports the broader Dynamic Liability Handshake concept while allowing the public API to use implementation-focused names such as acknowledgment, responsibility handshake, or reflexive acknowledgment.

An acknowledgment workflow may include:

* Intent summary
* Risk notice
* Required confirmation
* Consent or responsibility record
* Timestamp
* Actor identity
* Policy version
* Correlation ID

## Audit receipt

An audit receipt records what happened.
