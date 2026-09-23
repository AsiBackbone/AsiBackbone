[CmdletBinding()]
param(
    [string]$RepositoryRoot,
    [string]$ConfigurationPath = 'eng/documentation-release-claims.json',
    [string]$ReleaseTag
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($RepositoryRoot)) {
    $RepositoryRoot = Split-Path -Parent $PSScriptRoot
}

$repoRoot = [System.IO.Path]::GetFullPath($RepositoryRoot)
$directoryBuildPropsPath = Join-Path $repoRoot 'Directory.Build.props'

if (-not (Test-Path -LiteralPath $directoryBuildPropsPath -PathType Leaf)) {
    throw "Directory.Build.props was not found under repository root '$repoRoot'."
}

[xml]$directoryBuildProps = Get-Content -LiteralPath $directoryBuildPropsPath -Raw
$versionPrefixNodes = @(
    $directoryBuildProps.Project.PropertyGroup.ChildNodes |
        Where-Object {
            $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and
            $_.Name -eq 'VersionPrefix'
        }
)

if ($versionPrefixNodes.Count -eq 0) {
    throw "VersionPrefix was not found in '$directoryBuildPropsPath'."
}

$versionPrefix = $versionPrefixNodes[0].InnerText.Trim()
$versionPrefixMatch = [regex]::Match(
    $versionPrefix,
    '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$',
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)

if (-not $versionPrefixMatch.Success) {
    throw "VersionPrefix '$versionPrefix' must use MAJOR.MINOR.PATCH format."
}

$currentMajor = $versionPrefixMatch.Groups['major'].Value
$currentMajorLine = "$currentMajor.x"

function Get-VersionLineTokens {
    param([string]$Version)

    $versionMatch = [regex]::Match(
        $Version,
        '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)$',
        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)

    if (-not $versionMatch.Success) {
        return $null
    }

    $major = $versionMatch.Groups['major'].Value
    $minor = $versionMatch.Groups['minor'].Value

    return [pscustomobject]@{
        Exact = $Version
        MinorLine = "$major.$minor.x"
        MajorLine = "$major.x"
    }
}

$repositoryVersionTokens = Get-VersionLineTokens $versionPrefix

# Publication state distinguishes the version prepared on this branch from the
# latest version actually tagged and published. Without configuration the
# repository version is treated as released, which preserves the original rule.
$publicationState = 'released'
$latestPublishedVersion = $versionPrefix
$publicationStateErrors = [System.Collections.Generic.List[string]]::new()

$excludedPathPatterns = @()
$allowedClaims = @()
$configurationFilePath = if ([System.IO.Path]::IsPathRooted($ConfigurationPath)) {
    $ConfigurationPath
}
else {
    Join-Path $repoRoot $ConfigurationPath
}

