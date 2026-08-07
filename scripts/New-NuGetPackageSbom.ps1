[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [string]$DocumentNamespaceBase = 'https://github.com/AsiBackbone/AsiBackbone/sbom'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Hex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -Path $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function ConvertTo-SpdxIdPart {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Value
    )

    $sanitized = [regex]::Replace($Value, '[^A-Za-z0-9.-]+', '-')
    $sanitized = $sanitized.Trim('-')

    if ([string]::IsNullOrWhiteSpace($sanitized)) {
        return 'unknown'
    }

    return $sanitized
}

function Get-NuspecDocument {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackagePath
    )

    Add-Type -AssemblyName System.IO.Compression.FileSystem

    $archive = [System.IO.Compression.ZipFile]::OpenRead($PackagePath)

    try {
        $nuspecEntry = $archive.Entries |
            Where-Object { $_.FullName -like '*.nuspec' -and $_.FullName -notlike '*/*' } |
            Select-Object -First 1

        if ($null -eq $nuspecEntry) {
            throw "No root .nuspec entry was found in package '$PackagePath'."
        }

        $stream = $nuspecEntry.Open()

        try {
            $reader = [System.IO.StreamReader]::new($stream)

            try {
                $xml = [xml]$reader.ReadToEnd()
                return $xml
            }
            finally {
                $reader.Dispose()
            }
        }
        finally {
            $stream.Dispose()
        }
    }
    finally {
        $archive.Dispose()
    }
}

function Get-XmlElementValue {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNode]$Parent,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    $node = $Parent.SelectSingleNode("*[local-name()='$Name']")

    if ($null -eq $node) {
        return $null
    }

    return $node.InnerText.Trim()
}

function Get-XmlAttributeValue {
    param(
        [Parameter(Mandatory = $false)]
        [System.Xml.XmlNode]$Node,

        [Parameter(Mandatory = $true)]
        [string]$Name
    )

    if ($null -eq $Node -or $null -eq $Node.Attributes) {
        return $null
    }

    $attribute = $Node.Attributes.GetNamedItem($Name)

    if ($null -eq $attribute) {
        return $null
    }

    $value = [string]$attribute.Value

    if ([string]::IsNullOrWhiteSpace($value)) {
        return $null
    }

    return $value.Trim()
}

function Get-NuGetDependencies {
    param(
        [Parameter(Mandatory = $true)]
        [System.Xml.XmlNode]$MetadataNode
    )

    $dependencyNodes = $MetadataNode.SelectNodes(".//*[local-name()='dependency']")
    $dependencies = New-Object System.Collections.Generic.List[object]

    foreach ($dependencyNode in $dependencyNodes) {
        $id = Get-XmlAttributeValue -Node $dependencyNode -Name 'id'

        if ([string]::IsNullOrWhiteSpace($id)) {
            continue
        }

        $version = Get-XmlAttributeValue -Node $dependencyNode -Name 'version'
        $targetFramework = Get-XmlAttributeValue -Node $dependencyNode.ParentNode -Name 'targetFramework'

        $dependencies.Add([pscustomobject]@{
            Id = $id
            Version = $version
            TargetFramework = $targetFramework
        })
    }

    return $dependencies
}

function New-SpdxPackageExternalRefs {
    param(
        [Parameter(Mandatory = $true)]
        [string]$PackageId,

        [string]$Version
    )

    if ([string]::IsNullOrWhiteSpace($Version)) {
        return @()
    }

    $packageUrl = 'pkg:nuget/{0}@{1}' -f $PackageId, $Version

    return @(
        [ordered]@{
            referenceCategory = 'PACKAGE-MANAGER'
            referenceType = 'purl'
            referenceLocator = $packageUrl
        }
    )
}

$resolvedPackageDirectory = (Resolve-Path -Path $PackageDirectory).Path
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$resolvedOutputDirectory = (Resolve-Path -Path $OutputDirectory).Path

$packageFiles = @(Get-ChildItem -Path $resolvedPackageDirectory -Filter '*.nupkg' -File | Sort-Object Name)

if ($packageFiles.Count -eq 0) {
    throw "No .nupkg files were found in '$resolvedPackageDirectory'."
}

$manifestEntries = New-Object System.Collections.Generic.List[object]
$createdUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')

