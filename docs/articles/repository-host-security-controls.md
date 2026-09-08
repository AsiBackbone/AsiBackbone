# Repository Host Security Controls

This article documents the GitHub-host controls used to protect the AsiBackbone
source and release path. These controls protect repository changes and supply-chain
workflow entry points; they do not add runtime security behavior to the
`AsiBackbone.*` packages.

The repository currently operates under the bootstrap solo-maintainer model
documented in `GOVERNANCE.md` and `.github/CODEOWNERS`. The control set therefore
avoids pretending that self-review is independent review while still requiring a
pull-request trail, release-blocking CI, and an explicit emergency-bypass path.

## Canonical desired state

The machine-readable main-branch ruleset is committed at
`eng/repository-controls/main-branch-ruleset.json`. The live GitHub configuration
is expected to match it.

| Control | Selected posture | Rationale |
| --- | --- | --- |
| Secret scanning | Enabled | Detect supported credentials committed to repository history. |
| Secret-scanning push protection | **Required and enabled** | Block supported secrets before they land in the repository. A real secret should be removed/revoked rather than bypassed. |
| Non-provider patterns | Enable when the repository/plan exposes the control | Generic private keys and credential-bearing connection strings are relevant to a public .NET repository. Unsupported availability is reported as a warning, not treated as a reason to disable provider push protection. |
| Validity checks | Enable when the repository/plan exposes the control | Validity information improves alert triage for supported provider tokens. GitHub may contact the issuing service to determine validity, so this is kept as an explicit recorded decision. |
| Main-branch ruleset | Active | Makes the effective control set reviewable instead of relying only on legacy branch-protection defaults. |
| Pull request before merge | Required | All ordinary and emergency changes retain a PR/audit trail. |
| Required approvals | `0` during bootstrap solo-maintainer operation | GitHub does not permit an author to supply independent approval of their own PR. Requiring one approval with one active maintainer would create a permanent self-lock or force routine bypass. |
| Code Owner approval | Not required during bootstrap solo-maintainer operation | `.github/CODEOWNERS` still records ownership and review routing. Re-enable required Code Owner review when a second active maintainer can provide independent approval. |
| Last-push approval | Not required during bootstrap solo-maintainer operation | This control also requires a second person. Revisit with the review-count decision. |
| Review-thread resolution | Required | Blocking review conversations must be resolved before ordinary merge. |
| Merge method | Squash only | Keeps `main` linear and ensures normal GitHub merges create one reviewable merge result. |
| Linear history | Required | Prevents merge commits from being pushed to `main`. |
| Force push and deletion | Blocked | Protects stable history from destructive updates. |
| Required signed commits | Deferred | GitHub evaluates commits introduced by a PR; unsigned local feature-branch commits can block squash merge even when GitHub would sign the final squash commit. The current local/Visual Studio workflow is not yet consistently signed. See the compensating controls below. |
| Emergency bypass | `@cdcavell` only, pull-request mode only | Replaces a broad implicit administrator exemption with one repository-specific, auditable bypass actor that still must use a PR. |

### Required status checks

The ruleset carries forward the existing release-blocking GitHub Actions checks:

- `Dependency review`
- `Build, test, and pack`
- `CodeQL analysis`
- `Validate version consistency`
- `External consumer package smoke test`
- `Restore, build, test, docs, pack, and smoke`

The checks are bound to the GitHub Actions integration and use strict status-check
policy so the pull request must be validated against the current target branch.

## Signed-commit decision and compensating controls

Required commit signatures are intentionally not enabled by this issue. GitHub's
signed-commit rule evaluates commits introduced by the pull request, not only the
final squash commit. Enabling it before the maintainer's local commits and
automation identities are consistently signed would turn normal pull requests
into bypass-only merges, weakening rather than strengthening the intended model.

Until local signing is adopted and verified end to end, the compensating controls
are:

- every ordinary change reaches `main` through a pull request;
- only squash merge is allowed by the ruleset;
- all six release-blocking checks must pass unless the documented emergency
  bypass is explicitly used;
- force pushes and deletion are blocked;
- the emergency bypass is limited to one user and to pull requests only;
- GitHub's merge result and the ruleset-bypass event remain visible in repository
  history/Rule Insights.

Revisit `required_signatures` when local GPG/SSH signing and relevant automation
identities have been exercised successfully on a non-default branch. Do not enable
it only to make the settings page appear stricter if the practical result is that
every normal PR must be bypassed.

## Applying or auditing the controls

Repository settings are not versioned by Git, so the patch commits the desired
state and the tool used to compare/apply it. The live setting must still be
changed through GitHub.

Authenticate GitHub CLI with an account that has repository Administration
permission, then run the read-only audit:

```powershell
gh auth status
./scripts/Manage-RepositorySecurityControls.ps1
```

Preview mutations before applying them:

```powershell
./scripts/Manage-RepositorySecurityControls.ps1 -Apply -WhatIf
```

Apply the repository security settings and create/update the canonical ruleset:

```powershell
./scripts/Manage-RepositorySecurityControls.ps1 -Apply
```

The apply path enables secret scanning and push protection first. It then attempts
to enable non-provider patterns and validity checks independently; if GitHub does
not expose one of those optional controls for the repository/plan, the script
warns without weakening mandatory push protection. Finally it creates or updates
the named main-branch ruleset and reruns the audit.

The legacy branch-protection rule may remain in place as defense in depth while
the ruleset is active. Its administrator exemption is not the canonical bypass
mechanism: ordinary administrators are still constrained by the active ruleset,
and the ruleset contains only the explicit pull-request-only bypass actor above.

## Emergency bypass procedure

Bypass is for an urgent failure of the repository control plane, not a shortcut
around an inconvenient test.

1. Open or retain a pull request. Direct push to `main` is not the emergency path.
2. Record which rule/check is being bypassed and why waiting for repair is more
   dangerous than merging the reviewed change.
3. Keep the change as narrow as possible and use squash merge.
4. Never use ruleset bypass as a substitute for secret-scanning push-protection
   remediation. Remove the secret, revoke/rotate it when appropriate, and retry.
5. After the emergency merge, repair the failing control and rerun
   `Manage-RepositorySecurityControls.ps1`.
6. Preserve the PR and Rule Insights/bypass event as the audit trail.

If a second active Core Maintainer is appointed, the first repository-control
change should reevaluate the bypass list, required approval count, Code Owner
review, and last-push approval before the bootstrap exception is retired.

## Automation compatibility check

The ruleset targets `refs/heads/main`; release tags are not targeted. Package
publishing, provenance/attestation, and release workflows triggered by version
tags therefore keep their existing tag path. Normal pull requests continue to
run the same six required checks already used by branch protection.

After first applying or materially changing the live controls:

1. run the read-only repository-control audit;
2. open a normal PR and confirm all required checks are present;
3. confirm the PR can merge normally by squash after checks pass;
4. confirm the resulting `main` commit is recorded as expected;
5. run/observe Stable Release Validation and the external consumer smoke workflow;
6. on the next planned release, verify the tag-triggered package/release path
   still runs without requiring a branch-ruleset bypass.

GitHub documents repository rulesets, pull-request-only bypass actors, signed
commit behavior, and secret-scanning push protection in its repository and code
security documentation. The committed manifest is the AsiBackbone-specific
decision record; GitHub documentation remains authoritative for platform
semantics.
