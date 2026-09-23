[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$validatorPath = Join-Path $PSScriptRoot 'Validate-DocumentationReleaseClaims.ps1'
$fixturesRoot = Join-Path $repoRoot 'eng/test-fixtures/documentation-release-claims'
$pwshFileName = if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' }
$pwshPath = Join-Path $PSHOME $pwshFileName

foreach ($requiredPath in @($validatorPath, $fixturesRoot, $pwshPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required documentation release-claim test path was not found: $requiredPath"
    }
}

function Invoke-ValidationFixture {
    param(
        [string]$FixtureName,
        [string[]]$AdditionalArguments = @()
    )

    $fixturePath = Join-Path $fixturesRoot $FixtureName
    if (-not (Test-Path -LiteralPath $fixturePath -PathType Container)) {
        throw "Documentation release-claim fixture was not found: $fixturePath"
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $pwshPath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add('-NoLogo')
    $startInfo.ArgumentList.Add('-NoProfile')
    $startInfo.ArgumentList.Add('-File')
    $startInfo.ArgumentList.Add($validatorPath)
    $startInfo.ArgumentList.Add('-RepositoryRoot')
    $startInfo.ArgumentList.Add($fixturePath)

    foreach ($additionalArgument in $AdditionalArguments) {
        $startInfo.ArgumentList.Add($additionalArgument)
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
    param(
        [string]$FixtureName,
        [string[]]$AdditionalArguments = @()
    )

    $result = Invoke-ValidationFixture $FixtureName $AdditionalArguments
    if ($result.ExitCode -ne 0) {
        throw "Fixture '$FixtureName' should pass but exited with $($result.ExitCode).`n$($result.Output)"
    }
}

function Assert-FixtureFails {
    param(
        [string]$FixtureName,
        [string[]]$ExpectedText,
        [string[]]$AdditionalArguments = @()
    )

    $result = Invoke-ValidationFixture $FixtureName $AdditionalArguments
    if ($result.ExitCode -eq 0) {
        throw "Fixture '$FixtureName' should fail but passed.`n$($result.Output)"
    }

    foreach ($expected in $ExpectedText) {
        if (-not $result.Output.Contains($expected, [System.StringComparison]::Ordinal)) {
            throw "Fixture '$FixtureName' output did not contain '$expected'.`n$($result.Output)"
        }
    }
}

Assert-FixturePasses 'valid-current'
Assert-FixturePasses 'historical-mention'
Assert-FixturePasses 'excluded-historical'
Assert-FixturePasses 'prepared-valid'
Assert-FixturePasses 'released-valid'
Assert-FixturePasses 'released-valid' @('-ReleaseTag', 'v3.0.0')
Assert-FixturePasses 'prepared-prerelease'
Assert-FixturePasses 'prepared-prerelease' @('-ReleaseTag', 'v3.0.0-rc.1')

Assert-FixtureFails 'prepared-current-claim' @(
    'README.md:1',
    "release claim '3.0.0' presents the prepared, unpublished version as the current release",
    "Expected '2.0.0' from publication.latestPublishedVersion '2.0.0'")

Assert-FixtureFails 'prepared-invalid-latest' @(
    'eng/documentation-release-claims.json',
    "publication.latestPublishedVersion '3.0.0' must be lower")

Assert-FixtureFails 'released-stale-wording' @(
    'README.md:2',
    "published-release claim '2.0.0'",
    'README.md:3',
    "prepared-release claim '3.0.0'")

Assert-FixtureFails 'prepared-valid' @("Release tag 'v3.0.0' requires publication.state 'released'") @('-ReleaseTag', 'v3.0.0')
Assert-FixtureFails 'released-valid' @("Release tag 'v3.0.1' does not match Directory.Build.props version '3.0.0'") @('-ReleaseTag', 'v3.0.1')
Assert-FixtureFails 'released-valid' @("Release tag 'v3.0.0-rc.1' does not match Directory.Build.props version '3.0.0'") @('-ReleaseTag', 'v3.0.0-rc.1')
Assert-FixtureFails 'released-valid' @("Release tag 'release-3.0.0' is not a supported release tag") @('-ReleaseTag', 'release-3.0.0')
Assert-FixtureFails 'released-prerelease' @("Prerelease tag 'v3.0.0-rc.1' requires publication.state 'prepared'") @('-ReleaseTag', 'v3.0.0-rc.1')

$staleResult = Invoke-ValidationFixture 'stale-current'
if ($staleResult.ExitCode -eq 0) {
    throw "Fixture 'stale-current' should fail but passed.`n$($staleResult.Output)"
}

foreach ($expectedText in @('README.md:1', "release claim '2.x'", "Expected '3.x'")) {
    if (-not $staleResult.Output.Contains($expectedText, [System.StringComparison]::Ordinal)) {
        throw "Fixture 'stale-current' output did not contain '$expectedText'.`n$($staleResult.Output)"
    }
}

Write-Host 'Documentation release-claim validator fixtures passed.'
