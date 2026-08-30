# AsiBackbone Package Samples

The projects in this directory are **compile-ready implementation samples for shipped AsiBackbone packages**.

General architecture tutorials, exercises, labs, alternative patterns, and vendor-neutral executable teaching samples live in [AsiBackbone/Learning](https://github.com/AsiBackbone/Learning). Browse the published [Learning executable sample catalog](https://asibackbone.github.io/Learning/samples/) when the goal is to learn the pattern without depending on product packages.

## Ownership rule

A project belongs in this directory when its primary purpose is to demonstrate concrete AsiBackbone package/API behavior. If the example would still serve its purpose after removing all AsiBackbone package references, it belongs in Learning instead.

## Current samples

| Project | Product behavior demonstrated | Documentation |
| --- | --- | --- |
| `PlainAspNetCoreHost` | Standard ASP.NET Core registration, governance evaluation, endpoint integration, audit behavior, and host-owned execution boundaries. | [Plain ASP.NET Core Host Sample](../docs/articles/plain-aspnetcore-host-sample.md) |
| `AsiBackboneAspireAppHost` | Optional .NET Aspire orchestration around AsiBackbone sample services without making Aspire a package-family dependency. | [Aspire AppHost Sample](../docs/articles/aspire-apphost-sample.md) |
| `NcatAuditCompletionAdapter` | Concrete adapter behavior around AsiBackbone audit-completion/lifecycle integration. | [NCAT Audit Completion Adapter](../docs/articles/ncat-audit-completion-adapter.md) |

## Product reference

Use the published [Generated API Reference](https://asibackbone.github.io/AsiBackbone/api/) for exact public types and members.

The implementation documentation defines package/runtime behavior. Learning explains the broader architectural patterns and alternatives.

## Validation expectation

These projects remain part of repository build/sample validation. Changes should keep them compile-ready against the repository's current package version and avoid adding teaching-only abstractions that do not exercise the product.
