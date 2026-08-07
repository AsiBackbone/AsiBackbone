# AsiBackbone 3.2.1 Release Readiness Record

Release candidate date: 2026-08-07

## Release intent

`3.2.1` is a backward-compatible patch release for the stable `3.2.x`
package family.

Its primary purpose is to establish the dedicated `AsiBackbone` GitHub
organization as the canonical project location following the transfer from
the personal `cdcavell` namespace.

The release updates repository, documentation, Source Link, citation, SBOM,
validation, workflow, sample, and package metadata while preserving the
existing package, namespace, target-framework, binary-identity, runtime,
and host-ownership boundaries.

The capability-grant validation profiles introduced in `3.2.0` are carried
forward unchanged.

This record is a pre-tag checklist. Do not create `v3.2.1` or publish
packages until every required validation is complete on the final
release-candidate commit.

## Included scope

- Update the canonical repository URL to
  `https://github.com/AsiBackbone/AsiBackbone`.
- Update the canonical documentation URL to
  `https://asibackbone.github.io/AsiBackbone/`.
- Update NuGet repository and project metadata for `3.2.1`.
- Update Source Link validation expectations for the organization-owned
  repository.
- Update `CITATION.cff` and `.zenodo.json` for the organization-owned
  repository and documentation locations.
- Update SBOM document namespace and creator metadata.
- Update repository badges, security links, documentation navigation,
  GitHub Pages references, samples, package READMEs, quality documentation,
  and development-diagnostics examples.
- Update references to NetCoreApplicationTemplate to use
  `AsiBackbone/NetCoreApplicationTemplate` where the canonical current
  repository is intended.
- Update `AsiBackbone.Templates` fallback package references to `3.2.1`.
- Preserve the capability-validation functionality introduced by `3.2.0`
  without changing its runtime behavior.
- Preserve the existing `AsiBackbone.*` package and namespace identity.
- Preserve the stable `3.x` binary assembly identity.
- Preserve the current deferred NuGet package-signing posture.

## Compatibility boundary

- Package IDs remain unchanged.
- Public namespaces remain unchanged.
- Public APIs remain unchanged.
- Runtime governance behavior remains unchanged.
- The target framework remains `net10.0`.
- `AssemblyVersion` remains `3.0.0.0` for the compatible `3.x` binary line.
- `FileVersion` advances to `3.2.1.0`.
- Package version advances to `3.2.1`.
- Informational version and release metadata advance to `3.2.1`.
- Citation and archival metadata advance to `3.2.1`.
- No new stable public API is introduced.
- No serialized or persisted governance artifact shape is intentionally
  changed.
- No package boundary is added or removed.
- Existing `3.2.x` consumers require no source-code migration solely because
  of this patch release.
- Authentication, authorization, durable replay protection, signing-key
  custody, persistence, external execution, monitoring, and operational
  safety remain host-owned responsibilities.
- NuGet package signing remains deferred while the project is independently
  maintained.

## Repository metadata transition

`3.2.1` establishes the organization-owned repository as the canonical
repository metadata location:

```text
https://github.com/AsiBackbone/AsiBackbone
````

The canonical documentation and project URL is:

```text
https://asibackbone.github.io/AsiBackbone/
```

Published packages from earlier releases may retain the historical repository
URL embedded when those immutable artifacts were created:

```text
https://github.com/cdcavell/AsiBackbone
```

Earlier packages may also retain the historical GitHub Pages project URL:

```text
https://cdcavell.github.io/AsiBackbone/
```

The repository transfer does not rewrite previously published NuGet packages.

Release validation for `3.2.1` should verify the new canonical locations
without rewriting historical package evidence.

## Version and metadata checklist

* [ ] `Directory.Build.props` resolves package version `3.2.1`.
* [ ] `AssemblyVersion` remains `3.0.0.0`.
* [ ] `FileVersion` is `3.2.1.0`.
* [ ] `TargetFramework` remains `net10.0`.
* [ ] `RepositoryUrl` is
  `https://github.com/AsiBackbone/AsiBackbone`.
* [ ] `RepositoryType` remains `git`.
* [ ] `PackageProjectUrl` is
  `https://asibackbone.github.io/AsiBackbone/`.
