# Sensitive Data Access Request Scenario

AsiBackbone can help a host application evaluate sensitive information requests before the host continues through its own data path.

This scenario applies to systems where access depends on more than identity alone. The host may need to evaluate purpose, actor context, resource classification, region, policy version, risk category, and review requirements before continuing.

> [!IMPORTANT]
> AsiBackbone does not provide data access, database authorization, masking, retention, or compliance certification. The host owns identity, authorization, retrieval, presentation, retention, and compliance review. AsiBackbone provides a governance decision boundary before the host proceeds.

## Responsibility boundary

| Participant | Responsibility |
| --- | --- |
| Requesting actor | Requests access to protected information. |
| Host application | Owns identity, authorization, classification, retrieval, presentation, and final response. |
| AsiBackbone | Evaluates the host-provided policy context and returns a governance decision. |
| Acknowledgment layer | Handles responsibility acknowledgment when the decision requires it. |
| Audit sink or ledger | Preserves reason codes, policy metadata, correlation identifiers, and decision residue. |

## Sequence

```text
Requesting actor
  -> Host application: requests protected information
  -> Host application: builds policy context from actor, purpose, resource, and risk
  -> AsiBackbone evaluator: evaluates policy context
  -> Host application: receives governance decision

If denied, deferred, or escalation-recommended:
  -> Host application persists decision residue
  -> Host application returns governed outcome without continuing through the data path

If acknowledgment-required:
  -> Host application presents acknowledgment challenge
  -> Acknowledgment layer returns accepted or rejected response
  -> Host application persists decision and acknowledgment residue
  -> Host application continues only if accepted and host policy permits access

If allowed or warning:
  -> Host application persists decision residue
  -> Host application decides whether and how to continue through the host-owned data path
```

## Example requests

| Request | Example policy factors | Example governance posture |
| --- | --- | --- |
| View protected record | actor type, purpose, resource classification, region | Allow normal access; defer if purpose or policy metadata is missing. |
| Review protected report | data category, volume, destination, policy version | Require acknowledgment for sensitive review paths; deny prohibited combinations. |
| Access cross-region information | jurisdiction, actor location, resource location, policy hash | Defer or escalate when regional policy needs review. |
| Review exception request | exception reason, requester, target resource, correlation ID | Preserve reason codes and decision receipt for later review. |

## Implementation notes

The host should build a policy context before continuing through the data path. Useful metadata may include data category, purpose of access, target resource identifier, region, actor type, risk category, policy version, policy hash, correlation identifier, and ticket or case identifier.

AsiBackbone can return a decision that the host maps to its own API response, UI flow, workflow queue, or access path. The host remains responsible for enforcing the decision and controlling the data path.

## What this pattern helps prevent

- Treating protected information access as a simple identity check.
- Continuing through a data path before policy context is evaluated.
- Losing the reason codes that shaped the access decision.
- Mixing approval UI with unrelated application logging.
- Creating audit trails that are difficult to connect back to policy version and decision context.

## Adoption note

A good first adoption is one protected report or record-view workflow. The host can evaluate the request, persist decision residue, and return a governed response before any broader data-access pattern is changed.
