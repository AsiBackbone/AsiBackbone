# Privacy, Metadata, and Signing Boundaries

This article documents the current stable `4.x` boundary for metadata privacy, identifier handling, signing-ready fields, released signing providers, host responsibilities, and future provider work.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone provides governance-oriented software building blocks. It does not provide legal, compliance, privacy, security, or cryptographic guarantees by itself.

> [!IMPORTANT]
> The stable `4.x` package family includes metadata budget and sanitation primitives, signing-ready Core metadata, released local-development signing, and a managed-key adapter boundary. These surfaces do not provide production key custody, tamper-evidence, immutable storage, automatic privacy classification, legal non-repudiation, or compliance certification by themselves.

## Current stable boundary summary

The stable package family focuses on explicit governance records, host-owned integration seams, metadata minimization and sanitation boundaries, released local-development signing, managed-key adapter boundaries, provider-neutral emission, and optional provider projection.

| Area | Current stable behavior | Still not provided by default |
| --- | --- | --- |
| Metadata | Host-provided dictionaries and values can flow into contexts, decisions, audit residue, and ledger records. Optional budget and sanitation helpers can normalize, classify through host-owned classifiers, redact, drop, warn, deny, and validate shape limits and reserved key fragments. | Automatic domain-specific classification, encryption, tokenization, external DLP scanning, or proof that sanitized metadata is non-sensitive. |
| Identifiers | Correlation IDs, trace IDs, event IDs, record IDs, actor IDs, policy versions, and policy hashes are available for linking records. | Automatic pseudonymization, identity-proofing, cross-system identity governance, or secret handling. |
| Signing and verification boundaries | Core records can carry signing-ready metadata and canonical hashing inputs. Released local-development signing and managed-key adapter packages support test/sample signing and host-owned managed-key integration. | Production key custody, automatic key rotation, concrete Azure Key Vault/HSM/KMS implementation, immutable storage, tamper-evidence, legal non-repudiation, or compliance guarantees. |
| Persistence | EF Core integration supports host-owned persistence through the host application database. | Package-owned database lifecycle, retention policy, encryption-at-rest enforcement, backup policy, or immutable storage. |
| Audit | Audit residue, ledger records, and provider-neutral integrity seams make decision flow easier to inspect. | Regulatory audit certification, legal evidence guarantees, automated compliance approval, or production tamper-evidence by default. |

## Metadata privacy boundary

AsiBackbone treats metadata as host-owned data. The package does not know whether a metadata value is public, internal, confidential, regulated, personal, or secret.

The host application should classify and minimize metadata before passing it into AsiBackbone APIs.

Recommended metadata shape:

```csharp
metadata: new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["region"] = "US-LA",
    ["risk"] = "routine",
    ["workflow"] = "document-approval"
}
```

Avoid placing secrets, credentials, raw personal data, protected records, or other sensitive values directly in metadata.

Prefer stable but non-sensitive codes:

| Prefer | Avoid |
| --- | --- |
| `region = US-LA` | full address details when not needed |
| `risk = consequential` | free-form incident details containing personal data |
| `workflow = document-approval` | full document body |
| `resourceType = purchase-order` | complete financial details |
| opaque host-owned subject ID | direct personal identifiers when not required |

## Metadata budget and reserved-key guidance

Metadata is useful for governance review, but it should not become an unbounded diagnostic dump. Hosts should define a budget before metadata reaches durable audit storage, telemetry export, outbox persistence, or signing metadata.

The package exposes optional helper APIs under `AsiBackbone.Core.Metadata`:

- `GovernanceMetadataBudget` defines host-owned limits.
- `GovernanceMetadataBudget.Recommended` uses 32 entries, 64-character keys, 512-character values, and an 8,192-byte estimated serialized-size limit.
- `GovernanceMetadataBudgetValidator` trims metadata, drops blank keys, preserves ordinal key comparison, reports budget violations, and flags reserved or discouraged key fragments.
- `IGovernanceMetadataClassifier` lets hosts add provider-neutral classification logic.
- `DefaultGovernanceMetadataSanitizer` coordinates classification, redaction, dropping, denial, and post-sanitation budget validation.

Recommended reserved or discouraged key fragments include secrets, credentials, passwords, API keys, access tokens, refresh tokens, bearer/authorization headers, private keys, connection strings, SSNs, and social-security identifiers. Store opaque references, classification codes, hashes, or provider record IDs instead.

A passing budget or sanitation result is not a privacy guarantee. These helpers enforce configured shape and policy behavior; they do not independently discover every sensitive value, encrypt data, tokenize content, or certify compliance.

```csharp
using AsiBackbone.Core.Metadata;

var budgetResult = GovernanceMetadataBudgetValidator.Validate(metadata);
if (!budgetResult.IsValid)
{
    // Host policy decides whether to deny, redact, quarantine, or trim.
    throw new InvalidOperationException(string.Join("; ", budgetResult.Violations));
}

metadata = budgetResult.NormalizedMetadata;
```

See [Governance Metadata Sanitization](governance-metadata-sanitization.md) for the classifier and sanitation pipeline.

## Canonical signing metadata boundary

Canonical signing payloads intentionally exclude metadata unless the host supplies an explicit `CanonicalPayloadOptions.MetadataKeyAllowList`.

This behavior is important for two reasons:

- host-specific diagnostics should not silently change signing payloads;
- metadata containing sensitive, unstable, or oversized values should not become part of a canonical payload by accident.

When metadata must be included in a signed payload, prefer a small allow-list of stable governance keys such as policy version, policy hash, classification state, region code, or resource type. Avoid allow-listing raw prompts, request bodies, HTTP headers, exception messages, secrets, tokens, credentials, connection strings, or raw PII.

