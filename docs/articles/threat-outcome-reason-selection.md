# Threat Outcome Reason Selection

When multiple threat model contributors return actionable assessments, the default evaluator determines the final threat outcome independently from contributor order.

## Outcome precedence

The evaluator selects the most restrictive effective outcome using this order:

1. `Denied`
2. `EscalationRecommended`
3. `AcknowledgmentRequired`
4. `Deferred`
5. `Warning`

An earlier, less restrictive finding cannot supply the reason for a later, more restrictive final outcome.

## Deterministic reason handling

For `Deferred`, `AcknowledgmentRequired`, and `EscalationRecommended`, the evaluator retains the first contributor reason whose effective outcome matches the selected final outcome. Contributor registration order therefore remains the deterministic tie-breaker when multiple contributors return the same selected restrictive outcome.

For example, when contributors return `Warning`, then `EscalationRecommended`, then another `EscalationRecommended`, the final decision:

- is `EscalationRecommended`;
- uses the reason from the first escalation contributor;
- does not substitute the earlier warning reason; and
- does not replace the first escalation reason with the later matching escalation reason.

This rule avoids an arbitrary last-writer-wins result while preserving stable contributor ordering.

## Existing aggregation behavior

The reason-selection rule does not change the established behavior for other outcomes:

- `Denied` continues to retain all accumulated actionable threat reasons in contributor order.
- A warning-only evaluation continues to retain all warning reasons in contributor order.
- Non-actionable `ThreatAssessment.NoThreat()` results do not contribute reasons.

The selected reason is intended to explain the outcome that actually blocked or redirected execution. Decision receipt, telemetry, and API responses should therefore no longer associate a restrictive non-denial outcome with an unrelated earlier warning.