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
    Enable linting with the specified action. Defaults to 'None'.
    - 'None': Skip linting.
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
[CmdletBinding(DefaultParameterSetName = "PartialBuild")]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug',

    [Parameter(ParameterSetName = "FullBuild")]
    [switch]$Full,

    [Parameter(ParameterSetName = "PartialBuild")]
    [switch]$Clean,

    [Parameter(ParameterSetName = "PartialBuild")]
    [switch]$Build,

    [Parameter(ParameterSetName = "FullBuild")]
    [Parameter(ParameterSetName = "PartialBuild")]
    [ValidateSet('None', 'View', 'Fix')]
    [string]$Lint = 'None',

    [Parameter(ParameterSetName = "PartialBuild")]
    [switch]$Test,

    [Parameter(ParameterSetName = "PartialBuild")]
    [switch]$Publish,

    [Parameter]
    [ValidateSet('quiet', 'minimal', 'normal', 'detailed', 'diagnostic')]
    [string]$Verbosity = 'minimal'
)

$ErrorActionPreference = 'Stop'

#
# --------------------------------------------------------------------------------
# Helper function to validate required external commands
# --------------------------------------------------------------------------------
#
<#
.SYNOPSIS
    Validates that a required external command is available.

.DESCRIPTION
    Checks if a command exists in the PATH and is executable.
    Throws an error if the command is not found.

.PARAMETER CommandName
    The name of the command to validate (e.g., 'git', 'dotnet').

.EXAMPLE
    Test-Command -CommandName 'git'
    Validates that git is available in the PATH.
#>
function Test-Command {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$CommandName
    )

    $command = Get-Command $CommandName -ErrorAction SilentlyContinue
    if (-not $command) {
        throw "Required command '$CommandName' not found. Please install it and ensure it's in your PATH."
    }

    Write-Verbose "Command '$CommandName' found at: $($command.Source)"
}

#
# --------------------------------------------------------------------------------
#

# Validate required external commands
Test-Command -CommandName 'git'
Test-Command -CommandName 'dotnet'

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

.PARAMETER Operation
    Optional. If specified and build output does not exist, throws an error
    with a message indicating which operation requires the build.
    Example values are operation names like "Test", "Publish", or "Lint fix".

.OUTPUTS
    System.Boolean
    Returns $true if build output exists, $false otherwise.

.EXAMPLE
    Test-BuildOutput -RepoRoot $repoRoot -Configuration "Debug"
    Checks if Debug build output exists without throwing an error. Returns $true if found, $false otherwise.

.EXAMPLE
    if (Test-BuildOutput -RepoRoot $repoRoot -Configuration "Release" -Operation "Publish") {
        # Publish step code
    }
    Checks if Release build output exists. Returns $true if found, $false otherwise.
    If Operation is specified and output is missing, throws an error.
#>
function Test-BuildOutput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot,

        [Parameter(Mandatory)]
        [string]$Configuration,

        [Parameter]
        [string]$Operation
    )
    
    $projDir = Join-Path $RepoRoot 'src\MediaForgePS'
    $dllPath = Join-Path $projDir "bin\$Configuration\net9.0\MediaForgePS.dll"

    $exists = Test-Path $dllPath
    if (-not $exists -and $Operation) {
        throw "$Operation requires a successful build. Build output not found for $Configuration configuration."
    }

    return $exists
}

#
# --------------------------------------------------------------------------------
#

# Check if any operations were requested
$operationsRequested = $Full -or $Clean -or $Build -or ($Lint -ne 'None') -or $Test -or $Publish

# If no operations requested, enable all operations by default
if (-not $operationsRequested) {
    Write-Host "No operations specified. Running all operations by default." -ForegroundColor Cyan
    Write-Information "Build:DefaultOperations:Enabled=All" -InformationAction Continue
    $Full = $true
}

# Check for Full build, and enable all operations if specified
if ($Full) {
    Write-Host "Performing *all* operations for this project" -ForegroundColor Cyan
    Write-Host ""
    Write-Information "Build:Full:Enabled=True" -InformationAction Continue

    $Clean = $true
    $Build = $true

    # override lint to fix unless -Lint was explicitly set
    if (-not $PSBoundParameters.ContainsKey('Lint')) {
        $Lint = 'Fix'
    }
    $Test = $true
    $Publish = $true
}


# Step 1: Clean (optional, enabled by -Clean)
if ($Clean) {
    Write-Host "Cleaning solution..." -ForegroundColor Cyan
    Write-Host "Configuration: $Configuration" -ForegroundColor Gray
    Write-Host ""
    Write-Information "Build:Clean:Started:Configuration=$Configuration" -InformationAction Continue

    dotnet clean $slnPath --configuration $Configuration --verbosity $Verbosity

    if ($LASTEXITCODE -ne 0) {
        Write-Information "Build:Clean:Failed:ExitCode=$LASTEXITCODE" -InformationAction Continue
        throw "Clean failed with exit code $LASTEXITCODE"
    }

    Write-Host "Clean completed successfully." -ForegroundColor Green
    Write-Host ""
    Write-Information "Build:Clean:Completed:Success=True" -InformationAction Continue
}

