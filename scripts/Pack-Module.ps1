#Requires -Version 7.6
<#
.SYNOPSIS
    Stages a PowerShell Gallery-ready MediaForgePS module folder and zip artifact.

.DESCRIPTION
    Copies the built module layout (manifest, script module, assemblies, help) into
    artifacts/MediaForgePS and compresses it to MediaForgePS.<version>.zip.
    This is the PS module packaging path; the C# project is not packed as a NuGet package.

.PARAMETER Configuration
    Build configuration folder to copy from (Debug or Release). Defaults to Release.

.PARAMETER RepoRoot
    Repository root. Defaults to the parent of the scripts directory.
#>
[CmdletBinding()]
param(
    [ValidateSet('Debug', 'Release')]
    [string]$Configuration = 'Release',

    [string]$RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

$manifestSource = Join-Path $RepoRoot 'src/MediaForgePS/MediaForgePS.psd1'
if (-not (Test-Path -LiteralPath $manifestSource)) {
    throw "Module manifest not found: $manifestSource"
}

$manifest = Import-PowerShellDataFile -Path $manifestSource
$moduleVersion = [string]$manifest.ModuleVersion
if ([string]::IsNullOrWhiteSpace($moduleVersion)) {
    throw "ModuleVersion missing from $manifestSource"
}

$buildOutput = Join-Path $RepoRoot "src/MediaForgePS/bin/$Configuration/net10.0"
if (-not (Test-Path -LiteralPath (Join-Path $buildOutput 'MediaForgePS.dll'))) {
    throw "Build output not found at $buildOutput. Build the module first (e.g. Build.ps1 -Build -Configuration $Configuration)."
}

$artifactsRoot = Join-Path $RepoRoot 'artifacts'
$moduleRoot = Join-Path $artifactsRoot 'MediaForgePS'
$zipPath = Join-Path $artifactsRoot "MediaForgePS.$moduleVersion.zip"

Write-Host "Packing MediaForgePS $moduleVersion from $buildOutput" -ForegroundColor Cyan

if (Test-Path -LiteralPath $moduleRoot) {
    Remove-Item -LiteralPath $moduleRoot -Recurse -Force
}
if (Test-Path -LiteralPath $zipPath) {
    Remove-Item -LiteralPath $zipPath -Force
}

New-Item -ItemType Directory -Path $moduleRoot -Force | Out-Null

$requiredNames = @(
    'MediaForgePS.psd1'
    'MediaForgePS.psm1'
    'MediaForgePS.dll'
)

foreach ($name in $requiredNames) {
    $sourcePath = Join-Path $buildOutput $name
    if (-not (Test-Path -LiteralPath $sourcePath)) {
        throw "Required module file missing from build output: $sourcePath"
    }
    Copy-Item -LiteralPath $sourcePath -Destination (Join-Path $moduleRoot $name) -Force
}

$depsJson = Join-Path $buildOutput 'MediaForgePS.deps.json'
if (Test-Path -LiteralPath $depsJson) {
    Copy-Item -LiteralPath $depsJson -Destination (Join-Path $moduleRoot 'MediaForgePS.deps.json') -Force
}

# Ship only runtime dependency assemblies required by MediaForgePS (not PowerShell host / SDK).
$dependencyPrefixes = @(
    'Microsoft.Extensions.'
    'System.Security.Cryptography.Xml'
    'Microsoft.Bcl.AsyncInterfaces'
)

Get-ChildItem -LiteralPath $buildOutput -Filter '*.dll' -File |
    Where-Object {
        $name = $_.Name
        $dependencyPrefixes | Where-Object { $name.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }
    } |
    ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $moduleRoot $_.Name) -Force
    }

$helpSource = Join-Path $buildOutput 'en-US'
if (Test-Path -LiteralPath $helpSource) {
    Copy-Item -LiteralPath $helpSource -Destination (Join-Path $moduleRoot 'en-US') -Recurse -Force
}
else {
    Write-Warning "Help directory not found at $helpSource (module will pack without MAML help)."
}

$stagedManifest = Join-Path $moduleRoot 'MediaForgePS.psd1'
$null = Test-ModuleManifest -Path $stagedManifest -ErrorAction Stop

Compress-Archive -Path $moduleRoot -DestinationPath $zipPath -Force

Write-Host "Staged module: $moduleRoot" -ForegroundColor Green
Write-Host "Created package: $zipPath" -ForegroundColor Green
