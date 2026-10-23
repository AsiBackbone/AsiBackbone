# Central Transitive Pinning Decision Record

| Field | Decision |
| --- | --- |
| Status | Accepted, with mandatory review |
| Decision date | 2026-10-23 |
| Decision owner | [@cdcavell](https://github.com/cdcavell), Core Maintainer |
| Scope | NuGet central package management for the repository and the published `AsiBackbone.*` package family |
| Current decision | Keep `CentralPackageTransitivePinningEnabled` disabled |
| Mechanical enforcement | `Directory.Packages.props` sets the property to `false`; `scripts/Validate-NuGetPackageMetadata.ps1` fails if it is not `false` and checks every generated package's declared dependencies |
| Next mandatory review | Before the first `8.0.0` release candidate, or earlier when a trigger below occurs |
| Tracking issue | [#902](https://github.com/AsiBackbone/AsiBackbone/issues/902) |

## Context

The repository manages package versions centrally in `Directory.Packages.props`.
NuGet's optional transitive pinning lets a central `PackageVersion` override a
package that a project only receives transitively. Application repositories in
the organization, including NetCoreApplicationTemplate and ReferenceApplication,
enable it. That is not by itself a reason to enable it here: an application is
the end of its dependency graph, while every AsiBackbone package is a dependency
in someone else's graph.

The deciding behavior is what pinning does when a project is packed. NuGet
documents that it promotes a transitively pinned package to an explicit
dependency in the generated `.nuspec`, so the package keeps working with the
version it was built against, and it advises library authors to evaluate
transitive pinning carefully because it can produce dependencies they did not
expect ([Central Package Management: Transitive pinning](https://learn.microsoft.com/nuget/consume-packages/central-package-management#transitive-pinning)).

## Review

| Consideration | Finding |
| --- | --- |
| Repository restore reproducibility | Every project commits a `packages.lock.json` recording the full resolved graph, including transitive packages, and every CI, stable release validation, package publish, documentation, and OWASP restore runs with `--locked-mode`. A transitive version cannot change in a repository build without a reviewed lock-file change. Pinning would add no reproducibility. |
| Central versions | Every `PackageVersion` in `Directory.Packages.props` is a direct `PackageReference` of at least one project. The file centralizes direct versions; it does not currently exist to steer transitive ones. |
| Exposure to NuGet consumers | A shipped package declares only its direct package references and its AsiBackbone project references, each with a minimum version. Consumers resolve everything else in their own graph. |
| Implementation details as consumer constraints | The lock files already mark the central versions that pinning would apply to as `CentralTransitive`. In shipped packages these are `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options`, and `Microsoft.Extensions.Logging.Abstractions`, received through EF Core or through `AsiBackbone.Core` and `AsiBackbone.DependencyInjection`, in `AsiBackbone.EntityFrameworkCore`, `AsiBackbone.OpenTelemetry`, `AsiBackbone.Signing.LocalDevelopment`, `AsiBackbone.Signing.ManagedKey`, and `AsiBackbone.Storage.InMemory`. Enabling pinning would make these candidates for promotion into those packages' declared dependencies, at the repository's chosen versions, and a version chosen for a test or sample could become a consumer floor the package's own code does not require. |
| Tests and samples | Test, sample, and benchmark projects are not packed. Where one needs a different transitive version, it adds an explicit reference in that project only: `System.Security.Cryptography.Pkcs` in `AsiBackbone.EntityFrameworkCore.Tests` for [#870](https://github.com/AsiBackbone/AsiBackbone/issues/870), `SQLitePCLRaw.bundle_e_sqlite3` in the SQLite-backed test and sample, and `MessagePack` in the Aspire AppHost sample. The lift is scoped to the graph that needs it and is visible in the project file. |
| Vulnerability detection | NuGet audit evaluates the restored graph, transitive packages included, and the repository treats warnings as errors; #870 was found because a transitive advisory failed repository builds. Pull-request dependency review blocks newly introduced dependencies at moderate severity or higher, and OWASP Dependency-Check scans the restored transitive graph. |
| Dependabot | Dependabot proposes updates to the central `PackageVersion` entries, with grouping and a seven-day cooldown, and those pull requests regenerate the lock files under the same locked-restore checks. Because no transitive-only entries are pinned, there are no pins to keep current that could silently hold a transitive package back. |
| Package-consumer validation | The external consumer and stable package integration smoke tests restore the packed packages in clean consumer projects with no repository references, so they exercise the dependencies a consumer actually receives. |

## Decision

`CentralPackageTransitivePinningEnabled` stays disabled. `Directory.Packages.props`
sets it to `false` explicitly, with a comment pointing to this record, so the
absence of pinning is a recorded choice rather than a default.

Each shipped package's consumer-visible dependency surface is now a reviewed
contract. `scripts/Validate-NuGetPackageMetadata.ps1`, which runs in stable release
validation on every pull request and in package publish, compares each generated
package's dependency IDs and framework references with a reviewed list. It
checks package dependency versions against `Directory.Packages.props` and
AsiBackbone dependency versions against the package version, so a routine
version update passes while an added, removed, or promoted dependency fails.

## Handling a vulnerable or outdated transitive package

- **Repository-only graph** (tests, samples, benchmarks, tooling): add an explicit
  `PackageReference`, with its `PackageVersion`, in the affected project only, and
  regenerate that project's lock file. Do not add it to a shipped project.
- **Shipped package graph**: prefer raising the direct dependency that brings the
  transitive package in, so the consumer floor moves through a dependency the
  package already declares. If an explicit reference in the shipped project is
  the only fix, treat it as a deliberate consumer-facing change: add it to the
  package's reviewed dependencies in `Validate-NuGetPackageMetadata.ps1`, record it
  under the release's compatibility notes, and re-review this decision.

## Accepted residual risk

Repository lock files do not constrain consumers. A consumer can resolve a
transitive package at any version its own graph allows above the floors that
AsiBackbone's direct dependencies declare, including a version the repository
never tested. That is the normal contract for a NuGet library, and it leaves
consumers free to choose their own versions without inheriting the repository's
test-only choices. Consumers that need exact transitive control should use their
own lock files, central management, transitive pinning, and NuGet audit in their
applications.

## Mandatory early re-evaluation criteria

Re-evaluate this decision before the scheduled review when any of the following
occurs:

1. A shipped package needs a consumer-facing floor on a transitive package, for
   example to address a vulnerability that raising a direct dependency cannot fix.
2. The dependency check in `Validate-NuGetPackageMetadata.ps1` fails because a
   transitive package would be promoted into a shipped package.
3. `Directory.Packages.props` gains a `PackageVersion` that no project references
   directly, which would only take effect through pinning.
4. NuGet changes how transitive pinning affects pack, for example by allowing a
   pin that is not promoted to package dependencies.
5. A new shipped package, target framework, or dependency family changes the
   reviewed dependency surface substantially.

## Validation for the selected posture

The selected posture is validated with the repository's normal checks. Setting
the property explicitly to `false` does not change the restore graph, so the
committed lock files remain valid.

```bash
dotnet restore AsiBackbone.slnx --locked-mode -p:Configuration=Release
dotnet build AsiBackbone.slnx --configuration Release --no-restore
dotnet test --solution AsiBackbone.slnx --configuration Release --no-build --no-restore
bash ./eng/smoke-tests/external-consumer-smoke.sh
bash ./eng/smoke-tests/stable-package-integration-smoke.sh
```

Pack every package project under `src`, excluding template-content projects, then
run `./scripts/Validate-NuGetPackageMetadata.ps1 -PackageDirectory <package output>`.
Stable release validation does this on every pull request.

## Related documents

- [Stable Release Validation](release-validation.md)
- [Supply-Chain Provenance and Package SBOMs](supply-chain-provenance.md)
- [NuGet Package Signing Decision Record](nuget-package-signing-decision.md)
- [Contributing: Dependency changes](https://github.com/AsiBackbone/AsiBackbone/blob/main/CONTRIBUTING.md#dependency-changes)
