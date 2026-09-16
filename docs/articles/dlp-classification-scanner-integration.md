# DLP and Classification Scanner Integration

This article shows where a host-owned DLP, classification, or governance-screening scanner fits into an AsiBackbone flow.

Issue: #286.

AsiBackbone supplies provider-neutral governance policy primitives after a host reports a DLP/classification outcome. It is not a DLP scanner, content-safety service, Microsoft Purview adapter, regex engine, classifier, compliance product, or external screening provider by itself.

> [!IMPORTANT]
> Scanner execution is host-owned and provider-specific. The host chooses and invokes its scanner before provider emission or before a governed execution path continues. AsiBackbone receives the normalized failure or policy-handling context through `DlpFailurePolicyContext`, resolves it with `IDlpFailurePolicyResolver`, and returns a `DlpFailurePolicyResolution` containing a `GovernanceDecision`.

## Where the scanner fits

The scanner sits between the host's proposed action or emission payload and the policy decision to proceed.

```text
Host request / payload / governance emission envelope
  -> host-owned scanner
       regex scanner
       Azure AI Content Safety
       Microsoft Purview / DLP-adjacent service
       proprietary classifier
       internal compliance API
  -> scanner result or scanner failure
  -> normalize to DlpFailurePolicyContext when policy handling is needed
  -> IDlpFailurePolicyResolver
  -> DlpFailurePolicyResolution
  -> GovernanceDecision
  -> host applies allow / warn / deny / defer / acknowledgment / escalation
```

AsiBackbone does not inspect raw content for you. It gives the host a stable way to decide what to do when screening is unavailable, times out, returns an indeterminate result, blocks content, or classifies content that requires policy handling.

## Scanner outcomes and normalized failure kinds

| Host-owned scanner outcome | Normalize as | Typical meaning |
| --- | --- | --- |
| Service unavailable | `DlpClassificationFailureKind.ServiceUnavailable` | Scanner endpoint, local service, or provider dependency could not be reached. |
| Timeout | `DlpClassificationFailureKind.Timeout` | Scanner did not return within the host-defined timeout. |
| Indeterminate result | `DlpClassificationFailureKind.IndeterminateResult` | Scanner completed but the host cannot safely treat the result as allowed, blocked, or classified. |
| Blocked result | `DlpClassificationFailureKind.BlockedResult` | Scanner explicitly blocked the payload or action. |
| Classified result requiring policy handling | `DlpClassificationFailureKind.ClassifiedResult` | Scanner detected a category such as sensitive, regulated, confidential, or high-risk and the host wants governance policy to decide what happens next. |

A clean allowed scanner result does not need to call the failure-policy resolver. The host can continue with the normal policy, outbox, emission, or execution path.

## Minimal fake scanner sample

This sample uses a simple regex-style scanner with no Azure dependency, no external service, and no package beyond `AsiBackbone.Core`.

```csharp
using AsiBackbone.Core.Classification;
using AsiBackbone.Core.Decisions;

public enum FakeScannerOutcome
{
    Allowed,
    ServiceUnavailable,
    Timeout,
    Indeterminate,
    Blocked,
    Classified
}

public sealed record FakeScannerResult(
    FakeScannerOutcome Outcome,
    string? Category = null,
    TimeSpan? Timeout = null);

public static class FakeRegexScanner
{
    public static FakeScannerResult Scan(string payload)
    {
        if (payload.Contains("scanner-down", StringComparison.OrdinalIgnoreCase))
        {
            return new FakeScannerResult(FakeScannerOutcome.ServiceUnavailable);
        }

        if (payload.Contains("simulate-timeout", StringComparison.OrdinalIgnoreCase))
        {
            return new FakeScannerResult(FakeScannerOutcome.Timeout, Timeout: TimeSpan.FromSeconds(2));
        }

        if (payload.Contains("unknown-sensitive", StringComparison.OrdinalIgnoreCase))
        {
            return new FakeScannerResult(FakeScannerOutcome.Indeterminate);
        }

        if (payload.Contains("blocked-secret", StringComparison.OrdinalIgnoreCase))
        {
            return new FakeScannerResult(FakeScannerOutcome.Blocked, Category: "secret");
        }

        if (payload.Contains("regulated-data", StringComparison.OrdinalIgnoreCase))
        {
            return new FakeScannerResult(FakeScannerOutcome.Classified, Category: "regulated");
        }

        return new FakeScannerResult(FakeScannerOutcome.Allowed);
    }
}
```

The host then maps scanner outcomes into `DlpFailurePolicyContext` only when policy handling is needed.

