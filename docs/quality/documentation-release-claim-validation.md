# Documentation Release-Claim Validation

AsiBackbone validates current documentation against the release identity declared in `Directory.Build.props`.

The check exists to prevent evergreen pages from continuing to describe an older package line as current, stable, active, or canonical after the repository advances to a new release line. It also keeps a version that is prepared on `main` but not yet tagged or published distinct from the latest published release, so NuGet consumers who land on `main` are not told that unpublished APIs are the current release. It does not reject ordinary historical version references, migration guidance, compatibility comparisons, or version-specific release records.

## Validation command

Run the repository validation from the repository root:

```powershell
./scripts/Validate-DocumentationReleaseClaims.ps1
```

Validate a release tag before or after creating it with:

```powershell
./scripts/Validate-DocumentationReleaseClaims.ps1 -ReleaseTag v7.0.0
```

Run the deterministic fixture suite with:

```powershell
./scripts/Test-DocumentationReleaseClaims.ps1
```

Both commands run in the **Version Consistency** workflow. The repository validator also runs in the **Stable Release Validation** and **Publish AsiBackbone Packages** workflows. On a `v*.*.*` tag, all three workflows pass the tag name as `-ReleaseTag`, so a tagged commit cannot pass release validation or be packed while its documentation publication state contradicts the tag.

**Publish AsiBackbone Packages** publishes only from a tag ref. A manual dispatch from a branch must keep `skip_publish` enabled and is pack-only; a branch dispatch with publication enabled fails before packing, and the publish job itself also requires a tag ref.

## Source of truth

The validator parses `<VersionPrefix>` from `Directory.Build.props` as XML. From a value such as `3.0.0`, it derives:

- exact current version: `3.0.0`;
- current minor line: `3.0.x`;
- current major line: `3.x`.

The script does not duplicate the repository version in its own source or configuration. The configuration declares only the publication state described below.

## Publication state

The `publication` object in `eng/documentation-release-claims.json` records whether the repository version has been published:

```json
"publication": {
  "state": "prepared",
  "latestPublishedVersion": "6.0.0"
}
```

- `prepared`: `Directory.Build.props` names a version that is not yet tagged or published. `latestPublishedVersion` names the latest published stable release and must be lower than the repository version.
- `released`: the repository version is the release being tagged and published. `latestPublishedVersion` must equal the repository version.

When the object is absent the validator treats the repository version as released, which is the original behavior.

The validator applies three kinds of line checks:

| Claim kind | Example wording | `prepared` state | `released` state |
| --- | --- | --- | --- |
| Current | "`X.Y.Z` is the current release", "stable `X.x` package family" | Line descriptors such as "stable `7.x` package family" may name the repository line that `main` maintains. An exact-version currency claim, or a "current release" phrase, must not name the prepared version. | Must name the repository version or line. |
| Latest published | "the latest published stable release is `X.Y.Z`", "most recently published ... `X.Y.Z`" | Must name `latestPublishedVersion`. | Must name the repository version, so wording left over from the prepared state fails. |
| Prepared | "`X.Y.Z`, the prepared next release", "`X.Y.Z` is not yet tagged or published" | Must name the repository version. | Always fails, so prepared-release wording cannot ship in a tagged commit. |

Status phrases are attributed to the nearest version on the line; a match does not reach across another version token.

`-ReleaseTag` accepts the same tag grammar as `scripts/Validate-VersionConsistency.ps1`: `vMAJOR.MINOR.PATCH` with an optional prerelease suffix. The tag version must equal the `Directory.Build.props` version, including any `VersionSuffix`. A stable tag such as `v7.0.0` requires the `released` state. A prerelease tag such as `v7.0.0-rc.1` requires the `prepared` state, because it publishes prerelease packages only and the stable version remains unpublished. The release-preparation pull request makes that switch because the tagged commit's README files are packed into the published packages. See [Release Cadence and Readiness](../articles/release-cadence-and-readiness.md#prepared-and-published-release-wording) for the release sequence.

## Scanned documentation

The default scan includes:

- `README.md`, `CONTRIBUTING.md`, `GOVERNANCE.md`, and `SECURITY.md`;
- `docs/index.md`;
- Markdown articles under `docs/articles/`;
- package README files under `src/`.

Fenced code blocks are ignored because version examples inside commands or sample text are not necessarily release-posture claims.

When the validator finds a stale claim, it reports the file, line number, matched version, expected version, and source line. In GitHub Actions the failure is also emitted as a file-and-line annotation.

## Historical paths and reviewed exceptions

Configuration lives in:

```text
eng/documentation-release-claims.json
```

Use `excludedPaths` only for path classes whose purpose is explicitly historical, such as version-specific release notes, release-readiness records, archived quickstarts, or migration guides.

Use `allowedClaims` for a narrow exception inside an otherwise current document. Every entry must include:

- a path or narrowly scoped path pattern;
- a line-level regular expression;
- a written reason explaining why the exception is intentional or where its correction is tracked.

Prefer fixing stale wording over adding an exception. Prefer version-neutral phrases such as **current release line** in evergreen guidance when an exact version is unnecessary.

Do not add broad exclusions for all documentation or all references to an older major version. Older versions remain valid in historical comparisons; the validator is concerned with language that presents them as the active release posture.

## Fixture coverage

The deterministic fixtures verify that:

- a current release claim passes;
- a stale current release claim fails;
- an ordinary historical version mention passes;
- an explicitly excluded historical file passes;
- stale failure output identifies the file, line, stale value, and expected value;
- prepared-state wording that separates the prepared version from the latest published release passes;
- a prepared-state claim that the prepared version is the current release fails;
- a prepared state whose latest published version is not lower than the repository version fails;
- released-state documentation that retains prepared or latest-published wording for an older version fails;
- `-ReleaseTag` with a stable tag passes for a matching `released` state and fails for a `prepared` state;
- `-ReleaseTag` with a prerelease tag passes for a matching `prepared` state and fails for a `released` state;
- `-ReleaseTag` fails for a tag that does not match the `Directory.Build.props` version, including its prerelease suffix, and for a tag outside the supported grammar.

The fixture runner starts a child PowerShell process for each scenario so the validator's success and failure exit codes are tested exactly as CI observes them.

## Updating the rule

When a new documentation category is added:

1. decide whether the page is evergreen or explicitly historical;
2. keep evergreen pages in the scan whenever possible;
3. add a narrow path exclusion only when historical provenance is the page's purpose;
4. add a line exception only when the wording is intentionally retained and the reason is documented;
5. add or update a deterministic fixture when detection behavior changes;
6. run the fixture suite and the repository validator before opening the pull request.

This validation is a documentation-currency guard. It does not change package versions, release semantics, runtime behavior, public API, or compatibility policy.
