[CmdletBinding()]
param(
    [string]$PinPath = 'tests/AsiBackbone.Samples.NcatAuditCompletionAdapter.Tests/ContractVectors/ncat/ncat-contract-pin.json',

    [string]$UpstreamRef = 'main',

    [switch]$WarnOnly
)

# Compares the vendored NCAT audit-completion contract vectors with the pinned NCAT revision and with the
# latest upstream revision, and reports clearly when NCAT has advanced the contract and the pin needs review.

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-Sha256Hex {
    param([byte[]]$Bytes)

    $hasher = [System.Security.Cryptography.SHA256]::Create()
    try {
        return ([System.BitConverter]::ToString($hasher.ComputeHash($Bytes)) -replace '-', '').ToLowerInvariant()
    }
    finally {
        $hasher.Dispose()
    }
}

function Get-RequestHeaders {
    $headers = @{ 'User-Agent' = 'AsiBackbone-ncat-contract-check' }
    if ($env:GH_TOKEN) {
        $headers['Authorization'] = "Bearer $($env:GH_TOKEN)"
    }

    return $headers
}

function Get-RawBytes {
    param([string]$Repository, [string]$Ref, [string]$Path)

    $uri = "https://raw.githubusercontent.com/$Repository/$Ref/$Path"
    try {
        $response = Invoke-WebRequest -Uri $uri -Headers (Get-RequestHeaders) -UseBasicParsing
    }
    catch {
        $status = $_.Exception.Response.StatusCode.value__
        if ($status -eq 404) {
            return $null
        }

        throw "Unable to download '$uri': $($_.Exception.Message)"
    }

    if ($response.Content -is [byte[]]) {
        return [byte[]]$response.Content
    }

    return [System.Text.UTF8Encoding]::new($false).GetBytes([string]$response.Content)
}

function Write-Summary {
    param([string[]]$Lines)

    $Lines | ForEach-Object { Write-Host $_ }
    if ($env:GITHUB_STEP_SUMMARY) {
        Add-Content -LiteralPath $env:GITHUB_STEP_SUMMARY -Value ($Lines -join [Environment]::NewLine)
    }
}

$resolvedPinPath = (Resolve-Path -LiteralPath $PinPath).Path
$pin = Get-Content -LiteralPath $resolvedPinPath -Raw | ConvertFrom-Json
$vendoredPath = Join-Path (Split-Path -Parent $resolvedPinPath) $pin.vendoredPath
$vendoredSha = Get-Sha256Hex ([System.IO.File]::ReadAllBytes($vendoredPath))

if ($vendoredSha -ne $pin.sha256) {
    throw "The vendored NCAT vectors ($vendoredSha) do not match the pinned SHA-256 ($($pin.sha256)). Re-vendor the file from $($pin.repository)@$($pin.revision) or update the pin."
}

$pinnedBytes = Get-RawBytes -Repository $pin.repository -Ref $pin.revision -Path $pin.sourcePath
if ($null -eq $pinnedBytes) {
    throw "The pinned NCAT vectors were not found at $($pin.repository)@$($pin.revision):$($pin.sourcePath)."
}

$pinnedSha = Get-Sha256Hex $pinnedBytes
if ($pinnedSha -ne $pin.sha256) {
    throw "The vendored NCAT vectors differ from $($pin.repository)@$($pin.revision) ($pinnedSha). The vendored copy must be the exact upstream bytes."
}

$findings = [System.Collections.Generic.List[string]]::new()

$upstreamBytes = Get-RawBytes -Repository $pin.repository -Ref $UpstreamRef -Path $pin.sourcePath
if ($null -eq $upstreamBytes) {
    $findings.Add("``$($pin.sourcePath)`` no longer exists on NCAT ``$UpstreamRef``. NCAT may have retired this contract version.")
}
else {
    $upstreamSha = Get-Sha256Hex $upstreamBytes
    if ($upstreamSha -ne $pin.sha256) {
        $upstreamVersion = ([System.Text.Encoding]::UTF8.GetString($upstreamBytes) | ConvertFrom-Json).contractVersion
        $findings.Add("NCAT ``$UpstreamRef`` publishes different vectors (contract version ``$upstreamVersion``, SHA-256 ``$upstreamSha``) than the pinned revision ``$($pin.revision)``.")
    }
}

$pinnedMajor = [int](($pin.sourcePath -split '/') | Where-Object { $_ -match '^v\d+$' } | Select-Object -First 1).TrimStart('v')
$contractDirectory = ($pin.sourcePath -split '/v\d+/')[0]
$listingUri = "https://api.github.com/repos/$($pin.repository)/contents/$contractDirectory`?ref=$UpstreamRef"

# The newer-major check is part of the drift result, so an unreadable or implausible listing is a finding,
# never a clean pass.
$listed = $false
try {
    $entries = Invoke-RestMethod -Uri $listingUri -Headers (Get-RequestHeaders)
    $listed = $true
}
catch {
    $findings.Add("Unable to list NCAT contract versions at ``$listingUri``, so newer contract major versions were not checked: $($_.Exception.Message)")
}

if ($listed) {
    $versionDirectories = @($entries |
        Where-Object { $_.type -eq 'dir' -and $_.name -match '^v\d+$' } |
        Select-Object -ExpandProperty name)

    if ($versionDirectories -notcontains "v$pinnedMajor") {
        $findings.Add("The NCAT contract listing at ``$listingUri`` does not include the pinned ``v$pinnedMajor`` directory, so newer contract major versions could not be checked reliably.")
    }

    $newerMajors = @($versionDirectories | Where-Object { [int]$_.TrimStart('v') -gt $pinnedMajor })
    if ($newerMajors.Count -gt 0) {
        $findings.Add("NCAT ``$UpstreamRef`` publishes newer contract major version(s): $($newerMajors -join ', ').")
    }
}

if ($findings.Count -eq 0) {
    Write-Summary @(
        '## NCAT contract vectors',
        '',
        "The vendored vectors match $($pin.repository)@$($pin.revision) and NCAT ``$UpstreamRef``. No review is needed.")
    exit 0
}

Write-Summary (@(
        '## NCAT contract vectors need review',
        '',
        "Pinned: ``$($pin.repository)@$($pin.revision)``",
        '') + ($findings | ForEach-Object { "- $_" }) + @(
        '',
        'Follow the update procedure in `docs/articles/ncat-audit-completion-adapter.md`: review the NCAT change, re-vendor the vectors at the new revision, update the pin, and extend `NcatContractVectorTests` for any new vector or rule.'))

if ($WarnOnly) {
    exit 0
}

exit 1
