$ErrorActionPreference = 'Stop'

function Write-HookResult {
    param(
        [string]$SystemMessage = '',
        [string]$AdditionalContext = ''
    )

    $result = @{}

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

$supportedExtensions = @('.cs', '.csproj', '.props', '.targets', '.sln')
$solutionPath = Join-Path $workspaceRoot 'WearPartsControl.sln'

$filesToFormat = $editedPaths |
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
        (Test-Path $_) -and ($supportedExtensions -contains [IO.Path]::GetExtension($_).ToLowerInvariant())
    } |
    ForEach-Object {
        try {
            Resolve-Path -LiteralPath $_ | Select-Object -ExpandProperty Path
        }
        catch {
        }
    } |
    Sort-Object -Unique

if (-not $filesToFormat -or $filesToFormat.Count -eq 0) {
    Write-HookResult
    exit 0
}

if (-not (Test-Path $solutionPath)) {
    Write-HookResult -SystemMessage 'dotnet format hook skipped: solution file WearPartsControl.sln was not found.'
    exit 0
}

$relativeFiles = foreach ($path in $filesToFormat) {
    [IO.Path]::GetRelativePath($workspaceRoot, $path)
}

$arguments = @(
    'format'
    $solutionPath
    '--include'
) + $relativeFiles

Push-Location $workspaceRoot
try {
    $output = & dotnet @arguments 2>&1
    $exitCode = $LASTEXITCODE
}
finally {
    Pop-Location
}

if ($exitCode -ne 0) {
    $message = 'dotnet format hook failed for: ' + ($relativeFiles -join ', ')
    if ($output) {
        $message += "`n" + (($output | Out-String).Trim())
    }

    Write-HookResult -SystemMessage $message -AdditionalContext $message
    exit 0
}

$summary = 'dotnet format completed for: ' + ($relativeFiles -join ', ')
Write-HookResult -SystemMessage $summary -AdditionalContext $summary
exit 0