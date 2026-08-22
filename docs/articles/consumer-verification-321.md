# AsiBackbone 3.2.1 Consumer Verification Guide

This guide helps consumers verify the `3.2.1` AsiBackbone package family after
publication.

`3.2.1` is a backward-compatible patch release for the stable `3.2.x` line.
It is the first release prepared after the canonical AsiBackbone repository
was transferred from the personal `cdcavell` GitHub namespace to the dedicated
`AsiBackbone` organization.

The repository-transfer work changes canonical source, documentation, Source
Link, citation, SBOM, provenance, and package metadata locations. It does not
introduce a public API migration or change runtime governance behavior.

This guide does not claim that the packages are NuGet-signed, independently
audited, certified, or reproducibly built by every consumer environment.

## Confirm the package source

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
3.2.1
```

Confirm that no unexpected package source overrides the intended NuGet source
order in the consuming environment.

## Confirm the compatibility boundary

For `3.2.1`, verify:

* target framework: `net10.0`;
* package version: `3.2.1`;
* assembly version: `3.0.0.0`;
* file version: `3.2.1.0`;
* repository type: `git`;
* repository URL:
  `https://github.com/AsiBackbone/AsiBackbone`;
* project URL:
  `https://asibackbone.github.io/AsiBackbone/`; and
* public package IDs and namespaces remain in the existing
  `AsiBackbone.*` family.

`3.2.1` does not introduce a public API or runtime governance behavior
migration.

Consumers upgrading from `3.2.0` to `3.2.1` should not require source-code
changes solely because of this patch release.

Rebuild consumers after updating package references and run the host's
applicable policy, audit, acknowledgment, capability, outbox, signing,
actor-context, execution-accountability, endpoint-governance, and integration
tests.

## Verify the repository-transfer boundary

Beginning with `3.2.1`, the canonical source repository is:

```text
https://github.com/AsiBackbone/AsiBackbone
```

The canonical documentation and project site is:

```text
https://asibackbone.github.io/AsiBackbone/
```

Consumers inspecting packages released before the repository transfer may
still see the historical repository URL:

```text
https://github.com/cdcavell/AsiBackbone
```

Earlier packages may also contain the historical project URL:

```text
https://cdcavell.github.io/AsiBackbone/
```

That is expected.

NuGet packages are immutable after publication. The repository transfer does
not rewrite repository or project metadata embedded in earlier package
versions.

For `3.2.1`, the expected repository and project metadata are the
organization-owned locations.

## Confirm the release is a patch release

`3.2.1` should be treated as a patch release in the stable `3.2.x` line.

Its primary release scope is the transition of canonical project ownership and
associated metadata from the personal GitHub namespace to the dedicated
organization.

The capability-grant validation profiles present in the `3.2.x` package
family were introduced in `3.2.0` and are carried forward unchanged by
`3.2.1`.

The release should not be interpreted as introducing those APIs or changing
their semantics.

## Review carried-forward capability-validation behavior

The capability-grant validation profiles introduced in `3.2.0` remain
available in `3.2.1` without an intentional behavioral change.

### Consequential execution boundaries

Consequential-execution and operational-gateway code should prefer:

```csharp
CapabilityGrantValidationOptions.CreateExecutionBoundary(...)
```

This profile requires signed-artifact proof verification and enables
bounded-use validation by default with `maxUseCount: 1`.

If a host explicitly sets:

```csharp
requireUseCheck: false
```

verify that replay or use enforcement is performed atomically by another
trusted execution boundary and that this responsibility is documented in the
host threat model.

A missing required proof verifier should cause execution-boundary validation
to fail closed.

A missing required use store should produce a defer outcome rather than
silently broadening authority.

### Intentional metadata-only validation

Use:

```csharp
CapabilityGrantValidationOptions.CreateMetadataValidation(...)
```

only when proof verification and bounded-use enforcement are intentionally
outside the current validation step.

A successful metadata-only result establishes only that the configured
structural, temporal, scope, policy, acknowledgment, handshake, gateway,
resource-binding, or related metadata checks passed.

It does not establish:

* proof authenticity;
* replay resistance;
* authentication;
* authorization;
* host permission to execute an external action; or
* operational safety.

### Legacy 3.x compatibility path

Existing calls to:

```csharp
CapabilityGrantValidationOptions.Create(...)
```

retain their established `3.x` configuration behavior.

Existing calls to:

```csharp
CapabilityGrantValidator.ValidateAsync(signedGrant)
```

without explicit validation options also retain the existing `3.x` behavior.

