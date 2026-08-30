# Acknowledgment Workflow

The broader **Dynamic Liability Handshake** idea is taught in Learning through [Acknowledgment and Audit Residue](https://asibackbone.github.io/Learning/tutorials/acknowledgment-and-audit-residue.html) and the [Intent to Execution accountability pattern](https://asibackbone.github.io/Learning/architecture/intent-to-execution-accountability-pattern.html).

This page is authoritative for the concrete AsiBackbone acknowledgment workflow.

> [!IMPORTANT]
> The workflow records an acknowledgment checkpoint. It does not create legal protection, legal non-repudiation, compliance certification, production tamper-evidence, or a substitute for organizational/legal review.

## Implemented product role

Core exposes grounded responsibility-handshake records including `LiabilityHandshakeRequest` and `LiabilityHandshakeAcknowledgment`.

A typical product flow is:

~~~text
Governance decision requires acknowledgment
  -> Host creates handshake request
  -> Host presents the required acknowledgment
  -> Actor accepts or rejects
  -> Host records the acknowledgment response
  -> Host links the response to audit/lifecycle evidence
  -> Host decides whether execution may continue
~~~

## Product data carried by the workflow

The concrete contracts support fields such as:

- stable handshake and acknowledgment identifiers;
- actor identity/type/display information;
- operation name;
- reason and acknowledgment codes;
- required acknowledgment text;
- risk information;
- correlation and trace identifiers;
- policy version/hash;
- schema version;
- host-provided metadata;
- accepted/rejected state and timestamp.

Use the [Generated API Reference](../api/) for exact members and signatures.

## Host-owned responsibilities

The consuming host owns:

- authentication and authorization of the acknowledging actor;
- UI/presentation and accessibility;
- policy defining when acknowledgment is required;
- storage-provider selection and retention;
- whether accepted acknowledgment permits continuation;
- re-evaluation/freshness rules before execution;
- actual execution.

## Production/security boundaries

An acknowledgment record is evidence of a response, not a transferable execution credential by itself.

Do not describe it as:

- acceptance of all legal liability;
- proof of regulatory compliance;
- tamper-proof or legally non-repudiable by default;
- a substitute for current authorization or execution-boundary checks.

## Related product documentation

- [Core API Domain Model](core-domain-language.md)
- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
- [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md)
- [Signed Audit and Outbox Records](signed-audit-and-outbox-records.md)
- [Verification Policy and Result Handling](verification-policy-and-result-handling.md)