```csharp
public static async ValueTask<GovernanceDecision> EvaluateScannerOutcomeAsync(
    string payload,
    IDlpFailurePolicyResolver resolver,
    CancellationToken cancellationToken)
{
    FakeScannerResult result = FakeRegexScanner.Scan(payload);

    if (result.Outcome == FakeScannerOutcome.Allowed)
    {
        return GovernanceDecision.Allow(
            correlationId: "correlation-123",
            policyVersion: "policy-v1",
            policyHash: "policy-hash");
    }

    DlpFailurePolicyContext context = ToDlpContext(result);
    DlpFailurePolicyResolution resolution = await resolver.ResolveAsync(
        context,
        cancellationToken);

    return resolution.Decision;
}
```

The normalization function is intentionally host-owned. It keeps scanner-specific categories and provider details out of Core while preserving safe metadata.

```csharp
private static DlpFailurePolicyContext ToDlpContext(FakeScannerResult result)
{
    DlpClassificationFailureKind failureKind = result.Outcome switch
    {
        FakeScannerOutcome.ServiceUnavailable => DlpClassificationFailureKind.ServiceUnavailable,
        FakeScannerOutcome.Timeout => DlpClassificationFailureKind.Timeout,
        FakeScannerOutcome.Indeterminate => DlpClassificationFailureKind.IndeterminateResult,
        FakeScannerOutcome.Blocked => DlpClassificationFailureKind.BlockedResult,
        FakeScannerOutcome.Classified => DlpClassificationFailureKind.ClassifiedResult,
        _ => throw new InvalidOperationException("Allowed scanner results do not need DLP failure policy resolution.")
    };

    DlpIntentRiskLevel riskLevel = result.Outcome switch
    {
        FakeScannerOutcome.Blocked => DlpIntentRiskLevel.High,
        FakeScannerOutcome.Classified => DlpIntentRiskLevel.Medium,
        FakeScannerOutcome.Timeout => DlpIntentRiskLevel.Medium,
        FakeScannerOutcome.ServiceUnavailable => DlpIntentRiskLevel.Medium,
        _ => DlpIntentRiskLevel.High
    };

    IReadOnlyDictionary<string, string> metadata = new Dictionary<string, string>
    {
        ["scanner.family"] = "fake-regex",
        ["scanner.category"] = result.Category ?? "none"
    };

    return DlpFailurePolicyContext.Create(
        failureKind,
        riskLevel,
        intentCategory: "governance-emission",
        environment: "production",
        correlationId: "correlation-123",
        policyVersion: "policy-v1",
        policyHash: "policy-hash",
        timeout: result.Timeout,
        metadata: metadata);
}
```

## Resolver configuration sample

The default resolver uses a conservative baseline: low risk warns and allows, medium risk requires acknowledgment, and high risk denies. Hosts can override that behavior by risk level or by exact risk/failure pair.

```csharp
var options = new DlpFailurePolicyOptions
{
    LowRiskBehavior = DlpFailureBehavior.WarnAndAllow,
    MediumRiskBehavior = DlpFailureBehavior.RequireAcknowledgment,
    HighRiskBehavior = DlpFailureBehavior.Deny
};

options.BehaviorOverrides[new DlpFailurePolicyKey(
    DlpIntentRiskLevel.Medium,
    DlpClassificationFailureKind.Timeout)] = DlpFailureBehavior.Defer;

options.BehaviorOverrides[new DlpFailurePolicyKey(
    DlpIntentRiskLevel.High,
    DlpClassificationFailureKind.BlockedResult)] = DlpFailureBehavior.Escalate;

IDlpFailurePolicyResolver resolver =
    new DefaultDlpFailurePolicyResolver(options);
```

The host applies the returned decision using the same execution rules it uses for any other `GovernanceDecision`:

```csharp
GovernanceDecision decision = await EvaluateScannerOutcomeAsync(
    payload,
    resolver,
    cancellationToken);

if (!decision.CanProceed)
{
    // Host-owned behavior: return a response, defer work, require acknowledgment,
    // escalate to review, write decision receipt, or stop provider emission.
    return decision;
}

// Continue with provider emission or governed execution.
return decision;
```

## External provider pseudo-sample

A host adapter for Azure AI Content Safety, Microsoft Purview, a proprietary classifier, or an internal DLP API should follow the same shape:

