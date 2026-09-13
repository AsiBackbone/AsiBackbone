# Threat Model Contributors

Threat model contributors let a host attach threat-aware checks to the AsiBackbone policy evaluation pipeline without turning AsiBackbone into a standalone security product.

The package provides the extension point and structured decision plumbing. The host remains responsible for defining the actual checks, thresholds, categories, and operational response.

> [!IMPORTANT]
> Threat model contributors are governance hardening hooks, not a complete threat-detection product. They do not claim to detect every attack, prove legal compliance, replace application security review, or provide legal protection.

## Safe-collapse invariant

Unsafe, malformed, or ambiguous inputs should not become unsafe actions.
They should become traceable, constrained decisions.

For policy evaluation, this means suspicious or invalid policy inputs should generally collapse to one of the constrained outcomes:

- `Denied`
- `Deferred`
- `AcknowledgmentRequired`
- `EscalationRecommended`

They should not collapse to `Allowed` unless the input is valid, expected, and authorized under the active policy structure.

`Warning` may be appropriate for low-confidence or low-impact findings only when the host intentionally permits execution and preserves the finding in the decision reasons and audit trail.

## Why this exists

Policy evaluation already narrows a request into a governance decision such as allow, deny, defer, acknowledgment required, or escalation recommended. Threat model contributors add an earlier inspection point for signals such as malformed input, replay indicators, capability-token mismatch, regional-policy mismatch, or unsafe external command requests.

The intent is practical hardening:

- suspicious inputs should become traceable governance decisions;
- severe or high-confidence findings should not silently collapse to `Allowed`;
- contributor failures should fail closed by default when contributors are explicitly registered;
- assessment details should appear in decision reasons and audit-facing metadata where the selected decision shape supports it.

## Pipeline position

Threat contributors run before normal constraint composition in the default evaluator. They do not execute the requested operation. They only return structured assessments that the evaluator can translate into governance decisions and operation reasons.

A typical flow is:

```text
Policy context
  -> threat model contributors
  -> threat assessments and reason metadata
  -> constraint evaluation
  -> decision policy
  -> governance decision
  -> host-owned execution boundary
  -> audit receipt / governance emission
```

Severe or actionable assessments may stop normal constraint evaluation and produce a constrained decision immediately. Lower-severity assessments may flow forward as warning reasons, depending on host policy.

## Extension point

Implement `IThreatModelContributor<TContext>` for the same framework-neutral context type used by the policy evaluator.

```csharp
using AsiBackbone.Core.Constraints;
using AsiBackbone.Core.Decisions;
using AsiBackbone.Core.ThreatModeling;

public sealed class ReplayThreatContributor : IThreatModelContributor<MyPolicyContext>
{
    public string Name => "replay-threat-contributor";

    public ValueTask<ThreatAssessment> AssessAsync(
        MyPolicyContext context,
        CancellationToken cancellationToken = default)
    {
        if (!context.Metadata.TryGetValue("request.nonce", out string? nonce) || string.IsNullOrWhiteSpace(nonce))
        {
            return ValueTask.FromResult(ThreatAssessment.Create(
                ThreatSeverity.High,
                ThreatCategories.ReplayAttempt,
                "threat.replay_nonce_missing",
                "The request did not include a replay-protection nonce.",
                GovernanceDecisionOutcome.EscalationRecommended));
        }

        return ValueTask.FromResult(ThreatAssessment.NoThreat());
    }
}
```

## Register with the default evaluator

Threat contributors are supplied explicitly to the default evaluator. Multiple contributors run in deterministic order before normal constraint composition.

```csharp
var evaluator = DefaultAsiBackbonePolicyEvaluator.CreateBuilder<MyPolicyContext>()
    .AddConstraints(constraints)
    .AddThreatModelContributors(threatModelContributors)
    .WithDecisionPolicy(decisionPolicy)
    .WithOptions(new AsiBackbonePolicyEvaluatorOptions
    {
        TreatThreatContributorExceptionAsDenial = true,
        PreventThreatAssessmentAllowDowngrade = true
    })
    .Build();
```

## Assessment fields

A `ThreatAssessment` includes:

- `Severity` — `None`, `Low`, `Medium`, `High`, or `Critical`.
- `Category` — a host-defined category; `ThreatCategories` provides common names.
- `ReasonCode` — a stable machine-readable code.
- `Description` — a human-readable explanation.
- `RecommendedOutcome` — the governance outcome the contributor recommends.
- `Confidence` — a value from `0.0` to `1.0`.
- `Metadata` — optional host metadata retained on generated operation reasons.

The built-in category constants are intentionally conventional, not exhaustive:

