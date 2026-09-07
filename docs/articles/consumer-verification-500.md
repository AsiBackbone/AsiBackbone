# AsiBackbone 5.0.0 Consumer Verification Guide

Use this guide to verify the `5.0.0` package family and its major-version
migration boundary.

`5.0.0` is a security release. Alongside the usual package-identity checks, it
carries verification steps that earlier releases did not: the behavior changes
in this line make governance paths fail closed, and several require host action
before the upgrade is safe to deploy.

## Verify package identity

Expected version: `5.0.0`.

Expected package IDs remain `AsiBackbone.Core`,
`AsiBackbone.DependencyInjection`, `AsiBackbone.Storage.InMemory`,
`AsiBackbone.EntityFrameworkCore`, `AsiBackbone.AspNetCore`,
`AsiBackbone.Testing`, `AsiBackbone.Templates`, `AsiBackbone.Analyzers`,
`AsiBackbone.OpenTelemetry`, `AsiBackbone.Signing.LocalDevelopment`, and
`AsiBackbone.Signing.ManagedKey`.

## Verify compatibility metadata

- target framework: `net10.0`;
- package version: `5.0.0`;
- assembly version: `5.0.0.0`;
- file version: `5.0.0.0`;
- repository: `https://github.com/AsiBackbone/AsiBackbone`;
- project site: `https://asibackbone.github.io/AsiBackbone/`.

Consumers upgrading from `4.0.0` should complete the
[5.0.0 Migration Guide](upgrade-400-to-500.md) before deploying.

## Verify the security-relevant upgrade steps

These checks exist because `5.0.0` closes findings that affect `4.0.0` and
earlier. Complete them for the deployed host, not only for the build.

- [ ] Every host assembly is rebuilt against `5.0.0.0`. Enum constants are
      inlined at compile time, so an assembly still compiled against `4.x`
      keeps the old numbering and disagrees with this release silently.
- [ ] Persisted columns holding `0` for `VerificationPolicyAction`,
      `SignatureVerificationCategory`, `GrantUseState`, or
      `AuditIntegrityVerificationCategory` are migrated. Zero is now a rejected
      `Unspecified` sentinel rather than allow, valid, or accepted.
- [ ] Capability validation call sites pass explicit
      `CapabilityGrantValidationOptions`, including an audience expectation
      wherever proof or bounded-use checking is required.
- [ ] Partial audit chain verification supplies `expectedPreviousLinkHash`, and
      supplies an expected tip wherever completeness matters.
- [ ] No production configuration path registers local-development signing, or
      `AllowInProduction` is set deliberately with the ephemeral-key
      consequence accepted.
- [ ] Signing call sites either tolerate the throwing `requireSignature`
      default or pass `requireSignature: false` and inspect the result.
- [ ] Deployments that relied on email as a default actor-id or display-name
      claim have added those claim types back explicitly, or have accepted their
      removal from durable audit rows.
- [ ] A sample of retained signed artifacts and audit chains re-verifies under
      `5.0.0`. Artifacts that now fail were not bound to their signature before.

## Verify Source Link

After publication:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 5.0.0
```

Each package should report repository type `git`, the canonical repository URL,
and a non-empty repository commit associated with the final `v5.0.0` source.

## Verify template fallback references

Fallback package references should use `5.0.0` for
`AsiBackbone.AspNetCore`, `AsiBackbone.Core`,
`AsiBackbone.Storage.InMemory`, and `AsiBackbone.Analyzers`.

Hosts scaffolding the web API template should note that its sample capability
validator now denies by default and the template registers no authentication
scheme, so the scaffold refuses callers until its owner adds one.

## Verify security advisory status

`5.0.0` is the fixed version for the advisories published against this
repository, including `GHSA-q63q-xcvp-chv8` (critical) and
`GHSA-p6pw-5gr3-xrr7` (high). Each records an affected range of `<= 4.0.0`.

Consumers who cannot upgrade immediately should apply the host mitigation each
advisory describes: reconstruct the canonical payload with
`CanonicalPayloadBuilder`, recompute the hash with
`CanonicalPayloadHasher.ComputeHash`, and compare it to the stored signing hash
before treating a verification outcome as authoritative, as the
[regulated storage and signing verification checklist](regulated-storage-and-signing-verification-checklist.md)
describes. That mitigation is a host-side compensating control, not a fix.

## Verify release evidence

- package and template versions resolve to `5.0.0`;
- package IDs, repository metadata, and `net10.0` assets are present;
- the security-relevant upgrade steps above are complete for the deployed host;
- package SBOMs and provenance come from the release workflow for the tagged
  commit;
- NuGet package signing is understood as deferred;
- consumer-specific source review, vulnerability scanning, package-cache
  controls, and operational approval are complete.

SBOM and provenance artifacts are release evidence. They do not by themselves
prove package signing, vulnerability absence, production tamper evidence,
legal non-repudiation, or compliance approval.

Verifying this release does not establish that a host's own governed execution
paths are correct. The framework failing closed is a precondition for that
argument, not the argument itself.

## Related documentation

- [5.0.0 Release Notes](release-notes-500.md)
- [5.0.0 Migration Guide](upgrade-400-to-500.md)
- [5.0.0 Release Readiness Record](release-readiness-500.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