if (Test-Path -LiteralPath $configurationFilePath -PathType Leaf) {
    try {
        $configuration = Get-Content -LiteralPath $configurationFilePath -Raw | ConvertFrom-Json
    }
    catch {
        throw "Documentation release-claim configuration could not be parsed: $($_.Exception.Message)"
    }

    if ($null -ne $configuration.PSObject.Properties['excludedPaths']) {
        $excludedPathPatterns = @($configuration.excludedPaths | ForEach-Object {
            $pattern = [string]$_
            if ([string]::IsNullOrWhiteSpace($pattern)) {
                throw 'Documentation release-claim excludedPaths entries must not be blank.'
            }

            $pattern.Replace('\', '/')
        })
    }

    if ($null -ne $configuration.PSObject.Properties['allowedClaims']) {
        $allowedClaims = @($configuration.allowedClaims | ForEach-Object {
            $pathProperty = $_.PSObject.Properties['path']
            $linePatternProperty = $_.PSObject.Properties['linePattern']
            $reasonProperty = $_.PSObject.Properties['reason']

            if ($null -eq $pathProperty -or [string]::IsNullOrWhiteSpace([string]$pathProperty.Value)) {
                throw 'Each allowedClaims entry must define a nonblank path.'
            }

            if ($null -eq $linePatternProperty -or [string]::IsNullOrWhiteSpace([string]$linePatternProperty.Value)) {
                throw 'Each allowedClaims entry must define a nonblank linePattern.'
            }

            if ($null -eq $reasonProperty -or [string]::IsNullOrWhiteSpace([string]$reasonProperty.Value)) {
                throw 'Each allowedClaims entry must define a nonblank reason.'
            }

            try {
                $lineRegex = [regex]::new(
                    [string]$linePatternProperty.Value,
                    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
                        [System.Text.RegularExpressions.RegexOptions]::CultureInvariant)
            }
            catch {
                throw "Allowed claim linePattern '$($linePatternProperty.Value)' is not a valid regular expression. $($_.Exception.Message)"
            }

            [pscustomobject]@{
                Path = ([string]$pathProperty.Value).Replace('\', '/')
                LineRegex = $lineRegex
                Reason = [string]$reasonProperty.Value
            }
        })
    }

    if ($null -ne $configuration.PSObject.Properties['publication']) {
        $publication = $configuration.publication
        $stateProperty = if ($null -ne $publication) { $publication.PSObject.Properties['state'] } else { $null }
        $latestPublishedProperty = if ($null -ne $publication) { $publication.PSObject.Properties['latestPublishedVersion'] } else { $null }

        if ($null -eq $stateProperty -or [string]::IsNullOrWhiteSpace([string]$stateProperty.Value)) {
            throw "Documentation release-claim publication.state must be 'prepared' or 'released'."
        }

        $publicationState = ([string]$stateProperty.Value).Trim().ToLowerInvariant()
        if ($publicationState -cnotin @('prepared', 'released')) {
            throw "Documentation release-claim publication.state '$($stateProperty.Value)' must be 'prepared' or 'released'."
        }

        if ($null -eq $latestPublishedProperty -or [string]::IsNullOrWhiteSpace([string]$latestPublishedProperty.Value)) {
            throw 'Documentation release-claim publication.latestPublishedVersion must be defined.'
        }

        $latestPublishedVersion = ([string]$latestPublishedProperty.Value).Trim()
    }
}

$configurationRelativePath = [System.IO.Path]::GetRelativePath($repoRoot, $configurationFilePath).Replace('\', '/')
$publishedVersionTokens = Get-VersionLineTokens $latestPublishedVersion

if ($null -eq $publishedVersionTokens) {
    throw "Documentation release-claim publication.latestPublishedVersion '$latestPublishedVersion' must use MAJOR.MINOR.PATCH format."
}

if ($publicationState -eq 'released' -and
    -not [string]::Equals($latestPublishedVersion, $versionPrefix, [System.StringComparison]::Ordinal)) {
    $publicationStateErrors.Add("publication.state is 'released', so publication.latestPublishedVersion '$latestPublishedVersion' must equal Directory.Build.props VersionPrefix '$versionPrefix'.")
}

if ($publicationState -eq 'prepared' -and [version]$latestPublishedVersion -ge [version]$versionPrefix) {
    $publicationStateErrors.Add("publication.state is 'prepared', so publication.latestPublishedVersion '$latestPublishedVersion' must be lower than the prepared Directory.Build.props VersionPrefix '$versionPrefix'.")
}

if (-not [string]::IsNullOrWhiteSpace($ReleaseTag)) {
    $releaseTagName = $ReleaseTag.Trim()
    if ($releaseTagName.StartsWith('refs/tags/', [System.StringComparison]::Ordinal)) {
        $releaseTagName = $releaseTagName.Substring('refs/tags/'.Length)
    }

    $releaseTagVersion = $releaseTagName -replace '^[vV]', ''
    if (-not [string]::Equals($releaseTagVersion, $versionPrefix, [System.StringComparison]::Ordinal)) {
        $publicationStateErrors.Add("Release tag '$releaseTagName' does not match Directory.Build.props VersionPrefix '$versionPrefix'.")
    }

    if ($publicationState -ne 'released') {
        $publicationStateErrors.Add("Release tag '$releaseTagName' requires publication.state 'released'. The release-preparation pull request must switch publication.state to 'released' and replace prepared-release wording before the tag is created, because the tagged commit's README files are packed into the published packages.")
    }
}

function Test-ExcludedPath {
    param([string]$RelativePath)

    foreach ($pattern in $excludedPathPatterns) {
        if ($RelativePath -like $pattern) {
            return $true
        }
    }

    return $false
}

function Test-AllowedClaim {
    param(
        [string]$RelativePath,
        [string]$Line
    )

    foreach ($allowedClaim in $allowedClaims) {
        if ($RelativePath -like $allowedClaim.Path -and $allowedClaim.LineRegex.IsMatch($Line)) {
            return $true
        }
    }

    return $false
}

function Get-ExpectedVersionToken {
    param(
        [string]$VersionToken,
        [object]$VersionTokens
    )

    if ($VersionToken -match '^\d+\.[xX]$') {
        return $VersionTokens.MajorLine
    }

    if ($VersionToken -match '^\d+\.\d+\.[xX]$') {
        return $VersionTokens.MinorLine
    }

    return $VersionTokens.Exact
}

function Test-VersionTokenMatches {
    param(
        [string]$VersionToken,
        [object]$VersionTokens
    )

    $expectedToken = Get-ExpectedVersionToken $VersionToken $VersionTokens
    return [string]::Equals($VersionToken, $expectedToken, [System.StringComparison]::OrdinalIgnoreCase)
}

function ConvertTo-GitHubCommandValue {
    param([string]$Value)

    return $Value.Replace('%', '%25').Replace("`r", '%0D').Replace("`n", '%0A')
}

$documentationFiles = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
$seenFiles = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::OrdinalIgnoreCase)

function Add-DocumentationFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return
    }

    $file = Get-Item -LiteralPath $Path
    if ($seenFiles.Add($file.FullName)) {
        $documentationFiles.Add($file)
    }
}

