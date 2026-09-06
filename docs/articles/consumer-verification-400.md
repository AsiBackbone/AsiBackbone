# AsiBackbone 4.0.0 Consumer Verification Guide

Use this guide to verify the `4.0.0` package family and its major-version
migration boundary.

## Verify package identity

Expected version: `4.0.0`.

Expected package IDs remain `AsiBackbone.Core`,
`AsiBackbone.DependencyInjection`, `AsiBackbone.Storage.InMemory`,
`AsiBackbone.EntityFrameworkCore`, `AsiBackbone.AspNetCore`,
`AsiBackbone.Testing`, `AsiBackbone.Templates`, `AsiBackbone.Analyzers`,
`AsiBackbone.OpenTelemetry`, `AsiBackbone.Signing.LocalDevelopment`, and
`AsiBackbone.Signing.ManagedKey`.

## Verify compatibility metadata

- target framework: `net10.0`;
- package version: `4.0.0`;
- assembly version: `4.0.0.0`;
- file version: `4.0.0.0`;
- repository: `https://github.com/AsiBackbone/AsiBackbone`;
- project site: `https://asibackbone.github.io/AsiBackbone/`.

Consumers upgrading from `3.2.3` should complete the
[4.0.0 Migration Guide](upgrade-323-to-400.md), including the custom outbox
store and claim-leasing review.

## Verify Source Link

After publication:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 4.0.0
```

Each package should report repository type `git`, the canonical repository URL,
and a non-empty repository commit associated with the final `v4.0.0` source.

## Verify template fallback references

Fallback package references should use `4.0.0` for
`AsiBackbone.AspNetCore`, `AsiBackbone.Core`,
`AsiBackbone.Storage.InMemory`, and `AsiBackbone.Analyzers`.

## Verify release evidence

- package and template versions resolve to `4.0.0`;
- package IDs, repository metadata, and `net10.0` assets are present;
- the host has reviewed claim leasing and any custom outbox store;
- package SBOMs and provenance come from the release workflow for the tagged
  commit;
- NuGet package signing is understood as deferred;
- consumer-specific source review, vulnerability scanning, package-cache
  controls, and operational approval are complete.

SBOM and provenance artifacts are release evidence. They do not by themselves
prove package signing, vulnerability absence, production tamper evidence,
legal non-repudiation, or compliance approval.

## Related documentation

- [4.0.0 Release Notes](release-notes-400.md)
- [4.0.0 Migration Guide](upgrade-323-to-400.md)
- [4.0.0 Release Readiness Record](release-readiness-400.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
