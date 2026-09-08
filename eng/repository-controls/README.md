# Repository controls

This directory records the repository-specific desired state for GitHub-host
security controls that cannot be enforced by the .NET build itself.

`main-branch-ruleset.json` is the canonical repository ruleset payload for the
`main` branch. Apply/audit it with:

```powershell
./scripts/Manage-RepositorySecurityControls.ps1
./scripts/Manage-RepositorySecurityControls.ps1 -Apply -WhatIf
./scripts/Manage-RepositorySecurityControls.ps1 -Apply
```

The script requires an authenticated `gh` session. Mutation requires repository
Administration permission.

## Selected posture

- secret scanning: enabled;
- secret-scanning push protection: enabled and mandatory;
- non-provider secret patterns: enabled when GitHub exposes the feature for this
  repository/plan;
- secret validity checks: enabled when GitHub exposes the feature for this
  repository/plan;
- main changes: pull request required;
- merge method: squash only;
- linear history: required;
- force pushes and deletion: blocked;
- six existing release-blocking GitHub Actions checks: required and strict;
- required review-thread resolution: enabled;
- required approvals / Code Owner approval / last-push approval: disabled while
  bootstrap solo-maintainer governance applies;
- required signed commits: deferred until normal local and automation commits are
  consistently signed and can pass GitHub's PR signature evaluation without
  turning routine merges into bypasses;
- bypass: only GitHub user `@cdcavell` (user id `28095137`), and only through a
  pull request.

The explicit user bypass is intentionally narrower than a repository-role or
organization-administrator exemption. `pull_request` bypass mode preserves the
PR and ruleset-bypass audit trail and does not grant an emergency direct-push
path to `main`.

## Required checks captured by the manifest

The current branch-protection rule requires these GitHub Actions checks, and the
ruleset deliberately carries the same set forward:

- `Dependency review`
- `Build, test, and pack`
- `CodeQL analysis`
- `Validate version consistency`
- `External consumer package smoke test`
- `Restore, build, test, docs, pack, and smoke`

The GitHub Actions integration id currently associated with those required checks
is `15368`. If a check is renamed, added, removed, or moves to a different trusted
integration, update the manifest in the same PR as the workflow/protection change.

## Review rule changes like code

Changing this manifest changes the expected repository trust boundary. Review
the diff for bypass actors, target refs, required checks, merge methods, and PR
requirements before applying it. Do not use `-Apply` merely to overwrite an
unexpected live configuration without first determining why it drifted.

The rationale, optional-feature decision, signed-commit deferral, and emergency
bypass procedure are documented in
`docs/articles/repository-host-security-controls.md`.
