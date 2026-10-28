[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$validatorPath = Join-Path $PSScriptRoot 'Validate-CurrentTerminology.ps1'
$fixturesRoot = Join-Path $repoRoot 'eng/test-fixtures/current-terminology'
$pwshFileName = if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' }
$pwshPath = Join-Path $PSHOME $pwshFileName

foreach ($requiredPath in @($validatorPath, $fixturesRoot, $pwshPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required current terminology test path was not found: $requiredPath"
    }
}

function Invoke-ValidationFixture {
    param([string]$FixtureName)

    $fixturePath = Join-Path $fixturesRoot $FixtureName
    if (-not (Test-Path -LiteralPath $fixturePath -PathType Container)) {
        throw "Current terminology fixture was not found: $fixturePath"
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $pwshPath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    foreach ($argument in @(
            '-NoLogo',
            '-NoProfile',
            '-File', $validatorPath,
            '-RepositoryRoot', $fixturePath,
            '-ConfigurationPath', (Join-Path $fixturePath 'current-terminology.json'))) {
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

# Current wording, an allowed retained URL, and identifiers containing legacy words pass.
Assert-FixturePasses 'valid'

Assert-FixtureFails 'legacy-summary' @(
    "README.md:3: legacy term 'capability token' in a current-facing summary; use 'capability grant'",
    "src/Sample.Core/README.md:3: legacy term 'handshake' in a current-facing summary; use 'acknowledgment'")

Assert-FixtureFails 'unused-allowance' @(
    "Allowance for 'handshake' in 'README.md' no longer matches any line.")

Write-Host 'Current terminology validator fixtures passed.'
