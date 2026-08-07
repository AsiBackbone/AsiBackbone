# AsiBackbone 3.2.1 Release Notes

Release date: 2026-08-07

## Summary

`3.2.1` is a backward-compatible patch release for the stable `3.2.x`
AsiBackbone package family. It marks the transfer of the canonical project
repository from the personal `cdcavell` GitHub namespace to the dedicated
`AsiBackbone` organization.

The release updates repository, documentation, Source Link, citation, SBOM,
validation, workflow, sample, and package metadata so newly published
`3.2.1` artifacts identify `AsiBackbone/AsiBackbone` as the canonical source
repository.

The software contract itself remains unchanged. Package IDs, public
namespaces, public APIs, runtime governance behavior, the `net10.0` target
framework, and `AssemblyVersion` `3.0.0.0` are preserved.

The explicit capability-grant validation profiles introduced in `3.2.0`
remain part of the stable `3.2.x` package family without behavioral changes
in this patch.

## Changed

### Canonical repository location

The canonical source repository is now:

```text
https://github.com/AsiBackbone/AsiBackbone
```

The project was transferred from:

```
https://github.com/cdcavell/AsiBackbone
```

GitHub may redirect historical repository URLs, but consumers, package
metadata, documentation, tooling, and future release records should use the
organization-owned canonical location.

### Canonical documentation location

The canonical GitHub Pages documentation site is now:

```
https://asibackbone.github.io/AsiBackbone/
```

Repository documentation links, badges, samples, package READMEs, development
diagnostics examples, and documentation publishing configuration have been
updated to use the organization-owned site.

### NuGet and Source Link metadata

New 3.2.1 packages identify:

```
Repository URL:
https://github.com/AsiBackbone/AsiBackbone

Project URL:
https://asibackbone.github.io/AsiBackbone/
```

Source Link post-publication validation now expects the organization-owned
repository for `3.2.1`.

Published packages from earlier releases retain the repository metadata that
was embedded when those immutable packages were created. The repository
transfer does not rewrite previously published NuGet artifacts.

### Citation and archival metadata

CITATION.cff and .zenodo.json now identify the organization-owned
repository and documentation locations.

The NetCoreApplicationTemplate related-project reference now uses:

```
https://github.com/AsiBackbone/NetCoreApplicationTemplate
```

### SBOM metadata

SPDX package-SBOM generation now uses the organization-owned repository in
its document namespace and project creator metadata.

This changes provenance location metadata only. It does not change package
runtime behavior or imply package signing.

### Repository and documentation references

Release-facing references were updated across:

- repository badges;
- documentation navigation;
- GitHub Pages links;
- security-reporting links;
- historical issue links where the canonical transferred location is appropriate;
- samples;
- development-diagnostics examples;
- package READMEs;
- quality documentation;
- Source Link validation;
- NuGet metadata validation; and
- documentation publishing configuration.

### Template fallback packages

The `AsiBackbone.Templates` generated-project fallback references now use
version `3.2.1` for:

- `AsiBackbone.AspNetCore`;
- `AsiBackbone.Core`;
- `AsiBackbone.Storage.InMemory`; and
- `AsiBackbone.Analyzers`.

Repository-development mode continues to use project references when the
source projects are available.

### Compatibility notes

- Package IDs are unchanged.
- Public namespaces are unchanged.
- Public APIs are unchanged.
- Runtime governance behavior is unchanged.
- The target framework remains net10.0.
- AssemblyVersion remains 3.0.0.0.
- FileVersion advances to 3.2.1.0.
- Existing 3.2.x consumers require no source-code migration.
- Capability-grant validation profiles introduced in 3.2.0 remain unchanged.
- Authentication, authorization, durable replay protection, signing-key custody, persistence, external execution, monitoring, and operational safety controls remain host-owned responsibilities.

### Repository metadata transition boundary

Consumers inspecting published package metadata should distinguish between
the historical and current repository locations.

Packages published before the repository transfer may contain:

```
https://github.com/cdcavell/AsiBackbone
```

Beginning with `3.2.1`, newly published AsiBackbone packages are expected to
contain:

```
https://github.com/AsiBackbone/AsiBackbone
```

Both may resolve through GitHub repository-transfer redirects, but the
organization-owned URL is canonical for `3.2.1` and later releases.

### Package signing posture

NuGet package signing remains intentionally deferred while AsiBackbone is
independently maintained.

Consumers should continue to use the official NuGet source, canonical public
repository, release tags, Source Link repository metadata, SBOMs, provenance
artifacts where available, and their own dependency-verification policy.

Source Link, SBOMs, provenance artifacts, public source availability, and
GitHub release tags are useful trust signals, but none should be interpreted
individually as a signed-package guarantee.

### Validation

The release candidate should pass:

- locked restore and Debug/Release solution builds;
- formatting, analyzer, unit, integration, and property-based tests;
- repository-wide and package-specific coverage gates;
- Core branch coverage and XML-documentation inventory validation;
- API baseline and compatibility checks;
- package creation and generated NuGet metadata validation;
- template, external-consumer, and stable-package smoke tests;
- version-consistency validation for 3.2.1 and v3.2.1;
- repository and project URL validation;
- DocFX generation and documentation release-claim validation;
- CodeQL, dependency review, OpenSSF, actionlint/Zizmor, and OWASP checks;
- SBOM and provenance handling where supported.

After publication, validate Source Link repository metadata with:


```
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.2.1
```

The expected repository URL for the `3.2.1` packages is:

```
https://github.com/AsiBackbone/AsiBackbone
```
