# AsiBackbone 7.0.0 Release Readiness Record

Release candidate date: 2026-09-20

## Release intent

`7.0.0` is a major release for the AsiBackbone package family. It binds
acknowledgment responses to the actor that received the challenge, moves the DLP
failure-behavior and intent-risk enums away from permissive zero defaults,
renames the compatibility names that 6.0 retained, and renames five Entity
Framework Core columns and one JSON property name to match.

This record is a pre-tag checklist. Do not create `v7.0.0` or publish packages
until every required validation is complete on the final release-candidate
commit.

## Compatibility boundary

- Package IDs and the `net10.0` target remain unchanged.
- The `Handshakes` and `CapabilityTokens` namespaces in `AsiBackbone.Core`,
  `AsiBackbone.AspNetCore`, and `AsiBackbone.Storage.InMemory` become
  `Acknowledgments` and `CapabilityGrants`.
- The retained `AuditResidue*`, `LiabilityHandshake*`, `Handshake*`, and
  `CapabilityToken*` types and members are renamed to the decision receipt,
  acknowledgment, and capability grant vocabulary, with no `[Obsolete]`
  forwarding aliases.
- Signed and telemetry contracts keep their `6.x` values: canonical artifact
  tags, signed payload bytes, OpenTelemetry event and attribute names, EF Core
  table names, reason codes, and diagnostic IDs.
- JSON produced by serializing the renamed types uses `decisionReceiptId` where
  `6.x` used `auditResidueId`.
- Five EF Core columns are renamed, with their dependent indexes and foreign
  keys. Hosts must add a migration.
- The `GovernanceOutboxDrain` and `GovernanceOutboxDrainHostedService`
  constructors are replaced by source-compatible signatures with an optional
  `TimeProvider` parameter.
- Package version advances to `7.0.0`; `AssemblyVersion` and `FileVersion`
  advance to `7.0.0.0`.
- Acknowledgment responses must match the challenged actor's ID and type.
- `DlpFailureBehavior` and `DlpIntentRiskLevel` gain `Unspecified = 0`; every
  existing numeric value shifts by one and incomplete policy is rejected.
- NuGet package author signing remains deferred under the current decision
  record.

## Version and metadata checklist

- [x] `Directory.Build.props` resolves package version `7.0.0`.
- [x] `AssemblyVersion` and `FileVersion` are `7.0.0.0`.
- [ ] `CITATION.cff` and `.zenodo.json` report `7.0.0`, the release date, and
  the 7.0 major-release scope, including the renames and the schema change.
- [x] Template fallback package references use `7.0.0`.
- [x] NuGet lock files reference `7.0.0` for in-repository projects.
- [ ] Package validation compares `7.0.0` with the `5.1.0` baseline so the full
  two-major compatibility surface remains checked. Reviewed 6.0 breaks retain
  their exact suppressions; intentional 7.0 enum-value changes and the 7.0
  type, member, and namespace renames have exact suppressions.
- [ ] Public API baselines record the 7.0 enum values, new members, and renamed
  surface.
- [ ] `CHANGELOG.md`, release notes, and the upgrade guide describe the same
  change set and compatibility boundary.
- [ ] Evergreen documentation identifies `7.0.0` as the current release, uses
  the 7.0 names, and preserves prior records as historical evidence.

## Required validation before tag

- [x] Version consistency passes for `7.0.0` and `v7.0.0`.
- [x] Locked restore succeeds using the repository SDK and package configuration.
- [ ] Debug and Release solution builds succeed with warnings treated as errors.
- [ ] `dotnet format --verify-no-changes` succeeds.
- [ ] All tests pass, including actor-binding and DLP-default regression tests.
- [ ] Public API baseline and package-validation checks pass.
- [ ] XML-documentation inventory and enforcement checks pass.
- [ ] Documentation continuity, links, and release-claim validation pass.
- [ ] DocFX site generation succeeds with no release-blocking warnings.
- [ ] Package creation succeeds for the complete publishable package set.
- [ ] Package IDs, versions, dependencies, repository metadata, symbols, and
  README content are correct.
