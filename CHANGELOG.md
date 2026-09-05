# Changelog

All notable changes to this project are documented in this file.

This project follows the spirit of [Keep a Changelog](https://keepachangelog.com/) and adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added

* Added `CanonicalPayloadBuilder.ForCapabilityTokenGrant`, covering every field a `CapabilityTokenGrant` carries (#699). `CanonicalArtifactTypes.CapabilityTokenGrant` existed but had no builder, so every consumer invented its own payload. The repository's own reference implementations in the validator tests and the stable-package smoke script hashed four fields — `audience`, `expiresUtc`, `issuer`, and `scopes` — while `CapabilityGrantValidator` enforces `NotBeforeUtc`, `PolicyVersion`, `PolicyHash`, `AcknowledgmentId`, `HandshakeId`, `GatewayBinding`, `ResourceBinding`, `SubjectId`, `OperationName`, and `IssuedUtc` as well. Fields outside the payload are outside the proof, so a value changed after signing still validated. Scopes are normalized to a sorted, de-duplicated, ordinal set; grant metadata remains filtered through `CanonicalPayloadOptions.AllowsMetadataKey`, whose allow-list is empty by default, so metadata is unbound unless a host opts a key in. Both reference implementations now use the builder.

### Deprecated

* `RequireGovernancePolicy` on the endpoint route-builder extensions is obsolete in favor of `MarkGovernancePolicy` (#698). The framework never resolved the marked policy type or selected constraints from it, so the `Require` name overstated what the method did. The replacement records identical metadata; migrating is a rename with no behavior change. The generic `MarkGovernancePolicy<TPolicy>` overload constrains `TPolicy` to `IAsiBackboneDecisionPolicy<AsiBackboneConstraintEvaluationContext>` to point at the mechanism that actually varies behavior by policy; the `Type` overload remains unconstrained for plain marker types.

### Changed

* `endpoint.policy_types` is now retained under `AsiBackboneEndpointGovernanceMetadataMode.Reduced` (#698). It was previously dropped, so a host decision policy that varied its outcome by policy type silently stopped seeing the marker under Reduced mode and fell back to uniform behavior — a permissive change produced by a metadata setting rather than a policy one. Reduced mode still drops every other descriptor entry.
* Documented on `MarkGovernancePolicy`, `RequireGovernancePolicyAttribute`, `AsiBackboneTestHarnessPolicyEvaluator`, and `AsiBackboneTestHarnessOptions.SetPolicyResult` that an endpoint's policy type is a marker rather than an evaluation selector (#698). The registered evaluator runs every registered constraint on every governed endpoint; varying behavior by policy requires a host-supplied `IAsiBackboneDecisionPolicy` that reads `endpoint.policy_types`. The Testing harness keeps its per-policy result API, now labeled as simulating that host policy rather than framework behavior.

* **Breaking:** `AsiBackboneGovernanceOutboxOptions.UseClaimLeases` now defaults to `true`, and `ClaimWorkerId` defaults to the machine name and process identifier (#697). Two hosts running the default drain worker against the same durable outbox previously emitted every entry twice. Hosts supplying a custom outbox store that does not implement `IAsiBackboneGovernanceOutboxClaimStore` must set `UseClaimLeases` to `false`; the drain throws rather than falling back, because falling back would restore the duplicate-emission behavior the default exists to prevent. Both shipped stores are claim-capable and need no change.

### Added

* Added `AsiBackboneGovernanceOutboxOptions.ClaimPageSize` (default `10`) so a drain pass leases entries a page at a time instead of holding one lease across the whole batch (#697). A slow emitter can no longer exhaust a single lease partway through a batch and leave later entries reclaimable by a peer while they are still in flight.
* Added `AsiBackboneGovernanceOutboxOptions.MaxClaimAttempts` (default `5`), `DeadLetterOnMaxClaimAttempts`, `MaxClaimAttemptsReasonCode`, and `MaxClaimAttemptsReasonMessage` to bound reclaim (#697). An emitter that hangs or is killed mid-emission leaves an entry claimed but never failed, so its retry count never advances and the retry-based poison-message policy never applies; the claim count does advance on every reclaim. Entries past the threshold are dead-lettered before emission is attempted.

### Fixed

* Preserve delivered and dead-lettered EF Core outbox entries during non-claim saves and status transitions, including stale deferred saves (#695). Terminal no-ops preserve existing evidence and the concurrency stamp; write conflicts still propagate to the caller.

### Compatibility

* The claim-lease default change alters runtime behavior without changing any public signature. It is a behavior break for hosts that supply a non-claim-capable outbox store, and a behavior improvement for every host using the shipped stores. Claim leasing coordinates workers before provider emission; it does not by itself create an exactly-once delivery guarantee.

## [3.2.3] - 2026-08-30

### Release summary

`3.2.3` is a backward-compatible maintenance patch for the stable `3.2.x`
AsiBackbone package family. It carries forward the `3.2.2` runtime and public
API contract while modernizing repository test/coverage infrastructure and
reorganizing documentation ownership, navigation, and release evidence.

No production runtime source files under `src/AsiBackbone.*` changed relative
to `v3.2.2`. Package IDs, public namespaces, public APIs, runtime governance
behavior, the `net10.0` target, and the stable `3.x` binary assembly identity
remain unchanged.

### Changed

* Migrated repository tests from xUnit `3.2.2` / VSTest-oriented execution to
  xUnit `4.0.0` with Microsoft Testing Platform.
* Enabled Microsoft Testing Platform as the repository test runner in
  `global.json`.
* Replaced `coverlet.collector` / `coverlet.msbuild` with `coverlet.MTP` and
  added reusable MTP coverage orchestration.
* Updated CI, release-validation, package-publish, quality-report, and smoke-test
  paths for MTP-compatible execution and coverage gating.
* Defined `AsiBackbone/Learning` as the canonical organization-level
  educational source while keeping `AsiBackbone/AsiBackbone` authoritative for
  package, API, runtime, security, operations, compatibility, release, and
  maintainer documentation.
* Reorganized DocFX information architecture around implementation,
  releases/compatibility, maintainer evidence, and historical records.
* Added documentation-ownership and cross-repository link guardrails while
  preserving historical inbound URLs where appropriate.
* Consolidated the DocFX top navigation into grouped Documentation,
  Releases & Evidence, and Resources menus.
* Refreshed SHA-pinned CodeQL actions from `4.37.7` to `4.37.8`.

### Compatibility

* Package IDs remain unchanged.
* Public namespaces remain unchanged.
* Public APIs remain unchanged.
* Runtime governance behavior remains unchanged.
* Target framework remains `net10.0`.
* `AssemblyVersion` remains `3.0.0.0`.
* `FileVersion` advances to `3.2.3.0`.
* Existing `3.2.x` consumers require no source-code migration solely because
  of this patch release.
* The xUnit/MTP migration is repository test infrastructure and does not expand
  the runtime package contract.
* Authentication, authorization, persistence, signing-key custody, external
  execution, monitoring, and operational safety remain host-owned
  responsibilities.

### Package signing

NuGet package signing remains intentionally deferred while AsiBackbone is
independently maintained. Consumers should continue to use the official NuGet
source, canonical repository and release tag, Source Link metadata, SBOMs, and
provenance artifacts where available as complementary verification signals.

## [3.2.2] - 2026-08-22

### Release summary

`3.2.2` is a backward-compatible maintenance patch for the stable `3.2.x`
AsiBackbone package family. It carries forward the `3.2.1` software contract
while refreshing approved dependencies and SHA-pinned CI, security, and release
tooling.

No runtime source files changed relative to `v3.2.1`. Package IDs, public
namespaces, public APIs, runtime governance behavior, the `net10.0` target, and
the stable `3.x` binary assembly identity remain unchanged.

### Changed

* Updated `Microsoft.EntityFrameworkCore`,
  `Microsoft.EntityFrameworkCore.InMemory`,
  `Microsoft.EntityFrameworkCore.Relational`, and
  `Microsoft.EntityFrameworkCore.Sqlite` from `10.0.10` to `10.0.11`.
* Updated `Microsoft.Extensions.Logging.Abstractions` from `10.0.10` to
  `10.0.11`.
* Updated `Microsoft.NET.Test.Sdk` from `18.8.1` to `18.9.0`.
* Refreshed SHA-pinned GitHub Actions used by CI, release validation, package
  provenance, CodeQL, OpenSSF/OWASP, and workflow-security checks. The current
  release-candidate set includes CodeQL `4.37.7`,
  `actions/attest-build-provenance` `4.2.2`, and Zizmor action `0.6.2`.
* Aligned action version comments with their pinned commit SHAs in package and
  release-validation workflows.
* Normalized `.config/dotnet-tools.json` line endings without changing the
  configured tool version or tool behavior.

### Compatibility

* Package IDs remain unchanged.
* Public namespaces remain unchanged.
* Public APIs remain unchanged.
* Runtime governance behavior remains unchanged.
* Target framework remains `net10.0`.
* `AssemblyVersion` remains `3.0.0.0`.
* `FileVersion` advances to `3.2.2.0`.
* Existing `3.2.x` consumers require no source-code migration solely because
  of this patch release.
* Capability-grant validation profiles introduced in `3.2.0` remain unchanged.
* Canonical organization-owned repository and documentation metadata
  established in `3.2.1` remain unchanged.

### Package signing

NuGet package signing remains intentionally deferred while AsiBackbone is
independently maintained. Consumers should continue to use the official NuGet
source, canonical repository and release tag, Source Link metadata, SBOMs, and
provenance artifacts where available as complementary verification signals.

## [3.2.1] - 2026-08-07

### Release summary

`3.2.1` is a backward-compatible patch release for the stable `3.2.x`
AsiBackbone package family. It marks the transfer of the canonical project
repository from the personal `cdcavell` GitHub namespace to the dedicated
`AsiBackbone` organization.

The release updates repository, documentation, Source Link, citation, SBOM,
validation, workflow, sample, and package metadata to establish
`AsiBackbone/AsiBackbone` as the canonical project location.

No public API, runtime governance behavior, package ID, public namespace,
target-framework, or binary assembly identity change is introduced by this
release.

### Changed

* Updated the canonical repository location from
  `https://github.com/cdcavell/AsiBackbone` to
  `https://github.com/AsiBackbone/AsiBackbone`.
* Updated the canonical GitHub Pages documentation location to
  `https://asibackbone.github.io/AsiBackbone/`.
* Updated NuGet repository and project metadata for newly published packages.
* Updated Source Link validation expectations for `3.2.1` and later releases
  to use the organization-owned canonical repository.
* Updated citation metadata and Zenodo metadata for the organization-owned
  repository and documentation locations.
* Updated SPDX SBOM document namespace and creator metadata to reflect the
  organization-owned project location.
* Updated documentation publishing configuration, repository badges,
  documentation links, security-reporting links, samples, issue references,
  and repository navigation to use the canonical organization locations.
* Updated NetCoreApplicationTemplate references to
  `AsiBackbone/NetCoreApplicationTemplate` where applicable.
* Updated `AsiBackbone.Templates` fallback package references from `3.2.0`
  to `3.2.1`.
* Updated release-facing documentation to classify `3.2.1` as a patch release
  in the stable `3.2.x` line.

### Compatibility

* Package IDs remain unchanged.
* Public namespaces remain unchanged.
* Public APIs remain unchanged.
* Runtime governance behavior remains unchanged.
* Target framework remains `net10.0`.
* `AssemblyVersion` remains `3.0.0.0`.
* `FileVersion` advances to `3.2.1.0`.
* Existing `3.2.x` consumers require no source-code migration.
* Capability-grant validation profiles introduced in `3.2.0` are carried
  forward unchanged.
* Repository-transfer changes affect canonical project, documentation,
  Source Link, citation, SBOM, provenance, and package metadata locations
  rather than the runtime contract.

### Package signing

NuGet package signing remains intentionally deferred while AsiBackbone is
independently maintained. Release integrity continues to rely on the official
NuGet source, the canonical public repository and release tags, Source Link
metadata, SBOM generation, provenance artifacts where available, and
transparent release practices.

## [3.2.0] - 2026-08-02

### Release summary

`3.2.0` is a backward-compatible minor release for the stable `3.x`
AsiBackbone package family. It strengthens capability-grant validation at
consequential execution boundaries by introducing explicit validation
profiles that distinguish strict execution-boundary enforcement from
intentional metadata-only inspection.

The release reduces ambiguity around signed capability grants without
silently changing existing `3.x` behavior. Hosts can now opt into a
clearly named execution-boundary profile that requires proof verification
and enables bounded-use validation by default, while metadata-only
validation remains available through an explicitly named reduced-validation
profile.

AsiBackbone continues to provide governance and accountability primitives
rather than replacing host authentication, authorization, durable replay
storage, signature-key custody, external execution controls, or compliance
responsibility.

### Added

* Added `CapabilityGrantValidationOptions.CreateExecutionBoundary(...)`
  for consequential execution and operational-gateway validation. (#649)
* Added `CapabilityGrantValidationOptions.CreateMetadataValidation(...)`
  for intentional structural, temporal, scope, policy, and binding
  validation without proof or bounded-use enforcement. (#649)
* Added execution-boundary defaults that require signed-artifact proof
  verification and enable bounded-use validation with a default maximum
  use count of one. (#649)
* Added explicit support for hosts to disable the execution-boundary
  bounded-use check when replay or use enforcement is owned atomically by
  another trusted host boundary. (#649)
* Added focused tests covering validation-profile defaults, missing proof
  verification, unavailable bounded-use storage, metadata-only validation,
  and preservation of the legacy no-options behavior. (#649)
* Added documentation describing execution-boundary, metadata-only, and
  backward-compatible `Create(...)` validation semantics. (#649)
* Added `3.x` migration guidance for consumers adopting the explicit
  validation profiles. (#649)

### Changed

* Updated execution-boundary guidance and examples to prefer
  `CreateExecutionBoundary(...)` and to proceed only when
  `CapabilityGrantValidationResult.ShouldAllow` is `true`. (#649)
* Clarified that `CapabilityGrantValidator.ValidateAsync(signedGrant)`
  without explicit validation options preserves the existing `3.x`
  metadata-oriented behavior and does not automatically verify proof or
  perform bounded-use/replay checks. (#649)
* Clarified metadata-only validation semantics so a successful result is
  not interpreted as proof authenticity, replay resistance,
  authentication, authorization, or permission to execute an external
  action. (#649)
* Updated `SQLitePCLRaw.bundle_e_sqlite3` from `3.0.3` to `3.0.4` and
  refreshed the affected reproducible NuGet lock files.
* Updated repository GitHub Actions dependencies, including CodeQL,
  OpenSSF Scorecard, Zizmor, checkout, and .NET setup actions.
* Preserved the existing `CapabilityGrantValidationOptions.Create(...)`
  defaults and the no-options `ValidateAsync(...)` path for compatibility
  throughout the `3.x` line.

### Fixed

* Fixed PowerShell environment-variable expansion in the quality-report
  workflow so Core branch-coverage validation receives the configured
  project, configuration, coverage-output, threshold, and workspace
  values correctly.
* Updated the corresponding quality-report coverage input path to use
  the corrected PowerShell environment-variable syntax.

### Compatibility

* Package IDs and public namespaces remain unchanged.
* The target framework remains `net10.0`.
* `AssemblyVersion` remains `3.0.0.0` for compatible `3.x` releases.
* The new capability-grant validation profiles are additive.
* Existing consumers using `CapabilityGrantValidationOptions.Create(...)`
  retain their current validation behavior.
* Existing consumers using `ValidateAsync(signedGrant)` without explicit
  options are not silently moved to proof or bounded-use enforcement.
* Hosts remain responsible for authentication, authorization, durable and
  concurrency-safe replay protection, signing-key trust, persistence,
  external execution, and operational safety controls.


## [3.1.0] - 2026-07-20

### Release summary

`3.1.0` is a backward-compatible minor release for the stable `3.x`
AsiBackbone package family. It expands the stable public surface around
host execution accountability, capability-proof trust policy, and
ASP.NET Core actor classification while preserving package IDs, public
namespaces, the `net10.0` target, and the stable `AssemblyVersion` of
`3.0.0.0`.

The release strengthens the evidence chain between governance decisions
and host-owned execution outcomes without transferring execution,
authentication, authorization, persistence, key custody, monitoring, or
compliance responsibility from the consuming host to AsiBackbone.

### Added

* Added `GovernedOperationExecutionReceipt`, a provider-neutral completion
  receipt for binding governance lifecycle evidence to one host-owned
  execution attempt. (#634)
* Added `GovernedOperationPersistenceOutcome` with explicit outcomes for
  committed, failed, rolled-back, and completed-without-mutation
  execution attempts. (#634)
* Added canonical execution-receipt payload construction suitable for
  hashing, signing, verification, and durable evidence workflows. (#634)
* Added stable host-accountability metadata keys and lifecycle helpers
  that preserve decision correlation while linking logical execution,
  retry attempt, and optional host mutation batch identifiers. (#634)
* Added optional capability-proof trust pins for signing key ID, key
  version, proof policy version, proof policy hash, verification
  provider, and proof hash algorithm. (#638)
* Added a provider-neutral `VerificationPolicyContext` to capability-proof
  validation so proof-signing metadata can be constrained independently
  from capability-grant policy metadata. (#638)
* Added conservative ASP.NET Core actor-type claim controls through
  `AllowedActorTypesFromClaims` and
  `DefaultAuthenticatedActorType`. (#639)
* Added explicit host opt-in before `System`, `Service`, `Agent`, or
  `Unknown` actor types supplied through HTTP claims are honored. (#639)
* Added documentation and tests covering trusted actor-type claim
  boundaries, capability-proof trust pinning, host mutation
  accountability, and audit-integrity sequence handling.
* Added an isolated, source-neutral NCAT audit-completion reference
  adapter and test project outside the required AsiBackbone package
  graph. The adapter is sample-only and non-packable. (#636)
* Added property-based tests for governance metadata normalization,
  including idempotence and bounded-output properties.
* Added OpenSSF Scorecard, workflow-security, actionlint/Zizmor, and
  OWASP Dependency-Check workflows for complementary repository and
  supply-chain analysis.
* Added reproducible NuGet restore lock files across source, test,
  benchmark, sample, and platform-specific Aspire projects.

### Changed

* Promoted central package version metadata from `3.0.1` to `3.1.0`.
* Updated `FileVersion` to `3.1.0.0` while preserving
  `AssemblyVersion` at `3.0.0.0` for the compatible `3.x` binary line.
* Updated citation metadata, Zenodo metadata, template fallback package
  references, Source Link validation defaults, README release posture,
  documentation navigation, release guidance, and consumer verification
  documentation for `3.1.0`.
* Changed claim-based actor classification to accept only defined actor
  types explicitly permitted by the host-configured allow-list.
* Changed the conservative actor-type claim default to accept only
  `Human`; other actor categories require explicit host authorization.
* Changed unrecognized, undefined, or disallowed actor-type claim values
  to fall back to `DefaultAuthenticatedActorType`.
* Updated dependency-management workflows to use reproducible locked
  restores and recognize solution, platform-specific lock-file, and
  dependency-related path changes.
* Moved scheduled Dependabot updates to Friday so dependency changes can
  be reviewed before weekend security scans.
* Scoped GitHub Actions permissions more narrowly across CI,
  documentation, quality-report, package-publishing, and attestation
  jobs.
* Updated selected GitHub Actions to immutable versions or digests where
  appropriate.

### Fixed

* Fixed audit-integrity verification so the outer verification loop has
  sole ownership of recording accepted sequence values. (#640)
* Fixed duplicate audit sequence handling so repeated values are
  classified consistently as `ForkedChain` before continuity checks are
  evaluated. (#640)
* Removed the temporary add/remove accepted-sequence pattern from
  individual audit-link verification. (#640)
* Fixed the hosted governance outbox worker leaving a losing polling
  delay scheduled after an options-change signal completed first. The
  delay is now canceled and disposed before returning. (#641)
* Removed a redundant capability-validation verification-action
  pass-through helper while preserving existing validation behavior.
  (#641)
* Fixed GitHub Actions permission findings by moving privileged
  permissions to only the jobs that require them.
* Fixed unsafe GitHub Actions expression expansion that could permit
  workflow command or script injection.
* Fixed release-workflow PowerShell environment-variable interpolation
  that previously used non-interpolating string syntax.
* Fixed OpenSSF Scorecard workflow parsing and SARIF-upload behavior.
* Fixed dependency-scanning noise caused by cross-ecosystem CVE
  associations by adding narrow, documented, expiring suppressions.
* Removed the redundant explicit Source Link package dependency while
  preserving the Source Link support supplied by the .NET SDK.

### Security and governance hardening

* Privileged actor classifications supplied through HTTP claims are no
  longer trusted merely because their text parses as an enum value.
* Hosts must establish a trusted identity-provider or host-generated
  claim boundary before permitting non-human actor categories.
* Capability proofs may now be restricted to an expected signing
  authority, key version, policy identity, provider, and hash algorithm.
* A cryptographically valid proof can be rejected when its signing
  metadata does not satisfy the host's configured trust policy.
* Governed execution receipts carry minimized opaque identifiers,
  counts, hashes, timestamps, provider information, and bounded metadata
  instead of copying raw application values into AsiBackbone.
* Audit-integrity duplicate and fork classification is deterministic and
  retains stable failure codes.
* Repository workflows apply narrower token permissions and stronger
  dependency and action pinning.
* Reviewed OWASP suppressions are package-specific, ecosystem-specific,
  documented, and expiration-bound.
* NuGet package signing remains intentionally deferred while AsiBackbone
  is independently maintained. Official NuGet publication, the public
  repository, release tags, Source Link, SBOMs, provenance, and
  reproducible release practices remain the current supply-chain
  verification posture.

### Compatibility notes

* Package IDs and public namespaces remain unchanged.
* The target framework remains `net10.0`.
* `AssemblyVersion` remains `3.0.0.0`; `FileVersion` advances to
  `3.1.0.0`.
* The new Core and ASP.NET Core public APIs are additive.
* Hosts that currently map `System`, `Service`, `Agent`, or `Unknown`
  from a trusted actor-type claim must explicitly add those values to
  `AllowedActorTypesFromClaims`.
* Without explicit actor-type opt-in, authenticated claim mapping falls
  back to `DefaultAuthenticatedActorType`, which defaults to `Human`.
* Capability-proof trust pins are optional. Existing consumers that do
  not configure them retain the previous proof-verification behavior.
* `GovernedOperationExecutionReceipt` does not persist host mutations,
  authenticate actors, authorize execution, manage transactions, or
  replace the host's authoritative audit store.
* The NCAT audit-completion adapter is sample-only and non-packable; it
  does not add NCAT to the stable package dependency graph.
* No production key-management, host execution, robotics, legal, or
  compliance capability is introduced by this release.

### Validation

* Release validation covers locked restore, Debug and Release solution
  builds, formatting, analyzers, tests, property-based tests, coverage
  gates, Core branch coverage, XML-documentation inventory, API
  compatibility, package creation, generated NuGet metadata, template
  and consumer smoke tests, version consistency, DocFX, documentation
  release-claim validation, CodeQL, dependency review, OpenSSF
  Scorecard, actionlint/Zizmor, OWASP Dependency-Check, SBOM generation,
  and provenance handling where supported.
* After publication, Source Link repository commit metadata can be
  validated with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.1.0
```

## [3.0.1] - 2026-07-13

### Release summary

`3.0.1` is a compatible patch release for the stable `3.0.x` AsiBackbone package family. It hardens threat-assessment validation, protects framework-generated threat provenance, and ensures restrictive threat decisions retain the reason associated with the outcome that was actually selected.

This release does not add a new stable public feature surface. Package IDs, public namespaces, the `net10.0` target, and the host-owned execution boundary remain unchanged.

### Changed

* Promoted central package version metadata from `3.0.0` to `3.0.1`.
* Updated `FileVersion` to `3.0.1.0` while preserving `AssemblyVersion` at `3.0.0.0` for compatible `3.x` releases.
* Updated citation metadata, Zenodo metadata, template fallback package references, Source Link validation defaults, README release posture, DocFX navigation, release guidance, and consumer verification documentation for `3.0.1`.
* Defined the complete normalized, case-insensitive `threat.*` metadata namespace as framework-owned provenance.

### Fixed

* Fixed undefined numeric `ThreatSeverity` and `GovernanceDecisionOutcome` values being accepted during `ThreatAssessment` construction. Invalid values now fail explicitly and follow the existing fail-closed contributor-exception path when produced inside a registered contributor. (#612)
* Fixed contributor metadata being able to overwrite canonical threat provenance fields such as `threat.category`, `threat.severity`, `threat.recommended_outcome`, `threat.effective_outcome`, `threat.confidence`, and `threat.contributor`. (#613)
* Fixed earlier warning reasons being attached to later `Deferred`, `AcknowledgmentRequired`, or `EscalationRecommended` outcomes. The evaluator now retains the first contributor reason matching the selected restrictive outcome in deterministic registration order. (#614)
* Preserved existing multi-reason denial aggregation, warning-only aggregation, cancellation propagation, and critical runtime failure propagation.

### Security and governance hardening

* Invalid threat-assessment enum values can no longer enter the decision pipeline as apparently valid governance data.
* Framework-generated threat provenance remains authoritative and cannot be replaced by contributor-supplied `threat.*` metadata.
* Restrictive non-denial decisions now explain the outcome that actually blocked or redirected execution, improving audit, telemetry, and API-response integrity.
* NuGet package signing remains intentionally deferred while AsiBackbone is independently maintained; official NuGet publication, the public repository, release tags, Source Link, SBOMs, provenance, and reproducible release practices remain the current supply-chain verification posture.

### Compatibility notes

* Package IDs and public namespaces remain unchanged.
* The target framework remains `net10.0`.
* `AssemblyVersion` remains `3.0.0.0`; `FileVersion` advances to `3.0.1.0`.
* Callers that pass undefined or deserialized numeric enum values into `ThreatAssessment` may now receive `ArgumentOutOfRangeException`.
* Contributors using custom metadata keys beginning with `threat.` must move those fields to a host-owned or extension-owned namespace.
* No production key-management, replay-protection, host execution, robotics, legal, or compliance capability is introduced by this patch.

### Validation

* Release validation covers Debug and Release solution builds, formatting, analyzers, tests, coverage gates, Core branch coverage, XML-documentation inventory, API compatibility, package creation, NuGet metadata, template and consumer smoke tests, version consistency, DocFX, documentation release-claim validation, CodeQL, dependency review, SBOM generation, and provenance handling where supported.
* After publication, Source Link repository commit metadata can be validated with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.0.1
```
## [3.0.0] - 2026-07-13

### Release summary

`3.0.0` establishes the stable `3.x` AsiBackbone package family and advances the project as a provider-neutral Accountable Systems Infrastructure governance spine for governed .NET decision flow.

This is a major release because it:

* starts a new binary identity line with `AssemblyVersion` `3.0.0.0`;
* intentionally targets `.NET 10` through `net10.0`;
* changes empty-policy and eligible constraint-exception handling to fail closed by default;
* adds threat-model, metadata-sanitation, regulated-governance, durable outbox coordination, signing, verification, and release-quality hardening.

The existing `AsiBackbone.*` package IDs and public namespaces remain unchanged. Consumers should update package references, rebuild, and validate host behavior, especially policy defaults, durable outbox processing, signing configuration, and assembly-loading assumptions.

### Added

* Added threat-model extension points through `IThreatModelContributor<TContext>`, `ThreatAssessment`, `ThreatSeverity`, and conventional threat categories.
* Added provider-neutral governance metadata classification and sanitation abstractions with ordered allow, warn, redact, drop, and deny behavior.
* Added `DefaultGovernanceMetadataSanitizer` with post-sanitation metadata-budget enforcement and fail-closed denial behavior.
* Added the regulated governance deployment profile through `AddAsiBackboneRegulatedGovernance()` and `UseRegulatedGovernanceProfile()`.
* Added explicit strict-governance profile helpers through `AddAsiBackboneStrictGovernance()` and `UseStrictGovernanceProfile()`.
* Added provider-neutral governance outbox claim and lease models through `IAsiBackboneGovernanceOutboxClaimStore`.
* Added opt-in claim-based outbox draining with worker identity, opaque claim tokens, lease expiration, and claim-attempt tracking.
* Added `IAsiBackboneGovernanceOutboxClaimOutcomeStore` with explicit `Applied`, `StaleClaim`, `Terminal`, `ConcurrencyLost`, and `Missing` transition outcomes.
* Added provider-neutral poison-message and maximum failed-attempt controls for governance outbox draining.
* Added `InMemoryCapabilityGrantUseStore` as a non-durable reference implementation for tests, samples, smoke tests, and local replay validation.
* Added configurable `CapabilityGrantValidationOptions.AllowedClockSkew` while preserving zero-skew behavior by default.
* Added persistence of audit-ledger signing hash, key version, provider, and signing timestamp through the EF Core audit store.
* Added shared `AsiBackboneIdentifierLimits.MaximumLength` guidance for bounded identifiers.
* Added bounded exponential managed-key retry backoff with jitter and the `MaxRetryDelay` configuration boundary.
* Added a provider-neutral managed-key metadata allowlist for safe region, zone, service, request, status, and key-state diagnostics.
* Added an explicit `LocalDevelopmentSigningOptions.MinimumKeySizeBits` contract for local-development RSA keys.
* Added reusable framework-neutral contract fixtures under `AsiBackbone.Testing.Contracts`.
* Added a 3.0.0 consumer verification guide covering package source, package IDs, Source Link, SBOMs, provenance, target framework, and deferred package signing.
* Added regulated signing and storage verification guidance covering canonical hashing, signature verification, retention, key rotation, revocation, hash chains, and external anchoring.
* Added a governance and standards crosswalk covering major AI governance, cybersecurity, privacy, and interoperability references without claiming certification.
* Added Debug solution build coverage validation and reviewed exclusions.
* Added tracked XML-documentation and package-specific coverage baselines.
* Added targeted mutation-analysis reports for Core, ASP.NET Core, managed-key metadata boundaries, and OpenTelemetry event-name mapping.
* Added automated validation for stale evergreen current-release documentation claims.

### Changed

* Promoted central package version metadata from `2.3.0` to `3.0.0`.
* Updated `AssemblyVersion` from `2.0.0.0` to `3.0.0.0`.
* Updated `FileVersion` to `3.0.0.0`.
* Updated `CITATION.cff` and `.zenodo.json` for the `3.0.0` release.
* Confirmed `net10.0` as the intentional target framework for the `3.0.x` package family.
* Changed `AsiBackbonePolicyEvaluatorOptions.DenyWhenNoConstraints` to default to `true`.
* Changed `AsiBackbonePolicyEvaluatorOptions.TreatConstraintExceptionAsDenial` to default to `true`.
* Preserved explicit opt-outs for hosts that intentionally require permissive empty-policy evaluation or propagated constraint exceptions.
* Hardened threat-contributor exception handling so eligible contributor failures default to denied governance outcomes while cancellation and critical runtime failures continue to propagate.
* Froze policy-evaluator options after successful validation so constructed evaluators cannot observe later caller-side configuration drift.
* Changed the default local-development and managed-key signature descriptors to RSA-PSS terminology.
* Changed local-development signing and verification to use RSA-PSS with SHA-256.
* Changed local-development signing key configuration so values below 2048 bits fail explicitly instead of being silently replaced with the default.
* Changed managed-key retries from a fixed delay to bounded exponential backoff with jitter.
* Changed managed-key provider metadata handling from permissive normalization to a narrow, bounded allowlist.
* Changed the hosted governance outbox worker so runtime `Enabled` changes can pause, enable, and re-enable draining without restarting the process.
* Clarified that disabling the hosted drain prevents subsequent cycles but does not cancel a cycle already in progress.
* Defined non-claim EF Core outbox concurrency conflicts as caller-owned and preserved the original `DbUpdateConcurrencyException`.
* Retained the provider-neutral EF Core per-row claim implementation after performance evaluation; future bulk-claim optimization remains a provider-specific extension boundary.
* Updated README, package documentation, DocFX navigation, release guidance, templates, Source Link defaults, security posture, governance wording, and current-release claims for the `3.x` line.
* Documented that NuGet package signing is intentionally deferred while AsiBackbone remains independently maintained.

### Fixed

* Fixed fail-open empty-policy behavior by making denial the default when no constraints are configured.
* Fixed ambiguity around ordinary constraint failures by making eligible exceptions auditable denied decisions by default.
* Fixed local-development hash comparison by using fixed-time comparison for verification hashes.
* Fixed hostile client correlation values reaching governance, logging, or persistence boundaries by rejecting overlength, whitespace-only, and control-character-containing values.
* Fixed EF Core audit append failures exposing raw provider exception messages through returned operation results.
* Fixed EF Core audit persistence so verification-relevant signing metadata survives durable round trips.
* Fixed outbox claimed-transition attribution so a losing worker cannot report another worker's durable success as its own.
* Fixed stale, superseded, terminal, deleted-row, and optimistic-concurrency claim-transition handling through explicit durable outcomes.
* Fixed startup-disabled outbox workers terminating instead of remaining available for runtime re-enablement.
* Fixed local-development signing key configuration errors being hidden by silent coercion.
* Fixed local-development signing disposal so repeated and simple concurrent disposal remains idempotent.
* Fixed managed-key provider metadata exposure by dropping unrecognized, sensitive, malformed, oversized, or control-character-containing fields.
* Fixed retry coordination pressure caused by fixed managed-key delays.
* Fixed template fallback package references so generated fallback projects use `3.0.0`.
* Fixed Source Link validation defaults so post-publication validation targets `3.0.0`.
* Fixed package publication discovery so template payload projects cannot enter the package publishing candidate set.
* Fixed stale current-release documentation that still presented historical `1.x` or `2.x` lines as current.
* Fixed package-signing documentation so SBOMs and provenance are not described as signed-package guarantees.

### Security and governance hardening

* Empty policies and eligible policy failures now fail closed by default.
* Threat-model contributors run ahead of ordinary constraint composition and cannot be silently downgraded from actionable threat results to pure allow decisions.
* Regulated-profile metadata is sanitized before policy evaluation, capability validation, audit emission, acknowledgment challenge creation, or endpoint execution.
* Client-supplied correlation identifiers are treated as untrusted boundary data.
* Managed-key provider metadata is minimized through an explicit allowlist and bounded size limits.
* Local-development RSA keys require a minimum size of 2048 bits.
* Audit-ledger signing metadata remains queryable for later signature verification, key-rotation review, and incident analysis.
* Outbox poison-message handling provides stable terminal reason codes while leaving alerting, replay, incident handling, and manual remediation host-owned.
* Package signing remains deferred; the current supply-chain trust posture relies on official NuGet publication, the public repository, release tags, Source Link, SBOMs, provenance, and reproducible release practices.

### Reliability and concurrency

* Outbox claim leases provide explicit worker ownership before provider emission.
* Claimed transitions can distinguish applied writes from stale claims, durable terminal rows, concurrency winners, and deleted entries.
* Non-claim EF Core mutations continue to surface optimistic-concurrency exceptions without hidden retries.
* Runtime outbox enable, disable, and re-enable changes do not create scopes or resolve scoped stores while disabled.
* Managed-key retry waits increase with bounded jitter and remain cancellation-aware.
* Policy-evaluator configuration is stable after construction.
* Local-development RSA operations and disposal use synchronized lifecycle access.

### Performance and quality

* Verified that the provider-neutral EF Core claim path uses one candidate query plus one read and one optimistic-concurrency update per successful claim.
* Documented the resulting `1 + 2N` command shape for uncontended claim batches.
* Retained provider-neutral behavior rather than adding unverified provider-conditional raw SQL.
* Reduced policy evaluator, endpoint-governance, audit-residue, reason-normalization, OpenTelemetry mapping, and metadata-classification complexity or allocation pressure.
* Expanded BenchmarkDotNet coverage for evaluator composition, first-denial short-circuiting, governance decisions, operation results, and audit residue creation.
* Added deterministic branch and lifecycle coverage across Core, ASP.NET Core, EF Core, OpenTelemetry, analyzers, testing contracts, in-memory storage, and signing packages.
* Added package-specific line-coverage baselines alongside the repository-wide coverage gate and Core branch-coverage gate.
* Added tracked XML-documentation debt validation so new public API documentation gaps cannot silently increase.
* Added release-line documentation validation to detect stale current-version claims.

### Documentation

* Added 3.0.0 release notes, release readiness, and consumer verification guidance.
* Added target-framework support guidance documenting the `.NET 10+` boundary.
* Added production managed-key integration guidance while preserving provider-neutral key custody.
* Added regulated deployment, signing verification, storage, metadata sanitation, threat modeling, and outbox-delivery guidance.
* Added explicit host-responsibility boundaries for authentication, authorization, policy registration, execution enforcement, durable storage, key custody, replay protection, monitoring, incident response, legal review, and compliance interpretation.
* Clarified that AsiBackbone is a governance spine rather than an intelligence engine, AI model host, robot controller, compliance certification, or complete tamper-evidence platform.
* Clarified that signed, verified, chained, retained, immutable, externally anchored, legally admissible, and compliance-approved are separate claims.
* Documented the decision to defer NuGet package signing while the project remains solo-maintained.

### Compatibility notes

* Package IDs remain unchanged.
* Public namespaces remain unchanged.
* No broad package rename or namespace migration is included.
* The package family intentionally targets `net10.0`; consumers require a .NET 10 SDK/runtime or later.
* `AssemblyVersion` changes to `3.0.0.0`; consumers should rebuild and validate strict assembly loading, plugin discovery, binding redirects, and binary compatibility assumptions.
* `DenyWhenNoConstraints` now defaults to `true`. Hosts requiring permissive empty-policy behavior must explicitly set it to `false`.
* `TreatConstraintExceptionAsDenial` now defaults to `true`. Hosts requiring fail-fast propagation must explicitly set it to `false`.
* Local-development RSA key sizes below 2048 bits now fail configuration validation rather than silently using 2048 bits.
* Local-development signing uses RSA-PSS by default; fixtures expecting the previous PKCS#1 v1.5 descriptor should be regenerated or explicitly configured for a test-only compatibility path.
* Managed-key retry timing is no longer fixed when retry delay is enabled.
* Existing outbox claim convenience methods remain available; outcome-aware consumers can opt into the new explicit transition-result contract.
* `InMemoryCapabilityGrantUseStore` remains non-durable and is not production replay protection.
* Strict and regulated governance profiles do not replace host-owned authentication, authorization, policy, persistence, signing, monitoring, or execution controls.
* Event Hubs, Purview, Azure-specific non-signing adapters, Aspire runtime packages, robotics, immutable storage, and additional provider packages remain outside the stable contract unless separately reviewed and released.

### Validation

* Release candidates are validated through Debug and Release solution builds, formatting, unit tests, repository and package coverage gates, Core branch coverage, XML-documentation debt validation, CodeQL, dependency review, package creation, NuGet metadata checks, SBOM generation, template smoke validation, external-consumer smoke tests, stable-package integration tests, version consistency, DocFX generation, documentation release-claim validation, and provenance handling where supported.
* Release validation confirms that package metadata, README content, release notes, release readiness, templates, Source Link defaults, consumer verification guidance, and this changelog agree on the `3.0.0` / `3.x` release posture.
* After publication, Source Link repository commit metadata can be validated with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 3.0.0
```

## [2.3.0] - 2026-07-06

### Release summary

`2.3.0` is a compatible minor release for the stable `2.x` package family.

This release preserves the `2.0.0` public package and namespace boundary while adding host-facing governance guardrails, safer managed-key signing defaults, diagnostic policy signals, outbox/query hardening, endpoint-governance validation cleanup, template fallback alignment, tests, and release metadata alignment.

### Added

* Added optional `GovernanceMetadataBudget` and `GovernanceMetadataBudgetValidator` helper APIs for host-owned metadata count, key length, value length, serialized-size, and reserved-key validation.
* Added opt-in `AsiBackbonePolicyEvaluatorOptions.TreatConstraintExceptionAsDenial` behavior so hosts can convert constraint exceptions into denied governance decisions with configured safe reason codes.
* Added managed-key local-validation helpers through `CreateLocalValidation(...)` and `AddAsiBackboneManagedKeySigningForLocalValidation(...)` for samples, tests, diagnostics, and explicit host fallback policies.
* Added warning-level logging when policy evaluation runs with zero constraints while permissive empty-policy behavior remains enabled.
* Added an outbox claim/lease design record documenting future opt-in multi-worker claim behavior without mutating existing find/read APIs.
* Added focused tests for metadata budgets, constraint-exception handling, managed-key fail-closed defaults, empty-policy warning diagnostics, EF Core outbox ordering/max-count behavior, template fallback versions, and collection-backed governance reason normalization.
* Added `2.3.0` release notes and a `2.3.0` release readiness record.

### Changed

* Promoted central package version metadata from `2.2.1` to `2.3.0` while preserving `AssemblyVersion` as `2.0.0.0` for the compatible stable `2.x` line.
* Updated `FileVersion` to `2.3.0.0`.
* Updated `CITATION.cff` and `.zenodo.json` for the `2.3.0` release.
* Updated README, documentation home, article index, DocFX article navigation, release validation, API compatibility / SemVer guidance, template documentation, release notes, release readiness guidance, template fallback package versions, and Source Link validation defaults for the `2.3.0` package family.
* Changed managed-key signing to fail closed by default by making `ManagedKeySigningOptions.ReturnUnsignedOnFailure` default to `false` in production-oriented paths.
* Removed repeated endpoint-governance option validation from the request path while preserving fail-closed startup/configured-options validation.
* Pushed EF Core governance outbox pending and retry-ready filtering, ordering, and `Take(maxCount)` into database queries before materialization.
* Added deterministic `OutboxEntryId` tie-breakers and related index coverage guidance for provider-neutral outbox drain paths.
* Updated template fallback package references from `2.2.1` to `2.3.0`.

### Fixed

* Fixed template fallback package version drift so generated fallback `.csproj` files use the repository package version.
* Preserved empty and all-null governance decision reason fallback behavior while optimizing collection-backed reason normalization to use a single enumeration pass for non-empty `ICollection<OperationReason>` inputs.
* Preserved cancellation propagation for `OperationCanceledException` in the new constraint-exception policy path.
* Preserved default permissive empty-policy behavior while documenting it as a named sharp edge and recommending fail-closed configuration/startup validation.
* Preserved endpoint-governance configuration validation through registration/configured-options validation and `ValidateOnStart` rather than per-request validation.

### Security and governance hardening

* Managed-key signing now fails closed by default in production-oriented registration paths; hosts that intentionally need unsigned failure metadata should opt into local-validation or explicit fallback behavior.
* Metadata budget validation provides a host-owned guardrail for audit, telemetry, and signing metadata before durable or external-provider boundaries.
* Constraint-exception denial conversion is opt-in and uses safe, stable reason codes without exposing exception details through public decision reasons.
* Empty-policy warning diagnostics make permissive no-constraint evaluation visible without changing the existing default allow behavior.

### Performance and reliability

* Reduced EF Core outbox selection materialization pressure by applying filtering, ordering, and max-count limits in the database query.
* Reduced endpoint-governance request-path overhead by relying on startup/configured-options validation rather than repeated per-request validation.
* Reduced governance decision reason normalization enumeration for collection-backed reason inputs.
* Incorporated dependency/tooling updates for BenchmarkDotNet, dotnet-stryker, Roslyn analyzer packages, and CodeQL actions.

### Compatibility notes

* Existing `2.0.x`, `2.1.x`, `2.2.0`, and `2.2.1` consumers should continue to compile against existing APIs.
* `2.3.0` is a minor release because it includes optional public/helper APIs, host-facing option surfaces, validation helpers, implementation hardening, tests, documentation, and release metadata alignment.
* No package ID or namespace changes are included.
* No public API removals or renamed public APIs are intended.
* `AssemblyVersion` remains `2.0.0.0` for the compatible stable `2.x` line.
* Hosts using `AsiBackbone.Signing.ManagedKey` should review the managed-key fail-closed default and opt into local-validation/fallback behavior only when unsigned failure metadata is intentionally required.

## [2.2.1] - 2026-07-03

### Release summary

`2.2.1` is a compatible patch release for the stable `2.x` package family.

This release preserves the `2.0.0` public package and namespace boundary while adding BenchmarkDotNet allocation baselines and reducing allocation pressure across outbox drain, endpoint governance, policy evaluation, and audit residue decision-record hot paths. No public API expansion, package ID change, or namespace change is intended.

### Added

* Added BenchmarkDotNet allocation baselines for policy evaluation, endpoint governance, outbox drain, scoped outbox drain, and audit residue creation.
* Added benchmark documentation and profiling guidance for repeatable allocation measurement.
* Added focused tests for allocation-sensitive hot-path behavior and branch coverage preservation.
* Added release notes and a release readiness record for the `2.2.1` release.

### Changed

* Promoted central package version metadata from `2.2.0` to `2.2.1` while preserving `AssemblyVersion` as `2.0.0.0`.
* Updated `FileVersion` to `2.2.1.0`.
* Updated `CITATION.cff` and `.zenodo.json` for the `2.2.1` release.
* Updated README, documentation home, article index, DocFX navigation, release validation, API compatibility / SemVer guidance, release notes, release readiness guidance, and Source Link validation defaults.

### Performance

* Reduced allocation pressure in provider-neutral outbox drain batch handling.
* Reduced endpoint governance hot-path allocations through descriptor metadata caching and deferred fallback object creation.
* Reduced policy evaluator allocation overhead by reusing pass-through constraint results and avoiding eager reason-list allocation.
* Reduced audit residue decision-record allocation by reusing immutable reason-code collections and replacing hot-path enum formatting with stable outcome-name helpers.

### Compatibility notes

* Existing stable `2.0.x`, `2.1.x`, and `2.2.0` consumers should be able to upgrade to `2.2.1` without required source-code changes.
* `2.2.1` is a patch release because it focuses on benchmark-backed implementation hardening, allocation reduction, tests, documentation, and release metadata alignment.
* `AssemblyVersion` remains `2.0.0.0` for the compatible stable `2.x` line.

## [2.2.0] - 2026-07-01

### Release summary

`2.2.0` is a compatible minor release for the stable `2.x` package family.

This release preserves the `2.0.0` public package and namespace boundary while adding opt-in endpoint-governance reduced metadata mode and incorporating endpoint governance and Core decision hot-path allocation refinements.

### Added

* Added `AsiBackboneEndpointGovernanceMetadataMode` with default `Full` behavior and opt-in `Reduced` behavior.
* Added `AsiBackboneEndpointGovernanceOptions.MetadataMode` so high-throughput ASP.NET Core hosts can forward reduced endpoint metadata through governance evaluation, audit residue, acknowledgment challenge metadata, and development diagnostics.
* Added focused tests for default full metadata, reduced descriptor metadata, reduced evaluator metadata, and reduced development diagnostic metadata.
* Added release notes and a release readiness record for the `2.2.0` release.

### Changed

* Promoted central package version metadata from `2.1.1` to `2.2.0` while preserving `AssemblyVersion` as `2.0.0.0`.
* Updated `FileVersion` to `2.2.0.0`.
* Updated `CITATION.cff` and `.zenodo.json` for the `2.2.0` release.
* Updated README, documentation home, article index, DocFX navigation, release validation, API compatibility / SemVer guidance, release notes, release readiness guidance, and Source Link validation defaults.

### Performance

* Reduced repeated optional dependency resolution in endpoint governance evaluation.
* Reduced avoidable allocation churn in `GovernanceDecision.NormalizeReasons`.
* Added reduced endpoint-governance metadata mode for measured high-throughput production paths that can safely operate with only `endpoint.operation_name` metadata.

### Compatibility notes

* Existing stable `2.0.x` and `2.1.x` consumers should be able to upgrade to `2.2.0` without required source-code changes.
* `2.2.0` is a minor release because it includes backward-compatible public APIs and options.
* `AssemblyVersion` remains `2.0.0.0` for the compatible stable `2.x` line.

## [2.1.1] - 2026-06-28

### Fixed
- Updated the package README image markup for NuGet.org compatibility.
- Replaced unsupported HTML image/alignment tags with standard Markdown image syntax so the social preview image renders correctly on the NuGet package page instead of appearing as literal HTML text.
- Adjusted the README image reference to use a NuGet-compatible hosted image URL.

## [2.1.0] - 2026-06-28

### Release summary

`2.1.0` is a compatible minor release for the stable `2.x` package family.

This release preserves the `2.0.0` public package and namespace boundary while adding backward-compatible policy-pipeline ergonomics, audit-residue construction helpers, benchmark guidance, custom decision-policy examples, documentation alignment, and in-memory outbox hardening.

### Added

* Added `AsiBackbonePolicyEvaluatorOptions.ShortCircuitOnFirstDenial`, defaulting to `false`, so existing full constraint aggregation remains the default audit-friendly behavior.
* Added ASP.NET Core endpoint metadata support for the optional first-denial fast-abort preference through attribute, route-builder extension, endpoint descriptor, and descriptor metadata surfaces.
* Added `AuditResidueBuilder` as a fluent construction path for complex audit residue values while preserving existing immutable `AuditResidue.Create`, `FromDecision`, and `FromConstraint` factories.
* Added an isolated benchmark console project for core policy-pipeline measurement under `benchmarks/AsiBackbone.Benchmarks`.
* Added benchmark scenarios for policy evaluation, policy composition, and audit residue creation.
* Added custom decision-policy examples covering strict deny-wins composition, warning preservation, regional/local overlays, acknowledgment-required outcomes, gateway readiness checks, and latency-sensitive orchestration.
* Added release notes and a release readiness record for the `2.1.0` release.

### Changed

* Promoted central package version metadata from `2.0.2` to `2.1.0` while preserving `AssemblyVersion` as `2.0.0.0` for the compatible stable `2.x` line.
* Updated `FileVersion` to `2.1.0.0`.
* Updated `CITATION.cff` and `.zenodo.json` for the `2.1.0` release.
* Updated README, documentation home, article index, DocFX article navigation, release validation, API compatibility / SemVer guidance, release notes, release readiness guidance, and Source Link validation defaults for the `2.1.0` package family.
* Replaced stale alpha-era wording in current policy-evaluator documentation with stable package-family wording.

### Fixed

* Hardened in-memory governance outbox transition behavior so same-entry state transitions remain terminal and compare-and-swap style updates do not accidentally overwrite delivered or dead-lettered records.
* Consolidated repeated project-boundary disclaimers around the canonical Project Boundaries and Non-Claims guidance.
* Clarified release cadence and readiness guidance for patch, minor, and major release classification.
* Refreshed documentation branding/navigation alignment after the `2.0.2` package-icon correction.

### Validation

* Release-candidate validation is expected to pass through CI, Stable Release Validation, package metadata validation, template package smoke validation, external consumer smoke tests, stable package integration smoke tests, DocFX build, package SBOM generation, and artifact provenance handling before tagging.
* Version-consistency validation should pass for `2.1.0`, including `Directory.Build.props`, `CITATION.cff`, `.zenodo.json`, optional tag `v2.1.0`, and generated package filenames when package artifacts are supplied.
* After packages are published and visible on NuGet, Source Link repository commit metadata should be validated with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 2.1.0
```

### Compatibility notes

* Existing stable `2.0.0`, `2.0.1`, and `2.0.2` consumers should be able to upgrade to `2.1.0` without required source-code changes for existing APIs.
* `2.1.0` is a minor release because it includes additive public APIs, options, developer-experience tooling, tests, and documentation alignment.
* `AssemblyVersion` remains `2.0.0.0` for the compatible stable `2.x` line.
* `FileVersion` should be updated to `2.1.0.0`.
* Package `Version` and `InformationalVersion` should be updated to `2.1.0`.
* Event Hubs, Purview, Azure-specific SDK adapters, Aspire runtime packages, robotics, immutable-storage, and additional provider packages remain outside the stable package contract unless separately reviewed and released.

## [2.0.2] - 2026-06-26

### Release summary

`2.0.2` is a compatible patch release for the stable `2.0.x` package family.

This release corrects the package-facing icon presentation issue discovered after `2.0.1`. The previous package icon asset could render incompletely in package-list and package-detail contexts. `2.0.2` preserves the `2.0.0` public package and namespace boundary while aligning release metadata and documentation for the corrected package presentation release.

### Fixed

* Corrected the package icon asset used for NuGet and repository/package presentation.
* Rebuilt `PACKAGE-ICON.png` from the source SVG so the icon renders as a complete image rather than an incomplete or partially rendered PNG.
* Preserved bounded project language around Accountable Systems Infrastructure, host-owned execution, package-signing status, and non-claims.

### Changed

* Promoted central package version metadata from `2.0.1` to `2.0.2` while preserving `AssemblyVersion` as `2.0.0.0` for the compatible `2.x` line.
* Updated `FileVersion` to `2.0.2.0`.
* Updated `CITATION.cff` and `.zenodo.json` for the `2.0.2` release.
* Updated README, documentation home, article index, table of contents, release validation, API compatibility / SemVer guidance, release notes, release readiness guidance, and Source Link validation defaults for the `2.0.2` package family.

### Validation

* Release-candidate validation is expected to pass through CI, Stable Release Validation, package metadata validation, template package smoke validation, external consumer smoke tests, stable package integration smoke tests, DocFX build, package SBOM generation, and artifact provenance handling before tagging.
* Version-consistency validation should pass for `2.0.2`, including `Directory.Build.props`, `CITATION.cff`, `.zenodo.json`, optional tag `v2.0.2`, and generated package filenames when package artifacts are supplied.
* Package icon validation should confirm that `PACKAGE-ICON.png` is included in generated `.nupkg` artifacts and renders correctly in package-list and package-detail contexts.
* After packages are published and visible on NuGet, Source Link repository commit metadata should be validated with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 2.0.2
```

### Compatibility notes

* Existing stable `2.0.0` and `2.0.1` consumers should be able to upgrade to `2.0.2` without required source-code changes.
* `2.0.2` is a patch release focused on package/repository icon presentation metadata, release metadata, documentation alignment, and validation guidance.
* `AssemblyVersion` remains `2.0.0.0` for the compatible stable `2.x` line.
* `FileVersion` should be updated to `2.0.2.0`.
* Package `Version` and `InformationalVersion` should be updated to `2.0.2`.
* Event Hubs, Purview, Azure-specific SDK adapters, Aspire runtime packages, robotics, immutable-storage, and additional provider packages remain outside the stable package contract unless separately reviewed and released.

## [2.0.1] - 2026-06-26

### Release summary

`2.0.1` is a compatible patch release for the stable `2.0.x` package family.

This release preserves the `2.0.0` public package and namespace boundary while tightening post-`2.0.0` documentation alignment, release-path SBOM/provenance handling, repository/package icon metadata, and release-facing version metadata. No runtime behavior changes or breaking public API changes are intended.

`AssemblyVersion` remains fixed at `2.0.0.0` for the compatible stable `2.x` line. Package `Version`, `FileVersion`, and `InformationalVersion` move to `2.0.1`.

### Added

* Added package-level SBOM generation for produced `.nupkg` artifacts, including SPDX JSON output and an SBOM manifest.
* Added package and SBOM artifact provenance attestation steps where GitHub artifact attestations are available for the workflow event.
* Added refreshed text-free repository/package icon assets for README, DocFX, favicon, and NuGet package metadata.
* Added `2.0.1` release notes and a dedicated `2.0.1` release readiness record.

### Changed

* Promoted central package version metadata from `2.0.0` to `2.0.1` while preserving `AssemblyVersion` as `2.0.0.0` for the compatible `2.x` line.
* Updated `FileVersion` to `2.0.1.0`.
* Updated `CITATION.cff` and `.zenodo.json` for the `2.0.1` release.
* Updated README, documentation home, article index, table of contents, release validation, API compatibility / SemVer guidance, Source Link validation defaults, and release-readiness guidance for the `2.0.1` package family.
* Updated stable release workflows and release validation guidance to include package SBOM generation and package/SBOM provenance artifact handling.
* Refreshed post-`2.0.0` documentation currency while keeping host-owned production boundaries explicit.

### Fixed

* Corrected stale post-`2.0.0` references in current documentation navigation and release-validation guidance.
* Replaced the previous repository/package branding assets with a small-size-friendly governance spine icon for README, documentation, favicon, and package metadata contexts.
* Preserved bounded project language around Accountable Systems Infrastructure, host-owned execution, package-signing status, and non-claims.

### Validation

* Release-candidate validation is expected to pass through CI, Stable Release Validation, package metadata validation, template package smoke validation, external consumer smoke tests, stable package integration smoke tests, DocFX build, package SBOM generation, and artifact provenance handling before tagging.
* Version-consistency validation should pass for `2.0.1`, including `Directory.Build.props`, `CITATION.cff`, `.zenodo.json`, optional tag `v2.0.1`, and generated package filenames when package artifacts are supplied.
* After packages are published and visible on NuGet, Source Link repository commit metadata should be validated with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 2.0.1
```

### Compatibility notes

* Existing stable `2.0.0` consumers should be able to upgrade to `2.0.1` without required source-code changes.
* `2.0.1` is a patch release focused on release readiness, documentation alignment, package SBOM/provenance hardening, repository/package branding, and metadata updates.
* `AssemblyVersion` remains `2.0.0.0` for the compatible stable `2.x` line.
* `FileVersion` should be updated to `2.0.1.0`.
* Package `Version` and `InformationalVersion` should be updated to `2.0.1`.
* Event Hubs, Purview, Azure-specific SDK adapters, Aspire runtime packages, robotics, immutable-storage, and additional provider packages remain outside the stable package contract unless separately reviewed and released.

## [2.0.0] - 2026-06-24

### Breaking Changes

* Renamed NuGet package IDs from `CDCavell.AsiBackbone.*` to `AsiBackbone.*`.
* Renamed public namespaces from `CDCavell.AsiBackbone.*` to `AsiBackbone.*`.
* Consumers must update package references and `using` statements when migrating from the 1.x package line.

### Changed

* Updated project/package identity to align the public NuGet package names with the simplified `AsiBackbone.*` namespace.
* Updated source namespaces, project references, and documentation references to use the new `AsiBackbone.*` naming convention.
* Prepared the package family for the new 2.x release line.

### Migration Notes

Replace package references such as:

```xml
<PackageReference Include="CDCavell.AsiBackbone.Core" Version="1.x.x" />
```

with:

```xml
<PackageReference Include="AsiBackbone.Core" Version="2.0.0" />
```

Replace namespace imports such as:

```csharp
using CDCavell.AsiBackbone.Core;
```

with:

```csharp
using AsiBackbone.Core;
```

### Notes

The previous `CDCavell.AsiBackbone.*` packages should be treated as the 1.x package line. The `AsiBackbone.*` packages begin the 2.x package line and represent the preferred package identity going forward.

## [1.2.1] - 2026-06-24

### Release summary

`1.2.1` is a compatible patch release for the stable `1.2.x` package family.

This release preserves the `1.2.0` package/API boundary while hardening release metadata, Source Link repository-commit metadata, package-signing wording, workflow hygiene, documentation alignment, and release-validation guidance. No breaking public API changes are intended.

`AssemblyVersion` remains fixed at `1.0.0.0` for the compatible stable `1.x` line. Package `Version`, `FileVersion`, and `InformationalVersion` move to `1.2.1`.

### Added

* Added Source Link metadata support across package projects and samples so generated packages can include repository commit metadata when built with Source Link enabled.
* Added `scripts/Validate-Source-Link-commit-metadata.ps1` for post-publish validation of NuGet repository metadata and Source Link commit metadata.
* Added `1.2.1` release notes and a dedicated `1.2.1` release readiness record.
* Added explicit .NET Foundation Code of Conduct alignment wording.

### Changed

* Promoted central package version metadata from `1.2.0` to `1.2.1` while preserving `AssemblyVersion` as `1.0.0.0` for the compatible `1.x` line.
* Updated `CITATION.cff` and `.zenodo.json` for the `1.2.1` release.
* Updated README, documentation home, article index, table of contents, release validation, API compatibility / SemVer guidance, template package documentation, and quality posture for the `1.2.1` package family.
* Updated package-signing wording to clarify that current NuGet packages are not signed release artifacts from the project maintainer.
* Updated the Source Link validation script default package version to `1.2.1`.
* Refreshed GitHub Actions checkout usage to `actions/checkout` `v7.0.0`.
* Normalized GitHub workflow YAML files to LF line endings to prevent repeated local modification noise after checkout or hard reset.

### Fixed

* Hardened release-validation documentation so generated NuGet metadata and post-publish Source Link metadata checks are called out explicitly.
* Clarified `1.2.1` as a patch release on the `1.2.0` minor-release boundary rather than a new package/API expansion.
* Preserved bounded project language around Accountable Systems Infrastructure, host-owned execution, package-signing status, and non-claims.

### Validation

* Release-candidate validation is expected to pass through CI, Stable Release Validation, package metadata validation, template package smoke validation, external consumer smoke tests, stable package integration smoke tests, and DocFX build before tagging.
* Version-consistency validation should pass for `1.2.1`, including `Directory.Build.props`, `CITATION.cff`, `.zenodo.json`, optional tag `v1.2.1`, and generated package filenames when package artifacts are supplied.
* After packages are published and visible on NuGet, Source Link repository commit metadata should be validated with:

```powershell
./scripts/Validate-Source-Link-commit-metadata.ps1 -Version 1.2.1
```

### Compatibility notes

* Existing stable `1.2.0` consumers should be able to upgrade to `1.2.1` without required source-code changes.
* `1.2.1` is a patch release focused on release readiness, package metadata, Source Link validation, workflow hygiene, documentation, and implementation hardening.
* `AssemblyVersion` remains `1.0.0.0` for the compatible stable `1.x` line.
* `FileVersion` should be updated to `1.2.1.0`.
* Package `Version` and `InformationalVersion` should be updated to `1.2.1`.
* Event Hubs, Purview, Azure-specific SDK adapters, robotics, immutable-storage, and additional provider packages remain outside the stable package contract unless separately reviewed and released.

## [1.2.0] - 2026-06-20

### Release summary

`1.2.0` is a compatible minor release for the stable `1.x` package family.

This release promotes post-`1.1.x` adoption, diagnostics, testing, templates, samples, documentation alignment, and project-governance work into the current stable release line. No breaking public API changes are intended.

`AssemblyVersion` remains fixed at `1.0.0.0` for the compatible stable `1.x` line. Package `Version`, `FileVersion`, and `InformationalVersion` move to `1.2.0`.

### Added

* Added `AsiBackbone.DependencyInjection` with an explicit `AddAsiBackbone(...)` builder facade for host-selected provider registration.
* Added `AsiBackbone.Testing` as a test-only package for deterministic endpoint-governance harnesses, policy-result shaping, in-memory inspection, and no-signature signing seams.
* Added `AsiBackbone.Templates` with `dotnet new asibackbone-webapi` scaffolding for governed ASP.NET Core hosts.
* Added opt-in ASP.NET Core endpoint-governance development diagnostics gated by explicit configuration and development-environment checks.
* Added a sample-first .NET Aspire AppHost path without introducing an Aspire runtime package.
* Added reference-deployment evidence for the Plain ASP.NET Core host sample.
* Added visual governance flow diagrams for intent-to-execution, policy evaluation, acknowledgment, capability tokens, and durable outbox/emission.
* Added `1.2.0` release notes and a `1.2.0` release readiness record.
* Added project governance and contribution documentation, including code of conduct and governance process documentation.

### Changed

* Promoted central package version metadata from `1.1.1` to `1.2.0` while preserving `AssemblyVersion` as `1.0.0.0` for the compatible `1.x` line.
* Updated README, documentation home, article index, table of contents, release validation, API compatibility, citation metadata, and Zenodo metadata for the `1.2.0` package family.
* Clarified that `1.2.0` is a minor release because it includes additive public/package surfaces and developer-experience tooling.
* Improved implementation-first documentation, search/navigation guidance, release-readiness guidance, and package-family wording.
* Clarified that Aspire remains a sample path, not a runtime package boundary.

### Fixed

* Fixed DocFX/Mermaid rendering for the Dynamic Liability Handshake diagram.
* Fixed the documentation workflow Core coverage input path so Core branch coverage report generation consumes the docs workflow test output.
* Corrected documentation evaluation findings around package-family listings, glossary wording, and SemVer expectations.

### Validation

* Release-candidate validation is expected to pass through CI, Stable Release Validation, package metadata validation, template package smoke validation, external consumer smoke tests, stable package integration smoke tests, and DocFX build before tagging.
* Version-consistency validation should pass for `1.2.0`, including `Directory.Build.props`, `CITATION.cff`, `.zenodo.json`, optional tag `v1.2.0`, and generated package filenames when package artifacts are supplied.

### Compatibility notes

* Existing stable `1.x` consumers should be able to upgrade to `1.2.0` without required source-code changes for existing APIs.
* `1.2.0` is a minor release because it includes additive public APIs, packages, diagnostics, samples, templates, and documentation alignment.
* `AssemblyVersion` remains `1.0.0.0` for the compatible stable `1.x` line.
* `FileVersion` should be updated to `1.2.0.0`.
* Package `Version` and `InformationalVersion` should be updated to `1.2.0`.
* Event Hubs, Purview, Azure-specific SDK adapters, Aspire runtime packages, robotics, immutable-storage, and additional provider packages remain outside the stable package contract unless separately reviewed and released.

## [1.1.1] - 2026-06-20

### Release summary

`1.1.1` is a compatible patch release for the stable `1.1.x` package family.

This release focuses on post-`1.1.0` hardening, documentation clarity, endpoint-governance safety, telemetry bounds, allocation reduction, coverage enforcement, and test expansion. No breaking public API changes are intended.

`AssemblyVersion` remains fixed at `1.0.0.0` for the compatible stable `1.x` line. Package `Version`, `FileVersion`, and `InformationalVersion` move to `1.1.1`.

### Added

* Added optional ASP.NET Core endpoint-governance strict mode through `RequireGovernanceMetadata`.
* Added explicit public/excluded endpoint metadata so hosts can allow known public endpoints when strict endpoint-governance mode is enabled.
* Added `AllowMissingGovernanceMetadataAttribute` for explicit host-owned endpoint exclusions.
* Added Core-only branch coverage enforcement with a stricter branch coverage expectation for `AsiBackbone.Core`.
* Added focused test coverage for endpoint governance route-builder behavior.
* Added additional branch and behavior tests for capability grants, signing and verification, canonical payloads, governance emission, outbox behavior, DLP/classification policy, audit integrity, and operation/decision results.
* Added documentation for progressive adoption, API-gating, host-owned execution, DLP/classification scanner seams, safe audit/telemetry data, outbox reliability, outbox concurrency, and terminology mapping.

### Changed

* Clarified stable `1.x` API compatibility and semantic versioning guidance.
* Clarified that `1.1.1` is a patch release that preserves the stable `1.x` binary identity by keeping `AssemblyVersion` at `1.0.0.0`.
* Reorganized documentation navigation to emphasize current stable usage, package-selection guidance, provider boundaries, quality reports, and historical records.
* Reframed alpha-era documents as historical records rather than current release guidance.
* Reconciled production wording around signing, verification, and tamper-evidence boundaries.
* Clarified design-only provider documentation boundaries for Event Hubs, Purview, Azure-specific adapters, robotics, immutable-storage, and future provider packages.
* Updated quality documentation to distinguish repository-wide line coverage, Core-only branch coverage, and targeted mutation analysis.
* Updated release validation and readiness guidance so it can be reused for future stable `1.x` patch and minor releases.

### Fixed

* Bounded normalized `GovernanceDecision` correlation and trace identifiers to deterministic 256-character maximums.
* Preserved existing blank-to-null telemetry identifier behavior while trimming and truncating overlong telemetry identifiers safely.
* Corrected stale namespace references from earlier capability-token documentation.
* Reduced ambiguity in API compatibility documentation around released stable provider packages versus future/design-only provider plans.
* Clarified that endpoint governance does not replace ASP.NET Core authentication, authorization, routing, middleware enforcement, persistence, UI, or execution controls.

### Performance

* Reduced default ASP.NET Core middleware 403 allocation pressure by using a cached, bodyless forbidden result by default.
* Added an opt-in forbidden-result factory so hosts can still return richer safe responses when desired.
* Reduced avoidable Core allocation overhead in reason handling for `GovernanceDecision`, `ConstraintEvaluationResult`, and `OperationResult`.

### Security and governance hardening

* Added optional fail-closed endpoint governance behavior for hosts that require every selected endpoint to carry explicit AsiBackbone governance metadata.
* Preserved existing default endpoint-governance behavior for backward compatibility.
* Reinforced host-owned execution boundaries.
* Clarified DLP/classification scanner integration boundaries and fail-open/fail-closed behavior by risk level.
* Replaced the vulnerable/deprecated SQLite native transitive restore path used by SQLite-backed sample and test projects.

### Compatibility notes

* Existing `1.1.0` consumers should be able to upgrade to `1.1.1` without required source-code changes.
* Endpoint-governance strict mode is opt-in.
* Event Hubs, Purview, Azure-specific SDK adapters, robotics, immutable-storage, and additional provider packages remain outside the stable package contract unless separately reviewed and released.

## [1.1.0] - 2026-06-16

### Added

* Added provider-neutral governance emission contracts and envelope/result/error primitives for downstream observability and governance projection.
* Added durable governance outbox and audit residue lifecycle surfaces so local accountability records can be preserved before provider emission is attempted.
* Added `AsiBackbone.OpenTelemetry` as the first concrete governance emission provider package for projecting governance envelopes into `ActivitySource` and `Meter` diagnostics.
* Added `AsiBackbone.Analyzers` as Roslyn analyzer safety rails for governance persistence and continuation flows.
* Added signing-ready receipt, canonical hashing/signing, and verification-policy seams while keeping Core provider-neutral.
* Added `AsiBackbone.Signing.LocalDevelopment` for local-development RSA signing and verification in tests, samples, and wiring proof paths.
* Added `AsiBackbone.Signing.ManagedKey` as a provider-neutral managed-key signing adapter boundary where the host supplies the actual managed-key client and operational policy.
* Added ASP.NET Core endpoint governance metadata and hosted outbox drain integration.

### Changed

* Promoted central package version metadata from `1.0.0` to `1.1.0` while preserving `AssemblyVersion` as `1.0.0.0` for the compatible `1.x` line.
* Updated release-facing README, citation metadata, Zenodo metadata, release notes, package validation anchors, and release guidance for the `1.1.0` package family.
* Expanded package validation to include Analyzers, OpenTelemetry, Signing.LocalDevelopment, and Signing.ManagedKey package artifacts.
* Clarified that OpenTelemetry is a provider projection path and that Azure Monitor remains host-configured through the host OpenTelemetry pipeline.
* Clarified signing language so provider signing is described as one part of an operational trust model, not production tamper-evidence or non-repudiation by itself.

### Validation

* Version-consistency validation is expected to pass for `1.1.0`, including `Directory.Build.props`, `CITATION.cff`, `.zenodo.json`, tag version checks, and generated package filenames when package artifacts are supplied.
* NuGet metadata validation now expects the `1.1.0` stable package-family README wording and all eight package artifacts.
* Release validation and package publish workflows should restore, build, format-check, test, build DocFX, pack packages, validate package metadata, and run package smoke checks before publication.

### Boundary Notes

* `1.1.0` remains Accountable Systems Infrastructure and governance spine infrastructure, not an artificial superintelligence implementation, AI model host, robot controller, legal/compliance guarantee, or production tamper-evident ledger provider.
* Event Hubs, Purview, Azure-specific SDK adapters, robotics/physical execution, immutable storage, and external anchoring remain outside the `1.1.0` stable package boundary unless separately released as stable packages.
* The managed-key signing adapter does not ship live Azure Key Vault, Managed HSM, cloud KMS, HSM, certificate-store, or credential-provider implementation by default.

## [1.0.0] - 2026-06-14

### Added

* Finalized the first stable package-family release boundary for `AsiBackbone.Core`, `AsiBackbone.Storage.InMemory`, `AsiBackbone.EntityFrameworkCore`, and `AsiBackbone.AspNetCore`.
* Added release-ready metadata alignment across `Directory.Build.props`, `CITATION.cff`, `.zenodo.json`, release notes, and version-consistency validation.
* Added schema-version stamping for durable or exported governance artifacts that require stable migration anchors.
* Added stable package integration and external consumer smoke-test coverage to validate consumer-style package usage before publication.
* Added generated `.nupkg` metadata validation for package IDs, descriptions, tags, license metadata, project URL, repository URL, and packaged README files.

### Changed

* Promoted package version metadata from `0.4.0-alpha.3` to `1.0.0` without a preview suffix.
* Updated citation and Zenodo metadata to describe the `1.0.0` release consistently.
* Updated documentation that described the package family as alpha so current release-facing pages describe the stable package line instead.
* Updated NuGet package descriptions and tags to use bounded Accountable Systems Infrastructure wording and package-specific stable roles.

### Validation

* Version-consistency validation now checks central MSBuild version metadata, package project metadata, `CITATION.cff`, `.zenodo.json`, optional release tag metadata, and generated package names when package artifacts are supplied.
* NuGet metadata validation now inspects generated package artifacts before stable release validation and package publication continue.
* Release notes, schema-version guidance, API compatibility expectations, privacy/signing boundaries, and known limitations are documented for the `1.0.0` release path.

### Boundary Notes

* `1.0.0` remains Accountable Systems Infrastructure and governance spine infrastructure, not an artificial superintelligence implementation, AI model host, robot controller, legal/compliance guarantee, or tamper-evident ledger provider.
* Future signing, gateway, cloud observability, and robotics/provider packages remain outside the stable `1.0.0` contract unless separately released as stable.

## [0.4.0-alpha.3] - 2026-06-13

### Added

* Added an external consumer smoke-test workflow for package ergonomics validation.
* Added `eng/smoke-tests/external-consumer-smoke.sh` to pack local package artifacts, generate a clean temporary xUnit consumer project, install AsiBackbone packages from a local NuGet source, and run HTTP-based smoke assertions.
* Added external consumer validation for:

  * Core + ASP.NET Core adapter registration through `AddAsiBackboneAspNetCore()`.
  * host-owned `DbContext` integration for the EF Core audit ledger path.
  * in-memory audit storage for minimal non-durable hosts.
  * allow, deny, and acknowledgment-required HTTP decision flows.
* Added external consumer smoke-test documentation under Quality Reports.
* Added the external consumer smoke test to quality documentation navigation.
* Added focused Core mutation-survivor triage documentation covering evaluator, decision, audit, and handshake behavior.
* Added mutation-focused Core tests for:

  * deny-wins evaluator composition and reason aggregation.
  * warning-only evaluator composition.
  * full constraint-result visibility to decision policy.
  * cancellation between evaluator constraints.
  * non-allow governance decision factory propagation.
  * read-only decision reason snapshots.
  * audit residue trace, policy, metadata, and actor propagation.
  * liability handshake request and acknowledgment boundary behavior.
* Added governance tool comparison documentation comparing Azure Policy, Open Policy Agent, Microsoft Agent Governance Toolkit, and AsiBackbone as complementary governance layers.

### Changed

* Updated quality documentation to include external consumer smoke-test guidance.
* Updated quality documentation to link Core test triage from the Quality Reports index.
* Updated the external consumer smoke-test workflow to run on pull requests, pushes to `main`, and manual dispatch.
* Updated the external consumer smoke-test script to run the generated consumer project outside the repository tree, preventing inheritance of repository Central Package Management settings.
* Updated the external consumer smoke-test script to normalize package output paths before use.
* Updated PR validation expectations to include the external consumer package smoke test as a required confidence check.

### Fixed

* Fixed CI formatting failures in Core mutation-focused test files.
* Fixed external consumer smoke-test path resolution so generated project paths remain valid after directory changes.
* Fixed external consumer smoke-test package installation failures caused by generated projects inheriting repository `Directory.Packages.props` settings.

### Validation

* Confirmed the Release test suite passes with 385 tests, 0 failed, and 0 skipped after formatting cleanup.
* Added package-shaped validation that verifies a clean consumer-style host can wire AsiBackbone packages without project references.
* Verified the smoke-test design exercises allow, deny, and acknowledgment-required flows through HTTP.
* Verified host-owned persistence boundaries remain explicit for the EF Core ledger path.

### Boundary Notes

* The external consumer smoke test is a package-consumer ergonomics check, not a production host template.
* The generated smoke project intentionally avoids repository project references.
* EF Core persistence remains host-owned: the host supplies the `DbContext`, provider, connection string, schema lifecycle, and migration strategy.
* In-memory audit storage remains non-durable and intended only for tests, samples, and local validation.
* AsiBackbone remains Accountable Systems Infrastructure and governance spine infrastructure, not an intelligence engine, AI model host, or artificial superintelligence implementation.

## [0.4.0-alpha.2] - 2026-06-12

### Added

* Added Stryker.NET as a local .NET tool for mutation-analysis validation.
* Added an initial Core test-project Stryker configuration for evaluator and policy-pipeline mutation testing.
* Added an ASP.NET Core test-project Stryker configuration for acknowledgment challenge mutation testing.
* Added mutation-focused ASP.NET Core acknowledgment challenge tests covering safe-default challenge shaping and response conversion.
* Added a Quality Reports landing page for coverage and mutation-analysis reports.
* Added Quality to the DocFX top navigation.

### Changed

* Updated the documentation publishing workflow to generate and publish the Core mutation report alongside the existing coverage report.
* Updated the release/manual quality workflow to generate separate Core and ASP.NET Core mutation reports.
* Updated DocFX content configuration so the Quality Reports landing page is included in the documentation site.

### Documentation

* Clarified that ASI means **Accountable Systems Infrastructure** within the AsiBackbone software project.
* Updated the README, documentation index, Getting Started guide, Why AsiBackbone article, and Core domain language article to frame AsiBackbone as governance infrastructure rather than artificial superintelligence.
* Added Accountable Systems Infrastructure to the core domain language and alignment guidance.

### Boundary Notes

* Reinforced that AsiBackbone does not implement artificial superintelligence, host or train AI models, control robots, or prove the Eden/Backbone framework.
* Clarified that broader Eden/Backbone concepts may inspire the package while implementation claims remain limited to practical software governance.

## [0.4.0-alpha.1] - 2026-06-11

### Samples and Host Validation

### Added

* Added `samples/PlainAspNetCoreHost` as the canonical in-repository ASP.NET Core validation sample.
* Added a plain ASP.NET Core sample project demonstrating:

  * `AddAsiBackboneAspNetCore()` registration.
  * host-defined constraint evaluation.
  * host-defined decision policy behavior.
  * acknowledgment-required decision flow.
  * in-memory audit residue capture.
  * EF Core audit ledger persistence through a host-owned `DbContext`.
  * SQLite-based local validation.
* Added sample documentation for the plain ASP.NET Core host.
* Added DocFX article for the plain ASP.NET Core host sample.
* Added DocFX article documenting `NetCoreApplicationTemplate` as an optional external local validation host.
* Added package-reference and local project-reference guidance for validating AsiBackbone against a `NetCoreApplicationTemplate`-generated host.
* Added host-owned EF Core integration guidance showing `ApplyAsiBackboneConfigurations()`.
* Added temporary validation endpoint sketch for external host validation.
* Added targeted branch coverage tests for:

  * ASP.NET Core options.
  * request correlation resolution.
  * acknowledgment challenge handling.
  * EF Core audit ledger edge paths.

### Changed

* Updated documentation navigation to include sample and host-validation guidance.
* Updated README links to reference the new sample and host-validation documentation.
* Clarified that the plain ASP.NET Core host sample is the canonical in-repository validation baseline.
* Clarified that `NetCoreApplicationTemplate` is a preferred external validation host, not a required dependency or parent framework.

### Validation

* Confirmed the solution builds successfully in Release configuration.
* Confirmed the full test suite passes in Release configuration.
* Regenerated local coverage after targeted branch coverage additions.

### Boundary Notes

* No AsiBackbone project references `NetCoreApplicationTemplate`.
* No in-repository `NetCoreApplicationTemplate` sample was added.
* `NetCoreApplicationTemplate` remains optional and external.
* AsiBackbone remains governance infrastructure, not an intelligence engine, AI model host, or ASI implementation.

## [0.3.0-alpha.1] - 2026-06-11

### Added

* Added the initial `AsiBackbone.AspNetCore` alpha integration package.
* Added ASP.NET Core service registration extensions through `AddAsiBackboneAspNetCore(...)`.
* Added configurable ASP.NET Core integration options with startup validation.
* Added an HTTP actor context adapter for resolving Core-compatible actor context from `HttpContext.User`.
* Added configurable claim mapping for actor identifiers, display names, and actor type.
* Added safe unauthenticated actor handling without throwing during normal request flow.
* Added ASP.NET Core request correlation support for resolving correlation identifiers, trace identifiers, and safe request metadata.
* Added audit enrichment helpers for creating Core audit residue from HTTP request correlation data.
* Added HTTP result mapping helpers for Core `GovernanceDecision` and `OperationResult` values.
* Added host-overridable HTTP result mapping options for allowed, warning, denied, deferred, acknowledgment-required, escalation-recommended, and failed operation outcomes.
* Added Problem Details-style responses for non-success governance and operation outcomes.
* Added safe default response behavior that preserves reason codes and correlation identifiers while hiding reason messages, trace identifiers, policy versions, and policy hashes unless explicitly enabled.
* Added ASP.NET Core acknowledgment challenge models and service support for Core `AcknowledgmentRequired` governance decisions.
* Added acknowledgment challenge response handling that round-trips accepted or rejected responses into Core `LiabilityHandshakeAcknowledgment` values.
* Added tests for service registration, actor context resolution, request correlation, audit enrichment, HTTP result mapping, and acknowledgment challenge handling.

### Documentation

* Added ASP.NET Core integration boundary documentation.
* Added ASP.NET Core package README guidance for service registration, request correlation, audit enrichment, HTTP result mapping, and acknowledgment challenge usage.
* Documented the package as a thin web-host adapter around Core governance primitives.
* Documented that hosts remain responsible for authentication, authorization, persistence, routing, UI rendering, endpoint exposure, and operational execution.

### Boundaries

* The ASP.NET Core package keeps Core framework-neutral and free of ASP.NET Core dependencies.
* The ASP.NET Core package does not register EF Core, persistence stores, authentication handlers, MVC, Razor Pages, Minimal API endpoints, middleware enforcement, policy evaluators, or NetCoreApplicationTemplate dependencies by default.
* HTTP result mapping and acknowledgment challenge helpers are explicit host adapters and do not enforce decisions automatically.
* Hosts choose how to render, store, protect, and round-trip acknowledgment challenge state.

### Notes

* This alpha release establishes the first web-host integration layer for the AsiBackbone package family.
* The implementation is intentionally adapter-focused: it translates ASP.NET Core request context into Core governance language and translates Core outcomes into HTTP-friendly shapes when explicitly used by the host.

## [0.2.0-alpha.1] - 2026-06-10

### Added

* Added EF Core `ModelBuilder` extension support for host-owned persistence integration.
* Added `ApplyAsiBackboneConfigurations(this ModelBuilder modelBuilder)` for applying AsiBackbone EF Core model contributions from a consuming application's `DbContext`.
* Added tests proving the extension can be called from a host-owned `DbContext`.
* Added argument validation for null `ModelBuilder` usage.
* Added provider-neutral EF Core persistence entities and configurations for audit ledger records, reason codes, metadata, handshake requests, and handshake acknowledgments.
* Added an EF Core-backed audit ledger store for append-oriented accountability persistence through a host-owned `DbContext`.
* Added EF Core tests proving host-owned DbContext integration, model metadata, keys, relationships, indexes, enum conversion, and basic persistence behavior.
* Added SQLite-backed EF Core integration tests proving relational schema creation, persistence/readback, and query behavior without package-owned migrations.
* Added EF Core host ownership and migration guidance documentation.
* Added package-specific README files for the EF Core and in-memory storage packages.

### Fixed

* Updated EF Core documentation samples to show host applications calling the extension from `OnModelCreating`.
* Aligned EF Core documentation with the implemented `AsiBackbone.EntityFrameworkCore` package name.
* Normalized EF Core configuration folder and file paths.
* Updated the root README to describe the current 0.2 persistence package status.
* Cleared the EF Core change tracker after audit ledger append failures so failed append entities do not remain tracked in the host-owned context.

### Notes

* The EF Core integration preserves host ownership of the `DbContext`, database provider, connection string, migrations, deployment process, and schema lifecycle.
* AsiBackbone contributes model configuration; the consuming application remains the persistence composition root.
* Wired the configurations through the existing `ApplyAsiBackboneConfigurations` `ModelBuilder` extension.
* The in-memory storage package remains non-durable and intended only for tests, samples, and local validation hosts.

## [0.1.0-alpha.2] - 2026-06-09

### Added

* Added a host-neutral Core policy evaluator contract and default policy evaluator implementation.
* Added a decision policy extension point for raising composed decisions to deferred, acknowledgment-required, or escalation-recommended outcomes.
* Added an audit sink contract for writing audit residue without requiring a database or web host.
* Added an in-memory audit ledger project for local validation, samples, and tests.
* Added branch-focused unit tests for `AuditLedgerRecord.FromResidue`.
* Added branch-focused unit tests for `DefaultAsiBackbonePolicyEvaluator<TContext>`.
* Added end-to-end policy evaluator tests covering allow, deny, warning, acknowledgment-required, escalation-recommended, deferred, and not-applicable constraint scenarios.
* Added policy evaluator pipeline documentation with a minimal in-memory usage example.

### Fixed

* Aligned policy evaluator tests with the intended constraint-versus-decision policy boundary.
* Preserved elevated-risk warnings as constraint-layer results instead of replacing them in the decision policy layer.
* Updated test expectations to match current `AsiBackbone` assembly casing.
* Added explicit switch handling for low-risk and elevated-risk document policy scenarios.

### Boundaries

* The evaluator remains framework-neutral and does not depend on ASP.NET Core, Entity Framework Core, robotics packages, database providers, or AI model hosting.
* The in-memory ledger is non-durable and intended only for tests, samples, and local validation hosts.

## [0.1.0-alpha.1] - 2026-06-04

### Added

* Introduced the initial `AsiBackbone.Core` alpha package boundary.
* Added framework-neutral domain primitives for governance-oriented decision flow.
* Added actor context primitives for describing who or what is requesting an operation.
* Added entity identity and optimistic-concurrency abstractions.
* Added operation result primitives for package execution outcomes.
* Added reason code primitives for explainable result and decision handling.
* Added constraint evaluation primitives for allow, deny, warning, and not-applicable outcomes.
* Added governance decision primitives for allowed, warning, denied, deferred, acknowledgment-required, and escalation-recommended outcomes.
* Added audit residue primitives for capturing decision traces, reason codes, policy version/hash, correlation data, timestamps, actor data, and metadata.
* Added persistent audit ledger record shape and framework-neutral storage contract.
* Added liability/responsibility handshake primitives for acknowledgment before consequential execution.
* Added capability-token abstractions for scoped, time-bound, traceable permission grants.
* Added assembly marker support for discovery-friendly package references.
* Added XML documentation coverage for public Core types and members.
* Added unit tests for introduced Core primitives.

### Documentation

* Added README documentation describing AsiBackbone as a governance spine rather than an intelligence engine.
* Added Core domain language documentation for the initial alpha boundary.
* Added package boundary documentation clarifying what belongs in Core versus future integration packages.
* Added EF Core integration boundary documentation for future host-owned persistence support.
* Added alpha readiness review documentation.

### Boundaries

* Core does not implement artificial superintelligence.
* Core does not host, train, or run AI models.
* Core does not prove the ASI Backbone concept or the Eden Hypothesis.
* Core does not depend on ASP.NET Core, Entity Framework Core, NetCoreApplicationTemplate, robotics packages, or AI model dependencies.
* Core does not provide middleware, endpoint mapping, database storage, signing implementation, robotics control, or provider-specific persistence behavior.

### Notes

* This alpha release establishes the foundational language and primitives for the AsiBackbone package family.
* Future packages may provide ASP.NET Core integration, EF Core persistence integration, in-memory storage, signing support, samples, and later gateway or robotics examples.
