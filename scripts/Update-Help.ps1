#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Updates Markdown help from the built module and publishes to MediaForgePS.dll-Help.xml.

.DESCRIPTION
    Syncs platyPS Markdown in src/MediaForgePS/docs with the current cmdlets, then
    generates the MAML help file. Use this after adding cmdlets or changing parameters.

    Steps:
    1. Build the project so the module is loadable.
    2. Update existing .md files to match current parameters (Update-MarkdownHelp).
    3. Add new .md files for any cmdlets that do not yet have docs (New-MarkdownHelp).
    4. Generate MediaForgePS.dll-Help.xml in en-US from the Markdown (New-ExternalHelp).

    Requires the platyPS module. Paths are relative to the repository root.

.EXAMPLE
    .\scripts\Update-Help.ps1

    Builds, syncs docs with the module, and regenerates en-US\MediaForgePS.dll-Help.xml.
#>

[CmdletBinding()]
param(
    # Skip dotnet build; use when the project is already built.
    [switch] $SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$docsPath = Join-Path $repoRoot 'src' 'MediaForgePS' 'docs'
$enUsPath = Join-Path $repoRoot 'src' 'MediaForgePS' 'en-US'
$helpXmlName = 'MediaForgePS.dll-Help.xml'

if (-not (Test-Path -LiteralPath $docsPath -PathType Container)) {
    Write-Error "Docs folder not found: $docsPath"
}

# Ensure platyPS is available
$null = Get-Module -ListAvailable platyPS | Select-Object -First 1
if (-not (Get-Module -ListAvailable platyPS)) {
    try {
        Import-Module platyPS -Scope CurrentUser -ErrorAction Stop
    } catch {
        Write-Error "platyPS module is required. Install with: Install-Module platyPS -Scope CurrentUser"
    }
}

# Step 1: Build
if (-not $SkipBuild) {
    Write-Host 'Building project...' -ForegroundColor Cyan
    Push-Location $repoRoot
    try {
        dotnet build --nologo -v q
        if ($LASTEXITCODE -ne 0) {
            Write-Error "Build failed with exit code $LASTEXITCODE"
        }
    } finally {
        Pop-Location
    }
}

# Locate built module (e.g. bin/Debug/net9.0 or bin/Release/net9.0)
$binBase = Join-Path $repoRoot 'src' 'MediaForgePS' 'bin'
$frameworkDir = Get-ChildItem -Path $binBase -Recurse -Directory -Filter 'net*' -ErrorAction SilentlyContinue |
    Where-Object { Test-Path (Join-Path $_.FullName 'MediaForgePS.psd1') } |
    Select-Object -First 1

if (-not $frameworkDir) {
    Write-Error "Built module not found under $binBase. Run 'dotnet build' or call this script without -SkipBuild."
}

$modulePath = $frameworkDir.FullName
Write-Verbose "Using module path: $modulePath"

$moduleScriptPath = Join-Path $modulePath "MediaForgePS.psd1"
if (-not (Test-Path $moduleScriptPath)) {
    Write-Error "Module script not found at: $moduleScriptPath"
}

# Step 2: Load the built module and update Markdown
Write-Host 'Loading MediaForgePS and updating Markdown help...' -ForegroundColor Cyan
Import-Module $moduleScriptPath -Force

# Refresh existing .md to match current cmdlet parameters and syntax
Update-MarkdownHelp -Path $docsPath

# Add .md for any cmdlets that do not yet have a file (does not overwrite existing)
New-MarkdownHelp -Module MediaForgePS -OutputFolder $docsPath

# Step 3: Publish to en-US XML
Write-Host "Generating $helpXmlName..." -ForegroundColor Cyan
New-Item -ItemType Directory -Path $enUsPath -Force | Out-Null
New-ExternalHelp -Path $docsPath -OutputPath $enUsPath

# platyPS may write MediaForgePS-help.xml; normalize to MediaForgePS.dll-Help.xml for the module
$altName = 'MediaForgePS-help.xml'
$altFile = Join-Path $enUsPath $altName
$outFile = Join-Path $enUsPath $helpXmlName
if ((Test-Path -LiteralPath $altFile -PathType Leaf) -and $altName -ne $helpXmlName) {
    Move-Item -LiteralPath $altFile -Destination $outFile -Force
}

if (Test-Path -LiteralPath $outFile -PathType Leaf) {
    Write-Host "Help published: $outFile" -ForegroundColor Green
} else {
    $generated = Get-ChildItem -Path $enUsPath -Filter '*.xml' -File -ErrorAction SilentlyContinue
    if ($generated) {
        Write-Warning "Expected $helpXmlName not found; XML file(s) in en-US: $($generated.Name -join ', ')"
    } else {
        Write-Warning "No help XML was generated in $enUsPath"
    }
}

# Remove the module from the session to release the DLL locks
Remove-Module -Name MediaForgePS -Force