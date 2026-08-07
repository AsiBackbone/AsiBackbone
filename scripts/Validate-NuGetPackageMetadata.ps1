[CmdletBinding()]
param(
    [string]$PackageDirectory = 'artifacts/packages',
    [string]$ExpectedVersion
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

Add-Type -AssemblyName System.IO.Compression.FileSystem

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
    $node = $project.Project.PropertyGroup.ChildNodes | Where-Object {
        $_.NodeType -eq [System.Xml.XmlNodeType]::Element -and $_.Name -eq $PropertyName
    } | Select-Object -First 1

    if ($null -eq $node) {
        if ($Required) {
            Add-Failure "Property '$PropertyName' was not found in $Path."
        }

        return $null
    }

    return $node.InnerText.Trim()
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

function Assert-ContainsLiteral {
    param(
        [string]$Text,
        [string]$Expected,
        [string]$Description
    )

    if ($Text.IndexOf($Expected, [System.StringComparison]::Ordinal) -lt 0) {
        Add-Failure "$Description should contain '$Expected'."
    }
}

function Get-NuspecElementValue {
    param(
        [System.Xml.XmlElement]$Metadata,
        [string]$ElementName
    )

    $node = $Metadata.SelectSingleNode("./*[local-name()='$ElementName']")
    if ($null -eq $node) {
        return $null
    }

    return $node.InnerText.Trim()
}

function Get-NuspecElement {
    param(
        [System.Xml.XmlElement]$Metadata,
        [string]$ElementName
    )

    return $Metadata.SelectSingleNode("./*[local-name()='$ElementName']")
}

$directoryBuildPropsPath = Resolve-RepositoryPath 'Directory.Build.props'
$directoryVersionPrefix = Get-ProjectProperty $directoryBuildPropsPath 'VersionPrefix'
$directoryVersionSuffix = Get-ProjectProperty $directoryBuildPropsPath 'VersionSuffix' $false
$directoryVersionSuffix = if ($null -eq $directoryVersionSuffix) { '' } else { $directoryVersionSuffix }

if ([string]::IsNullOrWhiteSpace($ExpectedVersion)) {
    $ExpectedVersion = Resolve-Version $directoryVersionPrefix $directoryVersionSuffix
}

$expectedProjectUrl = 'https://asibackbone.github.io/AsiBackbone/'
$expectedRepositoryUrl = 'https://github.com/AsiBackbone/AsiBackbone'
$expectedVersionParts = $ExpectedVersion -split '\.'
$expectedStablePackageFamily = if ($expectedVersionParts.Count -ge 2) {
    'Stable `' + $expectedVersionParts[0] + '.' + $expectedVersionParts[1] + '.x` package family'
}
else {
    'Stable `' + $ExpectedVersion + '` package family'
}

$expectedPackages = @(
    [pscustomobject]@{
        Id = 'AsiBackbone.Core'
        Description = 'Framework-neutral Accountable Systems Infrastructure governance primitives for consequential .NET decision flow.'
        Tags = @('accountable-systems-infrastructure', 'asi-backbone', 'dotnet', 'governance', 'decision-flow', 'constraint-evaluation', 'audit', 'acknowledgment', 'capability-token')
        ReadmeMustContain = @('Accountable Systems Infrastructure', $expectedStablePackageFamily)
    },
    [pscustomobject]@{
        Id = 'AsiBackbone.DependencyInjection'
        Description = 'Explicit AddAsiBackbone builder facade for coordinating host-selected AsiBackbone provider registrations.'
        Tags = @('accountable-systems-infrastructure', 'asi-backbone', 'dotnet', 'governance', 'dependency-injection', 'builder', 'configuration', 'provider-coordination')
        ReadmeMustContain = @('Explicit `AddAsiBackbone(...)` builder facade', 'does not register persistence')
    },
    [pscustomobject]@{
        Id = 'AsiBackbone.Analyzers'
        Description = 'Roslyn analyzer safety rails for Accountable Systems Infrastructure governance persistence and continuation flows.'
        Tags = @('accountable-systems-infrastructure', 'asi-backbone', 'dotnet', 'governance', 'roslyn', 'analyzer', 'safety-rails', 'audit', 'outbox')
        ReadmeMustContain = @('Roslyn analyzer safety rails', 'ASIB001')
    },
    [pscustomobject]@{
        Id = 'AsiBackbone.AspNetCore'
        Description = 'ASP.NET Core host adapters for Accountable Systems Infrastructure actor context, request correlation, HTTP result mapping, and acknowledgment challenge flows.'
        Tags = @('accountable-systems-infrastructure', 'asi-backbone', 'dotnet', 'governance', 'aspnetcore', 'web', 'host-integration', 'http', 'acknowledgment')
        ReadmeMustContain = @('host adapters only', 'do not enforce decisions automatically')
    },
    [pscustomobject]@{
        Id = 'AsiBackbone.Storage.InMemory'
        Description = 'Non-durable in-memory storage helpers for Accountable Systems Infrastructure local validation, tests, and samples.'
        Tags = @('accountable-systems-infrastructure', 'asi-backbone', 'dotnet', 'governance', 'in-memory', 'non-durable', 'storage', 'local-validation', 'testing')
        ReadmeMustContain = @('not durable storage', 'local validation')
    },
    [pscustomobject]@{
        Id = 'AsiBackbone.Testing'
        Description = 'Test harness helpers for Accountable Systems Infrastructure endpoint governance, deterministic policy decisions, and in-memory inspection.'
        Tags = @('accountable-systems-infrastructure', 'asi-backbone', 'dotnet', 'governance', 'testing', 'test-harness', 'aspnetcore', 'policy-evaluation', 'audit')
        ReadmeMustContain = @('test harness helpers', 'not a production enforcement provider')
    },
    [pscustomobject]@{
        Id = 'AsiBackbone.Templates'
        Description = 'dotnet new templates for Accountable Systems Infrastructure governed ASP.NET Core hosts.'
        Tags = @('accountable-systems-infrastructure', 'asi-backbone', 'dotnet', 'governance', 'templates', 'dotnet-new', 'aspnetcore', 'webapi', 'endpoint-governance')
        ReadmeMustContain = @('dotnet new asibackbone-webapi', 'not a runtime dependency')
    },
    [pscustomobject]@{
        Id = 'AsiBackbone.EntityFrameworkCore'
        Description = 'Entity Framework Core model configuration and host-owned persistence helpers for Accountable Systems Infrastructure audit and acknowledgment records.'
        Tags = @('accountable-systems-infrastructure', 'asi-backbone', 'dotnet', 'governance', 'entity-framework-core', 'efcore', 'host-owned-persistence', 'audit-ledger')
        ReadmeMustContain = @('host-owned', 'does not provide or require a package-owned')
    },
    [pscustomobject]@{
        Id = 'AsiBackbone.OpenTelemetry'
        Description = 'OpenTelemetry-friendly Accountable Systems Infrastructure governance emission provider for AsiBackbone decision-flow telemetry.'
        Tags = @('accountable-systems-infrastructure', 'asi-backbone', 'dotnet', 'governance', 'opentelemetry', 'observability', 'audit', 'outbox')
        ReadmeMustContain = @('provider-neutral', 'OpenTelemetry')
    },
    [pscustomobject]@{
        Id = 'AsiBackbone.Signing.LocalDevelopment'
        Description = 'Local-development RSA signing and verification provider for exercising AsiBackbone signing abstractions without cloud key-management dependencies.'
        Tags = @('accountable-systems-infrastructure', 'asi-backbone', 'dotnet', 'governance', 'signing', 'local-development', 'testing', 'audit')
        ReadmeMustContain = @('local development, samples, and tests', 'not a production managed-key provider')
    },
    [pscustomobject]@{
        Id = 'AsiBackbone.Signing.ManagedKey'
        Description = 'Provider-neutral managed-key signing adapter for Accountable Systems Infrastructure governance artifacts without loading raw private keys into Core.'
        Tags = @('accountable-systems-infrastructure', 'asi-backbone', 'dotnet', 'governance', 'signing', 'managed-key', 'key-management', 'audit')
        ReadmeMustContain = @('Provider-neutral managed-key signing adapter', 'raw key material must not be returned')
    }
)

$packageDirectoryPath = Resolve-RepositoryPath $PackageDirectory
if (-not (Test-Path -LiteralPath $packageDirectoryPath -PathType Container)) {
    Add-Failure "Package directory was not found: $PackageDirectory"
}
else {
    $packages = @(Get-ChildItem -LiteralPath $packageDirectoryPath -Filter '*.nupkg' -File | Sort-Object Name)

    if ($packages.Count -eq 0) {
        Add-Failure "No generated packages were found in $PackageDirectory."
    }

    foreach ($expectedPackage in $expectedPackages) {
        $expectedPackageName = "$($expectedPackage.Id).$ExpectedVersion.nupkg"
        $package = $packages | Where-Object { $_.Name -eq $expectedPackageName } | Select-Object -First 1

        if ($null -eq $package) {
            Add-Failure "Expected package '$expectedPackageName' was not found in $PackageDirectory."
            continue
        }

        $archive = $null
        try {
            $archive = [System.IO.Compression.ZipFile]::OpenRead($package.FullName)
            $nuspecEntry = $archive.Entries | Where-Object { $_.FullName -like '*.nuspec' } | Select-Object -First 1

            if ($null -eq $nuspecEntry) {
                Add-Failure "Package '$($package.Name)' does not contain a .nuspec file."
                continue
            }

            $reader = [System.IO.StreamReader]::new($nuspecEntry.Open())
            try {
                [xml]$nuspec = $reader.ReadToEnd()
            }
            finally {
                $reader.Dispose()
            }

            $metadata = $nuspec.package.metadata
            if ($null -eq $metadata) {
                Add-Failure "Package '$($package.Name)' does not contain nuspec metadata."
                continue
            }

            Assert-Equal (Get-NuspecElementValue $metadata 'id') $expectedPackage.Id "$($package.Name) package ID"
            Assert-Equal (Get-NuspecElementValue $metadata 'version') $ExpectedVersion "$($package.Name) package version"
            Assert-Equal (Get-NuspecElementValue $metadata 'description') $expectedPackage.Description "$($package.Name) description"
            Assert-Equal (Get-NuspecElementValue $metadata 'projectUrl') $expectedProjectUrl "$($package.Name) project URL"
            Assert-Equal (Get-NuspecElementValue $metadata 'readme') 'README.md' "$($package.Name) README metadata"

            $licenseNode = Get-NuspecElement $metadata 'license'
            if ($null -eq $licenseNode) {
                Add-Failure "$($package.Name) license metadata was not found."
            }
            else {
                Assert-Equal $licenseNode.GetAttribute('type') 'expression' "$($package.Name) license metadata type"
                Assert-Equal $licenseNode.InnerText.Trim() 'MIT' "$($package.Name) license expression"
            }

            $repositoryNode = Get-NuspecElement $metadata 'repository'
            if ($null -eq $repositoryNode) {
                Add-Failure "$($package.Name) repository metadata was not found."
            }
            else {
                Assert-Equal $repositoryNode.GetAttribute('type') 'git' "$($package.Name) repository type"
                Assert-Equal $repositoryNode.GetAttribute('url') $expectedRepositoryUrl "$($package.Name) repository URL"

                $repositoryCommit = $repositoryNode.GetAttribute('commit')
                if ([string]::IsNullOrWhiteSpace($repositoryCommit)) {
                    Add-Failure "$($package.Name) repository commit metadata was not found."
                }
            }

            $actualTags = @((Get-NuspecElementValue $metadata 'tags') -split '[;\s]+' | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
            foreach ($expectedTag in $expectedPackage.Tags) {
                if ($actualTags -notcontains $expectedTag) {
                    Add-Failure "$($package.Name) expected tag '$expectedTag' was not found. Actual tags: $($actualTags -join ', ')"
                }
            }

            $unexpectedTags = @($actualTags | Where-Object { $expectedPackage.Tags -notcontains $_ })
            if ($unexpectedTags.Count -gt 0) {
                Add-Failure "$($package.Name) contains unexpected tags: $($unexpectedTags -join ', ')"
            }

            $readmeEntry = $archive.Entries | Where-Object { $_.FullName -eq 'README.md' } | Select-Object -First 1
            if ($null -eq $readmeEntry) {
                Add-Failure "Package '$($package.Name)' does not contain README.md at the package root."
            }
            else {
                $readmeReader = [System.IO.StreamReader]::new($readmeEntry.Open())
                try {
                    $readme = $readmeReader.ReadToEnd()
                }
                finally {
                    $readmeReader.Dispose()
                }

                if ([string]::IsNullOrWhiteSpace($readme)) {
                    Add-Failure "Package '$($package.Name)' contains an empty README.md."
                }

                if ($readme -match 'Early alpha package family') {
                    Add-Failure "Package '$($package.Name)' README still describes the package family as early alpha."
                }

                foreach ($requiredReadmeText in $expectedPackage.ReadmeMustContain) {
                    Assert-ContainsLiteral $readme $requiredReadmeText "Package '$($package.Name)' README"
                }
            }
        }
        finally {
            if ($null -ne $archive) {
                $archive.Dispose()
            }
        }
    }

    $expectedPackageNames = @($expectedPackages | ForEach-Object { "$($_.Id).$ExpectedVersion.nupkg" })
    $unexpectedPackages = @($packages | Where-Object { $expectedPackageNames -notcontains $_.Name })
    foreach ($unexpectedPackage in $unexpectedPackages) {
        Add-Failure "Unexpected package artifact was found: $($unexpectedPackage.Name)"
    }
}

if ($failures.Count -gt 0) {
    Write-Host 'NuGet package metadata validation failed.'
    foreach ($failure in $failures) {
        Write-Host "::error::$failure"
        Write-Host "- $failure"
    }

    exit 1
}

Write-Host "NuGet package metadata validation passed for version $ExpectedVersion."
