[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [ValidatePattern('^[^/]+/[^/]+$')]
    [string]$Repository = 'AsiBackbone/AsiBackbone',

    [ValidateRange(0, 720)]
    [int]$ReviewWindowHours = 72,

    [switch]$RequestMissingCves,

    [switch]$Force
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Assert-GitHubCli {
    $gh = Get-Command gh -ErrorAction SilentlyContinue

    if ($null -eq $gh) {
        throw 'GitHub CLI (gh) is required. Install it and authenticate before running this script.'
    }

    & gh auth status 1>$null 2>$null
    if ($LASTEXITCODE -ne 0) {
        throw 'GitHub CLI is not authenticated. Run gh auth login with an account that can read repository security advisories.'
    }
}

function Invoke-GitHubApi {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [switch]$AllowNotFound
    )

    $stderrPath = [System.IO.Path]::GetTempFileName()

    try {
        $apiArguments = @(
            '-H',
            'Accept: application/vnd.github+json',
            '-H',
            'X-GitHub-Api-Version: 2022-11-28'
        ) + $Arguments

        $output = @(& gh api @apiArguments 2> $stderrPath)
        $exitCode = $LASTEXITCODE
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
                $stderr.Trim()
            }

            throw $message
        }

        if ($output.Count -eq 0) {
            return $null
        }

        $json = $output -join [System.Environment]::NewLine
        if ([string]::IsNullOrWhiteSpace($json)) {
            return $null
        }

        return ($json | ConvertFrom-Json)
    }
    finally {
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue
    }
}

