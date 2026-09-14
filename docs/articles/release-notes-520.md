# AsiBackbone 5.2.0 Release Notes

Release date: 2026-09-14

## Summary

`5.2.0` is a backward-compatible minor release for the stable `5.x`
AsiBackbone package family. The principal public API addition is a fluent builder
for `DefaultAsiBackbonePolicyEvaluator<TContext>`. The release also begins the
`ASIB900` deprecation window for five partial evaluator constructors that remain
available in `5.2.0` and are planned for removal in `6.0`.

The release carries forward post-`5.1.0` work that strengthens package
compatibility validation, durable release evidence, documentation quality,
repository quality gates, and project stewardship.

Package IDs, public namespaces, and the `net10.0` target remain unchanged.
`AssemblyVersion` remains `5.0.0.0`; package and file versions advance to
`5.2.0` and `5.2.0.0` respectively.

## Added

- Added `DefaultAsiBackbonePolicyEvaluator.CreateBuilder<TContext>()`.
- Added `AsiBackbonePolicyEvaluatorBuilder<TContext>` for fluent configuration
  of constraints, threat-model contributors, decision policy, evaluator options,
  and logging.
- Added an opt-in adopter registry and structured adopter-reporting path.
- Added evidence-based contributor, triager, and Core Maintainer progression and
  onboarding guidance.
- Added durable GitHub release publication and post-upload validation for package
  SBOMs, package/hash mappings, exact release notes, and the release-evidence
  manifest.
- Added a dated NuGet package-signing decision record with accepted residual
  risk, compensating controls, mandatory review, and event-driven re-evaluation
  criteria.

## Deprecated

Five partial-argument
`DefaultAsiBackbonePolicyEvaluator<TContext>` constructor overloads are obsolete
with diagnostic `ASIB900`.

They remain callable in `5.2.0`. Their documented removal target is `6.0`. Use
one of these supported paths:

```csharp
DefaultAsiBackbonePolicyEvaluator.CreateBuilder<TContext>()
```

or the constructor that accepts all dependencies. The all-dependencies
constructor remains supported for dependency-injection activation.

Projects that treat warnings as errors must migrate affected direct constructor
calls or temporarily suppress `ASIB900` while completing the migration.

See [ASIB900: Obsolete Policy Evaluator Constructors](asib900-policy-evaluator-constructors.md).

## Changed

- Compiler, analyzer, and code-style warnings are treated as errors
  repository-wide, while the existing XML-documentation inventory behavior is
  preserved outside enforcement mode.
- XML-documentation baseline ceilings are calibrated to observed package
  inventories so new documentation debt cannot silently increase.
- Stable package validation continues to compare the `5.2.0` candidate against
  the published `5.1.0` package baseline.
- Release evidence distinguishes retention-limited GitHub Actions artifacts from
  durable public release assets and documents subject-digest provenance
  verification.
- The original `v5.0.0` and `v5.1.0` package SBOM evidence was backfilled onto
  those GitHub releases without rebuilding either tag.

## Fixed

- XML-documentation enforcement now handles empty staged-project sets, Windows
  project paths, incremental build behavior, and duplicate/cross-project
  `CS1591` counting correctly.

## Compatibility

- No stable public member is removed in `5.2.0`.
- The evaluator builder is additive.
- The five `ASIB900` constructors remain present for the deprecation window.
- The all-dependencies evaluator constructor remains supported for DI.
- Package IDs and public namespaces remain unchanged.
- The target framework remains `net10.0`.
- `AssemblyVersion` remains `5.0.0.0`.
- NuGet package author signing remains intentionally deferred.

Consumers already on `5.1.0` can update to `5.2.0`, rebuild, and run their
normal host validation. Consumers moving from `4.x` must still follow the
[5.0.0 Migration Guide](upgrade-400-to-500.md).

## Release evidence and package signing

The stable release workflow should retain the attested build packages, matching
SPDX SBOMs, `sbom-manifest.json`, `release-evidence-manifest.json`, and a copy of
these release notes as durable GitHub release assets.

NuGet.org repository-signs packages during ingestion. That repository signature
is a separate trust signal from GitHub build-package attestations and does not
turn the project into an author-signed package publisher.

The current author-signing decision remains documented in the
[NuGet Package Signing Decision Record](nuget-package-signing-decision.md).

## Validation

The final release candidate should pass:

- version consistency for `5.2.0` and `v5.2.0`;
- locked restore and Debug/Release solution builds;
- formatting and warning-as-error checks;
- the complete test suite;
- public API and package-validation checks against `5.1.0`;
- XML-documentation validation;
- DocFX, documentation-link, continuity, and release-claim validation;
- package creation, NuGet metadata validation, and package SBOM generation;
- template, external-consumer, and stable-package smoke tests; and
- required security, dependency, workflow-security, and supply-chain checks.

After publication, validate Source Link repository commit metadata with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 5.2.0
```

See the [5.2.0 Consumer Verification Guide](consumer-verification-520.md) and
[5.2.0 Release Readiness Record](release-readiness-520.md).