foreach ($packageFile in $packageFiles) {
    Write-Host "Generating SPDX SBOM for $($packageFile.FullName)"

    $nuspec = Get-NuspecDocument -PackagePath $packageFile.FullName
    $metadata = $nuspec.SelectSingleNode("//*[local-name()='metadata']")

    if ($null -eq $metadata) {
        throw "No nuspec metadata node was found in package '$($packageFile.FullName)'."
    }

    $packageId = Get-XmlElementValue -Parent $metadata -Name 'id'
    $packageVersion = Get-XmlElementValue -Parent $metadata -Name 'version'
    $licenseExpression = Get-XmlElementValue -Parent $metadata -Name 'license'
    $authors = Get-XmlElementValue -Parent $metadata -Name 'authors'
    $description = Get-XmlElementValue -Parent $metadata -Name 'description'
    $packageSha256 = Get-Sha256Hex -Path $packageFile.FullName

    if ([string]::IsNullOrWhiteSpace($packageId)) {
        throw "Package id was missing from '$($packageFile.FullName)'."
    }

    if ([string]::IsNullOrWhiteSpace($packageVersion)) {
        throw "Package version was missing from '$($packageFile.FullName)'."
    }

    if ([string]::IsNullOrWhiteSpace($licenseExpression)) {
        $licenseExpression = 'NOASSERTION'
    }

    $packageSpdxId = 'SPDXRef-Package-{0}' -f (ConvertTo-SpdxIdPart -Value $packageId)
    $documentNamespace = '{0}/{1}/{2}/{3}' -f $DocumentNamespaceBase.TrimEnd('/'), $packageId, $packageVersion, $packageSha256
    $dependencies = @(Get-NuGetDependencies -MetadataNode $metadata)

    $spdxPackages = New-Object System.Collections.Generic.List[object]
    $relationships = New-Object System.Collections.Generic.List[object]

    $mainPackage = [ordered]@{
        name = $packageId
        SPDXID = $packageSpdxId
        versionInfo = $packageVersion
        downloadLocation = 'NOASSERTION'
        filesAnalyzed = $false
        licenseConcluded = 'NOASSERTION'
        licenseDeclared = $licenseExpression
        copyrightText = 'NOASSERTION'
        summary = $description
        supplier = if ([string]::IsNullOrWhiteSpace($authors)) { 'NOASSERTION' } else { "Organization: $authors" }
        checksums = @(
            [ordered]@{
                algorithm = 'SHA256'
                checksumValue = $packageSha256
            }
        )
        externalRefs = @(New-SpdxPackageExternalRefs -PackageId $packageId -Version $packageVersion)
    }

    $spdxPackages.Add($mainPackage)

    $relationships.Add([ordered]@{
        spdxElementId = 'SPDXRef-DOCUMENT'
        relationshipType = 'DESCRIBES'
        relatedSpdxElement = $packageSpdxId
    })

    foreach ($dependency in $dependencies) {
        $dependencyVersion = $dependency.Version
        $dependencyId = 'SPDXRef-Dependency-{0}' -f (ConvertTo-SpdxIdPart -Value ("$($dependency.Id)-$dependencyVersion"))

        $dependencyPackage = [ordered]@{
            name = $dependency.Id
            SPDXID = $dependencyId
            versionInfo = if ([string]::IsNullOrWhiteSpace($dependencyVersion)) { 'NOASSERTION' } else { $dependencyVersion }
            downloadLocation = 'NOASSERTION'
            filesAnalyzed = $false
            licenseConcluded = 'NOASSERTION'
            licenseDeclared = 'NOASSERTION'
            copyrightText = 'NOASSERTION'
            supplier = 'NOASSERTION'
            externalRefs = @(New-SpdxPackageExternalRefs -PackageId $dependency.Id -Version $dependencyVersion)
        }

        if (-not [string]::IsNullOrWhiteSpace($dependency.TargetFramework)) {
            $dependencyPackage['comment'] = "NuGet dependency group target framework: $($dependency.TargetFramework)"
        }

        $spdxPackages.Add($dependencyPackage)

        $relationships.Add([ordered]@{
            spdxElementId = $packageSpdxId
            relationshipType = 'DEPENDS_ON'
            relatedSpdxElement = $dependencyId
        })
    }

    $document = [ordered]@{
        spdxVersion = 'SPDX-2.3'
        dataLicense = 'CC0-1.0'
        SPDXID = 'SPDXRef-DOCUMENT'
        name = "NuGet package SBOM - $packageId $packageVersion"
        documentNamespace = $documentNamespace
        creationInfo = [ordered]@{
            created = $createdUtc
            creators = @(
                'Tool: AsiBackbone New-NuGetPackageSbom.ps1',
                'Organization: AsiBackbone/AsiBackbone'
            )
        }
        packages = @($spdxPackages.ToArray())
        relationships = @($relationships.ToArray())
    }

    $sbomFileName = '{0}.{1}.spdx.json' -f (ConvertTo-SpdxIdPart -Value $packageId), (ConvertTo-SpdxIdPart -Value $packageVersion)
    $sbomPath = Join-Path -Path $resolvedOutputDirectory -ChildPath $sbomFileName

    $document | ConvertTo-Json -Depth 20 | Set-Content -Path $sbomPath -Encoding utf8 -NoNewline

    $manifestEntries.Add([ordered]@{
        packageId = $packageId
        version = $packageVersion
        packageFile = $packageFile.Name
        packageSha256 = $packageSha256
        sbomFile = $sbomFileName
        sbomSha256 = Get-Sha256Hex -Path $sbomPath
        dependencyCount = $dependencies.Count
    })
}

$manifest = [ordered]@{
    schemaVersion = 1
    createdUtc = $createdUtc
    sbomFormat = 'SPDX-2.3 JSON'
    packageDirectory = $resolvedPackageDirectory
    packages = @($manifestEntries.ToArray())
}

$manifestPath = Join-Path -Path $resolvedOutputDirectory -ChildPath 'sbom-manifest.json'
$manifest | ConvertTo-Json -Depth 10 | Set-Content -Path $manifestPath -Encoding utf8 -NoNewline

Write-Host "Generated $($manifestEntries.Count) package SBOM file(s) in '$resolvedOutputDirectory'."
Write-Host "Generated SBOM manifest '$manifestPath'."
