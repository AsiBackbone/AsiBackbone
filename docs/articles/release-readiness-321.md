
---

### `docs/articles/release-readiness-321.md`

```markdown
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

This record is a pre-tag checklist. Do not create `v3.2.1` or publish
packages until every required validation is complete on the final
release-candidate commit.

## Included scope

- Update the canonical repository URL to
  `https://github.com/AsiBackbone/AsiBackbone`.
- Update the canonical documentation URL to
  `https://asibackbone.github.io/AsiBackbone/`.
- Update NuGet repository and project metadata for `3.2.1`.
- Update Source Link validation for the organization-owned repository.
- Update `CITATION.cff` and `.zenodo.json`.
- Update SBOM document namespace and creator metadata.
- Update repository badges, security links, documentation navigation,
  GitHub Pages references, samples, package READMEs, quality documentation,
  and development-diagnostics examples.
- Update references to NetCoreApplicationTemplate to use
  `AsiBackbone/NetCoreApplicationTemplate`.
- Update template fallback package references to `3.2.1`.
- Preserve the capability-validation functionality introduced by `3.2.0`
  without changing its runtime behavior.
- Preserve the existing `AsiBackbone.*` package and namespace identity.

## Compatibility boundary

- Package IDs remain unchanged.
- Public namespaces remain unchanged.
- Public APIs remain unchanged.
- Runtime governance behavior remains unchanged.
- The target framework remains `net10.0`.
- `AssemblyVersion` remains `3.0.0.0` for the compatible `3.x` binary line.
- `FileVersion`, package version, informational version, citation metadata,
  and release metadata advance to `3.2.1`.
- No new stable public API is introduced.
- No serialized or persisted governance artifact shape is intentionally
  changed.
- No package boundary is added or removed.
- Existing `3.2.x` consumers require no source-code migration.
- NuGet package signing remains deferred while the project is independently
  maintained.

## Repository metadata transition

`3.2.1` establishes the organization-owned repository as the canonical
repository metadata location:

```text
https://github.com/AsiBackbone/AsiBackbone
```

Published packages from earlier releases may retain the historical repository
URL embedded when those immutable artifacts were created:

```
https://github.com/cdcavell/AsiBackbone
```

Release validation for `3.2.1` should verify the new canonical location
without rewriting historical package evidence.

### Version and metadata checklist

