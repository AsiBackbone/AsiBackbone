[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$repositoryRoot = Split-Path -Parent $PSScriptRoot
$agentDirectory = Join-Path $repositoryRoot '.github/agents'
$profiles = @(
    @{ File = 'security-assurance.agent.md'; Scope = 'Fail-open and fail-closed behavior' },
    @{ File = 'architecture-assurance.agent.md'; Scope = 'framework neutrality' },
    @{ File = 'quality-assurance.agent.md'; Scope = 'Negative paths' },
    @{ File = 'release-assurance.agent.md'; Scope = 'Report readiness evidence and blockers only' }
)

$requiredProfileText = @(
    'target: github-copilot',
    'disable-model-invocation: true',
    'user-invocable: true',
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

$errors = [System.Collections.Generic.List[string]]::new()
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
    $frontmatter = [regex]::Match(
        $content,
        '\A---\r?\n(?<yaml>.*?)\r?\n---\r?\n',
        [System.Text.RegularExpressions.RegexOptions]::Singleline)
    if (-not $frontmatter.Success) {
        $errors.Add("$($profile.File) must start with YAML frontmatter.")
    }
    else {
        $toolLines = [regex]::Matches(
            $frontmatter.Groups['yaml'].Value,
            '^tools:\s*.*$',
            [System.Text.RegularExpressions.RegexOptions]::Multiline)
        if ($toolLines.Count -ne 1 -or $toolLines[0].Value -ne 'tools: ["read", "search"]') {
            $errors.Add("$($profile.File) must declare exactly tools: [`"read`", `"search`"].")
        }
    }

    foreach ($required in $requiredProfileText + $profile.Scope) {
        if (-not $content.Contains($required, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("$($profile.File) is missing required text: $required")
        }
    }

    foreach ($forbidden in @('mcp-servers:', '"edit"', '"execute"', '"web"', '"agent"', 'tools: ["*"]')) {
        if ($content.Contains($forbidden, [System.StringComparison]::OrdinalIgnoreCase)) {
            $errors.Add("$($profile.File) contains forbidden tool or MCP configuration: $forbidden")
        }
    }
}

$agentsPath = Join-Path $repositoryRoot 'AGENTS.md'
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

$guidePath = Join-Path $repositoryRoot 'docs/maintainers/agent-assurance.md'
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

if ($errors.Count -gt 0) {
    $errors | ForEach-Object { Write-Error $_ }
    exit 1
}

Write-Host "Validated $($profiles.Count) read-only custom-agent profiles, root guidance and maintainer handoffs."
