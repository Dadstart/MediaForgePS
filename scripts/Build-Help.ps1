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
$repoRoot = Split-Path -Parent $PSScriptRoot
$docsPath = Join-Path $repoRoot 'src' 'MediaForgePS' 'docs'
$enUsPath = Join-Path $repoRoot 'src' 'MediaForgePS' 'en-US'

if (-not (Test-Path -LiteralPath $docsPath -PathType Container)) {
    Write-Error "Docs folder not found: $docsPath"
}

$platyPS = Get-Module -ListAvailable platyPS | Select-Object -First 1
if (-not $platyPS) {
    $platyPS = Import-Module platyPS -Scope CurrentUser
    if (-not $platyPS) {
        Write-Error "platyPS module is required. Error installing it. Try manually with: Install-Module platyPS -Scope CurrentUser"
    }
}

Write-Verbose "Docs path: $docsPath"
Write-Verbose "Output path: $enUsPath"

New-Item -ItemType Directory -Path $enUsPath -Force | Out-Null
New-ExternalHelp -Path $docsPath -OutputPath $enUsPath

$outFile = Join-Path $enUsPath 'MediaForgePS-help.xml'
if (Test-Path -LiteralPath $outFile -PathType Leaf) {
    Write-Host "Help file generated: $outFile" -ForegroundColor Green
} else {
    Write-Warning "Expected output file not found: $outFile"
}