- `InputMalformed`
- `InputOversized`
- `PolicyBypassAttempt`
- `PromptInjectionLikeInput`
- `ReplayAttempt`
- `CapabilityTokenMismatch`
- `RegionPolicyMismatch`
- `UnsafeExternalCommand`
- `AuditIntegrityRisk`

Hosts may use their own category strings when these names do not fit.

## Recommended reason-code shape

Reason codes should be stable, machine-readable, and safe to expose in logs or support workflows. Prefer lowercase dotted names that identify the contributor family and finding without storing sensitive input values.

Examples:

| Category | Example reason code | Typical constrained outcome |
| --- | --- | --- |
| `InputMalformed` | `threat.input_malformed` | `Denied` or `Deferred` |
| `InputOversized` | `threat.input_oversized` | `Denied` |
| `PolicyBypassAttempt` | `threat.policy_bypass_attempt` | `EscalationRecommended` or `Denied` |
| `PromptInjectionLikeInput` | `threat.prompt_injection_like_input` | `AcknowledgmentRequired`, `EscalationRecommended`, or `Denied` |
| `ReplayAttempt` | `threat.replay_attempt` | `Denied` or `EscalationRecommended` |
| `CapabilityTokenMismatch` | `threat.capability_token_mismatch` | `Denied` |
| `RegionPolicyMismatch` | `threat.region_policy_mismatch` | `Deferred` or `EscalationRecommended` |
| `UnsafeExternalCommand` | `threat.unsafe_external_command` | `AcknowledgmentRequired`, `EscalationRecommended`, or `Denied` |
| `AuditIntegrityRisk` | `threat.audit_integrity_risk` | `Denied` or `Deferred` |

Avoid embedding raw prompts, secrets, tokens, personal data, or full command payloads in reason codes or descriptions. Put only sanitized, bounded, host-approved metadata into `Metadata`.

## Safe and unsafe policy-input examples

These examples are illustrative. Hosts should define exact thresholds and outcomes for their domain.

| Input condition | Example finding | Safer decision behavior | Notes |
| --- | --- | --- | --- |
| Expected request shape, authorized capability grant, matching region, valid audit context | No threat | Continue through normal constraints; may become `Allowed` if all policies pass | `Allowed` should be the result of valid input plus successful policy evaluation, not the absence of checks. |
| Missing required metadata such as operation name, region, tenant, or correlation key | `InputMalformed` | `Denied` or `Deferred` | Missing shape should not become execution by default. |
| Payload exceeds host-defined size or field-count budget | `InputOversized` | `Denied` | Size/shape guardrails protect policy evaluation and audit storage from unbounded input. |
| Nonce missing, expired, or reused | `ReplayAttempt` | `Denied` or `EscalationRecommended` | Replay uncertainty should create a traceable stop or review path. |
| Capability token does not match operation, subject, scope, region, or expiry | `CapabilityTokenMismatch` | `Denied` | Least-privilege boundaries should fail closed. |
| Request attempts to route around declared policy or endpoint governance metadata | `PolicyBypassAttempt` | `EscalationRecommended` or `Denied` | Bypass indicators should be visible to operators. |
| Prompt-injection-like text appears in a tool or agent command request | `PromptInjectionLikeInput` | `AcknowledgmentRequired`, `EscalationRecommended`, or `Denied` | The host decides whether review, acknowledgment, or denial is appropriate for the action class. |
| Region-specific rule cannot be resolved or conflicts with the requested action | `RegionPolicyMismatch` | `Deferred` or `EscalationRecommended` | Deferral is useful when another resolver or policy version may be needed. |
| External-system command is not in an allowlist or exceeds a declared operational boundary | `UnsafeExternalCommand` | `AcknowledgmentRequired`, `EscalationRecommended`, or `Denied` | External execution should remain host-owned and capability-gated. |
| Audit receipt, signing context, or outbox metadata is missing for an operation that requires it | `AuditIntegrityRisk` | `Denied` or `Deferred` | Consequential actions should not proceed without required accountability residue. |

## Fail-closed behavior

`TreatThreatContributorExceptionAsDenial` defaults to `true`. If a registered contributor throws an expected non-cancellation, non-critical exception, the evaluator returns a denied governance decision with the stable reason code `asibackbone.threat.contributor_exception`.

Set this option to `false` only when the host intentionally wants contributor exceptions to propagate to its own error boundary.

Fail-closed behavior is especially important when a contributor is responsible for capability-token integrity, regional policy checks, replay detection, or external command screening. A broken check should not silently create a path to execution.

### Critical failures still propagate

Fail-closed contributor handling is not a substitute for host/runtime failure handling. The evaluator does not convert cancellation or critical host/runtime failures into ordinary denied governance decisions.

