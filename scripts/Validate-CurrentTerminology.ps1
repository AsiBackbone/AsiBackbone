[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$ConfigurationPath = 'eng/docs/current-terminology.json'
)

# Checks current-facing summary surfaces for legacy terminology (#915). The scope,
# terms, and every allowance are declared in eng/docs/current-terminology.json;
# historical, compatibility, and generated documentation is outside the scanned
# surfaces. An allowance that no longer matches any line fails, so retained
# exceptions are reviewed instead of accumulating.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

$repoRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$configurationFilePath = if ([System.IO.Path]::IsPathRooted($ConfigurationPath)) {
    $ConfigurationPath
}
else {
    Join-Path $repoRoot $ConfigurationPath
}

if (-not (Test-Path -LiteralPath $configurationFilePath -PathType Leaf)) {
    throw "Terminology configuration was not found: $configurationFilePath"
}

$configuration = Get-Content -LiteralPath $configurationFilePath -Raw | ConvertFrom-Json
$failures = [System.Collections.Generic.List[string]]::new()
$regexOptions = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant

function Get-RequiredText {
    param(
        [object]$Entry,
        [string]$Name,
        [string]$Section
    )

    $property = $Entry.PSObject.Properties[$Name]
    if ($null -eq $property -or [string]::IsNullOrWhiteSpace([string]$property.Value)) {
        throw "Each $Section entry must define a nonblank '$Name'."
    }

    return ([string]$property.Value).Trim()
}

$terms = [ordered]@{}
foreach ($term in @($configuration.terms)) {
    $name = Get-RequiredText $term 'name' 'terms'
    $terms[$name] = [pscustomobject]@{
        Name = $name
        Regex = [regex]::new((Get-RequiredText $term 'pattern' 'terms'), $regexOptions)
        Use = Get-RequiredText $term 'use' 'terms'
    }
    [void](Get-RequiredText $term 'reason' 'terms')
}

if ($terms.Count -eq 0) {
    throw 'The terminology configuration must define at least one term.'
}

$allowances = @(foreach ($allowance in @($configuration.allowances)) {
    $termName = Get-RequiredText $allowance 'term' 'allowances'
    if (-not $terms.Contains($termName)) {
        throw "Allowance term '$termName' is not defined in terms."
    }

    [void](Get-RequiredText $allowance 'reason' 'allowances')
    [pscustomobject]@{
        Path = (Get-RequiredText $allowance 'path' 'allowances').Replace('\', '/')
        Term = $termName
        LineRegex = [regex]::new((Get-RequiredText $allowance 'linePattern' 'allowances'), $regexOptions)
        Used = $false
    }
})

$scannedFiles = [System.Collections.Generic.SortedSet[string]]::new([System.StringComparer]::Ordinal)
foreach ($surface in @($configuration.surfaces)) {
    $pattern = ([string]$surface).Trim()
    if ([string]::IsNullOrWhiteSpace($pattern)) {
        throw 'Terminology surfaces entries must not be blank.'
    }

    # -Force includes dot-prefixed files such as .zenodo.json, which are hidden on Linux and macOS.
    $matchedFiles = @(Get-ChildItem -Path (Join-Path $repoRoot $pattern) -File -Force -ErrorAction SilentlyContinue)
    if ($matchedFiles.Count -eq 0) {
        $failures.Add("Terminology surface '$pattern' matched no files. Remove or correct the surface entry.")
        continue
    }

    foreach ($file in $matchedFiles) {
        [void]$scannedFiles.Add([System.IO.Path]::GetRelativePath($repoRoot, $file.FullName).Replace('\', '/'))
    }
}

foreach ($relativePath in $scannedFiles) {
    $lines = Get-Content -LiteralPath (Join-Path $repoRoot $relativePath)
    for ($index = 0; $index -lt $lines.Count; $index++) {
        $line = [string]$lines[$index]

        foreach ($term in $terms.Values) {
            if (-not $term.Regex.IsMatch($line)) {
                continue
            }

            $allowance = $allowances | Where-Object {
                $_.Term -eq $term.Name -and
                [string]::Equals($_.Path, $relativePath, [System.StringComparison]::Ordinal) -and
                $_.LineRegex.IsMatch($line)
            } | Select-Object -First 1

            if ($null -ne $allowance) {
                $allowance.Used = $true
                continue
            }

            $failures.Add("${relativePath}:$($index + 1): legacy term '$($term.Name)' in a current-facing summary; use '$($term.Use)', or add a reviewed allowance with a reason to the terminology configuration.")
        }
    }
}

foreach ($allowance in $allowances) {
    if (-not $allowance.Used) {
        $failures.Add("Allowance for '$($allowance.Term)' in '$($allowance.Path)' no longer matches any line. Remove it or update its linePattern.")
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Current terminology validation failed:'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }

    Write-Error "Current terminology validation failed with $($failures.Count) issue(s)."
    exit 1
}

Write-Host ("Current terminology validation passed. Files scanned: {0}; terms: {1}; reviewed allowances: {2}." -f $scannedFiles.Count, $terms.Count, $allowances.Count)
