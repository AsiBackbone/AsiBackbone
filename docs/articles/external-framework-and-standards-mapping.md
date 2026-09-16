# External Governance, Regulatory, and Standards Mapping

Last reviewed: July 12, 2026

> [!IMPORTANT]
> This page is a scoping crosswalk, not a certification, legal opinion, compliance assessment, control implementation statement, or claim that AsiBackbone satisfies any framework, regulation, or standard. AsiBackbone provides software primitives that a host may use within a broader governance, security, privacy, risk-management, or assurance program.

AsiBackbone is a governance spine for consequential .NET decision flow. It can help a host make policy decisions explicit, preserve evidence about those decisions, require acknowledgment, constrain follow-on authority, and keep execution under host control. It does not determine which laws apply, define an organization's risk appetite, establish a management system, configure security infrastructure, or make a consuming application compliant.

## How to read this page

The references below are intentionally separated by type:

| Category | References |
| --- | --- |
| Voluntary AI governance and risk guidance | NIST AI RMF 1.0; ISO/IEC 42001:2023; ISO/IEC 23894:2023 |
| Laws and regulations | EU AI Act; GDPR; HIPAA Security Rule |
| Cybersecurity and control frameworks | NIST CSF 2.0; NIST SP 800-53 Rev. 5; ISO/IEC 27001:2022 |
| Technical interoperability standard | OpenUSD Core Specification 1.0 |

OpenUSD is not a regulation. ISO management-system standards are not laws. NIST guidance and control catalogs do not certify an implementation. This page preserves those distinctions.

The following alignment labels are used throughout:

| Label | Meaning |
| --- | --- |
| **Direct library primitive** | AsiBackbone exposes an implemented type, workflow, or contract relevant to the concern. |
| **Host-configured support** | AsiBackbone provides a seam, but the host must supply policy, infrastructure, providers, or enforcement. |
| **Evidence contribution only** | AsiBackbone records may contribute to a broader assessment or audit process but do not satisfy it by themselves. |
| **Outside AsiBackbone scope** | The concern is not implemented or decided by AsiBackbone. |

## AsiBackbone primitives used in the crosswalk

The mappings refer only to implemented surfaces:

- `GovernanceDecision` outcomes and reason codes;
- actor context and operation identity;
- policy version and policy hash;
- host-owned constraints and decision policies;
- acknowledgment and responsibility-handshake workflows;
- capability grants and bounded continuation;
- decision receipt, durable ledger records, lifecycle records, and outbox records;
- correlation and trace identifiers;
- metadata budgets, classification results, and sanitization helpers;
- signing, verification, and audit-integrity seams;
- host-owned execution enforcement;
- optional OpenTelemetry projection after local evidence exists.

## NIST AI Risk Management Framework 1.0

NIST AI RMF 1.0 is voluntary guidance organized around **Govern**, **Map**, **Measure**, and **Manage**. NIST states that AI RMF 1.0 is being revised, so this mapping identifies the version explicitly and should be reviewed when NIST publishes a successor.

| AI RMF function | Relevant AsiBackbone primitive | Potential support | Host-owned work still required | Explicit non-coverage |
| --- | --- | --- | --- | --- |
| Govern | Actor context, policy version/hash, decision policies, acknowledgment, capability grants, decision receipt | **Direct library primitive** for accountable decision flow and evidence about which policy governed a proposed action | Governance structure, roles, policy approval, risk appetite, oversight, training, management review | AsiBackbone does not establish an AI governance program or assign organizational accountability |
| Map | Evaluation context, operation identity, metadata, threat-model contributors, regional and domain constraints | **Host-configured support** for representing known context and policy-relevant facts | Use-case inventory, affected-party analysis, system boundaries, impact analysis, lifecycle context | AsiBackbone does not discover risks, affected populations, dependencies, or deployment context automatically |
| Measure | Structured outcomes, reason codes, tests, decision receipt, telemetry projection | **Evidence contribution only** for observed governance decisions and policy-path behavior | Model evaluation, fairness assessment, robustness testing, security testing, quantitative risk measurement | AsiBackbone does not measure model quality, bias, safety, explainability, or trustworthiness |
| Manage | Allow, warning, deny, defer, acknowledgment, escalation, bounded grants, outbox/lifecycle evidence | **Direct library primitive** for decision gating and controlled continuation | Organizational risk treatment, remediation ownership, exception approval, monitoring thresholds, incident response | AsiBackbone does not choose or execute enterprise risk treatments |

