[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [ValidatePattern('^[^/]+/[^/]+$')]
    [string]$Repository = 'AsiBackbone/AsiBackbone',

    [ValidateRange(0, 720)]
    [int]$ReviewWindowHours = 72,

    [switch]$RequestMissingCves,

    [switch]$Force,

    # Durable record of CVE requests this script has submitted. GitHub's CVE
    # request endpoint returns 202 Accepted and populates
    # cve_request_submitted_at asynchronously, so the API can report null for a
    # request that was genuinely accepted. Without a local record, every run
    # would resubmit, and the endpoint returns 422 when it is spammed.
    [string]$StatePath = (
        Join-Path -Path $PSScriptRoot -ChildPath '..\eng\security-advisory-cve-requests.json'
    ),

    # Deliberately resubmit advisories that were recorded locally but that
    # GitHub has still not reflected. Use only after confirming with GitHub
    # Support that the original request was lost.
    [switch]$ResubmitUnconfirmed,

    [ValidateRange(0, 20)]
    [int]$ConfirmationAttempts = 5,

    [ValidateRange(0, 60)]
    [int]$ConfirmationDelaySeconds = 3
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

        # Windows PowerShell can promote native stderr to a NativeCommandError
        # when ErrorActionPreference is Stop. That would terminate here before
        # AllowNotFound can inspect gh's exit code and captured 404 response.
        # Keep the strict preference for the rest of the script, but allow this
        # native command to return normally so its result can be classified.
        $previousWhatIfPreference = $WhatIfPreference

        try {
            $WhatIfPreference = $false

            $previousErrorActionPreference = $ErrorActionPreference
            try {
                $ErrorActionPreference = 'Continue'
                $output = @(& gh api @apiArguments 2> $stderrPath)
                $exitCode = $LASTEXITCODE
            }
            finally {
                $ErrorActionPreference = $previousErrorActionPreference
            }
        }
        finally {
            $WhatIfPreference = $previousWhatIfPreference
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
        Remove-Item -LiteralPath $stderrPath -Force -ErrorAction SilentlyContinue -WhatIf:$false
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

function Get-CveRequestLedger {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $ledger = [ordered]@{}

    if (-not (Test-Path -LiteralPath $Path)) {
        return $ledger
    }

    $raw = Get-Content -LiteralPath $Path -Raw
    if ([string]::IsNullOrWhiteSpace($raw)) {
        return $ledger
    }

    $parsed = $raw | ConvertFrom-Json

    foreach ($property in $parsed.PSObject.Properties) {
        $ledger[$property.Name] = $property.Value
    }

    return $ledger
}

function Save-CveRequestLedger {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [object]$Ledger
    )

    $directory = Split-Path -Parent -Path $Path
    if (-not [string]::IsNullOrWhiteSpace($directory) -and -not (Test-Path -LiteralPath $directory)) {
        New-Item -ItemType Directory -Path $directory -Force -WhatIf:$false | Out-Null
    }

    $json = $Ledger | ConvertTo-Json -Depth 5
    Set-Content -LiteralPath $Path -Value $json -Encoding utf8 -WhatIf:$false
}

function Get-RecordedCveRequestTimestamp {
    <#
        .SYNOPSIS
        Re-reads the advisory from GitHub and returns the server-recorded
        cve_request_submitted_at value, or $null when GitHub has not reflected
        the request yet.

        .DESCRIPTION
        Success is never inferred from the POST itself. The CVE request endpoint
        returns 202 Accepted with an empty body, which means the request was
        queued for processing, not that it was recorded. Only a subsequent read
        of the advisory establishes that GitHub holds the request.
    #>
    param(
        [Parameter(Mandatory = $true)]
        [string]$Repository,

        [Parameter(Mandatory = $true)]
        [string]$GhsaId,

        [Parameter(Mandatory = $true)]
        [int]$Attempts,

        [Parameter(Mandatory = $true)]
        [int]$DelaySeconds
    )

    for ($attempt = 1; $attempt -le $Attempts; $attempt++) {
        if ($DelaySeconds -gt 0) {
            Start-Sleep -Seconds $DelaySeconds
        }

        $current = Invoke-GitHubApi -Arguments @(
            "repos/$Repository/security-advisories/$GhsaId"
        ) -AllowNotFound

        if ($null -ne $current) {
            $recorded = [string](
                Get-OptionalPropertyValue -InputObject $current -Name 'cve_request_submitted_at'
            )

            if (-not [string]::IsNullOrWhiteSpace($recorded)) {
                return $recorded
            }
        }
    }

    return $null
}

Assert-GitHubCli

$ledger = Get-CveRequestLedger -Path $StatePath

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

    $ledgerEntry = if ($ledger.Contains($ghsaId)) { $ledger[$ghsaId] } else { $null }
    $locallySubmitted = $null -ne $ledgerEntry
    $locallySubmittedAt = if ($locallySubmitted) {
        [string](Get-OptionalPropertyValue -InputObject $ledgerEntry -Name 'submittedAt')
    }
    else {
        $null
    }

    $action = if ($isGlobal) {
        'No action required'
    }
    elseif (-not [string]::IsNullOrWhiteSpace($cveRequestSubmittedAt)) {
        'CVE request recorded by GitHub; continue global-database verification'
    }
    elseif ($locallySubmitted) {
        "CVE request submitted $locallySubmittedAt but not yet recorded by GitHub; do not resubmit"
    }
    elseif (-not $reviewWindowExpired) {
        "Within GitHub review window (< $ReviewWindowHours hours)"
    }
    else {
        'Global entry missing; CVE request is available'
    }

    $needsRequest = (
        -not $isGlobal -and
        [string]::IsNullOrWhiteSpace($cveId) -and
        [string]::IsNullOrWhiteSpace($cveRequestSubmittedAt)
    )

    if ($RequestMissingCves -and $needsRequest) {
        if ($locallySubmitted -and -not $ResubmitUnconfirmed) {
            Write-Warning (
                "$ghsaId already has a CVE request submitted at $locallySubmittedAt that GitHub has " +
                'not recorded yet. The request endpoint returns 422 when it is spammed, so this run ' +
                'is not resubmitting. Use -ResubmitUnconfirmed only after confirming with GitHub ' +
                'Support that the original request was lost.'
            )
        }
        elseif (-not $reviewWindowExpired -and -not $Force) {
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
            $submittedAt = [System.DateTimeOffset]::UtcNow.ToString('o')

            Invoke-GitHubApi -Arguments @(
                '--method',
                'POST',
                "repos/$Repository/security-advisories/$ghsaId/cve"
            ) | Out-Null

            # Record the attempt before verifying it. If verification or the rest
            # of this run fails, the ledger must still prevent a duplicate
            # submission on the next run.
            $ledger[$ghsaId] = [pscustomobject]@{
                submittedAt = $submittedAt
                confirmedAt = $null
                repository  = $Repository
            }
            Save-CveRequestLedger -Path $StatePath -Ledger $ledger

            $locallySubmitted = $true
            $locallySubmittedAt = $submittedAt

            $recorded = Get-RecordedCveRequestTimestamp -Repository $Repository -GhsaId $ghsaId -Attempts $ConfirmationAttempts -DelaySeconds $ConfirmationDelaySeconds

            if (-not [string]::IsNullOrWhiteSpace($recorded)) {
                $cveRequestSubmittedAt = $recorded

                $ledger[$ghsaId] = [pscustomobject]@{
                    submittedAt = $submittedAt
                    confirmedAt = $recorded
                    repository  = $Repository
                }
                Save-CveRequestLedger -Path $StatePath -Ledger $ledger

                $action = 'CVE request confirmed by GitHub; continue global-database verification'
            }
            else {
                $action = 'CVE request accepted (HTTP 202) but GitHub has not recorded it yet; re-check read-only, do not resubmit'
            }
        }
    }

    $status = if ($isGlobal) {
        'Global'
    }
    elseif (-not [string]::IsNullOrWhiteSpace($cveId)) {
        'CVE assigned'
    }
    elseif (-not [string]::IsNullOrWhiteSpace($cveRequestSubmittedAt)) {
        'CVE request recorded'
    }
    elseif ($locallySubmitted) {
        'CVE request unconfirmed'
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
            CveRequestRecorded = -not [string]::IsNullOrWhiteSpace($cveRequestSubmittedAt)
            CveRequestSubmittedLocally = $locallySubmitted
            LocalSubmittedAt = $locallySubmittedAt
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

$unconfirmed = @(
    $missingGlobal |
        Where-Object { $_.CveRequestSubmittedLocally -and -not $_.CveRequestRecorded }
)

$expiredWithoutRequest = @(
    $missingGlobal |
        Where-Object {
            $_.ReviewWindowExpired -and
            -not $_.CveRequestRecorded -and
            -not $_.CveRequestSubmittedLocally
        }
)

if ($RequestMissingCves -and $WhatIfPreference) {
    Write-Host 'WhatIf preview completed. No CVE requests were submitted.'
    exit 0
}

if ($expiredWithoutRequest.Count -gt 0) {
    Write-Error (
        "$($expiredWithoutRequest.Count) published advisories remain outside the global database " +
        "after the $ReviewWindowHours-hour review window with no CVE request on record, either at " +
        'GitHub or in the local ledger. Run once with -RequestMissingCves (use -WhatIf first) to ' +
        'submit them.'
    )
    exit 1
}

if ($unconfirmed.Count -gt 0) {
    Write-Warning (
        "$($unconfirmed.Count) advisories have a CVE request recorded locally that GitHub has not " +
        'reflected in cve_request_submitted_at. The endpoint returns 202 Accepted and is processed ' +
        'asynchronously, so this is expected shortly after submission. Re-run this script read-only ' +
        '(without -RequestMissingCves) to track it. Do not resubmit: repeated requests can trigger ' +
        'the endpoint 422 spam response. If GitHub has still not recorded the request after 48 ' +
        'hours, contact GitHub Support with the affected GHSA identifiers.'
    )
    exit 0
}

Write-Warning (
    "$($missingGlobal.Count) published advisories are not yet in the global database. CVE requests " +
    'are recorded for them; continue verifying until the global endpoint resolves each GHSA.'
)
exit 0
