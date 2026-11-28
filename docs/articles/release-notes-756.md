# AsiBackbone 7.5.6 Release Notes

Release date: 2026-11-27

`7.5.6` is a backward-compatible governance, dependency, documentation, and
release-validation maintenance patch for the stable `7.x` package family. It
establishes proposal and assurance-agent guardrails, improves project entry
points, services development dependencies and GitHub Actions, and proves that
the packed analyzer executes under the compiler shipped with the .NET 10 SDK.
Package and file versions advance to `7.5.6` and `7.5.6.0`; binary assembly
identity remains `7.0.0.0`.

## Governance and contribution controls

- Four read-only assurance-agent profiles now support security, architecture,
  quality, and release review. Maintainer guidance and a fail-closed validator
  define their evidence, handoff, and authority boundaries.
- Proposal-first contribution guidance routes cross-repository ideas through
  organization Discussions. AI-assisted proposals require visible disclosure,
  duplicate checks, and a responsible human poster; only human maintainers
  decide and authorize organizational work.
- The README and documentation homepage now lead with a plain-language project
  description and clearer routes for evaluators, adopters, integrators,
  contributors, and maintainers.

## Dependency and workflow servicing

- The Aspire AppHost sample advances from 13.6.5 to 17.0.0 and adopts the split
  `Aspire.Hosting.Dotnet` package required by that toolchain.
- Npgsql.EntityFrameworkCore.PostgreSQL advances from 10.0.3 to 10.0.4,
  coverlet.MTP from 10.1.0 to 10.1.1, and ReportGenerator from 5.6.0 to 5.6.1.
- Pinned GitHub Actions receive maintenance updates.
- These are sample, test, tool, and workflow dependencies. Dependencies
  declared by shipped `AsiBackbone.*` runtime packages do not change.

## Analyzer compatibility and release validation

- Microsoft.CodeAnalysis.Analyzers and Microsoft.CodeAnalysis.CSharp remain on
  5.9.0, matching the compiler assembly shipped by .NET SDK 10.0.404.
- The packed external-consumer smoke test now requires the analyzer to emit
  `ASIB001` for a deliberately discarded governance decision, then requires a
  clean build after the consumer retains the decision. This detects analyzer
  load failures that package restore alone cannot expose.
- The protected package workflow now restores and builds both generated
  template host styles in locked mode against the exact packages it just
  packed, before attestation or publication can begin.
- Agent-governance regression self-tests prove weakened fixture configurations
  fail closed before the live repository configuration is validated.

## Compatibility

No stable public signature, durable schema, canonical wire value, package ID,
public namespace, target framework, documented default, shipped runtime
dependency declaration, or runtime decision behavior changes in this patch.
Managed assemblies retain `AssemblyVersion` `7.0.0.0`, and the package family
remains on `net10.0`.

## Upgrade actions

1. Update adopted `AsiBackbone.*` packages to `7.5.6` together.
2. Restore and inspect the resolved dependency graph, rebuild with warnings
   enabled, and run the consumer's integration tests.
3. When admitting GitHub release build packages, verify their Sigstore bundles
   against the exact release tag, source commit, and protected workflow
   identity described in the consumer verification guide.

Consumers moving from `6.x` must still complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

## Release verification

Stable publication occurs only from the protected `v7.5.6` tag after the
release-preparation pull request is merged to `main` and required hosted checks
pass on the release candidate. The date above is the planned release date;
publication and hosted evidence must be verified separately.

See also:

- [7.5.6 Release Readiness Record](release-readiness-756.md)
- [7.5.6 Consumer Verification Guide](consumer-verification-756.md)
- [7.5.5 Release Notes](release-notes-755.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