Official source: [NIST AI Risk Management Framework](https://www.nist.gov/itl/ai-risk-management-framework)

## ISO/IEC 42001:2023

ISO/IEC 42001 defines requirements for an artificial intelligence management system. AsiBackbone can contribute operational decision evidence inside such a system, but it does not establish or certify the management system.

| Concern | Relevant AsiBackbone primitive | Potential support | Host-owned work still required | Explicit non-coverage |
| --- | --- | --- | --- | --- |
| Policy and operational control | Policy version/hash, constraints, decision policies, host-owned execution boundary | **Host-configured support** for applying approved rules at a consequential-action boundary | AIMS scope, policy governance, documented procedures, control ownership | AsiBackbone does not create an AIMS |
| Roles and accountability | Actor context, operation identity, acknowledgments | **Direct library primitive** for associating a decision and responsibility checkpoint with an actor | Organizational roles, authority, competence, training, segregation of duties | Actor records do not prove organizational accountability by themselves |
| Monitoring and evidence | Decision receipt, durable records, lifecycle events, OpenTelemetry projection | **Evidence contribution only** for review and monitoring | Monitoring objectives, metrics, management review, internal audit, corrective action | AsiBackbone does not conduct audits or management review |
| Improvement inputs | Reason codes, escalation outcomes, verification failures, outbox/dead-letter state | **Evidence contribution only** for identifying recurring decision and delivery conditions | Nonconformity handling, root-cause analysis, corrective action, continual improvement | The library does not operate a continual-improvement program |

Official source: [ISO/IEC 42001:2023](https://www.iso.org/standard/81230.html)

## ISO/IEC 23894:2023

ISO/IEC 23894 provides guidance on AI-related risk management. AsiBackbone can support selected treatment checkpoints and evidence paths after an organization has identified and evaluated its risks.

| Concern | Relevant AsiBackbone primitive | Potential support | Host-owned work still required | Explicit non-coverage |
| --- | --- | --- | --- | --- |
| Risk context | Evaluation context, metadata, operation identity, threat-model contributors | **Host-configured support** for carrying known risk-relevant facts into a decision | Risk identification, stakeholder analysis, impact and likelihood assessment | AsiBackbone does not identify or score risks automatically |
| Risk treatment checkpoint | Deny, defer, acknowledgment-required, escalation-recommended outcomes | **Direct library primitive** for applying a host-selected response before execution | Risk appetite, treatment selection, approval authority, residual-risk acceptance | Outcomes are not a complete risk-treatment process |
| Traceability | Policy version/hash, reason codes, actor, correlation and trace identifiers | **Evidence contribution only** for reconstructing the decision path | Retention, evidence review, source-policy preservation, audit procedures | Traceability records do not demonstrate effective risk management by themselves |

Official source: [ISO/IEC 23894:2023](https://www.iso.org/standard/77304.html)

## EU Artificial Intelligence Act

The EU AI Act is a legal framework with obligations that depend on the system, use case, legal role, risk category, jurisdiction, and implementation timeline. This section is a high-level technical scoping aid, not legal interpretation.

| Concern | Relevant AsiBackbone primitive | Potential support | Host-owned work still required | Explicit non-coverage |
| --- | --- | --- | --- | --- |
| Risk-aware operational gating | Constraints, decision policies, structured outcomes, host-owned execution checks | **Host-configured support** for preventing or pausing selected operations | Legal applicability analysis, role determination, risk classification, approved control design | AsiBackbone does not determine whether a system is prohibited, high-risk, limited-risk, or general-purpose AI |
| Recordkeeping and traceability | Decision receipt, durable ledger/outbox records, policy identity, actor and operation identifiers | **Evidence contribution only** for selected application decisions | Required logging scope, technical-documentation set, retention, access, reporting | AsiBackbone records are not an EU AI Act technical file or conformity dossier |
| Human oversight | Acknowledgment-required and escalation-recommended outcomes | **Host-configured support** for a human checkpoint before host execution | Competent oversight design, authority, training, UI, procedures, monitoring | Acknowledgment alone does not satisfy human-oversight obligations |
| Post-deployment evidence | Lifecycle events, verification results, telemetry projection, dead-letter state | **Evidence contribution only** for operational review | Post-market monitoring, incident reporting, regulator interaction, quality management | AsiBackbone does not perform market surveillance, registration, or conformity assessment |

AsiBackbone does not classify an organization as a provider, deployer, importer, distributor, product manufacturer, or authorized representative. It also does not determine applicable deadlines or legal obligations.

Official sources:

- [European Commission AI regulatory framework](https://digital-strategy.ec.europa.eu/en/policies/regulatory-framework-ai)
- [Regulation (EU) 2024/1689](https://eur-lex.europa.eu/eli/reg/2024/1689/oj)

## HIPAA Security Rule

The Health Insurance Portability and Accountability Act is abbreviated **HIPAA**. The HIPAA Security Rule protects electronic protected health information through administrative, physical, and technical safeguards. The January 2025 Security Rule update remains identified by HHS as a proposed rule as of this page's review date.

| Concern | Relevant AsiBackbone primitive | Potential support | Host-owned work still required | Explicit non-coverage |
| --- | --- | --- | --- | --- |
| Access-sensitive or disclosure-sensitive operations | Actor context, operation identity, constraints, decision outcomes | **Host-configured support** for governing a defined application operation | Authentication, authorization, identity proofing, workforce policy, minimum-necessary analysis | AsiBackbone does not determine whether an actor may access PHI or ePHI |
| Audit controls | Decision receipt, durable ledger records, correlation and trace identifiers | **Evidence contribution only** for selected governed decisions | Complete audit logging, monitoring, review procedures, retention, access controls | AsiBackbone is not a complete HIPAA audit-control implementation |
| Integrity | Canonical hashing, signing/verification seams, audit-integrity chain model | **Host-configured support** for protected evidence paths | Key custody, encryption, verification operations, immutable storage, backup, recovery | Signing seams do not create HIPAA compliance or tamper-evidence by themselves |
| Data minimization | Metadata budgets, reserved-key guidance, sanitization and classification seams | **Host-configured support** for reducing metadata exposure | PHI/ePHI identification, privacy policy, DLP, redaction, encryption, data lifecycle | AsiBackbone does not classify data as PHI or ePHI |
| High-risk review | Acknowledgment and escalation outcomes | **Evidence contribution only** for a responsibility checkpoint | Administrative safeguards, incident response, contingency planning, breach analysis | Acknowledgment does not replace required safeguards or authorization |

AsiBackbone does not establish covered-entity or business-associate status and does not provide business associate agreements, network security, physical safeguards, disaster recovery, breach notification, or HIPAA compliance.

Official source: [HHS HIPAA Security Rule](https://www.hhs.gov/hipaa/for-professionals/security/index.html)

## General Data Protection Regulation

GDPR obligations depend on the processing activity, legal role, lawful basis, jurisdiction, data category, and organizational controls. AsiBackbone can contribute decision evidence and metadata-minimization patterns, but it does not perform privacy governance.

| Concern | Relevant AsiBackbone primitive | Potential support | Host-owned work still required | Explicit non-coverage |
| --- | --- | --- | --- | --- |
| Accountability evidence | Actor, operation, policy identity, reason codes, decision receipt | **Evidence contribution only** for selected consequential decisions | Controller/processor analysis, records of processing, privacy governance, review | AsiBackbone does not demonstrate GDPR compliance |
| Data minimization | Metadata budgets, allow-list signing metadata, sanitization guidance | **Host-configured support** for limiting governance metadata | Data inventory, lawful basis, purpose limitation, retention, deletion | The library does not determine which personal data is necessary |
| Human review of automated processing | Acknowledgment and escalation outcomes, host-owned execution boundary | **Host-configured support** for inserting a review checkpoint | Article 22 applicability, meaningful human review design, notices, appeal and remedy | AsiBackbone does not interpret Article 22 or provide data-subject rights |
| Traceability | Correlation IDs, trace IDs, durable records | **Evidence contribution only** for connecting decisions to host processing | DPIAs, transfer assessments, breach notification, regulator and subject responses | Decision records are not a DPIA or record of processing |

Official source: [Regulation (EU) 2016/679](https://eur-lex.europa.eu/eli/reg/2016/679/oj)

## NIST Cybersecurity Framework 2.0

NIST CSF 2.0 is a voluntary cybersecurity risk-management framework. AsiBackbone is most relevant as an application-level evidence and decision component within a host's broader Govern, Protect, Detect, Respond, and Recover practices.

| Concern | Relevant AsiBackbone primitive | Potential support | Host-owned work still required | Explicit non-coverage |
| --- | --- | --- | --- | --- |
| Govern | Policy identity, actor accountability, provider boundaries, durable evidence | **Evidence contribution only** for selected software decisions | Cybersecurity strategy, roles, supply-chain governance, risk appetite | AsiBackbone does not operate a cybersecurity program |
| Protect | Constraints, capability grants, host-owned execution enforcement | **Host-configured support** for selected application operations | IAM, network controls, endpoint protection, secrets, secure configuration | AsiBackbone is not a preventive security platform |
| Detect and Respond | Reason codes, escalation, verification failures, telemetry, outbox/dead-letter state | **Evidence contribution only** for observable governance conditions | Detection engineering, SIEM, alerting, incident response, containment | The library does not detect intrusions or run incident response |
| Recover | Durable records and lifecycle evidence | **Evidence contribution only** for reconstruction and review | Backups, restoration, resilience, continuity planning | AsiBackbone does not provide recovery infrastructure |

Official source: [NIST Cybersecurity Framework](https://www.nist.gov/cyberframework)

## NIST SP 800-53 Rev. 5

NIST SP 800-53 is a control catalog. AsiBackbone may contribute implementation evidence around selected control concerns, especially audit and accountability, access-control decision evidence, assessment and monitoring, system integrity, and privacy-related processing records.

| Control concern | Relevant AsiBackbone primitive | Potential support | Host-owned work still required | Explicit non-coverage |
| --- | --- | --- | --- | --- |
| Audit and accountability | Decision receipt, durable records, actor, operation, policy identity | **Evidence contribution only** for governed operations | Control selection, complete audit architecture, retention, review, protection | AsiBackbone does not implement the AU family as a whole |
| Access-control evidence | Constraints, outcomes, capability grants, execution checks | **Host-configured support** for application decisions | Authentication, authorization, entitlement management, enforcement coverage | Capability grants do not replace access control |
| Assessment, authorization, monitoring | Tests, verification results, telemetry, lifecycle evidence | **Evidence contribution only** for selected components | Security assessment, authorization package, continuous monitoring program | AsiBackbone does not produce an authorization to operate |
| System and information integrity | Signing, verification, integrity-chain seams, failure outcomes | **Host-configured support** for selected records | Secure architecture, malware protection, vulnerability management, key operations | The library does not implement a complete integrity program |

Official source: [NIST SP 800-53 Rev. 5](https://csrc.nist.gov/pubs/sp/800/53/r5/upd1/final)

## ISO/IEC 27001:2022

ISO/IEC 27001 defines requirements for an information security management system. AsiBackbone may provide operational evidence inside an ISMS but does not establish, operate, audit, or certify one.

| Concern | Relevant AsiBackbone primitive | Potential support | Host-owned work still required | Explicit non-coverage |
| --- | --- | --- | --- | --- |
| Controlled operational decisions | Policy context, constraints, outcomes, host execution boundary | **Host-configured support** for selected application controls | ISMS scope, risk assessment, control selection, procedures | AsiBackbone does not create an ISMS |
| Evidence and monitoring | Decision receipt, durable records, telemetry, verification results | **Evidence contribution only** for selected operations | Monitoring program, internal audit, management review, corrective action | Records do not prove Annex A control effectiveness |
| Change and responsibility traceability | Actor, operation, policy version/hash, acknowledgment | **Evidence contribution only** for accountable decisions | Change management, segregation of duties, approvals, competence | AsiBackbone does not define the Statement of Applicability or certify controls |

Official source: [ISO/IEC 27001:2022](https://www.iso.org/standard/27001)

## OpenUSD Core Specification 1.0: technical interoperability context

OpenUSD is a technical interoperability and scene-description standard, not a regulatory framework. It can describe and compose shared 3D scenes and digital-twin world models. AsiBackbone does not currently implement OpenUSD.

A possible future composition is:

```text
OpenUSD-aware host or external adapter
  -> identifies a consequential stage, layer, prim, variant, physics, or deployment operation
  -> translates safe operation facts into AsiBackbone policy context
  -> receives a GovernanceDecision
  -> preserves decision receipt and any acknowledgment or capability grant
  -> host performs or refuses the OpenUSD operation
```

Potentially governed operations could include publishing a layer, changing an external reference, selecting a production variant, modifying physics properties, deploying a simulation, or handing a digital-twin decision to a physical execution system.

| Concern | Relevant AsiBackbone primitive | Potential support | Host-owned work still required | Explicit non-coverage |
| --- | --- | --- | --- | --- |
| Consequential scene operation | Operation identity, constraints, decision outcomes | **Host-configured support** through an external adapter | USD parsing, stage access, composition, validation, asset resolution | AsiBackbone does not parse USD or implement stage composition |
| Simulation-to-execution handoff | Acknowledgment, capability grants, host-owned execution boundary | **Host-configured support** for an accountable checkpoint | Simulation validation, robotics/industrial safety, controller integration | AsiBackbone does not control robots or physical systems |
| Scene-change evidence | Decision receipt, policy identity, actor, correlation, signing seams | **Evidence contribution only** for host-defined operations | Canonical stage manifest, dependency hashing, asset provenance, retention | Hashing one USD file does not identify a complete composed stage |

Any future OpenUSD integration should remain an external add-on package. `AsiBackbone.Core` must not depend on OpenUSD. AsiBackbone does not implement USDA, USDC, USDZ, an asset resolver, the OpenUSD composition algorithm, or the OpenUSD compliance framework, and it does not claim OpenUSD conformance.

Official source: [OpenUSD Core Specification 1.0 announcement](https://aousd.org/blog/foundations-of-open-3d-development-introducing-aousd-core-specification-1-0/)

## Possible future sector profiles

The crosswalk is intentionally not an exhaustive compliance catalog. More detailed profiles should be added only when a concrete consumer need exists and the mapping can be maintained from primary sources.

Potential follow-up profiles include:

- PCI DSS for payment-card environments;
- FedRAMP and NIST RMF profiles for United States federal cloud systems;
- CMMC for defense-contractor environments;
- SOC 2 trust-services evidence orientation;
- DORA for applicable European Union financial entities;
- sector-specific healthcare, finance, education, and public-sector overlays.

These references have different legal and assurance characteristics and should not be grouped together as regulations.

## Host-owned responsibilities across all mappings

The consuming organization remains responsible for:

- applicability analysis and legal interpretation;
- governance and risk-management program design;
- identity, authentication, authorization, and access control;
- policy authorship, approval, testing, and retention;
- model, system, privacy, safety, and impact assessments;
- infrastructure, encryption, key custody, networking, backups, and recovery;
- data inventory, classification, minimization, retention, deletion, and subject rights;
- human oversight design and authority;
- execution, monitoring, incident response, reporting, and remediation;
- internal audit, external assessment, certification, and regulator engagement.

AsiBackbone can contribute a disciplined application decision boundary and evidence trail inside those responsibilities. It does not replace them.

## Related documentation

- [Government and Regulated Systems Guidance](government-and-regulated-systems.md)
- [Project Boundaries and Non-Claims](project-boundaries.md)
- [Privacy and Signing Boundaries](privacy-and-signing-boundaries.md)
- [Governance Tool Comparisons](governance-tool-comparisons.md)
- [Host-Owned Execution Enforcement](host-owned-execution-enforcement.md)
- [Regulated Storage and Signing Verification Checklist](regulated-storage-and-signing-verification-checklist.md)
