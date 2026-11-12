[CmdletBinding()]
param()

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

# Exercises scripts/Assert-SigstorePackageBundles.ps1 without network access or
# a Sigstore signing identity. A stub verifier stands in for cosign: it accepts
# a blob only when the blob digest and every pinned identity claim match the
# values recorded in the fixture bundle, mirroring the checks cosign performs.

$assertPath = Join-Path $PSScriptRoot 'Assert-SigstorePackageBundles.ps1'
$testRoot = Join-Path ([System.IO.Path]::GetTempPath()) ("asibackbone-sigstore-bundles-{0}" -f [guid]::NewGuid().ToString('N'))
$repository = 'AsiBackbone/AsiBackbone'
$tagName = 'v9.8.7'
$sourceCommit = 'c' * 40
$trustedIdentity = "https://github.com/$repository/.github/workflows/publish-packages.yml@refs/tags/$tagName"
$issuer = 'https://token.actions.githubusercontent.com'
$bundleMediaType = 'application/vnd.dev.sigstore.bundle.v0.3+json'

$stubVerifier = @'
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$options = @{}
$positional = [System.Collections.Generic.List[string]]::new()

foreach ($argument in $args) {
    $text = [string]$argument

    if ($text -match '^--(?<name>[a-z-]+)=(?<value>.*)$') {
        $options[$Matches['name']] = $Matches['value']
    }
    else {
        $positional.Add($text)
    }
}

if ($positional.Count -ne 2 -or $positional[0] -cne 'verify-blob' -or -not $options.ContainsKey('bundle')) {
    Write-Output 'stub: unexpected invocation'
    exit 2
}

$bundle = Get-Content -LiteralPath $options['bundle'] -Raw | ConvertFrom-Json
$blobHash = (Get-FileHash -LiteralPath $positional[1] -Algorithm SHA256).Hash.ToLowerInvariant()

if ($blobHash -ne $bundle.testSubjectSha256) {
    Write-Output 'Error: provided artifact digest does not match the bundle'
    exit 1
}

$claims = [ordered]@{
    'certificate-identity' = $bundle.testCertificateIdentity
    'certificate-oidc-issuer' = $bundle.testIssuer
    'certificate-github-workflow-repository' = $bundle.testWorkflowRepository
    'certificate-github-workflow-ref' = $bundle.testWorkflowRef
    'certificate-github-workflow-sha' = $bundle.testWorkflowSha
    'certificate-github-workflow-trigger' = $bundle.testWorkflowTrigger
}

foreach ($claim in $claims.Keys) {
    if (-not $options.ContainsKey($claim) -or $options[$claim] -cne [string]$claims[$claim]) {
        Write-Output "Error: no matching certificate identity found ($claim)"
        exit 1
    }
}

Write-Output 'Verified OK'
exit 0
'@

$permissiveVerifier = @'
Write-Output 'Verified OK'
exit 0
'@

