#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Builds the MediaForgePS solution with optional clean, linting, and publish steps.

.DESCRIPTION
    This script provides a unified workflow for building the MediaForgePS solution.
    It supports cleaning, building, viewing/fixing linting issues, and publishing
    the main module project. Each step can be enabled or disabled independently.

.PARAMETER Configuration
    The build configuration to use (Debug or Release). Defaults to Debug.

.PARAMETER NoClean
    Skip the clean step. By default, the solution is cleaned before building.

.PARAMETER NoBuild
    Skip the build step. Useful when only running lint or publish operations.

.PARAMETER Lint
    Enable linting with the specified action. Defaults to 'View'.
    - 'View': Check for formatting issues without fixing them (runs 'dotnet format --verify-no-changes').
    - 'Fix': Auto-fix formatting issues (runs 'dotnet format'). Only runs if build succeeded.

.PARAMETER Publish
    Enable publish step. Publishes the main module project to the bin directory.
    Only runs if build succeeded.

.PARAMETER Verbosity
    The verbosity level for dotnet commands. Defaults to minimal.
    Valid values: quiet, minimal, normal, detailed, diagnostic

.EXAMPLE
    .\scripts\Build-Project.ps1
    Cleans and builds the solution in Debug configuration.

.EXAMPLE
    .\scripts\Build-Project.ps1 -Configuration Release -Publish
    Cleans, builds in Release configuration, and publishes the module.

.EXAMPLE
    .\scripts\Build-Project.ps1 -NoClean -Lint
    Builds without cleaning, then checks for linting issues (defaults to View).

.EXAMPLE
    .\scripts\Build-Project.ps1 -NoClean -Lint View
    Builds without cleaning, then checks for linting issues.

.EXAMPLE
    .\scripts\Build-Project.ps1 -NoBuild -Lint Fix
    Skips build and only runs lint fix to correct formatting issues.

.EXAMPLE
    .\scripts\Build-Project.ps1 -Configuration Release -Lint View -Lint Fix -Publish
    Full workflow: clean, build in Release, check linting, fix linting, and publish.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [Parameter()]
    [switch]$NoClean,

    [Parameter()]
    [switch]$NoBuild,

    [Parameter()]
    [ValidateSet('View', 'Fix')]
    [string]$Lint = 'View',

    [Parameter()]
    [switch]$Publish,

    [Parameter()]
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'minimal'
)

$ErrorActionPreference = 'Stop'

# Determine repository root using git
$repoRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    throw "Failed to determine repository root. Make sure you're in a git repository."
}

$repoRoot = $repoRoot.Trim()
$slnPath = Join-Path $repoRoot 'MediaForgePS.sln'

# Verify solution file exists
if (-not (Test-Path $slnPath)) {
    throw "Solution file not found: $slnPath"
}

# Helper function to check if build output exists for the specified configuration
function Test-BuildOutput {
    param(
        [string]$RepoRoot,
        [string]$Configuration
    )
    
    $projDir = Join-Path $RepoRoot 'src\MediaForgePS'
    $dllPath = Join-Path $projDir "bin\$Configuration\net9.0\MediaForgePS.dll"
    
    return (Test-Path $dllPath)
}


# Step 1: Clean and Build (enabled by default, can skip with -NoBuild)
if (-not $NoBuild) {

    # Step 1a: Clean (enabled by default, can skip with -NoClean)
    if (-not $NoClean) {
        Write-Host "Cleaning solution..." -ForegroundColor Cyan
        Write-Host "Configuration: $Configuration" -ForegroundColor Gray
        Write-Host ""

        dotnet clean $slnPath --configuration $Configuration --verbosity $Verbosity

        if ($LASTEXITCODE -ne 0) {
            throw "Clean failed with exit code $LASTEXITCODE"
        }

        Write-Host "Clean completed successfully." -ForegroundColor Green
        Write-Host ""
    }

    # Step 1b: Build
    Write-Host "Building solution..." -ForegroundColor Cyan
    Write-Host "Configuration: $Configuration" -ForegroundColor Gray
    Write-Host ""

    $buildArgs = @(
        'build',
        $slnPath,
        '--configuration', $Configuration,
        '--verbosity', $Verbosity
    )

    & dotnet $buildArgs

    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }

    Write-Host "Build completed successfully." -ForegroundColor Green
    Write-Host ""
}

# Step 2: Lint (optional, enabled with -Lint, defaults to View)
if ($PSBoundParameters.ContainsKey('Lint')) {
    if ($Lint -eq 'View') {
        Write-Host "Checking for linting issues..." -ForegroundColor Cyan
        Write-Host ""

        dotnet format $slnPath --verify-no-changes --verbosity $Verbosity

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Linting issues found. Use -Lint Fix to auto-fix them." -ForegroundColor Yellow
            Write-Host ""
        }
        else {
            Write-Host "No linting issues found." -ForegroundColor Green
            Write-Host ""
        }
    }
    elseif ($Lint -eq 'Fix') {
        if (-not (Test-BuildOutput -RepoRoot $repoRoot -Configuration $Configuration)) {
            Write-Host "Lint fix requires a successful build. Build output not found for $Configuration configuration. Skipping lint fix." -ForegroundColor Yellow
            Write-Host ""
        }
        else {
            Write-Host "Auto-fixing linting issues..." -ForegroundColor Cyan
            Write-Host ""

            dotnet format $slnPath --verbosity $Verbosity

            if ($LASTEXITCODE -ne 0) {
                Write-Host "Lint fix completed with warnings or errors." -ForegroundColor Yellow
                Write-Host ""
            }
            else {
                Write-Host "Lint fix completed successfully." -ForegroundColor Green
                Write-Host ""
            }
        }
    }
}

# Step 3: Publish (optional, enabled with -Publish)
if ($Publish) {
    if (-not (Test-BuildOutput -RepoRoot $repoRoot -Configuration $Configuration)) {
        Write-Host "Publish requires a successful build. Build output not found for $Configuration configuration. Skipping publish." -ForegroundColor Yellow
        Write-Host ""
    }
    else {
        Write-Host "Publishing MediaForgePS module..." -ForegroundColor Cyan
        Write-Host "Configuration: $Configuration" -ForegroundColor Gray

        # Determine project directory
        $projDir = Join-Path $repoRoot 'src\MediaForgePS'
        $csprojPath = Join-Path $projDir 'MediaForgePS.csproj'

        # Verify project file exists
        if (-not (Test-Path $csprojPath)) {
            throw "Project file not found: $csprojPath"
        }

        # Set output directory to bin\{Configuration}\net9.0
        $outputDir = Join-Path $projDir "bin\$Configuration\net9.0"
        Write-Host "Output: $outputDir" -ForegroundColor Gray
        Write-Host ""

        dotnet publish $csprojPath --configuration $Configuration --verbosity $Verbosity --output $outputDir

        if ($LASTEXITCODE -ne 0) {
            throw "Publish failed with exit code $LASTEXITCODE"
        }

        Write-Host "Publish completed successfully." -ForegroundColor Green
    }
}

Write-Host "All requested operations completed." -ForegroundColor Green

