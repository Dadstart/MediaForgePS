#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Generates the MAML external help file from platyPS Markdown (step 5 of the help workflow).

.DESCRIPTION
    Runs New-ExternalHelp to produce MediaForgePS-help.xml in src/MediaForgePS/en-US from
    the Markdown files in src/MediaForgePS/docs. Requires the platyPS module. Paths are
    resolved relative to the repository root (parent of the scripts folder).

.EXAMPLE
    .\scripts\Build-Help.ps1

    Generates en-US\MediaForgePS-help.xml from the docs folder.
#>

[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

Import-Module (Join-Path $PSScriptRoot 'MediaForge.DevTools.psm1') -Force

$repoRoot = Get-RepoRoot
$docsPath = Join-Path $repoRoot 'src' 'MediaForgePS' 'docs'
$enUsPath = Join-Path $repoRoot 'src' 'MediaForgePS' 'en-US'

if (-not (Test-Path -LiteralPath $docsPath -PathType Container)) {
    Write-Error "Docs folder not found: $docsPath"
}

if (-not (Get-Module -ListAvailable -Name platyPS)) {
    Write-Host "platyPS not found. Installing for CurrentUser..." -ForegroundColor Yellow
    try {
        Install-Module platyPS -Scope CurrentUser -Force -ErrorAction Stop
    } catch {
        Write-Error "platyPS module is required. Install manually with: Install-Module platyPS -Scope CurrentUser"
    }
}

Import-Module platyPS -ErrorAction Stop

Write-Verbose "Docs path: $docsPath"
Write-Verbose "Output path: $enUsPath"

New-Item -ItemType Directory -Path $enUsPath -Force | Out-Null
New-ExternalHelp -Path $docsPath -OutputPath $enUsPath

$canonicalName = 'MediaForgePS.dll-Help.xml'
$legacyName = 'MediaForgePS-help.xml'

$legacyPath = Join-Path $enUsPath $legacyName
$canonicalPath = Join-Path $enUsPath $canonicalName

if ((Test-Path -LiteralPath $legacyPath -PathType Leaf) -and -not (Test-Path -LiteralPath $canonicalPath -PathType Leaf)) {
    Move-Item -LiteralPath $legacyPath -Destination $canonicalPath -Force
}

if (Test-Path -LiteralPath $canonicalPath -PathType Leaf) {
    Write-Host "Help file generated: $canonicalPath" -ForegroundColor Green
} else {
    Write-Warning "Expected output file not found: $canonicalPath"
}