function Get-OptionalPropertyValue {
    param(
        [Parameter(Mandatory = $true)]
        [object]$InputObject,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

Assert-GitHubCli

$advisories = [System.Collections.Generic.List[object]]::new()
$page = 1

while ($true) {
    $pageItems = @(
        Invoke-GitHubApi -Arguments @(
            "repos/$Repository/security-advisories?per_page=100&page=$page"
        )
    )

    foreach ($item in $pageItems) {
        $advisories.Add($item)
    }

    if ($pageItems.Count -lt 100) {
        break
    }

    $page++
}

$publishedAdvisories = @(
    $advisories |
        Where-Object {
            (Get-OptionalPropertyValue -InputObject $_ -Name 'state') -eq 'published'
        }
)

if ($publishedAdvisories.Count -eq 0) {
    Write-Host "No published repository security advisories were found for $Repository."
    exit 0
}

$results = [System.Collections.Generic.List[object]]::new()
$now = [System.DateTimeOffset]::UtcNow

foreach ($advisory in $publishedAdvisories) {
    $ghsaId = [string](Get-OptionalPropertyValue -InputObject $advisory -Name 'ghsa_id')
    if ([string]::IsNullOrWhiteSpace($ghsaId)) {
        throw 'A published repository advisory did not contain a ghsa_id.'
    }

    $publishedAtText = [string](Get-OptionalPropertyValue -InputObject $advisory -Name 'published_at')
    $publishedAt = if ([string]::IsNullOrWhiteSpace($publishedAtText)) {
        $null
    }
    else {
        [System.DateTimeOffset]::Parse($publishedAtText)
    }

    $ageHours = if ($null -eq $publishedAt) {
        [double]::PositiveInfinity
    }
    else {
        [Math]::Round(($now - $publishedAt).TotalHours, 1)
    }

    $reviewWindowExpired = $ageHours -ge $ReviewWindowHours

    $globalAdvisory = Invoke-GitHubApi -Arguments @(
        "advisories/$ghsaId"
    ) -AllowNotFound

    $isGlobal = $null -ne $globalAdvisory
    $cveId = [string](Get-OptionalPropertyValue -InputObject $advisory -Name 'cve_id')
    $cveRequestSubmittedAt = [string](
        Get-OptionalPropertyValue -InputObject $advisory -Name 'cve_request_submitted_at'
    )

    $action = if ($isGlobal) {
        'No action required'
    }
    elseif (-not [string]::IsNullOrWhiteSpace($cveRequestSubmittedAt)) {
        'CVE request pending; continue global-database verification'
    }
    elseif (-not $reviewWindowExpired) {
        "Within GitHub review window (< $ReviewWindowHours hours)"
    }
    else {
        'Global entry missing; CVE request is available'
    }

    if (
        $RequestMissingCves -and
        -not $isGlobal -and
        [string]::IsNullOrWhiteSpace($cveId) -and
        [string]::IsNullOrWhiteSpace($cveRequestSubmittedAt)
    ) {
        if (-not $reviewWindowExpired -and -not $Force) {
            Write-Warning (
                "$ghsaId was published $ageHours hours ago. GitHub documents that " +
                "repository-advisory review for the global database can take up to " +
                "$ReviewWindowHours hours. Use -Force only when an immediate CVE request is intentional."
            )
        }
        elseif (
            $PSCmdlet.ShouldProcess(
                "$Repository advisory $ghsaId",
                'Request a CVE from GitHub'
            )
        ) {
            Invoke-GitHubApi -Arguments @(
                '--method',
                'POST',
                "repos/$Repository/security-advisories/$ghsaId/cve"
            ) | Out-Null

            $cveRequestSubmittedAt = 'submitted during this run'
            $action = 'CVE request submitted; recheck the global database later'
        }
    }

    $status = if ($isGlobal) {
        'Global'
    }
    elseif (-not [string]::IsNullOrWhiteSpace($cveRequestSubmittedAt)) {
        'CVE request pending'
    }
    elseif ($reviewWindowExpired) {
        'Global entry missing'
    }
    else {
        'GitHub review pending'
    }

    $results.Add(
        [pscustomobject]@{
            Advisory = $ghsaId
            Status = $status
            CVE = if ([string]::IsNullOrWhiteSpace($cveId)) { '-' } else { $cveId }
            PublishedAgeHours = $ageHours
            Global = $isGlobal
            ReviewWindowExpired = $reviewWindowExpired
            CveRequestSubmitted = -not [string]::IsNullOrWhiteSpace($cveRequestSubmittedAt)
            Action = $action
        }
    )
}

$results |
    Sort-Object Advisory |
    Format-Table Advisory, Status, CVE, PublishedAgeHours, Action -AutoSize

$missingGlobal = @($results | Where-Object { -not $_.Global })

if ($missingGlobal.Count -eq 0) {
    Write-Host "All published repository advisories resolve through the global GitHub Advisory Database."
    exit 0
}

if ($RequestMissingCves) {
    if ($WhatIfPreference) {
        Write-Host 'WhatIf preview completed. No CVE requests were submitted.'
        exit 0
    }

    $expiredWithoutRequest = @(
        $missingGlobal |
            Where-Object {
                $_.ReviewWindowExpired -and -not $_.CveRequestSubmitted
            }
    )

    if ($expiredWithoutRequest.Count -gt 0) {
        Write-Error (
            "$($expiredWithoutRequest.Count) published advisories remain outside the global database " +
            "after the review window and do not have a CVE request recorded."
        )
        exit 1
    }

    Write-Warning (
        "$($missingGlobal.Count) published advisories are still absent from the global database. " +
        'CVE requests that were already pending or submitted by this run still require follow-up verification.'
    )
    exit 0
}

$expiredMissingGlobal = @(
    $missingGlobal | Where-Object { $_.ReviewWindowExpired }
)

if ($expiredMissingGlobal.Count -gt 0) {
    Write-Error (
        "$($expiredMissingGlobal.Count) published advisories remain absent from the global database " +
        "after the $ReviewWindowHours-hour review window. Re-run with -RequestMissingCves " +
        '(use -WhatIf first) and continue tracking until the global endpoint resolves each GHSA.'
    )
    exit 1
}

Write-Warning (
    "$($missingGlobal.Count) published advisories are not yet in the global database, but all remain " +
    "inside the documented $ReviewWindowHours-hour review window. Re-run this check after the window expires."
)
exit 0
