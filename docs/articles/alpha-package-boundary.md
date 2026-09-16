# Historical Alpha Package Boundary

This article is a historical stable-era reference for the original intended `0.1.0-alpha.1` boundary for `AsiBackbone.Core`.

> [!NOTE]
> This page is preserved for project history and early package-boundary rationale. It describes the alpha planning posture before the stable `1.x` package family. Current stable package guidance is documented in [1.2.1 Release Notes](release-notes-121.md), [API Compatibility and Semantic Versioning](api-compatibility-and-semver.md), and [Historical Stable API Review](stable-api-review.md).


At the alpha stage, `0.1.0-alpha.1` was intended to establish the first shared language for the package family. It was not intended to deliver persistence, web integration, signing infrastructure, robotics integration, or AI model functionality.

> [!IMPORTANT]
> The alpha package was planned as a governance and policy-control foundation. It was not an ASI implementation and did not host, train, run, or orchestrate AI models.

## Historical release intent

The first alpha was intended to answer one question:

> What are the shared framework-neutral primitives that future AsiBackbone packages will build on?

It was expected to provide enough vocabulary for later implementation issues to reference the same concepts consistently.

## Historical boundary statements

The alpha documentation and package metadata were expected to preserve these boundaries:

* Core had no ASP.NET Core dependency.
* Core had no Entity Framework Core dependency.
* Core had no database-provider dependency.
* Core had no NetCoreApplicationTemplate dependency.
* Core had no AI model dependency.
* Core had no robotics or physical execution dependency.
* Core remained framework-neutral and host-neutral.

These boundaries later informed the stable `1.x` package-family direction. Current stable package boundaries are documented in [API Compatibility and Semantic Versioning](api-compatibility-and-semver.md) and [1.2.1 Release Notes](release-notes-121.md).

## Initial package responsibility

At the alpha planning stage, `AsiBackbone.Core` was responsible for defining the first set of shared abstractions and primitives around:

* governance spine
* intent/request evaluation
* actor context
* policy context
* constraints
* decision result outcomes
* operation result outcomes
* reason codes
* acknowledgment or responsibility-handshake workflows
* decision receipt and receipts
* capability-token concepts
* policy version and hash tracking
* correlation IDs

The stable `1.x` Core surface has since expanded beyond this initial list while keeping the same host-neutral and provider-neutral direction.

## Suggested first vertical slice

The first useful vertical slice was intended to be testable without a web host or database.

```text
Create intent/request
  -> Build policy context
  -> Evaluate constraints
  -> Produce decision result
  -> Produce audit receipt shape
```

The initial unit tests were expected to cover:

* allow
* deny
* defer
* require acknowledgment
* escalate
* missing or invalid context
* reason code assignment
* correlation ID propagation
* policy version/hash propagation

## Non-goals for `0.1.0-alpha.1`

The first alpha was not intended to include:

* EF Core entity mappings
* migrations
* DbContext ownership
* ASP.NET Core middleware
* ASP.NET Core endpoint mapping
* authentication provider integration
* claims translation implementation
* durable ledger implementation
* cryptographic signing implementation
* robot command schemas
* external system gateway SDKs
* AI inference or training APIs

Those areas were expected to become later package responsibilities after the Core language stabilized. In the stable `1.x` line, several of those areas are now represented as explicit package boundaries or host-owned integration seams and are carried forward into the current `1.2.x` package family, while AI inference, robotics control, production tamper-evidence, and compliance guarantees remain outside the default package claim.

## Historical implementation sequence

The early practical implementation sequence was:

1. Core abstractions and value objects
2. Policy pipeline contracts
3. Decision result model
4. Acknowledgment workflow contracts
5. Audit receipt contracts
6. Capability-token contracts
7. In-memory validation package or samples
8. ASP.NET Core integration
9. EF Core storage integration
10. Signing support
11. Gateway examples
12. Robotics or high-risk external execution examples

This sequence is retained as planning history. Current package status should be read from [1.2.1 Release Notes](release-notes-121.md) and the current package documentation.

## Historical alpha acceptance checklist

Before `0.1.0-alpha.1`, the repository was expected to show:

* documented Core domain language
* documented Core package boundary
* README language that avoided overclaiming
* no Core dependency on ASP.NET Core, EF Core, NetCoreApplicationTemplate, or AI model packages
* XML documentation for public Core primitives as they were introduced
* tests proving basic decision outcome behavior once implementation began

## Stable-release caution

Names introduced in alpha were allowed to change before a stable `1.0.0` release. The conceptual boundary was expected to remain stable: Core defines host-neutral governance primitives; integration packages own persistence, web, signing, storage, samples, and external execution concerns.

That stable-era boundary is now tracked through the current stable documentation rather than this historical alpha note.

## Related documentation

- [1.2.1 Release Notes](release-notes-121.md)
- [1.2.0 Release Notes](release-notes-120.md)
- [1.1.x Release Notes](release-notes-110.md)
- [API Compatibility and Semantic Versioning](api-compatibility-and-semver.md)
- [Historical Stable API Review](stable-api-review.md)
- [Core Domain Language](core-domain-language.md)
- [Historical Core Alpha Readiness Review](core-alpha-readiness-review.md)
