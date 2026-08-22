<#
    Runtime behavior:
    - When no -Version value is supplied, the script validates the currently documented
      default package version below.
    - For future releases, pass the released package version explicitly, for example:
        ./scripts/Validate-Source-Link-commit-metadata.ps1 -Version '3.2.2'
    - Use -KeepArtifacts only when troubleshooting. By default, downloaded and extracted
      NuGet package verification artifacts are cleaned up before the script exits.
#>

[CmdletBinding()]
param(
    [string]$Version = '3.2.2',
    [switch]$KeepArtifacts
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$expectedRepositoryUrl = 'https://github.com/AsiBackbone/AsiBackbone'

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

        Invoke-WebRequest -Uri $packageUrl -OutFile $nupkgPath
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
