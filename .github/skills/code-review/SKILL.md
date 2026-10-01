---
name: code-review
description: Review AsiBackbone code, documentation, APIs, compatibility changes, and integration boundaries. Use this when reviewing pull requests, implementing issues, evaluating public API changes, checking release or migration work, or assessing whether a change preserves AsiBackbone repository contracts.
---

# AsiBackbone Code Review

Use this skill to review or modify AsiBackbone without losing the repository's package, compatibility, governance, security, release, and cross-repository boundaries.

## Repository role

Treat AsiBackbone as **Accountable Systems Infrastructure for governed .NET decision flow**.

It is practical governance infrastructure: a governance spine for consequential software decision flow, not an intelligence engine.

AsiBackbone owns:

- package installation and configuration;
- public APIs, types, and documented behavior;
- runtime semantics and integration boundaries;
- security and cryptographic implementation posture;
- compatibility and migration guidance;
- provider operations;
- releases, consumer verification, and maintainer evidence.

Learning owns reusable architecture education and broader governed-execution teaching. NetCoreApplicationTemplate (NCAT) owns its template, generated output, runtime defaults, and generated-contract behavior.

Do not create a dependency merely because concepts or terminology align across repositories.

## Review public API and compatibility first

Before reviewing an implementation change, determine whether it affects any stable contract:

- public API surface;
- namespaces, types, members, or enum values;
- documented extension points or service registration;
- serialized or persisted contracts;
- canonical signing bytes or artifact tags;
- telemetry names or stable reason codes;
- EF Core schema or persistence identity;
- package IDs, assembly identity, or target frameworks;
- analyzer diagnostics;
- documented defaults or compatibility guarantees.

When the public surface changes:

- inspect the committed baselines under `eng/api-baseline/`;
- apply the repository's semantic-versioning policy before accepting a baseline update;
- use `./scripts/Validate-PublicApiBaseline.ps1 -Update` only for an approved API change;
- require at least a minor version for additive stable API;
- require a major release plus migration guidance for breaking stable API changes;
- preserve intentional compatibility, wire, telemetry, or protocol names unless the applicable migration explicitly changes them.

Do not classify every legacy-looking name as stale. Check whether it is an intentional protocol, persistence, telemetry, historical, or compatibility contract.

## Apply the deprecation policy exactly

Before adding, changing, or removing `[Obsolete]`, read `docs/articles/api-compatibility-and-semver.md`.

A stable deprecation is complete only when the repository policy is satisfied. In particular:

- use `[Obsolete]` as a compiler warning, not an error;
- use the established stable `ASIB9xx` diagnostic family;
- name a concrete replacement in the obsolete message;
- ship the replacement in the same release as the deprecation;
- use an additive replacement plus obsolete forwarding member for renames;
- document the replacement, migration path, and earliest removal major in the changelog, release notes, and the corresponding `ASIB9xx` migration article;
- account for consumers that promote warnings through `TreatWarningsAsErrors`;
- update public API baselines and focused metadata tests when applicable;
- do not remove a deprecated stable member before the documented major-release and minimum-window rules permit it.

Do not use a baseline update to bypass the compatibility policy.

## Preserve governance and execution boundaries

Review changes against these invariants:

- a governance decision does not itself perform the protected side effect;
- acknowledgment is distinct from authorization and execution;
- capability grants remain scoped, explicit, and independently validated;
- host ownership of authentication, authorization, persistence, transaction management, routing, execution, production key custody, and operational monitoring remains explicit;
- durable decision, lifecycle, and outbox evidence is not replaced by downstream observability or provider emission;
- `AsiBackbone.Core` remains framework-neutral;
- provider-specific infrastructure stays outside Core unless the package architecture explicitly says otherwise.

Design-only, strategy-only, sample-only, host-owned, or future-provider work must not be described as a released stable package unless a stable release explicitly ships it.

## Use careful security and evidence language

Reject wording or implementation assumptions that overclaim the system.

Keep these distinctions explicit:

- signing is not automatically tamper-proof or immutable storage;
- hashing is not confidentiality;
- development signing is not production key custody;
- telemetry is not an authoritative audit ledger;
- framework guidance is not certification, regulatory approval, or a compliance guarantee;
- sample/reference integrations are not required runtime dependencies;
- durable outbox delivery is not exactly-once execution unless the implementation explicitly proves that property.

Prefer fail-closed behavior for security-sensitive defaults unless repository policy explicitly documents another posture.

Do not include secrets, credentials, tokens, private keys, passwords, or raw sensitive records in examples, tests, issues, documentation, or diagnostics.

## Distinguish current guidance from historical evidence

Classify documentation before editing it:

- current evergreen product/API/runtime guidance;
- historical release or migration records;
- immutable tagged release evidence;
- design-only or strategy-only future work.

For historical cross-repository references, use the matching release tag or exact reviewed commit instead of mutable `main` when the historical baseline matters.

Do not modify published or attested release artifacts merely to modernize their wording unless the release process explicitly permits it.

If surrounding evergreen documentation becomes stale, update that documentation while preserving the historical artifact.

For current provider or integration pages, verify that release-state wording matches the current stable package family and does not present a historical deferral as the current release boundary.

## Respect cross-repository documentation ownership

Before recommending a documentation change, identify the authoritative repository:

- Learning = reusable architecture education, terminology lineage, tutorials, comparisons, tradeoffs, and labs;
- AsiBackbone = concrete package, API, runtime, configuration, integration, security, compatibility, migration, release, and maintainer truth;
- NCAT = template, generated output, runtime defaults, configuration, ADR, and generated-contract truth.

When another repository owns the subject, link to it instead of duplicating or redefining its contract.

Preserve established AsiBackbone documentation URLs when practical. If educational authority moves to Learning, keep a concise product-boundary or transition page when repository policy requires URL continuity.

## Validate against the affected surface

Use the repository-prescribed Release validation from `CONTRIBUTING.md`.

For substantive source changes, start with:

```bash
dotnet restore AsiBackbone.slnx --locked-mode -p:Configuration=Release
dotnet build AsiBackbone.slnx --configuration Release --no-restore
dotnet test --solution AsiBackbone.slnx --configuration Release --no-build --no-restore
```

A Debug build is an inner-loop convenience and is not evidence that the complete source/test surface was validated.

When package-consumer behavior changes, run the applicable smoke tests when possible:

```bash
bash ./eng/smoke-tests/external-consumer-smoke.sh
bash ./eng/smoke-tests/stable-package-integration-smoke.sh
```

When public APIs change, run the public API baseline validation.

When documentation or public behavior changes:

- restore repository tools as needed;
- build DocFX;
- validate cross-repository documentation links with `./scripts/Validate-DocumentationLinks.ps1`;
- run any targeted metadata, compatibility, release, or documentation checks required by the changed surface.

When packaging, persistence, signing, analyzer, or release behavior changes, run the corresponding targeted repository validation in addition to the baseline build/test path.

Do not claim a command, test, smoke check, documentation build, or CI gate passed unless it was actually run or there is authoritative CI evidence.

## Prioritize meaningful review findings

Prioritize findings that can affect:

- correctness;
- public compatibility or SemVer;
- security posture;
- persistence or serialized evidence;
- signing or verification semantics;
- host ownership;
- package boundaries;
- release integrity;
- historical/current documentation accuracy;
- cross-repository ownership.

Avoid generic style-only findings unless they violate an explicit repository rule or materially affect maintainability.

When reporting a finding, identify the concrete consumer, host, compatibility, security, or release consequence and point to the repository contract that makes it important.
