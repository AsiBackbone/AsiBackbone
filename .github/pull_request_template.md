## Summary

- 
- 
- 

## Validation

- [ ] Documentation-only change.
- [ ] Ran `dotnet restore AsiBackbone.slnx`.
- [ ] Ran `dotnet build AsiBackbone.slnx --configuration Release --no-restore`.
- [ ] Ran `dotnet test AsiBackbone.slnx --configuration Release --no-build --no-restore`.
- [ ] Ran `dotnet format AsiBackbone.slnx --verify-no-changes --verbosity minimal`.
- [ ] Ran `dotnet tool restore`.
- [ ] Ran `dotnet tool run docfx docs/docfx.json`.
- [ ] Ran `./scripts/Validate-DocumentationLinks.ps1` after the DocFX build.
- [ ] Documentation ownership/URL continuity reviewed (or not applicable): general teaching belongs in Learning; package/API/runtime truth belongs in AsiBackbone; established public URLs are preserved with transition pages when authority moves.
- [ ] Ran external-consumer smoke test: `bash ./eng/smoke-tests/external-consumer-smoke.sh`.
- [ ] Ran stable-package integration smoke test: `bash ./eng/smoke-tests/stable-package-integration-smoke.sh`.
- [ ] Not applicable / explained below.

## Issue Link

Closes #

## Notes

Add any deployment notes, migration notes, screenshots, or review context here.

## Contributor License Agreement

- [ ] By submitting this pull request, I agree to the Contributor License Agreement in `CONTRIBUTING.md`.
