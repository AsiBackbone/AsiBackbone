# Stable Release Validation

This article documents the reusable release-blocking validation path for stable release lines. The current released stable package family is `3.x`, with `3.2.0` as the current minor release.

The binary assembly identity for the `3.x` line remains `3.0.0.0`.

In this software project, **ASI** means **Accountable Systems Infrastructure**. Release validation should confirm that the package family remains practical governance infrastructure and that implementation claims stay within the documented software boundary. See [Release Cadence and Readiness](release-cadence-and-readiness.md) for the release-stream and stabilization guidance that complements this checklist.

The [3.2.0 Release Readiness Record](release-readiness-320.md) is the current release-candidate control sheet. The [3.2.0 Consumer Verification Guide](consumer-verification-320.md) documents the consumer-facing package-source, package ID, capability-validation, Source Link, SBOM/provenance, and deferred package-signing verification path. Earlier readiness records are retained for traceability.

## Required checks before tagging a stable release

Before cutting a stable release tag, confirm the following checks have passed on the release-candidate commit:

| Check | Where it runs | Release purpose |
| --- | --- | --- |
| Release stream classification | Release PR, release readiness record | Confirms the release is correctly classified as patch, minor, major, or preview. |
| Version metadata validation | stable release validation, package publish | Confirms MSBuild version metadata, citation metadata, Zenodo metadata, optional tag metadata, and generated package filenames align. |
| Debug solution build coverage validation | CI, stable release validation, developer checklist | Confirms first-party package and test projects remain enabled for default Debug solution builds and that any remaining Debug exclusions are reviewed. |
| Locked restore | CI, stable release validation, package publish | Confirms committed lock files match the current package and project dependency graph. |
| Build | CI, stable release validation, package publish | Confirms release projects compile in Release configuration. |
| Public API XML documentation inventory | CI, release readiness record | Inventories `CS1591` gaps for selected public package projects while staged enforcement is phased in. |
| Formatting | CI, stable release validation, package publish | Confirms source formatting is stable before release. |
| Tests | CI, stable release validation, package publish | Confirms the solution test suite passes before packaging or publishing. |
| Package creation | CI, stable release validation, package publish | Confirms every package project under `src`, excluding template-content projects, can be packed. |
| Package version validation | stable release validation, package publish | Confirms generated package versions and tag identity align with repository version metadata. |
| NuGet metadata validation | stable release validation, package publish | Confirms generated `.nupkg` metadata, README files, IDs, descriptions, tags, license metadata, project URL, repository URL, and repository commit metadata align before publication. |
| Package metadata asset checklist | release readiness record, generated package inspection | Confirms package icons, packaged README rendering, NuGet metadata, Source Link metadata, SBOM/provenance artifacts, and documentation links are reviewed. |
| Package SBOM generation | CI, stable release validation, package publish | Generates SPDX JSON SBOM files and an SBOM manifest for generated `.nupkg` artifacts. |
| Package provenance attestation | CI on non-PR events, stable release validation on non-PR events, package publish | Attests generated package and SBOM artifacts where supported. |
| Consumer verification guide | README, release notes, release readiness record, docs navigation | Confirms consumers have a copy/paste verification path for package source, package IDs, package version, repository metadata, Source Link, SBOM/provenance, and deferred package signing. |
| NuGet package signing readiness | release readiness record, `SECURITY.md`, release notes | Confirms package signing is either explicitly deferred as an open supply-chain item or, when available, documented with signing process, verification guidance, and updated public wording before release. |
| Template package smoke validation | CI, stable release validation | Confirms the packed `AsiBackbone.Templates` package can be installed, generate supported host styles, restore, and build. |
| Documentation build | publish docs, stable release validation, package publish | Confirms DocFX can build the documentation included in the release posture. |
| Documentation link review | release readiness record, manual docs review | Confirms README links, DocFX navigation, release notes, migration guides, package documentation links, and GitHub Pages links point to current pages. |
| External consumer smoke tests | external consumer smoke workflow, stable release validation | Confirms clean consumer-style projects can reference and wire the package family. |
| Source Link metadata validation | manual post-publish validation | Confirms published NuGet packages include expected repository type, repository URL, and non-empty repository commit metadata. |

