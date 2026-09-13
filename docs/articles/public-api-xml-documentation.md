# Public API XML Documentation

AsiBackbone generates XML documentation for package and DocFX output. Public API documentation is part of the governance contract because consumers rely on stable names, decision semantics, acknowledgment behavior, audit records, signing boundaries, and host-owned integration points.

## Staged CS1591 policy

`CS1591` is no longer suppressed at repository scope. Instead, existing package projects opt into a project-scoped compatibility baseline with `AsiBackboneSuppressMissingXmlDocs=true` while the current public API documentation gaps are inventoried.

This is intentionally staged for the `3.x` line:

1. Inventory every public package project listed in `eng/xml-docs/public-api-projects.txt`.
2. Check the inventory against `eng/xml-docs/cs1591-baseline.csv` so selected projects cannot regress above their tracked ceiling.
3. Clean one package surface at a time.
4. Lower the tracked ceiling as each package improves.
5. Move clean package projects into `eng/xml-docs/staged-enforcement-projects.txt`.
6. Run enforcement mode for only those selected package surfaces.
7. Remove project-scoped suppression from a project only when its public API XML documentation is complete or its remaining exceptions are documented.

This avoids a destabilizing repository-wide cleanup while preventing new package surfaces from inheriting a blanket `CS1591` suppression by default.

## Inventory mode

Run inventory mode to build the selected package projects with `CS1591` unsuppressed and write a Markdown report:

```powershell
./scripts/Validate-XmlDocumentation.ps1 -Mode Inventory
```

The generated report is written to:

```text
artifacts/xml-docs/cs1591-inventory.md
```

CI uploads the same file as the `asi-backbone-cs1591-inventory` artifact.

When `eng/xml-docs/cs1591-baseline.csv` is present, inventory mode also compares each selected project against its `MaxCS1591` ceiling. The script fails if a project exceeds its tracked ceiling or if a project with gaps is selected but has no baseline entry. This makes the inventory an explicit regression gate rather than a report-only artifact.

## Baseline ceilings

The baseline file uses this shape:

```csv
Project,MaxCS1591,Notes
src/AsiBackbone.Core/AsiBackbone.Core.csproj,92,Calibrated to observed inventory; Core remains the first candidate for staged enforcement once cleaned.
```

Ceilings are calibrated to the observed inventory count for each project, so any new public member without XML documentation fails the inventory gate. They are a guardrail for regression, not the final documentation target, and should trend downward to zero as package surfaces are cleaned.

Use this policy for future updates:

- Do not raise a baseline ceiling unless the reason is documented in the same PR.
- Lower ceilings whenever a package cleanup reduces the observed inventory count.
- Add new public package projects to `eng/xml-docs/public-api-projects.txt` and `eng/xml-docs/cs1591-baseline.csv` before they become release candidates.
- Prefer moving a clean package to `eng/xml-docs/staged-enforcement-projects.txt` over carrying a permanent baseline ceiling.

## Enforcement mode

Run enforcement mode after at least one project has been cleaned and added to `eng/xml-docs/staged-enforcement-projects.txt`:

```powershell
./scripts/Validate-XmlDocumentation.ps1 -Mode Enforce
```

Enforcement mode builds only the staged projects with:

```text
/p:AsiBackboneSuppressMissingXmlDocs=false
/p:AsiBackboneEnforceMissingXmlDocs=true
```

This treats `CS1591` as an error for those projects without forcing immediate enforcement across every package.

## Current hardening threshold

The current threshold is:

- all public package projects remain inventoried;
- all inventoried projects are compared to tracked `MaxCS1591` ceilings;
- selected clean projects may be promoted to full enforcement;
- baseline ceilings are calibrated to observed counts: `AsiBackbone.Core` (92) and `AsiBackbone.Analyzers` (8) carry remaining gaps, and every other public package project is held at 0;
- Core remains the first candidate for staged enforcement because it is the framework-neutral governance engine.

The next hardening threshold is to promote the zero-gap packages into `eng/xml-docs/staged-enforcement-projects.txt`, then document the remaining Core and Analyzers gaps and lower their ceilings until those packages can move into enforcement mode.

## Intentional exceptions

Existing source package projects currently set `AsiBackboneSuppressMissingXmlDocs=true` as a legacy compatibility baseline. That property is an intentional transitional exception, not a permanent default.

Do not add the property to new projects unless the exception is documented with:

- the reason the project cannot yet satisfy `CS1591`;
- the expected cleanup path;
- whether the project appears in the inventory list;
- whether the project appears in the tracked baseline file;
- whether the project is a candidate for staged enforcement;
- whether the exception affects NuGet or DocFX consumer documentation.

## Generated documentation output

`GenerateDocumentationFile` remains enabled centrally so NuGet packages and DocFX continue receiving generated XML documentation. This policy changes only how missing public API comments are surfaced and phased into enforcement; it does not disable XML documentation generation.

## Related files

- `Directory.Build.props`
- `Directory.Build.targets`
- `eng/xml-docs/public-api-projects.txt`
- `eng/xml-docs/cs1591-baseline.csv`
- `eng/xml-docs/staged-enforcement-projects.txt`
- `scripts/Validate-XmlDocumentation.ps1`
- `.github/workflows/ci.yml`
