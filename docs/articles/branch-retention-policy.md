# Branch Retention Policy

This article documents which branches the AsiBackbone repository keeps, which it
removes, and what must be verified before a branch is deleted. It governs
repository navigation and maintenance hygiene; it does not change any runtime
behavior in the `AsiBackbone.*` packages.

The governing principle is that **a branch is not release evidence**. Release
evidence is carried by the annotated tag and the published GitHub release for a
version, both of which are immutable and independently addressable. A release
branch that has been merged and tagged holds no information its tag does not, so
retaining it adds navigation noise without adding durability.

## Canonical desired state

The machine-readable policy is committed at
`eng/repository-controls/branch-retention-policy.json`. The live branch list is
expected to match it.

| Retention class | Membership | Posture |
| --- | --- | --- |
| Active | `main`, `issue_work` | Never removed by maintenance. `main` is the released trunk; `issue_work` is the long-lived integration branch every pull request is raised from. |
| Release evidence | Tags matching `v*` with a published release | Immutable and permanent. Carried by tags and releases, never by branches, so this class contains no branches. |
| Disposable | `release/*`, `dependabot/*`, `docs/*`, `issue-*`, `feature/*`, `fix/*` | Removed once merged into `main` or covered by a release tag. |

A branch that matches no class is a policy failure rather than an implicit
"keep". The audit reports it as `UNCLASSIFIED` and fails, so that retaining a
branch is always a recorded decision instead of an accident.

## Deletion preconditions

A disposable branch may be deleted only when at least one of the following holds,
and never otherwise:

1. The branch is fully reachable from `main` — GitHub reports it as `0` commits
   ahead — so nothing unique would be lost; or
2. The branch is fully reachable from a release tag, and that tag has a published
   release.

If a branch is ahead of `main` and no tag covers it, deleting it would discard
commits. The audit refuses to mark such a branch removable and fails with the
commit count, so unique work is resolved before any cleanup rather than
discovered missing afterward.

A tag without a published release is reported as a warning even when no branch
depends on it, because the release page is the human-readable half of the
evidence pair.

## Current state

As of the `5.0.0` release line, the repository holds only the two active
branches. The twenty-nine former `release/*` branches have been removed, and
every one of them is represented by a matching annotated tag with a published
release — a complete `29` tag to `29` release pairing, verified before removal.

## Auditing and applying the policy

Repository settings and the live branch list are not versioned by Git, so the
repository commits the desired state and the tool used to compare and apply it.

Authenticate GitHub CLI with an account that can administer the repository, then
run the read-only audit:

```powershell
gh auth status
./scripts/Manage-BranchRetention.ps1
```

The audit classifies every branch, verifies tag and release coverage for anything
it would remove, and checks the `delete_branch_on_merge` repository setting
against the policy. It exits non-zero when any check fails and mutates nothing.

Preview the mutations before applying them:

```powershell
./scripts/Manage-BranchRetention.ps1 -Apply -WhatIf
```

Apply the policy — enable automatic branch deletion on merge and prune the
branches the audit verified as removable:

```powershell
./scripts/Manage-BranchRetention.ps1 -Apply
```

The apply path deletes only branches that already passed the preconditions above.
A branch reported as `RETAIN` or `UNCLASSIFIED` is never deleted, including under
`-Apply`.

## Automatic deletion of merged branches

The policy sets `deleteBranchOnMerge` to `true`. With that repository setting
enabled, GitHub removes a work branch when its pull request is squash-merged, so
the ordinary case needs no maintenance step at all and the branch list stays
readable without periodic cleanup.

The audit reports the setting as a failure while it is disabled. Enabling it is a
repository-settings change rather than a source change, so it is applied through
GitHub or through `-Apply` above, not by merging this article.

## Maintenance step

Automatic deletion covers branches merged through a pull request. It does not
cover branches created and abandoned without a merge, or branches created by
automation that bypasses the pull-request path. Run the read-only audit as part
of [release validation](release-validation.md) to catch those:

```powershell
./scripts/Manage-BranchRetention.ps1
```

The offline decision logic is covered by fixtures under
`eng/test-fixtures/branch-retention` and exercised by
`./scripts/Test-BranchRetention.ps1`, which runs in CI. Those fixtures assert the
safety property directly: a branch that is ahead of `main` with no covering tag,
and a branch whose covering tag has no published release, are never reported as
removable.
