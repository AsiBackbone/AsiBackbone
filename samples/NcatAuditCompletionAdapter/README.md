# NCAT Audit-Completion Adapter Sample

This non-packable reference project demonstrates an optional composition-boundary adapter between NCAT mutation-audit completion evidence and AsiBackbone governed execution lifecycle evidence.

## Boundary

- The project references `AsiBackbone.Core` only.
- No required AsiBackbone package references NCAT.
- NCAT core does not reference AsiBackbone.
- A consuming host translates its NCAT receipt or completion-outbox entry into `NcatAuditCompletionHandoff`.
- The adapter does not coordinate a distributed transaction or claim exactly-once delivery.
- `NcatAuditCompletionContract` validates an independently modeled `NcatAuditCompletionMessage` against version 1 of NCAT's audit-completion contract. The sample tests replay NCAT's pinned contract vectors to detect drift.

See [Optional NCAT Audit-Completion Adapter](../../docs/articles/ncat-audit-completion-adapter.md) for the integration sequence, outcome mapping, idempotency behavior, and privacy boundary.