function Get-Sha256Hex {
    param([string]$Path)

    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

function New-Fixture {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Name,

        [string]$SignerIdentity = $trustedIdentity,

        [string]$SignerRef = "refs/tags/$tagName"
    )

    $root = Join-Path $testRoot $Name
    $packages = Join-Path $root 'packages'
    $bundles = Join-Path $root 'bundles'
    New-Item -ItemType Directory -Path $packages -Force | Out-Null
    New-Item -ItemType Directory -Path $bundles -Force | Out-Null
    $sbomEntries = New-Object System.Collections.Generic.List[object]
    $artifactEntries = New-Object System.Collections.Generic.List[object]

    foreach ($packageId in @('AsiBackbone.Core', 'AsiBackbone.Testing')) {
        $packageFile = "$packageId.9.8.7.nupkg"
        $packagePath = Join-Path $packages $packageFile
        Set-Content -LiteralPath $packagePath -Value "package $packageId" -Encoding utf8 -NoNewline
        $packageHash = Get-Sha256Hex -Path $packagePath

        $bundleFile = "$packageFile.sigstore.json"
        $bundlePath = Join-Path $bundles $bundleFile
        [ordered]@{
            mediaType = $bundleMediaType
            testSubjectSha256 = $packageHash
            testCertificateIdentity = $SignerIdentity
            testIssuer = $issuer
            testWorkflowRepository = $repository
            testWorkflowRef = $SignerRef
            testWorkflowSha = $sourceCommit
            testWorkflowTrigger = 'push'
        } | ConvertTo-Json | Set-Content -LiteralPath $bundlePath -Encoding utf8 -NoNewline

        $sbomEntries.Add([ordered]@{
            packageId = $packageId
            version = '9.8.7'
            packageFile = $packageFile
            packageSha256 = $packageHash
        })
        $artifactEntries.Add([ordered]@{
            packageFile = $packageFile
            packageSha256 = $packageHash
            sigstoreBundleFile = $bundleFile
            sigstoreBundleSha256 = Get-Sha256Hex -Path $bundlePath
        })
    }

    $sbomManifestPath = Join-Path $root 'sbom-manifest.json'
    [ordered]@{ schemaVersion = 1; packages = @($sbomEntries.ToArray()) } |
        ConvertTo-Json -Depth 5 |
        Set-Content -LiteralPath $sbomManifestPath -Encoding utf8 -NoNewline

    $evidenceManifestPath = Join-Path $root 'release-evidence-manifest.json'
    [ordered]@{
        schemaVersion = 2
        repository = $repository
        releaseTag = $tagName
        sourceCommit = $sourceCommit
        packageArtifacts = @($artifactEntries.ToArray())
        sigstoreSignatures = [ordered]@{
            certificateIdentity = $trustedIdentity
            certificateOidcIssuer = $issuer
            workflowRepository = $repository
            workflowRef = "refs/tags/$tagName"
            workflowSha = $sourceCommit
            workflowTrigger = 'push'
        }
    } | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $evidenceManifestPath -Encoding utf8 -NoNewline

    return [pscustomobject]@{
        Packages = $packages
        Bundles = $bundles
        SbomManifest = $sbomManifestPath
        EvidenceManifest = $evidenceManifestPath
    }
}

function Invoke-Assert {
    param(
        [Parameter(Mandatory = $true)]
        [object]$Fixture,

        [Parameter(Mandatory = $true)]
        [string]$Verifier,

        [switch]$UseManifests
    )

    $arguments = @{
        PackageDirectory = $Fixture.Packages
        BundleDirectory = $Fixture.Bundles
        TagName = $tagName
        SourceCommit = $sourceCommit
        Repository = $repository
        CosignPath = $Verifier
        IncludeNegativeChecks = $true
    }

    if ($UseManifests) {
        $arguments['SbomManifestPath'] = $Fixture.SbomManifest
        $arguments['EvidenceManifestPath'] = $Fixture.EvidenceManifest
    }

    & $assertPath @arguments
}

function Assert-Rejected {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Description,

        [Parameter(Mandatory = $true)]
        [string]$ExpectedMessagePattern,

        [Parameter(Mandatory = $true)]
        [scriptblock]$Action
    )

    $rejected = $false

    try {
        & $Action
    }
    catch {
        if ($_.Exception.Message -notlike $ExpectedMessagePattern) {
            throw "$Description failed with an unexpected message: $($_.Exception.Message)"
        }

        $rejected = $true
    }

    if (-not $rejected) {
        throw "$Description was not rejected."
    }

    Write-Host "Rejected as expected: $Description"
}

