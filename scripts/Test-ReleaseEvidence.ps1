[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$generatorPath = Join-Path $PSScriptRoot 'New-ReleaseEvidence.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("asibackbone-release-evidence-{0}" -f [guid]::NewGuid().ToString('N'))
$packageDirectory = Join-Path $testRoot 'packages'
$sbomDirectory = Join-Path $testRoot 'sbom'
$bundleDirectory = Join-Path $testRoot 'sigstore'
$emptyBundleDirectory = Join-Path $testRoot 'sigstore-missing'
$missingBundleOutputDirectory = Join-Path $testRoot 'missing-bundle-output'
$outputDirectory = Join-Path $testRoot 'output'
$tamperedPackageOutputDirectory = Join-Path $testRoot 'tampered-package-output'
$tamperedOutputDirectory = Join-Path $testRoot 'tampered-output'
$releaseNotesPath = Join-Path $testRoot 'release-notes.md'

try {
    New-Item -ItemType Directory -Path $packageDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $sbomDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $bundleDirectory -Force | Out-Null
    New-Item -ItemType Directory -Path $emptyBundleDirectory -Force | Out-Null
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

    $bundleFileName = "$packageFileName.sigstore.json"
    $bundlePath = Join-Path $bundleDirectory $bundleFileName
    Set-Content -LiteralPath $bundlePath -Value '{"mediaType":"application/vnd.dev.sigstore.bundle.v0.3+json"}' -Encoding utf8 -NoNewline
    $bundleHash = (Get-FileHash -LiteralPath $bundlePath -Algorithm SHA256).Hash.ToLowerInvariant()

    # A missing Sigstore bundle must stop evidence generation.
    $missingBundleRejected = $false

    try {
        & $generatorPath `
            -PackageDirectory $packageDirectory `
            -SbomDirectory $sbomDirectory `
            -SigstoreBundleDirectory $emptyBundleDirectory `
            -ReleaseNotesPath $releaseNotesPath `
            -OutputDirectory $missingBundleOutputDirectory `
            -TagName 'v9.8.7' `
            -SourceCommit ('b' * 40)
    }
    catch {
        $missingBundleRejected = $_.Exception.Message -like 'Sigstore bundle missing*'
    }

    if (-not $missingBundleRejected) {
        throw 'Release evidence generation did not reject a package without a Sigstore bundle.'
    }

    & $generatorPath `
        -PackageDirectory $packageDirectory `
        -SbomDirectory $sbomDirectory `
        -SigstoreBundleDirectory $bundleDirectory `
        -ReleaseNotesPath $releaseNotesPath `
        -OutputDirectory $outputDirectory `
        -TagName 'v9.8.7' `
        -SourceCommit ('b' * 40)

    $expectedFiles = @(
        $packageFileName,
        $sbomFileName,
        $bundleFileName,
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

    if ($evidenceManifest.releaseTag -ne 'v9.8.7' -or $evidenceManifest.assets.Count -ne 5) {
        throw 'Generated release evidence manifest did not preserve the expected release identity and asset inventory.'
    }

    $packageArtifact = @($evidenceManifest.packageArtifacts)[0]

    if ($packageArtifact.sigstoreBundleFile -cne $bundleFileName -or $packageArtifact.sigstoreBundleSha256 -ne $bundleHash) {
        throw 'Generated release evidence manifest did not record the Sigstore bundle file and digest.'
    }

    $signatures = $evidenceManifest.sigstoreSignatures
    $expectedIdentity = 'https://github.com/AsiBackbone/AsiBackbone/.github/workflows/publish-packages.yml@refs/tags/v9.8.7'

    if ($signatures.certificateIdentity -cne $expectedIdentity -or
        $signatures.certificateOidcIssuer -cne 'https://token.actions.githubusercontent.com' -or
        $signatures.workflowRef -cne 'refs/tags/v9.8.7' -or
        $signatures.workflowSha -cne ('b' * 40) -or
        $signatures.workflowTrigger -cne 'push') {
        throw 'Generated release evidence manifest did not record the exact Sigstore signer identity.'
    }

    $bundleAsset = @($evidenceManifest.assets | Where-Object { $_.fileName -ceq $bundleFileName })

    if ($bundleAsset.Count -ne 1 -or $bundleAsset[0].purpose -cne 'package-sigstore-bundle') {
        throw 'Generated release evidence manifest did not list the Sigstore bundle as a release asset.'
    }

    Set-Content -LiteralPath $packagePath -Value 'tampered package content' -Encoding utf8 -NoNewline
    $packageTamperRejected = $false

    try {
        & $generatorPath `
            -PackageDirectory $packageDirectory `
            -SbomDirectory $sbomDirectory `
            -SigstoreBundleDirectory $bundleDirectory `
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
            -SigstoreBundleDirectory $bundleDirectory `
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

    Write-Host 'Release evidence tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
