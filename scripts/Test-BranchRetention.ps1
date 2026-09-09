[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$scriptPath = Join-Path $PSScriptRoot 'Manage-BranchRetention.ps1'
$policyPath = Join-Path $repoRoot 'eng/repository-controls/branch-retention-policy.json'
$fixturesRoot = Join-Path $repoRoot 'eng/test-fixtures/branch-retention'
$pwshFileName = if ($IsWindows) { 'pwsh.exe' } else { 'pwsh' }
$pwshPath = Join-Path $PSHOME $pwshFileName

foreach ($requiredPath in @($scriptPath, $policyPath, $fixturesRoot, $pwshPath)) {
    if (-not (Test-Path -LiteralPath $requiredPath)) {
        throw "Required branch retention test path was not found: $requiredPath"
    }
}

function Invoke-RetentionFixture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$FixtureName,

        [switch]$Apply
    )

    $fixturePath = Join-Path $fixturesRoot "$FixtureName.json"
    if (-not (Test-Path -LiteralPath $fixturePath -PathType Leaf)) {
        throw "Branch retention fixture was not found: $fixturePath"
    }

    $startInfo = [System.Diagnostics.ProcessStartInfo]::new()
    $startInfo.FileName = $pwshPath
    $startInfo.UseShellExecute = $false
    $startInfo.RedirectStandardOutput = $true
    $startInfo.RedirectStandardError = $true
    $startInfo.ArgumentList.Add('-NoLogo')
    $startInfo.ArgumentList.Add('-NoProfile')
    $startInfo.ArgumentList.Add('-File')
    $startInfo.ArgumentList.Add($scriptPath)
    $startInfo.ArgumentList.Add('-PolicyPath')
    $startInfo.ArgumentList.Add($policyPath)
    $startInfo.ArgumentList.Add('-FixturePath')
    $startInfo.ArgumentList.Add($fixturePath)

    if ($Apply) {
        # -WhatIf keeps the apply path from issuing mutations while still
        # exercising the pruning branch of the script.
        $startInfo.ArgumentList.Add('-Apply')
        $startInfo.ArgumentList.Add('-WhatIf')
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

function Assert-OutputContains {
    param(
        [Parameter(Mandatory = $true)]
        [pscustomobject]$Result,

        [Parameter(Mandatory = $true)]
        [string[]]$ExpectedText
    )

    foreach ($expected in $ExpectedText) {
        if (-not $Result.Output.Contains($expected, [System.StringComparison]::Ordinal)) {
            throw "Fixture '$($Result.FixtureName)' output did not contain '$expected'.`n$($Result.Output)"
        }
    }
}

# A repository whose branches are all classified and whose release evidence is
# complete must pass.
$cleanResult = Invoke-RetentionFixture -FixtureName 'clean-repository'
if ($cleanResult.ExitCode -ne 0) {
    throw "Fixture 'clean-repository' should pass but exited with $($cleanResult.ExitCode).`n$($cleanResult.Output)"
}

Assert-OutputContains -Result $cleanResult -ExpectedText @(
    'active       main',
    'active       issue_work',
    'No disposable branches are pending removal.',
    'Branch retention audit passed.'
)

# The pruning fixture must classify each branch correctly and refuse to treat
# unmerged or unpublished work as removable.
$prunableResult = Invoke-RetentionFixture -FixtureName 'prunable-branches'
if ($prunableResult.ExitCode -eq 0) {
    throw "Fixture 'prunable-branches' should fail but passed.`n$($prunableResult.Output)"
}

Assert-OutputContains -Result $prunableResult -ExpectedText @(
    # Reachable from main, so nothing unique would be lost.
    'removable    release/5.0.0 (fully merged into main)',
    # Ahead of main but preserved by a tag that has a published release.
    'removable    release/4.0.0 (covered by v4.0.0)',
    # Preserved by a tag, but that tag has no published release.
    'RETAIN       release/3.0.0 (tag v3.0.0 has no published release)',
    # Ahead of main and preserved by nothing: deleting it would lose commits.
    'RETAIN       release/9.9.9 (ahead 4, no covering tag)',
    # Matches no retention class, so its retention is not a recorded decision.
    'UNCLASSIFIED experiment',
    'deleting it would lose commits',
    "Tag 'v3.0.0' has no published release"
)

# Guard the safety property directly: neither retained branch may be reported
# as removable.
foreach ($mustNotBeRemovable in @('removable    release/3.0.0', 'removable    release/9.9.9')) {
    if ($prunableResult.Output.Contains($mustNotBeRemovable, [System.StringComparison]::Ordinal)) {
        throw "Fixture 'prunable-branches' reported a retained branch as removable: '$mustNotBeRemovable'.`n$($prunableResult.Output)"
    }
}

# The apply path must preview deletion only for the verified-removable branches.
$applyResult = Invoke-RetentionFixture -FixtureName 'prunable-branches' -Apply

Assert-OutputContains -Result $applyResult -ExpectedText @(
    'release/5.0.0',
    'release/4.0.0'
)

foreach ($mustNotBeDeleted in @('Delete remote branch" on target "AsiBackbone/AsiBackbone branch release/9.9.9', 'Delete remote branch" on target "AsiBackbone/AsiBackbone branch release/3.0.0')) {
    if ($applyResult.Output.Contains($mustNotBeDeleted, [System.StringComparison]::Ordinal)) {
        throw "Fixture 'prunable-branches' previewed deletion of a retained branch.`n$($applyResult.Output)"
    }
}

Write-Host 'Branch retention fixtures passed.'