- [ ] Package SBOM generation succeeds.
- [ ] Template, external-consumer, and stable-package smoke tests succeed.
- [ ] CodeQL and dependency review report no blocking findings.
- [ ] Required actionlint/Zizmor, workflow-security, OWASP Dependency-Check,
  and related repository checks have no unexplained blocking findings.
- [x] No package-author-signing claim is made for unsigned project packages.

Unchecked items are publication gates. They remain unchecked until the final
release-candidate commit has the corresponding local or CI evidence. Items that
had evidence before the legacy-name rename were reset because the rename changed
the source, documentation, API surface, and packages they covered.

## Rename and schema checks

- [ ] The EF Core schema generated from the 7.0 model differs from the `6.x`
  schema only by the five documented column renames and their dependent indexes
  and foreign keys.
- [ ] An upgrade-path test creates the `6.x` schema, inserts rows into each
  affected table, applies the reference migration, and reads the rows back
  through the 7.0 model.
- [ ] The reference migration's `Down` operations restore the `6.x` schema.
- [ ] Canonical payload and signing tests confirm that artifacts signed by `6.x`
  verify under 7.0 and that artifact tags are unchanged.
- [ ] Tests assert that `DecisionReceipt`, `AuditLedgerRecord`, and
  `GovernanceEmissionEnvelope` serialize `decisionReceiptId`, and that canonical
  signed payloads are unaffected by the JSON key change.
- [ ] The upgrade guide documents the JSON key change and the effect of
  deserializing stored `6.x` JSON.
- [ ] OpenTelemetry event and attribute names are unchanged.
- [ ] No old type or namespace name remains in a string literal that refers to
  a CLR type, including analyzer metadata-name lookups, reflection, and test
  assertions.
- [ ] Samples and templates compile against the renamed surface.

## Major-release checks

Before publication, confirm that:

- the [Upgrade from 6.x to 7.0](upgrade-600-to-700.md) guide covers actor
  binding, every shifted enum value, persisted numeric data, configuration, the
  complete rename inventory, the EF Core migration, the JSON key change, and
  consumer rebuild requirements;
- the rename tables in the upgrade guide match the public API baseline diff
  between `6.0.0` and `7.0.0`;
- first-party samples, templates, tests, and documentation use explicit DLP
  enum values where required;
- actor mismatch failures disclose one stable reason code without revealing
  whether the ID or actor type differed;
- the [6.0 public API naming record](public-api-naming-600.md) notes that its
  planned deprecations were completed as renames in 7.0; and
- consumers have a documented verification path for the new major boundary,
  including the EF Core migration.

## Release sequence

1. Complete every required validation above on the final candidate commit.
2. Merge the release-preparation pull request after required checks pass.
3. Confirm `main` contains the final `7.0.0` metadata and documentation.
4. Create the annotated tag `v7.0.0` from the validated commit.
5. Run the stable release workflow against that tag.
6. Confirm all expected NuGet and symbol packages are published.
7. Confirm build packages, SBOMs, manifests, and release notes are attached to
   the GitHub release.
8. Verify package/SBOM attestations by subject digest.
9. Confirm documentation deployment succeeds.
10. Run post-publication Source Link validation:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 7.0.0
```

11. Verify package repository commit metadata resolves to the tagged source
    commit.
12. Advance the package-validation baseline only after `7.0.0` is published.
13. Record any release exception explicitly rather than weakening a gate.

## Related documentation

- [7.0.0 Release Notes](release-notes-700.md)
- [7.0.0 Consumer Verification Guide](consumer-verification-700.md)
- [Upgrade from 6.x to 7.0](upgrade-600-to-700.md)
- [Stable Release Validation](release-validation.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Public API Naming in 6.0](public-api-naming-600.md)
- [NuGet Package Signing Decision Record](nuget-package-signing-decision.md)