# Step 2: Build (optional, enabled by -Build)
if ($Build) {
    Write-Host "Building solution..." -ForegroundColor Cyan
    Write-Host "Configuration: $Configuration" -ForegroundColor Gray
    Write-Host ""
    Write-Information "Build:Build:Started:Configuration=$Configuration:Solution=$slnPath" -InformationAction Continue

    $buildArgs = @(
        'build',
        $slnPath,
        '--configuration', $Configuration,
        '--verbosity', $Verbosity
    )

    & dotnet $buildArgs

    if ($LASTEXITCODE -ne 0) {
        Write-Information "Build:Build:Failed:ExitCode=$LASTEXITCODE" -InformationAction Continue
        throw "Build failed with exit code $LASTEXITCODE"
    }

    Write-Host "Build completed successfully." -ForegroundColor Green
    Write-Host ""
    Write-Information "Build:Build:Completed:Success=True" -InformationAction Continue
}

# Step 3: Lint (optional, enabled with -Lint, defaults to None)
if ($Lint -eq 'View') {
    Write-Host "Checking for linting issues..." -ForegroundColor Cyan
    Write-Host ""
    Write-Information "Build:Lint:Started:Action=View:Solution=$slnPath" -InformationAction Continue

    dotnet format $slnPath --verify-no-changes --verbosity $Verbosity

    if ($LASTEXITCODE -ne 0) {
        Write-Host "Linting issues found. Use -Lint Fix to auto-fix them." -ForegroundColor Yellow
        Write-Host ""
        Write-Information "Build:Lint:View:Completed:IssuesFound=True:ExitCode=$LASTEXITCODE" -InformationAction Continue
    }
    else {
        Write-Host "No linting issues found." -ForegroundColor Green
        Write-Host ""
        Write-Information "Build:Lint:View:Completed:IssuesFound=False" -InformationAction Continue
    }
}
elseif ($Lint -eq 'Fix') {
    if (Test-BuildOutput -RepoRoot $repoRoot -Configuration $Configuration -Operation "Lint fix") {
        Write-Host "Auto-fixing linting issues..." -ForegroundColor Cyan
        Write-Host ""
        Write-Information "Build:Lint:Started:Action=Fix:Solution=$slnPath" -InformationAction Continue

        dotnet format $slnPath --verbosity $Verbosity

        if ($LASTEXITCODE -ne 0) {
            Write-Host "Lint fix completed with warnings or errors." -ForegroundColor Yellow
            Write-Host ""
            Write-Information "Build:Lint:Fix:Completed:Success=False:ExitCode=$LASTEXITCODE" -InformationAction Continue
        }
        else {
            Write-Host "Lint fix (if any) completed successfully." -ForegroundColor Green
            Write-Host ""
            Write-Information "Build:Lint:Fix:Completed:Success=True" -InformationAction Continue
        }
    }
}

# Step 4: Test (optional, enabled with -Test)
if ($Test) {
    if (Test-BuildOutput -RepoRoot $repoRoot -Configuration $Configuration -Operation "Test") {
        Write-Host "Running tests..." -ForegroundColor Cyan
        Write-Host "Configuration: $Configuration" -ForegroundColor Gray
        Write-Host ""
        Write-Information "Build:Test:Started:Configuration=$Configuration:Solution=$slnPath" -InformationAction Continue

        $testArgs = @(
            'test',
            $slnPath,
            '--configuration', $Configuration,
            '--verbosity', $Verbosity,
            '--no-build'
        )

        & dotnet $testArgs

        if ($LASTEXITCODE -ne 0) {
            Write-Information "Build:Test:Failed:ExitCode=$LASTEXITCODE" -InformationAction Continue
            throw "Tests failed with exit code $LASTEXITCODE"
        }

        Write-Host "Tests completed successfully." -ForegroundColor Green
        Write-Host ""
        Write-Information "Build:Test:Completed:Success=True" -InformationAction Continue
    }
}

# Step 5: Publish (optional, enabled with -Publish)
if ($Publish) {
    if (Test-BuildOutput -RepoRoot $repoRoot -Configuration $Configuration -Operation "Publish") {
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
        Write-Information "Build:Publish:Started:Configuration=$Configuration:Project=$csprojPath:Output=$outputDir" -InformationAction Continue

        dotnet publish $csprojPath --configuration $Configuration --verbosity $Verbosity --output $outputDir

        if ($LASTEXITCODE -ne 0) {
            Write-Information "Build:Publish:Failed:ExitCode=$LASTEXITCODE" -InformationAction Continue
            throw "Publish failed with exit code $LASTEXITCODE"
        }

        Write-Host "Publish completed successfully." -ForegroundColor Green
        Write-Information "Build:Publish:Completed:Success=True" -InformationAction Continue
    }
}

Write-Host "All requested operations completed." -ForegroundColor Green
Write-Information "Build:AllOperations:Completed" -InformationAction Continue