foreach ($topLevelDocument in @('README.md', 'CONTRIBUTING.md', 'GOVERNANCE.md', 'SECURITY.md')) {
    Add-DocumentationFile (Join-Path $repoRoot $topLevelDocument)
}

Add-DocumentationFile (Join-Path $repoRoot 'docs/index.md')

$articlesRoot = Join-Path $repoRoot 'docs/articles'
if (Test-Path -LiteralPath $articlesRoot -PathType Container) {
    Get-ChildItem -LiteralPath $articlesRoot -Recurse -Filter '*.md' -File |
        ForEach-Object { Add-DocumentationFile $_.FullName }
}

$sourceRoot = Join-Path $repoRoot 'src'
if (Test-Path -LiteralPath $sourceRoot -PathType Container) {
    Get-ChildItem -LiteralPath $sourceRoot -Recurse -Filter 'README.md' -File |
        ForEach-Object { Add-DocumentationFile $_.FullName }
}

if ($documentationFiles.Count -eq 0) {
    throw "No documentation files were found under repository root '$repoRoot'."
}

$versionPattern = '\d+\.(?:[xX]|\d+\.(?:[xX]|\d+))'
$regexOptions = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
    [System.Text.RegularExpressions.RegexOptions]::CultureInvariant
$claimPatterns = @(
    [regex]::new(
        '\b(?:current|active|canonical)\b[^\r\n]{0,80}?[`*_~]*v?(?<version>' + $versionPattern + ')[`*_~]*',
        $regexOptions),
    [regex]::new(
        '[`*_~]*v?(?<version>' + $versionPattern + ')[`*_~]*[^\r\n]{0,80}?\b(?:is|remains|represents|defines|establishes)\s+(?:the\s+)?(?:current|active|canonical)\b',
        $regexOptions),
    [regex]::new(
        '\bstable\b[^\r\n]{0,50}?[`*_~]*v?(?<version>' + $versionPattern + ')[`*_~]*[^\r\n]{0,50}?\b(?:package\s+family|package\s+lineup|release\s+line|stable\s+line|major\s+release)\b',
        $regexOptions)
)

# A bounded gap that cannot cross another version token, so a status phrase is
# attributed to the nearest version rather than to one earlier in the line.
function New-VersionFreeGap {
    param([int]$MaximumLength)

    return '(?:(?!v?' + $versionPattern + ')[^\r\n]){0,' + $MaximumLength + '}?'
}

