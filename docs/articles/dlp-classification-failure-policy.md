# DLP and Classification Failure Policy

This article documents the provider-neutral DLP, classification, and governance-screening failure behavior model for the `1.1.0 - Observability, Outbox, and Governance Emission Providers` milestone.

AsiBackbone remains a governance spine for consequential software decision flow. It is not a DLP product, classification engine, Microsoft Purview adapter, cloud governance platform, AI model host, compliance guarantee, or completed ASI implementation.

## Purpose

Optional DLP, classification, and governance-screening services may be unavailable, slow, misconfigured, inconclusive, or intentionally block a payload. AsiBackbone should not hard-code one response for every case.

The Core model defines a neutral policy resolver so host applications can decide whether a screening failure should:

* allow the operation;
* allow with warning;
* deny the operation;
* defer the operation;
* require acknowledgment;
* escalate for review.

This keeps the behavior policy-driven and provider-neutral. Purview, custom classifiers, internal compliance APIs, SIEM enrichment, or future DLP services can all normalize into the same Core vocabulary without making Core depend on a provider SDK.

> [!NOTE]
> AsiBackbone supplies the governance policy response after the host reports a scanner outcome. It does not run the scanner. For a host-owned scanner integration example, see [DLP and Classification Scanner Integration](dlp-classification-scanner-integration.md).

## Core-neutral model

The model lives in `AsiBackbone.Core` under provider-neutral classification language.

Core owns:

| Type | Role |
| --- | --- |
| `DlpClassificationFailureKind` | Stable failure kinds such as service unavailable, timeout, indeterminate result, blocked result, and classified result. |
| `DlpIntentRiskLevel` | Host-assigned risk level: low, medium, or high. |
| `DlpFailureBehavior` | Configured behavior: allow, warn and allow, deny, defer, require acknowledgment, or escalate. |
| `DlpFailurePolicyContext` | Provider-neutral failure context carrying risk, category, environment, timeout, correlation, policy, and safe metadata. |
| `DlpFailurePolicyOptions` | Default risk behavior and risk/failure-specific overrides. |
| `IAsiBackboneDlpFailurePolicyResolver` | Resolver abstraction for converting failure context into a policy resolution. |
| `DefaultAsiBackboneDlpFailurePolicyResolver` | Default resolver using `DlpFailurePolicyOptions`. |
| `DlpFailurePolicyResolution` | Resolved behavior, reason code, and `GovernanceDecision`. |
| `DlpFailureReasonCodes` | Stable reason-code constants. |

Core does not own:

* scanner execution;
* scanner selection;
* Microsoft Purview SDK dependencies;
* Azure SDK dependencies;
* OpenTelemetry exporters;
* provider-specific classifier APIs;
* provider-specific DLP payloads;
* raw classification labels or protected content storage;
* legal/compliance guarantees.

## Failure kinds

| Failure kind | Reason code | Meaning |
| --- | --- | --- |
| `ServiceUnavailable` | `dlp.service_unavailable` | The screening service, API, or provider endpoint was unavailable. |
| `Timeout` | `dlp.timeout` | Screening did not finish inside the host-defined timeout. |
| `IndeterminateResult` | `dlp.indeterminate_result` | Screening returned a result that could not safely be treated as allowed, blocked, or classified. |
| `BlockedResult` | `dlp.blocked_result` | Screening returned a block result. |
| `ClassifiedResult` | `dlp.classified_result` | Screening returned a classification that requires configured handling before emission or execution. |

The reason codes intentionally distinguish service/API failure from classification-result failure. This matters during audit review: an outage is not the same thing as a provider saying the payload is blocked or sensitive.

## Default risk posture

The default resolver uses a conservative baseline:

| Risk level | Default behavior | Governance decision |
| --- | --- | --- |
| `Low` | `WarnAndAllow` | `GovernanceDecision.Warning(...)` |
| `Medium` | `RequireAcknowledgment` | `GovernanceDecision.RequireAcknowledgment(...)` |
| `High` | `Deny` | `GovernanceDecision.Deny(...)` |

This is only a default. Hosts can override behavior globally by risk level or specifically by risk/failure pair.

Example override:

