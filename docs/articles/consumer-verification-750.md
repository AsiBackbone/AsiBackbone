# AsiBackbone 7.5.0 Consumer Verification Guide

This guide records the consumer checks for the `7.5.0` package family. Perform
the artifact-verification steps against the published `v7.5.0` GitHub Release
and NuGet packages after publication.

## Confirm the compatibility boundary

- Package IDs remain `AsiBackbone.*`.
- All packages continue to target `net10.0`.
- Managed assemblies retain `AssemblyVersion` `7.0.0.0`.
- Package and file versions are `7.5.0` and `7.5.0.0`.
- Canonical wire values, persistence schemas, public namespaces, and documented defaults remain unchanged.
- Existing custom evaluation contexts continue to compile without implementing `TraceId`.

## Verify endpoint trace correlation

For hosts that use governed ASP.NET Core endpoints:

1. Start a request with W3C activity propagation enabled and capture `Activity.Current.Id`.
2. Exercise endpoints that allow, deny, warn, defer, require acknowledgment, or recommend escalation through policy evaluation.
3. Confirm each final decision and persisted decision receipt carries the request activity identifier.
4. Exercise a request without an active activity and confirm the receipt uses `HttpContext.TraceIdentifier`.
5. If custom policy or capability evaluators omit trace identifiers, confirm the endpoint service backfills the request trace.
6. If a custom evaluator supplies a trace identifier, confirm the supplied value is retained.
7. Query `IAuditLedgerStore.FindByTraceIdAsync(...)` and confirm it returns the expected governed endpoint records.

## Verify the published packages

```powershell
gh release download v7.5.0 --repo AsiBackbone/AsiBackbone --pattern 'AsiBackbone.Core.7.5.0.nupkg'
gh attestation verify ./AsiBackbone.Core.7.5.0.nupkg --repo AsiBackbone/AsiBackbone
```

Inspect package metadata and Source Link repository commit, compare SHA-256 with
the release evidence manifest, and repeat for each adopted package.

## Build a clean consumer

Create a clean `net10.0` project, add only the required `AsiBackbone.*` packages
at `7.5.0`, restore, build with warnings enabled, exercise the integration
boundary used by the application, and run the consumer's tests.

Consumers upgrading from `6.x` must first complete the
[6.x to 7.0 upgrade guide](upgrade-600-to-700.md).

See also:

- [7.5.0 Release Notes](release-notes-750.md)
- [7.5.0 Release Readiness Record](release-readiness-750.md)
- [ASP.NET Core Endpoint Governance](aspnetcore-endpoint-governance.md)
- [Supply Chain and Provenance](supply-chain-provenance.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
