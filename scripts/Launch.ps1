#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Launches a new PowerShell instance with the MediaForgePS module imported for debugging.

.DESCRIPTION
    This script launches a new PowerShell 7.5 instance, imports the MediaForgePS module
    from the published DLL location, and displays the process ID (PID) so you can attach
    a debugger to the process. The session remains open for interactive use and debugging.

.PARAMETER Configuration
    The build configuration to use (Debug or Release). Defaults to Debug.
    The script will look for the module DLL in the corresponding bin directory.

.EXAMPLE
    .\scripts\Launch.ps1
    Launches a new PowerShell instance with the Debug build of MediaForgePS imported.

.EXAMPLE
    .\scripts\Launch.ps1 -Configuration Release
    Launches a new PowerShell instance with the Release build of MediaForgePS imported.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
)

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'MediaForge.DevTools.psm1') -Force

Assert-CommandAvailable -CommandName 'git'
Assert-CommandAvailable -CommandName 'pwsh'

# Determine repository root
$repoRoot = Get-RepoRoot

# Construct path to Import.ps1 script
$importScriptPath = Join-Path -Path $repoRoot -ChildPath 'scripts' -AdditionalChildPath 'Import.ps1'

# Verify Import.ps1 exists
if (-not (Test-Path $importScriptPath)) {
    throw "Import script not found at: $importScriptPath"
}

Write-Host "Launching PowerShell with MediaForgePS module..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host ""

# Launch new PowerShell instance
<#
# Use -NoExit to keep the session open, and -File to execute the import script
$pwshPath = (Get-Command pwsh).Source
$importArgs = @(
    '-NoExit',
    '-File',
    $importScriptPath,
    '-Configuration', $Configuration
)
Start-Process -FilePath $pwshPath -NoNewWindow -ArgumentList $importArgs
#>

$command = @"
Write-Host "`$(`$PSStyle.Dim)Started new PowerShell process `$(`$PSStyle.DimOff)`$(`$PSStyle.Foreground.Cyan)`$PID`$(`$PSStyle.Reset)"
& "$($importScriptPath)" -Configuration "$Configuration"
Set-Alias -Name 'bonus' -Value 'Invoke-BonusFileProcessing' -Scope Global
Set-Alias -Name 'sub' -Value 'Export-Subtitles' -Scope Global
"@
pwsh -NoExit -Command $command

#Write-Host "PowerShell instance launched successfully." -ForegroundColor Green
#Write-Host "Check the new window for the Process ID (PID) to attach your debugger." -ForegroundColor Cyan

