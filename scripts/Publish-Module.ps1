#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Publishes the MediaForgePS module using dotnet publish.

.DESCRIPTION
    This script publishes the MediaForgePS module to the bin directory, ensuring
    all dependencies are copied to the output folder. The published output can
    be used for module import or distribution.

.PARAMETER Configuration
    The build configuration to use (Debug or Release). Defaults to Debug.

.PARAMETER Verbosity
    The verbosity level for dotnet publish. Defaults to minimal.
    Valid values: quiet, minimal, normal, detailed, diagnostic

.EXAMPLE
    .\scripts\Publish-Module.ps1
    Publishes the module in Debug configuration with minimal verbosity.

.EXAMPLE
    .\scripts\Publish-Module.ps1 -Configuration Release
    Publishes the module in Release configuration.

.EXAMPLE
    .\scripts\Publish-Module.ps1 -Configuration Release -Verbosity detailed
    Publishes the module in Release configuration with detailed output.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [Parameter()]
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'minimal'
)

$ErrorActionPreference = 'Stop'

# Determine project directory (one level up from scripts folder, then into src\MediaForgePS)
$projDir = Join-Path $PSScriptRoot '..\src\MediaForgePS'
$csprojPath = Join-Path $projDir 'MediaForgePS.csproj'

# Verify project file exists
if (-not (Test-Path $csprojPath)) {
    throw "Project file not found: $csprojPath"
}

# Set output directory to bin\{Configuration}\net9.0
$outputDir = Join-Path $projDir "bin\$Configuration\net9.0"

# Publish the module
Write-Host "Publishing MediaForgePS module..." -ForegroundColor Cyan
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Output: $outputDir" -ForegroundColor Gray
Write-Host ""

dotnet publish $csprojPath --configuration $Configuration --verbosity $Verbosity --output $outputDir

if ($LASTEXITCODE -ne 0) {
    throw "Publish failed with exit code $LASTEXITCODE"
}

Write-Host "Publish completed successfully." -ForegroundColor Green
