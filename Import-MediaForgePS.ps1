#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Installs and imports the MediaForgePS module in a new PowerShell session.

.DESCRIPTION
    This script builds the MediaForgePS module (if needed) and imports it into
    a new isolated PowerShell process, allowing you to use the cmdlets. By default,
    it spawns a new PowerShell process to ensure clean module loading.

.PARAMETER NoIsolatedProcess
    When specified, imports the module in the current PowerShell process instead
    of spawning a separate one. By default, the module is imported in an isolated
    process for clean state.

.PARAMETER BuildConfiguration
    The build configuration to use (Debug or Release). Defaults to Release.

.EXAMPLE
    .\Install-MediaForgePS.ps1
    Builds and imports the module in a new PowerShell session.

.EXAMPLE
    .\Install-MediaForgePS.ps1 -BuildConfiguration Debug
    Builds the module in Debug configuration and imports it.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [switch]$NoIsolatedProcess,

    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$BuildConfiguration = 'Debug'
)

$ErrorActionPreference = 'Stop'

try {
    # Determine the module path
    $modulePath = Join-Path $PSScriptRoot "src\MediaForgePS"
    
    if (-not (Test-Path $modulePath)) {
        throw "Module path not found: $modulePath"
    }
    
    # Publish the module to ensure all dependencies are copied
    Write-Host "Publishing MediaForgePS module..." -ForegroundColor Cyan
    Write-Host "Configuration: $BuildConfiguration" -ForegroundColor Gray
    Write-Host ""
    
    $projectPath = Join-Path $modulePath "MediaForgePS.csproj"
    
    if (-not (Test-Path $projectPath)) {
        throw "Project file not found: $projectPath"
    }
    
    $publishArgs = @(
        'publish',
        $projectPath,
        '--configuration', $BuildConfiguration,
        '--verbosity', 'minimal',
        '--output', (Join-Path $modulePath "bin\$BuildConfiguration\net9.0")
    )
    
    & dotnet $publishArgs | Out-Null
    
    if ($LASTEXITCODE -ne 0) {
        throw "Publish failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "Publish completed successfully." -ForegroundColor Green
    Write-Host ""
    
    # Check if module DLL exists
    $dllPath = Join-Path $modulePath "bin\$BuildConfiguration\net9.0\MediaForgePS.dll"
    
    if (-not (Test-Path $dllPath)) {
        throw "Module DLL not found at expected path: $dllPath"
    }
    
    # Import the module
    Write-Host "Importing MediaForgePS module..." -ForegroundColor Cyan
    Write-Host "Module path: $modulePath" -ForegroundColor Gray
    Write-Host ""
    
    Import-Module $modulePath -Force -ErrorAction Stop
    
    Write-Host "MediaForgePS module imported successfully!" -ForegroundColor Green
    Write-Host ""
    Write-Host "Available cmdlets:" -ForegroundColor Cyan
    Get-Command -Module MediaForgePS | ForEach-Object {
        Write-Host "  $($_.Name)" -ForegroundColor Gray
    }
    Write-Host ""
    Write-Host "You can now use the MediaForgePS cmdlets in this session." -ForegroundColor Green
}
catch {
    Write-Error "Failed to install MediaForgePS module: $_"
    exit 1
}
