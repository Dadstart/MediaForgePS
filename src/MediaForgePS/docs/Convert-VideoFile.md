---
external help file: MediaForgePS.dll-Help.xml
Module Name: MediaForgePS
online version:
schema: 2.0.0
---

# Convert-VideoFile

## SYNOPSIS
Converts video files in a directory (or specified paths) to MP4 with automatic audio mapping and optional caption extraction. Supports common container formats including MKV, MP4, MOV, AVI, WebM, and more.

## SYNTAX

```
Convert-VideoFile [-InputPath] <String[]> [[-OutputDirectory] <String>] [-Recurse]
 [-DefaultVideoEncoder <String>] [-X265Params <String>] [-SkipSubtitles] [-Ocr <String>] [-SkipRepair]
 [-ProgressAction <ActionPreference>] [<CommonParameters>]
```

## DESCRIPTION
Convert-VideoFile is the primary batch conversion cmdlet for video sources. For each input it auto-detects audio track mappings, converts to MP4 in the output directory (same relative path layout as the input root), and writes VideoFileConversionResult objects to the pipeline.

Supported input extensions: `.mkv`, `.mp4`, `.m4v`, `.mov`, `.avi`, `.wmv`, `.flv`, `.webm`, `.mpg`, `.mpeg`, `.ts`, `.m2ts`, `.mts`, `.vob`, `.ogv`, `.3gp`, `.asf`. Extension matching is case-insensitive. When -InputPath is a directory, only files with a supported extension are enumerated; other files are silently ignored. A single file passed via -InputPath must have a supported extension or the cmdlet emits an `InvalidInputPath` error.

Unless -SkipSubtitles is specified, after successful conversions the cmdlet extracts English subtitle streams next to the output MP4. For Matroska sources, VobSub (dvd_subtitle) tracks are extracted with `mkvextract`; all other codecs and all non-Matroska containers use `ffmpeg`. When falling back to `ffmpeg` for VobSub, both `.idx` and `.sub` companion files are produced.

Use `-Ocr` to control post-extraction OCR and repair of image-based captions (SUP, SUB). Accepted values are **Auto** (default), **Skip**, and **Force**:

- **Auto** - OCR image subtitles only when the source has a single exported subtitle format and it is not SRT.
- **Force** - OCR all exported image subtitle files.
- **Skip** - extract subtitles only; no OCR or repair.

When OCR runs, SRT files are repaired by default unless `-SkipRepair` is specified. `-Ocr` and `-SkipRepair` have no effect when `-SkipSubtitles` is set.

Default video encoding follows -DefaultVideoEncoder: x264 (libx264, CRF 18, preset medium), x265 (libx265, CRF 18, preset medium), or nvenc (hevc_nvenc, CQ 18, preset p5). The default encoder is nvenc. Pass -X265Params for extra x265 options when using x265.

InputPath accepts a directory, a single video file, or multiple video file paths. Aliases: InputDirectory, Path. When -OutputDirectory is omitted, output is written alongside each input (same directory as the source file or input root). Use -Recurse to include video files in subdirectories when InputPath is a directory.

Progress reporting includes per-file and batch ETA based on completed file sizes.

## EXAMPLES

### Example 1: Convert all video files in a folder with NVENC (default)
```powershell
Convert-VideoFile -InputPath "C:\Videos\Season1"
```

Converts each supported video file under C:\Videos\Season1 to .mp4 in the same folder and extracts English caption sidecars.

### Example 2: Convert to a separate output directory with x265
```powershell
Convert-VideoFile -InputPath "C:\Source" -OutputDirectory "C:\Out" -DefaultVideoEncoder x265 -Recurse
```

Converts all supported video files under C:\Source (including subfolders) to C:\Out preserving relative paths, using libx265 defaults.

### Example 3: Convert without caption extraction
```powershell
Convert-VideoFile -InputPath "D:\movie.mkv" -SkipSubtitles
```

Converts only the video and audio; skips subtitle extraction and OCR.

### Example 4: Convert and extract captions with OCR (default Auto mode)
```powershell
Convert-VideoFile -InputPath "C:\In" -Ocr Auto
```

Extracts English subtitle sidecars. Image-based tracks are converted to SRT via OCR when Auto conditions are met, and OCR-produced SRT files are repaired.

### Example 5: Force OCR on all image subtitles
```powershell
Convert-VideoFile -InputPath "C:\In" -Ocr Force
```

Converts all exported image subtitle files to SRT and repairs them.

### Example 6: Convert a non-Matroska source (e.g. MP4 or MOV)
```powershell
Convert-VideoFile -InputPath "D:\Cameras\clip.mov" -OutputDirectory "D:\Out"
```

Accepts any supported container as input. Subtitle extraction uses ffmpeg for non-Matroska sources.

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
Directory containing video files, a single video file path, or an array of video file paths. Supported extensions: `.mkv`, `.mp4`, `.m4v`, `.mov`, `.avi`, `.wmv`, `.flv`, `.webm`, `.mpg`, `.mpeg`, `.ts`, `.m2ts`, `.mts`, `.vob`, `.ogv`, `.3gp`, `.asf` (case-insensitive).

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
When InputPath is a directory, include video files in subdirectories.

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
Skip SRT repair during OCR processing. Has no effect when -Ocr is not specified or -SkipSubtitles is specified.

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
Skip caption extraction after converting video files.

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

### -Ocr
Controls OCR of image-based captions after extraction. Default is Auto.

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

### System.String[]
Directory path, video file path(s), or piped paths. Aliases: InputDirectory, Path.

## OUTPUTS

### VideoFileConversionResult
For each processed file: InputPath, OutputPath, Success, Status.

## NOTES
Requires FFmpeg and ffprobe. Caption extraction from Matroska (.mkv) sources with VobSub (dvd_subtitle) tracks additionally requires `mkvextract` (mkvtoolnix); non-Matroska sources and all other subtitle codecs are extracted via FFmpeg and do not require mkvextract. OCR requires Subtitle Edit (under %ProgramFiles%\Subtitle Edit) and Tesseract when `-Ocr` is Auto or Force. `Launch.ps1` defines convenience aliases `convert` and `mkv` for this cmdlet in dev sessions.

## RELATED LINKS

[Convert-MediaFiles](Convert-MediaFiles.md)
[Export-Subtitles](Export-Subtitles.md)
[Get-AudioStreams](Get-AudioStreams.md)
[Invoke-SubtitleOcrRepair](Invoke-SubtitleOcrRepair.md)
