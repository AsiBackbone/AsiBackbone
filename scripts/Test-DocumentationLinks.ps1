[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$validatorPath = Join-Path $PSScriptRoot 'Validate-DocumentationLinks.ps1'
$fixturesRoot = Join-Path $repoRoot 'eng/test-fixtures/documentation-links'
$pwshFileName = if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' }
$pwshPath = Join-Path $PSHOME $pwshFileName

foreach ($requiredPath in @($validatorPath, $fixturesRoot, $pwshPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required documentation link test path was not found: $requiredPath"
    }
}

function Invoke-ValidationFixture {
    param([string]$FixtureName)

    $fixturePath = Join-Path $fixturesRoot $FixtureName
    if (-not (Test-Path -LiteralPath $fixturePath -PathType Container)) {
        throw "Documentation link fixture was not found: $fixturePath"
    }

    # Fixtures exercise the offline manifest and current-release rules only, so
    # remote URL probes are skipped.
    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $pwshPath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @(
            '-NoLogo',
            '-NoProfile',
            '-File', $validatorPath,
            '-ManifestPath', (Join-Path $fixturePath 'cross-repository-links.json'),
            '-SiteRoot', (Join-Path $fixturePath 'site'),
            '-ReleaseClaimsPath', (Join-Path $fixturePath 'documentation-release-claims.json'),
            '-SkipRemote')) {
        $startInfo.ArgumentList.Add($argument)
    }

    $process = [System.Diagnostics.Process]::Start($startInfo)
    if ($null -eq $process) {
        throw "Failed to start PowerShell for fixture '$FixtureName'."
    }

    $standardOutput = $process.StandardOutput.ReadToEnd()
    $standardError = $process.StandardError.ReadToEnd()
    $process.WaitForExit()

    return [pscustomobject]@{
        FixtureName = $FixtureName
        ExitCode = $process.ExitCode
        Output = ($standardOutput + $standardError).Trim()
    }
}

function Assert-FixturePasses {
    param([string]$FixtureName)

    $result = Invoke-ValidationFixture $FixtureName
    if ($result.ExitCode -ne 0) {
        throw "Fixture '$FixtureName' should pass but exited with $($result.ExitCode).`n$($result.Output)"
    }
}

function Assert-FixtureFails {
    param(
        [string]$FixtureName,
        [string[]]$ExpectedText
    )

    $result = Invoke-ValidationFixture $FixtureName
    if ($result.ExitCode -eq 0) {
        throw "Fixture '$FixtureName' should fail but passed.`n$($result.Output)"
    }

    # The validator lists each failure on its own line; collapse whitespace so
    # expectations are independent of line endings.
    $normalizedOutput = [regex]::Replace($result.Output, '\s+', ' ')
    foreach ($expected in $ExpectedText) {
        if (-not $normalizedOutput.Contains($expected, [System.StringComparison]::Ordinal)) {
            throw "Fixture '$FixtureName' output did not contain '$expected'.`n$($result.Output)"
        }
    }
}

Assert-FixturePasses 'valid'

Assert-FixtureFails 'stale-published' @(
    "Current published 'release-notes' destination 'Latest published release notes (3.0.0)' targets 'https://asibackbone.github.io/AsiBackbone/articles/release-notes-300.html', but publication.latestPublishedVersion '3.0.1' requires 'https://asibackbone.github.io/AsiBackbone/articles/release-notes-301.html'.",
    "Current published 'consumer-verification' destination 'Latest published consumer verification guide (3.0.0)' targets 'https://asibackbone.github.io/AsiBackbone/articles/consumer-verification-300.html'",
    "destination label 'Latest published release notes (3.0.0)' names '3.0.0'",
    "Current 'release-notes' destinations disagree: published 'https://asibackbone.github.io/AsiBackbone/articles/release-notes-300.html' does not match local 'articles/release-notes-301.html'.")

Assert-FixtureFails 'stale-label' @(
    "destination label 'Latest published release notes (3.0.0)' names '3.0.0', but publication.latestPublishedVersion is '3.0.1'.",
    "destination label 'Latest published consumer verification guide' must identify version '3.0.1'.")

Assert-FixtureFails 'stale-local' @(
    "Current local 'release-notes' destination 'Current release notes' targets 'articles/release-notes-300.html', but publication.latestPublishedVersion '3.0.1' requires 'articles/release-notes-301.html'.",
    "Current 'release-notes' destinations disagree: published 'https://asibackbone.github.io/AsiBackbone/articles/release-notes-301.html' does not match local 'articles/release-notes-300.html'.")

Assert-FixtureFails 'undeclared-release-page' @(
    "publishedDestinations entry 'Latest published release notes (3.0.1)' targets a version-specific release page",
    "declares neither currentReleaseDocument nor `"historical`": true.",
    "publishedDestinations must declare exactly one entry with currentReleaseDocument 'release-notes'; found 0.")

Write-Host 'Documentation link validator fixtures passed.'