* [ ] `CITATION.cff` reports version `3.2.1`.
* [ ] `CITATION.cff` reports release date `2026-08-07`.
* [ ] `CITATION.cff` uses
  `https://github.com/AsiBackbone/AsiBackbone` as `repository-code`.
* [ ] `CITATION.cff` uses
  `https://asibackbone.github.io/AsiBackbone/` as the project URL.
* [ ] `.zenodo.json` reports version `3.2.1`.
* [ ] `.zenodo.json` describes `3.2.1` as a backward-compatible patch release.
* [ ] `.zenodo.json` identifies
  `https://github.com/AsiBackbone/AsiBackbone` as the canonical software
  repository.
* [ ] `.zenodo.json` references
  `https://github.com/AsiBackbone/NetCoreApplicationTemplate`.
* [ ] Template fallback package references use `3.2.1`.
* [ ] Source Link post-publication validation defaults to `3.2.1`.
* [ ] Source Link validation expects
  `https://github.com/AsiBackbone/AsiBackbone`.
* [ ] NuGet package metadata validation expects the organization repository
  and documentation URLs.
* [ ] SBOM generation uses the organization-owned repository in the SPDX
  document namespace.
* [ ] SBOM creator metadata reflects the organization-owned project identity.
* [ ] GitHub Pages publishing configuration uses
  `https://asibackbone.github.io/AsiBackbone`.
* [ ] Security-reporting links target the organization-owned repository.
* [ ] Repository badges target the organization-owned repository.
* [ ] Lock files remain consistent after release-version changes.
* [ ] Locked restore succeeds.
* [ ] `CHANGELOG.md` contains the `3.2.1` release entry.
* [ ] `CHANGELOG.md` and `release-notes-321.md` describe the same release
  scope and compatibility boundary.
* [ ] Evergreen documentation identifies `3.2.1` as the current patch release.
* [ ] Capability-validation functionality is attributed to `3.2.0` where
  release-specific attribution is required.
* [ ] Historical release records remain factually accurate regarding metadata
  embedded in previously published packages.

## Repository-transfer link checklist

* [ ] No active release-facing link unintentionally references
  `github.com/cdcavell/AsiBackbone`.
* [ ] No active documentation-site link unintentionally references
  `cdcavell.github.io/AsiBackbone`.
* [ ] Current NetCoreApplicationTemplate references use
  `AsiBackbone/NetCoreApplicationTemplate` where appropriate.
* [ ] Security reporting targets the organization-owned repository.
* [ ] Repository badges target the organization-owned repository.
* [ ] Documentation navigation targets the organization-owned repository.
* [ ] Package READMEs use the organization-owned documentation site.
* [ ] Sample and development-diagnostics URLs use the organization-owned
  documentation site.
* [ ] Current workflow references use the canonical repository and site where
  applicable.
* [ ] Current quality documentation uses the canonical documentation site.
* [ ] Current issue and repository references use the transferred repository
  location where historical accuracy does not require preservation of the
  former URL.

Historical text that intentionally documents a previous repository location
should not be rewritten merely to eliminate every occurrence of `cdcavell`.

Maintainer identity, author attribution, usernames, email addresses, and other
personal references should likewise remain unchanged unless independently
incorrect.

## Release documentation checklist

* [ ] `CHANGELOG.md` contains a `3.2.1` entry dated `2026-08-07`.
* [ ] `docs/articles/release-notes-321.md` exists and accurately describes
  the patch-release scope.
* [ ] `docs/articles/release-readiness-321.md` exists and contains this
  complete pre-tag checklist.
* [ ] `docs/articles/consumer-verification-321.md` exists and documents the
  consumer verification path.
* [ ] `README.md` identifies `3.2.1` as the current patch release.
* [ ] `README.md` links to the `3.2.1` release notes.
* [ ] `README.md` links to the `3.2.1` release readiness record.
* [ ] `README.md` links to the `3.2.1` consumer verification guide.
* [ ] `docs/index.md` identifies `3.2.1` as the current patch release.
* [ ] `docs/articles/index.md` identifies `3.2.1` as the current patch release.
* [ ] `docs/articles/index.md` links to the `3.2.1` release notes and consumer
  verification guide.