## Canonical local release-hardening commands

Use these commands before treating local validation as release evidence:

```bash
dotnet restore AsiBackbone.slnx --locked-mode
dotnet build AsiBackbone.slnx --configuration Release --no-restore
dotnet test AsiBackbone.slnx --configuration Release --no-build --no-restore
```

The default Debug solution build should include all first-party package and test projects:

```bash
dotnet build AsiBackbone.slnx
```

The reviewed Debug exclusion allowlist is enforced by:

```powershell
./scripts/Validate-DebugSolutionBuildCoverage.ps1
```

## Release-blocking workflows

The following workflows form the reusable gate for stable release candidates:

- `CI` validates dependency review for pull requests, Debug solution build coverage, solution restore/build/test, public API XML documentation inventory, formatting, package creation, package SBOM generation, template package smoke validation, coverage output, and CodeQL analysis.
- `External Consumer Smoke Test` validates package-consumer wiring through the external consumer and stable package integration smoke scripts.
- `Publish Documentation` validates the DocFX build used for the documentation site.
- `Stable Release Validation` provides a single release-candidate gate for version metadata, Debug solution build coverage, locked restore, build, formatting, tests, DocFX, package creation, generated package version validation, generated NuGet metadata validation, SBOM generation, template package smoke validation, smoke checks, and provenance handling where supported.
- `Publish AsiBackbone Packages` repeats release-critical validation before package publish.

## Tagging rule

Do not cut a stable release tag until the release-candidate commit has passed the release-blocking workflows above, or until any intentionally deferred check is documented in the release notes or release readiness record for that release.

If a tag is pushed and package validation fails, do not publish replacement packages by hand. Fix the release candidate, document the reason, and repeat the release process with a corrected tag or clearly documented follow-up plan.

## Stable Release Validation workflow

The `Stable Release Validation` workflow runs on pull requests to `main`, pushes to `main`, `v*.*.*` tags, and manual dispatch.

The workflow validates .NET SDK setup, version metadata, Debug solution build coverage, locked restore, Release build, formatting, tests, tool restore, DocFX, package creation, package versions, NuGet metadata, package SBOM generation, template package smoke validation, external consumer smoke tests, stable package integration smoke tests, provenance handling where supported, and artifact upload.

## Package publish validation

The package publish workflow performs release-critical validation before publishing packages. It validates version metadata, restores dependencies in locked mode, builds the solution, verifies formatting, runs tests, restores .NET tools, builds DocFX documentation, packs package projects, validates generated package versions and NuGet metadata, generates SBOMs, handles provenance where supported, uploads artifacts, and publishes only after validation succeeds.

## NuGet metadata validation

`Validate-NuGetPackageMetadata.ps1` inspects generated `.nupkg` files rather than only project files. It validates package ID casing, package version, package descriptions, package tags, license metadata, project URL, repository URL metadata, repository commit metadata when available, README metadata, packaged README presence, package icon metadata, packaged icon presence, and package-specific README wording anchors.

This check catches release-blocking NuGet metadata mistakes before package publication because NuGet package metadata for a published version cannot be overwritten.

## Public API XML documentation inventory

`Validate-XmlDocumentation.ps1` builds selected public package projects with `CS1591` unsuppressed and writes a Markdown inventory to `artifacts/xml-docs/cs1591-inventory.md`. CI uploads this file as the `asi-backbone-cs1591-inventory` artifact.

The staged policy is documented in [Public API XML Documentation](public-api-xml-documentation.md). Existing package projects may carry `AsiBackboneSuppressMissingXmlDocs=true` as a transitional, project-scoped baseline. New projects should not add that property unless the exception is documented.

## Pre-release metadata and asset checklist

For every stable release, the release readiness record should explicitly confirm:

