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

.PARAMETER Clean
    Enable the clean step. By default, the solution is not cleaned before building.

.PARAMETER Build
    Enable the build step. By default, the solution is not built unless this parameter is explicitly set.

.PARAMETER Lint
    Enable linting with the specified action. Defaults to 'View'.
    - 'View': Check for formatting issues without fixing them (runs 'dotnet format --verify-no-changes').
    - 'Fix': Auto-fix formatting issues (runs 'dotnet format'). Only runs if build succeeded.

.PARAMETER Test
    Enable test step. Runs all tests in the solution.
    Only runs if build succeeded. Uses --no-build flag if build output exists.

.PARAMETER Publish
    Enable publish step. Publishes the main module project to the bin directory.
    Only runs if build succeeded.

.PARAMETER Verbosity
    The verbosity level for dotnet commands. Defaults to minimal.
    Valid values: quiet, minimal, normal, detailed, diagnostic

.EXAMPLE
    .\scripts\Build-Project.ps1
    Runs all operations (equivalent to -Build -Clean -Lint -Test -Publish).

.EXAMPLE
    .\scripts\Build-Project.ps1 -Configuration Release -Build -Publish
    Builds in Release configuration, then publishes the module.

.EXAMPLE
    .\scripts\Build-Project.ps1 -Build -Lint
    Builds without cleaning, then checks for linting issues (defaults to View).

.EXAMPLE
    .\scripts\Build-Project.ps1 -Build -Lint View
    Builds without cleaning, then checks for linting issues.

.EXAMPLE
    .\scripts\Build-Project.ps1 -Build -Lint Fix
    Builds the solution, then runs lint fix to correct formatting issues.

.EXAMPLE
    .\scripts\Build-Project.ps1 -Configuration Release -Clean -Build -Test
    Cleans, builds in Release configuration, and runs all tests.

.EXAMPLE
    .\scripts\Build-Project.ps1 -Configuration Release -Clean -Build -Lint View -Lint Fix -Test -Publish
    Full workflow: clean, build in Release, check linting, fix linting, test, and publish.
#>
[CmdletBinding(DefaultParameterSetName = "SpecifiedOperations")]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [Parameter(ParameterSetName = "AllOperations")]
    [switch]$Full,

    [Parameter(ParameterSetName = "SpecifiedOperations")]
    [switch]$Clean,

    [Parameter(ParameterSetName = "SpecifiedOperations")]
    [switch]$Build,

    [Parameter(ParameterSetName = "SpecifiedOperations")]
    [ValidateSet('View', 'Fix')]
    [string]$Lint = 'View',

    [Parameter(ParameterSetName = "SpecifiedOperations")]
    [switch]$Test,

    [Parameter(ParameterSetName = "SpecifiedOperations")]
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
#
# --------------------------------------------------------------------------------
# Helper function to check if build output exists for the specified configuration
# --------------------------------------------------------------------------------
#
<#
.SYNOPSIS
    Checks if build output exists for the specified configuration.

.DESCRIPTION
    Tests for the presence of the main module DLL at the expected build output path.
    This is used by various script steps (Test, Publish, Lint Fix) to verify that
    a successful build has occurred before proceeding.

.PARAMETER RepoRoot
    The root directory of the git repository.

.PARAMETER Configuration
    The build configuration to check (Debug or Release).

.PARAMETER ThrowFor
    Optional. If specified and build output does not exist, throws an error
    with a message indicating which operation requires the build.
    Example values are operation names like "Test", "Publish", or "Lint".

.OUTPUTS
    System.Boolean
    Returns $true if build output exists, $false otherwise.

.EXAMPLE
    Test-BuildOutput -RepoRoot $repoRoot -Configuration "Debug"
    Checks if Debug build output exists without throwing an error.

.EXAMPLE
    if (Test-BuildOutput -RepoRoot $repoRoot -Configuration "Release" -ThrowFor "Publish") {
        # Publish step code
    }
    Checks if Release build output exists and throws an error if not found,
    otherwise proceeds with publish operation.
#>
function Test-BuildOutput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory = $true)]
        [string]$RepoRoot,

        [Parameter(Mandatory = $true)]
        [string]$Configuration,

        [Parameter(Mandatory = $false)]
        [string]$ThrowFor
    )
    
    $projDir = Join-Path $RepoRoot 'src\MediaForgePS'
    $dllPath = Join-Path $projDir "bin\$Configuration\net9.0\MediaForgePS.dll"
    $exists = Test-Path $dllPath

    if (-not $exists -and $ThrowFor) {
        throw "$ThrowFor requires a successful build. Build output not found for $Configuration configuration."
    }

    return $exists
}
#
# --------------------------------------------------------------------------------
#

# Check for Full build, and enable all operations if specified
if ($Full) {
    Write-Host "Performing *all* operations for this project" -ForegroundColor Cyan
    Write-Host ""

    $Clean = $true
    $Build = $true
    $Lint = 'Fix'
    $Test = $true
    $Publish = $true
}


# Step 1: Clean (optional, enabled by -Clean)
if ($Clean) {
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

# Step 2: Build (optional, enabled by -Build)
if ($Build) {
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

# Step 3: Lint (optional, enabled with -Lint, defaults to View)
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
        if (Test-BuildOutput -RepoRoot $repoRoot -Configuration $Configuration -ThrowFor "Lint fix") {
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

# Step 4: Test (optional, enabled with -Test)
if ($Test) {
    if (Test-BuildOutput -RepoRoot $repoRoot -Configuration $Configuration -ThrowFor "Test") {
        Write-Host "Running tests..." -ForegroundColor Cyan
        Write-Host "Configuration: $Configuration" -ForegroundColor Gray
        Write-Host ""

        $testArgs = @(
            'test',
            $slnPath,
            '--configuration', $Configuration,
            '--verbosity', $Verbosity,
            '--no-build'
        )

        & dotnet $testArgs

        if ($LASTEXITCODE -ne 0) {
            throw "Tests failed with exit code $LASTEXITCODE"
        }

        Write-Host "Tests completed successfully." -ForegroundColor Green
        Write-Host ""
    }
}

# Step 5: Publish (optional, enabled with -Publish)
if ($Publish) {
    if (Test-BuildOutput -RepoRoot $repoRoot -Configuration $Configuration -ThrowFor "Publish") {
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

