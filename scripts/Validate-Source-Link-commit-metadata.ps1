<#
    Validates that the AsiBackbone packages published to nuget.org carry the Source Link
    repository metadata that README.md and SECURITY.md tell consumers to verify: a git
    repository type, this repository's URL, and a non-empty commit.

    Runtime behavior:
    - When no -Version value is supplied, the script validates the version declared by
      Directory.Build.props, so the default cannot drift away from the repository.
    - To validate a specific released package version, pass it explicitly, for example:
        ./scripts/Validate-Source-Link-commit-metadata.ps1 -Version '4.0.0'
    - nuget.org does not serve a package the moment it is pushed. Use
      -WaitForPublicationMinutes to poll until every package is downloadable; the
      default of 0 fails immediately, which is what a manual run of an
      already-published version wants.
    - Use -KeepArtifacts only when troubleshooting. By default, downloaded and extracted
      NuGet package verification artifacts are cleaned up before the script exits.
#>

[CmdletBinding()]
param(
    [string]$Version,
    [int]$WaitForPublicationMinutes = 0,
    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedRepositoryUrl = 'https://github.com/AsiBackbone/AsiBackbone'
$repoRoot = Split-Path -Parent $PSScriptRoot

function Get-DeclaredVersion {
    $propsPath = Join-Path $repoRoot 'Directory.Build.props'

    if (-not (Test-Path -LiteralPath $propsPath -PathType Leaf)) {
        throw "Directory.Build.props was not found at $propsPath; pass -Version explicitly."
    }

    [xml]$props = Get-Content -LiteralPath $propsPath -Raw

    $properties = @($props.Project.PropertyGroup.ChildNodes | Where-Object {
        $_.NodeType -eq [System.Xml.XmlNodeType]::Element
    })

    $prefix = @($properties | Where-Object { $_.Name -eq 'VersionPrefix' } | Select-Object -First 1)
    if ($prefix.Count -eq 0) {
        throw "VersionPrefix was not found in Directory.Build.props; pass -Version explicitly."
    }

    $prefixValue = $prefix[0].InnerText.Trim()
    $suffix = @($properties | Where-Object { $_.Name -eq 'VersionSuffix' } | Select-Object -First 1)
    $suffixValue = if ($suffix.Count -eq 0) { '' } else { $suffix[0].InnerText.Trim() }

    if ([string]::IsNullOrWhiteSpace($suffixValue)) {
        return $prefixValue
    }

    return "$prefixValue-$suffixValue"
}

function Save-PublishedPackage {
    <#
        Downloads a published package, tolerating the delay between `dotnet nuget push`
        and nuget.org serving the package from the flat container.
    #>
    param(
        [Parameter(Mandatory = $true)][string]$Uri,
        [Parameter(Mandatory = $true)][string]$OutFile,
        [Parameter(Mandatory = $true)][datetime]$Deadline
    )

    $delaySeconds = 15

    while ($true) {
        try {
            Invoke-WebRequest -Uri $Uri -OutFile $OutFile
            return
        }
        catch {
            if ((Get-Date) -ge $Deadline) {
                throw
            }

            Write-Host "Package is not downloadable yet ($Uri); retrying in $delaySeconds second(s)."
            Start-Sleep -Seconds $delaySeconds
        }
    }
}

if ([string]::IsNullOrWhiteSpace($Version)) {
    $Version = Get-DeclaredVersion
    Write-Host "No -Version supplied; validating the version declared by Directory.Build.props: $Version."
}

Write-Host "Validating Source Link repository metadata for AsiBackbone $Version on nuget.org."

$deadline = (Get-Date).AddMinutes($WaitForPublicationMinutes)

$packageIds = @(
    'AsiBackbone.Core',
    'AsiBackbone.DependencyInjection',
    'AsiBackbone.Storage.InMemory',
    'AsiBackbone.EntityFrameworkCore',
    'AsiBackbone.AspNetCore',
    'AsiBackbone.Testing',
    'AsiBackbone.Templates',
    'AsiBackbone.Analyzers',
    'AsiBackbone.OpenTelemetry',
    'AsiBackbone.Signing.LocalDevelopment',
    'AsiBackbone.Signing.ManagedKey'
)

$workRoot = Join-Path $PWD "nuget-sourcelink-check-$Version"
$exitCode = 0
$results = @()

try {
    Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    New-Item -ItemType Directory -Path $workRoot | Out-Null

    $results = foreach ($packageId in $packageIds) {
        $idLower = $packageId.ToLowerInvariant()

        $packageDirectory = Join-Path $workRoot $packageId
        New-Item -ItemType Directory -Path $packageDirectory | Out-Null

        $nupkgPath = Join-Path $workRoot "$packageId.$Version.nupkg"
        $zipPath = Join-Path $workRoot "$packageId.$Version.zip"

        $packageUrl = "https://api.nuget.org/v3-flatcontainer/$idLower/$Version/$idLower.$Version.nupkg"

        Save-PublishedPackage -Uri $packageUrl -OutFile $nupkgPath -Deadline $deadline
        Copy-Item -LiteralPath $nupkgPath -Destination $zipPath -Force
        Expand-Archive -LiteralPath $zipPath -DestinationPath $packageDirectory -Force

        $nuspecPath = Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nuspec' -Recurse |
            Select-Object -First 1

        if ($null -eq $nuspecPath) {
            throw "No .nuspec found in $packageId $Version."
        }

        [xml]$nuspec = Get-Content -LiteralPath $nuspecPath.FullName -Raw

        $metadataNode = $nuspec.SelectSingleNode("/*[local-name()='package']/*[local-name()='metadata']")
        if ($null -eq $metadataNode) {
            throw "No nuspec metadata node found in $packageId $Version."
        }

        $repositoryNode = $metadataNode.SelectSingleNode("*[local-name()='repository']")
        if ($null -eq $repositoryNode) {
            throw "No repository metadata found in $packageId $Version."
        }

        $repositoryType = $repositoryNode.GetAttribute('type')
        $repositoryUrl = $repositoryNode.GetAttribute('url')
        $repositoryCommit = $repositoryNode.GetAttribute('commit')

        [pscustomobject]@{
            PackageId = $packageId
            RepositoryType = $repositoryType
            RepositoryUrl = $repositoryUrl
            RepositoryCommit = $repositoryCommit
            HasCommit = -not [string]::IsNullOrWhiteSpace($repositoryCommit)
            TypeMatches = $repositoryType -eq 'git'
            UrlMatches = $repositoryUrl -eq $expectedRepositoryUrl
        }
    }

    foreach ($result in $results) {
        if (-not $result.TypeMatches) {
            Write-Error "$($result.PackageId) repository type expected 'git' but found '$($result.RepositoryType)'."
            $exitCode = 1
        }

        if (-not $result.UrlMatches) {
            Write-Error "$($result.PackageId) repository URL expected '$expectedRepositoryUrl' but found '$($result.RepositoryUrl)'."
            $exitCode = 1
        }

        if (-not $result.HasCommit) {
            Write-Error "$($result.PackageId) repository commit metadata was empty."
            $exitCode = 1
        }
    }

    $results | Format-Table -AutoSize
}
finally {
    if (-not $KeepArtifacts) {
        Remove-Item -LiteralPath $workRoot -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if ($exitCode -ne 0) {
    exit $exitCode
}
