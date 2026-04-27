[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [string]$RuntimeIdentifier = "win-x64",
    [string]$Version = "1.0.0",
    [switch]$SelfContained,
    [switch]$SkipTests,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

$repoRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repoRoot "src/WearPartsControl/WearPartsControl.csproj"
$testProject = Join-Path $repoRoot "tests/WearPartsControl.Tests/WearPartsControl.Tests.csproj"
$installerProject = Join-Path $repoRoot "installer/WearPartsControl.Installer/WearPartsControl.Installer.wixproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishDir = Join-Path $artifactsRoot "publish/WearPartsControl"
$installerOutputDir = Join-Path $artifactsRoot "installer"

if ($Clean -and (Test-Path $artifactsRoot)) {
    Remove-Item $artifactsRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishDir -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutputDir -Force | Out-Null

dotnet restore $appProject -p:NuGetAudit=false

if (-not $SkipTests) {
    dotnet test $testProject --configuration $Configuration --no-restore -p:NuGetAudit=false
}

$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

dotnet publish $appProject `
    --configuration $Configuration `
    --runtime $RuntimeIdentifier `
    --self-contained:$selfContainedValue `
    -p:Version=$Version `
    -p:AssemblyVersion=$Version `
    -p:FileVersion=$Version `
    -p:PublishSingleFile=false `
    -p:DebugType=none `
    -p:DebugSymbols=false `
    -p:NuGetAudit=false `
    --output $publishDir

$publishDirForWix = if ($publishDir.EndsWith([IO.Path]::DirectorySeparatorChar)) { $publishDir } else { "$publishDir$([IO.Path]::DirectorySeparatorChar)" }

dotnet build $installerProject `
    --configuration $Configuration `
    -p:PackageVersion=$Version `
    -p:PublishDir=$publishDirForWix `
    -p:OutputPath=$installerOutputDir `
    -p:InstallerPlatform=x64 `
    -p:NuGetAudit=false

$msiPath = Get-ChildItem -Path $installerOutputDir -Filter "WearPartsControl-$Version-*.msi" -Recurse |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if (-not $msiPath) {
    throw "MSI package was not generated in '$installerOutputDir'."
}

Write-Host "MSI package created: $($msiPath.FullName)"