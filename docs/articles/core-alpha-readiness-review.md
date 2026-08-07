# Historical Core Alpha Readiness Review

This document preserves the readiness review for `AsiBackbone.Core` before the `0.1.0-alpha.1` milestone was completed.

> [!NOTE]
> This page is an archived design and readiness record. It is retained to explain early Core package boundary decisions, not to describe the current stable release posture. Current stable package guidance is documented in [1.2.1 Release Notes](release-notes-121.md), [API Compatibility and Semantic Versioning](api-compatibility-and-semver.md), and [Historical Stable API Review](stable-api-review.md).

## Historical review scope

This review confirmed that the Core package was expected to:

- define the intended foundation primitives;
- keep package boundaries clean;
- avoid accidental host, persistence, web, robotics, or AI-model assumptions;
- document alpha status clearly;
- include XML documentation for public Core types and members;
- include tests for introduced primitives;
- build, test, format, and pack successfully.

## Historical Core package boundary

At the alpha stage, `AsiBackbone.Core` was a dependency-light foundation package.

Core was responsible for framework-neutral domain primitives such as:

- actor context;
- entity contracts;
- operation results;
- reason codes;
- constraint evaluation;
- governance decisions;
- audit residue;
- liability / responsibility handshakes;
- shared correlation, trace, policy version, and policy hash fields where appropriate.

Core did not provide:

- ASP.NET Core middleware;
- endpoint mapping;
- Entity Framework Core mappings;
- database storage;
- logging implementation;
- signing implementation;
- robotics control;
- AI model hosting, training, inference, or orchestration;
- NetCoreApplicationTemplate dependency.

The stable `1.x` package family now contains additional released packages and provider boundaries, but the same general dependency direction remains: Core stays framework-neutral and host-owned integration lives outside Core.

## Public API and namespace review

The alpha public API was organized by domain area:

```text
AsiBackbone.Core.Actors
AsiBackbone.Core.Audit
AsiBackbone.Core.Constraints
AsiBackbone.Core.Decisions
AsiBackbone.Core.Entities
AsiBackbone.Core.Handshakes
AsiBackbone.Core.Results
```

The domain-based namespace model kept Core readable and avoided a broad catch-all abstractions namespace.

Current stable API and namespace guidance is documented in [API Compatibility and Semantic Versioning](api-compatibility-and-semver.md) and [Historical Stable API Review](stable-api-review.md).

## Implemented foundation primitives reviewed for alpha

### Actors

Actor context primitives provided a framework-neutral description of who or what was requesting an operation.

Historical review status: Complete for alpha.

### Entities

Entity contracts provided minimal identity and optimistic-concurrency primitives.

Historical review status: Complete for alpha.

### Results

Operation result primitives separated package execution success/failure from governance decision outcomes.

Historical review status: Complete for alpha.

### Constraints

Constraint evaluation primitives supported:

- allowed;
- denied;
- warning;
- not applicable.

Historical review status: Complete for alpha.

### Decisions

Governance decision primitives supported:

- allowed;
- warning;
- denied;
- deferred;
- acknowledgment required;
- escalation recommended.

Historical review status: Complete for alpha.

### Audit

Audit residue primitives captured the framework-neutral trace of an operation, including actor, operation, outcome, reason codes, correlation data, policy version/hash, timestamp, and metadata.

Historical review status: Complete for alpha.

### Handshakes

Liability / responsibility handshake primitives represented required acknowledgment before consequential execution without assuming UI, HTTP, persistence, logging, or legal protection behavior.

Historical review status: Complete for alpha.

## Documentation review

The README and Core domain language documentation were expected to describe AsiBackbone as governance infrastructure, not an intelligence engine.

Documentation was expected to avoid claims that AsiBackbone:

- implements artificial superintelligence;
- proves the Eden Hypothesis;
- is an AI model;
- replaces legal review, AI safety governance, or organizational accountability.

Historical review status: Complete, pending final proofread at the time of the alpha record.

## XML documentation review

Public Core types and public members were expected to include XML documentation.

Historical review status: Complete, pending compiler/doc build validation at the time of the alpha record.

## Test review

Unit tests were expected to cover:

- actor context construction;
- entity identity and concurrency behavior;
- operation result behavior;
- reason code behavior;
- constraint evaluation outcomes;
- governance decision outcomes;
- audit residue construction and mapping;
- handshake request and acknowledgment behavior.

Historical review status: Complete, pending final `dotnet test` at the time of the alpha record.

## Stable-release readiness checklist

Before cutting a stable `1.0.0` release, the release readiness review was expected to confirm:

- the stable API review in [issue #13](https://github.com/AsiBackbone/AsiBackbone/issues/13) was complete or intentionally deferred with follow-up issues;
- the [API Compatibility and Semantic Versioning](api-compatibility-and-semver.md) statement was published and linked from documentation navigation;
- the stable package list was identified before release notes were finalized;
- preview, provider-specific, gateway, signing, robotics, telemetry, and cloud-emission packages were not implied to be part of the stable Core contract unless they completed their own stable API review;
- stable persisted or exported artifacts had schema-version guidance where needed;
- breaking changes found during review were resolved before `1.0.0` or captured for a later major version.

Historical review status: Pending stable release review at the time this alpha document was written.

## Current stable-era reference

For current stable documentation, use:

- [1.2.1 Release Notes](release-notes-121.md)
- [1.2.0 Release Notes](release-notes-120.md)
- [1.1.x Release Notes](release-notes-110.md)
- [API Compatibility and Semantic Versioning](api-compatibility-and-semver.md)
- [Historical Stable API Review](stable-api-review.md)
- [Schema Versioning](schema-versioning.md)
- [Core Domain Language](core-domain-language.md)
- [Historical Alpha Package Boundary](alpha-package-boundary.md)
