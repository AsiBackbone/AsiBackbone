# Adoption and Target Use Cases

General governed-execution scenario teaching and role-based learning belong in [ASI Backbone Learning](https://asibackbone.github.io/Learning/). This page stays product-focused: where the current AsiBackbone packages fit, and where they do not.

> [!IMPORTANT]
> AsiBackbone does not execute AI, robotics, infrastructure, physical-control, or external-system commands by itself. The host remains responsible for authentication, authorization, execution, persistence, operational safeguards, and compliance review.

## Good product fit

AsiBackbone is a reasonable candidate when a .NET host needs several of these concrete capabilities together:

- explicit governance outcomes before execution;
- policy and reason metadata that can be preserved;
- acknowledgment as a separate workflow record;
- durable audit/outbox evidence;
- short-lived scoped continuation authority;
- host-owned execution that can independently refuse the side effect.

## Poor product fit

Prefer a simpler design when:

- ordinary framework authorization is sufficient;
- the action is low risk and immediate;
- no policy provenance or acknowledgment is needed;
- no delayed/delegated authority crosses a boundary;
- ordinary logs already provide the required evidence.

For the general comparison, see [When ASP.NET Core Authorization Is Enough](https://asibackbone.github.io/Learning/architecture/when-aspnet-core-authorization-is-enough.html) and [When a Simple Application Service Is Enough](https://asibackbone.github.io/Learning/architecture/when-a-simple-application-service-is-enough.html).

## Current implementation scenarios

The following scenarios are documented against current product boundaries:

- [AI Agent Gateway](scenarios/ai-agent-gateway.md)
- [Human Approval Before AI Tool Execution](scenarios/human-approval-before-ai-tool-execution.md)
- [High-Risk Administrative Action](scenarios/high-risk-administrative-action.md)
- [Sensitive Data Access Request](scenarios/sensitive-data-access-request.md)
- [Deployment or Infrastructure Change Gate](scenarios/deployment-or-infrastructure-change-gate.md)
- [Robotics Operational Gateway](scenarios/robotics-operational-gateway.md)

These are examples of host integration. They do not transfer execution ownership to AsiBackbone.

## Host obligations in every scenario

The host remains responsible for:

- authoritative actor/resource/tenant/region context;
- identity and authorization;
- policy content and legal interpretation;
- persistence configuration and retention;
- execution credentials and secrets;
- idempotency/replay behavior;
- operational safety;
- error handling and recovery;
- compliance and security program controls.

## Practical first adoption

1. Choose one consequential host-owned action.
2. Build a current policy context.
3. Add one or two meaningful constraints.
4. Evaluate an explicit decision.
5. Persist decision residue.
6. Add acknowledgment only where the policy requires it.
7. Add scoped capability only when delayed/delegated execution justifies it.
8. Keep the final side effect in the host.

## Learn the broader scenarios

Use [ASI Backbone Learning](https://asibackbone.github.io/Learning/) for architecture tutorials, case studies, labs, comparisons, and adoption personas.
