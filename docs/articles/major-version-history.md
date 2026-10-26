# Major Version History and Upgrade Cost

AsiBackbone published seven major lines between June and September 2026. This page explains why each major boundary
was taken, which contracts it broke, and how much work an adopter should expect, so the pace can be judged on its
reasons rather than its count. It summarizes; the linked release notes and migration guides remain authoritative for
every individual change.

Every major version listed here intentionally broke compatibility with the line before it. Semantic Versioning
compatibility applies only within a major line: a consumer of any `7.x` release can move to a later `7.x` release
without the work described below, but no transition on this page is a drop-in upgrade.

## Current status

The stable package family is `7.x`, and the current release is `7.5.2`.

| Line | Support status |
| --- | --- |
| `7.x` | Supported stable line. |
| `6.x` | Previous stable line: High and Critical vulnerability fixes through 2027-03-26. |
| `5.x`, `4.x`, `3.x`, `2.x`, `1.x` | Historical lines: handled only for migration-sensitive reports that also affect `7.x`. |

[SECURITY.md](https://github.com/AsiBackbone/AsiBackbone/blob/main/SECURITY.md#supported-versions) is authoritative for
support. The [API Compatibility and SemVer](api-compatibility-and-semver.md#deprecation-and-major-release-policy) policy
now limits the pace of future breaks: at most one major release in any rolling six-month period, so none before
2027-03-26; a public announcement at least 30 days before tagging; and removal only after a published deprecation
window. Only a High or Critical vulnerability that cannot be fixed compatibly may break sooner.

## Transitions at a glance

**Security or correctness** marks a break that changed what the library allows, denies, or records, usually by making
a permissive default fail closed. **Cleanup** marks package identity, naming, or removal of deprecated members, which
changes how code is written but not what it permits.

| Transition | Released | Why a major version | Break type | Authoritative guidance |
| --- | --- | --- | --- | --- |
| `1.x` → `2.0` | 2026-06-24 | Package IDs and namespaces moved from `CDCavell.AsiBackbone.*` to `AsiBackbone.*`. | Cleanup | [2.0.0 Release Notes](release-notes-200.md#migration) |
| `2.x` → `3.0` | 2026-07-13 | Policy evaluation defaults changed to fail closed, and local-development signing configuration was tightened. | Security or correctness | [3.0.0 Release Notes](release-notes-300.md#compatibility) |
| `3.x` → `4.0` | 2026-09-06 | Governance outbox claim leasing became the default, so custom stores must support it, and inbound correlation IDs stopped being trusted by default. | Security or correctness | [Upgrade 3.2.3 to 4.0.0](upgrade-323-to-400.md), [4.0.0 Release Notes](release-notes-400.md) |
| `4.x` → `5.0` | 2026-09-07 | Security release for twelve review findings, one Critical and one High; verification, capability proof, and integrity checks fail closed and four enums gained an `Unspecified` zero. | Security or correctness | [Upgrade 4.0.0 to 5.0.0](upgrade-400-to-500.md), [5.0.0 Release Notes](release-notes-500.md) |
| `5.x` → `6.0` | 2026-09-19 | Signatures bind a versioned signature input, verification pin mismatches deny, 103 public types were renamed, and members deprecated in `5.x` were removed. | Security or correctness, and cleanup | [Upgrade from 5.x to 6.0](upgrade-500-to-600.md), [6.0 Public API Naming](public-api-naming-600.md), [6.0.0 Release Notes](release-notes-600.md) |
| `6.x` → `7.0` | 2026-09-26 | Acknowledgments bind to the challenged actor, capability grants bind to subject and operation, DLP enums left their permissive zero value, and the remaining compatibility names were renamed with matching EF Core columns. | Security or correctness, and cleanup | [Upgrade from 6.x to 7.0](upgrade-600-to-700.md), [7.0.0 Release Notes](release-notes-700.md) |

## Upgrade cost by transition

Each column is a concrete kind of work. **Yes** means every consumer does it; **If** means only consumers that use the
affected feature do. Every transition also advanced `AssemblyVersion` to the new major, so every application and plugin
that references AsiBackbone must be rebuilt.

| Transition | Package references | Namespaces or `using` | Other source changes | Default or behavior review | Persisted data | Signed artifacts or wire format | Custom extension code |
| --- | --- | --- | --- | --- | --- | --- | --- |
| `1.x` → `2.0` | Yes, new package IDs | Yes | No | No | No | No | No |
| `2.x` → `3.0` | Yes | No | No | Yes | No | If local-development signing fixtures are kept | No |
| `3.x` → `4.0` | Yes | No | If using the deprecated marker (warning only) | Yes | No | No | If a custom outbox store is supplied |
| `4.x` → `5.0` | Yes | No | If calling the changed verification APIs | Yes | If the four enums are stored as integers | If artifacts are hand-built or stored apart from their hash | If a managed-key provider is supplied |
| `5.x` → `6.0` | Yes | No | Yes | Yes | No | If pre-6.0 signatures must still verify (explicit legacy path) | If a managed-key client or verification service is supplied |
| `6.x` → `7.0` | Yes | Yes, four namespaces | Yes | Yes | If using EF Core (five renamed columns), storing DLP enums as integers, or storing decision receipt JSON | No, 6.x signatures verify unchanged | If an actor resolver or outbox drain is constructed directly |

## Transition notes

### `1.x` → `2.0`

- **Reason:** the project moved from the author-scoped `CDCavell.AsiBackbone.*` identity to the organization's
  `AsiBackbone.*` identity. The underlying package behavior did not change.
- **Breaking surfaces:** NuGet package IDs, public namespaces, the template identity, and assembly identity.
- **Adopter work:** replace each package reference with its `AsiBackbone.*` counterpart and update `using` directives.
  No behavior, configuration, data, or signature change is involved.
- **Classification:** cleanup.

### `2.x` → `3.0`

- **Reason:** permissive defaults in policy evaluation were treated as defects. An empty policy allowed by default, and
  a constraint that threw propagated an exception instead of producing an auditable denial.
- **Breaking surfaces:** `DenyWhenNoConstraints` and `TreatConstraintExceptionAsDenial` default to `true`;
  local-development RSA keys below 2048 bits fail configuration validation; local-development signing uses RSA-PSS by
  default.
- **Adopter work:** rebuild, then set those options to `false` explicitly where permissive behavior was intended, and
  regenerate local-development signing fixtures. Package IDs and namespaces did not change.
- **Classification:** security or correctness. No dedicated migration guide was published; the release notes'
  compatibility section is authoritative.

### `3.x` → `4.0`

- **Reason:** two hosts draining one durable outbox emitted every entry twice. Making claim leasing the default required
  custom outbox stores to support it, which changes an extension contract.
- **Breaking surfaces:** `UseClaimLeases` defaults to `true` and the drain fails rather than falling back when a custom
  store cannot claim; inbound correlation-ID headers are untrusted unless `TrustInboundCorrelationIdHeaders` is enabled.
  `RequireGovernancePolicy` was deprecated in favor of `MarkGovernancePolicy`, not removed.
- **Adopter work:** implement the claim-store contract in custom outbox stores or opt out explicitly; review claim
  worker, page-size, and dead-letter options; confirm correlation-header trust and middleware ordering.
- **Classification:** security or correctness.

### `4.x` → `5.0`

- **Reason:** a cross-repository review found twelve issues, one Critical and one High, in which verification,
  capability proof, integrity, and signing paths returned permissive results. Each fix made a path fail closed.
  It shipped one day after `4.0.0` because the findings were security defects, not planned changes.
- **Breaking surfaces:** artifact verification recomputes the canonical hash; capability proofs must cover the grant
  being evaluated; `VerificationPolicyAction`, `SignatureVerificationCategory`, `GrantUseState`, and
  `AuditIntegrityVerificationCategory` reserve zero as `Unspecified`; validation options are required; signing throws
  when no signature is produced; email claims left the default actor claims; local-development signing refuses
  Production without an explicit opt-in.
- **Adopter work:** migrate stored zero values of the four enums, pass explicit capability validation options with an
  audience, supply chain anchors for partial audit-chain verification, and remove local-development signing from
  production paths. Hosts using the shipped factories see no verification change.
- **Classification:** security or correctness.

### `5.x` → `6.0`

- **Reason:** signing metadata such as policy version and hash was not covered by the signature, so a signed artifact
  could be relabeled and still verify. Correcting that changed the signed bytes. The same release adopted
  plain-language public names and removed members whose `5.x` deprecation windows had completed.
- **Breaking surfaces:** signatures cover a versioned signature input; verification pin mismatches,
  `CanonicalizationMismatch`, and `MissingSignature` default to deny; 103 of 232 public type entries were renamed;
  the `ASIB900` evaluator constructors and `RequireGovernancePolicy` were removed.
- **Adopter work:** apply the renames from the naming inventory, update managed-key clients and verification services
  to sign and verify the supplied signature-input bytes, and decide how pre-6.0 artifacts are verified. They need the
  explicit legacy path and cannot satisfy policy pins.
- **Classification:** security or correctness for the signing and verification changes; cleanup for the renames and
  removals.

### `6.x` → `7.0`

- **Reason:** any actor naming an active challenge could answer it, a valid grant issued for another subject or
  operation could authorize execution, and an unassigned DLP setting meant `Allow`. Fixing the last one renumbered two
  public enums, which needs a new assembly identity. The release also renamed the compatibility names that `6.0`
  retained, as the `6.0` naming record planned.
- **Breaking surfaces:** acknowledgment responses bind to the challenged, authenticated actor; capability grants bind to
  the expected subject and operation; `DlpFailureBehavior` and `DlpIntentRiskLevel` gained `Unspecified` at zero; four
  namespaces and the `AuditResidue*`, `LiabilityHandshake*`, `Handshake*`, and `CapabilityToken*` names were renamed
  without aliases; five EF Core columns and one JSON property were renamed.
- **Adopter work:** resolve the same actor on both acknowledgment legs, supply subject and operation expectations, remap
  stored numeric DLP values, apply the renames, add a reviewed EF Core migration, and update consumers of
  `decisionReceiptId`. Signed payloads, canonical tags, telemetry names, table names, reason codes, and diagnostic IDs
  kept their `6.x` values.
- **Classification:** security or correctness for the binding and DLP changes; cleanup for the renames.

## Reading the pace

The seven majors span 2026-06-14 to 2026-09-26. One transition was an identity change, and each of the other five
carried at least one change that made a permissive behavior fail closed. Four of the six transitions required no
namespace change. Persisted data needed migration only at `5.0`, for the four enums when stored as integers, and at
`7.0`, for EF Core columns, numeric DLP values, and stored decision receipt JSON.

Two practices kept those breaks visible rather than silent. Each major line pins its own `AssemblyVersion`, so an
assembly compiled against one line cannot quietly load another. When an enum's zero value meant allow, valid, or
accepted, it was moved off zero in a major release, at `5.0` and `7.0`, rather than reinterpreted inside a line. The
spacing, announcement, and deprecation rules in
[API Compatibility and SemVer](api-compatibility-and-semver.md#deprecation-and-major-release-policy) were adopted on
2026-09-30, after `7.0.0`, and govern every later major release.

## Related documentation

- [Releases & Compatibility](../releases/index.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Schema Versioning](schema-versioning.md)
- [Target Framework Support](target-framework-support.md)
- [Repository CHANGELOG](https://github.com/AsiBackbone/AsiBackbone/blob/main/CHANGELOG.md)
