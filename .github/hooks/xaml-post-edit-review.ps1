$ErrorActionPreference = 'Stop'

function Write-HookResult {
    param(
        [string]$SystemMessage = '',
        [string]$AdditionalContext = '',
        [string]$Decision = '',
        [string]$Reason = ''
    )

    $result = @{}

    if ($Decision) {
        $result.decision = $Decision
    }

    if ($Reason) {
        $result.reason = $Reason
    }

    if ($SystemMessage) {
        $result.systemMessage = $SystemMessage
    }

    if ($AdditionalContext) {
        $result.hookSpecificOutput = @{
            hookEventName = 'PostToolUse'
            additionalContext = $AdditionalContext
        }
    }

    $result | ConvertTo-Json -Compress -Depth 6
}

function Get-PathsFromPatch {
    param([string]$PatchText)

    if ([string]::IsNullOrWhiteSpace($PatchText)) {
        return @()
    }

    $matches = [regex]::Matches($PatchText, '(?m)^\*\*\* (?:Add|Update) File: (?<path>.+)$')
    $paths = foreach ($match in $matches) {
        $match.Groups['path'].Value.Trim()
    }

    return $paths
}

function Resolve-EditedPaths {
    param($HookInput)

    $toolName = [string]$HookInput.tool_name
    $toolInput = $HookInput.tool_input
    $paths = New-Object System.Collections.Generic.List[string]

    switch ($toolName) {
        'apply_patch' {
            foreach ($path in Get-PathsFromPatch -PatchText ([string]$toolInput.input)) {
                $paths.Add($path)
            }
        }
        'create_file' {
            if ($toolInput.filePath) {
                $paths.Add([string]$toolInput.filePath)
            }
        }
        'mcp_pylance_mcp_s_pylanceInvokeRefactoring' {
            if ($toolInput.mode -eq 'update' -and $toolInput.fileUri) {
                $uri = [Uri][string]$toolInput.fileUri
                if ($uri.IsFile) {
                    $paths.Add($uri.LocalPath)
                }
            }
        }
    }

    return $paths
}

$rawInput = [Console]::In.ReadToEnd()
if ([string]::IsNullOrWhiteSpace($rawInput)) {
    Write-HookResult
    exit 0
}

$hookInput = $rawInput | ConvertFrom-Json -Depth 10
$editedPaths = Resolve-EditedPaths -HookInput $hookInput

if (-not $editedPaths -or $editedPaths.Count -eq 0) {
    Write-HookResult
    exit 0
}

$workspaceRoot = [string]$hookInput.cwd
if ([string]::IsNullOrWhiteSpace($workspaceRoot)) {
    $workspaceRoot = (Get-Location).Path
}

$projectPath = Join-Path $workspaceRoot 'src/WearPartsControl/WearPartsControl.csproj'
$xamlFiles = $editedPaths |
    Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
    ForEach-Object {
        if ([IO.Path]::IsPathRooted($_)) {
            $_
        }
        else {
            Join-Path $workspaceRoot $_
        }
    } |
    Where-Object {
        (Test-Path $_) -and ([IO.Path]::GetExtension($_).ToLowerInvariant() -eq '.xaml')
    } |
    ForEach-Object {
        try {
            Resolve-Path -LiteralPath $_ | Select-Object -ExpandProperty Path
        }
        catch {
        }
    } |
    Sort-Object -Unique

if (-not $xamlFiles -or $xamlFiles.Count -eq 0) {
    Write-HookResult
    exit 0
}

if (-not (Test-Path $projectPath)) {
    Write-HookResult -SystemMessage 'XAML review hook skipped: project file src/WearPartsControl/WearPartsControl.csproj was not found.'
    exit 0
}

$relativeFiles = foreach ($path in $xamlFiles) {
    [IO.Path]::GetRelativePath($workspaceRoot, $path)
}

$arguments = @(
    'build'
    $projectPath
    '/property:GenerateFullPaths=true'
)

Push-Location $workspaceRoot
try {
    $output = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($exitCode -ne 0) {
    $message = 'XAML validation failed after editing: ' + ($relativeFiles -join ', ')
    if ($output) {
        $message += "`n" + (($output | Out-String).Trim())
    }

    Write-HookResult -SystemMessage $message -AdditionalContext $message -Decision 'block' -Reason $message
    exit 0
}

$guidance = @(
    'XAML files changed: ' + ($relativeFiles -join ', ')
    'dotnet format does not format XAML in this workspace.'
    'Manually review indentation, attribute wrapping, Grid spacing, and localization-friendly layout after XAML edits.'
    'If a Window or UserControl was added or materially changed, ensure the corresponding InitializeComponent/XAML load test is updated or added.'
) -join ' '

Write-HookResult -SystemMessage ('XAML review required for: ' + ($relativeFiles -join ', ')) -AdditionalContext $guidance
exit 0