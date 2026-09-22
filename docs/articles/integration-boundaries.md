# Framework-Neutral Integration and Host-Owned Persistence

AsiBackbone is opt-in governance infrastructure. It is designed to fit inside an existing host application without becoming the parent application framework, routing model, database owner, migration owner, or deployment authority.

A consuming application can adopt AsiBackbone by wiring the packages it needs at the decision boundary where proposed intent becomes a governed decision. The host application still owns the web app, runtime composition, persistence lifecycle, operational controls, and final execution behavior.

> [!IMPORTANT]
> AsiBackbone does not require `NetCoreApplicationTemplate`, does not replace the host application architecture, and does not take over persistence. It contributes governance decision primitives, integration seams, and optional persistence shapes that the host chooses how to use.

## Framework-neutral by design

The Core package is intentionally independent of ASP.NET Core, Entity Framework Core, `NetCoreApplicationTemplate`, UI frameworks, authentication providers, database providers, agent frameworks, and robotics or infrastructure execution systems.

That means the same Core language can be used from different kinds of hosts:

- Plain ASP.NET Core applications.
- Enterprise ASP.NET Core applications with existing startup conventions.
- Worker services.
- Console validation hosts.
- Existing applications with their own authentication and authorization model.
- Optional external validation hosts such as `NetCoreApplicationTemplate`.

Integration packages may add host-specific conveniences, but the host still decides whether those packages are appropriate.

## Opt-in infrastructure, not a parent framework

AsiBackbone should be added as a module inside a host-owned architecture.

It does not require the host to adopt:

- a specific application template;
- a specific routing style;
- a specific controller or Minimal API pattern;
- a specific database provider;
- a specific migration strategy;
- a specific deployment model;
- a specific authentication provider;
- a specific logging stack;
- a specific user interface.

The host integrates AsiBackbone where governance is needed and leaves the rest of the architecture intact.

## Ownership comparison

| Concern | AsiBackbone role | Host application role |
| --- | --- | --- |
| Intent evaluation | Provides decision and policy primitives. | Builds the policy context and chooses when to evaluate. |
| Constraint evaluation | Provides abstractions and decision outcome language. | Defines and registers domain-specific constraints. |
| Acknowledgment flow | Provides acknowledgment and audit primitives. | Chooses UI, UX, authorization, and workflow behavior. |
| Decision receipt | Provides structured audit models and sinks/stores. | Chooses durability, retention, access controls, and operational review. |
| Capability grants | Provides scoped grant concepts and package primitives where available. | Chooses when a grant is trusted and how execution is performed. |
| Web application structure | Does not own routing, controllers, middleware, or UI. | Owns the host application shape and request pipeline. |
| Persistence lifecycle | Contributes persistence shape/configuration where EF Core is used. | Owns `DbContext`, provider, connection strings, migrations, deployment, and operations. |
| Execution | Does not execute external, infrastructure, AI, robotics, or physical-control actions. | Owns whether and how an allowed decision becomes real execution. |

## Host-owned persistence model

When using `AsiBackbone.EntityFrameworkCore`, the host application remains the persistence owner.

The host owns:

- `DbContext` lifecycle;
- database provider selection;
- connection strings;
- migrations;
- migration assembly and naming conventions;
- schema deployment;
- backup, retention, encryption, archival, and purge policy;
- operational configuration and monitoring.

AsiBackbone contributes governance persistence shape and configuration. It should not force a package-owned `DbContext`, a specific provider, or required package-owned migrations.

A typical EF Core boundary looks like this:

```text
Host DbContext
  -> applies AsiBackbone model configurations
  -> owns provider and connection string
  -> owns migrations and deployment
  -> persists governance records as part of host-owned infrastructure
```

The host can then decide whether governance records live in the primary application database, a separate audit database, or another provider-specific strategy supported by the consuming application.

## What the EF Core package contributes

The EF Core package contributes model configuration and storage helpers for AsiBackbone governance records. It can add persistence shape for records such as audit ledger entries, reason codes, metadata, handshake requests, and acknowledgments.

This is different from owning the host database. The package contributes the model pieces; the host decides how those pieces are deployed and operated.

## Adoption without rewriting architecture

A team can adopt AsiBackbone incrementally:

1. Keep the existing host application structure.
2. Add only the needed AsiBackbone packages.
3. Identify one consequential action that needs a governance boundary.
4. Build a policy context from existing host data.
5. Register host-defined constraints.
6. Evaluate the decision before execution.
7. Persist decision receipt using the host-owned persistence plan.
8. Keep actual execution in the host application.

This lets enterprise teams test the governance pattern without converting their application into a new template or reorganizing their entire persistence stack.

## Plain ASP.NET Core sample versus NetCoreApplicationTemplate validation

The [Plain ASP.NET Core Host Sample](plain-aspnetcore-host-sample.md) is the canonical in-repository validation path. It proves that AsiBackbone can integrate with a standard ASP.NET Core host without requiring any external application template.

`NetCoreApplicationTemplate` is optional. The [NetCoreApplicationTemplate Host Validation](netcoreapplicationtemplate-host-validation.md) article documents a separate external validation path for developers who want to test AsiBackbone against a fuller enterprise-style application baseline.

The relationship is intentionally one-way:

```text
Plain ASP.NET Core sample
  -> canonical in-repository validation baseline

NetCoreApplicationTemplate host
  -> optional external validation consumer

AsiBackbone source projects
  -> do not depend on NetCoreApplicationTemplate
```

This keeps AsiBackbone reusable for existing applications while still allowing richer external validation where helpful.

## Decision boundary versus execution boundary

AsiBackbone should normally live at the decision boundary, not the execution boundary.

```text
Proposed action
  -> AsiBackbone policy decision
  -> optional acknowledgment
  -> decision receipt
  -> optional capability grant
  -> host-owned execution path
```

The package can help determine whether an action should be allowed, denied, deferred, acknowledged, or escalated. The host decides what happens next.

That separation is important for enterprise adoption because different systems have different execution controls, deployment rules, operational approvals, and compliance programs.

## Practical checklist for adopters

Before integrating AsiBackbone, decide:

1. Which host application owns the integration.
2. Which action or workflow needs governance first.
3. Which package set is needed: Core only, ASP.NET Core integration, in-memory validation, or EF Core persistence.
4. Which `DbContext`, provider, and migration assembly own persistence if EF Core is used.
5. Which host authorization policy protects the acknowledgment or execution path.
6. Which audit records should be retained and for how long.
7. Which execution path remains outside AsiBackbone and under host control.

## Related documentation

- [Plain ASP.NET Core Host Sample](plain-aspnetcore-host-sample.md)
- [NetCoreApplicationTemplate Host Validation](netcoreapplicationtemplate-host-validation.md)
- [EF Core Host Ownership and Migration Guidance](ef-core-host-ownership-and-migrations.md)
- [EF Core Integration Boundary](ef-core-integration-boundary.md)
- [ASP.NET Core Integration Boundary](aspnetcore-integration-boundary.md)
- [Policy Evaluator Pipeline](policy-evaluator-pipeline.md)
