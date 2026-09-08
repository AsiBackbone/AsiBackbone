[CmdletBinding(SupportsShouldProcess = $true, ConfirmImpact = 'High')]
param(
    [ValidatePattern('^[^/]+/[^/]+$')]
    [string]$Repository = 'AsiBackbone/AsiBackbone',

    [string]$RulesetPath = 'eng/repository-controls/main-branch-ruleset.json',

    [switch]$Apply
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
        throw 'GitHub CLI is not authenticated. Run gh auth login with an account that can administer the repository.'
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
            'X-GitHub-Api-Version: 2026-03-10'
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

function Invoke-GitHubApiWithBody {
    param(
        [Parameter(Mandatory = $true)]
        [ValidateSet('POST', 'PATCH', 'PUT')]
        [string]$Method,

        [Parameter(Mandatory = $true)]
        [string]$Endpoint,

        [Parameter(Mandatory = $true)]
        [object]$Body
    )

    $bodyPath = [System.IO.Path]::GetTempFileName()

    try {
        $json = $Body | ConvertTo-Json -Depth 50
        $utf8NoBom = [System.Text.UTF8Encoding]::new($false)
        [System.IO.File]::WriteAllText($bodyPath, $json, $utf8NoBom)

        return Invoke-GitHubApi -Arguments @(
            '--method',
            $Method,
            $Endpoint,
            '--input',
            $bodyPath
        )
    }
    finally {
        Remove-Item -LiteralPath $bodyPath -Force -ErrorAction SilentlyContinue
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

function Get-SecurityFeatureStatus {
    param(
        [Parameter(Mandatory = $true)]
        [object]$SecurityAndAnalysis,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $feature = Get-OptionalPropertyValue -InputObject $SecurityAndAnalysis -Name $Name
    if ($null -eq $feature) {
        return $null
    }

    return [string](Get-OptionalPropertyValue -InputObject $feature -Name 'status')
}

function Test-StringSetEqual {
    param(
        [object[]]$Expected,
        [object[]]$Actual
    )

    $expectedValues = @($Expected | ForEach-Object { [string]$_ } | Sort-Object -Unique)
    $actualValues = @($Actual | ForEach-Object { [string]$_ } | Sort-Object -Unique)

    if ($expectedValues.Count -eq 0 -and $actualValues.Count -eq 0) {
        return $true
    }

    if ($expectedValues.Count -eq 0 -or $actualValues.Count -eq 0) {
        return $false
    }

    return @(Compare-Object -ReferenceObject $expectedValues -DifferenceObject $actualValues).Count -eq 0
}

function Get-RuleByType {
    param(
        [Parameter(Mandatory = $true)]
        [object[]]$Rules,

        [Parameter(Mandatory = $true)]
        [string]$Type
    )

    return @($Rules | Where-Object { [string]$_.type -eq $Type })
}

function Add-Failure {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[string]]$Failures,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $Failures.Add($Message)
}

function Add-WarningMessage {
    param(
        [Parameter(Mandatory = $true)]
        [AllowEmptyCollection()]
        [System.Collections.Generic.List[string]]$Warnings,

        [Parameter(Mandatory = $true)]
        [string]$Message
    )

    $Warnings.Add($Message)
}

Assert-GitHubCli

if (-not (Test-Path -LiteralPath $RulesetPath -PathType Leaf)) {
    throw "Repository ruleset manifest was not found at '$RulesetPath'."
}

$desiredRuleset = Get-Content -LiteralPath $RulesetPath -Raw | ConvertFrom-Json
if ([string]$desiredRuleset.name -ne 'Main branch security baseline') {
    throw "Ruleset manifest '$RulesetPath' does not use the expected repository ruleset name."
}

if ($Apply) {
    $mandatorySecurity = @{
        security_and_analysis = @{
            secret_scanning = @{
                status = 'enabled'
            }
            secret_scanning_push_protection = @{
                status = 'enabled'
            }
        }
    }

    if ($PSCmdlet.ShouldProcess($Repository, 'Enable secret scanning and secret-scanning push protection')) {
        Invoke-GitHubApiWithBody -Method PATCH -Endpoint "repos/$Repository" -Body $mandatorySecurity | Out-Null
        Write-Host 'Enabled mandatory secret scanning and push protection settings.'
    }

    foreach ($optionalFeature in @(
        'secret_scanning_non_provider_patterns',
        'secret_scanning_validity_checks'
    )) {
        $optionalSecurity = @{
            security_and_analysis = @{}
        }
        $optionalSecurity.security_and_analysis[$optionalFeature] = @{
            status = 'enabled'
        }

        if ($PSCmdlet.ShouldProcess($Repository, "Enable optional GitHub security feature '$optionalFeature'")) {
            try {
                Invoke-GitHubApiWithBody -Method PATCH -Endpoint "repos/$Repository" -Body $optionalSecurity | Out-Null
                Write-Host "Enabled optional GitHub security feature '$optionalFeature'."
            }
            catch {
                Write-Warning (
                    "GitHub did not enable optional feature '$optionalFeature'. " +
                    "The repository/plan may not expose it. Mandatory push protection is applied independently. " +
                    "GitHub response: $($_.Exception.Message)"
                )
            }
        }
    }

    $rulesets = @(Invoke-GitHubApi -Arguments @("repos/$Repository/rulesets"))
    $matchingRulesets = @($rulesets | Where-Object { [string]$_.name -eq [string]$desiredRuleset.name })

    if ($matchingRulesets.Count -gt 1) {
        throw "More than one repository ruleset is named '$($desiredRuleset.name)'. Resolve the duplicate before applying desired state."
    }

    if ($matchingRulesets.Count -eq 0) {
        if ($PSCmdlet.ShouldProcess($Repository, "Create active ruleset '$($desiredRuleset.name)'")) {
            Invoke-GitHubApiWithBody -Method POST -Endpoint "repos/$Repository/rulesets" -Body $desiredRuleset | Out-Null
            Write-Host "Created repository ruleset '$($desiredRuleset.name)'."
        }
    }
    else {
        $rulesetId = [long]$matchingRulesets[0].id
        if ($PSCmdlet.ShouldProcess($Repository, "Update ruleset '$($desiredRuleset.name)' ($rulesetId)")) {
            Invoke-GitHubApiWithBody -Method PUT -Endpoint "repos/$Repository/rulesets/$rulesetId" -Body $desiredRuleset | Out-Null
            Write-Host "Updated repository ruleset '$($desiredRuleset.name)'."
        }
    }

    if ($WhatIfPreference) {
        Write-Host 'WhatIf preview completed. No repository settings were changed.'
        exit 0
    }
}

$failures = [System.Collections.Generic.List[string]]::new()
$warnings = [System.Collections.Generic.List[string]]::new()

$repositoryState = Invoke-GitHubApi -Arguments @("repos/$Repository")
$securityAndAnalysis = Get-OptionalPropertyValue -InputObject $repositoryState -Name 'security_and_analysis'

if ($null -eq $securityAndAnalysis) {
    Add-Failure -Failures $failures -Message (
        'GitHub did not return security_and_analysis state. Authenticate gh with a repository administrator ' +
        'or security manager so secret-scanning settings can be audited.'
    )
}
else {
    foreach ($mandatoryFeature in @('secret_scanning', 'secret_scanning_push_protection')) {
        $status = Get-SecurityFeatureStatus -SecurityAndAnalysis $securityAndAnalysis -Name $mandatoryFeature
        if ($status -ne 'enabled') {
            Add-Failure -Failures $failures -Message "Mandatory GitHub security feature '$mandatoryFeature' is '$status' instead of 'enabled'."
        }
        else {
            Write-Host "GitHub security feature '$mandatoryFeature' is enabled."
        }
    }

    foreach ($optionalFeature in @(
        'secret_scanning_non_provider_patterns',
        'secret_scanning_validity_checks'
    )) {
        $status = Get-SecurityFeatureStatus -SecurityAndAnalysis $securityAndAnalysis -Name $optionalFeature
        if ([string]::IsNullOrWhiteSpace($status)) {
            Add-WarningMessage -Warnings $warnings -Message "Optional GitHub security feature '$optionalFeature' is not exposed for this repository/plan."
        }
        elseif ($status -ne 'enabled') {
            Add-WarningMessage -Warnings $warnings -Message "Optional GitHub security feature '$optionalFeature' is '$status'; the recorded preference is enabled when available."
        }
        else {
            Write-Host "Optional GitHub security feature '$optionalFeature' is enabled."
        }
    }
}

$rulesets = @(Invoke-GitHubApi -Arguments @("repos/$Repository/rulesets"))
$matchingRulesets = @($rulesets | Where-Object { [string]$_.name -eq [string]$desiredRuleset.name })

if ($matchingRulesets.Count -ne 1) {
    Add-Failure -Failures $failures -Message "Expected exactly one active repository ruleset named '$($desiredRuleset.name)'; found $($matchingRulesets.Count)."
}
else {
    $rulesetId = [long]$matchingRulesets[0].id
    $actualRuleset = Invoke-GitHubApi -Arguments @("repos/$Repository/rulesets/$rulesetId")

    if ([string]$actualRuleset.target -ne [string]$desiredRuleset.target) {
        Add-Failure -Failures $failures -Message "Ruleset target is '$($actualRuleset.target)' instead of '$($desiredRuleset.target)'."
    }

    if ([string]$actualRuleset.enforcement -ne 'active') {
        Add-Failure -Failures $failures -Message "Ruleset enforcement is '$($actualRuleset.enforcement)' instead of 'active'."
    }

    $expectedIncludes = @($desiredRuleset.conditions.ref_name.include)
    $actualIncludes = @($actualRuleset.conditions.ref_name.include)
    $expectedExcludes = @($desiredRuleset.conditions.ref_name.exclude)
    $actualExcludes = @($actualRuleset.conditions.ref_name.exclude)

    if (-not (Test-StringSetEqual -Expected $expectedIncludes -Actual $actualIncludes)) {
        Add-Failure -Failures $failures -Message 'Ruleset target-branch include conditions differ from the committed manifest.'
    }

    if (-not (Test-StringSetEqual -Expected $expectedExcludes -Actual $actualExcludes)) {
        Add-Failure -Failures $failures -Message 'Ruleset target-branch exclude conditions differ from the committed manifest.'
    }

    $expectedBypass = @(
        $desiredRuleset.bypass_actors |
            ForEach-Object { "$($_.actor_type)|$($_.actor_id)|$($_.bypass_mode)" }
    )
    $actualBypass = @(
        $actualRuleset.bypass_actors |
            ForEach-Object { "$($_.actor_type)|$($_.actor_id)|$($_.bypass_mode)" }
    )

    if (-not (Test-StringSetEqual -Expected $expectedBypass -Actual $actualBypass)) {
        Add-Failure -Failures $failures -Message 'Ruleset bypass actors differ from the committed repository-specific pull-request-only bypass list.'
    }

    $expectedRules = @($desiredRuleset.rules)
    $actualRules = @($actualRuleset.rules)
    $expectedRuleTypes = @($expectedRules | ForEach-Object { [string]$_.type })
    $actualRuleTypes = @($actualRules | ForEach-Object { [string]$_.type })

    if (-not (Test-StringSetEqual -Expected $expectedRuleTypes -Actual $actualRuleTypes)) {
        Add-Failure -Failures $failures -Message 'Ruleset rule types differ from the committed manifest (including the deliberate required-signatures deferral).'
    }

    $expectedPullRules = @(Get-RuleByType -Rules $expectedRules -Type 'pull_request')
    $actualPullRules = @(Get-RuleByType -Rules $actualRules -Type 'pull_request')
    if ($expectedPullRules.Count -ne 1 -or $actualPullRules.Count -ne 1) {
        Add-Failure -Failures $failures -Message (
            "Expected exactly one pull_request rule; manifest has $($expectedPullRules.Count) and GitHub has $($actualPullRules.Count)."
        )
    }
    else {
        $expectedPull = $expectedPullRules[0].parameters
        $actualPull = $actualPullRules[0].parameters

        foreach ($propertyName in @(
            'dismiss_stale_reviews_on_push',
            'require_code_owner_review',
            'require_last_push_approval',
            'required_approving_review_count',
            'required_review_thread_resolution'
        )) {
            if ((Get-OptionalPropertyValue -InputObject $actualPull -Name $propertyName) -ne (Get-OptionalPropertyValue -InputObject $expectedPull -Name $propertyName)) {
                Add-Failure -Failures $failures -Message "Pull-request ruleset parameter '$propertyName' differs from the committed manifest."
            }
        }

        if (-not (Test-StringSetEqual -Expected @($expectedPull.allowed_merge_methods) -Actual @($actualPull.allowed_merge_methods))) {
            Add-Failure -Failures $failures -Message 'Allowed merge methods differ from the committed squash-only policy.'
        }
    }

    $expectedStatusRules = @(Get-RuleByType -Rules $expectedRules -Type 'required_status_checks')
    $actualStatusRules = @(Get-RuleByType -Rules $actualRules -Type 'required_status_checks')
    if ($expectedStatusRules.Count -ne 1 -or $actualStatusRules.Count -ne 1) {
        Add-Failure -Failures $failures -Message (
            "Expected exactly one required_status_checks rule; manifest has $($expectedStatusRules.Count) and GitHub has $($actualStatusRules.Count)."
        )
    }
    else {
        $expectedStatus = $expectedStatusRules[0].parameters
        $actualStatus = $actualStatusRules[0].parameters

        if ([bool]$actualStatus.strict_required_status_checks_policy -ne [bool]$expectedStatus.strict_required_status_checks_policy) {
            Add-Failure -Failures $failures -Message 'Strict required-status-check policy differs from the committed manifest.'
        }

        $expectedChecks = @(
            $expectedStatus.required_status_checks |
                ForEach-Object { "$($_.context)|$($_.integration_id)" }
        )
        $actualChecks = @(
            $actualStatus.required_status_checks |
                ForEach-Object { "$($_.context)|$($_.integration_id)" }
        )

        if (-not (Test-StringSetEqual -Expected $expectedChecks -Actual $actualChecks)) {
            Add-Failure -Failures $failures -Message 'Required status checks or their trusted integration ids differ from the committed manifest.'
        }
    }

    if ($failures.Count -eq 0) {
        Write-Host "Repository ruleset '$($desiredRuleset.name)' matches the committed desired state."
    }
}

$legacyProtection = Invoke-GitHubApi -Arguments @("repos/$Repository/branches/main/protection") -AllowNotFound
if ($null -ne $legacyProtection) {
    $enforceAdmins = Get-OptionalPropertyValue -InputObject $legacyProtection -Name 'enforce_admins'
    $legacyAdminEnforcement = if ($null -eq $enforceAdmins) {
        'unknown'
    }
    else {
        [string](Get-OptionalPropertyValue -InputObject $enforceAdmins -Name 'enabled')
    }

    Write-Host (
        "Legacy main branch protection remains present (enforce_admins=$legacyAdminEnforcement). " +
        'It may remain as defense in depth; the explicit ruleset is the canonical administrator/bypass control.'
    )
}

foreach ($warningMessage in $warnings) {
    Write-Warning $warningMessage
}

if ($failures.Count -gt 0) {
    foreach ($failure in $failures) {
        Write-Host "ERROR: $failure" -ForegroundColor Red
    }

    throw "$($failures.Count) repository security control checks failed. Use -Apply -WhatIf to preview remediation."
}

Write-Host 'Repository security control audit passed. Mandatory push protection and the canonical main-branch ruleset are in the expected state.'
