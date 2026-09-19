---
description: Canonical 6.0 product terminology and plain-language writing guidance for AsiBackbone documentation and APIs.
---

# 6.0 Product Terminology

AsiBackbone documentation uses ordinary engineering language wherever it is sufficient. Product-specific language is reserved for distinctions the framework actually introduces. Writers should lead with the familiar behavior and introduce a specialized term only when the distinction affects implementation or operation.

## Canonical vocabulary

| Canonical term | Plain-language introduction | Decision and rationale |
| --- | --- | --- |
| AsiBackbone | A governance framework for accountable software execution. | Treat `AsiBackbone` as the product name. The historical expansion of `ASI` belongs in project-history or FAQ material rather than normal onboarding. |
| Policy decision pipeline | Rules evaluate request facts and produce a structured decision before execution. | Use this ordinary description in first-use material. **Governance spine** remains useful architectural positioning, but readers do not need it to understand low-level behavior. |
| Decision receipt | A record of a decision, its outcome, and its reasons. | Replaces **audit residue** in current prose and is the canonical 6.0 public type name. A receipt proves the evaluation outcome; it does not prove that the host performed the operation. |
| Acknowledgment | An actor confirms a challenge or responsibility statement before continuation. | Use **acknowledgment** for the behavior. Use **handshake** only for the actual multi-step request/response protocol or when referring to retained `LiabilityHandshake*` API names. |
| Capability grant | Short-lived, scoped authority for a bounded continuation. | Keep. This is established security language and describes a load-bearing authority boundary. |
| Outbox | Durable local records awaiting reliable delivery. | Use **outbox** in educational prose. Use **governance outbox** when identifying a `GovernanceOutbox*` API, schema/artifact identity, database object, or the specialized record stream. |
| Host-owned execution | The host application performs or refuses the protected operation after inspecting the decision. | Keep. It distinguishes policy evaluation from the real side effect and has no equally clear standard replacement. |
| Context, constraint, decision, evaluator | Familiar inputs, rules, results, and composition services. | Keep. These are ordinary descriptive engineering terms. |
| Signing, verification, trust policy | Integrity and provenance controls configured around records. | Keep. State the concrete guarantee and the host/provider boundary rather than inventing product vocabulary. |

## Progressive introduction

Introduce terms when a developer needs them:

1. **First governed request:** evaluation context, constraint, decision, decision receipt, and host-owned execution.
2. **Human interaction:** acknowledgment; explain the request/response handshake only when the protocol matters.
3. **Delegated authority:** capability grant, scope, and expiration.
4. **Reliable persistence and delivery:** outbox, retries, leasing, and dead-letter behavior.
5. **Trust and provenance:** signing, verification, and trust policy.
6. **Advanced governance:** DLP, classification, metadata limits, and provider-specific operations.

First-use documentation should not require vocabulary from a later stage unless the example implements that concern.

## Product and protocol compatibility

The 6.0 API naming review selected `DecisionReceipt` and related receipt type names. Some member names, serialized fields, schema versions, canonical artifact tags, enum values, database table or column names, and retained protocol types still contain earlier terms. Those identifiers preserve wire, persistence, or compatibility contracts. Documentation should show their exact spelling in code while describing the concept as a decision receipt, acknowledgment, or outbox in surrounding prose.

The [public API naming convention](public-api-naming-600.md) records the coordinated type-name decisions. The [terminology map](terminology-map.md) maps educational concepts to concrete product APIs without creating a second organization-level glossary.

## Writing examples

Prefer:

> AsiBackbone writes a decision receipt after evaluation so the host retains evidence of the outcome and its reasons.

Avoid introducing an internal or historical synonym first:

> AsiBackbone writes audit residue after evaluation.

Prefer:

> Store the decision receipt locally, then use an outbox to deliver the event reliably.

Use the qualified form when the implementation identity matters:

> Register `IGovernanceOutboxStore` and configure the `GovernanceOutboxDrain` worker.

## Documentation ownership

The [documentation ownership contract](documentation-ownership.md) remains unchanged. Learning owns general architecture education and terminology lineage. This repository owns product wording, exact API mappings, runtime behavior, compatibility, and release documentation. Cross-repository links may retain their published titles and URLs even when those titles contain earlier terminology.
