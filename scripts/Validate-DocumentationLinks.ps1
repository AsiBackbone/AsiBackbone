[CmdletBinding()]
param(
    [string]$ManifestPath = 'eng/docs/cross-repository-links.json',
    [string]$SiteRoot = 'docs/_site',
    [string]$ReleaseClaimsPath = 'eng/documentation-release-claims.json',
    [string]$PublishedSiteBaseUrl = 'https://asibackbone.github.io/AsiBackbone/',
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
    param(
        [Parameter(Mandatory = $true)][string]$Url,
        [string]$PendingPublicationPath
    )

    $lastFailure = $null

    for ($attempt = 1; $attempt -le $RetryCount; $attempt++) {
        try {
            $response = Invoke-WebRequest -Uri $Url -Method Get -MaximumRedirection 5 -TimeoutSec $TimeoutSeconds -UseBasicParsing -Headers @{ 'User-Agent' = 'AsiBackbone-documentation-link-validator' }

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

    if (-not [string]::IsNullOrWhiteSpace($PendingPublicationPath)) {
        # A release-preparation change advances the current release destinations
        # before the documentation site that contains them is deployed. The page
        # is present in this DocFX build, so it resolves once this build deploys.
        Write-Warning "Current release destination is not published yet: $Url ($lastFailure). This DocFX build contains '$PendingPublicationPath', so the destination resolves once this build is deployed."
        return $null
    }

    Add-Failure "URL did not resolve after $RetryCount attempt(s): $Url ($lastFailure)"
    return $null
}

function Get-OptionalPropertyValue {
    param(
        [AllowNull()][object]$InputObject,
        [Parameter(Mandatory = $true)][string]$Name
    )

    if ($null -eq $InputObject) {
        return $null
    }

    $property = $InputObject.PSObject.Properties[$Name]
    if ($null -eq $property) {
        return $null
    }

    return $property.Value
}

function Test-HistoricalEntry {
    param([AllowNull()][object]$InputObject)

    $historical = Get-OptionalPropertyValue -InputObject $InputObject -Name 'historical'
    return ($historical -is [bool] -and $historical)
}

function Get-ReleaseVersionTokens {
    param([AllowNull()][string]$Text)

    if ([string]::IsNullOrWhiteSpace($Text)) {
        return @()
    }

    return @(
        [regex]::Matches(
            $Text,
            '(?<![0-9.])\d+\.\d+\.\d+(?![0-9])',
            [System.Text.RegularExpressions.RegexOptions]::CultureInvariant) |
            ForEach-Object { $_.Value })
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

if ([string]::IsNullOrWhiteSpace($PublishedSiteBaseUrl)) {
    throw 'PublishedSiteBaseUrl must not be blank.'
}

$publishedSiteBase = $PublishedSiteBaseUrl.Trim()
if (-not $publishedSiteBase.EndsWith('/', [System.StringComparison]::Ordinal)) {
    $publishedSiteBase += '/'
}

$remoteUrls = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

# Published URLs for current release destinations whose page is present in this
# DocFX build, keyed by URL. Used to tolerate the window between merging a
# release-preparation change and deploying the site that publishes it.
$pendingPublicationPaths = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::OrdinalIgnoreCase)

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

    $oldPublishedUrl = $publishedSiteBase + $publishedPath
    if ([string]::Equals($canonicalUrl, $oldPublishedUrl, [System.StringComparison]::OrdinalIgnoreCase)) {
        Add-Failure "Transition page '$sourcePath' points to itself instead of a canonical Learning destination."
    }

    [void]$remoteUrls.Add($canonicalUrl)
}

foreach ($localPath in @($manifest.localPublishedPaths)) {
    $name = [string]$localPath.name
    $publishedPath = [string]$localPath.path

    if ([string]::IsNullOrWhiteSpace($publishedPath)) {
        Add-Failure 'Each localPublishedPaths entry must provide path.'
        continue
    }

    if (Test-Path -LiteralPath $resolvedSiteRoot -PathType Container) {
        $resolvedPublishedPath = Join-Path $resolvedSiteRoot $publishedPath
        if (-not (Test-Path -LiteralPath $resolvedPublishedPath -PathType Leaf)) {
            if ([string]::IsNullOrWhiteSpace($name)) {
                $name = $publishedPath
            }

            Add-Failure "Required DocFX output is missing for '$name': $publishedPath"
        }
    }
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

# Current release destinations must track the canonical latest published
# version. Each kind of current release page is declared once among the local
# DocFX outputs and once among the published destinations, and both must name
# the page for publication.latestPublishedVersion.
$currentReleasePagePatterns = [ordered]@{
    'release-notes'         = 'articles/release-notes-{0}.html'
    'consumer-verification' = 'articles/consumer-verification-{0}.html'
}
$versionedReleasePageRegex = [regex]::new(
    '(?:^|/)articles/(?:release-notes|consumer-verification)-\d+\.html$',
    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)

$latestPublishedVersion = $null
$resolvedReleaseClaimsPath = Resolve-RepositoryPath -Path $ReleaseClaimsPath

if (-not (Test-Path -LiteralPath $resolvedReleaseClaimsPath -PathType Leaf)) {
    Add-Failure "Release publication metadata was not found: $ReleaseClaimsPath"
}
else {
    $releaseClaims = $null

    try {
        $releaseClaims = Get-Content -LiteralPath $resolvedReleaseClaimsPath -Raw | ConvertFrom-Json
    }
    catch {
        Add-Failure "Release publication metadata could not be parsed: $ReleaseClaimsPath ($($_.Exception.Message))"
    }

    if ($null -ne $releaseClaims) {
        $publication = Get-OptionalPropertyValue -InputObject $releaseClaims -Name 'publication'
        $latestPublishedValue = [string](Get-OptionalPropertyValue -InputObject $publication -Name 'latestPublishedVersion')

        if ([string]::IsNullOrWhiteSpace($latestPublishedValue)) {
            Add-Failure "Release publication metadata must define publication.latestPublishedVersion: $ReleaseClaimsPath"
        }
        elseif (-not [regex]::IsMatch($latestPublishedValue.Trim(), '^\d+\.\d+\.\d+$', [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)) {
            Add-Failure "publication.latestPublishedVersion '$latestPublishedValue' in $ReleaseClaimsPath must use MAJOR.MINOR.PATCH format."
        }
        else {
            $latestPublishedVersion = $latestPublishedValue.Trim()
        }
    }
}

$currentLocalPaths = @{}
$currentPublishedUrls = @{}

foreach ($kind in $currentReleasePagePatterns.Keys) {
    $currentLocalPaths[$kind] = [System.Collections.Generic.List[object]]::new()
    $currentPublishedUrls[$kind] = [System.Collections.Generic.List[object]]::new()
}

function Get-CurrentReleaseKind {
    param(
        [AllowNull()][object]$Entry,
        [Parameter(Mandatory = $true)][string]$Section,
        [Parameter(Mandatory = $true)][string]$Label,
        [AllowNull()][string]$Target
    )

    $kind = [string](Get-OptionalPropertyValue -InputObject $Entry -Name 'currentReleaseDocument')

    if ([string]::IsNullOrWhiteSpace($kind)) {
        if (-not [string]::IsNullOrWhiteSpace($Target) -and
            $versionedReleasePageRegex.IsMatch($Target) -and
            -not (Test-HistoricalEntry $Entry)) {
            Add-Failure "$Section entry '$Label' targets a version-specific release page ($Target) but declares neither currentReleaseDocument nor ""historical"": true."
        }

        return $null
    }

    $kind = $kind.Trim()
    if (-not $currentReleasePagePatterns.Contains($kind)) {
        Add-Failure "$Section entry '$Label' declares unsupported currentReleaseDocument '$kind'. Supported values: $($currentReleasePagePatterns.Keys -join ', ')."
        return $null
    }

    if (Test-HistoricalEntry $Entry) {
        Add-Failure "$Section entry '$Label' cannot declare both currentReleaseDocument '$kind' and ""historical"": true."
    }

    return $kind
}

foreach ($localPath in @($manifest.localPublishedPaths)) {
    $publishedPath = [string](Get-OptionalPropertyValue -InputObject $localPath -Name 'path')
    $name = [string](Get-OptionalPropertyValue -InputObject $localPath -Name 'name')
    $label = if ([string]::IsNullOrWhiteSpace($name)) { $publishedPath } else { $name }

    $kind = Get-CurrentReleaseKind -Entry $localPath -Section 'localPublishedPaths' -Label $label -Target $publishedPath
    if ($null -ne $kind) {
        $currentLocalPaths[$kind].Add([pscustomobject]@{ Name = $label; Path = $publishedPath })
    }
}

foreach ($destination in @($manifest.publishedDestinations)) {
    $url = [string](Get-OptionalPropertyValue -InputObject $destination -Name 'url')
    $name = [string](Get-OptionalPropertyValue -InputObject $destination -Name 'name')
    $label = if ([string]::IsNullOrWhiteSpace($name)) { $url } else { $name }

    $kind = Get-CurrentReleaseKind -Entry $destination -Section 'publishedDestinations' -Label $label -Target $url
    if ($null -ne $kind) {
        $currentPublishedUrls[$kind].Add([pscustomobject]@{ Name = $name; Label = $label; Url = $url })
    }
}

if ($null -ne $latestPublishedVersion) {
    $compactVersion = $latestPublishedVersion.Replace('.', '')

    foreach ($kind in $currentReleasePagePatterns.Keys) {
        $expectedPath = $currentReleasePagePatterns[$kind] -f $compactVersion
        $expectedUrl = $publishedSiteBase + $expectedPath
        $locals = $currentLocalPaths[$kind]
        $publishedEntries = $currentPublishedUrls[$kind]

        if ($locals.Count -ne 1) {
            Add-Failure "localPublishedPaths must declare exactly one entry with currentReleaseDocument '$kind'; found $($locals.Count). Expected path: $expectedPath"
        }

        if ($publishedEntries.Count -ne 1) {
            Add-Failure "publishedDestinations must declare exactly one entry with currentReleaseDocument '$kind'; found $($publishedEntries.Count). Expected URL: $expectedUrl"
        }

        foreach ($local in $locals) {
            if (-not [string]::Equals($local.Path, $expectedPath, [System.StringComparison]::OrdinalIgnoreCase)) {
                Add-Failure "Current local '$kind' destination '$($local.Name)' targets '$($local.Path)', but publication.latestPublishedVersion '$latestPublishedVersion' requires '$expectedPath'."
            }

            foreach ($token in @(Get-ReleaseVersionTokens $local.Name)) {
                if (-not [string]::Equals($token, $latestPublishedVersion, [System.StringComparison]::Ordinal)) {
                    Add-Failure "Current local '$kind' destination label '$($local.Name)' names '$token', but publication.latestPublishedVersion is '$latestPublishedVersion'."
                }
            }
        }

        foreach ($published in $publishedEntries) {
            if (-not [string]::Equals($published.Url, $expectedUrl, [System.StringComparison]::OrdinalIgnoreCase)) {
                Add-Failure "Current published '$kind' destination '$($published.Label)' targets '$($published.Url)', but publication.latestPublishedVersion '$latestPublishedVersion' requires '$expectedUrl'."
            }

            $labelTokens = @(Get-ReleaseVersionTokens $published.Name)
            if ($labelTokens.Count -eq 0) {
                Add-Failure "Current published '$kind' destination label '$($published.Label)' must identify version '$latestPublishedVersion'."
            }

            foreach ($token in $labelTokens) {
                if (-not [string]::Equals($token, $latestPublishedVersion, [System.StringComparison]::Ordinal)) {
                    Add-Failure "Current published '$kind' destination label '$($published.Label)' names '$token', but publication.latestPublishedVersion is '$latestPublishedVersion'."
                }
            }

            foreach ($local in $locals) {
                $localUrl = $publishedSiteBase + $local.Path
                if (-not [string]::Equals($published.Url, $localUrl, [System.StringComparison]::OrdinalIgnoreCase)) {
                    Add-Failure "Current '$kind' destinations disagree: published '$($published.Url)' does not match local '$($local.Path)'."
                }
                elseif (Test-Path -LiteralPath $resolvedSiteRoot -PathType Container) {
                    $resolvedLocalPath = Join-Path $resolvedSiteRoot $local.Path
                    if (Test-Path -LiteralPath $resolvedLocalPath -PathType Leaf) {
                        $pendingPublicationPaths[$published.Url] = $local.Path
                    }
                }
            }
        }
    }
}

if (-not $SkipRemote) {
    $responses = @{}

    foreach ($url in @($remoteUrls | Sort-Object)) {
        Write-Host "Checking published documentation URL: $url"
        $pendingPublicationPath = $null
        [void]$pendingPublicationPaths.TryGetValue($url, [ref]$pendingPublicationPath)
        $response = Invoke-DocumentationRequest -Url $url -PendingPublicationPath $pendingPublicationPath
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
    # List each failure on its own unformatted line. A single long Write-Error
    # record is wrapped to the console width with gutter characters, which splits
    # individual failures across lines in logs and in Test-DocumentationLinks.ps1.
    Write-Host 'Documentation continuity/link validation failed:'
    foreach ($failure in $failures) {
        Write-Host " - $failure"
    }

    Write-Error "Documentation continuity/link validation failed with $($failures.Count) issue(s)."
    exit 1
}

Write-Host ("Documentation continuity/link validation passed. Transition pages: {0}; required local outputs: {1}; published/reciprocal URLs tracked: {2}; current release version: {3}." -f @($manifest.transitionPages).Count, @($manifest.localPublishedPaths).Count, $remoteUrls.Count, $latestPublishedVersion)
