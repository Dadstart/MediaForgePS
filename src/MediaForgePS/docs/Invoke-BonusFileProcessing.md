---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Invoke-BonusFileProcessing

## SYNOPSIS
Converts bonus MKV files and organizes them into Plex-style bonus content folders.

## SYNTAX

```
Invoke-BonusFileProcessing [-InputPath] <String> [-OutputPath] <String> [-DefaultVideoEncoder <String>]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Invoke-BonusFileProcessing does two steps: (1) Converts bonus MKV files in -InputPath (files whose names end with -behindthescenes, -deleted, -featurette, -interview, -scene, -short, -trailer, or -other) using the same encoder defaults as Convert-MediaFiles (-DefaultVideoEncoder: x264, x265, or nvenc). (2) Organizes the converted .mp4 and matching .srt files into Plex bonus folders (Behind The Scenes, Deleted Scenes, Featurettes, etc.) under -OutputPath. On Windows, -OutputPath must be under the P:\ drive. Source files are moved (copied then deleted) into the Plex folder structure.

## EXAMPLES

### Example 1: Process bonus folder to Plex location
```powershell
Invoke-BonusFileProcessing -InputPath "C:\Extras\Movie" -OutputPath "P:\Movies\Movie" -DefaultVideoEncoder nvenc
```

Converts bonus MKV files in C:\Extras\Movie and moves them into P:\Movies\Movie in Plex bonus subfolders.

### Example 2: Use x265 for conversion
```powershell
Invoke-BonusFileProcessing -InputPath "D:\Bonus" -OutputPath "P:\Movies\Title" -DefaultVideoEncoder x265
```

Converts bonus files with libx265 and organizes under P:\Movies\Title.

## PARAMETERS

### -DefaultVideoEncoder
Encoder to use for converting bonus MKV files: x264 (libx264), x265 (libx265), or nvenc (NVENC HEVC). Default is nvenc.

```yaml
Type: String
Parameter Sets: (All)
Aliases:
Accepted values: x264, x265, nvenc

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -InputPath
Source directory containing bonus MKV files (and optionally SRT). Only files with bonus suffixes (e.g. -trailer, -behindthescenes) are converted and moved.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 0
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -OutputPath
Destination root for Plex bonus folders. On Windows must be under P:\. Converted and matching SRT files are moved into subfolders (Behind The Scenes, Trailers, etc.).

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: True
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ProgressAction
Specifies how the cmdlet responds to progress updates. Use SilentlyContinue to hide progress.

```yaml
Type: ActionPreference
Parameter Sets: (All)
Aliases: proga

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
Parameters are specified directly.

## OUTPUTS

### None
This cmdlet does not write to the pipeline.

## NOTES
Bonus suffixes: behindthescenes, deleted, featurette, interview, scene, short, trailer, other. Requires FFmpeg. On Windows, output must be under P:\.

## RELATED LINKS
[Convert-MediaFiles](Convert-MediaFiles.md)