try {
    New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
    $stubPath = Join-Path $testRoot 'stub-cosign.ps1'
    $permissivePath = Join-Path $testRoot 'permissive-cosign.ps1'
    Set-Content -LiteralPath $stubPath -Value $stubVerifier -Encoding utf8
    Set-Content -LiteralPath $permissivePath -Value $permissiveVerifier -Encoding utf8

    # Valid bundles, pinned identity, matching manifests, and negative checks.
    $valid = New-Fixture -Name 'valid'
    Invoke-Assert -Fixture $valid -Verifier $stubPath -UseManifests

    $missing = New-Fixture -Name 'missing-bundle'
    Remove-Item -LiteralPath (Join-Path $missing.Bundles 'AsiBackbone.Testing.9.8.7.nupkg.sigstore.json')
    Assert-Rejected -Description 'Missing Sigstore bundle' -ExpectedMessagePattern 'Sigstore bundle missing*' -Action {
        Invoke-Assert -Fixture $missing -Verifier $stubPath
    }

    $orphan = New-Fixture -Name 'orphan-bundle'
    Remove-Item -LiteralPath (Join-Path $orphan.Packages 'AsiBackbone.Testing.9.8.7.nupkg')
    Assert-Rejected -Description 'Bundle without a package subject' -ExpectedMessagePattern 'Sigstore bundle(s) without a matching package subject*' -Action {
        Invoke-Assert -Fixture $orphan -Verifier $stubPath
    }

    $nonBundle = New-Fixture -Name 'non-bundle'
    Set-Content -LiteralPath (Join-Path $nonBundle.Bundles 'AsiBackbone.Core.9.8.7.nupkg.sigstore.json') -Value '{"signature":"legacy"}' -Encoding utf8 -NoNewline
    Assert-Rejected -Description 'Bundle without a Sigstore bundle media type' -ExpectedMessagePattern '*does not declare a Sigstore protobuf bundle media type*' -Action {
        Invoke-Assert -Fixture $nonBundle -Verifier $stubPath
    }

    $changedPackage = New-Fixture -Name 'changed-package'
    Set-Content -LiteralPath (Join-Path $changedPackage.Packages 'AsiBackbone.Core.9.8.7.nupkg') -Value 'replaced package' -Encoding utf8 -NoNewline
    Assert-Rejected -Description 'Package digest differing from sbom-manifest.json' -ExpectedMessagePattern 'Package digest mismatch*' -Action {
        Invoke-Assert -Fixture $changedPackage -Verifier $stubPath -UseManifests
    }

    $changedBundle = New-Fixture -Name 'changed-bundle'
    Add-Content -LiteralPath (Join-Path $changedBundle.Bundles 'AsiBackbone.Core.9.8.7.nupkg.sigstore.json') -Value ' ' -Encoding utf8 -NoNewline
    Assert-Rejected -Description 'Bundle digest differing from the evidence manifest' -ExpectedMessagePattern 'Sigstore bundle digest mismatch*' -Action {
        Invoke-Assert -Fixture $changedBundle -Verifier $stubPath -UseManifests
    }

    $tamperedSubject = New-Fixture -Name 'tampered-subject'
    Set-Content -LiteralPath (Join-Path $tamperedSubject.Packages 'AsiBackbone.Core.9.8.7.nupkg') -Value 'tampered package' -Encoding utf8 -NoNewline
    Assert-Rejected -Description 'Subject modified after signing' -ExpectedMessagePattern 'Sigstore verification failed*' -Action {
        Invoke-Assert -Fixture $tamperedSubject -Verifier $stubPath
    }

    $untrustedSigner = New-Fixture -Name 'untrusted-signer' `
        -SignerIdentity "https://github.com/$repository/.github/workflows/ci.yml@refs/heads/main" `
        -SignerRef 'refs/heads/main'
    Assert-Rejected -Description 'Valid signature from an untrusted workflow identity' -ExpectedMessagePattern 'Sigstore verification failed*' -Action {
        Invoke-Assert -Fixture $untrustedSigner -Verifier $stubPath
    }

    $permissive = New-Fixture -Name 'permissive-verifier'
    Assert-Rejected -Description 'Verifier that accepts every input' -ExpectedMessagePattern 'Negative check failed*' -Action {
        Invoke-Assert -Fixture $permissive -Verifier $permissivePath
    }

    Write-Host 'Sigstore package bundle verification tests passed.'
}
finally {
    if (Test-Path -LiteralPath $testRoot) {
        Remove-Item -LiteralPath $testRoot -Recurse -Force
    }
}
