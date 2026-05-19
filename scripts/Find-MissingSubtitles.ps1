#!/usr/bin/env pwsh
<#
.SYNOPSIS
    Finds .mp4 files that are missing a matching .srt subtitle file.

.DESCRIPTION
    Recursively scans a directory for .mp4 files and reports the ones that do
    not have a matching .srt file alongside them.

    Matching modes:
      - SameBaseName (default): an .mp4 is considered to have subtitles when an
        .srt with the same base name exists in the same folder
        (e.g. "Movie.mp4" pairs with "Movie.srt" or "Movie.en.srt").
      - AnyInFolder: any .srt file in the same folder satisfies the requirement.

.PARAMETER Path
    Folder to scan. Defaults to the current location.

.PARAMETER MatchMode
    How to decide whether a subtitle exists for an .mp4. Defaults to SameBaseName.

.PARAMETER PassThru
    Emit objects for every .mp4 (with a HasSubtitles flag) instead of only the
    missing ones. Useful for piping to Export-Csv or further filtering.

.EXAMPLE
    .\scripts\Find-MissingSubtitles.ps1 -Path 'D:\Videos'
    Lists every .mp4 under D:\Videos that has no matching .srt.

.EXAMPLE
    .\scripts\Find-MissingSubtitles.ps1 -Path 'D:\Videos' -MatchMode AnyInFolder
    Treats any .srt in the same folder as a valid subtitle.

.EXAMPLE
    .\scripts\Find-MissingSubtitles.ps1 -Path 'D:\Videos' -PassThru |
        Export-Csv subtitles-report.csv -NoTypeInformation
    Writes a full report of every video and whether it has subtitles.
#>
[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateNotNullOrEmpty()]
    [string]$Path = (Get-Location).Path,

    [Parameter()]
    [ValidateSet('SameBaseName', 'AnyInFolder')]
    [string]$MatchMode = 'SameBaseName',

    [Parameter()]
    [switch]$PassThru
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $Path -PathType Container)) {
    throw "Path not found or not a directory: $Path"
}

$resolvedRoot = (Resolve-Path -LiteralPath $Path).Path

Write-Verbose "Scanning '$resolvedRoot' for .mp4 files (MatchMode: $MatchMode)"

$mp4Files = Get-ChildItem -LiteralPath $resolvedRoot -Filter *.mp4 -File -Recurse -Depth 1

# Group .srt files by directory once so we don't hit the disk per video.
$srtByDir = @{}
foreach ($srt in (Get-ChildItem -LiteralPath $resolvedRoot -Filter *.srt -File -Recurse)) {
    $dir = $srt.DirectoryName
    if (-not $srtByDir.ContainsKey($dir)) {
        $srtByDir[$dir] = [System.Collections.Generic.List[System.IO.FileInfo]]::new()
    }
    $srtByDir[$dir].Add($srt)
}

function Test-HasSubtitles {
    param(
        [System.IO.FileInfo]$Mp4,
        [string]$Mode
    )

    if (-not $srtByDir.ContainsKey($Mp4.DirectoryName)) {
        return $false
    }

    $candidates = $srtByDir[$Mp4.DirectoryName]
    if ($Mode -eq 'AnyInFolder') {
        return $candidates.Count -gt 0
    }

    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($Mp4.Name)
    foreach ($srt in $candidates) {
        $srtBase = [System.IO.Path]::GetFileNameWithoutExtension($srt.Name)
        # Match "Movie.srt" exactly, or language variants like "Movie.en.srt".
        if ($srtBase -eq $baseName -or $srtBase -like "$baseName.*") {
            return $true
        }
    }

    return $false
}

$results = foreach ($mp4 in $mp4Files) {
    $hasSubs = Test-HasSubtitles -Mp4 $mp4 -Mode $MatchMode
    [pscustomobject]@{
        FullName      = $mp4.FullName
        Folder        = $mp4.DirectoryName
        FileName      = $mp4.Name
        HasSubtitles  = $hasSubs
        SizeBytes     = $mp4.Length
        LastWriteTime = $mp4.LastWriteTime
    }
}

if ($PassThru) {
    $results
}
else {
    $missing = $results | Where-Object { -not $_.HasSubtitles }
    Write-Verbose ("Scanned {0} .mp4 file(s); {1} missing subtitles." -f $results.Count, $missing.Count)
    $missing | Select-Object FullName, Folder, FileName, SizeBytes, LastWriteTime
}
