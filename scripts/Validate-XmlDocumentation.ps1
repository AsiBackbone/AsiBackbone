[CmdletBinding()]
param(
    [ValidateSet('Inventory', 'Enforce')]
    [string]$Mode = 'Inventory',

    [string[]]$Project = @(),

    [string]$Configuration = 'Release',

    [string]$RepositoryRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path,

    [string]$ProjectListPath = 'eng/xml-docs/public-api-projects.txt',

    [string]$EnforcedProjectListPath = 'eng/xml-docs/staged-enforcement-projects.txt',

    [string]$BaselinePath = 'eng/xml-docs/cs1591-baseline.csv',

    [string]$OutputPath = 'artifacts/xml-docs/cs1591-inventory.md',

    [switch]$NoRestore,

    [switch]$SkipBaselineCheck
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
        return [System.IO.Path]::GetRelativePath($RepositoryRoot, $Path).Replace('\', '/')
    }
    catch {
        return $Path.Replace('\', '/')
    }
}

function Read-ProjectList {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolvedPath = Resolve-RepositoryPath -Path $Path

    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        return @()
    }

    return @(
        Get-Content -LiteralPath $resolvedPath |
            ForEach-Object { $_.Trim() } |
            Where-Object { $_ -and -not $_.StartsWith('#') }
    )
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

function Read-Cs1591Baseline {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $resolvedPath = Resolve-RepositoryPath -Path $Path
    $baseline = @{}

    if (-not (Test-Path -LiteralPath $resolvedPath)) {
        return $baseline
    }

    $rows = @(Import-Csv -LiteralPath $resolvedPath)

    foreach ($row in $rows) {
        if (-not $row.Project) {
            throw ('CS1591 baseline row is missing Project in ' + $Path)
        }

        if (-not $row.MaxCS1591) {
            throw ('CS1591 baseline row is missing MaxCS1591 for ' + $row.Project)
        }

        $maximum = 0
        if (-not [int]::TryParse([string]$row.MaxCS1591, [ref]$maximum)) {
            throw ('CS1591 baseline MaxCS1591 must be an integer for ' + $row.Project)
        }

        if ($maximum -lt 0) {
            throw ('CS1591 baseline MaxCS1591 cannot be negative for ' + $row.Project)
        }

        $projectPath = ([string]$row.Project).Trim().Replace('\', '/')
        $baseline[$projectPath] = [pscustomobject]@{
            Project = $projectPath
            MaxCS1591 = $maximum
            Notes = [string]$row.Notes
        }
    }

    return $baseline
}

$resolvedRepositoryRoot = Resolve-Path -LiteralPath $RepositoryRoot
$RepositoryRoot = $resolvedRepositoryRoot.Path

if ($Project.Count -eq 0) {
    # Wrap in @() because an empty array returned from a function unrolls to
    # $null, which breaks .Count under strict mode.
    if ($Mode -eq 'Enforce') {
        $Project = @(Read-ProjectList -Path $EnforcedProjectListPath)
    }
    else {
        $Project = @(Read-ProjectList -Path $ProjectListPath)
    }
}

$baseline = Read-Cs1591Baseline -Path $BaselinePath
$applyBaseline = $Mode -eq 'Inventory' -and -not $SkipBaselineCheck -and $baseline.Count -gt 0
$baselineFailures = @()
$baselineResults = @()
$findings = @()
$projectSummaries = @()
$buildFailures = @()
$generatedAt = (Get-Date).ToUniversalTime().ToString('yyyy-MM-dd HH:mm:ssZ')
$enforceXmlDocs = if ($Mode -eq 'Enforce') { 'true' } else { 'false' }

foreach ($projectPath in $Project) {
    $absoluteProjectPath = Resolve-RepositoryPath -Path $projectPath

    if (-not (Test-Path -LiteralPath $absoluteProjectPath)) {
        throw ('Project path does not exist: ' + $projectPath)
    }

    $displayProjectPath = Get-RepositoryRelativePath -Path $absoluteProjectPath
    Write-Host ('Validating XML documentation coverage for ' + $displayProjectPath)

    $buildArguments = @(
        'build',
        $absoluteProjectPath,
        '--configuration',
        $Configuration,
        '--nologo',
        # Force recompilation; an up-to-date incremental build emits no CS1591
        # diagnostics and would report zero gaps.
        '--no-incremental',
        '/p:ContinuousIntegrationBuild=true',
        '/p:GenerateDocumentationFile=true',
        '/p:AsiBackboneSuppressMissingXmlDocs=false',
        ('/p:AsiBackboneEnforceMissingXmlDocs=' + $enforceXmlDocs)
    )

    if ($NoRestore) {
        $buildArguments += '--no-restore'
    }

    $projectOutput = & dotnet @buildArguments 2>&1
    $exitCode = $LASTEXITCODE
    $cs1591Count = 0
    $pattern = '^(?<path>.+?\.(?:cs|vb))\((?<line>\d+),(?<column>\d+)\):\s+(?<level>warning|error)\s+CS1591:\s+(?<message>.+?)(?:\s+\[(?<project>.+?\.csproj)\])?$'
    $seenDiagnostics = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

    foreach ($line in $projectOutput) {
        $text = ([string]$line).Trim()

        if ($text -match $pattern) {
            # Building a project also builds its references; count only
            # diagnostics that belong to the project under validation.
            if ($Matches['project'] -and -not [string]::Equals(
                    [System.IO.Path]::GetFullPath($Matches['project']),
                    [System.IO.Path]::GetFullPath($absoluteProjectPath),
                    [System.StringComparison]::OrdinalIgnoreCase)) {
                continue
            }

            # MSBuild can repeat diagnostics in its summary; count each once.
            if (-not $seenDiagnostics.Add($Matches['path'] + '|' + $Matches['line'] + '|' + $Matches['column'] + '|' + $Matches['message'])) {
                continue
            }

            $cs1591Count++
            $sourcePath = $Matches['path']

            if ([System.IO.Path]::IsPathRooted($sourcePath)) {
                $displaySourcePath = Get-RepositoryRelativePath -Path $sourcePath
            }
            else {
                $displaySourcePath = $sourcePath.Replace('\', '/')
            }

            $findings += [pscustomobject]@{
                Project = $displayProjectPath
                File = $displaySourcePath
                Line = $Matches['line']
                Column = $Matches['column']
                Level = $Matches['level']
                Message = $Matches['message']
            }
        }
    }

    $projectSummaries += [pscustomobject]@{
        Project = $displayProjectPath
        CS1591 = $cs1591Count
        ExitCode = $exitCode
    }

    if ($applyBaseline) {
        $baselineEntry = $baseline[$displayProjectPath]
        $maximum = $null
        $status = 'Not tracked'
        $notes = ''

        if ($null -ne $baselineEntry) {
            $maximum = $baselineEntry.MaxCS1591
            $notes = $baselineEntry.Notes

            if ($cs1591Count -le $maximum) {
                $status = 'Within baseline'
            }
            else {
                $status = 'Regression'
                $baselineFailures += [pscustomobject]@{
                    Project = $displayProjectPath
                    CS1591 = $cs1591Count
                    MaxCS1591 = $maximum
                    Reason = 'CS1591 count exceeds the tracked baseline ceiling.'
                }
            }
        }
        elseif ($cs1591Count -gt 0) {
            $status = 'Missing baseline'
            $baselineFailures += [pscustomobject]@{
                Project = $displayProjectPath
                CS1591 = $cs1591Count
                MaxCS1591 = 'n/a'
                Reason = 'Project has CS1591 gaps but no tracked baseline entry.'
            }
        }
        else {
            $status = 'Clean / no baseline required'
        }

        $baselineResults += [pscustomobject]@{
            Project = $displayProjectPath
            CS1591 = $cs1591Count
            MaxCS1591 = $maximum
            Status = $status
            Notes = $notes
        }
    }

    if ($exitCode -ne 0) {
        $buildFailures += [pscustomobject]@{
            Project = $displayProjectPath
            ExitCode = $exitCode
        }
    }
}

$outputAbsolutePath = Resolve-RepositoryPath -Path $OutputPath
$outputDirectory = Split-Path -Parent $outputAbsolutePath
New-Item -ItemType Directory -Path $outputDirectory -Force | Out-Null

$report = @(
    '# Public API XML Documentation Inventory',
    '',
    ('Generated: {0}' -f $generatedAt),
    '',
    ('Mode: `{0}`' -f $Mode),
    '',
    ('Baseline check: `{0}`' -f $(if ($applyBaseline) { 'enabled' } elseif ($SkipBaselineCheck) { 'skipped' } else { 'not configured' })),
    '',
    'This report is produced by `scripts/Validate-XmlDocumentation.ps1` with `CS1591` unsuppressed for the selected public package projects. Inventory mode records gaps and checks the tracked baseline ceiling when `eng/xml-docs/cs1591-baseline.csv` is present. Enforce mode treats `CS1591` as an error for projects listed in `eng/xml-docs/staged-enforcement-projects.txt` or passed with `-Project`.',
    ''
)

if ($Project.Count -eq 0) {
    $report += 'No projects were configured for this mode.'
    $report += ''
}
else {
    $report += '## Project summary'
    $report += ''
    $report += '| Project | CS1591 count | Build exit code |'
    $report += '| --- | ---: | ---: |'

    foreach ($summary in $projectSummaries) {
        $projectName = Escape-MarkdownTableValue -Value $summary.Project
        $reportLine = '| {0} | {1} | {2} |' -f $projectName, $summary.CS1591, $summary.ExitCode
        $report += $reportLine
    }

    $report += ''
}

if ($applyBaseline) {
    $report += '## Baseline status'
    $report += ''
    $report += '| Project | CS1591 count | Baseline ceiling | Status | Notes |'
    $report += '| --- | ---: | ---: | --- | --- |'

    foreach ($result in $baselineResults) {
        $projectName = Escape-MarkdownTableValue -Value $result.Project
        $maximum = if ($null -eq $result.MaxCS1591) { '' } else { [string]$result.MaxCS1591 }
        $status = Escape-MarkdownTableValue -Value $result.Status
        $notes = Escape-MarkdownTableValue -Value $result.Notes
        $report += '| {0} | {1} | {2} | {3} | {4} |' -f $projectName, $result.CS1591, $maximum, $status, $notes
    }

    $report += ''
}

if ($findings.Count -gt 0) {
    $report += '## CS1591 findings'
    $report += ''
    $report += '| Project | File | Line | Member/message |'
    $report += '| --- | --- | ---: | --- |'

    foreach ($finding in $findings) {
        $projectName = Escape-MarkdownTableValue -Value $finding.Project
        $fileName = Escape-MarkdownTableValue -Value $finding.File
        $message = Escape-MarkdownTableValue -Value $finding.Message
        $reportLine = '| {0} | {1} | {2} | {3} |' -f $projectName, $fileName, $finding.Line, $message
        $report += $reportLine
    }

    $report += ''
}
else {
    $report += 'No `CS1591` gaps were reported for the selected projects.'
    $report += ''
}

if ($baselineFailures.Count -gt 0) {
    $report += '## Baseline failures'
    $report += ''
    $report += '| Project | CS1591 count | Baseline ceiling | Reason |'
    $report += '| --- | ---: | ---: | --- |'

    foreach ($failure in $baselineFailures) {
        $projectName = Escape-MarkdownTableValue -Value $failure.Project
        $reason = Escape-MarkdownTableValue -Value $failure.Reason
        $report += '| {0} | {1} | {2} | {3} |' -f $projectName, $failure.CS1591, $failure.MaxCS1591, $reason
    }

    $report += ''
}

if ($buildFailures.Count -gt 0) {
    $report += '## Build failures'
    $report += ''
    $report += '| Project | Exit code |'
    $report += '| --- | ---: |'

    foreach ($failure in $buildFailures) {
        $projectName = Escape-MarkdownTableValue -Value $failure.Project
        $reportLine = '| {0} | {1} |' -f $projectName, $failure.ExitCode
        $report += $reportLine
    }

    $report += ''
}

$report | Set-Content -LiteralPath $outputAbsolutePath -Encoding utf8
$relativeOutputPath = Get-RepositoryRelativePath -Path $outputAbsolutePath
Write-Host $relativeOutputPath

if ($buildFailures.Count -gt 0) {
    throw 'One or more XML documentation validation builds failed.'
}

if ($baselineFailures.Count -gt 0) {
    throw ('Found ' + $baselineFailures.Count + ' CS1591 public API XML documentation baseline regression(s).')
}

if ($Mode -eq 'Enforce' -and $findings.Count -gt 0) {
    throw ('Found ' + $findings.Count + ' CS1591 public API XML documentation gaps in enforced projects.')
}

if ($findings.Count -gt 0) {
    Write-Warning ('Found ' + $findings.Count + ' CS1591 public API XML documentation gaps.')
}
