[CmdletBinding()]
param(
    [string]$ExpectedVersion,
    [string]$TagName,
    [string]$PackageDirectory
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$failures = [System.Collections.Generic.List[string]]::new()
$repoRoot = Split-Path -Parent $PSScriptRoot

function Add-Failure {
    param([string]$Message)

    $script:failures.Add($Message)
}

function Resolve-RepositoryPath {
    param([string]$RelativePath)

    return Join-Path $repoRoot $RelativePath
}

function Get-ProjectProperty {
    param(
        [string]$Path,
        [string]$PropertyName,
        [bool]$Required = $true
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        Add-Failure "Required project file was not found: $Path"
        return $null
    }

    [xml]$project = Get-Content -LiteralPath $Path -Raw
    $nodes = @($project.Project.PropertyGroup.ChildNodes | Where-Object {
        $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq $PropertyName
    })

    if ($nodes.Count -eq 0) {
        if ($Required) {
            Add-Failure "Property '$PropertyName' was not found in $Path."
        }

        return $null
    }

    return $nodes[0].InnerText.Trim()
}

function Resolve-Version {
    param(
        [string]$VersionPrefix,
        [string]$VersionSuffix
    )

    if ([string]::IsNullOrWhiteSpace($VersionSuffix)) {
        return $VersionPrefix
    }

    return "$VersionPrefix-$VersionSuffix"
}

function Resolve-StableAssemblyVersion {
    param([string]$SemanticVersion)

    $versionMatch = [regex]::Match($SemanticVersion, '^(?<major>\d+)\.(?<minor>\d+)\.(?<patch>\d+)(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?$')
    if (-not $versionMatch.Success) {
        return $null
    }

    $major = [int]$versionMatch.Groups['major'].Value
    if ($major -eq 0) {
        $major = 1
    }

    return "$major.0.0.0"
}

function Assert-Equal {
    param(
        [string]$Actual,
        [string]$Expected,
        [string]$Description
    )

    if ($Actual -ne $Expected) {
        Add-Failure "$Description expected '$Expected' but found '$Actual'."
    }
}

function Assert-OrdinalEqual {
    param(
        [string]$Actual,
        [string]$Expected,
        [string]$Description
    )

    # Names are case-sensitive, unlike PowerShell's -eq/-ne comparison.
    if (-not [string]::Equals($Actual, $Expected, [System.StringComparison]::Ordinal)) {
        Add-Failure "$Description expected '$Expected' but found '$Actual'."
    }
}

function Get-RegexGroupValue {
    param(
        [string]$Text,
        [string]$Pattern,
        [string]$GroupName,
        [string]$Description,
        [bool]$Required = $true
    )

    $match = [regex]::Match($Text, $Pattern)
    if (-not $match.Success) {
        if ($Required) {
            Add-Failure "$Description was not found."
        }

        return $null
    }

    return $match.Groups[$GroupName].Value
}

$directoryBuildPropsPath = Resolve-RepositoryPath 'Directory.Build.props'
$directoryVersionPrefix = Get-ProjectProperty $directoryBuildPropsPath 'VersionPrefix'
$directoryVersionSuffix = Get-ProjectProperty $directoryBuildPropsPath 'VersionSuffix' $false
$directoryVersionSuffix = if ($null -eq $directoryVersionSuffix) { '' } else { $directoryVersionSuffix }
$resolvedDirectoryVersion = Resolve-Version $directoryVersionPrefix $directoryVersionSuffix

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $ExpectedVersion = $resolvedDirectoryVersion
}

if ($ExpectedVersion -notmatch '^\d+\.\d+\.\d+(-[0-9A-Za-z][0-9A-Za-z.-]*)?$') {
    Add-Failure "Expected version '$ExpectedVersion' is not a supported semantic version. Use MAJOR.MINOR.PATCH with an optional prerelease suffix."
}

Assert-Equal $resolvedDirectoryVersion $ExpectedVersion 'Directory.Build.props resolved version'

$expectedAssemblyVersion = Resolve-StableAssemblyVersion $ExpectedVersion
if ($null -ne $expectedAssemblyVersion) {
    Assert-Equal (Get-ProjectProperty $directoryBuildPropsPath 'AssemblyVersion') $expectedAssemblyVersion 'Directory.Build.props AssemblyVersion'
}

Assert-Equal (Get-ProjectProperty $directoryBuildPropsPath 'FileVersion') "$directoryVersionPrefix.0" 'Directory.Build.props FileVersion'

$projectFiles = @(
    Get-ChildItem -LiteralPath (Resolve-RepositoryPath 'src') -Recurse -Filter '*.csproj' -File |
        Where-Object { $_.FullName -notmatch '[\\/]templates[\\/]' } |
        Sort-Object FullName
)

if ($projectFiles.Count -eq 0) {
    Add-Failure 'No package projects were found under src.'
}

