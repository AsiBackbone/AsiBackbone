[CmdletBinding()]
param(
    [string]$ConfigurationPath,
    [switch]$SelfTest
)

# Validates that current-facing summary surfaces (README, package metadata, homepage, citation)
# use the current public vocabulary. Only the paths listed in eng/docs/current-terminology.json
# are scanned; historical, generated, and identifier-bearing content is excluded by construction
# and the exclusions are documented in that file.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot

if ([string]::IsNullOrWhiteSpace($ConfigurationPath)) {
    $ConfigurationPath = Join-Path $repoRoot 'eng/docs/current-terminology.json'
}

function Test-TerminologyContent {
    param(
        [string]$RelativePath,
        [string]$Content,
        [object[]]$LegacyTerms,
        [object[]]$AllowedLines,
        [System.Collections.Generic.List[string]]$Errors
    )

    $lines = $Content -split "`r?`n"
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = $lines[$index]
        foreach ($term in $LegacyTerms) {
            $match = [regex]::Match($line, $term.pattern)
            if (-not $match.Success) {
                continue
            }

            $allowed = @($AllowedLines | Where-Object {
                    $_.path -eq $RelativePath -and [regex]::IsMatch($line, $_.linePattern)
                }).Count -gt 0
            if (-not $allowed) {
                $Errors.Add("${RelativePath}:$($index + 1) uses legacy term '$($match.Value)'; use '$($term.replacement)' or add a reviewed allowedLines entry.")
            }
        }
    }
}

function Invoke-SelfTest {
    $legacyTerms = @([pscustomobject]@{ pattern = '(?i)\bcapability[- ]tokens?\b'; replacement = 'capability grant' })
    $allowedLines = @([pscustomobject]@{ path = 'README.md'; linePattern = 'CapabilityTokenId'; reason = 'Retained identifier.' })

    $cases = @(
        @{ Name = 'current vocabulary'; Path = 'README.md'; Content = 'Capability-grant abstractions and acknowledgment workflows.'; Pass = $true }
        @{ Name = 'hyphenated legacy term'; Path = 'README.md'; Content = 'Capability-token abstractions.'; Pass = $false }
        @{ Name = 'spaced plural legacy term'; Path = 'README.md'; Content = 'Issues capability tokens.'; Pass = $false }
        @{ Name = 'package tag legacy term'; Path = 'src/AsiBackbone.Core/AsiBackbone.Core.csproj'; Content = '<PackageTags>audit;capability-token</PackageTags>'; Pass = $false }
        @{ Name = 'allowed retained identifier'; Path = 'README.md'; Content = 'The CapabilityTokenId capability token column is retained.'; Pass = $true }
        @{ Name = 'allowance scoped to path'; Path = 'docs/index.md'; Content = 'The CapabilityTokenId capability token column is retained.'; Pass = $false }
        @{ Name = 'identifier without separator'; Path = 'README.md'; Content = 'CapabilityTokenId'; Pass = $true }
    )

    $failures = [System.Collections.Generic.List[string]]::new()
    foreach ($case in $cases) {
        $caseErrors = [System.Collections.Generic.List[string]]::new()
        Test-TerminologyContent $case.Path $case.Content $legacyTerms $allowedLines $caseErrors
        if (($caseErrors.Count -eq 0) -ne $case.Pass) {
            $expected = if ($case.Pass) { 'success' } else { 'failure' }
            $failures.Add("Self-test '$($case.Name)' expected $expected but got $($caseErrors.Count) error(s).")
        }
    }

    if ($failures.Count -gt 0) {
        $failures | ForEach-Object { Write-Error $_ -ErrorAction Continue }
        exit 1
    }

    Write-Host 'Current terminology validator self-test passed.'
    exit 0
}

if ($SelfTest) {
    Invoke-SelfTest
}

if (-not (Test-Path -LiteralPath $ConfigurationPath -PathType Leaf)) {
    Write-Error "Terminology configuration is missing: $ConfigurationPath"
    exit 1
}

$configuration = Get-Content -LiteralPath $ConfigurationPath -Raw | ConvertFrom-Json
$legacyTerms = @($configuration.legacyTerms)
$allowedLines = @($configuration.allowedLines)
$errors = [System.Collections.Generic.List[string]]::new()

foreach ($entry in $allowedLines) {
    if ([string]::IsNullOrWhiteSpace($entry.reason)) {
        $errors.Add("allowedLines entry for '$($entry.path)' must document a reason.")
    }
}

$scannedCount = 0
foreach ($scannedPath in @($configuration.scannedPaths)) {
    $matchedFiles = @(Get-ChildItem -Path (Join-Path $repoRoot $scannedPath) -File -ErrorAction SilentlyContinue)
    if ($matchedFiles.Count -eq 0) {
        $errors.Add("Scanned path '$scannedPath' matched no files; update eng/docs/current-terminology.json.")
        continue
    }

    foreach ($file in $matchedFiles) {
        $relativePath = [System.IO.Path]::GetRelativePath($repoRoot, $file.FullName).Replace('\', '/')
        Test-TerminologyContent $relativePath (Get-Content -LiteralPath $file.FullName -Raw) $legacyTerms $allowedLines $errors
        $scannedCount++
    }
}

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ -ErrorAction Continue }
    exit 1
}

Write-Host "Current terminology validation passed for $scannedCount file(s)."