```csharp
try
{
    ExternalScannerResult scannerResult = await hostScanner.ScanAsync(
        minimizedPayload,
        cancellationToken);

    if (scannerResult.Allowed)
    {
        return GovernanceDecision.Allow(
            correlationId: correlationId,
            policyVersion: policyVersion,
            policyHash: policyHash);
    }

    DlpClassificationFailureKind failureKind = scannerResult.Blocked
        ? DlpClassificationFailureKind.BlockedResult
        : DlpClassificationFailureKind.ClassifiedResult;

    DlpFailurePolicyContext context = DlpFailurePolicyContext.Create(
        failureKind,
        scannerResult.RiskLevel,
        intentCategory: scannerResult.IntentCategory,
        environment: environment,
        correlationId: correlationId,
        traceId: traceId,
        policyVersion: policyVersion,
        policyHash: policyHash,
        metadata: scannerResult.SafeMetadata);

    return (await resolver.ResolveAsync(context, cancellationToken)).Decision;
}
catch (TimeoutException)
{
    var context = DlpFailurePolicyContext.TimeoutFailure(
        DlpIntentRiskLevel.Medium,
        timeout: TimeSpan.FromSeconds(2),
        intentCategory: "governance-emission",
        environment: environment,
        correlationId: correlationId,
        traceId: traceId,
        policyVersion: policyVersion,
        policyHash: policyHash,
        metadata: new Dictionary<string, string>
        {
            ["scanner.family"] = "external-provider",
            ["scanner.failure"] = "timeout"
        });

    return (await resolver.ResolveAsync(context, cancellationToken)).Decision;
}
catch (HttpRequestException)
{
    var context = DlpFailurePolicyContext.Create(
        DlpClassificationFailureKind.ServiceUnavailable,
        DlpIntentRiskLevel.Medium,
        intentCategory: "governance-emission",
        environment: environment,
        correlationId: correlationId,
        traceId: traceId,
        policyVersion: policyVersion,
        policyHash: policyHash,
        metadata: new Dictionary<string, string>
        {
            ["scanner.family"] = "external-provider",
            ["scanner.failure"] = "service_unavailable"
        });

    return (await resolver.ResolveAsync(context, cancellationToken)).Decision;
}
```

Keep that adapter in the host application or in a provider-specific package. Do not make Core depend on the external scanner SDK.

## Choosing fail-open or fail-closed behavior

Choose behavior by risk, data class, environment, and operational need.

| Scenario | Recommended posture | Reason |
| --- | --- | --- |
| Low-risk diagnostic metadata emission | Warn and allow or allow | Blocking all low-risk telemetry during scanner outage may create unnecessary operational blind spots. |
| Medium-risk business workflow with recoverable delay | Require acknowledgment or defer | Human acknowledgment or delayed retry preserves accountability without silently allowing uncertain content. |
| High-risk regulated payload, secret, credential, protected record, or external transfer | Deny or escalate | Fail closed when the consequence of unclassified or blocked content leaving the boundary is unacceptable. |
| Provider emission after local durable audit is already written | Defer provider emission | Preserve the local record and retry or review external emission later. |
| Scanner returns explicit blocked result | Deny or escalate | Treat provider block differently from provider outage. A block says the content is unsafe or disallowed, not merely unknown. |
| Scanner returns classified/sensitive result | Require acknowledgment, defer, deny, or escalate depending on policy | Classification may be allowed in some workflows and forbidden in others. |

Default to fail closed for high-risk, regulated, externally visible, or irreversible actions. Use fail-open only when the host has accepted the risk and records the decision with safe reason codes and metadata.

## Privacy and metadata guidance

`DlpFailurePolicyContext.Metadata` should contain only minimized, safe, provider-neutral values.

Safe examples:

- `scanner.family = regex`
- `scanner.failure = timeout`
- `scanner.category = regulated`
- `classification.family = pii`
- `retry.category = transient`

Unsafe examples:

- raw prompt text;
- raw document content;
- raw scanner findings that include personal data;
- secrets, credentials, keys, tokens, or connection strings;
- raw provider exception payloads;
- unredacted labels that reveal confidential workflow details.

## Operational placement

Common placements include:

| Placement | Use |
| --- | --- |
| Before provider emission | Classify and minimize a governance emission envelope before it leaves the local boundary. |
| Before outbox drain delivery | Defer or dead-letter external provider delivery while preserving local audit/outbox state. |
| Before governed execution | Stop, defer, acknowledge, or escalate an execution request before side effects occur. |
| Before Purview/Event Hubs enrichment | Keep strategy/design provider paths downstream of classification and minimization. |

The host should persist the governance decision, scanner failure category, resolved behavior, and safe metadata in decision receipt or lifecycle records according to its retention policy.

## Related documentation

- [DLP and Classification Failure Policy](dlp-classification-failure-policy.md)
- [Safe Audit and Telemetry Data](safe-audit-telemetry-data.md)
- [Governance Emission Contract](governance-emission-contract.md)
- [Durable Audit and Outbox Persistence](durable-audit-outbox-persistence.md)
- [Hosted Governance Outbox Drain](hosted-governance-outbox-drain.md)
- [Outbox Drain Reliability and Alerting](outbox-drain-reliability-and-alerting.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