foreach ($projectFile in $projectFiles) {
    $relativeProjectPath = [System.IO.Path]::GetRelativePath($repoRoot, $projectFile.FullName)

    $version = Get-ProjectProperty $projectFile.FullName 'Version' $false
    if (-not [string]::IsNullOrWhiteSpace($version)) {
        $allowedVersionValues = @($ExpectedVersion, '$(VersionPrefix)', '$(Version)')
        if ($allowedVersionValues -notcontains $version) {
            Add-Failure "$relativeProjectPath Version should be '$ExpectedVersion' or derive from shared version metadata, but found '$version'."
        }
    }

    $packageVersion = Get-ProjectProperty $projectFile.FullName 'PackageVersion' $false
    if (-not [string]::IsNullOrWhiteSpace($packageVersion)) {
        $allowedPackageVersionValues = @($ExpectedVersion, '$(VersionPrefix)', '$(Version)')
        if ($allowedPackageVersionValues -notcontains $packageVersion) {
            Add-Failure "$relativeProjectPath PackageVersion should be '$ExpectedVersion' or derive from shared version metadata, but found '$packageVersion'."
        }
    }

    $packageId = Get-ProjectProperty $projectFile.FullName 'PackageId' $false
    if ([string]::IsNullOrWhiteSpace($packageId)) {
        Add-Failure "$relativeProjectPath should define PackageId for package publishing."
    }
}

# Citation and archive titles use the canonical product name. "ASI Backbone"
# (with a space) is a legacy spelling under the AsiBackbone/.github
# LANDING-PAGE-STANDARD.md naming rules, and Zenodo copies the .zenodo.json
# title into each new archived release record.
$canonicalMetadataTitle = 'AsiBackbone'
$citationTitle = $null
$zenodoTitle = $null

$citationPath = Resolve-RepositoryPath 'CITATION.cff'
if (Test-Path -LiteralPath $citationPath -PathType Leaf) {
    $citation = Get-Content -LiteralPath $citationPath -Raw
    $citationVersion = Get-RegexGroupValue $citation '(?m)^version:\s*["'']?(?<version>[^"''\r\n]+)["'']?\s*$' 'version' 'CITATION.cff version metadata' $false
    if (-not [string]::IsNullOrWhiteSpace($citationVersion)) {
        Assert-Equal $citationVersion $ExpectedVersion 'CITATION.cff version metadata'
    }

    $citationTitle = Get-RegexGroupValue $citation '(?m)^title:\s*["'']?(?<title>[^"''\r\n]+?)["'']?\s*$' 'title' 'CITATION.cff title metadata' $false
    if ([string]::IsNullOrWhiteSpace($citationTitle)) {
        Add-Failure 'CITATION.cff title metadata was not found.'
    }
    else {
        Assert-OrdinalEqual $citationTitle $canonicalMetadataTitle 'CITATION.cff title metadata'
    }
}

$zenodoPath = Resolve-RepositoryPath '.zenodo.json'
if (Test-Path -LiteralPath $zenodoPath -PathType Leaf) {
    try {
        $zenodo = Get-Content -LiteralPath $zenodoPath -Raw | ConvertFrom-Json
        $zenodoVersion = $zenodo.version

        if ([string]::IsNullOrWhiteSpace($zenodoVersion)) {
            Add-Failure '.zenodo.json version metadata was not found.'
        }
        else {
            Assert-Equal $zenodoVersion $ExpectedVersion '.zenodo.json version metadata'
        }

        $zenodoTitleProperty = $zenodo.PSObject.Properties['title']
        $zenodoTitle = if ($null -ne $zenodoTitleProperty) { [string]$zenodoTitleProperty.Value } else { $null }
        if ([string]::IsNullOrWhiteSpace($zenodoTitle)) {
            Add-Failure '.zenodo.json title metadata was not found.'
        }
        else {
            Assert-OrdinalEqual $zenodoTitle $canonicalMetadataTitle '.zenodo.json title metadata'
        }
    }
    catch {
        Add-Failure ".zenodo.json could not be parsed as JSON. $($_.Exception.Message)"
    }
}

if (-not [string]::IsNullOrWhiteSpace($citationTitle) -and
    -not [string]::IsNullOrWhiteSpace($zenodoTitle) -and
    -not [string]::Equals($citationTitle, $zenodoTitle, [System.StringComparison]::Ordinal)) {
    Add-Failure "CITATION.cff title '$citationTitle' and .zenodo.json title '$zenodoTitle' must match."
}

if (-not [string]::IsNullOrWhiteSpace($TagName)) {
    $tagMatch = [regex]::Match($TagName, '^v(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z][0-9A-Za-z.-]*)?)$')
    if (-not $tagMatch.Success) {
        Add-Failure "Tag '$TagName' is not a supported release tag. Use vMAJOR.MINOR.PATCH with an optional prerelease suffix."
    }
    else {
        Assert-Equal $tagMatch.Groups['version'].Value $ExpectedVersion 'Git release tag version'
    }
}

if (-not [string]::IsNullOrWhiteSpace($PackageDirectory)) {
    $packageDirectoryPath = Resolve-RepositoryPath $PackageDirectory
    if (-not (Test-Path -LiteralPath $packageDirectoryPath -PathType Container)) {
        Add-Failure "Package directory was not found: $PackageDirectory"
    }
    else {
        $packages = @(Get-ChildItem -LiteralPath $packageDirectoryPath -Filter '*.nupkg' -File | Sort-Object Name)

        if ($packages.Count -eq 0) {
            Add-Failure "No generated packages were found in $PackageDirectory."
        }

        $escapedVersion = [regex]::Escape($ExpectedVersion)
        foreach ($package in $packages) {
            if ($package.Name -notmatch "^.+\.$escapedVersion\.nupkg$") {
                Add-Failure "Generated package filename '$($package.Name)' does not match expected version '$ExpectedVersion'."
            }
        }
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'Version consistency validation failed.'
    foreach ($failure in $failures) {
        Write-Host "::error::$failure"
        Write-Host "- $failure"
    }

    exit 1
}

Write-Host "Version consistency validation passed for version $ExpectedVersion."
