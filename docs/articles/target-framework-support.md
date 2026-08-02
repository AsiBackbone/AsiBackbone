# Target Framework Support Decision Record

## Status

Accepted for the stable `3.2.x` package family.

## Decision

The `3.2.x` AsiBackbone package family stays .NET 10+ by targeting `net10.0`. The project will not multi-target .NET 8 for the `3.2.x` release line.

This decision applies to released package projects, templates, samples, analyzers, tests, smoke validation, and release documentation unless a later release explicitly changes the supported target-framework posture.

## Current support boundary

Consumers should use a .NET 10 SDK/runtime or later when adopting the `3.2.x` packages.

The repository uses a single shared target framework in `Directory.Build.props`:

```xml
<TargetFramework>net10.0</TargetFramework>
```

That single target keeps the package family, sample hosts, template smoke tests, analyzer validation, packaging validation, Source Link checks, SBOM/provenance output, and release documentation aligned around one current platform baseline.

## Context

Issue #513 evaluated whether AsiBackbone should broaden adoption by multi-targeting framework-neutral packages, especially for .NET 8 consumers.

The decision is to document the intentional boundary instead of refactoring the package family to support .NET 8 for only a short adoption window. The main considerations are:

- `AsiBackbone.EntityFrameworkCore` aligns with centrally managed EF Core `10.0.x` package references.
- The current package family already has a broad validation surface: Core, ASP.NET Core, EF Core, OpenTelemetry, analyzers, templates, samples, tests, package metadata, SBOM/provenance, and documentation.
- Multi-targeting would require coordinated updates across CI, packaging validation, analyzer compatibility, template smoke tests, consumer verification, release notes, and published documentation.
- .NET 8 is in maintenance support and reaches end of support on November 10, 2026, according to [Microsoft's .NET support policy](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).
- No current external consumer demand has been identified that justifies adding the compatibility burden to the `3.2.x` line.

## Consequences

### Positive

- Keeps the `3.2.x` line simple, current, and easier to validate.
- Avoids partial compatibility where only some packages support an older TFM.
- Keeps EF Core, ASP.NET Core, analyzers, templates, and smoke tests on one platform baseline.
- Reduces release risk before wider public promotion.

### Tradeoffs

- Consumers still on .NET 8 cannot adopt the `3.2.x` packages without upgrading their host application.
- Some framework-neutral surfaces, such as `AsiBackbone.Core`, might technically be easier to multi-target than provider packages, but partial targeting would add documentation and expectation-management risk.
- Adoption may be narrower until more hosts move to .NET 10 or later.

## Revisit criteria

Reconsider additional TFM support only if at least one of the following becomes true:

- real external consumer demand appears from a host that cannot move to .NET 10+;
- a downstream integration partner needs a narrower compatibility surface for a specific package;
- a future major release chooses a different platform baseline;
- tooling changes make multi-targeting low-risk across analyzers, templates, CI, smoke tests, and package validation.

If multi-targeting is pursued later, it should be handled as a coordinated release change, not a project-by-project patch. The implementation should update:

- project files and central build properties;
- CI restore/build/test matrices;
- analyzer compatibility validation;
- EF Core and ASP.NET Core dependency compatibility checks;
- template generation and template smoke tests;
- package metadata validation;
- Source Link, SBOM, and provenance validation;
- README, release notes, consumer verification guidance, and DocFX navigation.

## Consumer wording

Use this wording for the `3.2.x` line:

> AsiBackbone `3.2.x` targets `net10.0`. Consumers should use a .NET 10 SDK/runtime or later. The project does not multi-target .NET 8 for this release line; compatibility can be reconsidered in a future release if real consumer demand justifies the full validation surface.

Avoid wording that implies .NET 8 support is planned, promised, or blocked by a single package defect. The current posture is an intentional support-boundary decision.

## Related documentation

- [3.2.0 Release Notes](release-notes-320.md)
- [3.2.0 Consumer Verification Guide](consumer-verification-320.md)
- [3.1.0 Release Notes](release-notes-310.md)
- [3.1.0 Consumer Verification Guide](consumer-verification-310.md)
- [Implementation-First Adoption Path](implementation-first-adoption.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
