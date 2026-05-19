---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Convert-VideoFile

## SYNOPSIS
Converts MKV files in a directory (or specified paths) to MP4 with automatic audio mapping and optional caption extraction.

## SYNTAX

```
Convert-VideoFile [-InputPath] <String[]> [[-OutputDirectory] <String>] [-Recurse]
 [-DefaultVideoEncoder <String>] [-X265Params <String>] [-SkipSubtitles] [-SkipOcr] [-SkipRepair]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Convert-VideoFile is the primary batch conversion cmdlet for MKV sources. For each input it auto-detects audio track mappings, converts to MP4 in the output directory (same relative path layout as the input root), and writes VideoFileConversionResult objects to the pipeline.

Unless -SkipSubtitles is specified, after successful conversions the cmdlet extracts English subtitle streams from each source MKV next to the output MP4 (using mkvextract). Unless -SkipOcr is specified, image-based captions (SUP, SUB) are converted to SRT via OCR and SRT files are repaired by default; use -SkipRepair to skip only the repair step. -SkipOcr has no effect when -SkipSubtitles is set.

Default video encoding follows -DefaultVideoEncoder: x264 (libx264, CRF 18, preset medium), x265 (libx265, CRF 18, preset medium), or nvenc (hevc_nvenc, CQ 18, preset p5). The default encoder is nvenc. Pass -X265Params for extra x265 options when using x265.

InputPath accepts a directory, a single MKV file, or multiple MKV paths. Aliases: InputDirectory, Path. When -OutputDirectory is omitted, output is written alongside each input (same directory as the source file or input root). Use -Recurse to include MKV files in subdirectories when InputPath is a directory.

Progress reporting includes per-file and batch ETA based on completed file sizes.

## EXAMPLES

### Example 1: Convert all MKV files in a folder with NVENC (default)
```powershell
Convert-VideoFile -InputPath "C:\Videos\Season1"
```

Converts each .mkv under C:\Videos\Season1 to .mp4 in the same folder, extracts English captions, and runs OCR plus SRT repair on image-based tracks.

### Example 2: Convert to a separate output directory with x265
```powershell
Convert-VideoFile -InputPath "C:\Source" -OutputDirectory "C:\Out" -DefaultVideoEncoder x265 -Recurse
```

Converts all MKV files under C:\Source (including subfolders) to C:\Out preserving relative paths, using libx265 defaults.

### Example 3: Convert without caption extraction
```powershell
Convert-VideoFile -InputPath "D:\movie.mkv" -SkipSubtitles
```

Converts only the video and audio; skips subtitle extraction and OCR.

### Example 4: Convert and extract captions without OCR or repair
```powershell
Convert-VideoFile -InputPath "C:\In" -SkipOcr
```

Extracts English subtitle sidecars but does not convert SUP/SUB to SRT or repair SRT files.

## PARAMETERS

### -DefaultVideoEncoder
Default encoder: x264 (libx264), x265 (libx265), or nvenc (NVENC HEVC). Default is nvenc.

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

### -InputPath
Directory containing MKV files, a single MKV file path, or an array of MKV file paths.

```yaml
Type: String[]
Parameter Sets: (All)
Aliases: InputDirectory, Path

Required: True
Position: 0
Default value: None
Accept pipeline input: True (ByPropertyName, ByValue)
Accept wildcard characters: False
```

### -OutputDirectory
Directory where converted files are written. Defaults to the input location (same folder as each source file).

```yaml
Type: String
Parameter Sets: (All)
Aliases:

Required: False
Position: 1
Default value: None
Accept pipeline input: False
Accept wildcard characters: False
```

### -Recurse
When InputPath is a directory, include MKV files in subdirectories.

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

### -SkipOcr
Skip OCR conversion of image captions (SUP, SUB) to SRT. Has no effect when -SkipSubtitles is specified.

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
Skip SRT repair during default OCR processing. Has no effect when -SkipOcr or -SkipSubtitles is specified.

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

### -SkipSubtitles
Skip caption extraction after converting MKV files.

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

### -X265Params
Additional x265 params passed to ffmpeg via -x265-params. Applies when -DefaultVideoEncoder is x265.

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

### System.String[]
Directory path, MKV file path(s), or piped paths. Aliases: InputDirectory, Path.

## OUTPUTS

### VideoFileConversionResult
For each processed file: InputPath, OutputPath, Success, Status.

## NOTES
Requires FFmpeg and ffprobe. Caption extraction requires mkvextract. OCR requires Subtitle Edit (under %ProgramFiles%\Subtitle Edit) and Tesseract when -SkipOcr is not specified. This cmdlet is aliased as `convert` and `mkv` when using scripts/Launch.ps1.

## RELATED LINKS

[Convert-MediaFiles](Convert-MediaFiles.md)
[Export-Subtitles](Export-Subtitles.md)
[Get-AudioStreams](Get-AudioStreams.md)
[Invoke-SubtitleOcrRepair](Invoke-SubtitleOcrRepair.md)