Critical exceptions, including `OutOfMemoryException`, `StackOverflowException` where catchable, `AccessViolationException`, `AppDomainUnloadedException`, `BadImageFormatException`, `InvalidProgramException`, and wrapper exceptions containing those failures, continue to propagate. Hosts should handle those through health checks, process restart policy, incident response, or other operational mechanisms rather than treating them as normal threat-contributor denials.

When a contributor exception is converted to denial, the evaluator logs event `4130` (`ThreatContributorExceptionDeniedError`) with the contributor name, exception type, correlation ID, policy version, and policy hash. This event identifies the exception-as-denial path and is distinct from an ordinary threat assessment denial.

## Allow-downgrade protection

`PreventThreatAssessmentAllowDowngrade` defaults to `true`. When enabled, actionable threat assessments are protected from being silently replaced by a pure `Allowed` decision from a custom decision policy.

This keeps suspicious, malformed, ambiguous, replayed, or unsafe inputs traceable in the governance trail.

Disable this only for a deliberate host-owned policy design, and cover that design with tests that prove the host still preserves any safety-critical reason metadata somewhere appropriate.

## Audit and decision receipt expectations

Threat assessments should leave enough residue for later review without leaking sensitive data. At minimum, a consequential threat finding should preserve:

- the final governance outcome;
- stable reason codes;
- contributor name;
- threat category;
- threat severity;
- confidence when meaningful;
- sanitized metadata needed for support or investigation;
- correlation ID;
- policy version and policy hash when the context supplies them;
- decision timestamp and host-owned actor/request identifiers where appropriate.

The decision receipt should answer: what was requested, which policy structure evaluated it, what threat signal changed the decision path, and which constrained outcome resulted.

## Fuzz-test and contract-test guidance

Threat contributors should be tested as small deterministic units and as part of the composed evaluator.

Recommended contract tests:

- a no-threat assessment allows normal constraint evaluation to continue;
- malformed, replayed, mismatched, or unsafe inputs do not produce a pure `Allowed` decision;
- high or critical findings short-circuit to a constrained outcome where intended;
- low-severity findings become `Warning` only when execution is intentionally permitted;
- contributor exceptions fail closed by default;
- cancellation and critical contributor failures still propagate to the host;
- `PreventThreatAssessmentAllowDowngrade` prevents a custom decision policy from erasing actionable findings;
- reason metadata includes contributor, category, severity, confidence, and sanitized host metadata;
- policy version and policy hash remain attached to the final decision when supplied by the context.

Recommended fuzz tests:

- missing metadata keys;
- empty, whitespace, oversized, or unusual Unicode values;
- unexpected operation names, regions, subjects, scopes, and capability strings;
- replayed nonce-like values;
- malformed or expired token-like values;
- prompt-injection-like command text in fields that may later drive tools or external systems;
- conflicting region or policy-version values;
- null or extreme confidence values when constructing assessments.

Fuzz tests do not need to prove that AsiBackbone detects every threat. Their purpose is narrower: preserve the invariant that unsafe, malformed, or ambiguous inputs do not become unsafe actions through the policy pipeline.

## Guidance for extension authors

Extension authors should keep contributors narrow and explicit.

Prefer contributors that:

- inspect one threat family or input invariant at a time;
- use deterministic checks where possible;
- return stable reason codes;
- keep descriptions human-readable but non-sensitive;
- bound metadata size and content;
- avoid network calls unless the host has a clear timeout and failure policy;
- treat missing context as suspicious when that context is required for safe evaluation;
- document the intended severity, category, and recommended outcome;
- include tests for valid input, invalid input, contributor failure, critical failure passthrough, and downgrade protection.

Avoid contributors that:

- claim general-purpose threat detection;
- make legal conclusions;
- execute the operation being evaluated;
- mutate host state during assessment;
- store raw prompts, secrets, tokens, or personal data in reason metadata;
- return `Allowed` for inputs they cannot parse or classify.

## Gateway, robotics, and external-system integrations

Threat model contributors are useful for future gateway, robotics, agent-tool, and external-system integrations, but they do not make those integrations part of the initial core feature by themselves.

For gateway-style integrations, contributors can inspect whether the request is shaped like an authorized external command, whether the capability grant matches the requested operation, whether the region or policy resolver agrees with the command, and whether required audit/signing context is present.

For robotics-style or physical-world execution scenarios, keep the same software boundary: AsiBackbone can help produce constrained governance decisions, but the host remains responsible for the operational gateway, device safety controls, hardware interlocks, regional legal review, monitoring, and emergency stop behavior. Do not present a contributor as a robot safety system.
