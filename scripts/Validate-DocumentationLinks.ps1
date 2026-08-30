[CmdletBinding()]
param(
    [string]$ManifestPath = 'eng/docs/cross-repository-links.json',
    [string]$SiteRoot = 'docs/_site',
    [ValidateRange(1, 10)]
    [int]$RetryCount = 3,
    [ValidateRange(1, 120)]
    [int]$TimeoutSeconds = 20,
    [switch]$SkipRemote
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent $PSScriptRoot
$failures = [System.Collections.Generic.List[string]]::new()

function Add-Failure {
    param([Parameter(Mandatory = $true)][string]$Message)

    $script:failures.Add($Message)
}

function Resolve-RepositoryPath {
    param([Parameter(Mandatory = $true)][string]$Path)

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return $Path
    }

    return Join-Path $repoRoot $Path
}

function Invoke-DocumentationRequest {
    param([Parameter(Mandatory = $true)][string]$Url)

    $lastFailure = $null

    for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -MaximumRedirection 5 -TimeoutSec $TimeoutSeconds -Headers @{ 'User-Agent' = 'AsiBackbone-documentation-link-validator' }

            $statusCode = [int]$response.StatusCode
            if ($statusCode -ge 200 -and $statusCode -lt 400) {
                return $response
            }

            $lastFailure = "HTTP $statusCode"
        }
        catch {
            $lastFailure = $_.Exception.Message
        }

        if ($attempt -lt $RetryCount) {
            $delaySeconds = [Math]::Min([Math]::Pow(2, $attempt - 1), 4)
            Start-Sleep -Seconds $delaySeconds
        }
    }

    Add-Failure "URL did not resolve after $RetryCount attempt(s): $Url ($lastFailure)"
    return $null
}

$resolvedManifestPath = Resolve-RepositoryPath -Path $ManifestPath
if (-not (Test-Path -LiteralPath $resolvedManifestPath -PathType Leaf)) {
    throw "Documentation link manifest was not found: $resolvedManifestPath"
}

$manifest = Get-Content -LiteralPath $resolvedManifestPath -Raw | ConvertFrom-Json
$resolvedSiteRoot = Resolve-RepositoryPath -Path $SiteRoot

if (-not (Test-Path -LiteralPath $resolvedSiteRoot -PathType Container)) {
    Add-Failure "DocFX output directory was not found: $resolvedSiteRoot. Build docs before running this validator."
}

$remoteUrls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

foreach ($transition in @($manifest.transitionPages)) {
    $sourcePath = [string]$transition.sourcePath
    $publishedPath = [string]$transition.publishedPath
    $canonicalUrl = [string]$transition.canonicalUrl

    if ([string]::IsNullOrWhiteSpace($sourcePath) -or
        [string]::IsNullOrWhiteSpace($publishedPath) -or
        [string]::IsNullOrWhiteSpace($canonicalUrl)) {
        Add-Failure 'Each transitionPages entry must provide sourcePath, publishedPath, and canonicalUrl.'
        continue
    }

    $resolvedSourcePath = Resolve-RepositoryPath -Path $sourcePath
    if (-not (Test-Path -LiteralPath $resolvedSourcePath -PathType Leaf)) {
        Add-Failure "Transition source file was not found: $sourcePath"
    }
    else {
        $sourceText = Get-Content -LiteralPath $resolvedSourcePath -Raw
        if ($sourceText.IndexOf($canonicalUrl, [System.StringComparison]::Ordinal) -lt 0) {
            Add-Failure "Transition page '$sourcePath' no longer names its canonical Learning destination: $canonicalUrl"
        }
    }

    if (Test-Path -LiteralPath $resolvedSiteRoot -PathType Container) {
        $resolvedPublishedPath = Join-Path $resolvedSiteRoot $publishedPath
        if (-not (Test-Path -LiteralPath $resolvedPublishedPath -PathType Leaf)) {
            Add-Failure "Preserved DocFX URL output is missing: $publishedPath (source: $sourcePath)"
        }
    }

    $oldPublishedUrl = 'https://asibackbone.github.io/AsiBackbone/' + $publishedPath
    if ([string]::Equals($canonicalUrl, $oldPublishedUrl, [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-Failure "Transition page '$sourcePath' points to itself instead of a canonical Learning destination."
    }

    [void]$remoteUrls.Add($canonicalUrl)
}

foreach ($destination in @($manifest.publishedDestinations)) {
    $url = [string]$destination.url
    if ([string]::IsNullOrWhiteSpace($url)) {
        Add-Failure 'Each publishedDestinations entry must provide url.'
        continue
    }

    [void]$remoteUrls.Add($url)
}

foreach ($reciprocal in @($manifest.reciprocalLinks)) {
    $sourceUrl = [string]$reciprocal.sourceUrl
    $expectedDestination = [string]$reciprocal.expectedDestination

    if ([string]::IsNullOrWhiteSpace($sourceUrl) -or [string]::IsNullOrWhiteSpace($expectedDestination)) {
        Add-Failure 'Each reciprocalLinks entry must provide sourceUrl and expectedDestination.'
        continue
    }

    [void]$remoteUrls.Add($sourceUrl)
    [void]$remoteUrls.Add($expectedDestination)
}

if (-not $SkipRemote) {
    $responses = @{}

    foreach ($url in @($remoteUrls | Sort-Object)) {
        Write-Host "Checking published documentation URL: $url"
        $response = Invoke-DocumentationRequest -Url $url
        if ($null -ne $response) {
            $responses[$url] = $response
        }
    }

    foreach ($reciprocal in @($manifest.reciprocalLinks)) {
        $name = [string]$reciprocal.name
        $sourceUrl = [string]$reciprocal.sourceUrl
        $expectedDestination = [string]$reciprocal.expectedDestination

        if (-not $responses.ContainsKey($sourceUrl)) {
            continue
        }

        $content = [string]$responses[$sourceUrl].Content
        if ($content.IndexOf($expectedDestination, [System.StringComparison]::OrdinalIgnoreCase) -lt 0) {
            if ([string]::IsNullOrWhiteSpace($name)) {
                $name = $sourceUrl
            }

            Add-Failure "Reciprocal documentation link is missing for '$name': expected '$expectedDestination' in '$sourceUrl'."
        }
    }
}
else {
    Write-Host 'Skipping remote documentation URL checks by request.'
}

if ($failures.Count -gt 0) {
    $message = "Documentation continuity/link validation failed:" + [Environment]::NewLine + " - " + ($failures -join ([Environment]::NewLine + " - "))
    Write-Error $message
    exit 1
}

Write-Host ("Documentation continuity/link validation passed. Transition pages: {0}; published/reciprocal URLs tracked: {1}." -f @($manifest.transitionPages).Count, $remoteUrls.Count)
