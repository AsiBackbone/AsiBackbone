# AsiBackbone 3.0.1 Consumer Verification Guide

This guide helps consumers verify the `3.0.1` package family after publication. It does not claim that the packages are NuGet-signed, independently audited, certified, or reproducibly built by every consumer environment.

## Confirm the package source

Install release packages from the official NuGet source and confirm the package owner and package ID before adoption.

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

Verify that the selected version is exactly `3.0.1` and that no unexpected package source overrides your configured NuGet source order.

## Confirm the compatibility boundary

For `3.0.1`, verify:

- target framework: `net10.0`;
- package version: `3.0.1`;
- assembly version: `3.0.0.0`;
- file version: `3.0.1.0`;
- repository URL: `https://github.com/cdcavell/AsiBackbone`;
- public package IDs and namespaces remain in the existing `AsiBackbone.*` family.

Rebuild consumers after updating package references and run the host's policy, audit, acknowledgment, capability, outbox, signing, and endpoint-governance tests as applicable.

## Review patch behavior

`3.0.1` intentionally hardens threat-model input and evidence integrity:

- undefined numeric threat severity or recommended-outcome values are rejected;
- contributor metadata keys beginning with `threat.` are rejected after trimming and case-insensitive comparison; and
- restrictive non-denial decisions retain the reason associated with the selected outcome.

Consumers should review custom `IThreatModelContributor<TContext>` implementations for unvalidated enum deserialization and custom `threat.*` metadata keys before upgrading.

## Verify Source Link repository metadata

After the packages are available on NuGet, run:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.0.1
```

The validation confirms that each package exposes:

- repository type `git`;
- the expected public repository URL; and
- a non-empty repository commit value.

The repository commit should resolve to the source revision used for the published package. Source Link metadata improves traceability but is not equivalent to package signing or an independent supply-chain attestation.

## Inspect package contents

For higher-assurance adoption, download each `.nupkg` and `.snupkg`, retain cryptographic hashes in the consumer's own release record, and inspect:

- the `.nuspec` package ID and version;
- dependency versions and target-framework assets;
- embedded or linked README content;
- repository URL and commit metadata;
- symbols and source-document mappings;
- SBOM and provenance artifacts published with the release, where available; and
- the absence of unexpected executable tooling or package payloads.

## Package-signing status

Current AsiBackbone NuGet packages are intentionally published without NuGet package signing while the project is independently maintained.

Do not interpret Source Link, SBOMs, provenance statements, GitHub release tags, or public source availability as a signed-package guarantee. They are separate trust signals. Consumers that require signed packages should enforce that requirement in their own dependency policy and should not waive it based solely on this project documentation.

## Verify the release record

Compare the published packages with:

- the `v3.0.1` Git tag;
- the GitHub release and attached assets;
- the `3.0.1` release notes;
- the `3.0.1` release readiness record;
- `CHANGELOG.md`;
- `CITATION.cff` and `.zenodo.json`; and
- CI, CodeQL, dependency-review, package-validation, documentation, SBOM, and provenance results associated with the final release commit.

A missing or inconsistent artifact should be investigated rather than silently treated as equivalent evidence.

## Host responsibilities remain unchanged

Package verification does not replace host-owned authentication, authorization, policy registration, execution enforcement, durable storage, key custody, replay protection, monitoring, incident response, legal review, or compliance interpretation.

AsiBackbone provides governance-oriented software primitives. The consuming application remains responsible for deciding whether the package, its evidence, and its operational controls meet the host's risk requirements.