```csharp
var options = new DlpFailurePolicyOptions
{
    HighRiskBehavior = DlpFailureBehavior.Deny,
    MediumRiskBehavior = DlpFailureBehavior.RequireAcknowledgment,
    LowRiskBehavior = DlpFailureBehavior.WarnAndAllow
};

options.BehaviorOverrides[new DlpFailurePolicyKey(
    DlpIntentRiskLevel.High,
    DlpClassificationFailureKind.BlockedResult)] = DlpFailureBehavior.Escalate;

var resolver = new DefaultAsiBackboneDlpFailurePolicyResolver(options);
```

## Timeout handling

Timeouts are represented explicitly through `DlpClassificationFailureKind.Timeout` and may carry a timeout duration.

```csharp
DlpFailurePolicyContext context = DlpFailurePolicyContext.TimeoutFailure(
    DlpIntentRiskLevel.Medium,
    timeout: TimeSpan.FromSeconds(2),
    correlationId: correlationId,
    policyVersion: "policy-v1",
    policyHash: "policy-hash");

DlpFailurePolicyResolution resolution = await resolver.ResolveAsync(context, cancellationToken);
```

The resulting reason code is `dlp.timeout`. The reason metadata includes the failure kind, risk level, resolved behavior, and timeout milliseconds when supplied.

## Scanner integration seam

Host applications should invoke their chosen scanner before provider emission or governed execution. The scanner may be a regex/local scanner, Azure AI Content Safety, Microsoft Purview/DLP-adjacent service, proprietary classifier, or another host-defined service.

```text
Host scanner
  -> provider-specific result or exception
  -> DlpFailurePolicyContext
  -> IAsiBackboneDlpFailurePolicyResolver
  -> DlpFailurePolicyResolution
  -> GovernanceDecision
```

The scanner should return or throw provider-specific outcomes. The host adapter then normalizes those outcomes into `DlpFailurePolicyContext`. See [DLP and Classification Scanner Integration](dlp-classification-scanner-integration.md) for a minimal fake scanner sample, an external-provider pseudo-sample, and fail-open/fail-closed guidance.

## Provider boundary

Provider packages or host adapters should normalize provider-specific failures before they call the Core resolver.

```text
Purview / custom classifier / internal DLP / future provider
  -> provider-specific result or exception
  -> DlpFailurePolicyContext
  -> IAsiBackboneDlpFailurePolicyResolver
  -> DlpFailurePolicyResolution
  -> GovernanceDecision
```

Provider-specific HTTP status codes, SDK exception types, classifier labels, or payload details should not leak into Core. Hosts may preserve safe provider diagnostics in metadata after minimization and classification.

## Relationship to outbox and governance emission

The DLP failure policy model supports the durable audit and outbox direction:

```text
Audit residue or emission envelope
  -> host DLP/classification check
  -> failure or indeterminate result
  -> DlpFailurePolicyResolution
  -> allow, warn, deny, defer, acknowledge, or escalate
  -> outbox state and audit/lifecycle record
```

A provider outage should not erase the local audit record. A DLP block should not be treated as the same kind of event as an unavailable provider. The resolver gives hosts a stable reason-code and decision shape for both cases.

## Privacy and minimization

Do not place raw protected content in DLP failure policy context metadata.

Safe metadata examples:

* coarse policy scope;
* opaque tenant or organization hash;
* provider-neutral classifier family;
* timeout milliseconds;
* retry category;
* minimized provider error code.

Unsafe metadata examples:

* raw prompts;
* raw documents;
* raw personal data;
* secrets or credentials;
* raw classifier payloads;
* provider connection strings;
* full DLP findings when host policy has not approved emission.

## Tests

The Core tests cover:

* low-risk service unavailable behavior producing warn-and-allow;
* medium-risk timeout behavior requiring acknowledgment;
* high-risk indeterminate behavior failing closed;
* risk/failure-specific behavior overrides;
* distinct reason codes for service unavailable, timeout, indeterminate, blocked, and classified results;
* cancellation handling.

These tests do not require Purview, Azure SDKs, OpenTelemetry, external DLP services, or live provider resources.

## Related documentation

- [DLP and Classification Scanner Integration](dlp-classification-scanner-integration.md)
- [Observability and Governance Emission Architecture](observability-and-governance-emission-architecture.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