## Identifier handling boundary

AsiBackbone exposes identifiers so a host can connect decision flow to logs, audit records, and operational records.

Common identifiers include:

- `EventId`
- `RecordId`
- `CorrelationId`
- `TraceId`
- `ActorId`
- `PolicyVersion`
- `PolicyHash`
- `SchemaVersion`

These identifiers are not automatically private. They may become sensitive when they can be linked to a person, tenant, agency, customer, protected workflow, or confidential resource.

Host guidance:

- Use opaque identifiers where possible.
- Do not use raw secrets as identifiers.
- Do not place credentials, security tokens, or confidential key material in any identifier field.
- Treat actor identifiers as potentially personal data when they can be mapped to a person.
- Treat correlation and trace identifiers as operational data that may appear in logs.
- Keep tenant, jurisdiction, and resource identifiers no more specific than the decision requires.

## Signing-ready, local-development signing, and managed-key boundaries

The stable package family separates signing-related behavior into explicit boundaries:

- Core records may carry signing-ready metadata and canonical hashing inputs.
- `AsiBackbone.Signing.LocalDevelopment` provides local/test/sample signing and verification proof paths.
- `AsiBackbone.Signing.ManagedKey` provides a provider-neutral adapter boundary for host-owned managed-key clients.

These surfaces do not make audit records tamper-evident, immutable, legally non-repudiable, or compliance-certified by default.

## Host responsibilities

The consuming application remains responsible for privacy, security, compliance, and operational policy.

Before using AsiBackbone in a production or regulated environment, the host should decide:

- which metadata fields are allowed;
- which metadata count, key length, value length, and estimated serialized-size limits apply;
- which metadata key fragments are reserved, discouraged, or blocked;
- which metadata fields must be redacted, hashed, tokenized, or omitted;
- whether actor IDs are personal data;
- how long audit records are retained;
- who can read audit records;
- whether durable records require encryption at rest;
- whether logs include correlation IDs or decision metadata;
- whether metadata needs privacy review before persistence;
- whether records must be copied to immutable storage;
- whether signing, verification, or timestamp authority is required;
- how keys, certificates, secrets, and rotation are managed;
- how data deletion, discovery, and retention obligations are handled.

AsiBackbone should sit inside the host's existing security, privacy, and compliance program. It does not replace that program.

## EF Core and persistence boundary

`AsiBackbone.EntityFrameworkCore` provides model configuration and storage helpers. The host application owns the `DbContext`, provider, connection string, database permissions, migrations, schema deployment, backups, retention, and encryption choices.

For production persistence, the host should review:

- database access controls;
- encryption at rest;
- encryption in transit;
- backup and restore behavior;
- tenant isolation;
- retention and purge processes;
- administrative access;
- operational logging;
- migration review;
- incident response requirements.

See [EF Core Host Ownership and Migrations](ef-core-host-ownership-and-migrations.md) for host-owned persistence expectations.

## ASP.NET Core boundary

`AsiBackbone.AspNetCore` provides thin host adapters for request correlation, actor context, HTTP result mapping, acknowledgment challenge helpers, endpoint governance, and hosted outbox drain integration.

The ASP.NET Core package does not own:

- authentication;
- authorization;
- claims transformation;
- consent notices;
- privacy notices;
- session management;
- endpoint exposure;
- route protection;
- UI rendering;
- external execution.

The host must decide which requests are allowed to reach AsiBackbone and which actors may view or act on decisions and audit records.

## Released, host-owned, and future provider work

The stable `4.x` package family includes released provider or provider-adjacent surfaces for OpenTelemetry governance emission, local-development signing, and managed-key signing adapter boundaries.

Other provider areas remain host-owned, strategy-only, design-only, sample-only, preview, or future-provider work unless a later stable release explicitly ships them.

Provider documentation should state whether a provider is:

- stable;
- preview;
- experimental;
- sample-only;
- host-owned integration guidance.

## Release wording checklist

Use this checklist when preparing stable release notes or documentation:

- State that AsiBackbone provides Accountable Systems Infrastructure, not artificial superintelligence.
- State that metadata is host-owned.
- State that hosts must classify, minimize, redact, or omit sensitive metadata before passing it into package APIs.
- State that metadata budgets and sanitation helpers are policy guardrails, not automatic DLP, privacy certification, or compliance certification.
- State that canonical signing payload metadata remains allow-list only.
- State that signing-ready fields, local-development signing, and managed-key adapter boundaries are available where released, but production key custody, tamper-evidence, immutability, legal non-repudiation, and compliance certification remain host-owned.
- Avoid claims of tamper-evidence unless signing, verification, durable storage, and operational controls are actually implemented and documented.
- Avoid claims of regulatory compliance or legal non-repudiation.
- Keep provider, cloud, signing, outbox, and gateway behavior separate from the stable package boundary unless explicitly released as stable.

## Related documentation

- [3.0.0 Release Notes](release-notes-300.md)
- [Production Wording and Stable Signing Boundaries](production-wording-and-alpha-limitations.md)
- [Governance Metadata Sanitization](governance-metadata-sanitization.md)
- [Signing-Ready Receipts and Key Handling](signing-ready-receipts-and-key-handling.md)
- [Signing Provider Package Boundary](signing-provider-package-boundary.md)
- [Managed-Key Signing Provider](managed-key-signing-provider.md)
- [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md)
- [API Compatibility and SemVer](api-compatibility-and-semver.md)
- [Schema Versioning](schema-versioning.md)
- [EF Core Host Ownership and Migrations](ef-core-host-ownership-and-migrations.md)
- [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md)
