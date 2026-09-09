[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [ValidatePattern('^[^/]+/[^/]+$')]
    [string]$Repository = 'AsiBackbone/AsiBackbone',

    [string]$PolicyPath = 'eng/repository-controls/branch-retention-policy.json',

    [switch]$Apply,

    # Test-only seam. When supplied, GitHub API responses are served from a JSON
    # fixture keyed by endpoint instead of calling gh, so the retention decisions
    # can be exercised offline by Test-BranchRetention.ps1.
    [string]$FixturePath
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$script:fixture = $null

if (-not [string]::IsNullOrWhiteSpace($FixturePath)) {
    if (-not (Test-Path -LiteralPath $FixturePath -PathType Leaf)) {
        throw "Branch retention fixture was not found: $FixturePath"
    }

    $script:fixture = Get-Content -LiteralPath $FixturePath -Raw | ConvertFrom-Json
}

function Assert-GitHubCli {
    $gh = Get-Command gh -ErrorAction SilentlyContinue

    if ($null -eq $gh) {
        throw 'GitHub CLI (gh) is required. Install it and authenticate before running this script.'
    }

    & gh auth status 1>$null 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub CLI is not authenticated. Run gh auth login with an account that can administer the repository.'
    }
}

function Invoke-GitHubApi {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [switch]$AllowNotFound
    )

    if ($null -ne $script:fixture) {
        $endpoint = @($Arguments | Where-Object { $_ -like 'repos/*' }) | Select-Object -First 1

        if ($null -eq $endpoint) {
            return $null
        }

        $property = $script:fixture.PSObject.Properties[$endpoint]

        if ($null -eq $property) {
            if ($AllowNotFound) {
                return $null
            }

            throw "Fixture has no response for endpoint '$endpoint'."
        }

        return $property.Value
    }

    $stderrPath = [System.IO.Path]::GetTempFileName()

    try {
        $apiArguments = @(
            '-H',
            'Accept: application/vnd.github+json',
            '-H',
            'X-GitHub-Api-Version: 2026-03-10'
        ) + $Arguments

        # Windows PowerShell can promote native stderr to a NativeCommandError when
        # ErrorActionPreference is Stop. Relax it for this call only so a 404 can be
        # classified by exit code instead of terminating the script.
        $previousErrorActionPreference = $ErrorActionPreference
        try {
            $ErrorActionPreference = 'Continue'
            $output = @(& gh api @apiArguments 2> $stderrPath)
            $exitCode = $LASTEXITCODE
        }
        finally {
            $ErrorActionPreference = $previousErrorActionPreference
        }

        $stderr = if (Test-Path -LiteralPath $stderrPath) {
            Get-Content -LiteralPath $stderrPath -Raw
        }
        else {
            ''
        }

        if ($exitCode -ne 0) {
            if ($AllowNotFound -and $stderr -match '(?i)(HTTP\s+404|Not Found)') {
                return $null
            }

            $message = if ([string]::IsNullOrWhiteSpace($stderr)) {
                "GitHub API call failed with exit code $exitCode."
            }
            else {
                "GitHub API call failed with exit code ${exitCode}: $stderr"
            }

            throw $message
        }

        if ($output.Count -eq 0) {
            return $null
        }

        return ($output -join [Environment]::NewLine | ConvertFrom-Json)
    }
    finally {
        Remove-Item -LiteralPath $stderrPath -ErrorAction SilentlyContinue
    }
}

function Test-BranchPattern {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [string[]]$Patterns
    )

    foreach ($pattern in $Patterns) {
        if ($Name -like $pattern) {
            return $true
        }
    }

    return $false
}

