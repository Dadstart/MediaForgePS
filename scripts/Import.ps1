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

# Target framework version (matches MediaForgePS.csproj)
$targetFramework = 'net9.0'

# Determine repository root using git
$repoRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    throw "Failed to determine repository root. Make sure you're in a git repository."
}

$repoRoot = $repoRoot.Trim()
Write-Host "Repo root: $repoRoot"
# Construct path to the module DLL
$dllPath = Join-Path -Path $repoRoot -ChildPath 'src' -AdditionalChildPath ('MediaForgePS', 'bin', $Configuration, $targetFramework, 'MediaForgePS.dll')

# Verify DLL exists
if (-not (Test-Path $dllPath)) {
    Write-Warning "Module DLL not found at: $dllPath"
    $choice = Read-Host "Would you like to run the build script now? (Y/N)"
    if ($choice -match '^(Y|y)') {
        & "$repoRoot/scripts/Build.ps1" -Configuration $Configuration -Build -Publish
        if (-not (Test-Path $dllPath)) {
            throw "Module DLL still not found at: $dllPath`nBuild script did not produce the DLL. Please check for build errors."
        }
    }
    else {
        throw "Module DLL is required at: $dllPath`nPlease build and publish the module first using: .\scripts\Build.ps1 -Configuration $Configuration -Build -Publish"
    }
}

$Host.UI.RawUI.WindowTitle = "MediaForgePS Debug Session - Configuration: $Configuration"
Write-Host ''
Write-Host '========================================' -ForegroundColor Cyan
Write-Host 'MediaForgePS Debug Session' -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Host ''
Write-Host "Process ID (PID): $PID" -ForegroundColor Yellow
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Module DLL: $dllPath" -ForegroundColor Gray
Write-Host ''
Write-Host 'Attach your debugger to this process ID to begin debugging.' -ForegroundColor Green
Write-Host 'The session will remain open for interactive use.' -ForegroundColor Green
Write-Host ''
Import-Module $dllPath
Write-Host 'MediaForgePS module imported successfully.' -ForegroundColor Green
Write-Host ''

