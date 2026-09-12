[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$SbomDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseNotesPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$TagName,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceCommit,

    [string]$Repository = 'AsiBackbone/AsiBackbone'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Hex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

if ($TagName -notmatch '^v(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)$') {
    throw "Tag name '$TagName' must use the form vMAJOR.MINOR.PATCH with an optional prerelease suffix."
}

$version = $Matches['version']

if ($Repository -notmatch '^[^/]+/[^/]+$') {
    throw "Repository '$Repository' must use the owner/name form."
}

$resolvedSbomDirectory = (Resolve-Path -LiteralPath $SbomDirectory).Path
$resolvedReleaseNotesPath = (Resolve-Path -LiteralPath $ReleaseNotesPath).Path

if (-not (Test-Path -LiteralPath $resolvedReleaseNotesPath -PathType Leaf)) {
    throw "Release notes file was not found: $resolvedReleaseNotesPath"
}

if ([string]::IsNullOrWhiteSpace((Get-Content -LiteralPath $resolvedReleaseNotesPath -Raw))) {
    throw "Release notes file is empty: $resolvedReleaseNotesPath"
}

$sbomManifestPath = Join-Path $resolvedSbomDirectory 'sbom-manifest.json'

if (-not (Test-Path -LiteralPath $sbomManifestPath -PathType Leaf)) {
    throw "SBOM manifest was not found: $sbomManifestPath"
}

$sbomManifest = Get-Content -LiteralPath $sbomManifestPath -Raw | ConvertFrom-Json
$packageEntries = @($sbomManifest.packages)

if ($packageEntries.Count -eq 0) {
    throw 'The SBOM manifest does not contain any package entries.'
}

$declaredSbomNames = @($packageEntries | ForEach-Object { [string]$_.sbomFile } | Sort-Object -Unique)
$actualSbomFiles = @(Get-ChildItem -LiteralPath $resolvedSbomDirectory -Filter '*.spdx.json' -File | Sort-Object Name)
$actualSbomNames = @($actualSbomFiles.Name)
$sbomDifference = @(Compare-Object -ReferenceObject $declaredSbomNames -DifferenceObject $actualSbomNames)

if ($sbomDifference.Count -ne 0) {
    $details = $sbomDifference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
    throw "SBOM files do not match sbom-manifest.json:`n$($details -join "`n")"
}

foreach ($entry in $packageEntries) {
    $sbomPath = Join-Path $resolvedSbomDirectory ([string]$entry.sbomFile)
    $actualHash = Get-Sha256Hex -Path $sbomPath
    $expectedHash = ([string]$entry.sbomSha256).ToLowerInvariant()

    if ($actualHash -ne $expectedHash) {
        throw "SBOM hash mismatch for '$($entry.sbomFile)'. Expected $expectedHash but found $actualHash."
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$resolvedOutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$existingOutput = @(Get-ChildItem -LiteralPath $resolvedOutputDirectory -Force)

if ($existingOutput.Count -ne 0) {
    throw "Release evidence output directory must be empty: $resolvedOutputDirectory"
}

$assetRecords = New-Object System.Collections.Generic.List[object]

foreach ($sbomFile in $actualSbomFiles) {
    $destination = Join-Path $resolvedOutputDirectory $sbomFile.Name
    Copy-Item -LiteralPath $sbomFile.FullName -Destination $destination
    $assetRecords.Add([ordered]@{
        fileName = $sbomFile.Name
        sha256 = Get-Sha256Hex -Path $destination
        mediaType = 'application/spdx+json'
        purpose = 'package-sbom'
    })
}

$copiedSbomManifestPath = Join-Path $resolvedOutputDirectory 'sbom-manifest.json'
Copy-Item -LiteralPath $sbomManifestPath -Destination $copiedSbomManifestPath
$assetRecords.Add([ordered]@{
    fileName = 'sbom-manifest.json'
    sha256 = Get-Sha256Hex -Path $copiedSbomManifestPath
    mediaType = 'application/json'
    purpose = 'package-to-sbom-map'
})

$releaseNotesFileName = 'asibackbone-{0}-release-notes.md' -f $version
$copiedReleaseNotesPath = Join-Path $resolvedOutputDirectory $releaseNotesFileName
Copy-Item -LiteralPath $resolvedReleaseNotesPath -Destination $copiedReleaseNotesPath
$assetRecords.Add([ordered]@{
    fileName = $releaseNotesFileName
    sha256 = Get-Sha256Hex -Path $copiedReleaseNotesPath
    mediaType = 'text/markdown'
    purpose = 'release-notes'
})

$evidenceManifest = [ordered]@{
    schemaVersion = 1
    repository = $Repository
    releaseTag = $TagName
    releaseVersion = $version
    sourceCommit = $SourceCommit.ToLowerInvariant()
    createdUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    assets = @($assetRecords.ToArray())
    packageArtifacts = @($packageEntries | ForEach-Object {
        [ordered]@{
            packageId = [string]$_.packageId
            version = [string]$_.version
            packageFile = [string]$_.packageFile
            packageSha256 = ([string]$_.packageSha256).ToLowerInvariant()
            sbomFile = [string]$_.sbomFile
            sbomSha256 = ([string]$_.sbomSha256).ToLowerInvariant()
        }
    })
    attestationVerification = [ordered]@{
        repository = $Repository
        command = "gh attestation verify <downloaded-file> --repo $Repository"
    }
}

$evidenceManifestPath = Join-Path $resolvedOutputDirectory 'release-evidence-manifest.json'
$evidenceManifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $evidenceManifestPath -Encoding utf8 -NoNewline

Write-Host "Prepared $($assetRecords.Count + 1) durable release evidence file(s) in '$resolvedOutputDirectory'."
Write-Host "Release evidence manifest: $evidenceManifestPath"