The no-options path does not automatically establish proof verification or
bounded-use/replay enforcement and should not be treated as an
execution-boundary validation profile.

## Verify Source Link repository metadata

After the `3.2.1` packages are available from NuGet, run:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.1
```

The validation should confirm that each applicable package exposes:

```text
Repository type:
git

Repository URL:
https://github.com/AsiBackbone/AsiBackbone
```

Each package should also expose a non-empty repository commit value.

The repository commit should resolve to the source revision used for the
published package and should correspond to the final source revision associated
with the `v3.2.1` release.

Source Link improves source traceability but is not equivalent to package
signing or independent supply-chain attestation.

## Verify NuGet project metadata

Inspect the generated or published `.nuspec` metadata and confirm that the
project URL is:

```text
https://asibackbone.github.io/AsiBackbone/
```

The repository URL and project URL serve separate purposes:

```text
Repository:
https://github.com/AsiBackbone/AsiBackbone

Documentation / project site:
https://asibackbone.github.io/AsiBackbone/
```

The repository URL should identify the canonical source repository.

The project URL should identify the canonical documentation and project site.

## Inspect package contents

For higher-assurance adoption, download the `.nupkg` and `.snupkg` artifacts
and inspect them directly.

Verify:

* `.nuspec` package ID;
* package version;
* target-framework assets;
* dependency versions;
* repository type;
* repository URL;
* repository commit;
* project URL;
* packaged README content;
* package icon;
* symbol package contents;
* source-document mappings;
* expected assemblies and content;
* absence of unexpected executable tooling or payloads; and
* release SBOM and provenance artifacts where available.

Consumers may also retain cryptographic hashes of downloaded release artifacts
in their own dependency-approval or release record.

## Verify package version and assembly identity

For package assemblies, verify the expected release metadata:

```text
Package version:
3.2.1

AssemblyVersion:
3.0.0.0

FileVersion:
3.2.1.0
```

The stable `AssemblyVersion` is intentionally retained for the compatible
`3.x` binary line.

The NuGet package version and `FileVersion` advance with the patch release.

## Verify template fallback package references

Where `AsiBackbone.Templates` generates a host that cannot resolve local
repository project references, verify that fallback package references use:

```text
3.2.1
```

for the applicable packages, including:

* `AsiBackbone.AspNetCore`;
* `AsiBackbone.Core`;
* `AsiBackbone.Storage.InMemory`; and
* `AsiBackbone.Analyzers`.

Generated applications should restore and build successfully using the
published `3.2.1` packages.

## Verify SBOM metadata

Where package SBOMs are provided, inspect the SPDX artifacts and confirm that
the `3.2.1` release metadata references the organization-owned project location.

Expected canonical repository identity:

```text
https://github.com/AsiBackbone/AsiBackbone
```

The SBOM should correspond to the expected `3.2.1` package artifact and release
source revision.

An SBOM documents component composition and related metadata. It does not by
itself authenticate the package publisher or prove that the package has not
been modified.

## Verify provenance artifacts

Where provenance artifacts are produced by the release workflow, confirm that
they correspond to:

* the expected `3.2.1` package artifacts;
* the expected release workflow;
* the expected repository;
* the expected source revision; and
* the expected `v3.2.1` release.

A provenance artifact is an additional supply-chain signal and should not be
treated as equivalent to NuGet package signing.

## Package-signing status

Current AsiBackbone NuGet packages are intentionally published without NuGet
package signing while the project is independently maintained.

Do not interpret any of the following as a signed-package guarantee:

* Source Link metadata;
* repository commit metadata;
* SBOMs;
* provenance artifacts;
* GitHub release tags;
* public repository ownership;
* public source availability; or
* release documentation.

These are complementary traceability and supply-chain signals.

Consumers requiring signed packages should enforce that requirement through
their own dependency-management policy.

## Verify the release record

Compare the published packages and release artifacts with:

* the `v3.2.1` Git tag;
* the GitHub `3.2.1` release;
* `CHANGELOG.md`;
* `docs/articles/release-notes-321.md`;
* `docs/articles/release-readiness-321.md`;
* `docs/articles/consumer-verification-321.md`;
* `CITATION.cff`;
* `.zenodo.json`;
* generated package SBOMs;
* provenance artifacts where available; and
* CI, CodeQL, dependency-review, OpenSSF, workflow-security, OWASP,
  package-validation, template, documentation, and release-validation results
  associated with the final release commit.

A missing or inconsistent artifact should be investigated rather than silently
treated as equivalent evidence.

## Verify release metadata consistency

The following release-facing values should agree for `3.2.1`:

```text
Version:
3.2.1

