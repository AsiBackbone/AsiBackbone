# High-Risk Administrative Action Scenario

AsiBackbone can help a host application add a governance step before administrative workflows that have business, security, or compliance impact.

Examples include account-status changes, approval of sensitive workflow steps, policy exception requests, data export review, or administrative configuration changes.

> [!IMPORTANT]
> AsiBackbone does not perform the administrative action. The host application owns identity, authorization, UI, persistence, operational execution, and rollback behavior. AsiBackbone helps the host evaluate, acknowledge, and audit the decision before execution.

## Responsibility boundary

| Participant | Responsibility |
| --- | --- |
| Requesting actor | Requests the administrative workflow action. |
| Host application | Owns identity, authorization, operation validation, UI, persistence, and final execution. |
| AsiBackbone | Evaluates the host-provided policy context and returns a governance decision. |
| Acknowledgment layer | Presents a responsibility or risk acknowledgment when the decision requires it. |
| Audit sink or ledger | Preserves decision receipt, reason codes, policy metadata, actor context, and correlation data. |

## Sequence

```text
Requesting actor
  -> Host application: requests administrative action
  -> Host application: builds policy context from actor, action, target, risk, and metadata
  -> AsiBackbone evaluator: evaluates policy context
  -> Host application: receives governance decision

If denied, deferred, or escalation-recommended:
  -> Host application persists decision receipt
  -> Host application returns governed outcome without execution

If acknowledgment-required:
  -> Host application presents acknowledgment challenge
  -> Acknowledgment layer returns accepted or rejected response
  -> Host application persists decision receipt and acknowledgment record
  -> Host application executes only if accepted and host policy permits execution

If allowed or warning:
  -> Host application persists decision receipt
  -> Host application decides whether and how to execute the host-owned operation
```

## Example actions

| Administrative action | Example policy factors | Example governance posture |
| --- | --- | --- |
| Change account status | actor context, target account, environment, policy version | Require a decision record for high-impact changes. |
| Review data export | data category, purpose, volume, destination, jurisdiction | Deny prohibited exports; require acknowledgment for approved but sensitive exports. |
| Request policy exception | exception reason, actor type, risk level, policy hash | Require acknowledgment and preserve reason codes before execution. |
| Approve sensitive workflow step | workflow type, target resource, risk category, correlation ID | Allow low-risk steps; defer or escalate higher-risk steps. |

## Implementation notes

The host should build the policy context before the administrative action is performed. Useful metadata may include operation name, target resource identifier, actor type, risk category, policy version, policy hash, correlation identifier, and host-provided justification.

The host can then map the `GovernanceDecision` into its own UI, API response, queue workflow, or administrative service boundary.

## What this pattern helps prevent

- High-impact administrative actions that rely only on a simple role check.
- Policy exception flows that are disconnected from reason codes.
- Audit trails that show only that something happened, not why it was allowed.
- Approval screens that are not connected to the same decision record the system stores.
- Host operations that proceed before acknowledgment or escalation is completed.

## Adoption note

Start with one administrative workflow that already needs review. Keep execution owned by the host, but route the proposed action through policy evaluation, decision receipt, and acknowledgment handling first.