- package icon source, generated PNG, package inclusion, and small-size rendering are acceptable;
- packaged README files are present and render acceptably in generated packages;
- NuGet metadata is correct for package ID, version, description, tags, license, project URL, repository URL, repository type, and repository commit where available;
- Source Link repository commit metadata is generated and has a post-publish validation plan when NuGet download is required to confirm it;
- package SBOM files and `sbom-manifest.json` are generated for produced `.nupkg` artifacts;
- package and SBOM provenance artifacts are uploaded and attested where the workflow event supports attestation;
- consumer verification guidance explains package-source, package ID, version, repository metadata, Source Link, SBOM/provenance, and deferred-signing checks without overstating signing or tamper-evidence;
- public API XML documentation inventory is reviewed, and staged enforcement changes or intentional exceptions are documented;
- Debug solution build coverage is reviewed so first-party package/test projects stay enabled for default local solution builds;
- NuGet package signing status is checked against `SECURITY.md`, and release notes/readiness records state whether signing remains deferred or has an adopted signing and verification process;
- README, DocFX navigation, release notes, migration notes, package README links, and GitHub Pages links are current;
- any intentionally deferred metadata, asset, Source Link, SBOM, provenance, package-signing, public API XML documentation, Debug solution build coverage, or documentation-link check is recorded with risk and follow-up.

## Source Link metadata validation

After `3.2.0` packages are published and visible on NuGet, maintainers should run:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.0
```

This post-publish check downloads the published packages and confirms the expected repository type, repository URL, and non-empty repository commit metadata are present.

## Deferred checks

If a release-critical check is intentionally deferred, document the deferred check, the reason, the accepted risk, the follow-up issue or milestone, and whether release notes need to mention it.

Deferred checks should be rare for a stable release.

NuGet package signing is currently a known open supply-chain readiness item. Until a reviewed signing process is adopted, release candidates should record the deferral instead of describing artifacts as signed. If package signing is introduced, update `SECURITY.md`, this validation guide, the release readiness record, release notes, and consumer verification guidance in the same release-preparation PR.

## Related documentation

- [Release Cadence and Readiness](release-cadence-and-readiness.md)
- [Public API XML Documentation](public-api-xml-documentation.md)
- [Supply-Chain Provenance and Package SBOMs](supply-chain-provenance.md)
- [3.2.0 Consumer Verification Guide](consumer-verification-320.md)
- [3.2.0 Release Readiness Record](release-readiness-320.md)
- [3.2.0 Release Notes](release-notes-320.md)
- [3.1.0 Consumer Verification Guide](consumer-verification-310.md)
- [3.1.0 Release Readiness Record](release-readiness-310.md)
- [3.1.0 Release Notes](release-notes-310.md)
- [3.0.1 Consumer Verification Guide](consumer-verification-301.md)
- [3.0.1 Release Readiness Record](release-readiness-301.md)
- [3.0.1 Release Notes](release-notes-301.md)
- [3.0.0 Consumer Verification Guide](consumer-verification-300.md)
- [3.0.0 Release Readiness Record](release-readiness-300.md)
- [3.0.0 Release Notes](release-notes-300.md)
- [2.3.0 Release Readiness Record](release-readiness-230.md)
- [2.3.0 Release Notes](release-notes-230.md)
- [2.2.1 Release Readiness Record](release-readiness-221.md)
- [2.2.1 Release Notes](release-notes-221.md)
- [2.2.0 Release Readiness Record](release-readiness-220.md)
- [2.2.0 Release Notes](release-notes-220.md)
- [2.1.0 Release Readiness Record](release-readiness-210.md)
- [2.1.0 Release Notes](release-notes-210.md)
- [2.0.2 Release Readiness Record](release-readiness-202.md)
- [2.0.2 Release Notes](release-notes-202.md)
- [2.0.1 Release Readiness Record](release-readiness-201.md)
- [2.0.1 Release Notes](release-notes-201.md)
- [2.0.0 Release Readiness Record](release-readiness-200.md)
- [2.0.0 Release Notes](release-notes-200.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Schema Versioning](schema-versioning.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
- [dotnet new Templates](templates.md)