Release date:
2026-08-07

Repository:
https://github.com/AsiBackbone/AsiBackbone

Project site:
https://asibackbone.github.io/AsiBackbone/

Target framework:
net10.0

AssemblyVersion:
3.0.0.0

FileVersion:
3.2.1.0
```

Check these values across applicable:

* MSBuild metadata;
* NuGet package metadata;
* `CITATION.cff`;
* `.zenodo.json`;
* release notes;
* changelog;
* generated packages;
* Source Link metadata;
* SBOMs; and
* release assets.

## Verify historical metadata is not incorrectly rewritten

Historical package verification should preserve the metadata that was actually
published with those package versions.

For example, a package published before the repository transfer may correctly
contain:

```text
https://github.com/cdcavell/AsiBackbone
```

That should not be treated as a defect in an immutable historical package.

Conversely, a newly published `3.2.1` package should use:

```text
https://github.com/AsiBackbone/AsiBackbone
```

This distinction preserves an accurate provenance trail across the repository
transfer.

## Verify no runtime-contract migration is implied

The repository transfer does not intentionally change:

* policy-evaluation semantics;
* governance-decision semantics;
* constraint handling;
* threat-model contributor behavior;
* acknowledgment behavior;
* capability-grant validation semantics;
* audit-residue contracts;
* lifecycle event contracts;
* governed execution receipt behavior;
* outbox contracts;
* signing abstractions;
* ASP.NET Core endpoint-governance behavior;
* package IDs;
* public namespaces;
* target framework;
* binary assembly identity; or
* host-owned execution authority.

A consumer upgrading from `3.2.0` to `3.2.1` should not need application
source changes solely because the project repository moved to the dedicated
GitHub organization.

## Verify host-owned responsibility boundaries

Package verification does not replace host-owned:

* authentication;
* authorization;
* identity-provider validation;
* claim provenance validation;
* policy registration;
* resource authorization;
* capability issuance;
* capability replay enforcement;
* execution enforcement;
* durable storage;
* transaction management;
* signing-key custody;
* production key management;
* network controls;
* DLP or classification policy;
* external provider configuration;
* monitoring;
* alerting;
* incident response;
* legal review;
* regulatory analysis; or
* compliance interpretation.

AsiBackbone provides governance-oriented software primitives. The consuming
application remains responsible for deciding whether the package, its release
evidence, and its operational controls meet the host's own risk requirements.

## Suggested consumer verification sequence

A cautious consumer may use the following sequence:

1. Confirm the package source.
2. Confirm the expected package ID.
3. Confirm package version `3.2.1`.
4. Confirm target framework `net10.0`.
5. Confirm `AssemblyVersion` `3.0.0.0`.
6. Confirm `FileVersion` `3.2.1.0`.
7. Confirm repository URL
   `https://github.com/AsiBackbone/AsiBackbone`.
8. Confirm project URL
   `https://asibackbone.github.io/AsiBackbone/`.
9. Confirm repository commit metadata is present.
10. Resolve the repository commit against the public source repository.
11. Compare the commit with the `v3.2.1` release source.
12. Inspect `.nupkg` metadata and contents.
13. Inspect `.snupkg` contents where applicable.
14. Review the `3.2.1` release notes.
15. Review the `3.2.1` release readiness record.
16. Review `CHANGELOG.md`.
17. Review `CITATION.cff` and `.zenodo.json`.
18. Review package SBOMs where available.
19. Review provenance artifacts where available.
20. Review relevant release workflow and security-validation results.
21. Run the consuming application's own regression and governance tests.
22. Record the approved package version and artifact hashes in the consumer's
    own dependency-management process where appropriate.

## Final verification statement

A verified `3.2.1` package should identify the dedicated `AsiBackbone`
organization as its canonical source location while preserving the stable
software contract of the `3.2.x` package family.

The expected canonical repository is:

```text
https://github.com/AsiBackbone/AsiBackbone
```

The expected canonical project and documentation site is:

```text
https://asibackbone.github.io/AsiBackbone/
```

The release preserves:

```text
Package IDs:
AsiBackbone.*

Public namespaces:
AsiBackbone.*

Target framework:
net10.0

AssemblyVersion:
3.0.0.0
```

`3.2.1` is a repository-transfer and release-metadata patch.

It does not transfer execution authority from the host to AsiBackbone, and it
does not make AsiBackbone an authentication system, authorization system,
production key-management system, production replay-protection system,
compliance certification, or intelligence engine.

