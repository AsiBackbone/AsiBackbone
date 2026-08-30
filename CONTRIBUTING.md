# Contributing to AsiBackbone

Thank you for helping improve AsiBackbone.

In this software project, **ASI** means **Accountable Systems Infrastructure**. AsiBackbone is a .NET package family for governance-oriented decision flow. Contributions should keep the project grounded as practical governance infrastructure, not an artificial superintelligence implementation, AI model host, legal/compliance guarantee, or robot controller.

## Contribution goals

Contributions should preserve the stable package direction:

- keep `AsiBackbone.Core` framework-neutral;
- keep host ownership explicit;
- keep package boundaries clear;
- avoid hidden infrastructure assumptions;
- document public behavior before presenting it as stable;
- preserve the documented implicit-usings posture unless a project-specific change improves package-boundary reviewability;
- avoid overclaiming around signing, tamper-evidence, privacy, compliance, AI, or physical execution.

## Documentation ownership

Before adding or substantially expanding documentation, decide which repository owns the subject:

- Put organization-level concepts, architecture education, terminology lineage, tutorials, comparisons, tradeoffs, labs, and general governed-execution teaching in [AsiBackbone/Learning](https://github.com/AsiBackbone/Learning). The published educational site is [ASI Backbone Learning](https://asibackbone.github.io/Learning/).
- Keep package installation/configuration, public API/type behavior, compile-ready package examples, runtime semantics, integration boundaries, security/cryptographic implementation posture, provider operations, compatibility, releases, consumer verification, and maintainer evidence in this repository.
- When a page mixes both concerns, keep the concrete AsiBackbone implementation/API material here and link to Learning for the broader architectural explanation instead of maintaining duplicate educational narratives.

Learning is the educational source of truth; it does not override AsiBackbone package, API, configuration, or runtime behavior. See [Documentation Ownership](docs/articles/documentation-ownership.md) for the canonical matrix and routing rule.

### Documentation URL continuity and link guardrails

When educational authority moves from this repository to Learning:

- preserve the established AsiBackbone documentation URL when practical;
- replace the old page with a concise transition/product-boundary page rather than silently deleting it;
- name the canonical Learning destination explicitly;
- retain useful product/API/runtime links on the preserved page;
- link Learning back to concrete AsiBackbone implementation or API pages, not back to a transition page in a way that creates a redirect loop.

Critical cross-repository routes are tracked in `eng/docs/cross-repository-links.json`. After building DocFX, validate the transition pages, published Learning destinations, current product/API/release destinations, and selected Learning-to-AsiBackbone links with:

```powershell
./scripts/Validate-DocumentationLinks.ps1
```

If a release changes the current release-note or consumer-verification URL, update the link manifest in the same pull request.

## Local setup

Install:

- .NET SDK supported by the repository;
- Git;
- a code editor such as Visual Studio, Visual Studio Code, or JetBrains Rider.

Restore local tools after cloning:

```bash
dotnet tool restore
```

## Build and test locally

Run the standard solution checks before opening a pull request:

```bash
dotnet restore AsiBackbone.slnx -p:Configuration=Release
dotnet build AsiBackbone.slnx --configuration Release
dotnet test --solution AsiBackbone.slnx --configuration Release --no-build --no-restore
```

These commands intentionally use `Release`. The repository CI and stable release validation workflows also use the `Release` solution configuration for restore, build, test, formatting, package, documentation, and smoke-test validation. A `Debug` solution build is a local inner-loop or IDE convenience build only and is not evidence that the complete source/test surface was validated. The reviewed `Debug|*` solution exclusions and their rationale are documented in the [Developer Checklist](docs/articles/developer-checklist.md#solution-build-configurations).

When package-consumer behavior changes, also run the smoke tests when possible:

```bash
bash ./eng/smoke-tests/external-consumer-smoke.sh
bash ./eng/smoke-tests/stable-package-integration-smoke.sh
```

The smoke scripts pack local package artifacts, create temporary external consumer projects, install packages from the local NuGet output, and validate that package consumers can wire the implemented package family without repository project references.

## Testing expectations

Major new functionality and changes to existing behavior must include automated
tests covering the new or changed behavior.

Bug fixes should include a regression test when the failure can be reproduced
reliably. Changes affecting package-consumer behavior should also update or add
the applicable smoke tests.

When automated testing is genuinely impractical, the pull request must explain
why and describe the alternative validation performed.

## Documentation checks

Build the documentation site locally when documentation changes or public behavior changes:

```bash
dotnet tool restore
dotnet tool run docfx docs/docfx.json --serve
```

Documentation should clearly separate:

- implemented stable behavior;
- alpha, preview, or sample behavior;
- host responsibilities;
- future provider work;
- conceptual ASI Backbone inspiration;
- practical software claims.

Use cautious language. For example, say records are **signing-ready** only when built-in signing is not implemented. Do not claim tamper-evidence, legal compliance, or non-repudiation unless the implementation and documentation explicitly support those claims.

## Issue guidance

Before starting work:

- check whether an issue already exists;
- keep the issue scope small enough to review;
- describe the package boundary involved;
- identify whether the work affects Core, ASP.NET Core, EF Core, in-memory storage, docs, samples, tests, or release process;
- call out any stable API or serialized/persisted schema implications.

Good issues should include acceptance criteria or a clear expected outcome.

## Pull request guidance

Pull requests should include:

- a clear title;
- a short summary of what changed;
- testing performed, or a clear note when tests were not run;
- documentation updates when public behavior changes;
- issue closure text such as `Closes #123` when appropriate.

Prefer focused pull requests. Avoid mixing unrelated code, documentation, release, and formatting work unless the issue explicitly calls for it.

## Stable package family checklist

Before opening a pull request, consider the checklist in [Developer Checklist](docs/articles/developer-checklist.md).

At minimum, ask:

- Does this preserve Core's framework-neutral boundary?
- Does this preserve host ownership of infrastructure, persistence, routing, authentication, authorization, and execution?
- Does this change public APIs or documented behavior?
- Does this affect persisted or serialized records?
- Does this require schema-version guidance?
- Does this need package-consumer smoke coverage?
- Does this require documentation updates?
- Does the wording avoid overclaiming?

## Release-readiness reminders

For stable release work, confirm:

- package versions and release notes agree;
- public documentation links resolve;
- known limitations are documented;
- API compatibility expectations are clear;
- privacy and signing boundaries are clear;
- provider work is not implied to be stable unless explicitly released as stable;
- tests and smoke checks have been run or the PR explains why they were not run.

## Security and sensitive information

Do not include secrets, credentials, tokens, private keys, passwords, confidential data, or raw sensitive records in issues, pull requests, examples, tests, screenshots, or documentation.

Use synthetic examples and opaque identifiers in documentation and tests.

## Contributor License Agreement

By submitting a contribution (including but not limited to code, documentation, tests, or configuration files) to AsiBackbone via pull request, patch, or any other means, you ("Contributor") agree to the following terms:

1. **Grant of copyright license.** You grant Christopher D. Cavell, as current project steward for AsiBackbone, and all recipients of software distributed by the project a perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable copyright license to reproduce, prepare derivative works of, publicly display, publicly perform, sublicense, and distribute your contribution and such derivative works under the terms of the license identified in the project's LICENSE file.

2. **Grant of patent license.** You grant Christopher D. Cavell, as current project steward for AsiBackbone, and all recipients of software distributed by the project a perpetual, worldwide, non-exclusive, no-charge, royalty-free, irrevocable patent license to make, have made, use, offer to sell, sell, import, and otherwise transfer the project, where such license applies only to those patent claims licensable by you that are necessarily infringed by your contribution alone or by combination of your contribution with the project.

3. **Representations.** You represent that (a) you are legally entitled to grant the above licenses; (b) if your employer has rights to intellectual property you create, you have received permission to make this contribution on behalf of your employer; and (c) your contribution is your original creation or you have sufficient rights to submit it.

4. **No warranty.** Except as required by applicable law, you provide your contribution on an "AS IS" basis, without warranties or conditions of any kind, either express or implied.

If AsiBackbone is accepted into the .NET Foundation, contribution licensing may transition to the .NET Foundation's CLA and automated CLA bot process as part of onboarding.

Questions about this agreement may be directed to [cdcavell@gmail.com](mailto:cdcavell@gmail.com).