* [ ] `docs/articles/release-validation.md` identifies the `3.2.1` readiness
  record as the current release-candidate control sheet.
* [ ] `docs/articles/release-validation.md` identifies the `3.2.1` consumer
  verification guide as the current consumer-facing verification record.
* [ ] `docs/articles/api-compatibility-and-semver.md` identifies `3.2.1` as
  the current stable patch release.
* [ ] `docs/articles/release-cadence-and-readiness.md` identifies `3.2.1` as
  the current patch release.
* [ ] `docs/articles/toc.yml` exposes the `3.2.1` release documentation.
* [ ] Historical `3.2.0`, `3.1.0`, and earlier release records remain
  available for traceability.

## Required validation before tag

* [ ] Restore succeeds in locked mode using the repository SDK and package
  configuration.
* [ ] Debug solution build succeeds.
* [ ] Release solution build succeeds.
* [ ] `dotnet format --verify-no-changes` succeeds.
* [ ] All test projects pass.
* [ ] Repository-wide line-coverage gate passes.
* [ ] Package-specific coverage gates pass.
* [ ] Core branch-coverage gate passes.
* [ ] XML-documentation inventory ceiling passes.
* [ ] API baseline validation passes.
* [ ] API compatibility validation passes.
* [ ] Version consistency validation passes for `3.2.1`.
* [ ] Version consistency validation passes for tag `v3.2.1`.
* [ ] Package creation succeeds for the complete publishable package set.
* [ ] Generated package IDs are correct.
* [ ] Generated package versions are `3.2.1`.
* [ ] Generated dependency metadata is correct.
* [ ] Generated repository metadata is correct.
* [ ] Generated project URL metadata is correct.
* [ ] Generated repository commit metadata is populated.
* [ ] Generated symbol packages are correct.
* [ ] Packaged README content is correct.
* [ ] Generated packages use
  `https://github.com/AsiBackbone/AsiBackbone` as repository metadata.
* [ ] Generated packages use
  `https://asibackbone.github.io/AsiBackbone/` as project metadata.
* [ ] Template smoke tests succeed against repository projects.
* [ ] Template smoke tests succeed against `3.2.1` fallback package
  references.
* [ ] External-consumer smoke tests succeed.
* [ ] Stable-package smoke tests succeed.
* [ ] Existing capability-validation profile tests continue to pass unchanged.
* [ ] DocFX build succeeds.
* [ ] Documentation release-claim validation succeeds.
* [ ] Documentation links have been reviewed for the repository-transfer
  boundary.
* [ ] CodeQL reports no blocking findings.
* [ ] Dependency review reports no blocking findings.
* [ ] OpenSSF Scorecard results have no unexplained blocking findings.
* [ ] Workflow-security checks have no unexplained blocking findings.
* [ ] `actionlint` validation succeeds where configured.
* [ ] Zizmor validation succeeds where configured.
* [ ] OWASP Dependency-Check has no unexplained blocking findings.
* [ ] Reviewed OWASP suppressions remain narrowly scoped, documented, and
  unexpired.
* [ ] SPDX SBOM artifacts are generated for the expected package set.
* [ ] SBOM metadata uses the canonical organization-owned repository.
* [ ] Provenance artifacts are produced where supported.
* [ ] No package-signing claim is made for unsigned packages.
* [ ] No documentation implies that Source Link, SBOMs, provenance, or public
  source availability is equivalent to NuGet package signing.

## Package verification expectations

Generated and published `3.2.1` packages should expose:

```text
Repository type:
git

Repository URL:
https://github.com/AsiBackbone/AsiBackbone

Project URL:
https://asibackbone.github.io/AsiBackbone/
```

The package repository commit value should identify the source revision used
for the release and should resolve to the final `v3.2.1` source commit.

The expected package IDs remain:

* `AsiBackbone.Core`
* `AsiBackbone.DependencyInjection`
* `AsiBackbone.Storage.InMemory`
* `AsiBackbone.EntityFrameworkCore`
* `AsiBackbone.AspNetCore`
* `AsiBackbone.Testing`
* `AsiBackbone.Templates`
* `AsiBackbone.Analyzers`
* `AsiBackbone.OpenTelemetry`
* `AsiBackbone.Signing.LocalDevelopment`
* `AsiBackbone.Signing.ManagedKey`

