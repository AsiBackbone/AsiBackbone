# Why AsiBackbone? Product Fit

The general rationale for governed execution is canonical in [ASI Backbone Learning](https://asibackbone.github.io/Learning/architecture/accountable-systems-infrastructure-and-governed-execution.html).

This URL is retained for continuity. For product adoption, the question is narrower: **does a .NET host need an explicit, auditable decision boundary before consequential execution?**

## Consider AsiBackbone when

The host needs one or more of the following:

- policy evaluation that produces explicit decision outcomes;
- structured reason codes and policy identity;
- acknowledgment before selected consequential actions;
- durable decision/audit residue;
- short-lived scoped capability grants;
- a host-owned execution boundary that can still refuse execution.

## Prefer a simpler design when

AsiBackbone may be unnecessary when ordinary authorization or a simple application service already expresses the complete risk boundary and no separate acknowledgment, provenance, delayed execution, or delegated authority is required.

For stack-neutral comparison guidance, see:

- [When ASP.NET Core Authorization Is Enough](https://asibackbone.github.io/Learning/architecture/when-aspnet-core-authorization-is-enough.html)
- [When a Simple Application Service Is Enough](https://asibackbone.github.io/Learning/architecture/when-a-simple-application-service-is-enough.html)

## Start with the product

- [Implementation-First Adoption Path](implementation-first-adoption.md)
- [First 15 Minutes: Standard API Gating](quickstart-api-gating.md)
- [Getting Started](getting-started.md)
- [Adoption and Target Use Cases](use-cases.md)
- [Generated API Reference](../api-reference.md)

Learning explains the broader architecture. AsiBackbone documentation defines what the shipped packages actually do.