$versionCapture = '[`*_~]*v?(?<version>' + $versionPattern + ')[`*_~]*'
$publishedClaimPatterns = @(
    [regex]::new(
        '\b(?:latest|most\s+recently)\s+published\b' + (New-VersionFreeGap 80) + $versionCapture,
        $regexOptions),
    [regex]::new(
        $versionCapture + (New-VersionFreeGap 60) + '\b(?:is|remains)\s+(?:the\s+)?(?:latest|most\s+recently)\s+published\b',
        $regexOptions)
)
$preparedClaimPatterns = @(
    [regex]::new(
        '\b(?:prepared|unpublished)\b' + (New-VersionFreeGap 40) + $versionCapture,
        $regexOptions),
    [regex]::new(
        $versionCapture + (New-VersionFreeGap 60) + '\b(?:(?:is|remains)\s+(?:the\s+)?(?:prepared|unpublished)\b|prepared\s+(?:next\s+)?release\b|not\s+yet\s+(?:tagged|published)\b)',
        $regexOptions)
)
$claimRules = @(
    foreach ($claimPattern in $claimPatterns) {
        [pscustomobject]@{ Kind = 'current'; Pattern = $claimPattern }
    }

    foreach ($claimPattern in $publishedClaimPatterns) {
        [pscustomobject]@{ Kind = 'published'; Pattern = $claimPattern }
    }

    foreach ($claimPattern in $preparedClaimPatterns) {
        [pscustomobject]@{ Kind = 'prepared'; Pattern = $claimPattern }
    }
)

$releasePublicationPhrasePattern = [regex]::new(
    '\b(?:current|active|canonical)\s+(?:stable\s+)?release\b(?!\s+line)',
    $regexOptions)

# An exact-version currency claim, or a "current release" phrase at or just
# after the match, asserts that a version is published rather than describing
# the release line the branch maintains.
function Test-ReleasePublicationClaim {
    param(
        [string]$VersionToken,
        [string]$Line,
        [System.Text.RegularExpressions.Match]$ClaimMatch
    )

    if ($VersionToken -match '^\d+\.\d+\.\d+$') {
        return $true
    }

    $claimLength = [Math]::Min($Line.Length - $ClaimMatch.Index, $ClaimMatch.Length + 24)
    return $releasePublicationPhrasePattern.IsMatch($Line.Substring($ClaimMatch.Index, $claimLength))
}

function Get-ClaimFailureDetail {
    param(
        [string]$Kind,
        [string]$VersionToken,
        [string]$DisplayLine,
        [string]$Line,
        [System.Text.RegularExpressions.Match]$ClaimMatch
    )

    switch ($Kind) {
        'current' {
            if ($publicationState -eq 'released') {
                if (Test-VersionTokenMatches $VersionToken $repositoryVersionTokens) {
                    return $null
                }

                $expectedToken = Get-ExpectedVersionToken $VersionToken $repositoryVersionTokens
                return "release claim '$VersionToken' is stale in '$DisplayLine'. Expected '$expectedToken' from Directory.Build.props VersionPrefix '$versionPrefix'."
            }

            # Prepared state: the branch may keep describing the line it
            # maintains (for example "stable 7.x package family"), but it must
            # not present the prepared version as the current release.
            $isReleaseClaim = Test-ReleasePublicationClaim $VersionToken $Line $ClaimMatch
            if (Test-VersionTokenMatches $VersionToken $repositoryVersionTokens) {
                if (-not $isReleaseClaim) {
                    return $null
                }

                $expectedToken = Get-ExpectedVersionToken $VersionToken $publishedVersionTokens
                return "release claim '$VersionToken' presents the prepared, unpublished version as the current release in '$DisplayLine'. Expected '$expectedToken' from publication.latestPublishedVersion '$latestPublishedVersion' while publication.state is 'prepared'; describe '$VersionToken' as the prepared next release until the release-preparation pull request switches publication.state to 'released'."
            }

            if (Test-VersionTokenMatches $VersionToken $publishedVersionTokens) {
                return $null
            }

            $repositoryExpectedToken = Get-ExpectedVersionToken $VersionToken $repositoryVersionTokens
            $publishedExpectedToken = Get-ExpectedVersionToken $VersionToken $publishedVersionTokens
            return "release claim '$VersionToken' is stale in '$DisplayLine'. Expected '$repositoryExpectedToken' from prepared Directory.Build.props VersionPrefix '$versionPrefix' or '$publishedExpectedToken' from publication.latestPublishedVersion '$latestPublishedVersion'."
        }

        'published' {
            if (Test-VersionTokenMatches $VersionToken $publishedVersionTokens) {
                return $null
            }

            $expectedToken = Get-ExpectedVersionToken $VersionToken $publishedVersionTokens
            return "published-release claim '$VersionToken' is stale in '$DisplayLine'. Expected '$expectedToken' from publication.latestPublishedVersion '$latestPublishedVersion'."
        }

        'prepared' {
            if ($publicationState -eq 'released') {
                return "prepared-release claim '$VersionToken' remains in '$DisplayLine', but publication.state is 'released'. Replace prepared or unpublished wording with released wording for '$versionPrefix'."
            }

            if (Test-VersionTokenMatches $VersionToken $repositoryVersionTokens) {
                return $null
            }

            $expectedToken = Get-ExpectedVersionToken $VersionToken $repositoryVersionTokens
            return "prepared-release claim '$VersionToken' is stale in '$DisplayLine'. Expected '$expectedToken' from Directory.Build.props VersionPrefix '$versionPrefix'."
        }
    }

    throw "Unknown documentation release-claim kind '$Kind'."
}

