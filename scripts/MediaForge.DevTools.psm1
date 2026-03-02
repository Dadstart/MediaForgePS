<#
.SYNOPSIS
    Shared helper functions for MediaForgePS development scripts.

.DESCRIPTION
    Provides common helpers for:
    - Repository root discovery
    - Target framework discovery
    - External command validation
    - Locating build output for the MediaForgePS module
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Get-RepoRoot {
    [CmdletBinding()]
    param()

    # Prefer git from the current script directory when available
    if ($PSCommandPath) {
        $repoRoot = git -C (Split-Path -Parent $PSCommandPath) rev-parse --show-toplevel 2>$null
    } else {
        $repoRoot = git rev-parse --show-toplevel 2>$null
    }

    if (-not $repoRoot) {
        throw "Failed to determine repository root. Make sure you're in a git repository."
    }

    return $repoRoot.Trim()
}

function Assert-CommandAvailable {
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

function Get-MediaForgeTargetFramework {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot
    )

    $sharedPropsPath = Join-Path $RepoRoot 'Shared.props'
    if (-not (Test-Path -LiteralPath $sharedPropsPath -PathType Leaf)) {
        # Fallback to the default used previously
        return 'net9.0'
    }

    try {
        $xml = [xml](Get-Content -LiteralPath $sharedPropsPath -Raw)
        $tfmNode = $xml.Project.PropertyGroup.TargetFramework
        if ($tfmNode -and $tfmNode.Trim()) {
            return $tfmNode.Trim()
        }
    } catch {
        Write-Verbose "Failed to read TargetFramework from Shared.props. Falling back to net9.0. $_"
    }

    return 'net9.0'
}

function Get-MediaForgeBuildOutput {
    [CmdletBinding()]
    param(
        [Parameter(Mandatory)]
        [string]$RepoRoot,

        [Parameter(Mandatory)]
        [ValidateSet('Debug', 'Release')]
        [string]$Configuration
    )

    $tfm = Get-MediaForgeTargetFramework -RepoRoot $RepoRoot
    $moduleRoot = Join-Path -Path $RepoRoot -ChildPath 'src' -AdditionalChildPath @('MediaForgePS', 'bin', $Configuration, $tfm)
    return $moduleRoot
}

Export-ModuleMember -Function Get-RepoRoot, Assert-CommandAvailable, Get-MediaForgeTargetFramework, Get-MediaForgeBuildOutput

