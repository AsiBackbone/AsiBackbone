# Repository assurance agents

## Purpose and boundary

AsiBackbone provides four specialized, read-only custom-agent profiles for
maintainer-requested review:

The profiles follow GitHub's
[custom agents configuration](https://docs.github.com/en/copilot/reference/custom-agents-configuration)
for repository-level files under `.github/agents/`.

| Profile | Primary responsibility |
| --- | --- |
| Security Assurance | Fail-closed behavior, authorization, capability grants, receipts, signing and supply-chain controls. |
| Architecture Assurance | Package boundaries, host ownership, public contracts and governance-flow separation. |
| Quality Assurance | Negative paths, concurrency, cancellation, distributed behavior and regression evidence. |
| Release Assurance | Compatibility, versioning, packages, provenance, documentation and release evidence. |

These profiles review evidence and recommend action. They do not implement
changes, execute validation, approve work or authorize a merge or release. They
do not provide independent separation of duties: instructions and personas are
not security boundaries, and the maintainer remains accountable for decisions.

No MCP server, external credential or secret is configured by these profiles.
SOFA research and contribution governance remain owned by AsiBackbone Learning
and are not duplicated here.

## Invocation and order

After the profiles are present on the selected repository branch, choose a
profile from the GitHub Copilot custom-agent selector and start it manually.
Automatic model invocation is disabled in every profile.

Provide a bounded task with the issue or change scope, affected paths and the
question the review must answer. Do not provide credentials, private reports or
sensitive operational data. For a security-sensitive change, request reviews in
this order:

1. Security Assurance.
2. Architecture Assurance.
3. Quality Assurance.
4. Release Assurance when compatibility or release posture is affected.

Later reviewers should receive the change under review and any sanitized,
maintainer-approved findings that affect their scope. A finding from one profile
does not instruct another profile to act; the maintainer decides the handoff.

Example safe prompts:

```text
Review the changes for issue #123. Inspect only src/AsiBackbone.Core,
tests/AsiBackbone.Core.Tests and the related documentation. Report findings
with file/line evidence. Do not modify files or claim tests were run.
```

```text
Review this release-preparation diff for compatibility and missing evidence.
Report blockers and deferred checks only; do not approve or publish a release.
```

## Required review record

Each review must include:

- the files, diff or commit scope inspected;
- findings ordered by severity;
- exact file/line evidence and practical impact;
- an actionable recommendation for every finding;
- assumptions, uncertainties and evidence gaps;
- validation performed and validation not performed; and
- any required human or cross-repository handoff.

If no finding is supported, the review must say so without claiming the change
is secure, correct, compliant or release-ready. Reviews are advisory evidence,
not approvals.

## Prohibited-action handling

The profiles declare only the `read` and `search` tool aliases. They must refuse
requests to edit files, execute commands, retrieve secrets, call external
services, change settings, approve their own work, merge, publish, deploy or
release. A request that requires one of those actions must be returned to a
human maintainer with the reason and the evidence still needed.

Repository or organization MCP settings and runtime permissions can change the
effective environment. Before relying on a review, confirm the actual session's
tool list and repository access. Never treat the profile text as proof that an
untrusted runtime cannot act outside the declared role.

## Validation and limitations

Run the structural validation from the repository root:

```powershell
./scripts/Validate-AgentGovernance.ps1 -SelfTest
./scripts/Validate-AgentGovernance.ps1
```

The fixture-based self-test proves the validator rejects missing profiles,
malformed frontmatter, expanded tools, MCP configuration, missing repository
guidance, and missing assurance-guide requirements. The live validator checks
that all four profiles are present, manually selected,
limited to read/search, free of embedded MCP configuration, explicit about
prohibited actions, and consistent about evidence-oriented output. It also
checks the root guidance and representative safe/prohibited review cases. CI
runs the same check.

This is configuration validation, not a live Copilot permission test. GitHub
custom agents and their runtime tool mappings can change, and no live custom
agent session is required by normal CI. On first use, the maintainer must verify
profile discoverability, the displayed tools and refusal/escalation behavior
with non-sensitive repository content. Record any product limitation or
unexpected tool exposure before relying on the agent review.

The profiles cannot run the repository's build, tests or release workflows.
Their output must therefore distinguish inspected configuration from validation
actually executed by a maintainer or CI.
