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

$targetFramework = 'net9.0' # matches csproj
$moduleBaseName = 'MediaForgePS'

# Determine repository root using git
$repoRoot = git rev-parse --show-toplevel
if ($LASTEXITCODE -ne 0) {
    throw "Failed to determine repository root. Make sure you're in a git repository."
    }
    
$repoRoot = $repoRoot.Trim()
Write-Host "Repo root: $repoRoot"
# Construct path to the module
$modulePath = Join-Path -Path $repoRoot -ChildPath 'src' -AdditionalChildPath ($moduleBaseName, 'bin', $Configuration, $targetFramework)

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
    foreach ($fileExtension in ('dll', 'psm1')) {
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
    Write-Error "bad"
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

$Host.UI.RawUI.WindowTitle = "$moduleBaseName Debug Session - Configuration: $Configuration"
Write-Host ''
Write-Host '========================================' -ForegroundColor Cyan
Write-Host "$moduleBaseName Debug Session" -ForegroundColor Cyan
Write-Host '========================================' -ForegroundColor Cyan
Write-Host ''
Write-Host "Process ID (PID): $PID" -ForegroundColor Yellow
Write-Host "Configuration: $Configuration" -ForegroundColor Gray
Write-Host "Module Directory: $modulePath" -ForegroundColor Gray
Write-Host ''
Write-Host 'Attach your debugger to this process ID to begin debugging.' -ForegroundColor Green
Write-Host 'The session will remain open for interactive use.' -ForegroundColor Green
Write-Host ''
$moduleScriptPath = Join-Path $modulePath "$moduleBaseName.psm1"
Write-Host "Importing $moduleScriptPath"
Import-Module $moduleScriptPath
Write-Host "$moduleBaseName module imported successfully." -ForegroundColor Green
Write-Host ''
<#
#>
