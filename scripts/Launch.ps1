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
    .\scripts\Launch-MediaForgePS.ps1
    Launches a new PowerShell instance with the Debug build of MediaForgePS imported.

.EXAMPLE
    .\scripts\Launch-MediaForgePS.ps1 -Configuration Release
    Launches a new PowerShell instance with the Release build of MediaForgePS imported.
#>
[CmdletBinding()]
param(
    [Parameter()]
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Debug'
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
    The name of the command to validate (e.g., 'git', 'pwsh').

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
Test-Command -CommandName 'pwsh'

# Determine repository root using git
$repoRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    throw "Failed to determine repository root. Make sure you're in a git repository."
}

$repoRoot = $repoRoot.Trim()

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
# Use -NoExit to keep the session open, and -File to execute the import script
$pwshPath = (Get-Command pwsh).Source
$importArgs = @(
    '-NoExit',
    '-File',
    $importScriptPath,
    '-Configuration', $Configuration
)
# Start-Process -FilePath $pwshPath -NoNewWindow -ArgumentList $importArgs

Write-Host "PowerShell instance launched successfully." -ForegroundColor Green
Write-Host "Check the new window for the Process ID (PID) to attach your debugger." -ForegroundColor Cyan

