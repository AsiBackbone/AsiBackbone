# Five-Minute Evaluator

A single-file .NET app that shows the AsiBackbone decision boundary with one package and no host infrastructure.

```bash
dotnet run samples/FiveMinuteEvaluator/evaluate.cs
```

Run it from the repository root with .NET SDK 10.0.400 or a later 10.0.4xx patch. It restores the published `AsiBackbone.Core` package pinned at the top of `evaluate.cs`.

## What it demonstrates

- `DefaultGovernancePolicyEvaluator` built with one host-owned `IGovernanceConstraint`.
- The same `orders.approve` request evaluated for an allowed region and a blocked region.
- A `DecisionReceipt` created for both outcomes, including the blocked one.
- Host-owned execution: the host's own code runs only when `decision.CanProceed` is true. The final line reports `Host executions: 1 of 2`.

## What it intentionally leaves out

It has no ASP.NET Core host, endpoint governance, acknowledgment flow, capability grants, durable audit storage, or signing. It is an evaluation slice, not an adoption template. For a compile-ready API walkthrough, use [First 15 Minutes: Standard API Gating](../../docs/articles/quickstart-api-gating.md). For a fuller in-repository host, see the [Plain ASP.NET Core Host Sample](../../docs/articles/plain-aspnetcore-host-sample.md).

## Validation

- `scripts/Validate-VersionConsistency.ps1` fails when the `#:package AsiBackbone.Core@...` pin differs from the repository release version.
- `eng/smoke-tests/external-consumer-smoke.sh` runs the file against locally packed packages and checks that exactly one of the two requests reaches host code.
