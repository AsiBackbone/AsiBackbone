# Government and Regulated Systems: Product Guidance

General regulated-system architecture teaching is canonical in [ASI Backbone Learning](https://asibackbone.github.io/Learning/advanced/governed-execution-in-regulated-systems.html).

This product page documents only how current AsiBackbone primitives can contribute application-level decision evidence.

> [!IMPORTANT]
> AsiBackbone does not guarantee compliance with any law, regulation, audit framework, security standard, records policy, or organizational policy.

## Product contribution

A consuming host can use AsiBackbone to preserve structured evidence such as:

- governance outcome and reason codes;
- actor and operation identity;
- policy version/hash;
- correlation and trace identifiers;
- acknowledgment identifiers when required;
- durable audit/lifecycle/outbox records;
- capability issuance/validation evidence where used.

These are evidence contributions inside a larger governance program.

## Product-owned standards mapping

For named frameworks, regulations, and standards, use [External Governance, Regulatory, and Standards Mapping](external-framework-and-standards-mapping.md).

That page remains product-owned because it maps external concerns to actual AsiBackbone primitives, implementation status, host obligations, and explicit non-coverage.

## Host-owned responsibilities

The consuming organization remains responsible for:

- legal applicability and interpretation;
- identity, authorization, and access control;
- policy governance and source retention;
- risk, privacy, safety, and impact assessment;
- infrastructure/security controls and key custody;
- records retention, deletion, and review;
- human oversight design;
- execution and operational safeguards;
- incident response, audit, certification, and regulator engagement.

## Product implementation references

- [Regulated Governance Profile](regulated-governance-profile.md)
- [Regulated Storage and Signing Verification Checklist](regulated-storage-and-signing-verification-checklist.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
- [Cryptographic Security Posture and Production Guidance](cryptographic-security-posture.md)
- [External Governance, Regulatory, and Standards Mapping](external-framework-and-standards-mapping.md)
