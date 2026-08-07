# AsiBackbone 3.1.0 Consumer Verification Guide

This guide helps consumers verify the `3.1.0` package family after publication. It does not claim that the packages are NuGet-signed, independently audited, certified, or reproducibly built by every consumer environment.

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

Verify that the selected version is exactly `3.1.0` and that no unexpected package source overrides your configured NuGet source order.

## Confirm the compatibility boundary

For `3.1.0`, verify:

- target framework: `net10.0`;
- package version: `3.1.0`;
- assembly version: `3.0.0.0`;
- file version: `3.1.0.0`;
- repository URL: `https://github.com/cdcavell/AsiBackbone`; and
- public package IDs and namespaces remain in the existing `AsiBackbone.*` family.

Rebuild consumers after updating package references and run the host's policy, audit, acknowledgment, capability, outbox, signing, actor-context, execution-accountability, and endpoint-governance tests as applicable.

## Review minor-release behavior

### Actor-type claims

The ASP.NET Core actor resolver accepts only actor types listed in `AllowedActorTypesFromClaims`. The default list contains only `Human`.

Hosts that intentionally map `System`, `Service`, `Agent`, or `Unknown` from a trusted identity-provider-issued or host-generated claim must add those values explicitly. Unrecognized, undefined, or disallowed claim values fall back to `DefaultAuthenticatedActorType`, which defaults to `Human`.

Do not map actor type from user-controlled request fields, profile data, scopes, or arbitrary external claims.

### Capability-proof trust pins

New proof trust-pin settings are optional. Consumers that configure them should verify the expected:

- signing key ID and key version;
- proof policy version and policy hash;
- verification provider; and
- proof hash algorithm.

A cryptographically valid proof may still fail when its signing metadata does not match the configured trust policy. Consumers that omit these pins retain the earlier verification behavior.

### Governed execution receipts

Consumers adopting `GovernedOperationExecutionReceipt` should confirm that:

- operation and attempt identifiers are stable and bounded;
- persistence outcomes accurately reflect committed, failed, rolled-back, or no-mutation completion;
- committed mutation bindings include the required opaque batch ID, record count, manifest hash, and algorithm;
- raw application values remain in the host audit store rather than receipt metadata; and
- receipt persistence, signing, retention, replay handling, and incident response remain host-owned.

The NCAT audit-completion adapter is a non-packable reference sample and does not create an NCAT dependency for released packages.

## Verify Source Link repository metadata

After the packages are available on NuGet, run:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.1.0
```

The validation confirms that each package exposes the expected repository type, public repository URL, and non-empty repository commit value. The repository commit should resolve to the source revision used for the published package.

Source Link metadata improves traceability but is not equivalent to package signing or an independent supply-chain attestation.

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

Do not interpret Source Link, SBOMs, provenance statements, GitHub release tags, or public source availability as a signed-package guarantee. They are separate trust signals. Consumers that require signed packages should enforce that requirement in their own dependency policy.

## Verify the release record

Compare the published packages with:

- the `v3.1.0` Git tag;
- the GitHub release and attached assets;
- the `3.1.0` release notes;
- the `3.1.0` release readiness record;
- `CHANGELOG.md`;
- `CITATION.cff` and `.zenodo.json`; and
- CI, CodeQL, dependency-review, OpenSSF, workflow-security, OWASP, package-validation, documentation, SBOM, and provenance results associated with the final release commit.

A missing or inconsistent artifact should be investigated rather than silently treated as equivalent evidence.

## Host responsibilities remain unchanged

Package verification does not replace host-owned authentication, authorization, identity-claim trust, policy registration, execution enforcement, durable storage, key custody, replay protection, monitoring, incident response, legal review, or compliance interpretation.

AsiBackbone provides governance-oriented software primitives. The consuming application remains responsible for deciding whether the package, its evidence, and its operational controls meet the host's risk requirements.
