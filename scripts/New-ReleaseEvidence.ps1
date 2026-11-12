[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$PackageDirectory,

    [Parameter(Mandatory = $true)]
    [string]$SbomDirectory,

    # Directory containing one <package-file>.sigstore.json Cosign keyless
    # bundle for every .nupkg. Release evidence is not produced without them.
    [Parameter(Mandatory = $true)]
    [string]$SigstoreBundleDirectory,

    [Parameter(Mandatory = $true)]
    [string]$ReleaseNotesPath,

    [Parameter(Mandatory = $true)]
    [string]$OutputDirectory,

    [Parameter(Mandatory = $true)]
    [string]$TagName,

    [Parameter(Mandatory = $true)]
    [ValidatePattern('^[0-9a-fA-F]{40}$')]
    [string]$SourceCommit,

    [string]$Repository = 'AsiBackbone/AsiBackbone',

    [string]$SignerWorkflowPath = '.github/workflows/publish-packages.yml',

    [string]$SignerWorkflowTrigger = 'push'
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Hex {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

if ($TagName -notmatch '^v(?<version>\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?)$') {
    throw "Tag name '$TagName' must use the form vMAJOR.MINOR.PATCH with an optional prerelease suffix."
}

$version = $Matches['version']

if ($Repository -notmatch '^[^/]+/[^/]+$') {
    throw "Repository '$Repository' must use the owner/name form."
}

$resolvedPackageDirectory = (Resolve-Path -LiteralPath $PackageDirectory).Path
$resolvedSbomDirectory = (Resolve-Path -LiteralPath $SbomDirectory).Path
$resolvedSigstoreBundleDirectory = (Resolve-Path -LiteralPath $SigstoreBundleDirectory).Path
$resolvedReleaseNotesPath = (Resolve-Path -LiteralPath $ReleaseNotesPath).Path

if (-not (Test-Path -LiteralPath $resolvedReleaseNotesPath -PathType Leaf)) {
    throw "Release notes file was not found: $resolvedReleaseNotesPath"
}

if ([string]::IsNullOrWhiteSpace((Get-Content -LiteralPath $resolvedReleaseNotesPath -Raw))) {
    throw "Release notes file is empty: $resolvedReleaseNotesPath"
}

$sbomManifestPath = Join-Path $resolvedSbomDirectory 'sbom-manifest.json'

if (-not (Test-Path -LiteralPath $sbomManifestPath -PathType Leaf)) {
    throw "SBOM manifest was not found: $sbomManifestPath"
}

$sbomManifest = Get-Content -LiteralPath $sbomManifestPath -Raw | ConvertFrom-Json
$packageEntries = @($sbomManifest.packages)

if ($packageEntries.Count -eq 0) {
    throw 'The SBOM manifest does not contain any package entries.'
}

$declaredSbomNames = @($packageEntries | ForEach-Object { [string]$_.sbomFile } | Sort-Object -Unique)
$declaredPackageNames = @($packageEntries | ForEach-Object { [string]$_.packageFile } | Sort-Object -Unique)
$actualPackageFiles = @(Get-ChildItem -LiteralPath $resolvedPackageDirectory -Filter '*.nupkg' -File | Sort-Object Name)
$actualPackageNames = @($actualPackageFiles.Name)
$actualSbomFiles = @(Get-ChildItem -LiteralPath $resolvedSbomDirectory -Filter '*.spdx.json' -File | Sort-Object Name)
$actualSbomNames = @($actualSbomFiles.Name)
$packageDifference = @(Compare-Object -ReferenceObject $declaredPackageNames -DifferenceObject $actualPackageNames)
$sbomDifference = @(Compare-Object -ReferenceObject $declaredSbomNames -DifferenceObject $actualSbomNames)

if ($packageDifference.Count -ne 0) {
    $details = $packageDifference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
    throw "Package files do not match sbom-manifest.json:`n$($details -join "`n")"
}

if ($sbomDifference.Count -ne 0) {
    $details = $sbomDifference | ForEach-Object { "$($_.SideIndicator) $($_.InputObject)" }
    throw "SBOM files do not match sbom-manifest.json:`n$($details -join "`n")"
}

if ($SignerWorkflowPath -notmatch '^\.github/workflows/[A-Za-z0-9_.-]+\.ya?ml$') {
    throw "Signer workflow path '$SignerWorkflowPath' must name a workflow file under .github/workflows."
}

$sigstoreBundleSuffix = '.sigstore.json'
$actualBundleFiles = @(Get-ChildItem -LiteralPath $resolvedSigstoreBundleDirectory -File |
    Where-Object { $_.Name.EndsWith($sigstoreBundleSuffix, [System.StringComparison]::Ordinal) } |
    Sort-Object Name)
$expectedBundleNames = @($actualPackageNames | ForEach-Object { $_ + $sigstoreBundleSuffix })
$actualBundleNames = @($actualBundleFiles | ForEach-Object { $_.Name })
$missingBundleNames = @($expectedBundleNames | Where-Object { $_ -cnotin $actualBundleNames })
$orphanBundleNames = @($actualBundleNames | Where-Object { $_ -cnotin $expectedBundleNames })

if ($missingBundleNames.Count -ne 0) {
    throw "Sigstore bundle missing for package subject(s): $($missingBundleNames -join ', ')"
}

if ($orphanBundleNames.Count -ne 0) {
    throw "Sigstore bundle(s) without a matching package subject: $($orphanBundleNames -join ', ')"
}

$bundleMediaTypes = @{}

foreach ($bundleFile in $actualBundleFiles) {
    if ($bundleFile.Length -eq 0) {
        throw "Sigstore bundle '$($bundleFile.Name)' is empty."
    }

    try {
        $bundleDocument = Get-Content -LiteralPath $bundleFile.FullName -Raw | ConvertFrom-Json
    }
    catch {
        throw "Sigstore bundle '$($bundleFile.Name)' is not valid JSON: $($_.Exception.Message)"
    }

    $mediaTypeProperty = $bundleDocument.PSObject.Properties['mediaType']

    if ($null -eq $mediaTypeProperty -or -not ([string]$mediaTypeProperty.Value).StartsWith('application/vnd.dev.sigstore.bundle', [System.StringComparison]::Ordinal)) {
        throw "Sigstore bundle '$($bundleFile.Name)' does not declare a Sigstore protobuf bundle media type."
    }

    $bundleMediaTypes[$bundleFile.Name] = [string]$mediaTypeProperty.Value
}

foreach ($entry in $packageEntries) {
    $packagePath = Join-Path $resolvedPackageDirectory ([string]$entry.packageFile)
    $actualPackageHash = Get-Sha256Hex -Path $packagePath
    $expectedPackageHash = ([string]$entry.packageSha256).ToLowerInvariant()

    if ($actualPackageHash -ne $expectedPackageHash) {
        throw "Package hash mismatch for '$($entry.packageFile)'. Expected $expectedPackageHash but found $actualPackageHash."
    }

    $sbomPath = Join-Path $resolvedSbomDirectory ([string]$entry.sbomFile)
    $actualHash = Get-Sha256Hex -Path $sbomPath
    $expectedHash = ([string]$entry.sbomSha256).ToLowerInvariant()

    if ($actualHash -ne $expectedHash) {
        throw "SBOM hash mismatch for '$($entry.sbomFile)'. Expected $expectedHash but found $actualHash."
    }
}

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$resolvedOutputDirectory = (Resolve-Path -LiteralPath $OutputDirectory).Path
$existingOutput = @(Get-ChildItem -LiteralPath $resolvedOutputDirectory -Force)

if ($existingOutput.Count -ne 0) {
    throw "Release evidence output directory must be empty: $resolvedOutputDirectory"
}

$assetRecords = New-Object System.Collections.Generic.List[object]

foreach ($packageFile in $actualPackageFiles) {
    $destination = Join-Path $resolvedOutputDirectory $packageFile.Name
    Copy-Item -LiteralPath $packageFile.FullName -Destination $destination
    $assetRecords.Add([ordered]@{
        fileName = $packageFile.Name
        sha256 = Get-Sha256Hex -Path $destination
        mediaType = 'application/zip'
        purpose = 'attested-package'
    })
}

foreach ($sbomFile in $actualSbomFiles) {
    $destination = Join-Path $resolvedOutputDirectory $sbomFile.Name
    Copy-Item -LiteralPath $sbomFile.FullName -Destination $destination
    $assetRecords.Add([ordered]@{
        fileName = $sbomFile.Name
        sha256 = Get-Sha256Hex -Path $destination
        mediaType = 'application/spdx+json'
        purpose = 'package-sbom'
    })
}

$bundleHashes = @{}

foreach ($bundleFile in $actualBundleFiles) {
    $destination = Join-Path $resolvedOutputDirectory $bundleFile.Name
    Copy-Item -LiteralPath $bundleFile.FullName -Destination $destination
    $bundleHash = Get-Sha256Hex -Path $destination
    $bundleHashes[$bundleFile.Name] = $bundleHash
    $assetRecords.Add([ordered]@{
        fileName = $bundleFile.Name
        sha256 = $bundleHash
        mediaType = $bundleMediaTypes[$bundleFile.Name]
        purpose = 'package-sigstore-bundle'
    })
}

$copiedSbomManifestPath = Join-Path $resolvedOutputDirectory 'sbom-manifest.json'
Copy-Item -LiteralPath $sbomManifestPath -Destination $copiedSbomManifestPath
$assetRecords.Add([ordered]@{
    fileName = 'sbom-manifest.json'
    sha256 = Get-Sha256Hex -Path $copiedSbomManifestPath
    mediaType = 'application/json'
    purpose = 'package-to-sbom-map'
})

$releaseNotesFileName = 'asibackbone-{0}-release-notes.md' -f $version
$copiedReleaseNotesPath = Join-Path $resolvedOutputDirectory $releaseNotesFileName
Copy-Item -LiteralPath $resolvedReleaseNotesPath -Destination $copiedReleaseNotesPath
$assetRecords.Add([ordered]@{
    fileName = $releaseNotesFileName
    sha256 = Get-Sha256Hex -Path $copiedReleaseNotesPath
    mediaType = 'text/markdown'
    purpose = 'release-notes'
})

$signerWorkflowRef = "refs/tags/$TagName"
$signerIdentity = "https://github.com/$Repository/$($SignerWorkflowPath)@$signerWorkflowRef"
$verificationCommand = (
    'cosign verify-blob --bundle <package-file>.sigstore.json' +
    " --certificate-identity $signerIdentity" +
    ' --certificate-oidc-issuer https://token.actions.githubusercontent.com' +
    " --certificate-github-workflow-repository $Repository" +
    " --certificate-github-workflow-ref $signerWorkflowRef" +
    " --certificate-github-workflow-sha $($SourceCommit.ToLowerInvariant())" +
    " --certificate-github-workflow-trigger $SignerWorkflowTrigger" +
    ' <package-file>')

$evidenceManifest = [ordered]@{
    schemaVersion = 2
    repository = $Repository
    releaseTag = $TagName
    releaseVersion = $version
    sourceCommit = $SourceCommit.ToLowerInvariant()
    createdUtc = (Get-Date).ToUniversalTime().ToString('yyyy-MM-ddTHH:mm:ssZ')
    assets = @($assetRecords.ToArray())
    packageArtifacts = @($packageEntries | ForEach-Object {
        [ordered]@{
            packageId = [string]$_.packageId
            version = [string]$_.version
            packageFile = [string]$_.packageFile
            packageSha256 = ([string]$_.packageSha256).ToLowerInvariant()
            sbomFile = [string]$_.sbomFile
            sbomSha256 = ([string]$_.sbomSha256).ToLowerInvariant()
            sigstoreBundleFile = [string]$_.packageFile + $sigstoreBundleSuffix
            sigstoreBundleSha256 = $bundleHashes[[string]$_.packageFile + $sigstoreBundleSuffix]
        }
    })
    attestationVerification = [ordered]@{
        repository = $Repository
        command = "gh attestation verify <downloaded-file> --repo $Repository"
    }
    sigstoreSignatures = [ordered]@{
        signatureType = 'Sigstore Cosign keyless blob signature (Sigstore protobuf bundle)'
        subject = 'The exact .nupkg build package attached to this GitHub release'
        certificateIdentity = $signerIdentity
        certificateOidcIssuer = 'https://token.actions.githubusercontent.com'
        workflowRepository = $Repository
        workflowRef = $signerWorkflowRef
        workflowSha = $SourceCommit.ToLowerInvariant()
        workflowTrigger = $SignerWorkflowTrigger
        trustRoot = 'Sigstore public-good instance (Fulcio certificate authority, Rekor transparency log, TUF-distributed trusted root)'
        verificationCommand = $verificationCommand
        boundaries = @(
            'Binds only to the GitHub release build package bytes; NuGet.org repository-signs packages on ingestion, so a NuGet.org download has a different digest and is verified with dotnet nuget verify --all instead.',
            'Is not NuGet author signing, NuGet repository signing, or Authenticode signing.',
            'Does not replace GitHub build-provenance attestations and does not attest runtime governance decisions, receipts, or signing-provider outcomes.'
        )
    }
}

$evidenceManifestPath = Join-Path $resolvedOutputDirectory 'release-evidence-manifest.json'
$evidenceManifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $evidenceManifestPath -Encoding utf8 -NoNewline

Write-Host "Prepared $($assetRecords.Count + 1) durable release evidence file(s) in '$resolvedOutputDirectory'."
Write-Host "Release evidence manifest: $evidenceManifestPath"
