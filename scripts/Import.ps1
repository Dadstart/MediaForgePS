#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Imports the MediaForgePS module and displays debug session information.

.DESCRIPTION
    This script is called by Launch.ps1 to import the MediaForgePS module
    in a new PowerShell instance. It finds the module DLL based on the configuration,
    displays the process ID (PID) and other debug information, then imports the module.

.PARAMETER Configuration
    The build configuration (Debug or Release) being used.

.EXAMPLE
    .\scripts\Import.ps1 -Configuration Debug
    Imports the module and displays debug information.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'MediaForge.DevTools.psm1') -Force

$moduleBaseName = 'MediaForgePS'

$repoRoot = Get-RepoRoot
$targetFramework = Get-MediaForgeTargetFramework -RepoRoot $repoRoot
$isDebugSession = ($Configuration -eq 'Debug')
if ($isDebugSession) {
    Write-Host "Repo root: $repoRoot"
}
# Construct path to the module
$modulePath = Get-MediaForgeBuildOutput -RepoRoot $repoRoot -Configuration $Configuration

function TestModulePathsExist {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$ModuleDir,
        [Parameter()]
        [switch]$ShouldThrow
    )

    function TestPathWithThrow($Path, $ThrowText, $ShouldThrow) {
        if (-not (Test-Path $Path)) {
            if ($ShouldThrow) {
                throw $ThrowText
            }
            return $false
        }

        return $true
    }

    if (-not (TestPathWithThrow -Path $ModuleDir -ThrowText "Module directory not found: $ModuleDir" -ShouldThrow $ShouldThrow)) {
        return $false
    }

    $baseName = Join-Path $ModuleDir $moduleBaseName
    foreach ($fileExtension in ('dll', 'psd1')) {
        $file = "$baseName.$fileExtension"
        if (-not (TestPathWithThrow -Path $file -ThrowText "Module file not found: $file" -ShouldThrow $ShouldThrow)) {
            return $false
        }
    }

    return $true
}

# test that required paths exist
Write-Debug "Testing for module files in `"$modulePath`""
if (-not (TestModulePathsExist -ModuleDir $modulePath)) {
    Write-Warning "Module not found at: $modulePath"
    $choice = Read-Host "Would you like to run the build script now? (Y/N)"
    if ($choice -match '^(Y|y)') {
        & "$repoRoot/scripts/Build.ps1" -Configuration $Configuration -Build -Publish

        # test paths again after build, but throw this time
        TestModulePathsExist -ModuleDir $modulePath -ShouldThrow $true
    }
    else {
        throw "Module is required at: `"$modulePath`". Please build and publish the module first using: .\scripts\Build.ps1 -Configuration $Configuration -Build -Publish"
    }
}

if ($isDebugSession) {
    $Host.UI.RawUI.WindowTitle = "$moduleBaseName Debug Session - Configuration: $Configuration"
    Write-Host ''
    Write-Host '========================================' -ForegroundColor Cyan
    Write-Host "$moduleBaseName Debug Session" -ForegroundColor Cyan
    Write-Host '========================================' -ForegroundColor Cyan
    Write-Host ''
    Write-Host "Process ID (PID): $PID" -ForegroundColor Yellow
    Write-Host "Configuration: $Configuration" -ForegroundColor Gray

    # Create a temporary copy of the module to prevent file locks during rebuilds
    $tempModuleDir = Join-Path -Path $([System.IO.Path]::GetTempPath()) -ChildPath "MediaForgePS_$PID"
    if (Test-Path $tempModuleDir) {
        Remove-Item -Path $tempModuleDir -Recurse -Force -ErrorAction SilentlyContinue
    }

    Write-Host "Copying module to temporary directory: $tempModuleDir" -ForegroundColor Gray
    Copy-Item -Path $modulePath -Destination $tempModuleDir -Recurse -Force

    Write-Host "Module Directory (original): $modulePath" -ForegroundColor Gray
    Write-Host "Module Directory (loaded from): $tempModuleDir" -ForegroundColor Gray
    Write-Host ''
    Write-Host 'Attach your debugger to this process ID to begin debugging.' -ForegroundColor Green
    Write-Host 'The session will remain open for interactive use.' -ForegroundColor Green
    Write-Host ''
    $moduleScriptPath = Join-Path $tempModuleDir "$moduleBaseName.psd1"
    Write-Host "Importing $moduleScriptPath"
}
else {
    $moduleScriptPath = Join-Path $modulePath "$moduleBaseName.psd1"
}
Import-Module $moduleScriptPath
Write-Host "$moduleBaseName module imported successfully." -ForegroundColor Green
if ($isDebugSession) {
    Write-Host ''
}
<#
#>