## Package-signing posture

NuGet package signing remains intentionally deferred while AsiBackbone is
independently maintained.

The release should not imply that unsigned packages are cryptographically
authenticated merely because they provide:

* Source Link metadata;
* repository commit metadata;
* SBOMs;
* provenance artifacts;
* GitHub release tags;
* public source code; or
* a public release record.

These are complementary traceability and supply-chain signals rather than a
substitute for NuGet package signing.

Consumers with mandatory package-signing requirements remain responsible for
enforcing those requirements through their own dependency policy.

## Release sequence

1. Confirm the prepared `3.2.1` entry is present in `CHANGELOG.md` and is
   consistent with `release-notes-321.md`.
2. Confirm `release-readiness-321.md` and `consumer-verification-321.md` are
   complete and linked from the current release documentation.
3. Confirm the canonical repository and documentation URLs are consistent
   across build, NuGet, Source Link, citation, Zenodo, SBOM, workflow, sample,
   package README, and documentation metadata.
4. Confirm historical package records preserve the repository metadata that
   was actually published with those package versions.
5. Regenerate and commit any NuGet lock files or generated release artifacts
   required by the final release-candidate commit.
6. Confirm locked restore succeeds.
7. Open the `release/3.2.1` pull request against `main`.
8. Allow all required pull-request, CI, security, package, documentation, and
   release-validation checks to complete.
9. Resolve any blocking validation or documentation findings without weakening
   the release criteria.
10. Confirm the release-readiness checklist accurately reflects the final
    release-candidate state.
11. Merge the release-preparation pull request only after required checks pass.
12. Confirm `main` contains the final `3.2.1` version metadata, release
    documentation, repository URLs, and project URLs.
13. Confirm the final `main` commit is the intended release source commit.
14. Create the annotated release tag `v3.2.1` from the validated commit.
15. Run the stable release workflow against `v3.2.1`.
16. Confirm all expected NuGet packages are published from the official
    package source.
17. Confirm all expected symbol packages are published.
18. Confirm published package versions are exactly `3.2.1`.
19. Confirm published NuGet metadata identifies:

```text
https://github.com/AsiBackbone/AsiBackbone
```

20. Confirm published project metadata identifies:

```text
https://asibackbone.github.io/AsiBackbone/
```

21. Confirm GitHub release assets are present as expected.
22. Confirm package SBOMs are present as expected.
23. Confirm provenance artifacts are attached where supported.
24. Confirm documentation deployment succeeds at:

```text
https://asibackbone.github.io/AsiBackbone/
```

25. Run post-publication Source Link validation:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.1
```

26. Verify that each package reports repository type `git`.
27. Verify that each package reports:

```text
https://github.com/AsiBackbone/AsiBackbone
```

28. Verify that each package repository commit resolves to the tagged
    `v3.2.1` source commit.
29. Review the published `3.2.1` NuGet pages for README rendering, package
    icon, repository link, project link, dependencies, and target-framework
    metadata.
30. Review the published documentation site for the current release links.
31. Record any release exception explicitly rather than silently weakening
    the release claim.

## Post-publication verification

After NuGet publication completes, run:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.1
```

The expected repository URL is:

```text
https://github.com/AsiBackbone/AsiBackbone
```

The expected project URL is:

```text
https://asibackbone.github.io/AsiBackbone/
```

Verify that the repository commit embedded in each published package resolves
to the final source commit associated with `v3.2.1`.

Any mismatch between package metadata, GitHub release metadata, the tag,
SBOMs, provenance artifacts, citation metadata, or the release documentation
should be investigated and documented before the release is considered
complete.

## Final scope statement

AsiBackbone remains Accountable Systems Infrastructure for governed .NET
decision flow.

`3.2.1` changes the canonical stewardship and metadata location of the project
from a personal GitHub namespace to the dedicated `AsiBackbone` organization.

It does not introduce a new public API surface, change runtime governance
semantics, alter package IDs or namespaces, or change host-owned execution
responsibilities.

It does not make AsiBackbone an authentication system, authorization system,
host executor, robot controller, compliance certification, complete
tamper-evidence platform, production key-management system, or production
replay-protection system by default.

The repository location changed.

The stable software contract did not.