if ($null -eq $script:fixture) {
    Assert-GitHubCli
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$resolvedPolicyPath = if ([System.IO.Path]::IsPathRooted($PolicyPath)) {
    $PolicyPath
}
else {
    Join-Path $repoRoot $PolicyPath
}

if (-not (Test-Path -LiteralPath $resolvedPolicyPath -PathType Leaf)) {
    throw "Branch retention policy was not found: $resolvedPolicyPath"
}

$policy = Get-Content -LiteralPath $resolvedPolicyPath -Raw | ConvertFrom-Json

$activeBranches = @($policy.retentionClasses.active.branches)
$disposablePatterns = @($policy.retentionClasses.disposable.branchPatterns)
$requirePublishedRelease = [bool]$policy.retentionClasses.releaseEvidence.requirePublishedRelease

$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()
$removable = [System.Collections.Generic.List[string]]::new()

Write-Host "Auditing branch retention for $Repository against $PolicyPath."
Write-Host ''

# --- Release evidence -------------------------------------------------------

$tags = @(Invoke-GitHubApi -Arguments @('--paginate', "repos/$Repository/tags"))
$releases = @(Invoke-GitHubApi -Arguments @('--paginate', "repos/$Repository/releases"))

$tagNames = @($tags | ForEach-Object { $_.name })
$releaseTagNames = @($releases | ForEach-Object { $_.tag_name })

Write-Host "Release evidence: $($tagNames.Count) tags, $($releaseTagNames.Count) published releases."

if ($requirePublishedRelease) {
    foreach ($tagName in $tagNames) {
        if ($releaseTagNames -notcontains $tagName) {
            $warnings.Add("Tag '$tagName' has no published release. Release evidence for that version rests on the tag alone.")
        }
    }
}

# --- Repository setting -----------------------------------------------------

$repositoryMetadata = Invoke-GitHubApi -Arguments @("repos/$Repository")
$desiredDeleteOnMerge = [bool]$policy.repositorySettings.deleteBranchOnMerge
$actualDeleteOnMerge = [bool]$repositoryMetadata.delete_branch_on_merge

if ($actualDeleteOnMerge -ne $desiredDeleteOnMerge) {
    if ($Apply) {
        if ($PSCmdlet.ShouldProcess($Repository, "Set delete_branch_on_merge to $desiredDeleteOnMerge")) {
            $literal = if ($desiredDeleteOnMerge) { 'true' } else { 'false' }
            Invoke-GitHubApi -Arguments @(
                '--method', 'PATCH',
                "repos/$Repository",
                '-F', "delete_branch_on_merge=$literal"
            ) | Out-Null

            Write-Host "Set delete_branch_on_merge to $desiredDeleteOnMerge."
        }
    }
    else {
        $failures.Add(
            "Repository setting delete_branch_on_merge is $actualDeleteOnMerge but the policy requires $desiredDeleteOnMerge. " +
            'Merged work branches will accumulate until this is enabled.'
        )
    }
}
else {
    Write-Host "Repository setting delete_branch_on_merge is $actualDeleteOnMerge, matching policy."
}

Write-Host ''

# --- Branch classification --------------------------------------------------

$branches = @(Invoke-GitHubApi -Arguments @('--paginate', "repos/$Repository/branches"))

Write-Host "Classifying $($branches.Count) branches."

foreach ($branch in $branches) {
    $name = $branch.name

    if ($activeBranches -contains $name) {
        Write-Host "  active       $name"
        continue
    }

    if (-not (Test-BranchPattern -Name $name -Patterns $disposablePatterns)) {
        $failures.Add(
            "Branch '$name' matches no retention class. Add it to the active list or to a disposable pattern in $PolicyPath " +
            'so its retention is a recorded decision rather than an accident.'
        )
        Write-Host "  UNCLASSIFIED $name"
        continue
    }

    # A disposable branch is removable only when nothing unique lives on it.
    $comparison = Invoke-GitHubApi -Arguments @(
        "repos/$Repository/compare/main...$name"
    ) -AllowNotFound

    if ($null -eq $comparison) {
        $failures.Add("Could not compare branch '$name' against main. Resolve manually before pruning.")
        continue
    }

    $aheadBy = [int]$comparison.ahead_by

    if ($aheadBy -eq 0) {
        Write-Host "  removable    $name (fully merged into main)"
        $removable.Add($name)
        continue
    }

    # Commits unreachable from main may still be preserved by a release tag.
    # A release branch almost always maps to the tag named for its version, so
    # try that first and fall back to scanning only when it does not cover the
    # branch. Scanning every tag for every branch is O(branches x tags) API calls.
    $candidateTags = [System.Collections.Generic.List[string]]::new()

    $derivedTag = if ($name -match '(?<version>\d+\.\d+\.\d+.*)$') {
        "v$($Matches['version'])"
    }
    else {
        $null
    }

    if ($null -ne $derivedTag -and $tagNames -contains $derivedTag) {
        $candidateTags.Add($derivedTag)
    }

    foreach ($tagName in $tagNames) {
        if (-not $candidateTags.Contains($tagName)) {
            $candidateTags.Add($tagName)
        }
    }

    $coveringTag = $null

    foreach ($tagName in $candidateTags) {
        $tagComparison = Invoke-GitHubApi -Arguments @(
            "repos/$Repository/compare/$tagName...$name"
        ) -AllowNotFound

        if ($null -ne $tagComparison -and [int]$tagComparison.ahead_by -eq 0) {
            $coveringTag = $tagName
            break
        }
    }

    if ($null -eq $coveringTag) {
        $failures.Add(
            "Branch '$name' is $aheadBy commit(s) ahead of main and is not covered by any release tag. " +
            'Merge or tag that work before pruning; deleting it would lose commits.'
        )
        Write-Host "  RETAIN       $name (ahead $aheadBy, no covering tag)"
        continue
    }

    if ($requirePublishedRelease -and ($releaseTagNames -notcontains $coveringTag)) {
        $failures.Add(
            "Branch '$name' is covered by tag '$coveringTag', but that tag has no published release. " +
            'Publish the release before pruning the branch.'
        )
        Write-Host "  RETAIN       $name (tag $coveringTag has no published release)"
        continue
    }

    Write-Host "  removable    $name (covered by $coveringTag)"
    $removable.Add($name)
}

Write-Host ''

# --- Prune ------------------------------------------------------------------

if ($removable.Count -eq 0) {
    Write-Host 'No disposable branches are pending removal.'
}
elseif ($Apply) {
    foreach ($name in $removable) {
        if ($PSCmdlet.ShouldProcess("$Repository branch $name", 'Delete remote branch')) {
            Invoke-GitHubApi -Arguments @(
                '--method', 'DELETE',
                "repos/$Repository/git/refs/heads/$name"
            ) | Out-Null

            Write-Host "Deleted branch '$name'."
        }
    }
}
else {
    Write-Host "$($removable.Count) branch(es) are verified removable. Re-run with -Apply -WhatIf to preview deletion."
}

foreach ($warningMessage in $warnings) {
    Write-Warning $warningMessage
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "ERROR: $failure" -ForegroundColor Red
    }

    throw "$($failures.Count) branch retention check(s) failed."
}

Write-Host 'Branch retention audit passed. Every branch is classified and all release evidence is intact.'
