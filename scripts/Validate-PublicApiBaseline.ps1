[CmdletBinding()]
param(
    [string]$SitePath = "docs/_site",
    [string]$BaselineDirectory = "eng/api-baseline",
    [switch]$Update
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$stableAssemblies = [ordered]@{
    "AsiBackbone.Analyzers.dll" = "AsiBackbone.Analyzers.txt"
    "AsiBackbone.AspNetCore.dll" = "AsiBackbone.AspNetCore.txt"
    "AsiBackbone.Core.dll" = "AsiBackbone.Core.txt"
    "AsiBackbone.DependencyInjection.dll" = "AsiBackbone.DependencyInjection.txt"
    "AsiBackbone.EntityFrameworkCore.dll" = "AsiBackbone.EntityFrameworkCore.txt"
    "AsiBackbone.OpenTelemetry.dll" = "AsiBackbone.OpenTelemetry.txt"
    "AsiBackbone.Signing.LocalDevelopment.dll" = "AsiBackbone.Signing.LocalDevelopment.txt"
    "AsiBackbone.Signing.ManagedKey.dll" = "AsiBackbone.Signing.ManagedKey.txt"
    "AsiBackbone.Storage.InMemory.dll" = "AsiBackbone.Storage.InMemory.txt"
    "AsiBackbone.Testing.dll" = "AsiBackbone.Testing.txt"
}

$regexOptions = [System.Text.RegularExpressions.RegexOptions]::IgnoreCase -bor
    [System.Text.RegularExpressions.RegexOptions]::Singleline

function ConvertTo-NormalizedApiText {
    param([Parameter(Mandatory)][string]$Value)

    $withoutTags = [regex]::Replace($Value, '<[^>]+>', '')
    $decoded = [System.Net.WebUtility]::HtmlDecode($withoutTags)
    return [regex]::Replace($decoded, '\s+', ' ').Trim()
}

function Get-StablePublicApiRows {
    param(
        [Parameter(Mandatory)][string]$ApiDirectory,
        [Parameter(Mandatory)][System.Collections.IDictionary]$Assemblies
    )

    if (-not (Test-Path -LiteralPath $ApiDirectory -PathType Container)) {
        throw "DocFX API output was not found at '$ApiDirectory'. Build docs/docfx.json before validating the public API baseline."
    }

    $rowsByAssembly = @{}
    foreach ($assembly in $Assemblies.Keys) {
        $rowsByAssembly[$assembly] = [System.Collections.Generic.HashSet[string]]::new([System.StringComparer]::Ordinal)
    }

    foreach ($file in Get-ChildItem -LiteralPath $ApiDirectory -Filter '*.html' -File) {
        $html = [System.IO.File]::ReadAllText($file.FullName)
        $assemblyMatch = [regex]::Match(
            $html,
            '<dl><dt>Assembly</dt><dd>(?<assembly>[^<]+)</dd></dl>',
            $regexOptions)

        if (-not $assemblyMatch.Success) {
            continue
        }

        $assembly = [System.Net.WebUtility]::HtmlDecode($assemblyMatch.Groups['assembly'].Value)
        if (-not $Assemblies.Contains($assembly)) {
            continue
        }

        $articleMatch = [regex]::Match(
            $html,
            '<article\s+data-uid="(?<uid>[^"]+)"',
            $regexOptions)

        if (-not $articleMatch.Success) {
            throw "Stable API page '$($file.FullName)' identifies assembly '$assembly' but has no article UID."
        }

        $pageUid = [System.Net.WebUtility]::HtmlDecode($articleMatch.Groups['uid'].Value)
        $articleHtml = $html.Substring($articleMatch.Index)
        $typeDeclaration = [regex]::Match(
            $articleHtml,
            '<pre><code class="lang-csharp[^"]*">(?<declaration>.*?)</code></pre>',
            $regexOptions)

        if (-not $typeDeclaration.Success) {
            throw "Stable API page '$($file.FullName)' has no C# type declaration."
        }

        $normalizedType = ConvertTo-NormalizedApiText $typeDeclaration.Groups['declaration'].Value
        [void]$rowsByAssembly[$assembly].Add("TYPE | $pageUid | $normalizedType")

        $memberMatches = [regex]::Matches(
            $html,
            '<h3\b(?=[^>]*\bdata-uid="(?<uid>[^"]+)")[^>]*>',
            $regexOptions)

        for ($index = 0; $index -lt $memberMatches.Count; $index++) {
            $memberMatch = $memberMatches[$index]
            $memberUid = [System.Net.WebUtility]::HtmlDecode($memberMatch.Groups['uid'].Value)
            $segmentStart = $memberMatch.Index + $memberMatch.Length

            if ($index + 1 -lt $memberMatches.Count) {
                $segmentEnd = $memberMatches[$index + 1].Index
            }
            else {
                $articleEnd = $html.IndexOf('</article>', $segmentStart, [System.StringComparison]::OrdinalIgnoreCase)
                $segmentEnd = if ($articleEnd -ge 0) { $articleEnd } else { $html.Length }
            }

            $nextSection = $html.IndexOf('<h2', $segmentStart, $segmentEnd - $segmentStart, [System.StringComparison]::OrdinalIgnoreCase)
            if ($nextSection -ge 0) {
                $segmentEnd = $nextSection
            }

            $memberHtml = $html.Substring($segmentStart, $segmentEnd - $segmentStart)
            $memberDeclaration = [regex]::Match(
                $memberHtml,
                '<pre><code class="lang-csharp[^"]*">(?<declaration>.*?)</code></pre>',
                $regexOptions)

            if (-not $memberDeclaration.Success) {
                throw "Stable API member '$memberUid' on '$($file.FullName)' has no C# declaration."
            }

            $normalizedMember = ConvertTo-NormalizedApiText $memberDeclaration.Groups['declaration'].Value
            [void]$rowsByAssembly[$assembly].Add("MEMBER | $memberUid | $normalizedMember")
        }

        # DocFX renders enum values as <dt id="..."><code>Name = Value</code></dt>
        # rather than h3/member declaration blocks. Keep the display value so enum
        # additions, removals, renames, and numeric changes participate in the gate.
        $enumFieldMatches = [regex]::Matches(
            $html,
            '<dt\s+id="[^"]+"><code>(?<display>.*?)</code></dt>',
            $regexOptions)

        foreach ($enumFieldMatch in $enumFieldMatches) {
            $fieldDisplay = ConvertTo-NormalizedApiText $enumFieldMatch.Groups['display'].Value
            [void]$rowsByAssembly[$assembly].Add("FIELD | $pageUid | $fieldDisplay")
        }
    }

    return $rowsByAssembly
}

function New-BaselineContent {
    param(
        [Parameter(Mandatory)][string]$Assembly,
        [Parameter(Mandatory)][System.Collections.Generic.HashSet[string]]$Rows
    )

    $header = @(
        '# AsiBackbone stable public API baseline',
        '# Baseline release: 5.0.0',
        "# Assembly: $Assembly",
        '# Format: KIND | UID | C# declaration (or enum field display)',
        '# Update only after explicit API/SemVer review.',
        ''
    )

    $orderedRows = @($Rows) | Sort-Object -CaseSensitive
    return (($header + $orderedRows) -join "`n") + "`n"
}

$apiDirectory = Join-Path $SitePath 'api'
$rowsByAssembly = Get-StablePublicApiRows -ApiDirectory $apiDirectory -Assemblies $stableAssemblies

if ($Update -and -not (Test-Path -LiteralPath $BaselineDirectory)) {
    [void](New-Item -ItemType Directory -Path $BaselineDirectory -Force)
}

$utf8NoBom = [System.Text.UTF8Encoding]::new($false)
$failed = $false

foreach ($assembly in $stableAssemblies.Keys) {
    $baselinePath = Join-Path $BaselineDirectory $stableAssemblies[$assembly]
    $actualContent = New-BaselineContent -Assembly $assembly -Rows $rowsByAssembly[$assembly]

    if ($Update) {
        [System.IO.File]::WriteAllText($baselinePath, $actualContent, $utf8NoBom)
        Write-Host "Updated $baselinePath ($($rowsByAssembly[$assembly].Count) API entries)."
        continue
    }

    if (-not (Test-Path -LiteralPath $baselinePath -PathType Leaf)) {
        Write-Error "Public API baseline is missing: $baselinePath"
        $failed = $true
        continue
    }

    $expectedContent = [System.IO.File]::ReadAllText($baselinePath).Replace("`r`n", "`n")
    if ($expectedContent -eq $actualContent) {
        Write-Host "Public API baseline matched for $assembly ($($rowsByAssembly[$assembly].Count) API entries)."
        continue
    }

    $failed = $true
    Write-Error "Unreviewed public API drift detected for $assembly."

    $expectedLines = $expectedContent -split "`n" | Where-Object { $_ -ne '' -and -not $_.StartsWith('#') }
    $actualLines = $actualContent -split "`n" | Where-Object { $_ -ne '' -and -not $_.StartsWith('#') }
    $differences = Compare-Object -ReferenceObject $expectedLines -DifferenceObject $actualLines

    foreach ($difference in ($differences | Select-Object -First 40)) {
        $prefix = if ($difference.SideIndicator -eq '=>') { '+' } else { '-' }
        Write-Host "$prefix $($difference.InputObject)"
    }

    if ($differences.Count -gt 40) {
        Write-Host "... $($differences.Count - 40) additional API baseline differences omitted."
    }
}

if ($Update) {
    Write-Host 'Public API baselines updated. Review the resulting diff and record the SemVer impact before committing.'
    exit 0
}

if ($failed) {
    throw 'Public API baseline validation failed. Intentional API changes require SemVer review and an explicit baseline update in the same pull request.'
}

Write-Host 'All stable managed package public API baselines matched.'
