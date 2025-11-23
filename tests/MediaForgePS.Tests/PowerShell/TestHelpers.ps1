# Shared test helper functions for Pester tests

<#
.SYNOPSIS
    Cleans up the MediaForgePS module after tests complete.

.DESCRIPTION
    Removes the MediaForgePS module and forces garbage collection to help
    release DLL locks. This should be called in an AfterAll block.
#>
function Remove-MediaForgePSModule {
    if (Get-Module -Name MediaForgePS -ErrorAction SilentlyContinue) {
        # Force garbage collection to help release DLL locks
        [System.GC]::Collect()
        [System.GC]::WaitForPendingFinalizers()
        
        Remove-Module -Name MediaForgePS -Force -ErrorAction SilentlyContinue
        
        # Additional cleanup pass
        [System.GC]::Collect()
        Start-Sleep -Milliseconds 50
    }
}

