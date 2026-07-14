---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Invoke-BonusFileProcessing

## SYNOPSIS
Converts bonus MKV files, extracts subtitles, and organizes them into Plex-style bonus content folders.

## SYNTAX

```
Invoke-BonusFileProcessing [-InputPath] <String> [-OutputPath] <String> [-DefaultVideoEncoder <String>]
 [-SkipSubtitles] [-Ocr <String>] [-SkipRepair] [-BackupPath <String>] [-ThrottleLimit <Int32>]
 [-ProgressAction <ActionPreference>] [-WhatIf] [-Confirm] [<CommonParameters>]
```

## DESCRIPTION
Invoke-BonusFileProcessing does three steps: (1) Converts bonus MKV files in `-InputPath` (files whose names end with `-behindthescenes`, `-deleted`, `-featurette`, `-interview`, `-scene`, `-short`, `-trailer`, or `-other`) using encoder defaults from `-DefaultVideoEncoder` (default **nvenc**). (2) Unless `-SkipSubtitles` is specified, extracts English subtitle streams from each bonus MKV. Use `-Ocr` (**Auto**, **Skip**, or **Force**; default Auto) to control image subtitle OCR; OCR-produced SRT files are repaired by default unless `-SkipRepair` is specified. (3) Organizes converted `.mp4` and matching subtitle files (`.srt`, `.vtt`) into Plex bonus folders under `-OutputPath`.

Source files are moved via copy-then-delete. If a destination file already exists, that file is skipped. Writes a MediaConversionResult per converted file (output path, size reduction, and processing time). Supports -WhatIf and -Confirm.

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

### Example 3: Extract subtitles with forced OCR and repair
```powershell
Invoke-BonusFileProcessing -InputPath "C:\Extras\Movie" -OutputPath "P:\Movies\Movie" -Ocr Force
```

Converts bonus files, extracts subtitles, converts all image-based (SUP/SUB) subtitles to SRT via OCR, repairs OCR-produced SRT files, then organizes into Plex folders. Use `-SkipRepair` to skip repair; use `-SkipSubtitles` to skip subtitle extraction entirely.

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
Default value: nvenc
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipSubtitles
Skip extracting subtitles from bonus files. By default, English subtitle streams are extracted from each bonus MKV before organization.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -SkipRepair
Skip the SRT repair step. By default, extracted or OCR-produced SRT files are repaired unless this switch is specified.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: False
Accept pipeline input: False
Accept wildcard characters: False
```

### -BackupPath
Directory to copy SRT files to before repairing. Path structure under the input directory is preserved. Only used when repair runs (i.e. when -SkipRepair is not specified and there are SRT files).

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -ThrottleLimit
Maximum number of image-to-SRT conversions to run in parallel when -Ocr is specified. Default is 10.

```yaml
Type: Int32
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: 10
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
Destination root for Plex bonus folders. Converted and matching SRT files are moved into subfolders (Behind The Scenes, Trailers, etc.).

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

### -WhatIf
Shows what would happen if the cmdlet runs.
The cmdlet is not run.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: wi

Required: False
Position: Named
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Confirm
Prompts you for confirmation before running the cmdlet.

```yaml
Type: SwitchParameter
Parameter Sets: (All)
Aliases: cf

Required: False
Position: Named
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

### -Ocr
Controls OCR of image-based subtitles after extraction. Default is Auto.

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: Named
Default value: Auto
Accept pipeline input: False
Accept wildcard characters: False
```

### CommonParameters
This cmdlet supports the common parameters: -Debug, -ErrorAction, -ErrorVariable, -InformationAction, -InformationVariable, -OutVariable, -OutBuffer, -PipelineVariable, -Verbose, -WarningAction, and -WarningVariable. For more information, see [about_CommonParameters](http://go.microsoft.com/fwlink/?LinkID=113216).

## INPUTS

### None
Parameters are specified directly.

## OUTPUTS

### MediaConversionResult
One object per converted bonus file: InputPath, OutputPath, Status, InputSizeBytes, OutputSizeBytes, SizeReductionPercent, and ProcessingTime. Status is `Success` when conversion completed.

## NOTES
Bonus suffixes: behindthescenes, deleted, featurette, interview, scene, short, trailer, other. Requires FFmpeg. Subtitle extraction uses FFmpeg (and mkvextract from mkvtoolnix for DVD subtitle streams). When OCR processing is enabled, Subtitle Edit and Tesseract are required on Windows.

## RELATED LINKS

[Convert-MediaFiles](Convert-MediaFiles.md)
[Export-Subtitles](Export-Subtitles.md)
