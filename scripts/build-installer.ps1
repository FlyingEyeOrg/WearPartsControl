[CmdletBinding()]
param(
    [string]$Configuration = "Release",
    [Alias("RuntimeIdentifier")]
    [string[]]$RuntimeIdentifiers = @("win-x64", "win-x86"),
    [string[]]$TestConfigurations = @("Debug", "Release"),
    [string[]]$TestRuntimeIdentifiers = @("win-x64", "win-x86"),
    [string]$Version = "1.0.0",
    [switch]$SelfContained,
    [switch]$FrameworkDependent,
    [switch]$SkipTests,
    [switch]$Clean
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Version Latest

function Invoke-DotNet {
    param(
        [Parameter(Mandatory = $true)]
        [string[]]$Arguments
    )

    & dotnet @Arguments
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet $($Arguments -join ' ') failed with exit code $LASTEXITCODE."
    }
}

function Get-InstallerPlatform {
    param([string]$RuntimeIdentifier)

    switch ($RuntimeIdentifier) {
        "win-x64" { return "x64" }
        "win-x86" { return "x86" }
        default { throw "Unsupported runtime identifier '$RuntimeIdentifier'. Supported values: win-x64, win-x86." }
    }
}

$repoRoot = Split-Path -Parent $PSScriptRoot
$appProject = Join-Path $repoRoot "src/WearPartsControl/WearPartsControl.csproj"
$testProject = Join-Path $repoRoot "tests/WearPartsControl.Tests/WearPartsControl.Tests.csproj"
$installerProject = Join-Path $repoRoot "installer/WearPartsControl.Installer/WearPartsControl.Installer.wixproj"
$artifactsRoot = Join-Path $repoRoot "artifacts"
$publishRoot = Join-Path $artifactsRoot "publish/WearPartsControl"
$installerOutputDir = Join-Path $artifactsRoot "installer"

if ($SelfContained -and $FrameworkDependent) {
    throw "Specify either -SelfContained or -FrameworkDependent, not both. The default is self-contained."
}

if ($Clean -and (Test-Path $artifactsRoot)) {
    Remove-Item $artifactsRoot -Recurse -Force
}

New-Item -ItemType Directory -Path $publishRoot -Force | Out-Null
New-Item -ItemType Directory -Path $installerOutputDir -Force | Out-Null

Invoke-DotNet @("restore", $appProject, "-p:NuGetAudit=false")
Invoke-DotNet @("restore", $testProject, "-p:NuGetAudit=false")

if (-not $SkipTests) {
    foreach ($testConfiguration in $TestConfigurations) {
        foreach ($testRuntimeIdentifier in $TestRuntimeIdentifiers) {
            Get-InstallerPlatform -RuntimeIdentifier $testRuntimeIdentifier | Out-Null
            Invoke-DotNet @(
                "restore", $testProject,
                "--runtime", $testRuntimeIdentifier,
                "-p:NuGetAudit=false")
            Invoke-DotNet @(
                "test", $testProject,
                "--configuration", $testConfiguration,
                "--runtime", $testRuntimeIdentifier,
                "-p:NuGetAudit=false")
        }
    }
}

$selfContainedValue = if ($FrameworkDependent) { "false" } else { "true" }
$createdPackages = @()

foreach ($runtimeIdentifier in $RuntimeIdentifiers) {
    $installerPlatform = Get-InstallerPlatform -RuntimeIdentifier $runtimeIdentifier
    $publishDir = Join-Path $publishRoot $runtimeIdentifier

    New-Item -ItemType Directory -Path $publishDir -Force | Out-Null

    Invoke-DotNet @(
        "publish", $appProject,
        "--configuration", $Configuration,
        "--runtime", $runtimeIdentifier,
        "--self-contained:$selfContainedValue",
        "-p:Version=$Version",
        "-p:AssemblyVersion=$Version",
        "-p:FileVersion=$Version",
        "-p:PublishSingleFile=false",
        "-p:DebugType=none",
        "-p:DebugSymbols=false",
        "-p:NuGetAudit=false",
        "--output", $publishDir)

    $publishDirForWix = if ($publishDir.EndsWith([IO.Path]::DirectorySeparatorChar)) { $publishDir } else { "$publishDir$([IO.Path]::DirectorySeparatorChar)" }
    $programFilesFolderId = if ($installerPlatform -eq "x64") { "ProgramFiles64Folder" } else { "ProgramFilesFolder" }
    $wixIntermediateOutputPath = Join-Path $artifactsRoot "obj/installer/$installerPlatform/"

    Invoke-DotNet @(
        "build", $installerProject,
        "--no-incremental",
        "--configuration", $Configuration,
        "-p:PackageVersion=$Version",
        "-p:PublishDir=$publishDirForWix",
        "-p:OutputPath=$installerOutputDir",
        "-p:IntermediateOutputPath=$wixIntermediateOutputPath",
        "-p:InstallerPlatform=$installerPlatform",
        "-p:ProgramFilesFolderId=$programFilesFolderId",
        "-p:NuGetAudit=false")

    $msiPath = Get-ChildItem -Path $installerOutputDir -Filter "WearPartsControl-$Version-$installerPlatform.msi" -Recurse |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1

    if (-not $msiPath) {
        throw "MSI package for '$runtimeIdentifier' was not generated in '$installerOutputDir'."
    }

    $createdPackages += $msiPath.FullName
}

Write-Host "MSI packages created:"
$createdPackages | ForEach-Object { Write-Host "  $_" }