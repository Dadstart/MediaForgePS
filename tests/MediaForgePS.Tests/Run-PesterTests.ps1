#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Runs Pester tests for MediaForgePS and ensures the module is removed afterward.

.DESCRIPTION
    This script runs all Pester tests in the PowerShell test directory and ensures
    that the MediaForgePS module is properly unloaded after the tests complete,
    regardless of whether the tests pass or fail.

.PARAMETER Path
    Path to the PowerShell test directory. Defaults to the PowerShell subdirectory
    relative to this script.

.PARAMETER Configuration
    Path to the Pester configuration file. Defaults to PesterConfig.psd1 in the
    same directory as this script.

.PARAMETER IsolatedProcess
    Run tests in a separate PowerShell process to ensure DLL locks are released.
    This is recommended to avoid file lock issues during builds. Defaults to $true.

.EXAMPLE
    .\Run-PesterTests.ps1

.EXAMPLE
    .\Run-PesterTests.ps1 -Path ".\PowerShell" -Configuration ".\PesterConfig.psd1"

.EXAMPLE
    .\Run-PesterTests.ps1 -IsolatedProcess:$false
    Run tests in the current process (may leave DLL locked)
#>
[CmdletBinding()]
param(
    [Parameter()]
    [string]$Path = (Join-Path $PSScriptRoot "PowerShell"),

    [Parameter()]
    [string]$Configuration = (Join-Path $PSScriptRoot "PesterConfig.psd1"),

    [Parameter()]
    [switch]$IsolatedProcess = $true
)

$ErrorActionPreference = 'Stop'

# If running in isolated process, spawn a new PowerShell process
if ($IsolatedProcess) {
    Write-Host "Running tests in isolated PowerShell process to ensure DLL cleanup..." -ForegroundColor Cyan
    Write-Host ""
    
    $scriptPath = $PSCommandPath
    $arguments = @(
        '-NoProfile',
        '-File',
        "`"$scriptPath`"",
        '-Path', "`"$Path`"",
        '-Configuration', "`"$Configuration`"",
        '-IsolatedProcess:$false'
    )
    
    $process = Start-Process -FilePath (Get-Command pwsh).Source -ArgumentList $arguments -Wait -PassThru -NoNewWindow
    
    exit $process.ExitCode
}

try {
    # Ensure Pester is available
    if (-not (Get-Module -ListAvailable -Name Pester)) {
        Write-Host "Installing Pester module..." -ForegroundColor Yellow
        Install-Module -Name Pester -Force -SkipPublisherCheck -Scope CurrentUser
    }

    Import-Module Pester -MinimumVersion 5.0 -ErrorAction Stop

    Write-Host "Running Pester tests..." -ForegroundColor Cyan
    Write-Host "Test Path: $Path" -ForegroundColor Gray
    Write-Host "Configuration: $Configuration" -ForegroundColor Gray
    Write-Host ""

    # Load the Pester configuration
    if (Test-Path $Configuration) {
        $config = Import-PowerShellDataFile -Path $Configuration
        $pesterConfig = New-PesterConfiguration -Hashtable $config
        $pesterConfig.Run.Path = $Path
    }
    else {
        Write-Warning "Configuration file not found at '$Configuration'. Using default configuration."
        $pesterConfig = New-PesterConfiguration
        $pesterConfig.Run.Path = $Path
        $pesterConfig.Output.Verbosity = 'Detailed'
    }

    # Run the tests
    $testResult = Invoke-Pester -Configuration $pesterConfig

    # Return the exit code based on test results
    if ($testResult.FailedCount -gt 0) {
        $exitCode = 1
    }
    else {
        $exitCode = 0
    }
}
catch {
    Write-Error "Failed to run Pester tests: $_"
    $exitCode = 1
}
finally {
    # Always remove the module, even if tests fail
    Write-Host ""
    Write-Host "Cleaning up module..." -ForegroundColor Cyan
    
    # Function to aggressively remove the module and release DLL locks
    function Remove-ModuleCompletely {
        param([string]$ModuleName)
        
        $removed = $false
        
        # Try multiple times to remove all instances
        for ($i = 0; $i -lt 5; $i++) {
            $modules = Get-Module -Name $ModuleName -ErrorAction SilentlyContinue
            if (-not $modules) {
                $removed = $true
                break
            }
            
            foreach ($module in $modules) {
                try {
                    # Remove exported functions and aliases (not cmdlets - those are handled by Remove-Module)
                    if ($module.ExportedFunctions) {
                        $module.ExportedFunctions.Values | ForEach-Object {
                            Remove-Item "Function:\$($_.Name)" -ErrorAction SilentlyContinue
                        }
                    }
                    if ($module.ExportedAliases) {
                        $module.ExportedAliases.Values | ForEach-Object {
                            Remove-Item "Alias:\$($_.Name)" -ErrorAction SilentlyContinue
                        }
                    }
                    
                    Remove-Module -ModuleInfo $module -Force -ErrorAction Stop
                    $removed = $true
                }
                catch {
                    Write-Verbose "Failed to remove module on attempt $($i + 1): $_"
                }
            }
            
            # Force garbage collection to release assembly locks
            [System.GC]::Collect()
            [System.GC]::WaitForPendingFinalizers()
            [System.GC]::Collect()
            
            # Small delay to allow file system to release locks
            Start-Sleep -Milliseconds 100
        }
        
        return $removed
    }
    
    $removed = Remove-ModuleCompletely -ModuleName 'MediaForgePS'
    
    if ($removed) {
        Write-Host "MediaForgePS module removed successfully." -ForegroundColor Green
        
        # Additional wait to allow file system to release locks
        Write-Host "Waiting for file system to release DLL locks..." -ForegroundColor Gray
        Start-Sleep -Milliseconds 200
    }
    else {
        Write-Warning "MediaForgePS module may still be loaded."
        Write-Warning "If you encounter DLL lock errors during build, try:"
        Write-Warning "  1. Close this PowerShell session and open a new one"
        Write-Warning "  2. Or run tests in a separate PowerShell process"
    }
}

exit $exitCode