$historicalContextPattern = [regex]::new(
    '\b(?:historical|original|initial|previous|prior|superseded|final stable patch|releases? expanded|release established)\b',
    $regexOptions)

$failures = [System.Collections.Generic.List[object]]::new()
$reportedMatches = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)

foreach ($documentationFile in @($documentationFiles | Sort-Object FullName)) {
    $relativePath = [System.IO.Path]::GetRelativePath($repoRoot, $documentationFile.FullName).Replace('\', '/')
    if (Test-ExcludedPath $relativePath) {
        continue
    }

    $lines = @(Get-Content -LiteralPath $documentationFile.FullName)
    $insideCodeFence = $false

    for ($lineIndex = 0; $lineIndex -lt $lines.Count; $lineIndex++) {
        $line = [string]$lines[$lineIndex]

        if ($line -match '^\s*(?:```|~~~)') {
            $insideCodeFence = -not $insideCodeFence
            continue
        }

        if ($insideCodeFence -or $historicalContextPattern.IsMatch($line)) {
            continue
        }

        $displayLine = $line.Trim()
        if ($displayLine.Length -gt 240) {
            $displayLine = $displayLine.Substring(0, 237) + '...'
        }

        foreach ($claimRule in $claimRules) {
            foreach ($claimMatch in $claimRule.Pattern.Matches($line)) {
                $versionGroup = $claimMatch.Groups['version']
                $versionToken = $versionGroup.Value
                $detail = Get-ClaimFailureDetail $claimRule.Kind $versionToken $displayLine $line $claimMatch

                if ($null -eq $detail) {
                    continue
                }

                if (Test-AllowedClaim $relativePath $line) {
                    continue
                }

                $lineNumber = $lineIndex + 1
                $matchKey = "$relativePath|$lineNumber|$($claimRule.Kind)|$($versionGroup.Index)|$versionToken"
                if (-not $reportedMatches.Add($matchKey)) {
                    continue
                }

                $failures.Add([pscustomobject]@{
                    Path = $relativePath
                    LineNumber = $lineNumber
                    Detail = $detail
                })
            }
        }
    }
}

if ($publicationStateErrors.Count -gt 0 -or $failures.Count -gt 0) {
    Write-Host "Documentation release-claim validation failed for VersionPrefix '$versionPrefix' (publication.state '$publicationState', latest published '$latestPublishedVersion')."

    foreach ($stateError in $publicationStateErrors) {
        $message = "$($configurationRelativePath): $stateError"
        Write-Host "::error file=$($configurationRelativePath)::$((ConvertTo-GitHubCommandValue $message))"
        Write-Host "- $message"
    }

    foreach ($failure in $failures) {
        $message = "$($failure.Path):$($failure.LineNumber): $($failure.Detail)"
        Write-Host "::error file=$($failure.Path),line=$($failure.LineNumber)::$((ConvertTo-GitHubCommandValue $message))"
        Write-Host "- $message"
    }

    exit 1
}

Write-Host "Documentation release-claim validation passed for VersionPrefix '$versionPrefix' ($currentMajorLine; publication.state '$publicationState', latest published '$latestPublishedVersion'); scanned $($documentationFiles.Count) file(s)."
