# AsiBackbone 3.2.0 Release Notes

Release date: 2026-08-02

## Summary

`3.2.0` is a backward-compatible minor release for the stable `3.x` AsiBackbone package family. It strengthens capability-grant validation at consequential execution boundaries by introducing explicit validation profiles that distinguish strict execution-boundary enforcement from intentional metadata-only inspection.

The release reduces ambiguity around signed capability grants without silently changing existing `3.x` behavior. Hosts can opt into a clearly named execution-boundary profile that requires proof verification and enables bounded-use validation by default, while metadata-only validation remains available through an explicitly named reduced-validation profile.

AsiBackbone continues to provide governance and accountability primitives rather than replacing host authentication, authorization, durable replay storage, signature-key custody, external execution controls, or compliance responsibility.

## Added

### Explicit capability-grant validation profiles

`CapabilityGrantValidationOptions` now provides two explicit validation profiles:

- `CreateExecutionBoundary(...)` for consequential execution and operational-gateway validation; and
- `CreateMetadataValidation(...)` for intentional structural, temporal, scope, policy, acknowledgment, handshake, gateway, and resource-binding inspection where proof and bounded-use enforcement are intentionally out of scope.

The execution-boundary profile always requires signed-artifact proof verification and enables bounded-use validation by default with `maxUseCount: 1`.

Hosts may explicitly set `requireUseCheck: false` only when replay or use enforcement is owned atomically by another trusted execution boundary. That opt-out remains a host responsibility and should be documented as part of the host threat model.

The metadata-validation profile intentionally does not expose proof or bounded-use switches, making the reduced validation contract visible in code review.

### Validation-profile coverage

Focused tests cover:

- execution-boundary proof and bounded-use defaults;
- explicit bounded-use opt-out;
- metadata-validation proof/use behavior;
- failure when a required proof verifier is unavailable;
- defer behavior when a required use store is unavailable after proof succeeds;
- successful intentional metadata-only validation; and
- preservation of the legacy no-options `ValidateAsync(...)` behavior for `3.x` compatibility.

## Changed

- Updated execution-boundary guidance and examples to prefer `CreateExecutionBoundary(...)` for consequential execution.
- Clarified that callers should proceed only when `CapabilityGrantValidationResult.ShouldAllow` is `true`.
- Clarified that `CapabilityGrantValidator.ValidateAsync(signedGrant)` without explicit options preserves the existing `3.x` metadata-oriented behavior and does not automatically verify proof or perform bounded-use/replay checks.
- Clarified that a successful metadata-only validation result does not establish proof authenticity, replay resistance, authentication, authorization, or permission to execute an external action.
- Updated `SQLitePCLRaw.bundle_e_sqlite3` from `3.0.3` to `3.0.4` and refreshed affected reproducible NuGet lock files.
- Updated repository GitHub Actions dependencies, including CodeQL, OpenSSF Scorecard, Zizmor, checkout, and .NET setup actions.

## Fixed

- Fixed PowerShell environment-variable expansion in the quality-report workflow so Core branch-coverage validation receives the configured project, configuration, coverage-output, threshold, and workspace values correctly.
- Updated the corresponding quality-report coverage input path to use the corrected PowerShell environment-variable syntax.

## Compatibility notes

- Package IDs and public namespaces are unchanged.
- The target framework remains `net10.0`.
- `AssemblyVersion` remains `3.0.0.0`; `FileVersion` advances to `3.2.0.0`.
- The new capability-grant validation profiles are additive.
- Existing consumers using `CapabilityGrantValidationOptions.Create(...)` retain their current validation behavior.
- Existing consumers using `ValidateAsync(signedGrant)` without explicit options are not silently moved to proof or bounded-use enforcement.
- Authentication, authorization, durable and concurrency-safe replay protection, signing-key trust, persistence, external execution, and operational safety controls remain host-owned responsibilities.

## Package signing posture

NuGet package signing remains intentionally deferred while AsiBackbone is independently maintained. Consumers should continue to rely on the official NuGet source, public repository, release tags, Source Link repository metadata, SBOMs, provenance artifacts where available, and reproducible release practices.

## Validation

The release candidate should pass:

- locked restore and Debug/Release solution builds;
- formatting, analyzer, unit, integration, and property-based tests;
- repository-wide and package-specific coverage gates;
- Core branch coverage and XML-documentation inventory validation;
- API baseline and compatibility checks;
- package creation and generated NuGet metadata validation;
- template, external-consumer, and stable-package smoke tests;
- version-consistency validation for `3.2.0` and `v3.2.0`;
- DocFX generation and documentation release-claim validation;
- CodeQL, dependency review, OpenSSF, actionlint/Zizmor, and OWASP checks; and
- SBOM and provenance handling where supported.

After publication, validate Source Link repository metadata with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.0
```
