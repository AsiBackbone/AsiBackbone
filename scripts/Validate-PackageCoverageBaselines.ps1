[CmdletBinding()]
param(
    [string]$BaselinePath = 'eng/coverage/package-coverage-baselines.csv',

    [string]$Configuration = 'Release',

    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,

    [string]$OutputRoot = 'artifacts/coverage/package-baselines',

    [switch]$NoBuild,

    [switch]$NoRestore
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Resolve-RepositoryPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $RepositoryRoot $Path
}

function Get-RepositoryRelativePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    try {
        return [System.IO.Path]::GetRelativePath($RepositoryRoot, $Path).Replace('\\', '/')
    }
    catch {
        return $Path.Replace('\\', '/')
    }
}

function Escape-MarkdownTableValue {
    param(
        [AllowNull()]
        [string]$Value
    )

    if ($null -eq $Value) {
        return ''
    }

    return $Value.Replace('|', '\|').Replace("`r", ' ').Replace("`n", ' ')
}

$resolvedRepositoryRoot = Resolve-Path -LiteralPath $RepositoryRoot
$RepositoryRoot = $resolvedRepositoryRoot.Path

$baselineAbsolutePath = Resolve-RepositoryPath -Path $BaselinePath
if (-not (Test-Path -LiteralPath $baselineAbsolutePath)) {
    throw ('Package coverage baseline file was not found: ' + $BaselinePath)
}

$targets = @(Import-Csv -LiteralPath $baselineAbsolutePath)
if ($targets.Count -eq 0) {
    throw ('Package coverage baseline file contains no targets: ' + $BaselinePath)
}

$outputRootAbsolutePath = Resolve-RepositoryPath -Path $OutputRoot
New-Item -ItemType Directory -Path $outputRootAbsolutePath -Force | Out-Null

$results = @()
$failures = @()
$generatedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ssZ')

foreach ($target in $targets) {
    foreach ($requiredColumn in @('Package', 'TestProject', 'Include', 'LineThreshold', 'OutputName')) {
        if (-not $target.$requiredColumn) {
            throw ('Package coverage baseline row is missing ' + $requiredColumn + '.')
        }
    }

    $threshold = 0
    if (-not [int]::TryParse([string]$target.LineThreshold, [ref]$threshold)) {
        throw ('LineThreshold must be an integer for package ' + $target.Package)
    }

    if ($threshold -lt 0 -or $threshold -gt 100) {
        throw ('LineThreshold must be between 0 and 100 for package ' + $target.Package)
    }

    $testProjectAbsolutePath = Resolve-RepositoryPath -Path $target.TestProject
    if (-not (Test-Path -LiteralPath $testProjectAbsolutePath)) {
        throw ('Test project path does not exist for package ' + $target.Package + ': ' + $target.TestProject)
    }

    $safeOutputName = ([string]$target.OutputName).Trim()
    if ($safeOutputName -match '[\\/:*?"<>|]') {
        throw ('OutputName contains invalid path characters for package ' + $target.Package + ': ' + $safeOutputName)
    }

    $packageOutputDirectory = Join-Path $outputRootAbsolutePath $safeOutputName
    New-Item -ItemType Directory -Path $packageOutputDirectory -Force | Out-Null
    $coverageOutputPrefix = Join-Path $packageOutputDirectory 'coverage'

    Write-Host ('Validating package coverage baseline for ' + $target.Package)

    $exitCode = 0
    $coverageReportPath = Join-Path $packageOutputDirectory 'coverage.cobertura.xml'
    $relativeCoverageReportPath = Get-RepositoryRelativePath -Path $coverageReportPath

    try {
        $coverageArguments = @{
            ProjectPath = $testProjectAbsolutePath
            Configuration = $Configuration
            OutputRoot = $packageOutputDirectory
            Include = $target.Include
            LineThreshold = $threshold
        }

        if ($NoRestore) {
            $coverageArguments['NoRestore'] = $true
        }

        if ($NoBuild) {
            $coverageArguments['NoBuild'] = $true
        }

        & (Join-Path $PSScriptRoot 'Invoke-MtpCoverage.ps1') @coverageArguments
    }
    catch {
        $exitCode = 1
        $failureMessage = 'Package coverage baseline failed for {0}. Test project: {1}; threshold: {2}%. {3}' -f `
            $target.Package,
            $target.TestProject,
            $threshold,
            $_.Exception.Message

        Write-Warning $failureMessage

        $failures += [pscustomobject]@{
            Package = $target.Package
            TestProject = $target.TestProject
            LineThreshold = $threshold
            ExitCode = $exitCode
        }
    }
    $results += [pscustomobject]@{
        Package = $target.Package
        TestProject = $target.TestProject
        Include = $target.Include
        LineThreshold = $threshold
        Output = $relativeCoverageReportPath
        ExitCode = $exitCode
        Notes = [string]$target.Notes
    }
}

$summaryPath = Join-Path $outputRootAbsolutePath 'package-coverage-baselines.md'
$summary = @(
    '# Package Coverage Baseline Results',
    '',
    ('Generated: {0}' -f $generatedAt),
    '',
    'This report is produced by `scripts/Validate-PackageCoverageBaselines.ps1`. Each target runs package-scoped Coverlet instrumentation with a tracked line-coverage floor so adapter and provider packages remain visible independently from the repository-wide coverage total.',
    '',
    '| Package | Test project | Include filter | Line threshold | Output | Exit code | Notes |',
    '| --- | --- | --- | ---: | --- | ---: | --- |'
)

foreach ($result in $results) {
    $package = Escape-MarkdownTableValue -Value $result.Package
    $testProject = Escape-MarkdownTableValue -Value $result.TestProject
    $include = Escape-MarkdownTableValue -Value $result.Include
    $output = Escape-MarkdownTableValue -Value $result.Output
    $notes = Escape-MarkdownTableValue -Value $result.Notes
    $summary += '| {0} | {1} | `{2}` | {3}% | {4} | {5} | {6} |' -f $package, $testProject, $include, $result.LineThreshold, $output, $result.ExitCode, $notes
}

$summary += ''

if ($failures.Count -gt 0) {
    $summary += '## Failures'
    $summary += ''
    $summary += '| Package | Test project | Line threshold | Exit code |'
    $summary += '| --- | --- | ---: | ---: |'

    foreach ($failure in $failures) {
        $package = Escape-MarkdownTableValue -Value $failure.Package
        $testProject = Escape-MarkdownTableValue -Value $failure.TestProject
        $summary += '| {0} | {1} | {2}% | {3} |' -f $package, $testProject, $failure.LineThreshold, $failure.ExitCode
    }

    $summary += ''
}

$summary | Set-Content -LiteralPath $summaryPath -Encoding utf8
Write-Host (Get-RepositoryRelativePath -Path $summaryPath)

if ($failures.Count -gt 0) {
    throw ('Found ' + $failures.Count + ' package coverage baseline failure(s).')
}
