# Package Discovery Metadata Review

| Field | Value |
| --- | --- |
| Review date | 2026-11-03 |
| Scope | GitHub repository description and topics, and the `Description` and `PackageTags` of every published `AsiBackbone.*` package, reviewed together as one discovery surface |
| Baseline | Published `7.5.2` packages on nuget.org and `main` at the time of review |
| Outcome | One evidence-based change, to `AsiBackbone.Core`; every other surface retained |
| Mechanical guard | `scripts/Validate-NuGetPackageMetadata.ps1` required discovery vocabulary |
| Origin | [AsiBackbone/AsiBackbone#924](https://github.com/AsiBackbone/AsiBackbone/issues/924) |

## Why review these together

People find the package family in three ways: GitHub topic and repository search, NuGet search over package IDs, descriptions, and tags, and links from the documentation site. The repository description and topics already carry the search terms an evaluator is likely to use. Package metadata matters most when someone searches NuGet directly, where `AsiBackbone.Core` is the package they should land on first.

This review deliberately did not rewrite the product vocabulary. "Accountable Systems Infrastructure" and the package-specific terms stay in place. The question was narrower: is there a term evaluators search for that the repository surface uses, but that no package carries?

## Repository surface

**Description:** "A .NET governance and policy-control framework for auditable AI and enterprise decision flows: policy-as-code, capability grants, acknowledgment workflows, and host-owned execution."

**Topics:** `ai-governance`, `aspnetcore`, `audit-logging`, `authorization`, `csharp`, `dotnet`, `enterprise-architecture`, `governance`, `middleware`, `nuget`, `policy-as-code`, `policy-engine`, `responsible-ai`, `security`, `governed-execution`, `decision-engine`, `auditability`, `access-control`, `capability-grants`, `capability-tokens`.

**Decision:** retain. The description leads with the problem in plain terms, and the topics cover framework, problem, and product vocabulary without repeating any one term. GitHub repository settings are maintained outside this repository, so this record documents them rather than changing them.

## Package surface

| Package | Search-intent vocabulary already present | Decision |
| --- | --- | --- |
| `AsiBackbone.Core` | `.NET`, `governance`, `decision-flow`, `constraint-evaluation`, `audit`, `acknowledgment`, `capability-grant`, `capability-token` | **Changed** (see below) |
| `AsiBackbone.DependencyInjection` | `dependency-injection`, `builder`, `configuration` | Retain |
| `AsiBackbone.AspNetCore` | "ASP.NET Core" in the description; `aspnetcore`, `http`, `acknowledgment` | Retain |
| `AsiBackbone.EntityFrameworkCore` | "Entity Framework Core" in the description; `entity-framework-core`, `efcore`, `audit-ledger` | Retain |
| `AsiBackbone.Storage.InMemory` | `in-memory`, `storage`, `testing` | Retain |
| `AsiBackbone.Testing` | `testing`, `test-harness`, `aspnetcore`, `policy-evaluation`, `audit` | Retain |
| `AsiBackbone.Templates` | "dotnet new" and "ASP.NET Core" in the description; `templates`, `dotnet-new`, `webapi` | Retain |
| `AsiBackbone.Analyzers` | "Roslyn analyzer" in the description; `roslyn`, `analyzer`, `safety-rails` | Retain |
| `AsiBackbone.OpenTelemetry` | "OpenTelemetry" in the description; `opentelemetry`, `observability` | Retain |
| `AsiBackbone.Signing.LocalDevelopment` | `signing`, `local-development` | Retain |
| `AsiBackbone.Signing.ManagedKey` | `signing`, `managed-key`, `key-management` | Retain |

Each package keeps tags that describe its own role. The review did not copy one tag set across the family.

## Evidence for the `AsiBackbone.Core` change

- **Gap:** `policy-as-code` is a repository topic and appears in the repository description, but no package carries it. Core, the package that owns policy evaluation, didn't mention "policy" in its description or tags at all.
- **NuGet search, 2026-11-03:** a search for `policy-as-code` and a `tags:policy-as-code` search each returned no `AsiBackbone.*` package in the top 20 results. Searches for `policy evaluation` and `capability grant` also returned none.
- **Published drift:** the published `7.5.2` Core package carries `capability-token` but not `capability-grant`. The source tag was added after `7.5.2` by the 7.0-vocabulary terminology alignment ([#918](https://github.com/AsiBackbone/AsiBackbone/pull/918)), so it first ships with the next release.

**Change:**

- Description, from "Framework-neutral Accountable Systems Infrastructure governance primitives for consequential .NET decision flow." to "Framework-neutral Accountable Systems Infrastructure governance primitives for policy evaluation, decision receipts, and capability grants in consequential .NET decision flow."
- Tags: add `policy-as-code`.

Both changes take effect when the next release is packed. The published `7.5.2` packages are unchanged.

**Not changed, and why:**

- `authorization`, `access-control`, `policy-engine`, and `ai-governance` are not added to package tags. They describe the repository's problem space, but on a package they could suggest that Core replaces framework authorization or is an AI component. [Project Boundaries and Non-Claims](project-boundaries.md) rules out both readings.
- No search-ranking outcome is assumed. NuGet ranking also weighs downloads and other signals, so this change removes a vocabulary gap; it does not promise better placement.

## Mechanical guard

`scripts/Validate-NuGetPackageMetadata.ps1` already requires each generated package's description and tags to match a reviewed list exactly. That catches accidental drift between the project file and the reviewed list, but it does not stop both from being edited together during release work.

The script now also has a required discovery vocabulary:

- **Tags:** `governance` on every package; `policy-as-code`, `audit`, and `capability-grant` on Core; `aspnetcore` on AspNetCore, Testing, and Templates; `entity-framework-core` and `efcore` on EntityFrameworkCore; `opentelemetry` on OpenTelemetry; `roslyn` and `analyzer` on Analyzers; and `policy-evaluation` on Testing.
- **Description terms:** "policy evaluation", "decision receipts", "capability grants", and ".NET" for Core; "ASP.NET Core" for AspNetCore; "Entity Framework Core" for EntityFrameworkCore; "OpenTelemetry" for OpenTelemetry; "Roslyn analyzer" for Analyzers; and "dotnet new" and "ASP.NET Core" for Templates.

Removing any of these fails validation and names this record. To change the vocabulary on purpose, update this record and the list in the script together.

## Next review

Review again when a package is added, renamed, or split, or when a release changes a package's role. Record new NuGet search observations with their date; don't treat download counts or stars as the outcome.
