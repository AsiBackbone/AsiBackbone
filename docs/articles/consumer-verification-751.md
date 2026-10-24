# AsiBackbone 7.5.1 Consumer Verification Guide

This guide records the consumer checks for the `7.5.1` package family. Perform
the artifact-verification steps against the published `v7.5.1` GitHub Release
and NuGet packages after publication.

## Confirm the compatibility boundary

- Package IDs remain `AsiBackbone.*`.
- All packages continue to target `net10.0`.
- Managed assemblies retain `AssemblyVersion` `7.0.0.0`.
- Package and file versions are `7.5.1` and `7.5.1.0`.
- Public APIs, package dependencies, durable schemas, canonical wire values,
  public namespaces, documented defaults, and runtime decision behavior are
  unchanged from `7.5.0`.

## Verify production-signing configuration

1. In a normal production configuration, register
   `AsiBackbone.Signing.ManagedKey` with the host's reviewed managed-key
   implementation.
2. Confirm the local-development signing provider still fails registration when
   the environment is Production and `AllowInProduction` is not enabled.
3. Confirm test or non-hosted scenarios without an environment name remain
   usable.
4. Treat `AllowInProduction` only as an exceptional test/demo escape hatch, not
   as approval for production key custody.

## Verify host enforcement

Exercise the host's protected-operation boundary and confirm the protected
executor is not called after a denied or deferred decision, unsatisfied
acknowledgment, missing or invalid capability authority, or required
evidence-persistence failure. Confirm an allowed governance decision remains
distinct from a later host execution failure.

Review alternate entry points and delayed-work paths so none can invoke the
protected executor before the governed seam permits execution.

## Verify the published packages

```powershell
gh release download v7.5.1 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.5.1.nupkg'
gh attestation verify ./AsiBackbone.Core.7.5.1.nupkg --repo AsiBackbone/AsiBackbone
```

Inspect package metadata and Source Link repository commit, compare SHA-256 with
the release evidence manifest, and repeat for each adopted package. Confirm the
declared package dependencies and framework references match `7.5.0`.

## Build a clean consumer

Create a clean `net10.0` project, add only the required `AsiBackbone.*` packages
at `7.5.1`, restore, build with warnings enabled, exercise the adopted
integration boundaries, and run the consumer's tests.

Consumers upgrading from `6.x` must first complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

See also:

- [7.5.1 Release Notes](release-notes-751.md)
- [7.5.1 Release Readiness Record](release-readiness-751.md)
- [Host Enforcement Boundary](host-enforcement-boundary.md)
- [Signing Provider Package Boundary](signing-provider-package-boundary.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
