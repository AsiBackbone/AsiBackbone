# AsiBackbone 3.2.2 Consumer Verification Guide

This guide helps consumers verify the `3.2.2` AsiBackbone package family after
publication.

`3.2.2` is a backward-compatible maintenance patch for the stable `3.2.x`
line. It refreshes approved dependency and SHA-pinned workflow/security inputs
without introducing a public API or runtime governance migration.

This guide does not claim that the packages are NuGet-signed, independently
audited, certified, or reproducibly built by every consumer environment.

## Confirm the package source and identity

Install release packages from the official NuGet source and confirm the package
owner, package ID, and version before adoption.

Expected package IDs:

- `AsiBackbone.Core`
- `AsiBackbone.DependencyInjection`
- `AsiBackbone.Storage.InMemory`
- `AsiBackbone.EntityFrameworkCore`
- `AsiBackbone.AspNetCore`
- `AsiBackbone.Testing`
- `AsiBackbone.Templates`
- `AsiBackbone.Analyzers`
- `AsiBackbone.OpenTelemetry`
- `AsiBackbone.Signing.LocalDevelopment`
- `AsiBackbone.Signing.ManagedKey`

Verify that the selected package version is exactly:

```text
3.2.2
```

## Confirm the compatibility boundary

For `3.2.2`, verify:

- target framework: `net10.0`;
- package version: `3.2.2`;
- assembly version: `3.0.0.0`;
- file version: `3.2.2.0`;
- repository type: `git`;
- repository URL: `https://github.com/AsiBackbone/AsiBackbone`;
- project URL: `https://asibackbone.github.io/AsiBackbone/`; and
- package IDs and public namespaces remain in the `AsiBackbone.*` family.

Consumers upgrading from `3.2.1` to `3.2.2` should not require source-code
changes solely because of this patch release. Rebuild the consuming application
and run its applicable policy, audit, acknowledgment, capability, outbox,
signing, actor-context, execution-accountability, endpoint-governance, and
integration tests.

## Verify the maintenance scope

The release should be treated as dependency, CI/security-tooling, and repository
hygiene maintenance. Review the release notes and confirm that no new runtime
source or public surface is being adopted as part of the version change.

The notable centrally managed dependency updates are:

- EF Core family `10.0.10` -> `10.0.11`;
- `Microsoft.Extensions.Logging.Abstractions` `10.0.10` -> `10.0.11`; and
- `Microsoft.NET.Test.Sdk` `18.8.1` -> `18.9.0`.

The release also refreshes SHA-pinned workflow/security actions. These are
repository release-process inputs rather than runtime package APIs.

## Verify Source Link repository metadata

After the `3.2.2` packages are available from NuGet, run:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.2
```

The validation should confirm that each applicable package exposes:

```text
Repository type:
git

Repository URL:
https://github.com/AsiBackbone/AsiBackbone
```

Each package should expose a non-empty repository commit value corresponding to
the final source revision associated with the `v3.2.2` release. Source Link
improves source traceability but is not equivalent to package signing.

## Verify NuGet project metadata

Inspect the generated or published `.nuspec` metadata and confirm the project
URL remains:

```text
https://asibackbone.github.io/AsiBackbone/
```

For the historical `3.2.2` release, the organization-owned source repository
and documentation site established in `3.2.1` remained canonical.

## Verify package version and assembly identity

For package assemblies, verify:

```text
Package version:
3.2.2

AssemblyVersion:
3.0.0.0

FileVersion:
3.2.2.0
```

The fixed assembly version preserves the compatible `3.x` binary identity.

## Verify template fallback package references

Where `AsiBackbone.Templates` cannot resolve repository project references,
verify that generated fallback package references use `3.2.2` for:

- `AsiBackbone.AspNetCore`;
- `AsiBackbone.Core`;
- `AsiBackbone.Storage.InMemory`; and
- `AsiBackbone.Analyzers`.

Generated applications should restore and build successfully using the
published `3.2.2` packages.

## Inspect package contents

For higher-assurance adoption, inspect downloaded `.nupkg` and `.snupkg`
artifacts directly. Verify package ID/version, target-framework assets,
dependency metadata, repository/project metadata, repository commit, packaged
README/icon content, symbols/source mappings, expected assemblies, and the
absence of unexpected executable payloads.

## Verify SBOM and provenance artifacts

Where release SBOMs and provenance artifacts are provided, confirm that they
refer to the expected `3.2.2` package artifacts, canonical repository, and
release source revision.

An SBOM describes component composition. Provenance records build-related
claims. Neither is, by itself, equivalent to NuGet package signing or proof of
consumer-environment reproducibility.

## Package signing status

NuGet package signing remains intentionally deferred. Consumers with mandatory
package-signing requirements should enforce those requirements through their own
dependency policy rather than treating Source Link, SBOMs, provenance, public
source, or release tags as substitutes for a package signature.

## Recommended consumer upgrade sequence

1. Review the `3.2.2` release notes and compatibility boundary.
2. Confirm the package source and exact package IDs.
3. Update required package references to `3.2.2`.
4. Restore and rebuild the consuming application.
5. Run host-owned tests for governed execution paths.
6. Inspect package metadata and release artifacts according to local assurance
   requirements.
7. Confirm no source migration was required solely because of the patch.

## Related documentation

- [3.2.2 Release Notes](release-notes-322.md)
- [3.2.2 Release Readiness Record](release-readiness-322.md)
- [Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Supply-Chain Provenance and Package SBOMs](supply-chain-provenance.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
