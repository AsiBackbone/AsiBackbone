[CmdletBinding()]
param(
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$profiles = @(
    @{ File = 'security-assurance.agent.md'; Scope = 'Fail-open and fail-closed behavior' },
    @{ File = 'architecture-assurance.agent.md'; Scope = 'framework neutrality' },
    @{ File = 'quality-assurance.agent.md'; Scope = 'Negative paths' },
    @{ File = 'release-assurance.agent.md'; Scope = 'Report readiness evidence and blockers only' }
)

$requiredProfileMetadata = @(
    @{ Key = 'target'; Value = 'github-copilot' },
    @{ Key = 'disable-model-invocation'; Value = 'true' },
    @{ Key = 'user-invocable'; Value = 'true' }
)

$requiredProfileBodyText = @(
    '## Prohibited actions',
    'Do not edit files',
    'execute commands',
    'access secrets',
    'approve work',
    '## Review output',
    'severity',
    'file/line evidence',
    'actionable',
    'Uncertainties',
    'validation not performed',
    'Never fabricate'
)

function Get-AgentGovernanceErrors {
param(
    [Parameter(Mandatory)]
    [string]$Root
)

$errors = [System.Collections.Generic.List[string]]::new()
$agentDirectory = Join-Path $Root '.github/agents'
$expectedFiles = @($profiles | ForEach-Object { $_.File })
$actualFiles = @(
    Get-ChildItem -LiteralPath $agentDirectory -File -Filter '*.md' |
        ForEach-Object { $_.Name }
)

foreach ($unexpected in @($actualFiles | Where-Object { $_ -notin $expectedFiles })) {
    $errors.Add("Unexpected custom-agent profile is not governed by this validator: $unexpected")
}

foreach ($profile in $profiles) {
    $path = Join-Path $agentDirectory $profile.File
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        $errors.Add("Missing custom-agent profile: $($profile.File)")
        continue
    }

    $content = Get-Content -LiteralPath $path -Raw
    $body = $content
    $yaml = ''
    $frontmatter = [regex]::Match(
        $content,
        '\A---\r?\n(?<yaml>.*?)\r?\n---\r?\n',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $frontmatter.Success) {
        $errors.Add("$($profile.File) must start with YAML frontmatter.")
    }
    else {
        $yaml = $frontmatter.Groups['yaml'].Value
        $body = $content.Substring($frontmatter.Length)

        foreach ($metadata in $requiredProfileMetadata) {
            $escapedKey = [regex]::Escape($metadata.Key)
            $metadataLines = @(
                $yaml -split '\r?\n' |
                    Where-Object { $_ -match "^\s*(?:'$escapedKey'|`"$escapedKey`"|$escapedKey)\s*:" }
            )
            $expectedPattern = "^\s*(?:'$escapedKey'|`"$escapedKey`"|$escapedKey)\s*:\s*$([regex]::Escape($metadata.Value))\s*(?:#.*)?$"
            if ($metadataLines.Count -ne 1 -or $metadataLines[0] -cnotmatch $expectedPattern) {
                $errors.Add("$($profile.File) must declare exactly $($metadata.Key): $($metadata.Value) in YAML frontmatter.")
            }
        }

        $toolLines = @(
            $yaml -split '\r?\n' |
                Where-Object { $_ -match '^tools:' }
        )
        if ($toolLines.Count -ne 1 -or $toolLines[0] -cne 'tools: ["read", "search"]') {
            $errors.Add("$($profile.File) must declare exactly tools: [`"read`", `"search`"].")
        }

        if ($yaml -match '(?im)^\s*([\x27\x22]?)mcp-servers\1\s*:') {
            $errors.Add("$($profile.File) contains forbidden tool or MCP configuration: mcp-servers")
        }
    }

    foreach ($required in $requiredProfileBodyText + $profile.Scope) {
        if (-not $body.Contains($required, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("$($profile.File) is missing required text: $required")
        }
    }

    foreach ($forbidden in @('"edit"', '"execute"', '"web"', '"agent"', 'tools: ["*"]')) {
        if ($yaml.Contains($forbidden, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("$($profile.File) contains forbidden tool or MCP configuration: $forbidden")
        }
    }
}

$agentsPath = Join-Path $Root 'AGENTS.md'
if (-not (Test-Path -LiteralPath $agentsPath -PathType Leaf)) {
    $errors.Add('Missing root AGENTS.md.')
}
else {
    $agents = Get-Content -LiteralPath $agentsPath -Raw
    foreach ($required in @(
        'governance spine',
        'not an intelligence engine',
        'dotnet restore AsiBackbone.slnx --locked-mode -p:Configuration=Release',
        'dotnet build AsiBackbone.slnx --configuration Release --no-restore',
        'dotnet test --solution AsiBackbone.slnx --configuration Release --no-build --no-restore',
        './scripts/Validate-AgentGovernance.ps1',
        'read-only review',
        'must not edit files',
        'Never disclose'
    )) {
        if (-not $agents.Contains($required, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("AGENTS.md is missing required text: $required")
        }
    }
}

$guidePath = Join-Path $Root 'docs/maintainers/agent-assurance.md'
if (-not (Test-Path -LiteralPath $guidePath -PathType Leaf)) {
    $errors.Add('Missing maintainer agent-assurance guide.')
}
else {
    $guide = Get-Content -LiteralPath $guidePath -Raw
    foreach ($required in @(
        'Example safe prompts',
        'Prohibited-action handling',
        'confirm the actual session',
        'configuration validation, not a live Copilot permission test',
        'Security Assurance',
        'Architecture Assurance',
        'Quality Assurance',
        'Release Assurance'
    )) {
        if (-not $guide.Contains($required, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("Agent assurance guide is missing required text: $required")
        }
    }
}

return @($errors)
}

function Invoke-AgentGovernanceSelfTest {
    $fixtureRoot = Join-Path ([System.IO.Path]::GetTempPath()) "asibackbone-agent-governance-$([guid]::NewGuid())"
    $script:assertions = 0
    $script:fixtureNumber = 0

    function New-GovernanceFixture {
        $script:fixtureNumber++
        $root = Join-Path $fixtureRoot "fixture-$script:fixtureNumber"
        $agentDirectory = Join-Path $root '.github/agents'
        $guideDirectory = Join-Path $root 'docs/maintainers'
        New-Item -ItemType Directory -Path $agentDirectory -Force | Out-Null
        New-Item -ItemType Directory -Path $guideDirectory -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $repositoryRoot 'AGENTS.md') -Destination $root
        Copy-Item -LiteralPath (Join-Path $repositoryRoot 'docs/maintainers/agent-assurance.md') -Destination $guideDirectory
        foreach ($profile in $profiles) {
            Copy-Item -LiteralPath (Join-Path $repositoryRoot ".github/agents/$($profile.File)") -Destination $agentDirectory
        }
        return $root
    }

    function Assert-RejectedFixture {
        param(
            [Parameter(Mandatory)]
            [scriptblock]$Mutation,
            [Parameter(Mandatory)]
            [string]$ExpectedError
        )

        $root = New-GovernanceFixture
        & $Mutation $root
        $actualErrors = @(Get-AgentGovernanceErrors -Root $root)
        $script:assertions++
        if (-not ($actualErrors | Where-Object { $_.Contains($ExpectedError, [System.StringComparison]::OrdinalIgnoreCase) })) {
            throw "Self-test expected an error containing '$ExpectedError', but received: $($actualErrors -join ' | ')"
        }
    }

    try {
        $validRoot = New-GovernanceFixture
        $validErrors = @(Get-AgentGovernanceErrors -Root $validRoot)
        $script:assertions++
        if ($validErrors.Count -ne 0) {
            throw "The unmodified self-test fixture must be valid: $($validErrors -join ' | ')"
        }

        Assert-RejectedFixture -ExpectedError 'Missing custom-agent profile' -Mutation {
            param($root)
            Remove-Item -LiteralPath (Join-Path $root '.github/agents/security-assurance.agent.md')
        }

        Assert-RejectedFixture -ExpectedError 'must start with YAML frontmatter' -Mutation {
            param($root)
            $path = Join-Path $root '.github/agents/architecture-assurance.agent.md'
            $content = Get-Content -LiteralPath $path -Raw
            Set-Content -LiteralPath $path -Value ([regex]::Replace($content, '\A---', 'malformed-frontmatter', 1))
        }

        Assert-RejectedFixture -ExpectedError 'must declare exactly tools' -Mutation {
            param($root)
            $path = Join-Path $root '.github/agents/quality-assurance.agent.md'
            $content = Get-Content -LiteralPath $path -Raw
            Set-Content -LiteralPath $path -Value $content.Replace('tools: ["read", "search"]', 'tools: ["read", "search", "edit"]')
        }

        Assert-RejectedFixture -ExpectedError 'must declare exactly disable-model-invocation: true in YAML frontmatter' -Mutation {
            param($root)
            $path = Join-Path $root '.github/agents/security-assurance.agent.md'
            $content = Get-Content -LiteralPath $path -Raw
            $content = $content.Replace('disable-model-invocation: true', 'disable-model-invocation: false')
            Set-Content -LiteralPath $path -Value "$content`n<!-- disable-model-invocation: true -->"
        }

        Assert-RejectedFixture -ExpectedError 'contains forbidden tool or MCP configuration: mcp-servers' -Mutation {
            param($root)
            $path = Join-Path $root '.github/agents/release-assurance.agent.md'
            $content = Get-Content -LiteralPath $path -Raw
            Set-Content -LiteralPath $path -Value $content.Replace(
                'user-invocable: true',
                "user-invocable: true`n'mcp-servers' :`n  external: enabled")
        }

        Assert-RejectedFixture -ExpectedError 'AGENTS.md is missing required text: governance spine' -Mutation {
            param($root)
            $path = Join-Path $root 'AGENTS.md'
            $content = Get-Content -LiteralPath $path -Raw
            Set-Content -LiteralPath $path -Value $content.Replace('governance spine', 'library')
        }

        Assert-RejectedFixture -ExpectedError 'Agent assurance guide is missing required text: Prohibited-action handling' -Mutation {
            param($root)
            $path = Join-Path $root 'docs/maintainers/agent-assurance.md'
            $content = Get-Content -LiteralPath $path -Raw
            Set-Content -LiteralPath $path -Value $content.Replace('Prohibited-action handling', 'Restricted requests')
        }

        Write-Host "Agent governance validator self-test passed ($script:assertions assertions)."
    }
    finally {
        if (Test-Path -LiteralPath $fixtureRoot) {
            Remove-Item -LiteralPath $fixtureRoot -Recurse -Force
        }
    }
}

if ($SelfTest) {
    Invoke-AgentGovernanceSelfTest
    return
}

$errors = @(Get-AgentGovernanceErrors -Root $repositoryRoot)
if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error -Message $_ -ErrorAction Continue }
    exit 1
}

Write-Host "Validated $($profiles.Count) read-only custom-agent profiles, root guidance and maintainer handoffs."
