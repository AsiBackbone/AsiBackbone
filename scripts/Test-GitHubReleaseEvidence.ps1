[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$generatorPath = Join-Path $PSScriptRoot 'New-GitHubReleaseEvidence.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("asibackbone-release-evidence-{0}" -f [guid]::NewGuid().ToString('N'))
$packageDirectory = Join-Path $testRoot 'packages'
$sbomDirectory = Join-Path $testRoot 'sbom'
$outputDirectory = Join-Path $testRoot 'output'
$tamperedPackageOutputDirectory = Join-Path $testRoot 'tampered-package-output'
$tamperedOutputDirectory = Join-Path $testRoot 'tampered-output'
$releaseNotesPath = Join-Path $testRoot 'release-notes.md'

try {
    New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $sbomDirectory -Force | Out-Null
    Set-Content -LiteralPath $releaseNotesPath -Value '# Test release notes' -Encoding utf8 -NoNewline

    $packageFileName = 'AsiBackbone.Core.9.8.7.nupkg'
    $packagePath = Join-Path $packageDirectory $packageFileName
    Set-Content -LiteralPath $packagePath -Value 'test package content' -Encoding utf8 -NoNewline
    $packageHash = (Get-FileHash -LiteralPath $packagePath -Algorithm SHA256).Hash.ToLowerInvariant()

    $sbomFileName = 'AsiBackbone.Core.9.8.7.spdx.json'
    $sbomPath = Join-Path $sbomDirectory $sbomFileName
    Set-Content -LiteralPath $sbomPath -Value '{"spdxVersion":"SPDX-2.3"}' -Encoding utf8 -NoNewline
    $sbomHash = (Get-FileHash -LiteralPath $sbomPath -Algorithm SHA256).Hash.ToLowerInvariant()

    $sbomManifest = [ordered]@{
        schemaVersion = 1
        packages = @(
            [ordered]@{
                packageId = 'AsiBackbone.Core'
                version = '9.8.7'
                packageFile = $packageFileName
                packageSha256 = $packageHash
                sbomFile = $sbomFileName
                sbomSha256 = $sbomHash
            }
        )
    }
    $sbomManifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $sbomDirectory 'sbom-manifest.json') -Encoding utf8 -NoNewline

    & $generatorPath `
        -PackageDirectory $packageDirectory `
        -SbomDirectory $sbomDirectory `
        -ReleaseNotesPath $releaseNotesPath `
        -OutputDirectory $outputDirectory `
        -TagName 'v9.8.7' `
        -SourceCommit ('b' * 40)

    $expectedFiles = @(
        $packageFileName,
        $sbomFileName,
        'sbom-manifest.json',
        'asibackbone-9.8.7-release-notes.md',
        'release-evidence-manifest.json'
    )

    foreach ($expectedFile in $expectedFiles) {
        $expectedPath = Join-Path $outputDirectory $expectedFile
        if (-not (Test-Path -LiteralPath $expectedPath -PathType Leaf)) {
            throw "Expected release evidence file was not generated: $expectedPath"
        }
    }

    $evidenceManifest = Get-Content -LiteralPath (Join-Path $outputDirectory 'release-evidence-manifest.json') -Raw | ConvertFrom-Json

    if ($evidenceManifest.releaseTag -ne 'v9.8.7' -or $evidenceManifest.assets.Count -ne 4) {
        throw 'Generated release evidence manifest did not preserve the expected release identity and asset inventory.'
    }

    Set-Content -LiteralPath $packagePath -Value 'tampered package content' -Encoding utf8 -NoNewline
    $packageTamperRejected = $false

    try {
        & $generatorPath `
            -PackageDirectory $packageDirectory `
            -SbomDirectory $sbomDirectory `
            -ReleaseNotesPath $releaseNotesPath `
            -OutputDirectory $tamperedPackageOutputDirectory `
            -TagName 'v9.8.7' `
            -SourceCommit ('b' * 40)
    }
    catch {
        $packageTamperRejected = $_.Exception.Message -like "Package hash mismatch*"
    }

    if (-not $packageTamperRejected) {
        throw 'Tampered package input was not rejected.'
    }

    Set-Content -LiteralPath $packagePath -Value 'test package content' -Encoding utf8 -NoNewline
    Set-Content -LiteralPath $sbomPath -Value '{"spdxVersion":"tampered"}' -Encoding utf8 -NoNewline
    $tamperRejected = $false

    try {
        & $generatorPath `
            -PackageDirectory $packageDirectory `
            -SbomDirectory $sbomDirectory `
            -ReleaseNotesPath $releaseNotesPath `
            -OutputDirectory $tamperedOutputDirectory `
            -TagName 'v9.8.7' `
            -SourceCommit ('b' * 40)
    }
    catch {
        $tamperRejected = $_.Exception.Message -like "SBOM hash mismatch*"
    }

    if (-not $tamperRejected) {
        throw 'Tampered SBOM input was not rejected.'
    }

    Write-Host 'GitHub release evidence tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
