# Governance Metadata Sanitization and Classification

This article documents the provider-neutral governance metadata sanitation pipeline introduced for issue #525.

The pipeline provides an enforcement seam before host-owned metadata reaches audit residue, audit ledger storage, governance outbox records, OpenTelemetry, or another emitter. It is not a complete DLP product, privacy certification, encryption service, or provider-specific classification engine.

## Budget validation is not DLP

`GovernanceMetadataBudgetValidator` remains a bounded-shape validator. It normalizes metadata and checks entry count, key length, value length, estimated serialized size, and reserved or discouraged key fragments.

A value can satisfy every budget limit and still contain personal data, credentials, protected business information, raw prompts, or another unsafe value under an ordinary-looking key. Passing budget validation must therefore never be interpreted as proof that metadata is sanitized, classified, compliant, or safe to export.

The sanitation pipeline composes classification and budget validation in this order:

```text
caller-owned metadata
    -> normalize into a new collection
    -> host-owned classifiers
    -> allow / warn / redact / drop / deny
    -> post-sanitation budget validation
    -> forward only when the result can proceed
```

Budget validation runs after redaction and dropping so a classifier can replace an oversized value or remove a prohibited entry before the final shape check.

## Core abstractions

The Core package exposes:

- `IGovernanceMetadataClassifier` for host-owned per-entry classification;
- `IGovernanceMetadataSanitizer` for the complete metadata collection;
- `GovernanceMetadataClassificationContext` and `GovernanceMetadataClassificationResult`;
- `GovernanceMetadataSanitizationAction` with `Allow`, `Warn`, `Redact`, `Drop`, and `Deny`;
- `GovernanceMetadataSanitizationResult`, including sanitized metadata, stable reasons, and the post-sanitation budget result;
- `DefaultGovernanceMetadataSanitizer`, which applies classifiers in registration order and then validates the configured metadata budget.

Classifiers remain provider-neutral. A host may implement them with an allow-list, regular expressions, an internal classification service, a cloud DLP API, a policy engine, or another reviewed mechanism without adding provider dependencies to `AsiBackbone.Core`.

## Action semantics

| Action | Downstream behavior |
| --- | --- |
| `Allow` | Preserve the normalized key and value. |
| `Warn` | Preserve the value and add an audit-worthy reason. |
| `Redact` | Replace the value with the classifier-provided safe value or `[REDACTED]`. |
| `Drop` | Remove the entry before budget validation and forwarding. |
| `Deny` | Fail closed and expose an empty forwardable metadata collection. |

When multiple classifiers inspect the same entry, the strictest action wins. A denial anywhere in the collection denies the complete collection. If post-sanitation budget validation fails, the result is denied with reason code `metadata.budget_violation`.

Classifier exceptions and cancellation stop the pipeline. Callers must not catch a classifier failure and continue with the original metadata. Treat an incomplete classification pass as a failed governance boundary.

## Regulated-mode example

```csharp
using AsiBackbone.Core.Metadata;

IGovernanceMetadataClassifier[] classifiers =
[
    new HostAllowListMetadataClassifier(),
    new HostSensitiveValueClassifier()
];

var sanitizer = new DefaultGovernanceMetadataSanitizer(
    classifiers,
    GovernanceMetadataBudget.Recommended);

GovernanceMetadataSanitizationResult result = await sanitizer.SanitizeAsync(
    candidateMetadata,
    cancellationToken);

// Fail closed before audit persistence, outbox enqueue, or telemetry export.
result.ThrowIfDenied(nameof(candidateMetadata));

IReadOnlyDictionary<string, string> safeMetadata = result.SanitizedMetadata;
```

A classifier can return a redaction result without mutating the caller-owned dictionary:

```csharp
return GovernanceMetadataClassificationResult.Redact(
    "metadata.personal_data.redacted",
    "A classified metadata value was replaced before persistence.");
```

Use curated reason codes and messages. Do not place the detected secret, personal value, raw prompt, request body, or provider response inside the reason.

## Integration boundary

Apply sanitation before the first durable or external boundary. Typical locations include:

1. metadata construction for audit residue or an audit ledger record;
2. governance outbox envelope creation;
3. OpenTelemetry or other governance emission;
4. ASP.NET Core endpoint-governance metadata mapping;
5. regulated templates or host composition roots.

Sanitizing only during outbox drain is too late when the unsafe metadata has already been persisted locally. Sanitizing only at the endpoint is also insufficient when background jobs or non-HTTP callers create governance artifacts. Hosts should centralize the sanitizer and require every artifact-producing path to use it before persistence or export.

The Core package does not automatically intercept every metadata dictionary because the host owns classification policy, classifier dependencies, failure posture, and the point at which metadata becomes durable. The first-class seam makes that policy enforceable without pretending that a framework-neutral package can infer deployment-specific sensitivity.

## Ownership and non-claims

The host remains responsible for:

- classifier implementation and validation;
- allow-lists, redaction rules, hashing, tokenization, or encryption policy;
- provider credentials and network behavior when an external DLP service is used;
- timeout, retry, circuit-breaker, and fail-closed handling;
- retention, access control, legal hold, residency, and deletion requirements;
- ensuring every audit, outbox, telemetry, and provider path uses the sanitation result rather than the original dictionary;
- reviewing stable reason codes and messages for data leakage;
- proving that the deployed configuration meets organizational and legal requirements.

The pipeline does not certify compliance, guarantee detection of sensitive data, encrypt metadata, make storage immutable, or make telemetry safe by default.

## Recommended validation checklist

- [ ] Every classifier returns a deterministic provider-neutral action.
- [ ] Denied results never forward the original metadata.
- [ ] Redaction and dropping occur before durable storage.
- [ ] Caller-owned dictionaries remain unchanged.
- [ ] Reserved key fragments are removed, redacted under an approved replacement key strategy, or denied by the final budget check.
- [ ] Budget validation runs after sanitation.
- [ ] Classifier exceptions do not fall back to unsanitized metadata.
- [ ] Reason codes and messages contain no raw protected values.
- [ ] Audit, ledger, outbox, OpenTelemetry, endpoint, and background-worker paths use the same reviewed policy.

## Related documentation

- [Safe Audit and Telemetry Data Guidance](safe-audit-telemetry-data.md)
- [DLP and Classification Scanner Integration](dlp-classification-scanner-integration.md)
- [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [OpenTelemetry Governance Emission Provider](opentelemetry-governance-emission-provider.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
